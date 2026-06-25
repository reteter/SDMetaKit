# SDMetaKit

Parse, write and transport **Stable Diffusion** image metadata. Supports A1111 / Automatic1111 / Forge text format.

- **Extract** metadata from PNG `tEXt` chunks and JPEG EXIF (`UserComment`, `ImageDescription`)
- **Parse** the raw A1111 parameter block into structured fields
- **Write** `parameters` chunk back into PNG images
- **Transport** metadata as a compact, self-contained blob (`SDMK1:...`)

## Features

| Capability | Core library | CLI (`sdmk`) | REST API |
|------------|:------------:|:-------------:|:--------:|
| Parse PNG/JPEG files | ✅ | `parse` | `POST /v1/parse` |
| Parse raw text | ✅ | `parse --text` | `POST /v1/parse/text` |
| Inject params into PNG | ✅ | `inject` | `POST /v1/inject` |
| Encode/decode blob | ✅ | `blob` | `POST /v1/blob/*` |
| Batch directory | — | `batch` | — |
| Health check | — | — | `GET /health` |

---

## Getting Started

### 1. Install the NuGet package (for .NET projects)

```bash
dotnet add package SDMetaKit.Core
```

### 2. Parse a PNG file

```csharp
using SDMetaKit;

var meta = await SdMetaKit.ParseFileAsync(@"C:\images\output.png");

Console.WriteLine(meta.Prompt);           // "a cat on a windowsill"
Console.WriteLine(meta.Steps);            // "20"
Console.WriteLine(meta.Sampler);          // "DPM++ 2M Karras"
Console.WriteLine(meta.Model);            // "v1-5-pruned"
Console.WriteLine(meta.ModelHash);        // "cc6cb27103"
Console.WriteLine(meta.Seed);             // "1234567890"
Console.WriteLine(meta.Size);             // "512x768"
Console.WriteLine(meta.CfgScale);         // "7"
Console.WriteLine(meta.NegativePrompt);   // "ugly, blurry"
```

### 3. Parse raw text (no file I/O)

```csharp
const string text = """
    a cat on a windowsill
    Negative prompt: blurry, watermark
    Steps: 28, Sampler: DPM++ 2M, CFG scale: 7, Seed: 42, Size: 512x768, Model: sd_xl
    """;

var meta = SdMetaKit.Parse(text);
Console.WriteLine(meta.Steps);   // "28"
Console.WriteLine(meta.Sampler); // "DPM++ 2M"
```

### 4. Inject metadata into a PNG

```csharp
byte[] png = await File.ReadAllBytesAsync("input.png");
byte[] withMeta = SdMetaKit.InjectParametersText(png, text);
await File.WriteAllBytesAsync("output.png", withMeta);
```

### 5. Portable blob encoding

```csharp
// Encode metadata to a compact string: SDMK1:base64url(deflate(json))
string blob = SdMetaKit.EncodeBlob(meta);

// Decode it back (safe variant — no exceptions)
if (SdMetaKit.TryDecodeBlob(blob, out var decoded))
    Console.WriteLine(decoded!.Prompt);
```

---

## CLI (`sdmk`)

Install as a global .NET tool:

```bash
dotnet tool install --global SDMetaKit.Cli
# or use the NuGet package
# dotnet tool install --global SDMetaKit.Cli --version 1.0.0
```

### `sdmk parse` — parse one or more files

```bash
# Single file
sdmk parse output.png

# Multiple files (JSON output)
sdmk parse output1.png output2.png output3.png

# Raw text input
sdmk parse --text "a cat\nSteps: 20, Sampler: Euler"

# JSON format
sdmk parse output.png --format json

# Custom minimum text length (default: 150)
sdmk parse output.png --min-length 0
```

**Example output:**
```
Prompt:    a cat on a windowsill
Negative:  ugly, blurry
Steps:                    20
Sampler:                  DPM++ 2M Karras
CFG scale:                7
Seed:                     1234567890
Size:                     512x512
Model:                    v1-5-pruned
Model hash:               cc6cb27103
```

