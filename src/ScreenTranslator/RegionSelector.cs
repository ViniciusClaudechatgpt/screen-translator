using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenTranslator;

public sealed class RegionSelector : Window
{
    static bool open;
    readonly Canvas canvas = new();
    readonly Rectangle box = new()
    {
        Stroke = Brushes.Lime, StrokeThickness = 2,
        Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)), Visibility = Visibility.Hidden
    };
    readonly Action<int[]?> done;
    Point start;
    bool drag, finished;

    RegionSelector(Action<int[]?> done)
    {
        this.done = done;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Cursor = Cursors.Cross;
        Left = 0; Top = 0; Width = 100; Height = 100;

        canvas.Children.Add(box);
        var hint = new TextBlock
        {
            Text = "Arraste para selecionar a região a traduzir  •  Esc cancela",
            Foreground = Brushes.White, FontSize = 18, Margin = new Thickness(20)
        };
        canvas.Children.Add(hint);
        Content = canvas;

        MouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(canvas); drag = true; CaptureMouse();
            box.Visibility = Visibility.Visible;
        };
        MouseMove += (_, e) =>
        {
            if (!drag) return;
            var p = e.GetPosition(canvas);
            Canvas.SetLeft(box, Math.Min(start.X, p.X));
            Canvas.SetTop(box, Math.Min(start.Y, p.Y));
            box.Width = Math.Abs(p.X - start.X);
            box.Height = Math.Abs(p.Y - start.Y);
        };
        MouseLeftButtonUp += (_, e) =>
        {
            if (!drag) return;
            drag = false; ReleaseMouseCapture();
            var p = e.GetPosition(canvas);
            var dpi = VisualTreeHelper.GetDpi(this);
            int x = (int)(Math.Min(start.X, p.X) * dpi.DpiScaleX);
            int y = (int)(Math.Min(start.Y, p.Y) * dpi.DpiScaleY);
            int w = (int)(Math.Abs(p.X - start.X) * dpi.DpiScaleX);
            int h = (int)(Math.Abs(p.Y - start.Y) * dpi.DpiScaleY);
            Finish(w >= 20 && h >= 20 ? new[] { x, y, w, h } : null);
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Finish(null); };
        Closed += (_, _) => open = false;
    }

    void Finish(int[]? r)
    {
        if (finished) return;
        finished = true;
        Close();
        done(r);
    }

    public static void Pick(int mx, int my, int mw, int mh, Action<int[]?> done)
    {
        if (open) return;
        open = true;
        var win = new RegionSelector(done);
        var h = new WindowInteropHelper(win).EnsureHandle();
        win.Show();
        Native.SetWindowPos(h, Native.HWND_TOPMOST, mx, my, mw, mh, Native.SWP_SHOWWINDOW);
        win.Activate();
        win.Focus();
    }
}
