# Test Plan — Number to Words Web Page

**Status:** draft, library section only (review gate 1). The API, security and UI sections are added at gates 2 and 3, and
slice 11 finishes the whole document (D52). Decision numbers (D8, E3, …) refer to [`DesignDecisions.md`](DesignDecisions.md).

## 1. What is tested, and how

The amount-to-words routine lives in `TechOneNumberToWordsApp.Core`. Every test goes through its one public class,
`AmountToWordsConverter`, so the tests describe behaviour and survive refactoring (D25). No internals are exposed to the tests.

| Layer | Project folder | Status |
|---|---|---|
| Library (`Core`): parsing, validation, wording | `tests/TechOneNumberToWordsApp.Tests/Unit/` | Done: this section |
| HTTP API (`Web`): same data over HTTP, error shape, security headers, 413, logging | `tests/…/Api/` | Gate 2 |
| Page (`wwwroot`): manual cases `UI-xxx` | this document | Gate 3 |

### 1.1 One data file drives the conversion tests (D43, D44)

`tests/TechOneNumberToWordsApp.Tests/TestData/conversion-cases.json` holds one row per case:

```json
{
  "id": "AND-005",
  "decisions": ["D8"],
  "description": "The dollars part of the brief's example",
  "input": "123",
  "expectedWords": "ONE HUNDRED AND TWENTY-THREE DOLLARS"
}
```

A rejected input has `expectedErrorCode` (and `messageContains`) instead of `expectedWords`. Characters that are hard to see
(non-breaking space, zero-width space, emoji) are written as `\uXXXX` escapes so they cannot be missed in the file.

One xUnit theory, `AmountToWordsConverterTests.Convert_MatchesExpectedOutcome`, runs every row. It receives only the case ID;
each row carries its `Id` and `Decision` as xUnit traits (D47, D48). At gate 2 the same rows will also run through
`POST /api/convert` (D44).

**The loader checks the file and fails the whole run** with a message naming every problem, rather than skipping a bad row:
a duplicate ID, a row with both or neither of `expectedWords` / `expectedErrorCode`, an `expectedErrorCode` that is not a
member of `ConversionErrorCode`, an empty `decisions` list, a missing required field, or an unknown (misspelt) field (D43).
D52: no automated tests for test code; each check fails the run with a message naming the row.

### 1.2 Test IDs and categories (D47)

An ID is a category prefix and a number and is never reused. IDs are shared by the data file and the C# tests, so the number
sequence in one category can skip numbers that belong to a C# test (`VAL-021`, `VAL-044`).

| Category | Covers | Tests now |
|---|---|---|
| `WORD` | Number words, hyphens, exact hundreds | 36 |
| `AND` | Where AND goes (D8) | 16 |
| `SCALE` | THOUSAND … TRILLION and the boundaries between them | 8 |
| `CENT` | Cents, singular/plural cent, the brief's example | 13 |
| `ZERO` | Zero in every decimal-point form | 4 |
| `PLUR` | DOLLAR vs DOLLARS, CENT vs CENTS | 4 |
| `FMT` | Valid formats: trimming, leading zeros, commas | 14 |
| `VAL` | Rejected input, one rule at a time (D27 checks 2–12) | 45 |
| `ORD` | Which error wins when an input breaks two rules (D27) | 12 |
| `LIM` | The size limits: 30-character cap, 15 digits | 11 |
| `API`, `SEC`, `UI` | Added at gates 2 and 3 | 0 |
| **Total** | | **163** |

### 1.3 Tests that are not data rows

| ID | Decision | Test | Why it is C# and not a data row |
|---|---|---|---|
| `VAL-021` | E4 | `LoneSurrogateTests` | A lone surrogate is half of an emoji with no partner. JSON cannot carry one (System.Text.Json rejects or replaces it) and xUnit's theory data mangles it, so the inputs are built inside the test. The message must give the real code point (U+D83D), never the replacement character U+FFFD, and no quoted character. |
| `VAL-044` | D23 | `NullInputTests` | Null is not text, so it cannot be a data row. `Convert(null)` throws `ArgumentNullException` (null is a programming error, D24e). |

## 2. How to run

From the repository root, with the .NET 10 SDK (`global.json`):

```
dotnet test
```

No flags are needed: `Directory.Build.props` points at `tests/TechOneNumberToWordsApp.Tests/test.runsettings`, which writes a
`.trx` result file and a Cobertura coverage report to `tests/TechOneNumberToWordsApp.Tests/TestResults/` (D45, D46). The
coverage collector excludes the test assembly, so the report covers production code only.

