namespace SDMetaKit;

/// <summary>
/// Główny punkt wejścia — statyczna fasada zgodna ze stylem istniejących klas
/// ExifParser i SdPackExifExtractor (brak wymaganego DI).
/// </summary>
public static class SdMetaKit
{
    private static readonly IMetadataParser DefaultParser = new A1111Parser();
    private static readonly IMetadataExtractor DefaultExtractor = new FileMetadataExtractor();
    private static readonly IPngWriter DefaultWriter = new PngChunkWriter();

    // ── Parsowanie ─────────────────────────────────────────────────────────

    /// <summary>
    /// Odczytuje plik PNG lub JPEG, wyciąga tekst parametrów SD i parsuje go.
    /// SHA-256 jest obliczany z tego samego bufora co metadane.
    /// </summary>
    public static async Task<SdMetadata> ParseFileAsync(
        string filePath,
        ExtractionOptions? options = null,
        CancellationToken ct = default)
    {
        var raw = await DefaultExtractor.ExtractAsync(filePath, options, ct);
        return MergeRaw(raw);
    }

    /// <summary>
    /// Parsuje surowy tekst parametrów A1111 bez dostępu do pliku.
    /// SHA-256, SourceKind i Raw będą puste.
    /// </summary>
    public static SdMetadata Parse(string parametersText)
        => DefaultParser.Parse(parametersText);

    // ── Zapis ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wstrzykuje chunk PNG-tEXt "parameters" przed IEND.
    /// Zwraca nowy bufor; oryginał nie jest modyfikowany.
    /// </summary>
    public static byte[] InjectParametersText(byte[] png, string parametersText)
        => DefaultWriter.InjectParametersText(png, parametersText);

    // ── Blob ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Koduje SdMetadata do przenośnego ciągu blob.
    /// Ten sam input → ten sam ciąg (deterministyczny).
    /// </summary>
    public static string EncodeBlob(SdMetadata meta)
        => SdBlobEncoder.Encode(meta);

    /// <summary>Dekoduje blob do SdMetadata.</summary>
    public static SdMetadata DecodeBlob(string blob)
        => SdBlobDecoder.Decode(blob);

    /// <summary>Bezpieczna wersja Decode — nie rzuca wyjątku.</summary>
    public static bool TryDecodeBlob(string blob, out SdMetadata? meta)
        => SdBlobDecoder.TryDecode(blob, out meta);

    // ── Fabryki (DI-friendly) ─────────────────────────────────────────────

    public static IMetadataExtractor CreateExtractor() => new FileMetadataExtractor();
    public static IMetadataParser CreateParser() => new A1111Parser();
    public static IPngWriter CreateWriter() => new PngChunkWriter();

    // ── Prywatne ──────────────────────────────────────────────────────────

    private static SdMetadata MergeRaw(RawMetadata raw)
    {
        if (string.IsNullOrWhiteSpace(raw.ParametersText))
        {
            return new SdMetadata
            {
                Sha256 = raw.Sha256,
                SourceKind = raw.SourceKind,
                Raw = raw.AllTags
            };
        }

        var parsed = DefaultParser.Parse(raw.ParametersText);
        return parsed with
        {
            Sha256 = raw.Sha256,
            SourceKind = raw.SourceKind,
            Raw = raw.AllTags
        };
    }
}
