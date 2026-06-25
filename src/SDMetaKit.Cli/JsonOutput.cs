namespace SDMetaKit.Cli;

internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    internal static string Serialize(object value) =>
        JsonSerializer.Serialize(value, Pretty);
}
