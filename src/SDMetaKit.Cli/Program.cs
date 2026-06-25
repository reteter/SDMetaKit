using SDMetaKit.Cli;

var rootCommand = new RootCommand("sdmk — narzędzie do parsowania metadanych Stable Diffusion");

// ── parse ──────────────────────────────────────────────────────────────

var parseCommand = new Command("parse", "Parsuje metadane SD z pliku PNG/JPEG lub surowego tekstu");

var parseFilesArg = new Argument<FileInfo[]>("pliki", "Pliki PNG lub JPEG do sparsowania")
    { Arity = ArgumentArity.ZeroOrMore };
var parseTextOpt = new Option<string?>("--text", "Surowy tekst parametrów A1111 (bez pliku)");
var parseFormatOpt = new Option<string>("--format", () => "text", "Format wyjścia: text | json");
var parseMinLengthOpt = new Option<int>("--min-length", () => 150, "Minimalna długość tekstu parametrów");

parseCommand.AddArgument(parseFilesArg);
parseCommand.AddOption(parseTextOpt);
parseCommand.AddOption(parseFormatOpt);
parseCommand.AddOption(parseMinLengthOpt);

parseCommand.SetHandler(async (files, text, format, minLen) =>
{
    var opts = new ExtractionOptions { MinimumTextLength = minLen };

    if (text is not null)
    {
        var meta = SdMetaKit.Parse(text);
        PrintMeta(meta, format);
        return;
    }

    if (files.Length == 0)
    {
        Console.Error.WriteLine("Podaj plik lub użyj --text.");
        Environment.Exit(3);
        return;
    }

    if (files.Length == 1)
    {
        var meta = await ParseFile(files[0]);
        PrintMeta(meta, format);
        return;
    }

    var results = new List<object>();
    foreach (var file in files)
    {
        var meta = await ParseFile(file);
        results.Add(new { file = file.FullName, metadata = meta });
    }
    Console.WriteLine(JsonOutput.Serialize(results));

    async Task<SdMetadata> ParseFile(FileInfo f)
    {
        if (!f.Exists) { Console.Error.WriteLine($"Plik nie istnieje: {f.FullName}"); Environment.Exit(1); }
        return await SdMetaKit.ParseFileAsync(f.FullName, opts);
    }
},
parseFilesArg, parseTextOpt, parseFormatOpt, parseMinLengthOpt);

// ── batch ──────────────────────────────────────────────────────────────

var batchCommand = new Command("batch", "Parsuje wszystkie PNG/JPEG w katalogu");

var batchFolderArg = new Argument<DirectoryInfo>("folder", "Katalog do przeszukania");
var batchRecursiveOpt = new Option<bool>("--recursive", "Przeszukuj rekurencyjnie");
var batchOutOpt = new Option<FileInfo?>("--out", "Zapisz wynik do pliku JSON");
var batchMinLengthOpt = new Option<int>("--min-length", () => 150, "Minimalna długość tekstu parametrów");

batchCommand.AddArgument(batchFolderArg);
batchCommand.AddOption(batchRecursiveOpt);
batchCommand.AddOption(batchOutOpt);
batchCommand.AddOption(batchMinLengthOpt);

batchCommand.SetHandler(async (folder, recursive, outFile, minLen) =>
{
    if (!folder.Exists)
    {
        Console.Error.WriteLine($"Katalog nie istnieje: {folder.FullName}");
        Environment.Exit(1);
    }

    var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var files = Directory.EnumerateFiles(folder.FullName, "*.*", searchOpt)
        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                 || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                 || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

    var opts = new ExtractionOptions { MinimumTextLength = minLen };
    var results = new List<object>();

    foreach (var f in files)
    {
        try
        {
            var meta = await SdMetaKit.ParseFileAsync(f, opts);
            results.Add(new { file = f, metadata = meta });
        }
        catch (Exception ex)
        {
            results.Add(new { file = f, error = ex.Message });
            Console.Error.WriteLine($"Błąd: {f} — {ex.Message}");
        }
    }

    var json = JsonOutput.Serialize(results);
    if (outFile is not null)
        await File.WriteAllTextAsync(outFile.FullName, json);
    else
        Console.WriteLine(json);
},
batchFolderArg, batchRecursiveOpt, batchOutOpt, batchMinLengthOpt);

// ── inject ─────────────────────────────────────────────────────────────

var injectCommand = new Command("inject", "Wstrzykuje parametry SD do pliku PNG");

