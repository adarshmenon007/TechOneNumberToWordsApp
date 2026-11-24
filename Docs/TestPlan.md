# Test Plan — Number to Words Web Page

**Status:** draft, library, API and page sections (review gates 1 to 3). The manual UI outcomes (section 1.5) are recorded by the
author and are still empty. Slice 11 finishes the whole document (D52). Decision numbers (D8, E3, …) refer to [`DesignDecisions.md`](DesignDecisions.md).

## 1. What is tested, and how

The amount-to-words routine lives in `TechOneNumberToWordsApp.Core`. Every test goes through its one public class,
`AmountToWordsConverter`, so the tests describe behaviour and survive refactoring (D25). No internals are exposed to the tests.

| Layer | Project folder | Status |
|---|---|---|
| Library (`Core`): parsing, validation, wording | `tests/TechOneNumberToWordsApp.Tests/Unit/` | Done: sections 1.1 to 1.3 |
| HTTP API (`Web`): same data over HTTP, error shape, security headers (also on the page's files), 413, logging | `tests/…/Api/` | Done: section 1.4 |
| Page (`wwwroot`): manual cases `UI-xxx`, plus `SEC-005` and `SEC-014` | this document | Cases written (section 1.5); outcomes to be recorded by the author |

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
| `SEC` | Security headers (also on the page's files), the 1 KB body limit, the `Server` header, nothing leaked in a 500 or a log, no HTML inserted by `app.js` | 12 |
| `UI` | Manual cases for the page (section 1.5); not run by `dotnet test` | 19 |
| **Total** | | **209** |

This table counts IDs. A data row is one ID although it runs twice (library and HTTP), `API-003` to `API-011` are one ID
each although they run twice (`Testing` and `Development`), and `SEC-005` is one ID although it runs four times (one per file);
section 4 counts every test that runs. The 19 `UI` cases are not run by `dotnet test`.

### 1.3 Tests that are not data rows

| ID | Decision | Test | Why it is C# and not a data row |
|---|---|---|---|
| `VAL-021` | E4 | `LoneSurrogateTests` | A lone surrogate is half of an emoji with no partner. JSON cannot carry one (System.Text.Json rejects or replaces it) and xUnit's theory data mangles it, so the inputs are built inside the test. The message must give the real code point (U+D83D), never the replacement character U+FFFD, and no quoted character. |
| `VAL-044` | D23 | `NullInputTests` | Null is not text, so it cannot be a data row. `Convert(null)` throws `ArgumentNullException` (null is a programming error, D24e). |

### 1.4 HTTP API tests (gates 2 and 3)

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

**The page's files (gate 3).** `SEC-005` and `SEC-014` are in `Api/StaticFilesApiTests.cs`. They fetch the real files from `wwwroot`
through the same in-memory factory, so they show that the static files sit behind the security headers (D34, D38). Everything else
about the page is checked by hand (section 1.5).

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
| `SEC-005` | D34, D38, C3 | The same headers on each file of the page: `/`, `/app.js`, `/site.css` and `/favicon.svg`, each with status 200 and its media type (`text/html`, `text/javascript`, `text/css`, `image/svg+xml`). One theory, four runs | Testing |
| `SEC-006` | D37, C1 | Body over 1 KB with a declared `Content-Length` → 413, `PayloadTooLarge` (not `AmountTooLong`), title "Invalid request", `detail` equal to `AmountToWordsConverter.AmountTooLongMessage` | Real Kestrel, http |
| `SEC-007` | D37 | The same with a chunked body (no `Content-Length`) | Real Kestrel, http |
| `SEC-008` | D34 | The response has no `Server` header | Real Kestrel, http |
| `SEC-010` | D37 | A body of exactly 1,024 bytes passes the limit and reaches the converter (400 `AmountTooLong`); 1,025 bytes gives the 413 | Real Kestrel, http |
| `SEC-011` | D33 | A unique marker typed into a rejected amount appears in no log entry (message, structured values, scopes, exception) | Testing |
| `SEC-012` | D24 | A 500 body has no stack trace, exception message or exception type name | Testing |
| `SEC-014` | D24 | `app.js` is served and contains `textContent`, and none of `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write` appears in it (comments included). A text scan: it cannot prove safety, so `UI-009` checks the behaviour by hand | Testing |
| `LIM-012` | D23, C1 | (library) `AmountTooLongMessage` equals the message of the `AmountTooLong` error; with `SEC-006` this proves the 413 `detail` matches | Library |

IDs that are deliberately not used: `API-001` and `API-002` (dropped in design; the HTTP theory above replaces them), `SEC-009`
(dropped; `LIM-012` with `SEC-006` replaces it) and `SEC-013` (proposed for a scan of `index.html` for inline script and style,
and dropped; the console check in `UI-017` covers CSP violations).

**Finding a log entry from a response (C2).** The `traceId` in an error response is the full W3C id
(`00-<TraceId>-<SpanId>-<flags>`); the log scope holds only the 32-character `TraceId`, which is the middle part. `API-015` checks
that the two match.

### 1.5 Manual UI cases (gate 3)

`app.js` has no automated tests (the solution has no JavaScript tooling, D47). These cases check the page by hand.

**How to run them.** Start the app with `dotnet run --project src/TechOneNumberToWordsApp.Web`, open `https://localhost:5443` in
Chrome with the development certificate trusted (D35), and open DevTools (Console and Network). Reload the page after any case that
changes `window.fetch` (`UI-013`, `UI-019`). Record each outcome by hand in the last column: pass or fail, the date, the commit and
the browser.

| ID | Decisions | Steps | Expected | Outcome |
|---|---|---|---|---|
| `UI-001` | R0, R1, D5, D40 | Type `123.45`, click **Convert** | The box shows ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS. The label reads "Amount (AUD)"; there is no currency dropdown; the heading is "Amount to Words" | |
| `UI-002` | D5 | Type `5`, press Enter without clicking | FIVE DOLLARS | |
| `UI-003` | D58 | Convert `1`, then `0.01`, then `.5` | ONE DOLLAR, ONE CENT, FIFTY CENTS | |
| `UI-004` | D6, D29 | Paste a 40-character string of digits. Check the field, then click **Convert** | The field keeps all 40 characters (no `maxlength`); the box shows the "amount is too long" message from the server | |
| `UI-005` | D24, D19 | Convert `12a` | The server's message about the invalid character `'a' (U+0061)`, in error styling with a visible "Error" label; no words | |
| `UI-006` | D19 | Leave the field empty, click **Convert** | "Please enter an amount." | |
| `UI-007` | D22 | Convert `$5` | "Please enter the amount without a currency symbol. Amounts are in Australian dollars." | |
| `UI-008` | C1, D37 | Paste about 2,000 digits, click **Convert** | The same "too long" message as `UI-004`; the Network tab shows a 413 | |
| `UI-009` | D24, D34 | Convert `<b>x</b>` | The message appears as plain text naming `'<' (U+003C)`; no element is created from the input and nothing is bold (the "Error" label is its own element) | |
| `UI-010` | E4 | Convert `5😀`, then a full-width `５` | Each message shows the character and its code point correctly | |
| `UI-011` | E6 | Network throttling "Slow 3G". Convert `5`; while it is pending press Enter again and try to type in the field | The button is disabled, the field is read-only (focus stays, typing changes nothing), the old result is gone, "Converting…" shows, and only one request appears in the Network tab. When it finishes the button is enabled, the field is editable and the new result shows | |
| `UI-012` | E6 | Convert `5`, then `12a`, then `5` again | Each result replaces the last: old words never sit beside an error, and the old error is gone after a success | |
| `UI-013` | F3 | In the Console run `window.fetch = () => Promise.resolve(new Response(JSON.stringify({status:500,traceId:"00-abc-def-00"}), {status:500, headers:{"Content-Type":"application/problem+json"}}))`, then Convert `5`. Reload the page afterwards | "Something went wrong. Please try again. Reference: 00-abc-def-00"; the button is enabled again | |
| `UI-014` | D24, D60 | DevTools Network "Offline" (or stop the server), Convert `5`; then go back online and Convert again | The fallback, "Something went wrong. Please try again." (no Reference); the button is enabled again and the field editable; the next conversion works without reloading | |
| `UI-015` | R1 (accessibility basics) | Keyboard only: Tab through the page; click the label; press Enter in the field. Then Tab to the **Convert** button and start a conversion from it with Enter or Space, and press Tab again. In DevTools > Accessibility, inspect the box | Tab goes input then button; the focus outline is visible; the label click focuses the field; Enter submits; the box is a `status` live region and the hint is the input's description; after a conversion started from the Convert button with the keyboard, the next Tab goes to a sensible place, not back to the top of the page | |
| `UI-016` | R1 | Zoom to 200%, then narrow the window to 320 px | No horizontal scrolling and nothing clipped; the input and button wrap | |
| `UI-017` | D34, C3 | Reload with the Console and Network cleared | No console errors or CSP violation messages; no 404; the icon loads as `image/svg+xml`; `/` shows the four security headers | |
| `UI-018` | D35 | Open `http://localhost:5080` | It redirects to `https://localhost:5443` and the page loads | |
| `UI-019` | D60 | In the Console run `window.fetch = (u, o) => new Promise((_, reject) => o.signal.addEventListener('abort', () => reject(o.signal.reason)))`, then Convert `5` and wait 15 seconds. Reload the page afterwards | After about 15 seconds the fallback, "Something went wrong. Please try again."; the button is enabled again and the field editable | |

`UI-019` only works because `app.js` passes the timeout signal in the options of its `fetch` call. `SEC-014` (section 1.4) is the automated companion: it scans `app.js` for the calls that insert HTML (D24).

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
| `dotnet test --filter "Id~SEC-"` | every security test (15: `SEC-005` runs four times) |
| `dotnet test --filter "Id~API-"` | every API test (22: `API-003` to `API-011` run twice) |
| `dotnet test --filter "FullyQualifiedName~Tests.Api"` | the whole HTTP layer (199) |

The test project uses the classic VSTest runner (`xunit.v3.mtp-off`, D48). The only packages in the solution are in the test
project (xunit.v3.mtp-off 4.0.3, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.12.0,
Microsoft.AspNetCore.Mvc.Testing 10.0.15); `Core` and `Web` have none (S4).

## 3. Traceability

### 3.1 The brief and the submission requirements

Library, API and page evidence. Rows marked "later" gain evidence in slice 11.

| Requirement | Decisions | Evidence now | How to check |
|---|---|---|---|
| **R0** the brief's example: "123.45" → ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS | D8 | `CENT-001` (and the dollars part alone, `AND-005`), in the library and over HTTP | `dotnet test --filter "Id=CENT-001"` |
| **R1** HTML UI | D5, D6, D24, D34, D38, D60, E6, F3, C3 | `wwwroot/` (`index.html`, `site.css`, `app.js`, `favicon.svg`); `SEC-005` and `SEC-014` automated; manual cases `UI-001` to `UI-019` (outcomes recorded by the author) | `dotnet test --filter "Id~SEC-005"`; the steps in section 1.5 |
| **R2** design-rationale document | D55 | `Docs/DesignDecisions.md` now; `Docs/DesignDocument.md` later (slice 11) | |
| **R3** test plan | D47, D52 | this document | |
| **R4** supporting files (solution, source, tests with configuration and data) | D43–D46, D49–D51 | `TechOneNumberToWordsApp.sln`, `tests/…/TestData/conversion-cases.json`, `xunit.runner.json`, `test.runsettings` | `dotnet test` from a clean clone |
| **R5** C# | D1 | `Core` and `Web` are C# on .NET 10 | |
| **R6** Readme | D56 | `Readme.md`, a draft with the API reference (gate 2) and "Using the page" (gate 3); finished in slice 11 | |
| **R7** AI model and conversation history | D57 | `Docs/AI/` (exports per phase, plan copies, the claude.ai review); `Docs/AI/README.md` later | |
| **S1** production quality | D7, D10, D19, D22, D23, D27–D29 (library); D24, D33–D35, D37, D41 (API) | 364 tests: the library rule by rule and every ordering pair (`ORD`), every row again over HTTP, one error shape for every error, security headers (also on the page's files), a 1 KB body limit, no leaked exception, no raw input in logs; 99.2% line / 99.3% branch coverage of `Core` and 97.4% / 90.9% of `Web`; warnings are errors (D39) | `dotnet test`; `dotnet build` (warnings are errors) |
| **S2** explain all code | D50, D55 | small single-purpose classes with comments that give the reason, not just the action; walkthrough later in the design document | |
| **S3** no internet solutions | D54 | `.claude/settings.json` denies `WebSearch` and `WebFetch` | `git log -p .claude/settings.json` |
| **S4** no libraries or NuGet packages | D4, D11 | `Core` and `Web` have no `PackageReference`; the parser, validator and wording are hand-written | `grep -c PackageReference src/*/*.csproj` prints 0 for both |
| **S5** deadline | | not a test matter | |
| **S6** private repository with reviewer access | D54 | not a test matter | |

### 3.2 Decisions → tests (generated from the data file and the C# test traits)

| Decision | Tests | Count |
|---|---|---|
| C1 | LIM-012, SEC-006, UI-008 | 3 |
| C2 | API-015 | 1 |
| C3 | SEC-005, UI-017 | 2 |
| D5 | UI-001..002 | 2 |
| D6 | UI-004 | 1 |
| D8 | AND-001..016, CENT-001, WORD-036 | 18 |
| D9 | FMT-001..005, VAL-045 | 6 |
| D10 | FMT-001..005, VAL-009..016 | 13 |
| D11 | CENT-011 | 1 |
| D12 | VAL-007, VAL-045 | 2 |
| D18 | API-003 | 1 |
| D19 | UI-005..006, VAL-001..003 | 5 |
| D22 | UI-007, VAL-004..006 | 4 |
| D23 | LIM-012, VAL-044 | 2 |
| D24 | API-012, SEC-012, SEC-014, UI-005, UI-009, UI-014 | 6 |
| D24c | API-009 | 1 |
| D24e | API-003..006 | 4 |
| D27 | LIM-005, LIM-007, ORD-001..012, VAL-001..003, VAL-008, VAL-022..024 | 21 |
| D28 | FMT-014, VAL-040 | 2 |
| D29 | LIM-003..007, ORD-009..010, UI-004 | 8 |
| D33 | API-013..015, SEC-011 | 4 |
| D34 | SEC-001..005, SEC-008, UI-009, UI-017 | 8 |
| D35 | UI-018 | 1 |
| D37 | SEC-006..007, SEC-010, UI-008 | 4 |
| D38 | SEC-005 | 1 |
| D40 | UI-001 | 1 |
| D41 | API-006..007, API-009..012 | 6 |
| D58 | UI-003, CENT-002..010, CENT-012..013, FMT-006..008, LIM-001..002, LIM-004, LIM-008..011, PLUR-001..004, ZERO-001..004 | 30 |
| D60 | UI-014, UI-019 | 2 |
| E1 | VAL-041..043 | 3 |
| E2 | VAL-025..028 | 4 |
| E3 | FMT-009..013, VAL-029..039 | 16 |
| E4 | UI-010, VAL-017..019, VAL-021, VAL-046 | 6 |
| E5 | API-007..008 | 2 |
| E6 | UI-011..012 | 2 |
| F1 | VAL-020 | 1 |
| F3 | UI-013 | 1 |
| O1 | CENT-007..008, SCALE-001..008, WORD-001..036 | 46 |

Counts are test IDs: a data row counts once although it runs twice (section 1.4), and a test can link to several decisions, so the
counts add up to more than the 209 IDs. The `UI` IDs come from the manual cases of section 1.5, not from test traits. D7, D21 and
D25 describe how the library is built and tested rather than rules to test, and D35 (HTTPS) has no automated test (section 5.3;
`UI-018` checks the redirect by hand); D7, D21 and D25 have no rows here.

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
| Commit tested | `74855e3` on `phase/3-ui`, the last code commit before this update |
| Environment | .NET SDK 10.0.401, Windows 11 |
| Command | `dotnet test` |
| Result | **364 run, 364 passed, 0 failed, 0 skipped** |
| Manual UI cases | `UI-001` to `UI-019` (section 1.5): outcomes not yet recorded by the author |

| Part | Tests |
|---|---|
| Library: 162 data rows, plus `VAL-021`, `VAL-044` and `LIM-012` (164 at gate 1; `LIM-012` was added in slice 9) | 165 |
| HTTP: the same 162 data rows through `POST /api/convert` | 162 |
| API and security tests in C#: `API-003` to `API-011` in two environments (18), `API-012` to `API-015` (4), `SEC-001` to `SEC-004` (4), `SEC-005` (4 files), `SEC-006` to `SEC-008` and `SEC-010` (4), `SEC-011` and `SEC-012` (2), `SEC-014` (1) | 37 |
| **Total** | **364** |

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
| **`TechOneNumberToWordsApp.Core` total** | **99.2% (250 of 252)** | **99.3% (133 of 134)** |

**`Web`**

| Class | Lines | Branches |
|---|---|---|
| `Program` | 100% | 100% |
| `ConvertEndpoint` | 100% | 100% |
| `ApiProblem` | 100% | 100% |
| `ProblemDetailsCustomizer` | 90.3% | 80.0% (see 5.4) |
| `SecurityHeadersMiddleware`, `SecurityHeadersExtensions` | 100% | 100% |
| `ConvertRequest`, `ConvertResponse` | 100% | 100% |
| **`TechOneNumberToWordsApp.Web` total** | **97.4% (111 of 114)** | **90.9% (40 of 44)** |

Coverage is evidence, not a target; there is no minimum (D46).

## 5. Code that the tests do not exercise, and why

Sections 5.1 and 5.2 are two guards that protect the library against a bug in the library itself. The validator makes both
unreachable through the public API, so no test can trigger them (D25: tests go through the public class only, with no
`InternalsVisibleTo`). Sections 5.3 and 5.4 are about the API layer, and section 5.5 is about the page's JavaScript, which
`dotnet test` does not run at all.

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
  they do not show what `dotnet run` does with the launch profile, where both ports exist (there a plain-http request gets a 307 to
  https, checked by hand on 2026-11-24). The manual case `UI-018` now covers it: open `http://localhost:5080` and the browser
  must end up on the page at `https://localhost:5443`.
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

### 5.5 `app.js` has no automated tests

The solution has no JavaScript tooling (no Node, no test runner, no package), and the brief rules out libraries, so nothing runs
`app.js` under `dotnet test`. What covers it instead:

- `SEC-014` scans the script for the calls that insert HTML (`innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`) and
  checks that it uses `textContent` (D24). It is a text scan and cannot prove safety.
- `SEC-005` shows the script, the page, the style sheet and the icon are served with the security headers (D34).
- The manual cases `UI-001` to `UI-019` (section 1.5) run the script in a browser: every response the page can get (200, 400,
  413, 500, no answer, timeout), the busy state (E6, D60), keyboard use and the console check for CSP violations.

Nothing checks the script in browsers other than the one the author uses; the cases are written for Chrome.

## 6. Still to come

- **Manual UI outcomes:** the author runs `UI-001` to `UI-019` and records each outcome (pass or fail, date, commit, browser) in
  section 1.5.
- **Slice 11:** final results (date, commit, totals, coverage), the finished matrix, and the executable decision (D20).
