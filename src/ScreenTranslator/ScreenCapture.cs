using System;
using System.Runtime.InteropServices;
using D3D = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;

namespace ScreenTranslator;

public enum FrameState { Timeout, Updated, Lost }

/// <summary>DXGI Desktop Duplication do monitor principal. Mantém a última imagem do desktop em GPU.</summary>
public sealed class ScreenCapture : IDisposable
{
    D3D.Device? dev;
    DXGI.OutputDuplication? dup;
    D3D.Texture2D? desktop, staging;
    int stW, stH;
    bool hasFrame;
    public int X, Y, Width, Height;
    public bool Ready => dup != null;

    public void Init()
    {
        Close();
        dev = new D3D.Device(SharpDX.Direct3D.DriverType.Hardware, D3D.DeviceCreationFlags.BgraSupport);
        using var dxgiDev = dev.QueryInterface<DXGI.Device>();
        using var adapter = dxgiDev.GetParent<DXGI.Adapter>();
        using var output = adapter.GetOutput(0);
        using var output1 = output.QueryInterface<DXGI.Output1>();
        var b = output.Description.DesktopBounds;
        X = b.Left; Y = b.Top;
        dup = output1.DuplicateOutput(dev);
        Width = b.Right - b.Left;
        Height = b.Bottom - b.Top;
        desktop = new D3D.Texture2D(dev, new D3D.Texture2DDescription
        {
            Width = Width, Height = Height, MipLevels = 1, ArraySize = 1,
            Format = DXGI.Format.B8G8R8A8_UNorm,
            SampleDescription = new DXGI.SampleDescription(1, 0),
            Usage = D3D.ResourceUsage.Default, BindFlags = D3D.BindFlags.None,
            CpuAccessFlags = D3D.CpuAccessFlags.None, OptionFlags = D3D.ResourceOptionFlags.None
        });
    }

    public FrameState Poll(int timeoutMs)
    {
        if (dup == null || dev == null || desktop == null) return FrameState.Lost;
        var r = dup.TryAcquireNextFrame(timeoutMs, out _, out var res);
        if (r.Failure)
            return r.Code == DXGI.ResultCode.WaitTimeout.Result.Code ? FrameState.Timeout : FrameState.Lost;
        try
        {
            using var tex = res.QueryInterface<D3D.Texture2D>();
            dev.ImmediateContext.CopyResource(tex, desktop);
            hasFrame = true;
        }
        finally { res.Dispose(); dup.ReleaseFrame(); }
        return FrameState.Updated;
    }

    /// <summary>Copia a região (ajustada aos limites) como BGRA.</summary>
    public byte[]? Crop(ref int x, ref int y, ref int w, ref int h)
    {
        if (dev == null || desktop == null || !hasFrame) return null;
        x = Math.Clamp(x, 0, Width - 1); y = Math.Clamp(y, 0, Height - 1);
        w = Math.Min(w, Width - x); h = Math.Min(h, Height - y);
        if (w < 16 || h < 16) return null;
        if (staging == null || stW != w || stH != h)
        {
            staging?.Dispose();
            staging = new D3D.Texture2D(dev, new D3D.Texture2DDescription
            {
                Width = w, Height = h, MipLevels = 1, ArraySize = 1,
                Format = DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new DXGI.SampleDescription(1, 0),
                Usage = D3D.ResourceUsage.Staging, BindFlags = D3D.BindFlags.None,
                CpuAccessFlags = D3D.CpuAccessFlags.Read, OptionFlags = D3D.ResourceOptionFlags.None
            });
            stW = w; stH = h;
        }
        var ctx = dev.ImmediateContext;
        ctx.CopySubresourceRegion(desktop, 0,
            new D3D.ResourceRegion { Left = x, Top = y, Front = 0, Right = x + w, Bottom = y + h, Back = 1 },
            staging, 0);
        var box = ctx.MapSubresource(staging, 0, D3D.MapMode.Read, D3D.MapFlags.None);
        try
        {
            var buf = new byte[checked(w * h * 4)];
            for (int row = 0; row < h; row++)
                Marshal.Copy(IntPtr.Add(box.DataPointer, row * box.RowPitch), buf, row * w * 4, w * 4);
            return buf;
        }
        finally { ctx.UnmapSubresource(staging, 0); }
    }

    public void Close()
    {
        hasFrame = false;
        staging?.Dispose(); staging = null; stW = stH = 0;
        desktop?.Dispose(); desktop = null;
        dup?.Dispose(); dup = null;
        dev?.Dispose(); dev = null;
    }

    public void Dispose() => Close();
}