Run part of the suite with a trait filter:

| Command | Runs |
|---|---|
| `dotnet test --filter "Decision=D8"` | every test linked to decision D8 (18 now) |
| `dotnet test --filter "Id=CENT-001"` | one test, here the brief's example |
| `dotnet test --filter "Id~AND-"` | every test whose ID contains `AND-` (a whole category, 16 now) |

The test project uses the classic VSTest runner (`xunit.v3.mtp-off`, D48). The only packages in the solution are in the test
project (xunit.v3.mtp-off 4.0.3, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.12.0,
Microsoft.AspNetCore.Mvc.Testing 10.0.15); `Core` and `Web` have none (S4).

## 3. Traceability

### 3.1 The brief and the submission requirements

Library-level evidence only. Rows marked "later" gain evidence at the gate named.

| Requirement | Decisions | Evidence now | How to check |
|---|---|---|---|
| **R0** the brief's example: "123.45" → ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS | D8 | `CENT-001` (and the dollars part alone, `AND-005`) | `dotnet test --filter "Id=CENT-001"` |
| **R1** HTML UI | D5, D6 | later (gate 3) | |
| **R2** design-rationale document | D55 | `Docs/DesignDecisions.md` now; `Docs/DesignDocument.md` later (slice 11) | |
| **R3** test plan | D47, D52 | this document | |
| **R4** supporting files (solution, source, tests with configuration and data) | D43–D46, D49–D51 | `TechOneNumberToWordsApp.sln`, `tests/…/TestData/conversion-cases.json`, `xunit.runner.json`, `test.runsettings` | `dotnet test` from a clean clone |
| **R5** C# | D1 | `Core` and `Web` are C# on .NET 10 | |
| **R6** Readme | D56 | later (slice 11) | |
| **R7** AI model and conversation history | D57 | `Docs/AI/` (exports per phase, plan copies, the claude.ai review); `Docs/AI/README.md` later | |
| **S1** production quality | D7, D10, D19, D22, D23, D27–D29 (library) | 163 tests, one rule at a time and every ordering pair (`ORD`); 99.2% line / 99.2% branch coverage of `Core`; warnings are errors (D39) | `dotnet test`; `dotnet build` (warnings are errors) |
| **S2** explain all code | D50, D55 | small single-purpose classes with comments that give the reason, not just the action; walkthrough later in the design document | |
| **S3** no internet solutions | D54 | `.claude/settings.json` denies `WebSearch` and `WebFetch` | `git log -p .claude/settings.json` |
| **S4** no libraries or NuGet packages | D4, D11 | `Core` and `Web` have no `PackageReference`; the parser, validator and wording are hand-written | `grep -c PackageReference src/*/*.csproj` prints 0 for both |
| **S5** deadline | | not a test matter | |
| **S6** private repository with reviewer access | D54 | not a test matter | |

### 3.2 Decisions → tests (generated from the data file and the C# test traits)

| Decision | Tests | Count |
|---|---|---|
| D8 | AND-001..016, CENT-001, WORD-036 | 18 |
| D9 | FMT-001..005, VAL-045 | 6 |
| D10 | FMT-001..005, VAL-009..016 | 13 |
| D11 | CENT-011 | 1 |
| D12 | VAL-007, VAL-045 | 2 |
| D19 | VAL-001..003 | 3 |
| D22 | VAL-004..006 | 3 |
| D23 | VAL-044 | 1 |
| D27 | LIM-005, LIM-007, ORD-001..012, VAL-001..003, VAL-008, VAL-022..024 | 21 |
| D28 | FMT-014, VAL-040 | 2 |
| D29 | LIM-003..007, ORD-009..010 | 7 |
| D58 | CENT-002..010, CENT-012..013, FMT-006..008, LIM-001..002, LIM-004, LIM-008..011, PLUR-001..004, ZERO-001..004 | 29 |
| E1 | VAL-041..043 | 3 |
| E2 | VAL-025..028 | 4 |
| E3 | FMT-009..013, VAL-029..039 | 16 |
| E4 | VAL-017..019, VAL-021 | 4 |
| F1 | VAL-020 | 1 |
| O1 | CENT-007..008, SCALE-001..008, WORD-001..036 | 46 |

A test can link to several decisions, so the counts add up to more than 163. D7, D21 and D25 describe how the library is built
and tested rather than rules to test; they have no rows of their own.

### 3.3 Rejected input → error code

