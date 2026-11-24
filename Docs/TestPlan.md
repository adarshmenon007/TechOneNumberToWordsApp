# Test Plan — Number to Words Web Page

**Status:** draft, library and API sections (review gates 1 and 2). The UI section is added at gate 3, and slice 11 finishes
the whole document (D52). Decision numbers (D8, E3, …) refer to [`DesignDecisions.md`](DesignDecisions.md).

## 1. What is tested, and how

The amount-to-words routine lives in `TechOneNumberToWordsApp.Core`. Every test goes through its one public class,
`AmountToWordsConverter`, so the tests describe behaviour and survive refactoring (D25). No internals are exposed to the tests.

| Layer | Project folder | Status |
|---|---|---|
| Library (`Core`): parsing, validation, wording | `tests/TechOneNumberToWordsApp.Tests/Unit/` | Done: sections 1.1 to 1.3 |
| HTTP API (`Web`): same data over HTTP, error shape, security headers, 413, logging | `tests/…/Api/` | Done: section 1.4 |
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
each row carries its `Id` and `Decision` as xUnit traits (D47, D48). The same rows also run through
`POST /api/convert` (section 1.4, D44).

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
| `VAL` | Rejected input, one rule at a time (D27 checks 2–12) | 46 |
| `ORD` | Which error wins when an input breaks two rules (D27) | 12 |
| `LIM` | The size limits: 30-character cap, 15 digits, and the message the web layer reuses (`LIM-012`) | 12 |
| `API` | HTTP API: request errors, the 500 path, logging (section 1.4) | 13 |
| `SEC` | Security headers, the 1 KB body limit, the `Server` header, nothing leaked in a 500 or a log | 10 |
| `UI` | Added at gate 3 | 0 |
| **Total** | | **188** |

This table counts IDs. A data row is one ID although it runs twice (library and HTTP), and `API-003` to `API-011` are one ID
each although they run twice (`Testing` and `Development`); section 4 counts every test that runs.

### 1.3 Tests that are not data rows

| ID | Decision | Test | Why it is C# and not a data row |
|---|---|---|---|
| `VAL-021` | E4 | `LoneSurrogateTests` | A lone surrogate is half of an emoji with no partner. JSON cannot carry one (System.Text.Json rejects or replaces it) and xUnit's theory data mangles it, so the inputs are built inside the test. The message must give the real code point (U+D83D), never the replacement character U+FFFD, and no quoted character. |
| `VAL-044` | D23 | `NullInputTests` | Null is not text, so it cannot be a data row. `Convert(null)` throws `ArgumentNullException` (null is a programming error, D24e). |

### 1.4 HTTP API tests (gate 2)

The API tests start the real application in memory (`WebApplicationFactory<Program>` through one shared `ApiFactory`, D45) and
send real HTTP requests to `POST /api/convert`. They check what a client sees: status code, content type, body and headers
(D24, D41). Every error response must have the same shape: `application/problem+json` with an `errorCode`, a `title` and the
`status`; the amount errors also have a `detail`.

**Conversion rows over HTTP (D44).** `ConvertApiTests.Api_Convert_MatchesExpectedOutcome` runs all 162 data rows of section 1.1 a
second time through `POST /api/convert`, under their existing IDs (the display name ends with " (HTTP)"). A words row must give
200, `application/json` and the same words. A rejection row must give 400, `application/problem+json`, the same `errorCode`, the
title "Invalid amount" and a `detail` that contains the row's `messageContains`. This proves the two layers agree and that
awkward characters (non-breaking space, tab, emoji) survive the JSON round trip. The two library-only C# tests (`VAL-021`, `VAL-044`)
have no HTTP twin: a lone surrogate cannot be sent as JSON, and null is covered by `API-004` and `API-005`. Because an ID now
selects both runs, `--filter "Id=CENT-001"` runs two tests.

