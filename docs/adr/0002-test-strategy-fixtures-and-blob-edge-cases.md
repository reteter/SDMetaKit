# ADR-0002 — Strategia testów: fixture end-to-end i edge cases SdBlobDecoder

- **Status:** Zaakceptowany
- **Data:** 2026-06-25
- **Kontekst wersji:** issue #3 — rozszerzenie pokrycia testowego

## Kontekst

Po wdrożeniu ADR-0001 (bugfixy) testy jednostkowe pokrywają parser i blob w izolacji. Brakuje dwóch warstw:

1. **Test end-to-end ekstrakcji** — `FileMetadataExtractor` odczytuje bajty PNG i przekazuje tekst do `A1111Parser`. Dotychczasowe testy weryfikują każdy z tych modułów osobno, ale nie weryfikują, że `FileMetadataExtractor` poprawnie wyciąga `PNG-tEXt` chunk i przekazuje go dalej.

2. **Kontrakt błędów `SdBlobDecoder`** — `Decode` rzuca różne wyjątki na różnych etapach (ArgumentException, FormatException, InvalidDataException, JsonException). `TryDecode` łapie wszystkie, ale nie ma testów granicznych dla każdej ścieżki błędu.

Trzy opcje dla fixture end-to-end: (a) prawdziwy plik PNG z generacji SD jako `EmbeddedResource`, (b) plik PNG generowany przez `PngChunkWriter` in-memory w teście, (c) oba.

---

## Decyzje

### D1 — Fixture PNG generowany in-memory zamiast pliku binarnego

**Decyzja:** Test E2E buduje fixture PNG w pamięci, używając `PngTestHelper.BuildMinimalPng()` + `SdMetaKit.InjectParametersText()`, bez żadnego zewnętrznego pliku w repozytorium.

```csharp
var rawPng = PngTestHelper.BuildMinimalPng();
var png    = SdMetaKit.InjectParametersText(rawPng, SampleParametersText);
// następnie zapisz do MemoryStream i przekaż do FileMetadataExtractor.ExtractAsync(Stream)
```

**Dlaczego nie plik binarny PNG?**
- Plik PNG z prawdziwej generacji SD waży 2–20 MB — znacząco zwiększa rozmiar repozytorium i spowalnia `git clone`.
- Plik binarny w repozytorium utrudnia diff i code review.
- `PngChunkWriter` jest już przetestowany jako samodzielny moduł — używanie go w fixture E2E zamienia to w test integracyjny write→read, który ma wyższą wartość diagnostyczną niż test z zewnętrznym plikiem (wiemy dokładnie co zostało zapisane).
- Jeśli `PngChunkWriter` ma błąd, test E2E go ujawni.

**Dlaczego nie oba?**
Prawdziwy plik PNG mógłby testować `EXIF-UserComment` / `EXIF-ImageDescription` path (JPEG z SD metadata). To jest osobny scenariusz — odłożony do issue #6 (real-file fixtures dla JPEG/EXIF), poza zakresem issue #3.

**Zakres E2E testu:**
- Zapisuje znany tekst parametrów jako `PNG-tEXt` chunk (`parameters:…`)
- Przekazuje bufor do `FileMetadataExtractor.ExtractAsync(Stream, ExtractionOptions.NoLengthFilter)`
- Weryfikuje, że zwrócony `SdMetadata` ma poprawne pola (Prompt, Steps, Model, Sha256)

---

### D2 — Kontrakt błędów `SdBlobDecoder.Decode` testowany przez `TryDecode`

**Decyzja:** Testy edge cases korzystają wyłącznie z `TryDecode` (returns false) zamiast sprawdzać konkretny typ wyjątku z `Decode`. Jeden test adicional weryfikuje, że `Decode` rzuca wyjątek (nie zwraca null) dla ewidentnie złego wejścia.

**Cztery ścieżki błędów objęte testami:**

