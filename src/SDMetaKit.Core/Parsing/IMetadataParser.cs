namespace SDMetaKit;

/// <summary>
/// Parser surowego tekstu parametrów A1111 → SdMetadata.
/// Czysta operacja string→struct: brak I/O, bezpieczny wielowątkowo.
/// </summary>
public interface IMetadataParser
{
    SdMetadata Parse(string parametersText);
}
