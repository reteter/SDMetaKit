using System.Text.Json.Serialization;

namespace SDMetaKit;

/// <summary>
/// Wewnętrzny payload bloba — serializowany do JSON, kompresowany i kodowany w base64url.
/// Klucze JSON są skrócone żeby zmniejszyć rozmiar skompresowanego bloba.
/// </summary>
internal sealed class SdBlobPayload
{
    [JsonPropertyName("v")]
    public int Version { get; init; } = 1;

    /// <summary>SHA-256 pliku źródłowego (hex) lub null gdy brak pliku.</summary>
    [JsonPropertyName("src")]
    public string? SourceSha256 { get; init; }

    /// <summary>Czy blob pochodzi z pliku (true) czy z surowego tekstu (false).</summary>
    [JsonPropertyName("has_file")]
    public bool HasFile { get; init; }

    [JsonPropertyName("p")]
    public string Prompt { get; init; } = "";

    [JsonPropertyName("np")]
    public string NegativePrompt { get; init; } = "";

    /// <summary>Znane parametry (non-null). Klucze posortowane dla determinizmu.</summary>
    [JsonPropertyName("params")]
    public SortedDictionary<string, string> Params { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Pozostałe parametry z other_params. Klucze posortowane.</summary>
    [JsonPropertyName("other")]
    public SortedDictionary<string, string> Other { get; init; } = new(StringComparer.Ordinal);
}