| Przypadek | Gdzie się sypie | Oczekiwany wynik `TryDecode` |
|-----------|-----------------|------------------------------|
| Pusty/whitespace string | `ArgumentException` w linii 16 | false, meta == null |
| Prawidłowy string, nieznany prefix (`SDMK2:…`) | `FormatException` w linii 20 | false |
| `SDMK1:` + uszkodzony base64 | `FormatException` z `Convert.FromBase64String` | false |
| `SDMK1:` + poprawny base64, uszkodzony deflate | `InvalidDataException` z `DeflateStream` | false |
| `SDMK1:` + poprawny base64+deflate, niepoprawny JSON | `JsonException` z `JsonSerializer.Deserialize` | false |

**Dlaczego nie testować konkretnych typów wyjątków przez `Decode`?**
`TryDecode` jest publicznym API do użytku produkcyjnego (API endpoint, CLI). Callerzy używają `TryDecode` — zachowanie `Decode` jest implementacyjnym szczegółem. Jeśli zdecydujemy zmienić wyjątek `ArgumentException` na `FormatException` dla pustego stringa, nie chcemy łamać testów.

Wyjątek: jeden test sprawdza, że `Decode` rzuca _cokolwiek_ dla null-like wejścia — dokumentuje kontrakt „nigdy nie zwraca null cicho".

**Jak wygenerować uszkodzony deflate bez biblioteki zewnętrznej?**
```csharp
// Kodujemy poprawny base64 losowych bajtów (nie-deflate):
var junk = Convert.ToBase64String(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 })
    .Replace('+', '-').Replace('/', '_').TrimEnd('=');
var blob = "SDMK1:" + junk;
```

**Jak wygenerować poprawny deflate + niepoprawny JSON?**
```csharp
// Deflatujemy ręcznie niepoprawny JSON:
using var ms = new MemoryStream();
using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest))
    deflate.Write("not json"u8);
var blob = "SDMK1:" + Convert.ToBase64String(ms.ToArray())
    .Replace('+', '-').Replace('/', '_').TrimEnd('=');
```

Obie metody nie wymagają dostępu do internals `SdBlobDecoder` — klasa jest `public static`, a `DeflateStream` jest standardową biblioteką.

---

### D3 — Testy edge cases w istniejących klasach, bez nowych plików

**Decyzja:**
- Testy SdBlobDecoder trafiają do `SdBlobTests.cs` (nowa klasa zagnieżdżona lub bezpośrednio w pliku)
- Test E2E ekstrakcji trafia do nowego `FileMetadataExtractorTests.cs`
- Brak folderu `fixtures/` — fixture PNG jest generowany w teście

**Dlaczego nowy plik dla ekstraktora?** `A1111ParserTests.cs` i `PngChunkWriterTests.cs` są skupione na jednym typie. Ekstraktor ma własne zachowanie (wybór kandydata, MinimumTextLength, CanSeek path) — plik testowy dla niego zachowuje czytelność i isolation.

---

## Skutki

- Repozytorium pozostaje bez plików binarnych w katalogu testów.
- Test E2E weryfikuje pełny pipeline write→extract→parse na PNG-tEXt.
- Edge cases bloba dokumentują kontrakt `TryDecode` dla integratorów.
- Ścieżka JPEG/EXIF pozostaje nieprzetestowana — do osobnego issue.
- Liczba testów wzrośnie z 27 do ~35 (+5 blob edge cases, +3 E2E ekstraktor).

## Implementacja

| Decyzja | Plik | Nowy? |
|---------|------|-------|
| D1 — E2E ekstrakcja PNG-tEXt | `tests/SDMetaKit.Core.Tests/FileMetadataExtractorTests.cs` | ✅ |
| D2 — blob edge cases | `tests/SDMetaKit.Core.Tests/SdBlobTests.cs` | rozszerzenie |
| D3 — brak fixtures/ | — | — |

## Weryfikacja

Implementacja została zweryfikowana lokalnie: `dotnet test` — 43/43 testów zielonych
(38 `SDMetaKit.Core.Tests`, 5 `SDMetaKit.Api.Tests`).