**Environments.** The factory runs the app as `Testing` by default. The request-error tests (`API-003` to `API-011`) run twice,
under `Testing` and `Development`, because Development once answered malformed JSON with a 400 that carried an exception and
stack trace (D24). The 500 tests run under `Testing` only: Development turns on the developer exception page, which shows a
stack trace by design.

**Real Kestrel.** `SEC-006` to `SEC-008` and `SEC-010` start a real Kestrel server on a free port over plain http, because the
in-memory server enforces no body limit and never sends a `Server` header. They use plain http because neither behaviour has
anything to do with HTTPS and CI has no development certificate (D37).

**Logging.** A small in-memory `ILoggerProvider` in the test project (no package) records each entry's category, level, message,
structured values, scopes and exception (D33). Each logging test creates its own factory, so it sees only its own entries.

| ID | Decisions | What it checks | Runs |
|---|---|---|---|
| `API-003` | D18, D24e | Empty body with a JSON content type → 400 `InvalidRequest` | Testing, Development |
| `API-004` | D24e | `{}` (no amount) → 400 `MissingField` | Testing, Development |
| `API-005` | D24e | `{"amount":null}` → 400 `MissingField` | Testing, Development |
| `API-006` | D24e, D41 | Malformed JSON → 400 `InvalidRequest` | Testing, Development |
| `API-007` | E5, D41 | `{"amount":5}` (a JSON number) → 400 `InvalidRequest` | Testing, Development |
| `API-008` | E5 | An unknown extra field is ignored → 200 | Testing, Development |
| `API-009` | D24c, D41 | Content-Type `text/plain` → 415 `UnsupportedMediaType` | Testing, Development |
| `API-010` | D41 | Unknown route → 404 `NotFound` | Testing, Development |
| `API-011` | D41 | `GET /api/convert` → 405 `MethodNotAllowed` | Testing, Development |
| `API-012` | D24, D41 | A converter that throws → 500, `ServerError`, title "Server error", the fixed detail "An unexpected error occurred.", a `traceId` | Testing |
| `API-013` | D33 | A rejected amount logs one Information entry from the endpoint, with `ErrorCode` as a structured value | Testing |
| `API-014` | D33 | A successful conversion logs nothing from the endpoint | Testing |
| `API-015` | D33, C2 | On a 500: one Error entry with the exception, whose log scope `TraceId` equals the middle part of the response's `traceId` | Testing |
| `SEC-001` | D34 | The four security headers (exact values) on a 200 | Testing |
| `SEC-002` | D34 | The same headers on a 400 | Testing |
| `SEC-003` | D34 | The same headers on a 404 | Testing |
| `SEC-004` | D34 | The same headers on a 500 | Testing |
| `SEC-006` | D37, C1 | Body over 1 KB with a declared `Content-Length` → 413, `PayloadTooLarge` (not `AmountTooLong`), title "Invalid request", `detail` equal to `AmountToWordsConverter.AmountTooLongMessage` | Real Kestrel, http |
| `SEC-007` | D37 | The same with a chunked body (no `Content-Length`) | Real Kestrel, http |
| `SEC-008` | D34 | The response has no `Server` header | Real Kestrel, http |
| `SEC-010` | D37 | A body of exactly 1,024 bytes passes the limit and reaches the converter (400 `AmountTooLong`); 1,025 bytes gives the 413 | Real Kestrel, http |
| `SEC-011` | D33 | A unique marker typed into a rejected amount appears in no log entry (message, structured values, scopes, exception) | Testing |
| `SEC-012` | D24 | A 500 body has no stack trace, exception message or exception type name | Testing |
| `LIM-012` | D23, C1 | (library) `AmountTooLongMessage` equals the message of the `AmountTooLong` error; with `SEC-006` this proves the 413 `detail` matches | Library |

IDs that are deliberately not used: `API-001` and `API-002` (dropped in design; the HTTP theory above replaces them) and
`SEC-009` (dropped; `LIM-012` with `SEC-006` replaces it). `SEC-005` is reserved for slice 10: the same headers on `index.html`.

