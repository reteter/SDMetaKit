using SDMetaKit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SDMetaKit API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── /health ────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0.0" }))
    .WithName("Health")
    .WithTags("Infrastructure");

// ── /v1/parse — upload pliku ───────────────────────────────────────────

app.MapPost("/v1/parse", async (IFormFile file, int? min_length) =>
{
    var opts = new ExtractionOptions { MinimumTextLength = min_length ?? 150 };

    using var stream = file.OpenReadStream();
    var extractor = SdMetaKit.CreateExtractor();
    var raw = await extractor.ExtractAsync(stream, opts);

    if (string.IsNullOrWhiteSpace(raw.ParametersText))
        return Results.UnprocessableEntity(new { error = "no_sd_metadata", message = "Brak metadanych SD w podanym pliku." });

    var meta = SdMetaKit.Parse(raw.ParametersText) with
    {
        Sha256 = raw.Sha256,
        SourceKind = raw.SourceKind,
        Raw = raw.AllTags
    };
    return Results.Ok(meta);
})
.WithName("ParseFile")
.WithTags("Parse")
.DisableAntiforgery();

// ── /v1/parse/text — surowy tekst ─────────────────────────────────────

app.MapPost("/v1/parse/text", (ParseTextRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "empty_text", message = "Pole text jest puste." });

    var meta = SdMetaKit.Parse(req.Text);
    return Results.Ok(meta);
})
.WithName("ParseText")
.WithTags("Parse");

// ── /v1/inject ─────────────────────────────────────────────────────────

app.MapPost("/v1/inject", async (IFormFile file, IFormFile? parameters_file, string? parameters_text) =>
{
    string? text = parameters_text;

    if (text is null && parameters_file is not null)
    {
        using var ps = parameters_file.OpenReadStream();
        var raw = await SdMetaKit.CreateExtractor().ExtractAsync(ps, ExtractionOptions.NoLengthFilter);
        text = raw.ParametersText;
    }

    if (string.IsNullOrWhiteSpace(text))
        return Results.BadRequest(new { error = "no_params", message = "Podaj parameters_text lub parameters_file." });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var result = SdMetaKit.InjectParametersText(ms.ToArray(), text!);

    return Results.File(result, "image/png", file.FileName);
})
.WithName("Inject")
.WithTags("Write")
.DisableAntiforgery();

// ── /v1/blob ───────────────────────────────────────────────────────────

app.MapPost("/v1/blob", (SdMetadata meta) =>
    Results.Ok(new { blob = SdMetaKit.EncodeBlob(meta) }))
.WithName("EncodeBlob")
.WithTags("Blob");

app.MapPost("/v1/blob/decode", (BlobRequest req) =>
{
    if (!SdMetaKit.TryDecodeBlob(req.Blob, out var meta))
        return Results.BadRequest(new { error = "invalid_blob", message = "Nie można zdekodować bloba." });
    return Results.Ok(meta);
})
.WithName("DecodeBlob")
.WithTags("Blob");

app.MapPost("/v1/blob/from-file", async (IFormFile file) =>
{
    using var stream = file.OpenReadStream();
    var raw = await SdMetaKit.CreateExtractor().ExtractAsync(stream);

    if (string.IsNullOrWhiteSpace(raw.ParametersText))
        return Results.UnprocessableEntity(new { error = "no_sd_metadata", message = "Brak metadanych SD w podanym pliku." });

    var meta = SdMetaKit.Parse(raw.ParametersText) with
    {
        Sha256 = raw.Sha256,
        SourceKind = raw.SourceKind,
        Raw = raw.AllTags
    };
    return Results.Ok(new { blob = SdMetaKit.EncodeBlob(meta) });
})
.WithName("BlobFromFile")
.WithTags("Blob")
.DisableAntiforgery();

app.MapPost("/v1/blob/from-text", (ParseTextRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "empty_text" });

    var meta = SdMetaKit.Parse(req.Text);
    return Results.Ok(new { blob = SdMetaKit.EncodeBlob(meta) });
})
.WithName("BlobFromText")
.WithTags("Blob");

app.Run();

// ── Modele żądań ───────────────────────────────────────────────────────

record ParseTextRequest(string Text);
record BlobRequest(string Blob);

public partial class Program { }
