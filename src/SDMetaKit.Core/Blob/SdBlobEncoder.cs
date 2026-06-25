using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace SDMetaKit;

/// <summary>
/// Koduje SdMetadata do przenośnego, samowystarczalnego ciągu blob.
/// Format: SDMK1:&lt;base64url(deflate(utf8(json(payload))))&gt;
/// Ten sam input zawsze generuje ten sam blob (deterministyczny).
/// </summary>
public static class SdBlobEncoder
{
    private const string Prefix = "SDMK1:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public static string Encode(SdMetadata meta)
    {
        var payload = BuildPayload(meta);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var compressed = Deflate(bytes);
        return Prefix + Base64UrlEncode(compressed);
    }

    private static SdBlobPayload BuildPayload(SdMetadata m)
    {
        var knownParams = new SortedDictionary<string, string>(StringComparer.Ordinal);

        void Add(string key, string? value) { if (value is not null) knownParams[key] = value; }

        Add("cfg_scale", m.CfgScale);
        Add("denoising_strength", m.DenoisingStrength);
        Add("distilled_cfg_scale", m.DistilledCfgScale);
        Add("hires_cfg_scale", m.HiresCfgScale);
        Add("hires_module_1", m.HiresModule1);
        Add("hires_steps", m.HiresSteps);
        Add("hires_upscale", m.HiresUpscale);
        Add("hires_upscaler", m.HiresUpscaler);
        Add("model", m.Model);
        Add("model_hash", m.ModelHash);
        Add("sampler", m.Sampler);
        Add("schedule_type", m.ScheduleType);
        Add("seed", m.Seed);
        Add("size", m.Size);
        Add("steps", m.Steps);

        var other = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in m.OtherParams)
            other[kv.Key] = kv.Value;

        return new SdBlobPayload
        {
            SourceSha256 = m.Sha256,
            HasFile = m.Sha256 is not null,
            Prompt = m.Prompt,
            NegativePrompt = m.NegativePrompt,
            Params = knownParams,
            Other = other
        };
    }

    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var deflate = new DeflateStream(ms, CompressionLevel.SmallestSize))
            deflate.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
