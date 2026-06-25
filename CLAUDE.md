# SDMetaKit — CLAUDE.md

Biblioteka parsowania metadanych Stable Diffusion (A1111/Forge), wydzielona z SDILab.

## Stack

- .NET 8, `net8.0`
- NuGet: `MetadataExtractor` (Core), `System.CommandLine 2.x` (Cli), `Microsoft.AspNetCore.OpenApi` (Api)
- Build: `dotnet build`, `dotnet test`
- Testy: xUnit, 27 testów (22 Core + 5 Api)

## Struktura

```
SDMetaKit/
  SDMetaKit.slnx
  src/
    SDMetaKit.Core/          net8.0 — NuGet-ready
    SDMetaKit.Cli/           sdmk.exe — System.CommandLine
    SDMetaKit.Api/           ASP.NET Core minimal API
  tests/
    SDMetaKit.Core.Tests/    parser, blob, opcje
    SDMetaKit.Api.Tests/     health, parse/text, blob round-trip
```

## Kluczowe pliki

| Plik | Opis |
|------|------|
| `src/SDMetaKit.Core/SdMetaKit.cs` | Statyczna fasada — `ParseFileAsync`, `Parse`, `InjectParametersText`, `EncodeBlob`, `DecodeBlob` |
| `src/SDMetaKit.Core/Parsing/A1111Parser.cs` | Parser tekstu parametrów A1111/Forge |
| `src/SDMetaKit.Core/Extraction/FileMetadataExtractor.cs` | PNG-tEXt + EXIF UserComment + EXIF ImageDescription |
| `src/SDMetaKit.Core/Blob/SdBlobEncoder.cs` | Format `SDMK1:base64url(deflate(json))` |
| `src/SDMetaKit.Core/Blob/SdBlobDecoder.cs` | Dekodowanie bloba → SdMetadata |
| `src/SDMetaKit.Core/Writing/PngChunkWriter.cs` | Zapis tEXt chunk przed IEND |
| `src/SDMetaKit.Core/Models/SdMetadata.cs` | Główny model (`sealed record`) |

## CLI — sdmk

```
sdmk parse <plik>
sdmk parse --text "Steps: 20, ..."
sdmk batch <folder> [--recursive] [--out plik.json]
sdmk inject <plik.png> --params "..."
sdmk inject <plik.png> --from <źródło.png>
sdmk blob <plik>
sdmk blob --text "..."
sdmk blob --decode "SDMK1:..."
```

## REST API

```
POST /v1/parse            multipart {file}
POST /v1/parse/text       json {text}
POST /v1/inject           multipart {file, ...}
POST /v1/blob             json {metadata}
POST /v1/blob/decode      json {blob}
POST /v1/blob/from-file   multipart {file}
POST /v1/blob/from-text   json {text}
GET  /health
GET  /swagger             (dev)
```

## Konwencje

- Język komentarzy i UI: **polski** (komunikaty CLI/API: angielski)
- `SdMetadata` to `sealed record` — wymagane dla `with` expressions
- `ExtractionOptions.NoLengthFilter` (MinimumTextLength = 0) — dla SDPack
- `InternalsVisibleTo(SDMetaKit.Core.Tests)` w `.csproj` — umożliwia test `A1111Parser` (internal)

## Powiązania z SDILab

SDMetaKit.Core jest project-reference w SDILab.sln. Zmiany parsowania idą tu, nie do SDILab. SDILab trzyma tylko cienkie adaptery:
- `SDPack/SdPackExifExtractor.cs`
- `SDInfo/src/ExifParser.cs`
- `SDForgePanel/PngMetadataWriter.cs`

## Zarządzanie zadaniami

GitHub Issues: https://github.com/reteter/SDMetaKit
