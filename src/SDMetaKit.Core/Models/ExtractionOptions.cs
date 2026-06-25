namespace SDMetaKit;

/// <summary>
/// Opcje ekstrakcji metadanych z pliku obrazu.
/// </summary>
public sealed class ExtractionOptions
{
    /// <summary>
    /// Minimalna liczba znaków tekstu parametrów, żeby był uznany za valid.
    /// Domyślnie 150 — eliminuje przypadkowe komentarze w PNG-tEXt.
    /// Ustaw 0 żeby akceptować każdy niepusty tekst.
    /// </summary>
    public int MinimumTextLength { get; init; } = 150;

    /// <summary>
    /// Gdy kilka kandydatów, preferuj ten zawierający "Steps:".
    /// Domyślnie true.
    /// </summary>
    public bool PreferStepsCandidate { get; init; } = true;

    /// <summary>
    /// Oblicz SHA-256 pliku. Domyślnie true.
    /// Ustaw false dla szybkości gdy hash jest niepotrzebny.
    /// </summary>
    public bool ComputeSha256 { get; init; } = true;

    /// <summary>
    /// Zbierz wszystkie tagi obrazu do RawMetadata.AllTags. Domyślnie true.
    /// </summary>
    public bool CollectAllTags { get; init; } = true;

    /// <summary>Domyślna konfiguracja: próg 150 znaków (zgodny z SDInfo UI).</summary>
    public static readonly ExtractionOptions Default = new();

    /// <summary>Konfiguracja bez progu długości (zgodna z poprzednim SdPackExifExtractor).</summary>
    public static readonly ExtractionOptions NoLengthFilter = new() { MinimumTextLength = 0 };
}
