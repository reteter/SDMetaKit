# ADR-0001 — Robustność PngChunkWriter i A1111Parser: trzy bugi z code review

- **Status:** Zaakceptowany (zaimplementowany 2026-06-25)
- **Data:** 2026-06-25
- **Kontekst wersji:** SDMetaKit initial release — przed publikacją NuGet

## Kontekst

Code review initial commitu ujawnił trzy potwierdzone błędy w krytycznej ścieżce parsowania i zapisu PNG. Wszystkie trzy dotyczą danych wejściowych pochodzących z zewnątrz (pliki PNG od użytkowników, tekst parametrów z promptów) i mogą się zmaterializować w normalnym użytkowaniu.

### Bug 1 — PngChunkWriter: integer overflow przy odczycie długości chunka

`FindIendOffset` rzutuje `uint length` na `int` bez sprawdzenia zakresu:

```csharp
pos += 8 + (int)length + 4;
```

Dla `length >= 0x80000000` wynik rzutowania jest ujemny. `pos` cofa się, pętla albo kręci się w nieskończoność (DoS), albo `ReadUInt32BE` rzuca `IndexOutOfRangeException`, który jest połykany przez `catch {}` — caller dostaje HTTP 200 z nieprzmodyfikowanym PNG bez sygnału błędu. Wektor: endpoint `/v1/inject` przyjmuje `IFormFile` z zewnątrz. PNG spec § 2.3 limituje chunk data length do 2^31−1, ale parser nie egzekwował tego ograniczenia.

### Bug 2 — A1111Parser: regex `^Steps:` trafia w prompt pozytywny

`stepsMatch` szuka `^Steps:` w całym `parametersText` i bierze pierwsze dopasowanie:

```csharp
var stepsMatch = Regex.Match(parametersText, @"(?m)^Steps:", RegexOptions.Multiline);
```

Jeśli prompt pozytywny zawiera `Steps:` na początku linii (np. _"painting with\nSteps: 1, 2, 3\n..."_), `stepsMatch.Index` trafia w treść promptu. Skutki: prompt obcięty, negativePrompt pusty, `ParseKeyValue` startuje od środka promptu i parsuje go jako blok parametrów — pola `Sampler`, `CFG scale`, `Model` itp. dostają błędne wartości lub null.

### Bug 3 — A1111Parser: `break` w ParseKeyValue przerywa parsowanie przy linii bez `:`

```csharp
if (i >= len || input[i] != ':') break;
```

`break` wychodzi z zewnętrznej pętli `while`. Jeśli blok parametrów zawiera linię bez `:` (np. pusta linia, `Wildcard prompt` bez wartości, komentarz pluginu), parser porzuca wszystkie dalsze parametry. Pola takie jak `CFG scale`, `Seed`, `Model` pojawiające się PO tej linii zwrócą `null`.

---

## Decyzje

### D1 — Zabezpieczenie przed overflow w FindIendOffset

**Decyzja:** Przed rzutowaniem na `int` weryfikujemy, że `length` mieści się w bezpiecznym zakresie. PNG spec limituje chunk data do 2^31−1; my dodajemy dodatkowy margines na nagłówek (8 B) i CRC (4 B):

```csharp
private static int FindIendOffset(byte[] png, int start)
{
    var pos = start;
    while (pos + 8 <= png.Length)
    {
        var length = ReadUInt32BE(png, pos);
        if (length > int.MaxValue - 12) return -1;   // chunk nie mieści się w buforze
        var type = Encoding.ASCII.GetString(png, pos + 4, 4);
        if (type == "IEND") return pos;
        pos += 8 + (int)length + 4;
    }
    return -1;
}
```

**Dlaczego return -1, nie wyjątek?** `InjectParametersText` traktuje brak IEND jako sygnał do zwrotu oryginału bez modyfikacji — to istniejący kontrakt metody. Złośliwy chunk jest równoważny z brakującym IEND dla naszych celów: nie injektujemy. Zachowanie jest bezpieczne i nie wymaga zmiany callera.

**Dlaczego nie `long`?** Zmiana `pos` na `long` wymagałaby przepisania wszystkich wywołań `ReadUInt32BE` i indeksowania tablicy. Guard na wejściu jest prostszy i wystarczający — PNG większy niż 2 GB i tak nie przejdzie przez `File.ReadAllBytesAsync` na 32-bitowym systemie.

---

### D2 — Zakotwiczenie wyszukiwania `Steps:` do bloku za negatywnym promptem

