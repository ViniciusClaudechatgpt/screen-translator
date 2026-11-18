using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenTranslator;

/// <summary>Janela transparente, click-through, sempre no topo e excluída da captura (sem loop de feedback).</summary>
public sealed class OverlayWindow : Window
{
    readonly Canvas canvas = new();
    readonly IntPtr hwnd;

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Left = 0; Top = 0; Width = 100; Height = 100;
        Content = canvas;

        hwnd = new WindowInteropHelper(this).EnsureHandle();
        long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        long newEx = ex | Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(newEx));
        if (!Native.SetWindowDisplayAffinity(hwnd, Native.WDA_EXCLUDEFROMCAPTURE))
            Log.Write("SetWindowDisplayAffinity falhou: overlay pode aparecer na captura.");

        var keepTop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        keepTop.Tick += (_, _) => Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        keepTop.Start();
        Show();
    }

    public void Place(int x, int y, int w, int h) =>
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

    public void Clear() => Dispatcher.InvokeAsync(() => canvas.Children.Clear());

    /// <param name="items">Blocos traduzidos, em pixels relativos à região.</param>
    /// <param name="ox">Origem X da região (px) no monitor.</param>
    /// <param name="oy">Origem Y da região (px) no monitor.</param>
    public void Draw(IReadOnlyList<OcrBlock> items, int ox, int oy)
    {
        Dispatcher.InvokeAsync(() =>
        {
            canvas.Children.Clear();
            double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
            foreach (var it in items)
            {
                var tb = new TextBlock
                {
                    Text = it.Text,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = Math.Clamp(it.LineH / s * 0.72, 10, 42),
                    MaxWidth = Math.Max(it.W / s + 16, 150)
                };
                var b = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(240, 14, 14, 18)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(3, 1, 3, 1),
                    MinWidth = it.W / s + 6,
                    MinHeight = it.H / s + 2,
                    Child = tb
                };
                Canvas.SetLeft(b, (ox + it.X) / s - 3);
                Canvas.SetTop(b, (oy + it.Y) / s - 1);
                canvas.Children.Add(b);
            }
        });
    }
}