var injectFileArg = new Argument<FileInfo>("plik", "Plik PNG do modyfikacji");
var injectParamsOpt = new Option<string?>("--params", "Tekst parametrów do wstrzyknięcia");
var injectFromOpt = new Option<FileInfo?>("--from", "Skopiuj parametry z innego pliku PNG");

injectCommand.AddArgument(injectFileArg);
injectCommand.AddOption(injectParamsOpt);
injectCommand.AddOption(injectFromOpt);

injectCommand.SetHandler(async (file, paramsText, fromFile) =>
{
    if (!file.Exists) { Console.Error.WriteLine($"Plik nie istnieje: {file.FullName}"); Environment.Exit(1); }

    string? text = paramsText;

    if (text is null && fromFile is not null)
    {
        if (!fromFile.Exists) { Console.Error.WriteLine($"Plik źródłowy nie istnieje: {fromFile.FullName}"); Environment.Exit(1); }
        var sourceMeta = await SdMetaKit.ParseFileAsync(fromFile.FullName, ExtractionOptions.NoLengthFilter);
        text = sourceMeta.RawText;
    }

    if (string.IsNullOrWhiteSpace(text))
    {
        Console.Error.WriteLine("Podaj --params lub --from.");
        Environment.Exit(3);
    }

    var png = await File.ReadAllBytesAsync(file.FullName);
    var result = SdMetaKit.InjectParametersText(png, text!);
    // Atomic write: najpierw do pliku tymczasowego, potem rename
    var tmpPath = file.FullName + ".tmp";
    await File.WriteAllBytesAsync(tmpPath, result);
    File.Move(tmpPath, file.FullName, overwrite: true);
    Console.WriteLine($"Zaktualizowano: {file.FullName}");
},
injectFileArg, injectParamsOpt, injectFromOpt);

// ── blob ───────────────────────────────────────────────────────────────

var blobCommand = new Command("blob", "Koduje/dekoduje SdBlob");

var blobFileArg = new Argument<FileInfo?>("plik", "Plik PNG/JPEG do zakodowania jako blob")
    { Arity = ArgumentArity.ZeroOrOne };
var blobTextOpt = new Option<string?>("--text", "Surowy tekst parametrów do zakodowania");
var blobDecodeOpt = new Option<string?>("--decode", "Blob do zdekodowania (ciąg SDMK1:...)");

blobCommand.AddArgument(blobFileArg);
blobCommand.AddOption(blobTextOpt);
blobCommand.AddOption(blobDecodeOpt);

blobCommand.SetHandler(async (file, text, decodeBlob) =>
{
    if (decodeBlob is not null)
    {
        if (!SdMetaKit.TryDecodeBlob(decodeBlob, out var decoded))
        {
            Console.Error.WriteLine("Nie można zdekodować bloba.");
            Environment.Exit(1);
        }
        Console.WriteLine(JsonOutput.Serialize(decoded!));
        return;
    }

    SdMetadata meta;
    if (text is not null)
    {
        meta = SdMetaKit.Parse(text);
    }
    else if (file is not null)
    {
        if (!file.Exists) { Console.Error.WriteLine($"Plik nie istnieje: {file.FullName}"); Environment.Exit(1); }
        meta = await SdMetaKit.ParseFileAsync(file.FullName);
    }
    else
    {
        Console.Error.WriteLine("Podaj plik, --text lub --decode.");
        Environment.Exit(3);
        return;
    }

    Console.WriteLine(SdMetaKit.EncodeBlob(meta));
},
blobFileArg, blobTextOpt, blobDecodeOpt);

rootCommand.AddCommand(parseCommand);
rootCommand.AddCommand(batchCommand);
rootCommand.AddCommand(injectCommand);
rootCommand.AddCommand(blobCommand);

return await rootCommand.InvokeAsync(args);

// ── Helpers ────────────────────────────────────────────────────────────

static void PrintMeta(SdMetadata meta, string format)
{
    if (format == "json")
    {
        Console.WriteLine(JsonOutput.Serialize(meta));
        return;
    }

    if (!string.IsNullOrEmpty(meta.Prompt))
        Console.WriteLine($"Prompt:    {meta.Prompt}");
    if (!string.IsNullOrEmpty(meta.NegativePrompt))
        Console.WriteLine($"Negative:  {meta.NegativePrompt}");

    foreach (var (label, value) in SdMetadataDisplay.PriorityFields(meta))
        Console.WriteLine($"{label + ":",-25} {value}");

    foreach (var kv in meta.OtherParams)
        Console.WriteLine($"{kv.Key + ":",-25} {kv.Value}");
}
