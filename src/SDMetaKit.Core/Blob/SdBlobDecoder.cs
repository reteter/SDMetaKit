using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace SDMetaKit;

/// <summary>
/// Dekoduje blob wygenerowany przez SdBlobEncoder z powrotem do SdMetadata.
/// </summary>
public static class SdBlobDecoder
{
    private const string Prefix = "SDMK1:";

    public static SdMetadata Decode(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob))
            throw new ArgumentException("Blob jest pusty.", nameof(blob));

        if (!blob.StartsWith(Prefix, StringComparison.Ordinal))
            throw new FormatException($"Nieznana wersja bloba. Oczekiwany prefix: {Prefix}");

        var b64 = blob[Prefix.Length..];
        var compressed = Base64UrlDecode(b64);
        var json = Encoding.UTF8.GetString(Inflate(compressed));

        var payload = JsonSerializer.Deserialize<SdBlobPayload>(json)
            ?? throw new FormatException("Błąd deserializacji bloba.");

        return PayloadToMetadata(payload);
    }

    public static bool TryDecode(string blob, out SdMetadata? meta)
    {
        try { meta = Decode(blob); return true; }
        catch { meta = null; return false; }
    }

    private static SdMetadata PayloadToMetadata(SdBlobPayload p)
    {
        string? Get(string key) => p.Params.TryGetValue(key, out var v) ? v : null;

        var other = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in p.Other) other[kv.Key] = kv.Value;

        return new SdMetadata
        {
            Prompt = p.Prompt,
            NegativePrompt = p.NegativePrompt,
            Steps = Get("steps"),
            Sampler = Get("sampler"),
            ScheduleType = Get("schedule_type"),
            CfgScale = Get("cfg_scale"),
            DistilledCfgScale = Get("distilled_cfg_scale"),
            Seed = Get("seed"),
            Size = Get("size"),
            Model = Get("model"),
            ModelHash = Get("model_hash"),
            DenoisingStrength = Get("denoising_strength"),
            HiresUpscale = Get("hires_upscale"),
            HiresSteps = Get("hires_steps"),
            HiresUpscaler = Get("hires_upscaler"),
            HiresCfgScale = Get("hires_cfg_scale"),
            HiresModule1 = Get("hires_module_1"),
            Sha256 = p.SourceSha256,
            SourceKind = p.HasFile ? "Blob-File" : "Blob-Text",
            OtherParams = other
        };
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