**Finding a log entry from a response (C2).** The `traceId` in an error response is the full W3C id
(`00-<TraceId>-<SpanId>-<flags>`); the log scope holds only the 32-character `TraceId`, which is the middle part. `API-015` checks
that the two match.

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
| `dotnet test --filter "Decision=D8"` | every test linked to decision D8 (18 data rows, each run in the library and over HTTP: 36) |
| `dotnet test --filter "Id=CENT-001"` | the brief's example: the library test and its HTTP twin (2) |
| `dotnet test --filter "Id~AND-"` | every test whose ID contains `AND-` (16 rows, run twice: 32) |
| `dotnet test --filter "Id~SEC-"` | every security test (10) |
| `dotnet test --filter "Id~API-"` | every API test (22: `API-003` to `API-011` run twice) |
| `dotnet test --filter "FullyQualifiedName~Tests.Api"` | the whole HTTP layer (194) |

The test project uses the classic VSTest runner (`xunit.v3.mtp-off`, D48). The only packages in the solution are in the test
project (xunit.v3.mtp-off 4.0.3, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.12.0,
Microsoft.AspNetCore.Mvc.Testing 10.0.15); `Core` and `Web` have none (S4).

## 3. Traceability

### 3.1 The brief and the submission requirements

Library and API evidence. Rows marked "later" gain evidence at the gate named.

| Requirement | Decisions | Evidence now | How to check |
|---|---|---|---|
| **R0** the brief's example: "123.45" → ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS | D8 | `CENT-001` (and the dollars part alone, `AND-005`), in the library and over HTTP | `dotnet test --filter "Id=CENT-001"` |
| **R1** HTML UI | D5, D6 | later (gate 3) | |
| **R2** design-rationale document | D55 | `Docs/DesignDecisions.md` now; `Docs/DesignDocument.md` later (slice 11) | |
| **R3** test plan | D47, D52 | this document | |
| **R4** supporting files (solution, source, tests with configuration and data) | D43–D46, D49–D51 | `TechOneNumberToWordsApp.sln`, `tests/…/TestData/conversion-cases.json`, `xunit.runner.json`, `test.runsettings` | `dotnet test` from a clean clone |
| **R5** C# | D1 | `Core` and `Web` are C# on .NET 10 | |
| **R6** Readme | D56 | `Readme.md`, a draft with the API reference (gate 2); finished in slice 11 | |
| **R7** AI model and conversation history | D57 | `Docs/AI/` (exports per phase, plan copies, the claude.ai review); `Docs/AI/README.md` later | |
| **S1** production quality | D7, D10, D19, D22, D23, D27–D29 (library); D24, D33–D35, D37, D41 (API) | 359 tests: the library rule by rule and every ordering pair (`ORD`), every row again over HTTP, one error shape for every error, security headers, a 1 KB body limit, no leaked exception, no raw input in logs; 99.2% line / 99.3% branch coverage of `Core` and 97.3% / 90.9% of `Web`; warnings are errors (D39) | `dotnet test`; `dotnet build` (warnings are errors) |
| **S2** explain all code | D50, D55 | small single-purpose classes with comments that give the reason, not just the action; walkthrough later in the design document | |
| **S3** no internet solutions | D54 | `.claude/settings.json` denies `WebSearch` and `WebFetch` | `git log -p .claude/settings.json` |
| **S4** no libraries or NuGet packages | D4, D11 | `Core` and `Web` have no `PackageReference`; the parser, validator and wording are hand-written | `grep -c PackageReference src/*/*.csproj` prints 0 for both |
| **S5** deadline | | not a test matter | |
| **S6** private repository with reviewer access | D54 | not a test matter | |

### 3.2 Decisions → tests (generated from the data file and the C# test traits)