### `sdmk batch` — parse all PNG/JPEG in a directory

```bash
# Current directory
sdmk batch .

# Recursive
sdmk batch ~/outputs --recursive

# Save results to JSON
sdmk batch . --out results.json

# Custom minimum text length
sdmk batch . --min-length 100
```

### `sdmk inject` — write parameters into a PNG

```bash
# Inject raw text parameters
sdmk inject input.png --params "a cat\nSteps: 20\nCFG scale: 7"

# Inject parameters from another PNG
sdmk inject input.png --from source.png

# (The original file is updated atomically — a .tmp file is written first, then renamed)
```

### `sdmk blob` — encode/decode metadata blobs

```bash
# Encode a PNG file to blob
sdmk blob output.png
# Output: SDMK1:ZGVmbGF0ZWRfamFzb25f...

# Encode raw text to blob
sdmk blob --text "a cat\nSteps: 20"

# Decode a blob
sdmk blob --decode "SDMK1:ZGVmbGF0ZWRfamFzb25f..."
```

---

## REST API

The API runs on `http://localhost:5000` by default. Swagger UI is available at `/swagger` in development mode.

### `POST /v1/parse` — upload and parse a PNG/JPEG

```bash
curl -X POST http://localhost:5000/v1/parse \
  -F "file=@output.png" \
  -F "min_length=0"
```

**Response:**
```json
{
  "prompt": "a cat",
  "negative_prompt": "ugly",
  "steps": "20",
  "sampler": "DPM++ 2M",
  "cfg_scale": "7",
  "seed": "42",
  "size": "512x512",
  "model": "v1-5-pruned",
  "model_hash": "cc6cb27103",
  "sha256": "abcdef1234567890...",
  "source_kind": "PNG-tEXt",
  "other_params": {},
  "raw": { "PNG-tEXt/Textual Data": "parameters: ..." }
}
```

### `POST /v1/parse/text` — parse raw text

```bash
curl -X POST http://localhost:5000/v1/parse/text \
  -H "Content-Type: application/json" \
  -d '{"text": "a cat\nSteps: 20, Sampler: Euler"}'
```

### `POST /v1/inject` — inject parameters into a PNG

```bash
curl -X POST http://localhost:5000/v1/inject \
  -F "file=@input.png" \
  -F "parameters_text=a cat\nSteps: 20"
```

### `POST /v1/blob` — encode metadata to blob

```bash
curl -X POST http://localhost:5000/v1/blob \
  -H "Content-Type: application/json" \
  -d '{"prompt":"a cat","steps":"20"}'
# Response: { "blob": "SDMK1:..." }
```

### `POST /v1/blob/decode` — decode a blob

```bash
curl -X POST http://localhost:5000/v1/blob/decode \
  -H "Content-Type: application/json" \
  -d '{"blob": "SDMK1:..."}'
```

### `POST /v1/blob/from-file` — encode file metadata to blob

```bash
curl -X POST http://localhost:5000/v1/blob/from-file \
  -F "file=@output.png"
```

### `POST /v1/blob/from-text` — encode raw text to blob

```bash
curl -X POST http://localhost:5000/v1/blob/from-text \
  -H "Content-Type: application/json" \
  -d '{"text": "a cat\nSteps: 20"}'
```

### `GET /health` — health check

```bash
curl http://localhost:5000/health
# { "status": "ok", "version": "1.0.0" }
```

**API limits (configurable):**
- Max file upload: **50 MB**
- Allowed MIME types: `image/png`, `image/jpeg`
- Rate limit: **100 requests/minute** per IP

---

## Core API Reference

