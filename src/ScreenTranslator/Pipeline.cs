using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenTranslator;

/// <summary>Captura → detecção de mudança → OCR → tradução (com cache) → overlay.</summary>
public sealed class Pipeline : IDisposable
{
    readonly AppConfig cfg;
    readonly ScreenCapture cap;
    readonly OverlayWindow overlay;
    readonly Translator tr;
    readonly OcrService ocr = new();
    readonly Thread thread;
    volatile bool run = true, force = true;

    public Pipeline(AppConfig cfg, ScreenCapture cap, OverlayWindow overlay, Translator tr)
    {
        this.cfg = cfg; this.cap = cap; this.overlay = overlay; this.tr = tr;
        thread = new Thread(Loop) { IsBackground = true, Name = "pipeline", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    public void Force() => force = true;

    void Loop()
    {
        byte[]? prev = null;
        long last = 0;
        bool pending = true;
        string lastKey = "";

        while (run)
        {
            try
            {
                if (!cap.Ready)
                {
                    cap.Init();
                    overlay.Dispatcher.InvokeAsync(() => overlay.Place(cap.X, cap.Y, cap.Width, cap.Height));
                    force = true;
                }

                var st = cap.Poll(40);
                if (st == FrameState.Lost) { cap.Close(); Thread.Sleep(300); continue; }
                if (st == FrameState.Updated) pending = true;

                if (!cfg.Enabled) { prev = null; lastKey = ""; continue; }
                if (!pending && !force) continue;
                if (Environment.TickCount64 - last < cfg.IntervalMs) continue;

                bool f = force;
                force = false; pending = false;
                last = Environment.TickCount64;

                int x = 0, y = 0, w = cap.Width, h = cap.Height;
                if (cfg.Region is { Length: 4 } r) { x = r[0]; y = r[1]; w = r[2]; h = r[3]; }
                var data = cap.Crop(ref x, ref y, ref w, ref h);
                if (data == null) continue;

                var sig = Sig(data, w, h);
                if (!f && prev != null && Same(prev, sig)) continue; // nada mudou: não reprocessa
                prev = sig;

                var blocks = ocr.RecognizeAsync(data, w, h, cfg.OcrLanguage).GetAwaiter().GetResult();
                var cand = blocks.Where(b => b.Text.Count(char.IsLetter) >= 2).Take(40).ToList();
                var outs = Task.WhenAll(cand.Select(b => tr.TranslateAsync(b.Text))).GetAwaiter().GetResult();
                if (!run) break;

                var items = new List<OcrBlock>();
                for (int i = 0; i < cand.Count; i++)
                    if (!string.Equals(outs[i].Trim(), cand[i].Text.Trim(), StringComparison.OrdinalIgnoreCase))
                        items.Add(cand[i] with { Text = outs[i] });

                if (!cfg.Enabled) continue;
                string key = $"{x},{y}|" + string.Join("|", items.Select(i => $"{(int)i.X / 6}:{(int)i.Y / 6}:{i.Text}"));
                if (key == lastKey) continue;
                lastKey = key;
                overlay.Draw(items, x, y);
            }
            catch (Exception e)
            {
                Log.Write(e.Message);
                Thread.Sleep(1500);
            }
        }
    }

    static byte[] Sig(byte[] d, int w, int h)
    {
        int sw = w / 2, sh = h / 2;
        var s = new byte[sw * sh];
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                int i = ((y * 2) * w + x * 2) * 4;
                s[y * sw + x] = (byte)((d[i] + d[i + 1] * 2 + d[i + 2]) >> 2);
            }
        return s;
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i] - b[i]) > 24) diff++;
        return diff <= Math.Max(2, a.Length / 1000);
    }

    public void Dispose()
    {
        run = false;
        // Aguarda o processamento atual terminar antes de o controlador liberar a captura DXGI.
        thread.Join();
    }
}
