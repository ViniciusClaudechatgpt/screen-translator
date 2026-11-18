using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Security.Cryptography;

namespace ScreenTranslator;

public sealed record OcrBlock(string Text, double X, double Y, double W, double H, double LineH);

public sealed class OcrService
{
    sealed class Blk { public double L, T, R, B, LH; public StringBuilder Text = new(); }

    OcrEngine? engine;
    string tag = "";

    public async Task<List<OcrBlock>> RecognizeAsync(byte[] bgra, int w, int h, string lang)
    {
        if (engine == null || tag != lang)
        {
            engine = null;
            try { if (Language.IsWellFormed(lang)) engine = OcrEngine.TryCreateFromLanguage(new Language(lang)); } catch { }
            engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
            tag = lang;
            if (engine == null) throw new InvalidOperationException("Nenhum idioma de OCR instalado no Windows.");
        }

        // O OCR do Windows limita a imagem a MaxImageDimension px: reduz por decimação se preciso.
        int max = (int)OcrEngine.MaxImageDimension;
        int sc = 1;
        while (w / sc > max || h / sc > max) sc++;
        if (sc > 1) bgra = Decimate(bgra, w, h, sc, out w, out h);

        var buffer = CryptographicBuffer.CreateFromByteArray(bgra);
        using var bmp = SoftwareBitmap.CreateCopyFromBuffer(buffer, BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Ignore);
        var res = await engine.RecognizeAsync(bmp);

        var blocks = new List<Blk>();
        foreach (var line in res.Lines)
        {
            if (line.Words.Count == 0) continue;
            double l = line.Words.Min(x => x.BoundingRect.Left), t = line.Words.Min(x => x.BoundingRect.Top);
            double r = line.Words.Max(x => x.BoundingRect.Right), b = line.Words.Max(x => x.BoundingRect.Bottom);
            double lh = Math.Max(1, b - t);
            var m = blocks.FirstOrDefault(k =>
            {
                double gap = t - k.B;
                return gap > -0.3 * lh && gap < 0.6 * lh && Math.Abs(l - k.L) < 1.5 * lh && lh / k.LH > 0.75 && lh / k.LH < 1.33;
            });
            if (m == null) { var n = new Blk { L = l, T = t, R = r, B = b, LH = lh }; n.Text.Append(line.Text); blocks.Add(n); }
            else { m.Text.Append(' ').Append(line.Text); m.L = Math.Min(m.L, l); m.R = Math.Max(m.R, r); m.B = Math.Max(m.B, b); m.LH = lh; }
        }
        return blocks.Select(k => new OcrBlock(k.Text.ToString(), k.L * sc, k.T * sc, (k.R - k.L) * sc, (k.B - k.T) * sc, k.LH * sc)).ToList();
    }

    static byte[] Decimate(byte[] s, int w, int h, int k, out int nw, out int nh)
    {
        nw = w / k; nh = h / k;
        var d = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
                Buffer.BlockCopy(s, ((y * k) * w + x * k) * 4, d, (y * nw + x) * 4, 4);
        return d;
    }
}
