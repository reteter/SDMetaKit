namespace SDMetaKit;

/// <summary>
/// Ekstraktor danych z pliku obrazu: odczyt bajtów, SHA-256, tagi, surowy tekst parametrów.
/// </summary>
public interface IMetadataExtractor
{
    Task<RawMetadata> ExtractAsync(string filePath,
        ExtractionOptions? options = null,
        CancellationToken ct = default);

    Task<RawMetadata> ExtractAsync(Stream imageStream,
        ExtractionOptions? options = null,
        CancellationToken ct = default);
}