| Decision | Tests | Count |
|---|---|---|
| C1 | LIM-012, SEC-006 | 2 |
| C2 | API-015 | 1 |
| D8 | AND-001..016, CENT-001, WORD-036 | 18 |
| D9 | FMT-001..005, VAL-045 | 6 |
| D10 | FMT-001..005, VAL-009..016 | 13 |
| D11 | CENT-011 | 1 |
| D12 | VAL-007, VAL-045 | 2 |
| D18 | API-003 | 1 |
| D19 | VAL-001..003 | 3 |
| D22 | VAL-004..006 | 3 |
| D23 | LIM-012, VAL-044 | 2 |
| D24 | API-012, SEC-012 | 2 |
| D24c | API-009 | 1 |
| D24e | API-003..006 | 4 |
| D27 | LIM-005, LIM-007, ORD-001..012, VAL-001..003, VAL-008, VAL-022..024 | 21 |
| D28 | FMT-014, VAL-040 | 2 |
| D29 | LIM-003..007, ORD-009..010 | 7 |
| D33 | API-013..015, SEC-011 | 4 |
| D34 | SEC-001..004, SEC-008 | 5 |
| D37 | SEC-006..007, SEC-010 | 3 |
| D41 | API-006..007, API-009..012 | 6 |
| D58 | CENT-002..010, CENT-012..013, FMT-006..008, LIM-001..002, LIM-004, LIM-008..011, PLUR-001..004, ZERO-001..004 | 29 |
| E1 | VAL-041..043 | 3 |
| E2 | VAL-025..028 | 4 |
| E3 | FMT-009..013, VAL-029..039 | 16 |
| E4 | VAL-017..019, VAL-021, VAL-046 | 5 |
| E5 | API-007..008 | 2 |
| F1 | VAL-020 | 1 |
| O1 | CENT-007..008, SCALE-001..008, WORD-001..036 | 46 |

Counts are test IDs: a data row counts once although it runs twice (section 1.4), and a test can link to several decisions, so the
counts add up to more than the 188 IDs. D7, D21 and D25 describe how the library is built and tested rather than rules to test, and
D35 (HTTPS) has no test of its own (section 5.3); none of these has rows here.

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
| `InvalidCharacter` | ORD-005, ORD-011, VAL-011..020, VAL-046 (13) |
| `MultipleDecimalPoints` | VAL-022..024 (3) |
| `InvalidThousandsSeparator` | ORD-007, VAL-029..040 (13) |
| `TooManyDecimalPlaces` | ORD-008, VAL-041..043 (4) |
| `AmountTooLarge` | LIM-008..011 (4) |

The order in which the checks run (D27) is pinned by the `ORD` tests: each one breaks two rules at once and expects the earlier
check to win, for every neighbouring pair that could be confused (1 before 2, 1 before 3, 3 before 4, 3 before 6, 4 before 7,
4 before 8, 5 before 7, 6 before 7, 7 before 8, 7 before 11, 10 before 11, 11 before 12). Two orderings are covered by `VAL` rows:
8 before 9 (`..`) and 9 before 10 (`.,`).

### 3.4 API errors → tests

Every error response has an `errorCode` (D24a). The 11 conversion codes of section 3.3 are checked over HTTP by the data rows
(400, title "Invalid amount"). The codes the API adds:

| Error code | Status | Tests |
|---|---|---|
| `InvalidRequest` | 400 | API-003, API-006, API-007 (3 × 2 environments) |
| `MissingField` | 400 | API-004, API-005 (2 × 2) |
| `UnsupportedMediaType` | 415 | API-009 (× 2) |
| `NotFound` | 404 | API-010 (× 2) |
| `MethodNotAllowed` | 405 | API-011 (× 2) |
| `PayloadTooLarge` | 413 | SEC-006, SEC-007, SEC-010 |
| `ServerError` | 500 | API-012, SEC-012 |

## 4. Results

| | |
|---|---|
| Date | 2026-11-24 |
| Commit tested | `749be38` on `phase/2-api`, the last code commit before this update |
| Environment | .NET SDK 10.0.401, Windows 11 |
| Command | `dotnet test` |
| Result | **359 run, 359 passed, 0 failed, 0 skipped** |

