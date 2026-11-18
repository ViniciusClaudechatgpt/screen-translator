using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenTranslator;

public sealed class Translator
{
    static readonly HttpClient Http = Create();
    static HttpClient Create()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        return c;
    }

    static string CachePath => Path.Combine(AppConfig.Dir, "cache.json");
    readonly AppConfig cfg;
    readonly ConcurrentDictionary<string, string> cache = new();

    public Translator(AppConfig c)
    {
        cfg = c;
        try
        {
            if (File.Exists(CachePath))
            {
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CachePath));
                if (d != null) foreach (var kv in d) cache[kv.Key] = kv.Value;
            }
        }
        catch (Exception e) { Log.Write(e); }
    }

    public void SaveCache()
    {
        try { Directory.CreateDirectory(AppConfig.Dir); File.WriteAllText(CachePath, JsonSerializer.Serialize(cache)); }
        catch (Exception e) { Log.Write(e); }
    }

    public async Task<string> TranslateAsync(string text)
    {
        text = text.Trim();
        if (text.Length > 1500) text = text[..1500];
        string key = $"{cfg.Provider}|{cfg.SourceLanguage}|{cfg.TargetLanguage}|{text}";
        if (cache.TryGetValue(key, out var hit)) return hit;
        try
        {
            string r = cfg.Provider.ToLowerInvariant() switch
            {
                "deepl" => await DeepL(text),
                "google" => await GoogleApi(text),
                _ => await GoogleFree(text)
            };
            if (string.IsNullOrWhiteSpace(r)) return text;
            if (cache.Count > 20000) cache.Clear();
            cache[key] = r;
            return r;
        }
        catch (Exception e) { Log.Write(e.Message); return text; }
    }

    async Task<string> GoogleFree(string text)
    {
        string url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t" +
                     $"&sl={Uri.EscapeDataString(cfg.SourceLanguage)}&tl={Uri.EscapeDataString(cfg.TargetLanguage)}&q={Uri.EscapeDataString(text)}";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var sb = new StringBuilder();
        foreach (var seg in doc.RootElement[0].EnumerateArray()) sb.Append(seg[0].GetString());
        return sb.ToString();
    }

    async Task<string> GoogleApi(string text)
    {
        var body = new Dictionary<string, string> { ["q"] = text, ["target"] = cfg.TargetLanguage, ["format"] = "text" };
        if (cfg.SourceLanguage != "auto") body["source"] = cfg.SourceLanguage;
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var resp = await Http.PostAsync("https://translation.googleapis.com/language/translate/v2?key=" + Uri.EscapeDataString(cfg.ApiKey), content);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("translations")[0].GetProperty("translatedText").GetString() ?? text;
    }

    async Task<string> DeepL(string text)
    {
        string host = cfg.ApiKey.EndsWith(":fx") ? "api-free.deepl.com" : "api.deepl.com";
        var form = new Dictionary<string, string> { ["text"] = text, ["target_lang"] = DeeplLang(cfg.TargetLanguage) };
        if (cfg.SourceLanguage != "auto") form["source_lang"] = cfg.SourceLanguage.Split('-')[0].ToUpperInvariant();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{host}/v2/translate") { Content = new FormUrlEncodedContent(form) };
        req.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + cfg.ApiKey);
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? text;
    }

    static string DeeplLang(string l) => l.ToLowerInvariant() switch
    {
        "pt" or "pt-br" => "PT-BR",
        "pt-pt" => "PT-PT",
        "en" or "en-us" => "EN-US",
        "en-gb" => "EN-GB",
        "zh" or "zh-cn" => "ZH",
        _ => l.Split('-')[0].ToUpperInvariant()
    };
}
