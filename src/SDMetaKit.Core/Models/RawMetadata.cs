namespace SDMetaKit;

/// <summary>
/// Wynik ekstrakcji surowych danych z pliku obrazu — przed parsowaniem do SdMetadata.
/// Typy z MetadataExtractor NuGet nie pojawiają się w tym modelu.
/// </summary>
public sealed class RawMetadata
{
    /// <summary>
    /// Surowy tekst parametrów A1111 (prompt + params), lub null gdy brak metadanych SD.
    /// </summary>
    public string? ParametersText { get; init; }

    /// <summary>
    /// Źródło znalezione: "PNG-tEXt", "EXIF-UserComment", "EXIF-ImageDescription", "Unknown".
    /// </summary>
    public string SourceKind { get; init; } = "Unknown";

    /// <summary>
    /// SHA-256 (hex lowercase) bajtów pliku. Null gdy używano przeciążenia Stream bez Seek
    /// lub gdy ComputeSha256 = false w ExtractionOptions.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Wszystkie tagi znalezione w obrazie. Klucz: "{DirectoryName}/{TagName}".
    /// </summary>
    public IReadOnlyDictionary<string, string> AllTags { get; init; }
        = new Dictionary<string, string>();
}