Every `ConversionErrorCode` has at least one test, and every rejection row also checks part of the message text.

| Error code | Tests |
|---|---|
| `AmountTooLong` | LIM-005, LIM-007, ORD-009..010 (4) |
| `AmountRequired` | VAL-001..003, VAL-025..028 (7) |
| `CurrencySymbolNotAllowed` | ORD-001, ORD-012, VAL-004..006 (5) |
| `NegativeAmount` | ORD-002, ORD-006, VAL-007, VAL-045 (4) |
| `SignNotAllowed` | ORD-003, VAL-008 (2) |
| `ContainsWhitespace` | ORD-004, VAL-009..010 (3) |
| `InvalidCharacter` | ORD-005, ORD-011, VAL-011..020 (12) |
| `MultipleDecimalPoints` | VAL-022..024 (3) |
| `InvalidThousandsSeparator` | ORD-007, VAL-029..040 (13) |
| `TooManyDecimalPlaces` | ORD-008, VAL-041..043 (4) |
| `AmountTooLarge` | LIM-008..011 (4) |

The order in which the checks run (D27) is pinned by the `ORD` tests: each one breaks two rules at once and expects the earlier
check to win, for every neighbouring pair that could be confused (1 before 2, 1 before 3, 3 before 4, 3 before 6, 4 before 7,
4 before 8, 5 before 7, 6 before 7, 7 before 8, 7 before 11, 10 before 11, 11 before 12). Two orderings are covered by `VAL` rows:
8 before 9 (`..`) and 9 before 10 (`.,`).

## 4. Results

| | |
|---|---|
| Date | 2026-11-24 |
| Commit tested | `7bf6a86` on `phase/1-library`, the last commit before this document |
| Environment | .NET SDK 10.0.401, Windows 11 |
| Command | `dotnet test` |
| Result | **163 run, 163 passed, 0 failed, 0 skipped** |
| Data rows / C# tests | 161 rows in `conversion-cases.json`, 2 C# tests (`VAL-021`, `VAL-044`) |

### Coverage (Microsoft code coverage, Cobertura, test assembly excluded)

| Class | Lines | Branches |
|---|---|---|
| `AmountParser` | 100% | 100% |
| `AmountToWordsConverter` | 100% | 100% |
| `AmountValidator` | 100% | 100% |
| `ConversionError` | 100% | 100% |
| `ConversionResult` | 100% | 100% |
| `MoneyAmount` | 100% | 100% (see 5.1) |
| `MoneyWords` | 100% | 100% |
| `NumberWords` | 96.5% | 95.8% (see 5.2) |
| **`TechOneNumberToWordsApp.Core` total** | **99.2% (246 of 248)** | **99.2% (119 of 120)** |

`Web` is not in the report yet: no test loads it. It appears once the API tests start the application (gate 2). Coverage is
evidence, not a target; there is no minimum (D46).

## 5. Code that the tests do not exercise, and why

Two guards protect the code against a bug in the library itself. The validator makes both unreachable through the public API,
so no test can trigger them (D25: tests go through the public class only, with no `InternalsVisibleTo`).

### 5.1 `MoneyAmount.Create` range checks (D11)

`Create` throws `ArgumentOutOfRangeException` for negative dollars, negative cents, or cents above 99. The validator has already
rejected a minus sign, more than two decimal places and every non-digit, and the parser builds cents from at most two digits, so
`Create` can only receive a valid amount. The guard is a bug detector, not an input check, which is why it throws instead of
returning a `ConversionError`. The coverage report shows these lines as covered because the check always runs; the *throw* is
what never happens.

### 5.2 `NumberWords.Spell` range guard

`Spell` throws `ArgumentOutOfRangeException` for 1,000,000,000,000,000 or more, because there is no scale word above TRILLION.
Check 12 of the validator rejects any amount of more than 15 significant digits first (`LIM-008..011`), so the guard can only
fire if the validator is changed to let a larger number through. These are the two uncovered lines in the report.

## 6. Still to come

- **Gate 2 (API):** `API` and `SEC` tests; every data row over HTTP; missing body and missing amount; `amount` sent as a JSON
  number (E5); 415, 404, 405; the 500 path with no stack trace; security headers; the 413 body limit on real Kestrel over plain
  http (D37); logging.
- **Gate 3 (UI):** manual cases `UI-xxx` with steps and a hand-recorded outcome (E6, F3, accessibility basics).
- **Slice 11:** final results (date, commit, totals, coverage), the finished matrix, and the executable decision (D20).
