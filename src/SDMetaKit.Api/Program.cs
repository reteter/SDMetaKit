using SDMetaKit;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SDMetaKit API", Version = "v1" });
});
builder.Services.AddProblemDetails();
builder.Services.Configure<FormOptions>(o =>
{
    o.ValueLengthLimit = 50_000_000;
    o.MultipartBodyLengthLimit = 50_000_000;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is not null)
        {
            var error = new
            {
                error = "internal_error",
                message = app.Environment.IsDevelopment()
                    ? feature.Error.Message
                    : "Wewnętrzny błąd serwera."
            };
            await context.Response.WriteAsJsonAsync(error);
        }
    });
});

app.UseRateLimiter();

var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
    app.UsePathBase(pathBase);

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
    var validationError = ValidateImageFile(file);
    if (validationError is not null) return validationError;

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
    var validationError = ValidateImageFile(file);
    if (validationError is not null) return validationError;

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
    var validationError = ValidateImageFile(file);
    if (validationError is not null) return validationError;

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
        return Results.BadRequest(new { error = "empty_text", message = "Pole text jest puste." });

    var meta = SdMetaKit.Parse(req.Text);
    return Results.Ok(new { blob = SdMetaKit.EncodeBlob(meta) });
})
.WithName("BlobFromText")
.WithTags("Blob");

app.Run();

// ── Helpers ────────────────────────────────────────────────────────────

static IResult? ValidateImageFile(IFormFile file)
{
    var allowed = new[] { "image/png", "image/jpeg" };
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "empty_file", message = "Plik jest pusty." });
    if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "invalid_content_type", message = "Akceptowane tylko image/png i image/jpeg." });
    return null;
}

// ── Modele żądań ───────────────────────────────────────────────────────

record ParseTextRequest(string Text);
record BlobRequest(string Blob);

public partial class Program { }