| Method | Description |
|--------|-------------|
| `ParseFileAsync(path, options?, ct?)` | Extract + parse metadata from PNG/JPEG file |
| `Parse(text)` | Parse A1111/Forge parameters from a string |
| `InjectParametersText(png, text)` | Inject `tEXt` chunk before IEND, returns new buffer |
| `EncodeBlob(meta)` | Encode `SdMetadata` to `SDMK1:...` compact blob |
| `DecodeBlob(blob)` | Decode blob, throws on invalid input |
| `TryDecodeBlob(blob, out meta?)` | Safe decode — returns `false` on invalid input |
| `CreateExtractor()` | DI-friendly factory: `IMetadataExtractor` |
| `CreateParser()` | DI-friendly factory: `IMetadataParser` |
| `CreateWriter()` | DI-friendly factory: `IPngWriter` |

---

## `SdMetadata` Model

```csharp
public sealed record SdMetadata
{
    string Prompt { get; init; }                // Positive prompt
    string NegativePrompt { get; init; }        // Negative prompt (if present)

    // Priority fields (parsed from the Steps: ... block)
    string? Steps, Sampler, ScheduleType,
            CfgScale, DistilledCfgScale,
            Seed, Size,
            Model, ModelHash,
            DenoisingStrength,
            HiresUpscale, HiresSteps, HiresUpscaler,
            HiresCfgScale, HiresModule1;

    string? Sha256;                             // File SHA-256 (hex, lowercase)
    string? SourceKind;                         // "PNG-tEXt", "EXIF-UserComment", ...

    IReadOnlyDictionary<string, string> OtherParams;   // Unknown keys (ControlNet, ADetailer, ...)
    IReadOnlyDictionary<string, string> Raw;            // All raw image tags
    string? RawText;                                    // Original unparsed parameter text
}
```

All fields are `string?` or `string` — no numeric conversion.
JSON property names follow the `snake_case` convention used by `SdPack`.

---

## `ExtractionOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `MinimumTextLength` | `150` | Ignore candidates shorter than this (set to `0` for SDPack-style) |
| `PreferStepsCandidate` | `true` | Prefer candidate containing `Steps:` |
| `ComputeSha256` | `true` | Compute SHA-256 of file bytes |
| `CollectAllTags` | `true` | Collect all image tags into `Raw` |

Pre-built instances:

```csharp
ExtractionOptions.Default              // MinimumTextLength = 150
ExtractionOptions.NoLengthFilter       // MinimumTextLength = 0
```

---

## Blob Format

The blob is a compact, self-contained string for transporting metadata without files:

```
SDMK1:<base64url(deflate(utf8(json(payload))))>
```

- **Deterministic**: same input → same blob string
- **Safe**: `TryDecodeBlob` never throws
- **Versioned**: prefix `SDMK1:` (future versions may use `SDMK2:`)
- **Compact**: deflate compression keeps blobs small

---

## Advanced Examples

### Bridge between file and text

```csharp
// Extract from PNG, then inject into another
var source = await SdMetaKit.ParseFileAsync("source.png");
var buffer = await File.ReadAllBytesAsync("target.png");
var injected = SdMetaKit.InjectParametersText(buffer, source.RawText!);
```

### Batch processing with error resilience

```csharp
var directory = @"C:\gallery";
var results = new List<object>();

foreach (var file in Directory.EnumerateFiles(directory, "*.png"))
{
    try
    {
        var meta = await SdMetaKit.ParseFileAsync(file,
            new ExtractionOptions { ComputeSha256 = false });
        results.Add(new { file, metadata = meta });
    }
    catch (Exception ex)
    {
        results.Add(new { file, error = ex.Message });
    }
}
```

### MVVM display helper

```csharp
foreach (var (label, value) in SdMetadataDisplay.PriorityFields(meta))
    Console.WriteLine($"{label,-25} {value}");
// Steps:                    20
// Sampler:                  DPM++ 2M
// CFG scale:                7
```

---

## Related Projects

- [SDMetaKit](https://github.com/reteter/SDMetaKit) — this repository (CLI, API, Core)
- [SDILab](https://github.com/reteter/SDILab) — WinUI desktop tools (SDPack, SDInfo, SDForgePanel)

## License

MIT — see [LICENSE](LICENSE).
