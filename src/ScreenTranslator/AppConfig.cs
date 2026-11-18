using System;
using System.IO;
using System.Text.Json;

namespace ScreenTranslator;

public sealed class AppConfig
{
    public string Provider { get; set; } = "google-free"; // google-free | deepl | google
    public string ApiKey { get; set; } = "";
    public string TargetLanguage { get; set; } = "pt";
    public string SourceLanguage { get; set; } = "auto";
    public string OcrLanguage { get; set; } = "en-US";
    public string[] Languages { get; set; } = new[] { "pt", "en", "es", "fr", "de", "it", "ru", "ja", "ko", "zh-CN" };
    public int IntervalMs { get; set; } = 200;
    public bool Enabled { get; set; } = true;
    public int[]? Region { get; set; } // x, y, largura, altura (pixels, relativo ao monitor principal)

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator");
    public static string FilePath => Path.Combine(Dir, "config.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var c = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath));
                if (c != null)
                {
                    if (c.Languages == null || c.Languages.Length == 0) c.Languages = new AppConfig().Languages;
                    c.Provider = string.IsNullOrWhiteSpace(c.Provider) ? "google-free" : c.Provider.Trim();
                    c.ApiKey ??= "";
                    c.TargetLanguage = string.IsNullOrWhiteSpace(c.TargetLanguage) ? "pt" : c.TargetLanguage.Trim();
                    c.SourceLanguage = string.IsNullOrWhiteSpace(c.SourceLanguage) ? "auto" : c.SourceLanguage.Trim();
                    c.OcrLanguage = string.IsNullOrWhiteSpace(c.OcrLanguage) ? "en-US" : c.OcrLanguage.Trim();
                    c.IntervalMs = Math.Clamp(c.IntervalMs, 50, 5000);
                    return c;
                }
            }
        }
        catch (Exception e) { Log.Write(e); }
        var d = new AppConfig();
        d.Save();
        return d;
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opt)); }
        catch (Exception e) { Log.Write(e); }
    }

    public void Reload()
    {
        var n = Load();
        Provider = n.Provider; ApiKey = n.ApiKey; TargetLanguage = n.TargetLanguage;
        SourceLanguage = n.SourceLanguage; OcrLanguage = n.OcrLanguage; Languages = n.Languages;
        IntervalMs = n.IntervalMs; Enabled = n.Enabled; Region = n.Region;
    }
}

public static class Log
{
    public static void Write(object e)
    {
        try
        {
            Directory.CreateDirectory(AppConfig.Dir);
            File.AppendAllText(Path.Combine(AppConfig.Dir, "log.txt"), $"{DateTime.Now:s} {e}\n");
        }
        catch { }
    }
}
