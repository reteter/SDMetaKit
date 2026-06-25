using System.Text.Json.Serialization;

namespace SDMetaKit;

/// <summary>
/// Sparsowane metadane generacji Stable Diffusion.
/// Wszystkie pola zachowują surowe wartości string — brak konwersji do typów numerycznych.
/// Schemat JSON jest nadzbiorem SdPackExif (identyczne nazwy pól) — archiva .sdpack
/// pozostają czytelne bez migracji.
/// Pola są wyłącznie dodawane między wersjami, nigdy usuwane ani zmieniane typów.
/// </summary>
public sealed record SdMetadata
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = "";

    [JsonPropertyName("negative_prompt")]
    public string NegativePrompt { get; init; } = "";

    [JsonPropertyName("steps")]
    public string? Steps { get; init; }

    [JsonPropertyName("sampler")]
    public string? Sampler { get; init; }

    [JsonPropertyName("schedule_type")]
    public string? ScheduleType { get; init; }

    [JsonPropertyName("cfg_scale")]
    public string? CfgScale { get; init; }

    [JsonPropertyName("distilled_cfg_scale")]
    public string? DistilledCfgScale { get; init; }

    [JsonPropertyName("seed")]
    public string? Seed { get; init; }

    [JsonPropertyName("size")]
    public string? Size { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("model_hash")]
    public string? ModelHash { get; init; }

    [JsonPropertyName("denoising_strength")]
    public string? DenoisingStrength { get; init; }

    [JsonPropertyName("hires_upscale")]
    public string? HiresUpscale { get; init; }

    [JsonPropertyName("hires_steps")]
    public string? HiresSteps { get; init; }

    [JsonPropertyName("hires_upscaler")]
    public string? HiresUpscaler { get; init; }

    [JsonPropertyName("hires_cfg_scale")]
    public string? HiresCfgScale { get; init; }

    [JsonPropertyName("hires_module_1")]
    public string? HiresModule1 { get; init; }

    /// <summary>SHA-256 pliku źródłowego (hex, lowercase). Null gdy parsowany z surowego tekstu.</summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    /// <summary>Źródło danych: "PNG-tEXt", "EXIF-UserComment", "EXIF-ImageDescription", "Text", "Unknown".</summary>
    [JsonPropertyName("source_kind")]
    public string? SourceKind { get; init; }

    /// <summary>Wszystkie pozostałe klucze z bloku parametrów (nie zmapowane na typowane pola).</summary>
    [JsonPropertyName("other_params")]
    public IReadOnlyDictionary<string, string> OtherParams { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Wszystkie surowe tagi z pliku obrazu. Klucz: "{DirectoryName}/{TagName}".</summary>
    [JsonPropertyName("raw")]
    public IReadOnlyDictionary<string, string> Raw { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Oryginalny, niesparsowany tekst bloku parametrów.</summary>
    [JsonPropertyName("raw_text")]
    public string? RawText { get; init; }
}
