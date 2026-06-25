namespace SDMetaKit;

/// <summary>
/// Wstrzykuje chunk PNG-tEXt z kluczem "parameters" do bufora PNG.
/// </summary>
public interface IPngWriter
{
    byte[] InjectParametersText(byte[] png, string parametersText);
}
