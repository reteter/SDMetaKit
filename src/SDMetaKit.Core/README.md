# SDMetaKit.Core

Parse and write **Stable Diffusion** image metadata in the A1111/Automatic1111 and Forge text format.

Supports PNG `tEXt` chunks (`parameters` keyword) and JPEG EXIF (`UserComment`, `ImageDescription`).

## Install

```bash
dotnet add package SDMetaKit.Core
```

## Quick start

```csharp
using SDMetaKit;

// Parse a PNG or JPEG file
var meta = await SdMetaKit.ParseFileAsync(@"C:\outputs\image.png");
Console.WriteLine(meta.Prompt);
Console.WriteLine(meta.Steps);      // "20"
Console.WriteLine(meta.Sampler);    // "Euler a"
Console.WriteLine(meta.Seed);

// Parse raw parameters text (no file I/O)
const string text = """
    a cat on a windowsill
    Negative prompt: blurry, watermark
    Steps: 28, Sampler: DPM++ 2M, CFG scale: 7, Seed: 42, Size: 512x768, Model: sd_xl
    """;
var fromText = SdMetaKit.Parse(text);

// Inject parameters into a PNG (returns new byte array)
byte[] png = await File.ReadAllBytesAsync("input.png");
byte[] withMeta = SdMetaKit.InjectParametersText(png, text);

// Portable blob encoding (SDMK1:base64url(deflate(json)))
string blob = SdMetaKit.EncodeBlob(meta);
if (SdMetaKit.TryDecodeBlob(blob, out var roundTrip))
    Console.WriteLine(roundTrip!.Prompt);
```

## API surface

| Method | Description |
|--------|-------------|
| `ParseFileAsync(path)` | Extract + parse metadata from PNG/JPEG |
| `Parse(text)` | Parse A1111 parameters block from string |
| `InjectParametersText(png, text)` | Write `tEXt` chunk before IEND |
| `EncodeBlob` / `DecodeBlob` | Compact metadata transport format |
| `CreateExtractor()` / `CreateParser()` / `CreateWriter()` | DI-friendly factories |

## Extraction options

```csharp
// SDPack-style: accept short EXIF comments without length filter
var meta = await SdMetaKit.ParseFileAsync(path, ExtractionOptions.NoLengthFilter);
```

## Related projects

- [SDMetaKit](https://github.com/reteter/SDMetaKit) — CLI (`sdmk`), REST API, and this core library
- Used by [SDILab](https://github.com/reteter/SDILab) (SDPack, SDInfo, SDForgePanel)

## License

MIT — see [LICENSE](https://github.com/reteter/SDMetaKit/blob/main/LICENSE).