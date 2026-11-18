using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace ScreenTranslator;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\ScreenTranslator.Instance", out bool first);
        if (!first) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppController ctl;
        try { ctl = new AppController(app); }
        catch (Exception e)
        {
            Log.Write(e);
            WinForms.MessageBox.Show("Não foi possível iniciar a captura de tela (DXGI):\n" + e.Message, "Screen Translator");
            return;
        }
        app.Run();
        ctl.Dispose();
    }
}

sealed class AppController : IDisposable
{
    readonly Application app;
    readonly AppConfig cfg = AppConfig.Load();
    readonly ScreenCapture cap = new();
    readonly Translator tr;
    readonly OverlayWindow overlay;
    readonly Pipeline pipe;
    readonly HotkeyManager hk = new();
    readonly WinForms.NotifyIcon tray;
    readonly WinForms.ToolStripMenuItem toggleItem, langMenu;
    bool disposed;

    public AppController(Application app)
    {
        this.app = app;
        tr = new Translator(cfg);
        cap.Init();
        overlay = new OverlayWindow();
        overlay.Place(cap.X, cap.Y, cap.Width, cap.Height);
        pipe = new Pipeline(cfg, cap, overlay, tr);

        var failed = new List<string>();
        if (!hk.Register(0x54, Toggle)) failed.Add("Ctrl+Alt+T");
        if (!hk.Register(0x52, PickRegion)) failed.Add("Ctrl+Alt+R");
        if (!hk.Register(0x46, FullScreen)) failed.Add("Ctrl+Alt+F");
        if (!hk.Register(0x4C, NextLanguage)) failed.Add("Ctrl+Alt+L");

        toggleItem = new WinForms.ToolStripMenuItem("Tradução ativa (Ctrl+Alt+T)") { Checked = cfg.Enabled };
        toggleItem.Click += (_, _) => Toggle();
        langMenu = new WinForms.ToolStripMenuItem("Idioma de destino (Ctrl+Alt+L)");
        foreach (var code in cfg.Languages)
        {
            var it = new WinForms.ToolStripMenuItem(code);
            it.Click += (_, _) => SetLanguage(code);
            langMenu.DropDownItems.Add(it);
        }
        RefreshLang();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(toggleItem);
        menu.Items.Add("Selecionar região (Ctrl+Alt+R)", null, (_, _) => PickRegion());
        menu.Items.Add("Tela inteira (Ctrl+Alt+F)", null, (_, _) => FullScreen());
        menu.Items.Add(langMenu);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Abrir configurações", null, (_, _) =>
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + AppConfig.FilePath + "\"") { UseShellExecute = true }));
        menu.Items.Add("Recarregar configurações", null, (_, _) => { cfg.Reload(); RefreshLang(); toggleItem.Checked = cfg.Enabled; pipe.Force(); });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Quit());

        tray = new WinForms.NotifyIcon { Icon = MakeIcon(), Text = "Screen Translator", ContextMenuStrip = menu, Visible = true };
        if (failed.Count > 0)
            tray.ShowBalloonTip(4000, "Screen Translator", "Atalhos em uso por outro programa: " + string.Join(", ", failed), WinForms.ToolTipIcon.Warning);
    }

    void Toggle()
    {
        cfg.Enabled = !cfg.Enabled;
        cfg.Save();
        toggleItem.Checked = cfg.Enabled;
        if (!cfg.Enabled) overlay.Clear(); else pipe.Force();
        Balloon(cfg.Enabled ? "Tradução ligada" : "Tradução desligada");
    }

    void PickRegion() =>
        RegionSelector.Pick(cap.X, cap.Y, cap.Width, cap.Height, r =>
        {
            if (r != null) { cfg.Region = r; cfg.Save(); }
            pipe.Force();
        });

    void FullScreen() { cfg.Region = null; cfg.Save(); pipe.Force(); Balloon("Traduzindo a tela inteira"); }

    void NextLanguage()
    {
        int i = Array.IndexOf(cfg.Languages, cfg.TargetLanguage);
        SetLanguage(cfg.Languages[(i + 1) % cfg.Languages.Length]);
    }

    void SetLanguage(string code)
    {
        cfg.TargetLanguage = code;
        cfg.Save();
        RefreshLang();
        pipe.Force();
        Balloon("Idioma de destino: " + code);
    }

    void RefreshLang()
    {
        foreach (var it in langMenu.DropDownItems.OfType<WinForms.ToolStripMenuItem>())
            it.Checked = it.Text == cfg.TargetLanguage;
    }

    void Balloon(string msg) => tray.ShowBalloonTip(1200, "Screen Translator", msg, WinForms.ToolTipIcon.Info);

    void Quit() { Dispose(); app.Shutdown(); }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        tray.Visible = false;
        tray.Dispose();
        hk.Dispose();
        pipe.Dispose();
        tr.SaveCache();
        cfg.Save();
        cap.Dispose();
    }

    static System.Drawing.Icon MakeIcon()
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(System.Drawing.Brushes.DodgerBlue, 1, 1, 30, 30);
            using var f = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
            g.DrawString("T", f, System.Drawing.Brushes.White, new System.Drawing.RectangleF(0, 0, 32, 32), sf);
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }
}