**Decyzja:** `stepsMatch` powinien szukać `Steps:` wyłącznie w tekście PO negatywnym prompcie (lub od początku, jeśli negative prompt nie istnieje). Regex pozostaje bez zmian; zmienia się punkt startowy wyszukiwania:

```csharp
// Po znalezieniu negMatch (lub od pozycji 0 gdy brak negMatch):
int searchFrom = negMatch.Success ? negMatch.Index : 0;
var stepsMatch = Regex.Match(
    parametersText, @"(?m)^Steps:",
    RegexOptions.Multiline,
    TimeSpan.Zero,
    searchFrom);  // <-- przeciążenie z startAt
```

Alternatywnie — prostsze i bez przeciążenia z startAt (które nie istnieje w `Regex.Match`):

```csharp
int searchFrom = negMatch.Success ? negMatch.Index + negMatch.Length : 0;
var stepsMatch = Regex.Match(
    parametersText[searchFrom..], @"(?m)^Steps:", RegexOptions.Multiline);
// Indeks dopasowania jest relatywny do podciągu — przywracamy offset:
int paramsAbsoluteIndex = stepsMatch.Success ? searchFrom + stepsMatch.Index : -1;
```

**Dlaczego nie opierać się na fakcie, że `Steps:` zawsze jest OSTATNIĄ sekcją?** Nie jest to gwarantowane przez format — pluginy takie jak ADetailer, ControlNet, AnimateDiff mogą dodawać własne linie przed `Steps:`. Kotwiczenie do `[tekst za negMatch]` jest bezpieczniejsze niż zakładanie kolejności sekcji.

**Kompatybilność wsteczna:** Żaden istniejący poprawny plik A1111/Forge nie ma `Steps:` na początku linii w promptach. Zmiana nie wpływa na wyniki dla poprawnych danych.

---

### D3 — `continue` zamiast `break` w ParseKeyValue dla nieznanych linii

**Decyzja:** Gdy parser natrafi na tekst bez `:` (linia bez pary klucz-wartość), pomija tę linię i kontynuuje parsowanie zamiast przerywać:

```csharp
int keyStart = i;
while (i < len && input[i] != ':' && input[i] != '\n')
    i++;

if (i >= len) break;               // koniec wejścia — stop
if (input[i] == '\n') { i++; continue; }  // linia bez ':' — pomiń, idź dalej

// input[i] == ':'
string key = input[keyStart..i].Trim();
i++;
// ... reszta bez zmian
```

**Dlaczego pominąć, a nie próbować parsować jako wartość?** Linia bez `:` nie pasuje do żadnego ze znanych schematów formatu A1111. Próba wyciągnięcia „wartości" bez klucza nie ma sensu semantycznie.

**Dlaczego nie logować pominięcia?** `ParseKeyValue` jest metodą `internal static` bez dostępu do loggera. Pomijanie jest ciche — odpowiada zachowaniu oryginalnego ExifParser z SDILab, który ignorował nieznane tokeny.

**Uwaga na wieloliniowe wartości w cudzysłowach:** Pętla quoted-string (obsługująca `\"`) nie jest zmieniana — wieloliniowe cytowane wartości nadal będą parsowane poprawnie, bo wychodzą z pętli wewnętrznej przez `break`, nie przez `continue`.

---

## Skutki

- Endpoint `/v1/inject` przestaje być podatny na DoS przez złośliwy chunk PNG.
- Parsowanie nie jest już mylone przez `Steps:` w treści promptu użytkownika.
- Pluginy emitujące dodatkowe linie w bloku parametrów przestają powodować utratę wszystkich kolejnych pól.
- Testy jednostkowe rozszerzone o trzy przypadki (→ issue #3, sekcja ADR-0001 — done).
- Żadna zmiana publicznego API — `SdMetadata`, `SdMetaKit`, sygnatury metod bez modyfikacji.

## Implementacja

| Decyzja | Plik | Test |
|---------|------|------|
| D1 | `src/SDMetaKit.Core/Writing/PngChunkWriter.cs` | `PngChunkWriterTests.InjectParametersText_MaliciousChunkLength_...` |
| D2 | `src/SDMetaKit.Core/Parsing/A1111Parser.cs` | `A1111ParserTests.Parse_StepsInPositivePrompt_...` |
| D3 | `src/SDMetaKit.Core/Parsing/A1111Parser.cs` | `ParseKeyValueTests.ParseKeyValue_SkipsLineWithoutColon_...` |