| Part | Tests |
|---|---|
| Library: 162 data rows, plus `VAL-021`, `VAL-044` and `LIM-012` (164 at gate 1; `LIM-012` was added in slice 9) | 165 |
| HTTP: the same 162 data rows through `POST /api/convert` | 162 |
| API and security tests in C#: `API-003` to `API-011` in two environments (18), `API-012` to `API-015` (4), `SEC-001` to `SEC-004` (4), `SEC-006` to `SEC-008` and `SEC-010` (4), `SEC-011` and `SEC-012` (2) | 32 |
| **Total** | **359** |

### Coverage (Microsoft code coverage, Cobertura, test assembly excluded)

**`Core`**

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
| **`TechOneNumberToWordsApp.Core` total** | **99.2% (251 of 253)** | **99.3% (133 of 134)** |

**`Web`**

| Class | Lines | Branches |
|---|---|---|
| `Program` | 100% | 100% |
| `ConvertEndpoint` | 100% | 100% |
| `ApiProblem` | 100% | 100% |
| `ProblemDetailsCustomizer` | 90.3% | 80.0% (see 5.4) |
| `SecurityHeadersMiddleware`, `SecurityHeadersExtensions` | 100% | 100% |
| `ConvertRequest`, `ConvertResponse` | 100% | 100% |
| **`TechOneNumberToWordsApp.Web` total** | **97.3% (108 of 111)** | **90.9% (40 of 44)** |

Coverage is evidence, not a target; there is no minimum (D46).

## 5. Code that the tests do not exercise, and why

Sections 5.1 and 5.2 are two guards that protect the library against a bug in the library itself. The validator makes both
unreachable through the public API, so no test can trigger them (D25: tests go through the public class only, with no
`InternalsVisibleTo`). Sections 5.3 and 5.4 are about the API layer.

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

### 5.3 Behaviour in the API layer that no automated test checks

- **HSTS (D35).** Nothing asserts a `Strict-Transport-Security` header. `UseHsts()` runs outside Development, and the middleware's
  default excluded hosts include `localhost`, which is the host every test uses, so a test would need another host name. D35 was
  added to slice 9 late and, by decision, without a test.
- **The HTTPS redirect (D35).** Nothing asserts that a plain-http request is redirected. The in-memory server and the real-Kestrel
  tests have no https port, so the middleware has nothing to redirect to (it logs "Failed to determine the https port" and passes
  the request on). The real-Kestrel tests therefore show only that the middleware does not interfere when no https port is known;
  they do not show what `dotnet run` does with the launch profile, where both ports exist.
- **Console logging scopes (C2).** `appsettings.json` sets `"IncludeScopes": true` for the console logger so the `TraceId` appears in
  the console output. The tests clear the console provider and read scopes through the in-memory provider instead, so the setting
  itself is not tested, only that the log scope carries the `TraceId` (`API-015`).
- **The developer exception page and a 500 in Development.** In Development the framework's developer exception page answers
  unexpected exceptions (D24), with a stack trace by design. No test covers it (the 500 tests run under `Testing` only, D45).

### 5.4 Branches in `Web` that no test reaches

`ProblemDetailsCustomizer` has 3 uncovered lines and 4 uncovered branches: the fall-through for a status that it does not map
(for example 401 or 408). No test produces such a status, and no request to this application is known to: the errors the tests and the
framework give are 400, 404, 405, 413, 415 and 500. The branch makes sure an unmapped status still gets a well-formed ProblemDetails (without an
`errorCode`), so it stays as a guard.

Both sides of each `IsDevelopment()` check in `Program.cs` do run, because the request-error tests run under `Testing` and
`Development` (section 1.4), so they are not uncovered.

## 6. Still to come

- **Gate 3 (UI):** manual cases `UI-xxx` with steps and a hand-recorded outcome (E6, F3, accessibility basics).
- **Slice 10:** `SEC-005` (security headers on `index.html`).
- **Slice 11:** final results (date, commit, totals, coverage), the finished matrix, and the executable decision (D20).
