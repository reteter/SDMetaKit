# SDMetaKit — Perspektywy rozwoju

> Ostatnia aktualizacja: 2026-06-25

## 1. Rozszerzenie formatów

### JPEG/EXIF (issue #6 z ADR-0002)
`FileMetadataExtractor` już obsługuje odczyt `EXIF-UserComment` i `EXIF-ImageDescription`, ale brakuje:
- Testów z prawdziwymi fixture JPEG z metadanymi SD.
- Potwierdzenia, że minimum-length filter i PriotityStepsCandidate działają poprawnie dla JPEG.
- Ewentualnie: wsparcia dla `EXIF-XMP` (A1111 webui od pewnej wersji zapisuje tam metadane).

### WebP
- `MetadataExtractor` już obsługuje WebP — rozszerzenie `FileMetadataExtractor` o obsługę `.webp` jest stosunkowo tanie.
- Potrzebne: identyfikacja chunku/metadanych SD w WebP (EXIF, XMP lub własny chunk).
- Nowe testy E2E.

### ComfyUI, InvokeAI, NovelAI
- Inne formaty metadata — ComfyUI używa JSON-embedded w PNG-tEXt pod kluczem `prompt` (węzły), InvokeAI używa własnego schematu.
- Można rozważyć strategię: `IMetadataParser` → `A1111Parser` (domyślny) + opcjonalne parsery do rejestracji.
- Format bloba (`SDMK1:...`) jest niezależny od źródła.

### zTXt / iTXt (kompresowane PNG chunk)
- `PngChunkWriter` obsługuje tylko `tEXt`. Część narzędzi SD zapisuje metadane w kompresowanych chunkach.
- W dłuższej perspektywie: wsparcie dla odczytu `zTXt` w `FileMetadataExtractor`.

---

## 2. Architektura

### DI-friendly builder / ISdMetadataService
- Obecnie `SdMetaKit` to statyczna fasada z prywatnymi instancjami parsera/ekstraktora.
- Propozycja: `SdMetadataService : ISdMetadataService` z konfigurowalnymi opcjami i loggerem.
- Statyczna fasada pozostaje jako wrapper dla prostych przypadków.

### Abstrakcja IFileSystem
- `FileMetadataExtractor` bezpośrednio czyta pliki przez `File.ReadAllBytesAsync`.
- `IFileSystem` pozwoliłby na testowanie ekstrakcji bez fizycznych plików i ułatwiłby mockowanie.

### Logging
- Brak `ILogger` w całym solution. W CLI i API przydadzą się logi błędów śledzące:
  - Nieudane parsowanie pliku.
  - Blob decode failure.
  - Duże pliki (>50 MB) jako warning.

### Streaming vs pełny bufor
- `FileMetadataExtractor` ładuje cały plik do pamięci (`byte[]`). Dla plików >100 MB to problem.
- Długa perspektywa: strumieniowa ekstrakcja, gdzie MetadataExtractor czyta bezpośrednio z `FileStream`.

---

## 3. API — skalowalność i bezpieczeństwo

- **Rate limiting** — `AddRateLimiter` z `FixedWindowLimiter` lub `TokenBucketLimiter` przed publicznym deploymentem.
- **Request size limits** — `[RequestSizeLimit]` dla endpointów uploadujących pliki.
- **Walidacja modeli** — `FluentValidation` dla `ParseTextRequest`, `BlobRequest` itp.
- **Global exception handler** — `AddProblemDetails` + `UseExceptionHandler` dla spójnych odpowiedzi błędów.
- **Auth** — API key lub JWT (zależnie od deploymentu).
- **OpenAPI/Swagger w prod** — obecnie tylko w Development. Rozważyć publikację specyfikacji.
- **Wersjonowanie URL** — obecnie `/v1/` jest hardkodowane. Wprowadzić routing wersji dla `/v2/` w przyszłości.

---

## 4. CLI

- **Progress reporting** — dla `batch` przy tysiącach plików (pasek postępu lub licznik).
- **Output templates** — użytkownik definiuje format wyjścia (`--template "{Prompt} | {Model}"`).
- **Dry-run dla inject** — `--dry-run` symuluje zapis bez modyfikacji pliku.
- **Filtry zaawansowane w batch** — regex na ścieżce, minimalna data modyfikacji, rozmiar pliku.
- **Glob patterns** — zamiast tylko katalogu: `sdmk batch '**/*.png'`.
- **JSON output dla pojedynczego parse** — obecnie tylko `--format json` dla wielu plików.

---

## 5. DevOps

### Docker (issue #4)
- Multi-stage Dockerfile dla `SDMetaKit.Api`.
- Obraz gotowy do `docker run -p 5000:5000 sdmetakit-api`.
- Healthcheck endpoint + readiness.

### CI/CD
- **Code coverage** — dodać zbieranie pokrycia do CI (coverlet + ReportGenerator).
- **Dependabot** — automatyczne aktualizacje NuGet.
- **Analyzers** — `StyleCop.Analyzers`, `Microsoft.CodeAnalysis.NetAnalyzers`.
- **Trivy / Grype** — skanowanie bezpieczeństwa zależności w CI.

### Wersjonowanie
- Zsynchronizować wersje `SDMetaKit.Core` i `SDMetaKit.Cli` lub udokumentować niezależny schemat.
- Rozważyć `MinVer` lub `NerdBank.GitVersioning` do automatycznego wersjonowania z tagów gita.

---

## 6. Jakość kodu

- **Usunięcie duplikacji** — `MergeRaw` w trzech miejscach, `KnownPriorityKeys` w dwóch (patrz issues #10, #11).
- **Timeout w Regex** — ochrona przed ReDoS (issue #7).
- **Stałe dla magic strings** — `SourceKind`, prefix bloba itp.
- **Crc32** — zduplikowana implementacja w `PngChunkWriter` i `PngTestHelper` (drobiazg, ale warto przenieść do helpera).
- **Więcej testów** — coverage dla CLI (obecnie brak), coverage dla API (5 testów).

---

## 7. Długofalowe wizje

- **sdmeta — format pliku/metadata** — rozszerzenie formatu bloba SDMK o wersjonowanie, podpisywanie (np. HMAC) i metadane kontekstowe (data generacji, aplikacja, GPU).
- **Pluginy parserów** — zewnętrzne biblioteki rejestrujące `IMetadataParser` dla różnych silników SD.
- **GUI** — lekkie okienko WinUI do drag-and-drop i podglądu metadanych (port z SDILab).

---

Zobacz też: `docs/adr/0002-test-strategy-fixtures-and-blob-edge-cases.md` (issue #6 — real-file fixtures dla JPEG).
