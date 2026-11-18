using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace ScreenTranslator;

public sealed class HotkeyManager : IDisposable
{
    readonly HwndSource src;
    readonly Dictionary<int, Action> map = new();

    public HotkeyManager()
    {
        src = new HwndSource(new HwndSourceParameters("ScreenTranslatorHotkeys") { ParentWindow = new IntPtr(-3) });
        src.AddHook(Hook);
    }

    /// <summary>Registra Ctrl+Alt+tecla.</summary>
    public bool Register(uint vk, Action action)
    {
        int id = map.Count + 1;
        if (!Native.RegisterHotKey(src.Handle, id, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, vk)) return false;
        map[id] = action;
        return true;
    }

    IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && map.TryGetValue(wParam.ToInt32(), out var a)) { a(); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in map.Keys) Native.UnregisterHotKey(src.Handle, id);
        src.Dispose();
    }
}
