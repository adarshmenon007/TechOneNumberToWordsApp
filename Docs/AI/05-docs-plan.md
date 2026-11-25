# Number to Words Web Page — Design & Plan

## Context
Tech test (Tech_Test_HR.pdf): web page + server routine converting numeric input to words
("123.45" → "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS"), passing the words back as a string output.
Deliverables: HTML UI, design-rationale doc, test plan, supporting test files (test harnesses with configuration and data,
source, solution/project files, executable if applicable), Readme.md (build/host/use), AI model + conversation history.
Constraints: C#, no third-party libraries/NuGet in the solution (test tooling exempt, D4), production quality.
Process: design → agree → TDD (tests first) → code → review, phase by phase. User makes design decisions; Claude challenges.
Decision log: this file during planning; becomes `Docs/DesignDecisions.md` (decision / rationale / rejected alternatives),
committed as the first step of the build phase, and feeds the design-rationale doc. Design doc and decision log are separate documents.
Rows are never deleted: superseded rows keep their original wording with a changed status; amended rows keep the original text plus an amendment note.
**Gate: every entry must be "Agreed" (or Superseded / Not adopted) before the build phase starts.**

## Decision log

### Platform & structure
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D1 | .NET 10 (LTS), ASP.NET Core Minimal API, thin web host | Single endpoint pair; controllers/Razor Pages/Blazor heavier than needed | Agreed |
| D2 | Projects: converter class library, web host, tests folder (unit + API tests). Names/layout: build-phase item | Separation of concerns; per-currency extension. **Amended by D40:** rationale is separation of concerns and unit testing only ("per-currency extension" dropped). **Amended by D49:** one test project (not two), with `Unit/` and `Api/` folders. Rejected: two test projects — compiler-enforced boundary, but more setup and low risk in a codebase this size. **Amended by D50:** project names and layout fixed (D50, D51) | Agreed (amended by D40, D49, D50) |
| D3 | `Models/` folder in web project; request/response DTOs are C# `record`s | Immutable DTOs | Agreed |
| D4 | xUnit + Microsoft.AspNetCore.Mvc.Testing, in test projects only | NuGet ban read as applying to the solution (class library + web), not test tooling; stated in design doc. **Amended by D48:** xUnit v3. **Setup note (slice 0):** `Microsoft.AspNetCore.Mvc.Testing` is pinned to 10.0.15, because the newest version (11.0.0) targets net11 only | Agreed (amended by D48) |
| D20 | `dotnet run` for now (Docker/self-contained executable revisited at end). `Docs/` holds design doc, decision log, test plan, AI conversation export. **Amended:** the executable is one of two end-of-project items (with CI, D36), decided at the end of the project; neither is required by the brief, so both are first to cut. Notes for that decision: a self-contained build is tied to one OS; "single-file" still ships `wwwroot` next to the exe; CI artifacts on a private repo need reviewer access and expire; `launchSettings.json` (D51) doesn't apply to a published build, so the HTTPS redirect (D35) has no port to redirect to unless one is supplied (e.g. `ASPNETCORE_HTTPS_PORT` or Kestrel endpoints in `appsettings.json`) — resolve when deciding | | Agreed (executable decision open until the end) |
| D40 | **Single currency: Australian dollars, no currency selection.** UI shows the label "Amount (AUD)" instead of a dropdown. Design doc states the brief says "dollars" without specifying, so AUD is assumed. Future work: when a second currency is actually needed, extract an interface and move unit words into currency data; unit tests go through the public converter, so that refactor is safe | Brief only shows dollars and cents; no use case for other currencies; lean development — nothing speculative. The dropdown came from our own wireframe, not the brief. Rejected (main alternative considered): multi-currency design — `ICurrencyConverter` + registry + `CurrencyDefinition` data, `GET /api/currencies`, and a dropdown; open for extension but speculative cost with no current user value | Agreed |

### Core library design
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D7 | Routine returns `ConversionResult` (IsSuccess, Words: string?, Error: ConversionError? = code + message), built only via `Success(words)` / `Failure(error)`; `[MemberNotNullWhen]` on IsSuccess | Brief's "string output parameter" is not a C#-specific term — read as "the routine's output is the words, as a string"; interpretation documented in design doc. Rejected: `TryConvert(..., out words, out error)` — adding an output is a breaking signature change across every currency (violates open/closed), not allowed in async methods/iterators, awkward in lambdas/LINQ. **Amended by D40:** "breaking change across every currency" → "breaking change for every caller". **Amended (gate 1):** `Success` and `Failure` are internal. Only Core builds results; callers only read them. **Amended (slice 9):** Core's public surface gains one member, the read-only `AmountToWordsConverter.AmountTooLongMessage` (C1). `ConversionResult`, `ConversionError` and `ConversionErrorCode` are unchanged | Agreed (amended by D40; amended) |
| D11 | Hand-written parser (no `decimal.Parse`) producing `MoneyAmount(long MajorUnits, int MinorUnits)`, no floating point. **A `MoneyAmount` cannot be created with invalid values**: private constructor + validating factory (throws `ArgumentOutOfRangeException` for negative units or minor units out of range); not a positional record, so `with` cannot bypass validation. **Amended by D40 (+F9):** cents are always 2 digits (a constant), so `MoneyAmount.Create` checks `MajorUnits ≥ 0` and `0 ≤ cents < 100`. The factory throws because an invalid value means a bug in our code, not a user error; it never returns a `ConversionResult`. The parser always catches user mistakes first, so the exception should never happen in normal use | Culture independence (no CurrentCulture traps); own algorithm per brief; integer major/minor matches how words are produced. Rejected: `decimal` (exact, but must be split back into parts). **Amended (slice 6):** MoneyAmount is a sealed class with read-only properties, a private constructor and a Create method that does the checks. Not a struct, because default(MoneyAmount) would skip Create. **Amended (gate 1):** the validator, not the parser, catches user mistakes (see D27 note) | Agreed (amended by D40; amended) |
| D13 | `ICurrencyConverter` (Code, DisplayName, `ConversionResult Convert(string input)`) + `ICurrencyConverterRegistry` (lookup by code, list all). Registered in DI as **singletons**; registry receives `IEnumerable<ICurrencyConverter>` | Stateless and immutable; a singleton must never depend on a scoped service | Superseded by D40 |
| D14 | One `StandardCurrencyConverter` configured by a `CurrencyDefinition` record (code, display name, major singular/plural, minor singular/plural, minor digits). AUD is a definition, not a class; only AUD registered. Adding NZD/USD/GBP = new definition + DI registration. **The app fails at startup if two definitions share a currency code** (compared OrdinalIgnoreCase, consistent with D30); the registry is resolved eagerly at startup so this check runs before the first request | Avoids duplicate per-currency classes / tech debt. INR (lakh/crore) would be a new converter class | Superseded by D40 |
| D15 | Currency codes are ISO 4217 strings validated via the registry; no enum. The literal "AUD" appears once, in the AUD definition | Enum must be edited for every new currency (violates open/closed); enums serialize as numbers by default | Superseded by D40 |
| D21 | `Convert` and endpoints stay synchronous (no `Task.Run`, no fake async). Async only where real I/O exists. Future DB-sourced currencies: load definitions asynchronously at startup (or a refreshable cache); lookups and conversion stay in-memory and sync. **Amended by D40:** the "currencies from a database" sentence moves to future work | CPU-bound, no I/O. Rejected: async registry now (speculative; async spreads through interfaces) | Agreed (amended by D40) |
| D23 | `AmountToWordsService.Convert(currencyCode, input)` in the class library orchestrates: shared checks (internal class, no interface) → registry lookup → currency converter. API layer: body/field presence checks + 200/400 mapping only. **Amended by D40:** no service/orchestration layer. One public **sealed** class `AmountToWordsConverter` (`ConversionResult Convert(string input)`), registered as a DI singleton, runs the D27 order, parses, then builds the words. Its helpers (parser, words builder) are `internal static` classes. API layer role unchanged | Rejected: API orchestrates (logic in web layer); template-method base class (inheritance, mixes responsibilities, can't do lookup). **Amended by D40:** instance, not static, because DI lets the 500 API test replace it with a factory that throws; it can take constructor dependencies later without changing callers; sealed because it isn't designed for inheritance. Rejected: a static class. **Amended (gate 1):** `Convert(null)` throws `ArgumentNullException`. Null is a programming error, not user input (consistent with D24e). **Amended (slice 9):** `AmountToWordsConverter` also exposes the public static `AmountTooLongMessage`, the text of the `AmountTooLong` error, so the web layer can reuse it for the 413 (C1). Nothing else is added to the public class | Agreed (amended by D40; amended) |
| D25 | Unit tests go through the public `AmountToWordsService` (behaviour, not internals); `InternalsVisibleTo` only as a logged exception. **Amended by D40:** tests go through the public `AmountToWordsConverter`; no `InternalsVisibleTo` needed. The `MoneyAmount` guard (D11) is unreachable through the public API by design, so it isn't unit-tested directly; the test plan says why | Tests survive refactoring. **Amended (gate 1):** `NumberWords`' range guard is also unreachable through the public API; the test plan explains both guards | Agreed (amended by D40; amended) |

### Conversion rules (AUD)
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D8 | British/Australian "AND": inside a 3-digit group between hundreds and the rest; before the final group if it is 1–99 and higher groups exist; never between large groups. Dollars–cents "AND" always | 1,100 → ONE THOUSAND ONE HUNDRED; 1,005 → ONE THOUSAND AND FIVE; 1,000,100 → ONE MILLION ONE HUNDRED; 101,000 → ONE HUNDRED AND ONE THOUSAND | Agreed |
| D9 | Shared checks (currency code empty, raw length cap, trim spaces+tabs, amount empty) run before currency-specific parsing. Symbol, grouping and decimal-place rules live in the currency implementation. **Amended by D40:** no shared/currency split — the converter applies the raw length cap, trims, checks for empty, then applies the character rules (order in D27) | Grouping/minor digits vary by currency (INR, OMR). **Amended by D40:** INR/OMR rationale dropped | Agreed (amended by D40) |
| D10 | Character allow-list: ASCII '0'–'9' (compared explicitly, never `char.IsDigit`), '.', ','; spaces/tabs only at start/end | Can't enumerate bad characters; `char.IsDigit` accepts Arabic-Indic/full-width digits | Agreed |
| D12 | Negative amounts rejected | MVP; no use case provided; possible future extension (refunds/credit notes) | Agreed |
| D22 | Currency symbols rejected (any Unicode CurrencySymbol char); dropdown is the sole source of currency. Specific message, e.g. "Please enter the amount without a currency symbol. The currency is selected above." **Amended by D40:** the dropdown reason no longer applies; symbols are still rejected. New message: "Please enter the amount without a currency symbol. Amounts are in Australian dollars." | Rejected: accept leading `$` (ambiguous across AUD/NZD/USD, extra placement rules); accept selected currency's symbol (over-engineering for MVP; possible later via `CurrencyDefinition.Symbol`). **Amended by D40:** rejected — accept a leading "$": friendlier, but adds edge cases ("$ 5", " $5", "-$5", "$$5", "5$") and the "Amount (AUD)" label already makes the currency clear | Agreed (amended by D40) |
| D27 | One error returned — the first match in this fixed order. API: body missing (`InvalidRequest`) → field null (`MissingField`). Service: currency empty/whitespace (`CurrencyRequired`) → raw length > 30 (`AmountTooLong`) → amount empty after trim (`AmountRequired`) → currency not registered (`UnsupportedCurrency`). AUD converter: currency symbol (`CurrencySymbolNotAllowed`) → leading '-' (`NegativeAmount`) → leading '+' (`SignNotAllowed`) → internal space/tab (`ContainsWhitespace`) → any other disallowed char, incl. non-leading '-'/'+' (`InvalidCharacter`) → more than one '.' (`MultipleDecimalPoints`) → no digits (`AmountRequired`) → bad grouping (`InvalidThousandsSeparator`) → > 2 decimals (`TooManyDecimalPlaces`) → > 15 significant integer digits (`AmountTooLarge`). Exact message wording settled while writing tests. **Amended by D40:** the two currency steps are removed. Order: API: body missing (`InvalidRequest`) → amount null (`MissingField`). Converter: raw length > 30 (`AmountTooLong`) → empty after trim (`AmountRequired`) → `CurrencySymbolNotAllowed` … `AmountTooLarge` unchanged | Specific messages before generic; characters → structure → precision → range. **Amended (slice 8):** an internal static `AmountValidator` runs these checks in this order on the input before parsing and returns the first error. The parser only ever sees valid input | Agreed (amended by D40; amended) |
| D28 | "1,234." accepted; a comma after the decimal point ("1.2,3") rejected | **Amended (slice 8):** a comma after the decimal point is rejected with `InvalidThousandsSeparator` | Agreed (amended) |
| D29 | 30-character cap measured on the raw input before trimming (longest valid input is 22 chars). **Wording fix:** the longest valid input *without leading zeros or surrounding whitespace* is 22 chars ("999,999,999,999,999.99"); with leading zeros or whitespace a valid input can reach the cap. Rule unchanged | Real abuse protection | Agreed |
| D30 | Currency code lookup case-insensitive (`StringComparer.OrdinalIgnoreCase`); code not trimmed (" AUD " → unsupported); responses always use the stored canonical code | Ordinal is culture-independent (Turkish-I) | Superseded by D40 |
| O1 | Output format: uppercase, single spaces, hyphen for 21–99 (also in cents), scale words THOUSAND / MILLION / BILLION / TRILLION, "ONE HUNDRED" not "A HUNDRED", 1,500 → ONE THOUSAND FIVE HUNDRED (no "FIFTEEN HUNDRED") | Matches brief's example | Agreed |
| E1 | "1.500" rejected (`TooManyDecimalPlaces`) | Simple rule; in de-DE "1.500" means 1500 | Agreed |
| E2 | ",", ",,", ".," → `AmountRequired` | No number present; "no digits" check precedes separator check | Agreed |
| E3 | When commas are used, the first group is 1–3 digits and must not start with 0 ("0,001", "00,500", "0,000" → `InvalidThousandsSeparator`). Leading zeros are allowed only without commas ("007" → SEVEN DOLLARS) | "0,001" means 0.001 in many locales | Agreed |
| E4 | Invisible invalid characters (whitespace other than leading/trailing space/tab, control and format chars, e.g. NBSP, newline, zero-width) are shown as a code point, e.g. "Invalid character (U+00A0)." | A blank quote in a message confuses users. **Amended (slice 8a):** visible invalid characters are shown in quotes with their code point, e.g. 'a' (U+0061), so look-alikes like a full-width ５ are clear; invisible ones (control, format, separators, lone surrogates) only as a code point. Safe because the UI uses textContent (D24) and only the errorCode is logged (D33). **Amended (gate 1):** combining marks, private-use and unassigned characters are also treated as invisible, so they show only the code point | Agreed (amended) |
| D58 | Value rules: maximum 999,999,999,999,999.99; leading zeros don't count towards the 15 digits; 0 / 0.00 / .00 / 0. → ZERO DOLLARS; exactly one uses the singular (ONE DOLLAR, ONE CENT), every other amount including zero uses the plural; cents only → cents words alone (0.45 → FORTY-FIVE CENTS); .1 → TEN CENTS; .5 → FIFTY CENTS; 5. → FIVE DOLLARS | Agreed in the design phase as an unnumbered "Value rules" line; numbered in slice 2 so the ZERO and PLUR test rows trace to a decision instead of O1. **Amended (slice 6):** whole dollars with zero cents leave the cents out, so 1.00 → ONE DOLLAR (consistent with 0.00 → ZERO DOLLARS) | Agreed (amended) |

Value rules: max 999,999,999,999,999.99; leading zeros not counted towards the 15 digits; 0 / 0.00 / .00 / 0. → ZERO DOLLARS;
0.45 → FORTY-FIVE CENTS; 5 → FIVE DOLLARS; 1 → ONE DOLLAR; 0.01 → ONE CENT; .1 → TEN CENTS; .5 → FIFTY CENTS; 5. → FIVE DOLLARS. (numbered as D58)

### HTTP API
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D16 | `GET /api/currencies` → `CurrenciesResponse` wrapper (see D26), built from the registry. `POST /api/convert` with `ConvertRequest { currencyCode, amount }`, `amount` a JSON string. **Amended by D40:** `GET /api/currencies` removed; `POST /api/convert` takes `{ amount }` only | Strings preserve commas and exact digits | Agreed (amended by D40) |
| D18 | Missing/empty body → 400; missing (null) `currencyCode` or `amount` → 400 (detail in D24e); unknown currency → 400. **Amended by D40:** covers only a missing body and a missing `amount` | | Agreed (amended by D40) |
| D19 | A specific error message per rule; "Please enter an amount" for empty / no-digit inputs | | Agreed |
| D24 | All errors are ProblemDetails (RFC 9457, built in) with an `errorCode` extension. UI shows only `detail` (fallback "Something went wrong. Please try again."). 400 `detail` = user-facing message about the input. 500 = generic "An unexpected error occurred." + `traceId` only; full exception logged server-side with the same traceId; developer exception page in Development only; API test asserts no stack trace leaks. UI inserts server text via `textContent`, never `innerHTML`. DTOs: `ConvertRequest`, `ConvertResponse(Words)`, `CurrencyResponse(Code, DisplayName)`, `CurrenciesResponse(Currencies)`. **Amended by D40:** DTOs are only `ConvertRequest` and `ConvertResponse(Words)`. **Amended by F3:** on a 500 the UI shows "Something went wrong. Please try again. Reference: <traceId>" | Rejected: custom ErrorResponse record (two error shapes once framework errors occur). **Amended (slice 9):** malformed JSON and E5 return 400 `InvalidRequest` in every environment. `RouteHandlerOptions.ThrowOnBadRequest` is set to false, because its default (true in Development only) made the request throw under `dotnet run`, and the developer exception page answered with the exception and its stack trace (a 500 once the exception handler is in). The exception handler is for unexpected exceptions only. Errors titled "Invalid request" carry no `detail`, whether the framework or the endpoint makes them (400 `InvalidRequest` and `MissingField`, 404, 405, 415), so the rule that a 400's `detail` is a user-facing message about the input applies to errors titled "Invalid amount"; the 413 is the exception, its `detail` is `AmountTooLongMessage` (C1) **Amended (slice 10):** what `app.js` shows. A 2xx with a string `words`: the words. A 500: the F3 text, never its generic `detail`. Otherwise a non-empty string `detail` (the 400 "Invalid amount" errors and the 413). Anything else (a body that is not JSON, a timeout or a network failure, D60): the fallback "Something went wrong. Please try again." with no Reference. The body is read as JSON inside a `try`, whatever its content type. All server text goes in with `textContent`; `SEC-014` scans `app.js` for `innerHTML`, `outerHTML`, `insertAdjacentHTML` and `document.write`, and `UI-009` checks the behaviour by hand | Agreed (amended by D40, F3; amended) |
| D24a | Every ProblemDetails carries an `errorCode`: conversion codes from the library enum; a small set of API-level codes in the web project (InvalidRequest, MissingField, UnsupportedMediaType, ServerError). **Amended:** add PayloadTooLarge (413, D37), NotFound (404) and MethodNotAllowed (405) (D41) | One consistent format | Agreed (amended) |
| D24b | `title` per category: "Invalid amount", "Unsupported currency", "Invalid request", "Server error" (CurrencyRequired: see D31). **Amended by D40:** titles are "Invalid amount", "Invalid request" (incl. PayloadTooLarge, NotFound, MethodNotAllowed, UnsupportedMediaType, InvalidRequest, MissingField) and "Server error" | Clear origin when logging | Agreed (amended by D40) |
| D24c | Non-JSON Content-Type → framework 415, returned as ProblemDetails | Correct HTTP semantics; UI always sends JSON | Agreed |
| D24d | `errorCode` serialized as a PascalCase string (`code.ToString()`); enum member names are part of the public API contract, stated explicitly in the design doc | System.Text.Json writes enums as numbers by default — brittle | Agreed |
| D24e | `ConvertRequest(string? CurrencyCode, string? Amount)`; endpoint takes `ConvertRequest?`; explicit null checks in the endpoint (body null → InvalidRequest; field null, absent or JSON `null` → MissingField). Null = malformed request (API); empty/whitespace = user input (service: "Please enter an amount" / "Please select a currency"). Malformed JSON, 415 and 500 get errorCode + title via `AddProblemDetails(o => o.CustomizeProblemDetails = ...)`. **Amended by D40:** `ConvertRequest(string? Amount)`; empty/whitespace handled by the converter ("Please enter an amount"); "Please select a currency" dropped | Rejected: `required`/`[JsonRequired]` (generic framework error, no errorCode); .NET 10 built-in validation (`ValidationProblemDetails` `errors` shape differs). **Amended (slice 9):** a body the framework cannot bind (malformed JSON, E5) gets an empty 400 from the framework, which the status-code handler turns into ProblemDetails `InvalidRequest` (D41). The endpoint's own checks (missing body, missing field, amount errors) build their own ProblemDetails with their own `errorCode` | Agreed (amended by D40; amended) |
| E5 | `amount` sent as a JSON number → 400 `InvalidRequest`. **Must have an API test.** Unknown extra JSON fields are ignored (framework default) | Contract says string | Agreed |
| D26 | `GET /api/currencies` returns a wrapper object `CurrenciesResponse(IReadOnlyList<CurrencyResponse> Currencies)`, not a bare array. No `defaultCurrencyCode` for now | A bare array can't gain top-level fields without a breaking change | Superseded by D40 |
| D41 | `UseStatusCodePages` so empty-body error responses (404, 405) become ProblemDetails with errorCode `NotFound` / `MethodNotAllowed` and title "Invalid request" | Keeps D24a true for every error response. **Amended (slice 9):** status code pages turns empty-body errors into ProblemDetails: 400 (malformed JSON, E5) `InvalidRequest`, 404 `NotFound`, 405 `MethodNotAllowed`, 413 `PayloadTooLarge`, 415 `UnsupportedMediaType`, all titled "Invalid request". `CustomizeProblemDetails` fills in `errorCode` and `title` only when the ProblemDetails does not already have an `errorCode`, so the endpoint's amount errors keep their own code and the title "Invalid amount". It also maps 500 (from the exception handler) to `ServerError`, title "Server error", `detail` "An unexpected error occurred." (D24) | Agreed (amended) |
| D42 | No global try/catch in `Program.cs`. The host already logs startup failures and exits non-zero; request errors are handled by the exception handler (D24) | A catch-all hides failure (exit code 0) and swallows `HostAbortedException`, which breaks `WebApplicationFactory`. Rejected: try/catch that writes the exception to the console | Agreed |

### UI
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D5 | Single page, HTML + CSS + vanilla JS, calls the JSON API, converts on submit. Wireframe layout (heading, currency dropdown, amount input + Convert button, result/error panel). Heading "Amount to Words" instead of "WELCOME". `<form>` so Enter submits; `aria-live` result; distinct error styling. **Amended by D40:** no dropdown; the input is labelled "Amount (AUD)" | **Amended (slice 10):** one output box, always on the page, `role="status"` with `aria-live="polite"` and `aria-atomic="true"`, shows the words, an error or "Converting…". It is never `hidden` (so a screen reader announces the first result); its border and padding apply only when it has text (`:not(:empty)`). An error shows a visible "Error" label, its own element, so it is not shown by colour alone. The hint under the label is just "For example 1,234.50", with no rules (D6), tied to the input with `aria-describedby`. The input has `autocomplete="off"`. The input and Convert are on one row. There is no `<noscript>` line | Agreed (amended by D40; amended) |
| D6 | No validation in the front end (no `maxlength`/`required`); amount input `type="text"` with `inputmode="decimal"`. All rules server-side in the class library | Single source of truth | Agreed |
| D17 | If `GET /api/currencies` fails or returns an empty list: show "Unable to load currencies. Please refresh the page." and disable the dropdown and Convert | | Superseded by D40 |
| E6 | Convert disabled while a request is in flight; previous result cleared when a new conversion starts | No double submits, no stale answer next to new input **Amended (slice 10):** while a request runs the Convert button is disabled and the amount field is `readOnly`, not disabled, so focus and the caret stay but the text cannot change and a stale result cannot sit beside different input. The output box shows "Converting…" in place of the old result, and `aria-busy` is set on it. A second submit is also ignored in code. All of it is undone in a `finally`, and the timeout in D60 makes sure the request ends. If focus was lost when Convert was disabled, it returns to Convert. **Amended (gate 3):** `aria-busy` is no longer set (the sentence above that says it is set is superseded): it stopped "Converting…" being announced, because a busy live region is not announced, and the disabled button already shows the busy state | Agreed (amended) |
| D60 | **The page gives a request 15 seconds** (`AbortSignal.timeout(15000)`). A timeout or a network failure shows the normal fallback, "Something went wrong. Please try again.", with no Reference and no new wording (D24, F3) | Without a limit, a server that accepts the request and never answers leaves Convert disabled and the field read-only until the browser gives up, which can take minutes (E6). Rejected: no timeout; separate wording for network failures (more text for the same action, which is to try again). Checked by hand in `UI-019` (no automated JavaScript tests) | Agreed |

### Cross-cutting (proposed in the final pass)
| # | Decision | Rationale / alternatives | Status |
|---|---|---|---|
| D31 | Rename the currency title category to **"Invalid currency"**, covering both `CurrencyRequired` and `UnsupportedCurrency`. Full mapping: amount codes → "Invalid amount"; currency codes → "Invalid currency"; InvalidRequest / MissingField / UnsupportedMediaType → "Invalid request"; ServerError → "Server error" | Alternative: keep "Unsupported currency" and add "Currency required" (five categories) | Not adopted (superseded by D40) |
| D32 | Don't echo the currency code back: `UnsupportedCurrency` message is "The selected currency is not supported." No separate length cap; overall size bounded by D37 | Removes reflected-input and log-injection risk at the root. Alternative: cap at 3 chars (ISO 4217 length) and keep echoing | Not adopted (superseded by D40) |
| D33 | Logging with built-in `ILogger` (console provider), levels set in `appsettings.json`. Unhandled exceptions → Error with traceId. Conversion failures → **Information** with `errorCode` only (no raw input). Structured message templates, never string concatenation. **Logging lives in the web project only**; the class library doesn't log | Built in; no Serilog (NuGet ban). Raw input is user-controlled, so not logged. A user typo isn't a system problem, hence Information. `ILogger` in a class library needs a NuGet package (Microsoft.Extensions.Logging.Abstractions). **Amended (slice 9):** a successful conversion logs nothing. A rejected amount logs one Information entry from the endpoint with the `errorCode` as a structured value (`{ErrorCode}`) and never the raw input. Tested with a small in-memory `ILoggerProvider` in the test project (no package): one Information entry with `ErrorCode`, and a unique marker in the input appears in no entry (text, state, scopes or exception). On a 500, one Error entry carries the exception, and the middle part of the response's `traceId` equals the `TraceId` in the log scope (C2) **Amended (slice 9):** the log category of the endpoint's entries is the full type name of `ConvertEndpoint`; a static class cannot be an `ILogger<T>` argument, so the handler gets the logger from `ILoggerFactory`, and the logging tests use `typeof(ConvertEndpoint)` for the category | Agreed (amended) |
| D34 | Small custom middleware adding security headers on every response: `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`. Server header removed via Kestrel `AddServerHeader = false` (not in the middleware). No inline JS/CSS — separate `app.js` / `site.css`. **API test asserts the headers are present** | No NuGet (e.g. NWebsec) allowed; CSP backs up `textContent`. **Amended (slice 9):** the headers are added with `Response.OnStarting` in a middleware placed before the exception handler and static files, so error responses and static files get them too. The API tests cover 200, 400, 404 and 500 in slice 9, and `index.html` in slice 10. The absence of a `Server` header (`AddServerHeader = false`) is asserted on real Kestrel only, because TestServer never sends one **Amended (slice 10):** static files sit after the security headers, the HTTPS redirect and the exception handler, and before the status code pages (`UseDefaultFiles`, then `UseStaticFiles`), so the page and its assets get the headers too. `SEC-005` is one theory over `/`, `/app.js`, `/site.css` and `/favicon.svg`: each answers 200 with its expected media type and the four headers with their exact values. The page must stay compatible with the CSP (no inline script, `<style>`, `style=` attribute or event-handler attribute); `UI-017` checks the browser console for violations, and no automated test scans for them | Agreed (amended) |
| D35 | HTTPS: `UseHttpsRedirection` always; `UseHsts` outside Development. Dev-cert trust step (`dotnet dev-certs https --trust`) near the top of the Readme so a reviewer's first run works. API test clients use an `https://localhost` base address so the redirect can never interfere | Standard production setup; HSTS in dev causes local pain | Agreed |
| D36 | CI: GitHub Actions workflow — on push and PR: setup .NET 10, restore, build (warnings as errors), `dotnet test`, upload test results as an artifact. **"If time"** — first item cut if behind. Readme notes the repo is private, so reviewers may need access to see Actions runs | Repeatable evidence the tests pass. Remote is GitHub (confirmed). **Amended:** CI isn't required by the brief, so it is the first thing cut (with the executable, D20). **Amended (gate 1):** CI isn't in the brief's requirements, so it moves to the very end, after submission-critical work, as a nice-to-have | Agreed (if time; amended) |
| D37 | **Global** Kestrel limit `MaxRequestBodySize = 1 KB`. Oversized requests → 413, errorCode `PayloadTooLarge`, title "Invalid request". Whatever the framework actually raises when the limit trips during JSON binding (413 / 400 / 500 — to verify) is mapped to 413 PayloadTooLarge. Test: automated API test on real Kestrel if `WebApplicationFactory` supports it, otherwise a manual test-plan case; either way it asserts 413 + PayloadTooLarge, **not** 400 AmountTooLong, proving the limit fired before the converter. **Amended by C1:** the 413's `detail` is identical to `AmountTooLong`'s. **The real-Kestrel test uses plain `http`**: the body limit has nothing to do with HTTPS, and GitHub Actions has no dev certificate by default, so an https test could fail in CI for the wrong reason (the `https://localhost` rule in D35 still applies to the in-memory API tests) | Bounds every field and any future endpoint; nothing else accepts a body. Kestrel default is ~30 MB. In-memory TestServer doesn't enforce Kestrel limits. **Setup check (slice 0, 2026-11-23):** `WebApplicationFactory.UseKestrel(0)` works over plain http, and a body over the limit during JSON binding returns a bare `413` with an empty body (not 400 or 500). So the mapping to `PayloadTooLarge` is done by the D41 status-code handler. Still to confirm in slice 9: the exception handler (D24) does not turn it into a 500. **Amended (slice 9):** Kestrel enforces the limit; the empty-body 413 is turned into ProblemDetails by the status-code handler (D41): `errorCode` `PayloadTooLarge`, title "Invalid request", `detail` `AmountToWordsConverter.AmountTooLongMessage` (C1). A scratch app (outside the repo, 2026-11-24) returned 413 for a body over a 1 KB limit in Development and Production, with and without the exception handler, never a 500. The real-Kestrel tests over plain http (SEC-006 declared length, SEC-007 chunked) confirm it against our own pipeline; the status moves to "verified at setup" when they pass. **Verified (slice 9, step 7):** SEC-006 (declared length) and SEC-007 (chunked) pass on real Kestrel over plain http, with SEC-010 showing the limit is exactly 1,024 bytes, so the status is "verified at setup". With `ThrowOnBadRequest = false` (D24) malformed JSON and E5 are 400 in Development too | Agreed (verified at setup; amended) |
| D38 | UI served by the same app from `wwwroot` (`UseDefaultFiles` + static files). Same origin, so no CORS policy | Was implied but never recorded | Agreed |
| D39 | Build settings in `Directory.Build.props`: `TargetFramework` net10.0, nullable enabled, warnings as errors, implicit usings, `AnalysisLevel` **latest** (not latest-recommended). File mentioned in the Readme | Enforces the null-safety attributes in D7. latest-recommended's extra rules would be noise as errors on a deadline (e.g. LoggerMessage suggestions). **Amended by D45:** also sets `<RunSettingsFilePath>` to the shared `.runsettings` file, so plain `dotnet test` picks it up and a reviewer needs no flags. **Setup note (slice 0):** with warnings as errors, xUnit1051 makes every async test call pass `TestContext.Current.CancellationToken` | Agreed (amended by D45) |

### Test harness & traceability
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D43 | Conversion cases live in an **external JSON file** (`tests/.../TestData/conversion-cases.json`). Row: `id`, `decisions[]`, `description`, `input`, and either `expectedWords` or `expectedErrorCode` (+ optional `messageContains`). A **self-validating loader** (System.Text.Json, no package) fails the run on a duplicate ID, a row with both or neither expectation, or an `expectedErrorCode` that isn't a real enum member. Tests take only the case ID and look the row up. HTTP and behaviour checks (E5, missing body/amount, 415/404/405, 500, security headers, 413) stay as ordinary C# tests | Example row: `{ "id": "AND-003", "decisions": ["D8"], "description": "No AND between large groups", "input": "1,000,100", "expectedWords": "ONE MILLION ONE HUNDRED DOLLARS" }`. Rejected: `[InlineData]` / `TheoryData` (data mixed into code, invisible characters easy to miss, weak fit with the brief's "test data"); CSV (inputs contain commas so every input needs quoting; no built-in .NET CSV reader; no escape syntax for NBSP/tab/emoji; Excel silently changes data — "007"→7, ".5"→0.5, "1e5"→100000). **Setup note (slice 0):** the loader's check that `expectedErrorCode` is a real enum member is added once the error-code enum exists. **Setup note (slice 2):** the loader also rejects unknown JSON fields (`UnmappedMemberHandling.Disallow`), so a misspelt field like `messageContain` fails the run instead of being ignored, and rejects rows with an empty decisions list. **Amended (slice 8a):** lone-surrogate inputs can't be written in JSON, so they're a C# test (VAL-021) with Id and Decision traits. IDs are shared across the JSON file and C# tests, so the next VAL row in the JSON starts at VAL-022. **Amended (gate 1):** the loader also rejects explicit nulls in fields that can't be null (`RespectNullableAnnotations`), with the row's position in the message | Agreed (amended) |
| D44 | **One data file drives both layers.** Every row runs through `AmountToWordsConverter.Convert` and through `POST /api/convert` (words → 200 + `words`; errorCode → 400 + same `errorCode` + title "Invalid amount") | Proves the layers agree, and checks that NBSP/tab/emoji survive the JSON-over-HTTP round trip. Rejected: API runs a tagged subset (drifts); separate files (two sources of truth). **Amended (slice 9):** each row also checks the response content type (`application/json` for 200, `application/problem+json` for 400) and, for rejections, that `detail` contains the row's `messageContains` | Agreed (amended) |
| D45 | Test configuration: (a) minimal `xunit.runner.json` (parallelism, method-name display, `$schema`); (b) separate `.runsettings` (results folder, `.trx`, coverage) referenced by `<RunSettingsFilePath>` in `Directory.Build.props` (amends D39); (c) **one shared API test factory** forcing the `Testing` environment (`WebApplicationFactory` defaults to Development, where the developer exception page would break the 500 test) — used by the 500 test (a converter that throws, registered via `ConfigureTestServices`) and the D37 spike (`UseKestrel`, port 0, plain http). No `appsettings.Testing.json` unless something needs it | Reviewer runs plain `dotnet test` with no flags. **Amended (slice 9):** the shared API factory takes the environment name (default `Testing`). The bad-request cases run under `Testing` and `Development`; the 500 test runs only under `Testing`, because Development turns on the developer exception page. The factory also registers the in-memory `ILoggerProvider` used by the logging tests (D33) | Agreed (amended) |
| D46 | Coverage: Microsoft's collector through the test SDK (`--collect "Code Coverage;Format=cobertura"`), Cobertura output, uploaded as a CI artifact (D36), percentage recorded in the test plan, **no hard minimum**. **No `coverlet.collector`** in the project (remove it from the template). Verify at setup that the collector ships with the test SDK; coverlet is the fallback only if it doesn't | Evidence, not a target; avoids shipping two coverage tools. **Verified at setup (slice 0, 2026-11-23):** the Microsoft collector writes a `.cobertura.xml` with no coverlet package. **Amended (slice 0):** coverage is configured in `test.runsettings` (Code Coverage data collector, Cobertura format, test project excluded with `.*Tests\.dll$`), so plain `dotnet test` collects coverage for Core and Web only and no `--collect` flag is needed; confirmed on the first green run in slice 2 | Agreed (verified at setup) |
| D47 | Test IDs: category prefix + number, never reused. 13 categories: `WORD`, `AND`, `SCALE`, `CENT`, `ZERO`, `PLUR`, `FMT`, `VAL`, `ORD`, `LIM`, `API`, `SEC`, `UI` (ZERO and PLUR kept as their own headings because they're the classic edge cases reviewers check first). Links: a `decisions` field in the JSON; traits on C# tests (`[Trait("Id", ...)]`, `[Trait("Decision", ...)]`). Test plan traces **R0–R7** (R0 = IO example; R1–R7 = the brief's "Please include" list) and **S1–S6** (the six submission requirements, e.g. production quality → D33–D39; no libraries/NuGet → D4/D11; AI model + conversation history → D57) → decisions → test IDs → `.trx` results (date, commit, totals, coverage). Manual `UI-xxx` cases are written in the test plan with steps and a hand-recorded outcome. The test plan doesn't copy every data row; it links to the file | IDs stable across amendments because decision links live in a separate field. Rejected: sequential `TC-001` (meaningless); decision-based `D8-003` (breaks when decisions are amended/superseded) **Amended (slice 10):** `SEC-005` is one theory over four files (one ID, four runs; D34) and `SEC-014` scans `app.js` for calls that insert HTML (D24). `SEC-013` was proposed and dropped (the console check in `UI-017` covers CSP violations), so it is never used, like `API-001`, `API-002` and `SEC-009`. The manual cases are `UI-001` to `UI-019` (`UI-018` checks the http to https redirect by hand, D35; `UI-019` checks the timeout, D60); the author records their outcomes | Agreed (amended) |
| D48 | **xUnit v3** (each `TheoryDataRow` can carry its own traits, so `--filter "Decision=E5"` also finds JSON rows; with v2 traits apply per method and would miss them) on the **classic VSTest runner** (`xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`), so `.runsettings` and `--collect "Code Coverage"` work as designed. Rejected: Microsoft.Testing.Platform (coverage needs a different package and flag; `.runsettings` support partial). Confirm both claims at setup. **Amended at setup (slice 0, 2026-11-23):** the plain `xunit.v3` package does not work with `dotnet test` on the .NET 10 SDK: it pulls in Microsoft.Testing.Platform, and the SDK stops with "Testing with VSTest target is no longer supported" (the `UseMicrosoftTestingPlatformRunner=false` property doesn't help). The test project references **`xunit.v3.mtp-off`** (same xUnit v3 API and version 4.0.3, without Microsoft.Testing.Platform) plus `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`. **Verified:** `dotnet test` runs on VSTest, `<RunSettingsFilePath>` in `Directory.Build.props` is picked up (`.trx` written to `TestResults`), and per-row traits work (`--filter "Decision=E5"` finds only the row that carries it) | | Agreed (amended; verified at setup) |
| D49 | **One test project** with `Unit/` and `Api/` folders; the shared JSON file lives in it, so no file linking. Amends D2 | See D2 | Agreed |

### Build phase: layout, workflow, documents
| # | Decision | Rationale / rejected alternatives | Status |
|---|---|---|---|
| D50 | Names: projects `TechOneNumberToWordsApp.Core` (class library), `.Web`, `.Tests`; solution `TechOneNumberToWordsApp.sln` (classic `.sln`, not `.slnx`); matches the GitHub repo. `Core` and `Web` under `src/`, the test project under `tests/`; `Docs/` keeps its casing. Namespaces follow project names. Main class stays `AmountToWordsConverter` (D23): it converts a currency amount, not any number. Library is flat (about seven files); the endpoint lives in its own class (`ConvertEndpoint`); `Program.cs` is only the composition root. Web layout: `Models/`, `Endpoints/`, `Errors/` (API error codes + ProblemDetails customisation), `Middleware/` (D34), `Properties/launchSettings.json`, `appsettings.json`, `wwwroot/` (`index.html`, `app.js`, `site.css`, `favicon.svg`). Test layout: `Unit/`, `Api/`, `TestData/conversion-cases.json`, `xunit.runner.json`, `test.runsettings` (D43–D45, D49) | Rejected: `.Converter` (reads oddly next to the class), `.Domain` (implies more than we have), `.slnx` (older Visual Studio can't open it; no gain for three projects), sub-folders in a seven-file library. **Setup note (slice 0):** the JSON loader lives in `TestData/` next to `conversion-cases.json`, because both the unit and API layers use it. **Amended (gate 1):** Core has nine files. Internal types are `AmountParser`, `AmountValidator`, `MoneyAmount`, `NumberWords` and `MoneyWords` **Amended (slice 9):** the Web types are `public`: it is an application, not a library, so nothing outside depends on them, and the tests use `typeof(ConvertEndpoint)` (D25 rules out `InternalsVisibleTo`) | Agreed (amended) |
| D51 | Repo files: `global.json` `{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }` (low version so any .NET 10 SDK works); `Properties/launchSettings.json` with fixed ports `https://localhost:5001` and `http://localhost:5000` so the Readme URL is correct (used only by `dotnet run` / Visual Studio, not by a published build); `appsettings.json` with the D33 log levels and `IncludeScopes` (C2); minimal `.editorconfig` (formatting only, no rule-severity changes, so it can't turn analyzer suggestions into errors under D39); `.gitattributes`. `.gitignore`: the repo's existing standard `dotnet new gitignore` template already ignores `bin/`, `obj/`, `TestResults/` (via `[Tt]est[Rr]esult*/`), `*.user` and Cobertura output (`coverage*.xml`), so only the existing `Tech_Test_HR.pdf` and `.claude/settings.local.json` entries need keeping. The brief PDF stays out of the repo; the wireframe photo goes into `Docs/` (added by the author) | Reproducible builds; correct Readme URL. **Amended:** the fixed ports are now `https://localhost:5443` and `http://localhost:5080` (not 5001/5000), avoiding 5000 and 7000, which macOS AirPlay Receiver uses; the Readme uses these URLs and needs no AirPlay note **Amended (slice 9):** `AllowedHosts` stays `"*"` in `appsettings.json` (the template default); host filtering is set per deployment | Agreed (amended) |
| D52 | TDD order: slice 0 setup (commit `Docs/DesignDecisions.md` first; solution, projects, build files; setup checks D37/D46/D48) → 1 JSON loader and its self-validation, **no dedicated tests for the loader** (its self-checks fail loudly with a clear message; tests for test code add little) → 2 zero and units (ZERO, PLUR, WORD; introduces `ConversionResult`, `MoneyAmount`, simplest parser) → 3 10–99 → 4 hundreds + AND → 5 scales + boundaries + maximum → 6 cents (the brief's "123.45" row, R0) → 7 valid formats (trim, leading zeros, separators, `"1,234."`) → 8 rejections in D27 order + order tests + message wording (F2, F5) → **review gate 1 (library)** → 9 API (Testing factory; all JSON rows via HTTP; MissingField, missing body, E5, 415/404/405, 500, security headers, 413 spike on real Kestrel over http; logging) → **gate 2 (API)** → 10 UI (E6, F3, manual `UI-xxx` cases) → **gate 3 (UI)** → 11 finish (CI if time, coverage run, DesignDocument, TestPlan with results, Readme, AI export, executable decision) → final review | Library first (the risk is the algorithm), then thin layers. Rejected: API-first with a stub converter; rejections before words. **Amended by D53:** the baseline commit of `Docs/DesignDecisions.md` is made on `main` by the author before slice 0; slice 0 proper (solution, projects, build files, setup checks) goes on `phase/1-library`. **Amended (slice 0):** slice 1 is folded into slice 2: the first theory test over the JSON rows drives the loader, so there is no separate loader slice; the other slice numbers stay the same. **Amended (slice 2):** MoneyAmount and the parser are introduced in the first slice whose tests need them (parser in slice 3), not slice 2. **Amended (slice 2):** the test plan and Readme are drafted at each review gate; slice 11 finishes and edits them. **Amended (slice 6):** slice 6 covers every decimal-point form (123.45, 0.45, .45, .5, 5., 0., .00); slice 7 keeps trimming, leading zeros, separators and 1,234. **Amended (slice 8):** slice 8 is split. 8a: error-code enum, `Failure`, validator and checks 1–7 (length, empty, currency symbol, leading minus, leading plus, inner space/tab, invalid character). 8b: checks 8–12 (decimal points, no digits, separators, decimal places, too large) and the order tests **Amended (slice 9):** D35 was missing from slice 9's list in this row. It was added to slice 9 as step 6b, after the security headers (step 6) and before the 413 on real Kestrel (step 7): `UseHsts()` outside Development and `UseHttpsRedirection()` always. It has no test of its own; the real-Kestrel tests in step 7 run over plain http, which shows the redirect does not get in the way of it **Correction (slice 9, gate 2):** the note above says the real-Kestrel tests show the redirect does not get in the way. They show that only when no https port is known, as in those tests. With both ports, as under the launch profile, a plain-http request gets a 307 redirect to https (checked by hand, 2026-11-24), and no test covers that case (see TestPlan 5.3) | Agreed (amended by D53; amended) |
| D53 | Git workflow: separate `test:` (red) / `feat:` (green) / `refactor:` / `docs:` commits, **pushed at the end of each slice** (CI only ever sees green); one branch per phase (library / API / UI / docs) merged by PR with a **merge commit, not squash** (squash would erase the red/green history). `Docs/DesignDecisions.md` is the first commit, straight to `main`, as the agreed baseline; branch-per-phase starts with the library phase. Claude commits and pushes only when told. **Amended:** slice 0 (setup) goes on the library branch, `phase/1-library`, not on `main`, so `main` only ever holds the agreed baseline and reviewed merges; later phases use `phase/2-api`, `phase/3-ui` and `phase/4-docs`. The library PR therefore contains the setup commits and is reviewed at gate 1 | Rejected: red/green commits pushed immediately (red CI runs); one commit per slice (TDD invisible); slice 0 directly on `main` | Agreed (amended) |
| D54 | Deliverables mapped to the brief: R0 IO example → data row (slice 6); R1 UI → `wwwroot/`; R2 design doc → `Docs/DesignDocument.md`; R3 test plan → `Docs/TestPlan.md`; R4 supporting files → solution, projects, tests, JSON data, `xunit.runner.json`, `.runsettings` (executable: D20); R5 C#; R6 `Readme.md`; R7 AI model + history → `Docs/AI/`. S1 production quality → D24, D33–D39, D41, D42, tests, CI; S2 explain every line → design doc's algorithm walkthrough, small readable classes; S3 no internet → a statement in the design doc **plus the committed `.claude/settings.json`, which denies `WebSearch` and `WebFetch`** (verifiable evidence); S4 no libraries/NuGet → D4/D11, design doc lists the only packages (test tooling); S5 deadline; S6 private repo + reviewer access (invite collaborators; Readme notes access is needed to see CI runs, D36) | | Agreed |
| D55 | Document split: `Docs/DesignDecisions.md` = the full decision log, including superseded and amended rows with their history, the F-items and future work. `Docs/DesignDocument.md` = the R2 narrative for the reviewer; links to the log instead of copying it (the author edits it into their own words). `Docs/TestPlan.md` = the R3 test plan | | Agreed |
| D56 | Readme outline (11 sections): title + one-line summary with the brief's example; quick start (.NET 10 SDK, **trust the dev cert** near the top, `dotnet run`, URL); using the page; API reference; running the tests; project structure; configuration; troubleshooting; CI; documentation and AI usage; limitations and future work. Additions: Troubleshooting notes that `dotnet dev-certs https --trust` doesn't fully work on Linux (use the http URL) and how to find a log entry from the UI reference (C2). **The `errorCode` table lives in one place, the Readme's API reference**; the design doc states the names are a public contract (D24d) and links to it. Section 10 names the same three models as `Docs/AI/README.md` and links to it | | Agreed |
| D57 | Conversation export (option B, per phase): the design-phase `/export` happens **before the first commit**; check it includes the earliest turns, and if the session has already been summarised fall back to the raw JSONL for the design phase after a privacy pass. At every review gate: `/export` into numbered files in `Docs/AI/`, a copy of the plan file next to it (e.g. `Docs/AI/01-design-phase-plan.md`), and `Docs/DesignDecisions.md` updated if any decision changed; Claude reminds at each gate. `Docs/AI/README.md` discloses **three** AI uses with what each was for: Claude Opus 5.5 and Claude Sonnet 5.5 in Claude Code, and Claude Opus 5.5 on claude.ai as a reviewer (explaining options and trade-offs before decisions such as D40; those chats are added by the author), and states that every decision was the author's | R7 evidence stays accurate and complete. Rejected: one export at the end (design phase at risk of being summarised) | Agreed |
| D59 | GitHub release: before inviting reviewers on Thursday, Adarsh creates release v1.0.0 on GitHub's Releases page, tagging the final main commit, with release notes (what it is, how to run it, link to the Readme). An executable is attached only if D20 decides to ship one | Marks exactly which version was submitted | Agreed |

Future work (recorded in the design doc): multi-currency support (D40), currencies from a database (D21), rate limiting
(built-in ASP.NET Core rate limiter), health-check endpoint, API versioning (`/api/v1`), localisation of messages (MVP is English only).

## Final pass — F-items
- **F1** `InvalidCharacter` reports whole Unicode code points: iterate by `Rune`, so an emoji isn't shown as half a surrogate pair. The 30-char cap stays measured as `string.Length` (UTF-16 units). — **Agreed**
- **F2** Limits (15 integer digits, 2 decimal places) are constants in the converter; the messages are built from those constants, not hard-coded "2" / "999,…". — **Agreed** (amended by D40: constants live in `AmountToWordsConverter`, not `StandardCurrencyConverter`). **Amended (gate 1):** the limit constants are internal; `MaxDecimalPlaces` must stay 2 (cents are fixed, D40). **Amended (slice 9):** the limit constants stay internal. The one message the web layer needs, `AmountTooLong`'s, is exposed as the public static `AmountToWordsConverter.AmountTooLongMessage` (see C1)
- **F3** On a 500 the UI shows "Something went wrong. Please try again. Reference: <traceId>". — **Agreed** (amends D24). **Amended (slice 10):** the reference is the `traceId` from the 500's body, the full W3C id (C2); if the body has none, the Reference part is left out. A timeout or a network failure shows the plain fallback without a Reference (D60)
- **F4** Order of currencies in `GET /api/currencies`: alphabetical by display name. — **Superseded by D40**
- **F5** D27's "settle message wording while writing tests" covers the generated messages (F2). — **Confirmed**
- **F6** D18 aligned with D24e. — **Confirmed** (D18 now covers only the amount)
- **F7** D9 omitted the empty-currency check. — **Superseded by D40**
- **F8** D27 draft "Currency 'XYZ' is not supported." conflicted with D32. — **Superseded by D40**
- **F9** `MoneyAmount` minor-unit range needed the currency's minor digits. — **Resolved by D40** (cents fixed at 2 digits, see D11)

## Verification notes (section 7 checks)
- **`public partial class Program`:** not needed. Confirmed in the installed SDK: ASP.NET Core 10's built-in source generator (`Microsoft.AspNetCore.App.SourceGenerators.dll`, `PublicTopLevelProgram.Generated.g.cs`) emits `public partial class Program { }` for top-level-statement apps.
- **`WebApplicationFactory` on real Kestrel:** to my knowledge .NET 10's Microsoft.AspNetCore.Mvc.Testing added `UseKestrel()` + `StartServer()`, but the package isn't in the local NuGet cache, so this is **unverified**. First build-phase spike: try it in the D37 test; fallback is a manual test-plan case (per D37). **Superseded (slice 0, 2026-11-23):** confirmed by the D37 setup check (`UseKestrel(0)` works over plain http); no further spike.
- **D37 framework behaviour:** unverified until code exists. The D37 API test is written first (red), documents what the framework actually returns, and the mapping to 413 PayloadTooLarge is made to pass it.
- **D35 redirect vs API tests:** the in-memory TestServer has no HTTPS port configured, so `UseHttpsRedirection` logs a warning and doesn't redirect; API test clients use an `https://localhost` base address regardless (recorded in D35), so tests can't be affected even if an HTTPS port is configured in CI.

## Mandatory test cases (to appear in the test data)
| Input | Expected |
|---|---|
| ".5" | FIFTY CENTS |
| "5." | FIVE DOLLARS |
| " 5 " (spaces), "\t5\t" (tabs), " \t5\t " (mixed) | FIVE DOLLARS |
| "12 3" | ContainsWhitespace |
| "+5" | SignNotAllowed |
| "$5" | CurrencySymbolNotAllowed |
| "", "   ", "." | AmountRequired |
| "$" | CurrencySymbolNotAllowed |
| "-5" / "5-" | NegativeAmount / InvalidCharacter |
| "1.2.3" | MultipleDecimalPoints |
| "1e5" | InvalidCharacter |
| 30-char "000…05" (28 zeros + "05") / 31-char equivalent | FIVE DOLLARS / AmountTooLong |
| "0000000000000001" (16 chars) | ONE DOLLAR (leading zeros not counted) |
| "1", "0.01", "1.01" | ONE DOLLAR, ONE CENT, ONE DOLLAR AND ONE CENT |
| "0", "0.00", ".00", "0." | ZERO DOLLARS |
| full-width "５", Arabic-Indic "٥" | InvalidCharacter |
| "5 " (NBSP) | InvalidCharacter, message contains "(U+00A0)" |
| "5😀" | InvalidCharacter, message contains "(U+1F600)" (F1) |
| "1,1", "12,34", ",123" | InvalidThousandsSeparator |
| "$-5" | CurrencySymbolNotAllowed (D27 order) |
| "-1.2.3" | NegativeAmount (D27 order) |
| "1,000,000,000,000,000.123" | TooManyDecimalPlaces (precision before range) |
| "1,000,100" | ONE MILLION ONE HUNDRED DOLLARS |
| "2,000,050" | TWO MILLION AND FIFTY DOLLARS |
| "101,000" | ONE HUNDRED AND ONE THOUSAND DOLLARS |
| 999 / 1,000 | NINE HUNDRED AND NINETY-NINE DOLLARS / ONE THOUSAND DOLLARS |
| 999,999 / 1,000,000 | NINE HUNDRED AND NINETY-NINE THOUSAND NINE HUNDRED AND NINETY-NINE DOLLARS / ONE MILLION DOLLARS |
| 999,999,999 / 1,000,000,000 | … NINETY-NINE DOLLARS / ONE BILLION DOLLARS |
| 999,999,999,999 / 1,000,000,000,000 | … NINETY-NINE DOLLARS / ONE TRILLION DOLLARS |
| 999,999,999,999,999.99 / 1,000,000,000,000,000 | max value in words / AmountTooLarge |
| Within-group boundaries: 9/10, 19/20, 99/100; cents .99 / 1.00 | words per O1 |
| E1–E5 inputs; "0,001"; "1.2,3"; "1,234." | per rules above |

## Consistency-pass items (resolved)
- **C1 — Agreed (amended).** A body over the 1 KB limit returns status 413 with errorCode `PayloadTooLarge`, and its `detail` uses exactly the same wording as `AmountTooLong`. The UI shows only `detail` (D24) and a page user can only trigger a 413 by pasting a huge amount, so they see the same message whether they paste 40 or 4,000 characters. The errorCode still lets logs and API tests tell the two cases apart. (Amends D37.) **Amended (slice 9):** the 413's `detail` is `AmountToWordsConverter.AmountTooLongMessage`, a public static read-only string that `AmountValidator` also returns for `AmountTooLong`, so the wording exists once. The 413 keeps its own `errorCode` (`PayloadTooLarge`).
- **C2 — Agreed.** `"IncludeScopes": true` for the console logger in `appsettings.json`; no custom exception logging. The Readme or test plan must note: the reference shown in the UI is the full W3C id (`00-<traceId>-<spanId>-01`), while the log shows only the 32-character TraceId — search the log for the middle part. **Amended (gate 2):** the last part of the response's trace id is the trace flags, `-00` (not sampled) or `-01` (sampled), so the id reads `00-<TraceId>-<SpanId>-<flags>`; the responses seen in slice 9 end in `-00`. The 32-character `TraceId` in the middle is still what the log shows
- **C3 — Agreed (amended).** Ship a small `favicon.svg` in `wwwroot` with a `<link rel="icon">` in the page (avoids a JSON 404 for `/favicon.ico` and is allowed by the CSP). **Amended (slice 10):** the icon is a small plain `favicon.svg` (a few shapes, no text, no external references) linked with `<link rel="icon" href="/favicon.svg" type="image/svg+xml">`; a `data:` URI would be blocked by the CSP

## Pending before build phase
1. Test harness — done (D43–D49). Build-phase items — done (D50–D57).
2. **Gate check (2026-11-23):** 69 rows; 6 Superseded (D13, D14, D15, D17, D26, D30), 2 Not adopted (D31, D32), 61 Agreed. No row is Proposed or Pending. Five Agreed rows carry an explicit open qualifier: D20 (executable decided at the end), D36 (CI "if time"), and the confirm-at-setup checks D37 (real-Kestrel test and 413 behaviour), D46 (coverage collector ships with the test SDK), D48 (xUnit v3 + VSTest + per-row traits).
3. Cleanup approach for `Docs/DesignDecisions.md` **approved** with these changes:
   - Keep: all decision tables with every superseded/amended row, F-items, C1–C3, future work. Header becomes a baseline header with a status legend and a document map; files that don't exist yet are marked "(to be written)" — `DesignDocument.md`, `TestPlan.md`, `Docs/AI/README.md` — until they're created.
   - No "Open at baseline" table (the Status column shows the five open qualifiers: D20, D36, D37, D46, D48).
   - Remove: pending lists, verification notes (already in the slice 0 checklist below), the mandatory test table (moves to the JSON data and `Docs/TestPlan.md`).
   - Replace `C:\Users\adars` with `%USERPROFILE%` in the plan copy saved to `Docs/AI/` and in `DesignDecisions.md` if any local path appears.
4. Execution sequence (needs plan mode exited): remind the author to run `/export` (design phase) → author checks it includes the earliest turns (raw-JSONL fallback after a privacy pass if not) → save the plan copy as `Docs/AI/01-design-phase-plan.md` → write `Docs/DesignDecisions.md` → wait for the author's go-ahead before committing to `main`.

### Slice 0 checklist (moved here from the verification notes)
- Already verified: `public partial class Program` is not needed (ASP.NET Core 10's built-in source generator `PublicTopLevelProgram.Generated.g.cs` emits it).
- To verify: D46 collector ships with the test SDK; D48 xUnit v3 per-row traits + VSTest runner + `.runsettings`; D37 `WebApplicationFactory` `UseKestrel()` (real Kestrel, port 0, http) and what the framework actually returns when the 1 KB limit trips during JSON binding (map to 413 `PayloadTooLarge`); `<RunSettingsFilePath>` set in `Directory.Build.props` with an absolute `$(MSBuildThisFileDirectory)` path; D35 redirect doesn't affect the API tests (in-memory server has no HTTPS port; clients use an `https://localhost` base address).

## Slice 9 open items (phase 2, raised 2026-11-24; options and recommendations, none decided yet)

Phase 2 is on `phase/2-api`, branched from `main` at `1617a64`. Nothing below is decided until the author says so; once decided,
each outcome becomes an amendment note on the decision named (never a deletion), in `Docs/DesignDecisions.md` and this file.

### 1. C1: the 413 `detail` must equal `AmountTooLong`'s, without copying the text
**Background.** C1 and D37 say a 413 reuses `AmountTooLong`'s `detail`. That text is built inside the internal `AmountValidator` from
`AmountToWordsConverter.MaxInputLength` (30), which gate 1 made `internal` (F2 amendment: "if slice 9 needs the length for the 413
message, we'll revisit then"). The 413 is produced by the web layer before the converter ever runs, so Web cannot reach the text.

| Option | How | For | Against |
|---|---|---|---|
| A (recommended) | Core exposes the one message: `public static string AmountTooLongMessage { get; }` on `AmountToWordsConverter`. `AmountValidator` returns that same property for check 1. Web uses it for the 413 `detail`. | One source for the wording; Web never sees the number 30; the constants stay `internal` (F2 amendment unchanged); one read-only string is the smallest possible public addition. Static, because it needs no state and the status-code handler can read it without DI. | Public API grows by one member (must be named in the D7/D23 notes and the Readme API list). |
| B | Public `ConversionError AmountTooLong` property (code + message). | Carries the code as well. | The 413 has its own `errorCode` (`PayloadTooLarge`), so Web would use only the message; bigger surface for no gain. |
| C | Make the constants `public` again and have Web build the text. | Simple. | Reverses the gate 1 decision (L2), and the text is then built in two places. |
| D | Copy the string into Web; add a test that the 413 `detail` equals the `AmountTooLong` `detail`. | No API change. | It is the copy C1 wanted to avoid; the test only detects drift after the fact. |
| E | `InternalsVisibleTo` from Core to Web. | Nothing to add. | Rejected in D25; opens every internal type to Web. |

**Test (written first).** One API test: send a 31-character amount and read the 400's `detail` (`AmountTooLong`); send a body over 1 KB
on real Kestrel and read the 413's `detail`; assert they are equal and that `errorCode` differs (`AmountTooLong` vs `PayloadTooLarge`).
**Amends:** C1 (how), F2 (message public, constants still internal), D23/D7 (one added public member).

### 2. The 413 approach
**Background.** D37: global Kestrel `MaxRequestBodySize` = 1 KB; the slice 0 check showed a body over the limit during JSON binding
yields a bare `413` with an empty body (not 400 or 500). D41 uses `UseStatusCodePages` for empty-body 404 and 405 only.

| Option | How | For | Against |
|---|---|---|---|
| A (recommended; the author's preference) | Keep the built-in Kestrel limit. Amend D41 so the status-code handler covers every empty-body error the framework produces: 404 `NotFound`, 405 `MethodNotAllowed`, 413 `PayloadTooLarge` (415 `UnsupportedMediaType` is already in D24a). One `CustomizeProblemDetails` maps status to `errorCode` and title; the 413 `detail` comes from item 1. | The framework enforces the limit before and while reading (declared and chunked bodies); no code of ours sits on the hot path; one mechanism covers all four statuses. | Relies on the framework returning a bare status; if a later version throws instead, the test below fails first. |
| B | Middleware that checks `Content-Length` and rejects over 1 KB. | Explicit. | Misses chunked bodies, which have no length; duplicates what Kestrel does. |
| C | Catch `BadHttpRequestException` in the exception handler and map 413. | Covers the "framework throws" case. | Logged as a server error unless filtered; the handler gets more branches. |
| D | Per-endpoint `RequestSizeLimit` metadata instead of a global limit. | Scoped. | D37 chose global so any future endpoint is bounded too; no gain with one endpoint. |

**Evidence (scratch app outside the repo, 2026-11-24, `MaxRequestBodySize = 1024`, `UseStatusCodePages` + `AddProblemDetails`):** a 3 KB
body returned **413** with a ProblemDetails body in Development and in Production, with and without `UseExceptionHandler`; the
exception handler did not turn it into a 500. This answers the open D37 question for plain http on real Kestrel; the real test in
slice 9 still confirms it against our own pipeline.
**Tests (first):** 413 on real Kestrel over plain http (D37) with `errorCode` = `PayloadTooLarge` and **not** `AmountTooLong`; once with a
declared `Content-Length` and once chunked; then a 404 and a 405 row so the amended D41 mapping is pinned for all of them.
**Amends:** D41 (add 413, note 415 is already covered), D37 (record the confirmed behaviour).

### 3. D37 in Development: `ThrowOnBadRequest`
**Confirmed by experiment, with a narrower scope than suspected.** A minimal API with a nullable record parameter (`ConvertRequest?`),
run with `ASPNETCORE_ENVIRONMENT` set to each value (same code, `UseExceptionHandler` and `UseStatusCodePages` on):

| Request | Development | Production |
|---|---|---|
| Malformed JSON body | **500** | 400 |
| `amount` sent as a JSON number (E5) | **500** | 400 |
| Content-Type `text/plain` (D24c) | 415 | 415 |
| Body over the limit (D37) | 413 | 413 |
| Unknown route / wrong method | 404 / 405 | 404 / 405 |
| Valid body; empty body with JSON type | 200; handler gets null | 200; handler gets null |

So in Development, **malformed JSON and E5 turn into 500s**; 415 and 413 are not affected. Cause: `RouteHandlerOptions.ThrowOnBadRequest`
defaults to true only in Development, so the binding failure is thrown instead of answered as 400. Setting it to false makes
Development match Production: re-running with `builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false)`
(namespace `Microsoft.AspNetCore.Routing`) gave 400 for both. Our tests run in `Testing` (D45), so they would not notice; a reviewer
who starts the app with `dotnet run` (Development) would hit it on a bad request.

| Option | How | For | Against |
|---|---|---|---|
| A (recommended) | Set `ThrowOnBadRequest = false` in `Program.cs`, with a comment saying why. | One line; same behaviour in every environment; the framework still produces the status, and our `CustomizeProblemDetails` adds `errorCode`. | None found. |
| B | Bind the body ourselves (`HttpRequest`, `JsonSerializer`), mapping errors by hand. | Full control of messages. | More code and branches for the same result; moves JSON handling out of the framework. |
| C | Catch `BadHttpRequestException` in the exception handler and map to 400. | Works in any environment. | Treats a client mistake as an exception, adds log noise, and the developer exception page can intercept first in Development. |
| D | Leave it and document "Development differs". | No code. | A reviewer sees a 500 in a normal run; contradicts D24 (400 for malformed requests). |

**How to test.** Make the shared API factory (D45) take the environment name. Run the bad-request cases (malformed JSON, E5, missing
body, 415, 404, 405) under both `Testing` and `Development` with one theory over the environment; run the **500 test only under
`Testing`**, because Development turns on the developer exception page, which prints a stack trace by design (D24). Write it first and
see the Development rows fail with 500, then add the one line. Also add a one-line manual check to the test plan: `dotnet run`, post
`{"amount":5}`, expect 400.
**Amends:** D37 (add the Development behaviour and the setting), D24 (400 in every environment), D45 (factory takes an environment).

### 4. The logging test (D33)
**Goal.** A rejected amount logs `errorCode` at Information, and the raw input never appears in any log entry.

| Option | How | For | Against |
|---|---|---|---|
| A (recommended; the author's preference) | A small `ILoggerProvider` in the test project (about 40 lines: an in-memory list of category, level, formatted message, structured state, scopes, exception), registered through the shared factory's `ConfigureLogging`. | No new package; automated; sees structured values as well as the text; also usable for the 500 test (Error entry with the same `traceId`). | We maintain a few lines of test tooling (D52: no tests for test code, so it must stay trivial). |
| B | `FakeLogger` from `Microsoft.Extensions.Diagnostics.Testing`. | Maintained and richer. | A new test-only package for something 40 lines do; the author wants none. |
| C | Redirect console output and parse it. | No provider. | Fragile (formatting, buffering, parallel tests). |
| D | Manual test-plan case. | Cheap. | Not repeatable; the "never logged" promise is exactly what must not rot. |

**Test (written first).** Post an amount with a unique marker (for example `"12abc-MARKER-7"`, which gives `InvalidCharacter`), then assert:
exactly one entry from our endpoint's category at **Information**; its structured state has `ErrorCode` = `InvalidCharacter` (so the
message template uses `{ErrorCode}`, never string concatenation, D33); and **no entry from any category, at any level, in text, state,
scopes or exception, contains the marker**. A second test for a 500 asserts one **Error** entry with the exception and the same trace id as
the response. A third asserts a successful conversion logs nothing at Information or above from our category (D33 is silent on this;
recommended: log nothing, since it has no business value and would carry user data). **Amends:** D33 (note the test; record the
success-path rule once decided).

### Housekeeping
- The verification note on `WebApplicationFactory` and real Kestrel is stale: **confirmed in the slice 0 setup check** (D37: `UseKestrel(0)` works over plain http). No new spike.
- Probe app: `scratchpad/probe-api` (outside the repo), kept only as evidence for items 2 and 3; nothing from it is in the repo.
- Proposed order for slice 9, to confirm with the decisions above: test factory (environment, logging provider) → conversion rows over HTTP → error shape and mapping (MissingField, InvalidRequest, E5, 415, 404, 405) → 500 → security headers (D34) → 413 on real Kestrel → logging. Each as a red `test:` then a green `feat:`; stop and show before any push (gate 2 at the end).

## Slice 9 decisions (agreed 2026-11-24): amendment wording, test IDs and order

**Status: approved by the author on 2026-11-24 (with the changes below); applied to the decision rows in `Docs/DesignDecisions.md`
and in this file in the step 0 `docs:` commit.** The notes are appended to each row's existing text, after ". ".

### A. Amendment notes (as applied)

| Row | Note appended | Status after |
|---|---|---|
| C1 | **Amended (slice 9):** the 413's `detail` is `AmountToWordsConverter.AmountTooLongMessage`, a public static read-only string that `AmountValidator` also returns for `AmountTooLong`, so the wording exists once. The 413 keeps its own `errorCode` (`PayloadTooLarge`) | Agreed (amended) |
| F2 | **Amended (slice 9):** the limit constants stay internal. The one message the web layer needs, `AmountTooLong`'s, is exposed as the public static `AmountToWordsConverter.AmountTooLongMessage` (see C1) | unchanged |
| D7 | **Amended (slice 9):** Core's public surface gains one member, the read-only `AmountToWordsConverter.AmountTooLongMessage` (C1). `ConversionResult`, `ConversionError` and `ConversionErrorCode` are unchanged | Agreed (amended by D40; amended) |
| D23 | **Amended (slice 9):** `AmountToWordsConverter` also exposes the public static `AmountTooLongMessage`, the text of the `AmountTooLong` error, so the web layer can reuse it for the 413 (C1). Nothing else is added to the public class | Agreed (amended by D40; amended) |
| D24 | **Amended (slice 9):** malformed JSON and E5 return 400 `InvalidRequest` in every environment. `RouteHandlerOptions.ThrowOnBadRequest` is set to false, because its default (true in Development only) made the request throw under `dotnet run`, and the developer exception page answered with the exception and its stack trace (a 500 once the exception handler is in). The exception handler is for unexpected exceptions only. Errors titled "Invalid request" carry no `detail`, whether the framework or the endpoint makes them (400 `InvalidRequest` and `MissingField`, 404, 405, 415), so the rule that a 400's `detail` is a user-facing message about the input applies to errors titled "Invalid amount"; the 413 is the exception, its `detail` is `AmountTooLongMessage` (C1) | Agreed (amended by D40, F3; amended) |
| D24e | **Amended (slice 9):** a body the framework cannot bind (malformed JSON, E5) gets an empty 400 from the framework, which the status-code handler turns into ProblemDetails `InvalidRequest` (D41). The endpoint's own checks (missing body, missing field, amount errors) build their own ProblemDetails with their own `errorCode` | Agreed (amended by D40; amended) |
| D33 | **Amended (slice 9):** a successful conversion logs nothing. A rejected amount logs one Information entry from the endpoint with the `errorCode` as a structured value (`{ErrorCode}`) and never the raw input. Tested with a small in-memory `ILoggerProvider` in the test project (no package): one Information entry with `ErrorCode`, and a unique marker in the input appears in no entry (text, state, scopes or exception). On a 500, one Error entry carries the exception, and the middle part of the response's `traceId` equals the `TraceId` in the log scope (C2) | Agreed (amended) |
| D34 | **Amended (slice 9):** the headers are added with `Response.OnStarting` in a middleware placed before the exception handler and static files, so error responses and static files get them too. The API tests cover 200, 400, 404 and 500 in slice 9, and `index.html` in slice 10. The absence of a `Server` header (`AddServerHeader = false`) is asserted on real Kestrel only, because TestServer never sends one | Agreed (amended) |
| D37 | **Amended (slice 9):** Kestrel enforces the limit; the empty-body 413 is turned into ProblemDetails by the status-code handler (D41): `errorCode` `PayloadTooLarge`, title "Invalid request", `detail` `AmountToWordsConverter.AmountTooLongMessage` (C1). A scratch app (outside the repo, 2026-11-24) returned 413 for a body over a 1 KB limit in Development and Production, with and without the exception handler, never a 500. The real-Kestrel tests over plain http (SEC-006 declared length, SEC-007 chunked) confirm it against our own pipeline; the status moves to "verified at setup" when they pass (step 7). With `ThrowOnBadRequest = false` (D24) malformed JSON and E5 are 400 in Development too | Agreed (verify at setup; amended); at step 7: Agreed (verified at setup; amended) |
| D41 | **Amended (slice 9):** status code pages turns empty-body errors into ProblemDetails: 400 (malformed JSON, E5) `InvalidRequest`, 404 `NotFound`, 405 `MethodNotAllowed`, 413 `PayloadTooLarge`, 415 `UnsupportedMediaType`, all titled "Invalid request". `CustomizeProblemDetails` fills in `errorCode` and `title` only when the ProblemDetails does not already have an `errorCode`, so the endpoint's amount errors keep their own code and the title "Invalid amount". It also maps 500 (from the exception handler) to `ServerError`, title "Server error", `detail` "An unexpected error occurred." (D24) | Agreed (amended) |
| D44 | **Amended (slice 9):** each row also checks the response content type (`application/json` for 200, `application/problem+json` for 400) and, for rejections, that `detail` contains the row's `messageContains` | Agreed (amended) |
| D45 | **Amended (slice 9):** the shared API factory takes the environment name (default `Testing`). The bad-request cases run under `Testing` and `Development`; the 500 test runs only under `Testing`, because Development turns on the developer exception page. The factory also registers the in-memory `ILoggerProvider` used by the logging tests (D33) | Agreed (amended) |

### B. New test IDs (categories `API` and `SEC`, D47). Each is a C# test with `Id` and `Decision` traits

IDs are never reused, so the gaps stay: `API-001`, `API-002` and `SEC-009` were dropped in design before any test existed and are not used.
Every response check covers status, content type `application/problem+json`, `errorCode` and `title`, not only the status.
Rows marked "both" run under `Testing` and `Development` (one theory over the environment name; the ID is the same, the display name
shows the environment).

| ID | Decisions | What it checks | Environments |
|---|---|---|---|
| `API-003` | D18, D24e | Empty body with JSON content type → 400 `InvalidRequest`, title "Invalid request" | both |
| `API-004` | D24e | `{}` (amount absent) → 400 `MissingField`, title "Invalid request" | both |
| `API-005` | D24e | `{"amount":null}` → 400 `MissingField` | both |
| `API-006` | D24e, D41 | Malformed JSON → 400 `InvalidRequest`, title "Invalid request" | both |
| `API-007` | E5, D41 | `{"amount":5}` (a JSON number) → 400 `InvalidRequest` | both |
| `API-008` | E5 | An unknown extra field is ignored → 200 | both |
| `API-009` | D24c, D41 | Content-Type `text/plain` → 415 `UnsupportedMediaType`, title "Invalid request" | both |
| `API-010` | D41 | Unknown route → 404 `NotFound`, title "Invalid request" | both |
| `API-011` | D41 | `GET /api/convert` → 405 `MethodNotAllowed`, title "Invalid request" | both |
| `API-012` | D24, D41 | A converter that throws → 500, `application/problem+json`, `errorCode` `ServerError`, title "Server error", `detail` "An unexpected error occurred.", a `traceId` | Testing only |
| `API-013` | D33 | A rejected amount logs one Information entry from the endpoint with structured `ErrorCode` | Testing |
| `API-014` | D33 | A successful conversion logs nothing at Information or above from the endpoint | Testing |
| `API-015` | D33, C2 | On a 500: one Error entry with the exception; the middle part of the response `traceId` equals the `TraceId` in the log scope | Testing only |
| `SEC-001` | D34 | The four headers (exact values) on a 200 | Testing |
| `SEC-002` | D34 | The same headers on a 400 | Testing |
| `SEC-003` | D34 | The same headers on a 404 | Testing |
| `SEC-004` | D34 | The same headers on a 500 | Testing only |
| `SEC-005` | D34 | The same headers on `index.html` (static file) | Slice 10 (ID reserved) |
| `SEC-006` | D37, C1 | Body over 1 KB, declared `Content-Length` → 413, `application/problem+json`, `errorCode` `PayloadTooLarge` (not `AmountTooLong`), title "Invalid request", `detail` = `AmountTooLongMessage` | Real Kestrel, plain http |
| `SEC-007` | D37 | The same with a chunked body (no `Content-Length`) | Real Kestrel, plain http |
| `SEC-008` | D34 | A response from real Kestrel has no `Server` header | Real Kestrel, plain http |
| `SEC-010` | D37 | A body of exactly 1,024 bytes is not rejected by the limit: 400 `AmountTooLong` (the converter ran); 1,025 bytes gives the 413. If the first run says otherwise, the number is amended and the observation recorded | Real Kestrel, plain http |
| `SEC-011` | D33 | The marker in a rejected amount appears in no log entry (any category, level, text, state, scope, exception) | Testing |
| `SEC-012` | D24 | A 500 body contains no stack trace, exception message or exception type name | Testing only |

**Core unit test (not HTTP):** `LIM-012` (D23, C1): `AmountToWordsConverter.AmountTooLongMessage` equals the message of the
`AmountTooLong` error for a 31-character input. Together with `SEC-006` it proves the 413 `detail` matches (this replaces the dropped `SEC-009`).

**Conversion rows over HTTP (D44).** The 162 data rows run a second time through `POST /api/convert` under **their existing IDs**: one
theory `Api_Convert_MatchesExpectedOutcome(caseId)` in `Api/`, `Testing` environment. It checks words → 200, content type `application/json`
and `words`; a rejection → 400, content type `application/problem+json`, the same `errorCode`, title "Invalid amount" and `detail` containing
`messageContains`. This theory replaces the dropped `API-001` and `API-002`. The two C# library tests (`VAL-021`, `VAL-044`) have no HTTP
twin: a lone surrogate cannot be sent as JSON, and null is covered by `API-004` and `API-005`. Because an ID now selects both runs,
`--filter "Id=CENT-001"` runs two tests; the test plan (at gate 2) reports the library and HTTP counts separately.

### C. Slice 9 order (each step: red `test:` commit, then green `feat:` commit, then show; nothing pushed until the author says)

0. `docs:` apply section A to both files (D37 stays "verify at setup" until step 7).
1. Core: `LIM-012` and `AmountTooLongMessage`; the validator uses it (no behaviour change; the 164 library tests stay green).
2. Test infrastructure: the shared factory with an environment parameter, the in-memory logger provider and the HTTP theory over the rows. Red: Web has no endpoint yet.
3. Endpoint, DTOs, `ConvertEndpoint`, `Program.cs` as composition root: the data rows go green.
4. Request errors and status-code mapping: `API-003` to `API-011` under both environments (Development red first, then `ThrowOnBadRequest = false`); `CustomizeProblemDetails` with the "only if no `errorCode`" rule.
5. The 500 path: `API-012`, `SEC-012`; exception handler.
6. Security headers middleware: `SEC-001` to `SEC-004`.
6b. **D35 added to slice 9 here (2026-11-24), at the author's request; it was missing from this order.** `UseHsts()` outside Development and `UseHttpsRedirection()` always, placed after the security headers. No new test: the in-memory tests already use `https://localhost`. The real-Kestrel tests in step 7 run over plain http with no https port configured, so the redirect middleware is inert there and they say nothing about a server that knows its https port (where plain http gets a 307 to https; see D52's correction note and TestPlan 5.3).
7. The 413 on real Kestrel: `SEC-006`, `SEC-007`, `SEC-008`, `SEC-010`; global `MaxRequestBodySize` = 1 KB, `AddServerHeader = false`. When green, D37's status becomes "Agreed (verified at setup; amended)" citing SEC-006/007.
8. Logging: `API-013`, `API-014`, `SEC-011`, then `API-015`.
9. Gate 2 housekeeping, with no PR until the author asks.

### D. Points decided
- Framework-made errors carry no `detail`; the 413 is the exception (recorded in D24).
- The HTTP theory also checks `messageContains` against `detail` and the content type (recorded in D44).
- `SEC-010` uses 1,024 bytes; amend if the first run disagrees.

### E. Notes for later steps (from the author, 2026-11-24)
- **Disposal:** every `IDisposable` is disposed, in the tests and in production code: `using` on every factory a test creates itself (for example `new ApiFactory("Development")`), and on clients and responses.
- **Logging tests (step 8):** a class fixture shares one factory, so `Logs` collects entries from every test in the class. The logging tests use their own factory, or look only at their own entries (by the marker or the `TraceId`), so an "exactly one entry" check cannot pick up another test's logs.
- **Gate 2, test plan:** under "what isn't exercised and why", say that C2's `IncludeScopes` (console output) is not covered by automated tests, because the tests clear the console provider and read scopes through the in-memory provider instead.
- **Step 3 temporary guard:** `ConvertEndpoint` throws `ArgumentNullException` for a missing amount until step 4, whose first tests (`API-004`, `API-005`) make it a 400 `MissingField`. This keeps those tests genuinely red first.

## Slice 10 open items (phase 3, raised 2026-11-24; options and recommendations, none decided yet)

Phase 3 is on `phase/3-ui`, branched from `main` at `e598fef` (PR 2 merged). Nothing below is decided until the author says so; once
decided, each outcome becomes an amendment note on the decision named (never a deletion), in `Docs/DesignDecisions.md` and this file.

**Constraints already in the log.** D34: the CSP is `default-src 'self'; script-src 'self'; style-src 'self'`, so the page may have no inline
script, no `<style>`, no `style="…"` attributes and no `onclick=` handlers; everything is in `app.js` and `site.css`. D24: server text goes
in with `textContent`, never `innerHTML`; the UI shows only `detail`, with the fallback "Something went wrong. Please try again." D6: no
front-end validation (no `maxlength`, `required` or `pattern`). D38: served from `wwwroot` by the same app, so no CORS. The brief is "plain,
production quality", so no framework, no build step, no visual polish beyond readable and accessible.

**What the wireframe (`Docs/Lo-Fi wireframe.jpg`) shows:** a heading; "Select currency" with a dropdown (dropped by D40); an "Amount" label with
the input and the Convert button on one row; below them one rounded box, "words will be displayed here or any error message".

### 1. Page layout and wording (D5, D6, D40)

| Option | What | For | Against |
|---|---|---|---|
| A (recommended) | The wireframe without the dropdown: `<main>`, `<h1>Amount to Words</h1>` (D5), a `<form>` with `<label for="amount">Amount (AUD)</label>` (D40), the input and **Convert** on one row, a one-line hint under the label, and one output box below | Matches what the author drew; fewest parts | None found |
| B | Input above, button below | Simpler on a phone | Differs from the wireframe for no gain: A also wraps at narrow widths |
| C | Add examples, a copy button or history | Friendlier | Not asked for; more to test |

Details to confirm under A:
- **Words and attributes.** `<html lang="en-AU">`, `<title>Amount to Words</title>`, the viewport meta. The input is `type="text" inputmode="decimal" autocomplete="off"` with no `maxlength`, `required` or `pattern` (D6). The button is `type="submit"` inside the form, so Enter submits.
- **Hint.** "Digits, an optional decimal point and commas, for example 1,234.50.", tied to the input with `aria-describedby`. Recommended: yes, because the label alone does not say commas are fine.
- **Output box: one or two live regions.** Recommended: one box, `role="status"` with `aria-live="polite"` and `aria-atomic="true"`, used for words, errors and "Converting…" (as the wireframe has one box). An error gets a class and a visible "Error" label, so it is not shown by colour alone. Alternative: a second `role="alert"` box for errors, which interrupts a screen reader; more correct for severe errors, but two elements for one box.
- **Empty box.** Recommended: hidden (`hidden` attribute) until there is something to show, so no empty bordered box. Alternative: placeholder text "The amount in words will appear here." (as in the wireframe).
- **Styling.** One small `site.css`: system font stack, a centred column about 36 rem wide, a visible focus outline, the error border and label, wrapping to a single column at 320 px and fine at 200% zoom. No dark mode, no framework.
- **No JavaScript.** Recommended: nothing special (the form does nothing useful without `app.js`). Alternative: a `<noscript>` line saying the page needs JavaScript. Small, but one more thing to word and test; recommended: skip.

### 2. How `app.js` handles each response (D24, F3, C1, C2)

The request is `POST /api/convert` with `{ "amount": <the field's value, untouched> }` (no trimming here, D6; the library trims).

| Outcome | What the user sees | Why |
|---|---|---|
| 200 | the `words`, as text | |
| 400 "Invalid amount" (every amount rule) | the `detail` | D24 |
| 413 | the `detail`, which is the same sentence as `AmountTooLong` (C1) | a pasted 4,000-character amount reads the same as a 40-character one |
| 400, 404, 405, 415 "Invalid request" (no `detail`; the page cannot normally cause them) | "Something went wrong. Please try again." | D24 fallback |
| 500 | "Something went wrong. Please try again. Reference: *traceId*" | F3; the `traceId` is the full W3C id, and the log shows the middle part (C2) |
| A body that is not JSON (a proxy's error page, an empty body) | the fallback; "Reference" only if a `traceId` was found | |
| **No answer at all** (offline, server stopped, request timed out) | "Could not reach the server. Please try again." | **new wording, not in the log** |

**Rule (recommended).** Read the body as JSON inside a `try`, whatever the content type. A 2xx with a string `words` shows the words. A 500 always
shows the F3 text, never its generic `detail` (F3 replaces it). Otherwise a non-empty string `detail` is shown. Anything else gets the fallback.
All text goes in with `textContent`. The button is re-enabled in a `finally`.

**Two new behaviours the author should accept or refuse** (each would be an amendment to D24 or F3):
- **The network-failure wording** above. Alternative: reuse the fallback, which is less helpful ("try again" with no hint that the server is unreachable).
- **A 15 second timeout** (`AbortSignal.timeout(15000)`), reported with the same network-failure wording. Without it, a server that accepts the request and never answers leaves the button disabled until the browser gives up, which can be minutes (E6). Alternative: no timeout (nothing extra to test).

**Shape.** One classic script (`<script src="app.js" defer>`), strict mode, no globals, about 60 lines in small functions: `convert(amount)` returns what to show, `show(kind, text)` renders it. `app.js` has no automated tests: the solution has no JavaScript tooling and D47 already puts UI checks in manual `UI-xxx` cases (item 7). Two cheap text-scan guards are an option in item 6.

### 3. E6: no double submit, no stale answer

Recommended: on submit, return at once if a request is in flight; otherwise mark busy, **disable the button** (and set `aria-busy` on the output), clear the old
words or error, show "Converting…" in the output box, and in the `finally` re-enable. The input stays enabled, so the cursor and focus are not lost
and the user can keep editing. A disabled default button also stops Enter from submitting; the JavaScript guard stays as the belt to that.
Alternatives: also disable the input (rejected: it drops focus); no "Converting…" text (a slow request would then show an empty page and say nothing to
a screen reader; the cost is one more string to test).

### 4. C3: favicon

Recommended: a small `favicon.svg` (a few plain shapes, no text, no external references) and `<link rel="icon" href="/favicon.svg" type="image/svg+xml">`.
The CSP allows it (`default-src 'self'`), and a browser with the link tag does not ask for `/favicon.ico`, so no ProblemDetails 404 in the console.
Alternatives: no icon (a 404 in the console for `/favicon.ico`); an inline `data:` URI (the CSP blocks it). Browsers that still ask for `/favicon.ico` get the
normal 404 ProblemDetails, which is harmless.

### 5. Static files in the pipeline (D34, D35, D38)

| Option | Pipeline | For | Against |
|---|---|---|---|
| A (recommended) | `UseSecurityHeaders` → `UseHsts` (not Development) → `UseHttpsRedirection` → `UseExceptionHandler` (not Development) → **`UseDefaultFiles` → `UseStaticFiles`** → `UseStatusCodePages` → `MapConvertEndpoint` | Headers and the redirect cover the page; a file the server cannot read gives the generic 500; a path with no file still falls through to the 404 ProblemDetails (`API-010` stays green); `UseDefaultFiles` must come before `UseStaticFiles` | None found |
| B | Static files before `UseHttpsRedirection` | The page would load over http | Breaks D35 |
| C | Static files after `UseStatusCodePages` | Same result for a 404 | Only differs for odd statuses such as 416, which would become ProblemDetails; no benefit |
| D | `MapStaticAssets()` (build-time fingerprints and compression) | Modern default | More machinery than four small files need; D38 already says `UseDefaultFiles` + static files |

Notes: (a) D34 already says static files get the headers through `OnStarting`; SEC-005 proves it. (b) The static file middleware sends `ETag` and `Last-Modified` but no
`Cache-Control`. Recommended: leave it (revalidation is the default and the log is silent); if a reviewer ever sees a stale `app.js`, add `no-cache` then. (c) The
red run of SEC-005 will also show whether `WebApplicationFactory` finds `wwwroot` (its content root is the Web project folder).

### 6. SEC-005 (and two optional guards)

**SEC-005 (D34), recommended:** one theory over `/`, `/app.js`, `/site.css` and `/favicon.svg`. Each must answer 200 with the expected media type (`text/html`, the script
type the framework reports for `.js`, `text/css`, `image/svg+xml`) and the same four headers with the exact values (the helper in `SecurityHeadersApiTests` moves to a shared place:
a small `refactor:`). It runs under `Testing`. Red first: the files and the middleware do not exist, so every row is 404. The script type is asserted as observed and the
observation is recorded. One ID, four runs, like `API-003`.

**Two optional guards on the page's source (the author decides):**

| ID | Decisions | What it checks | Limit |
|---|---|---|---|
| `SEC-013` | D34 | `index.html`, fetched over HTTP, has no inline `<script>`, no `<style>`, no `style=` attribute and no `on…=` handler (the browser would block these silently under the CSP, so the page would just break) | A text scan; it does not run a browser, so `UI-017` still checks the console |
| `SEC-014` | D24 | `app.js` does not contain `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write` (server text only goes in as text) | A text scan; it cannot prove safety, `UI-009` checks the real behaviour |

Recommended: yes to both; they are about ten lines each and fail the day someone adds the forbidden thing. Alternative: rely on the manual cases only. They would
live in `Api/StaticFilesApiTests.cs` with `SEC-005`. Counts: SEC-005 adds 4 test runs for 1 ID, `SEC-013` and `SEC-014` add 1 each.

### 7. Manual UI cases (`UI-xxx`, D47, E6, F3)

**How they are run.** Start the app (`dotnet run --project src/TechOneNumberToWordsApp.Web`), open `https://localhost:5443` in Chrome with the dev certificate trusted and
DevTools open (Console and Network). The author records the outcome of each case by hand (pass or fail, date, commit, browser) in a table in `Docs/TestPlan.md`; the
Outcome column starts empty. Recommended: before the author's run, Claude does a dry run in Chrome with the browser extension to catch defects early; the recorded outcomes
are still the author's.

| ID | Decisions | Steps | Expected |
|---|---|---|---|
| `UI-001` | R0, R1, D5, D40 | Type `123.45`, click **Convert** | The box shows ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS. The label reads "Amount (AUD)"; there is no currency dropdown; the heading is "Amount to Words" |
| `UI-002` | D5 | Type `5`, press Enter without clicking | FIVE DOLLARS |
| `UI-003` | D58 | Convert `1`, then `0.01`, then `.5` | ONE DOLLAR, ONE CENT, FIFTY CENTS |
| `UI-004` | D6, D29 | Paste a 40-character string of digits. Check the field, then click **Convert** | The field keeps all 40 characters (no `maxlength`); the box shows the "amount is too long" message from the server |
| `UI-005` | D24, D19 | Convert `12a` | The server's message about the invalid character `'a' (U+0061)`, in error styling with a visible "Error" label; no words |
| `UI-006` | D19 | Leave the field empty, click **Convert** | "Please enter an amount." |
| `UI-007` | D22 | Convert `$5` | "Please enter the amount without a currency symbol. Amounts are in Australian dollars." |
| `UI-008` | C1, D37 | Paste about 2,000 digits, click **Convert** | The same "too long" message as `UI-004`; the Network tab shows a 413 |
| `UI-009` | D24, D34 | Convert `<b>x</b>` | The message appears as literal text naming `'<' (U+003C)`; nothing is bold; the box holds a single text node (Elements panel) |
| `UI-010` | E4 | Convert `5😀`, then a full-width `５` | Each message shows the character and its code point correctly |
| `UI-011` | E6 | Network throttling "Slow 3G". Convert `5`; while it is pending press Enter again | The button is disabled, the old result is gone, "Converting…" shows, only one request appears in the Network tab, and when it finishes the button is enabled and the new result shows |
| `UI-012` | E6 | Convert `5`, then `12a`, then `5` again | Each result replaces the last: the old words never sit beside an error, and the old error is gone after a success |
| `UI-013` | F3 | In the Console run `window.fetch = () => Promise.resolve(new Response(JSON.stringify({status:500,traceId:"00-abc-def-00"}), {status:500, headers:{"Content-Type":"application/problem+json"}}))`, then Convert `5` | "Something went wrong. Please try again. Reference: 00-abc-def-00"; the button is enabled again. Reload the page afterwards |
| `UI-014` | D24 (and the network wording, if accepted) | DevTools Network "Offline" (or stop the server), Convert `5`; then go back online and Convert again | "Could not reach the server. Please try again."; the button is enabled again; the next conversion works without reloading |
| `UI-015` | R1 (accessibility basics) | Keyboard only: Tab through the page; click the label; press Enter in the field. In DevTools > Accessibility, inspect the box | Tab goes input then button, in that order; the focus outline is visible; the label click focuses the field; Enter submits; the box is a `status` live region and the hint is the input's description |
| `UI-016` | R1 | Zoom to 200%, then narrow the window to 320 px | No horizontal scrolling and nothing clipped; the input and button wrap |
| `UI-017` | D34, C3 | Reload with the Console and Network cleared | No console errors or CSP violation messages; no 404; the icon loads as `image/svg+xml`; `/` shows the four security headers |
| `UI-018` (optional) | D35 | Open `http://localhost:5080` | It redirects to `https://localhost:5443` and the page loads. Optional because the author dropped the manual `dotnet run` check at gate 2; it is the only place the redirect with a known https port would be seen |

### 8. Proposed order for slice 10 (each code step: red `test:` then green `feat:`, then show; nothing pushed until the author says)

0. `docs:` apply the decided outcomes of items 1 to 7 to the decision rows (D5, D24/F3, D34, D38, E6, C3, and D47 for the new IDs) in `Docs/DesignDecisions.md` and this file.
1. `refactor:` move the security-headers assertion into a shared test helper (`SecurityHeadersApiTests` keeps passing).
2. `test:` `SEC-005` (and `SEC-013`, `SEC-014` if accepted) in `Api/StaticFilesApiTests.cs`. Red: 404s.
3. `feat:` `UseDefaultFiles` and `UseStaticFiles` in the item 5 position, and `wwwroot` with `index.html`, `site.css`, `app.js` and `favicon.svg`. Green.
4. Dry run in Chrome; fixes as small commits.
5. `docs:` the `UI-xxx` table in `Docs/TestPlan.md` (Outcome empty) and the UI section's counts. The Readme's "Using the page" stays for slice 11 as planned.
6. Gate 3: the author runs the cases and records the outcomes; self-review, then the fresh-context reviewer; `/export` into `Docs/AI/` and a plan copy.

### Decisions needed from the author
Item 1 (layout A, the hint, one live region, hidden empty box, no `<noscript>`); item 2 (the network-failure wording, the 15 second timeout); item 3 ("Converting…" text);
item 4 (favicon); item 5 (pipeline A, leave caching); item 6 (SEC-005 as a four-row theory, `SEC-013` and `SEC-014` yes or no); item 7 (the case list, `UI-018` in or out, the Chrome dry run).

## Slice 10 decisions (agreed 2026-11-24): outcomes, final UI cases and order

**Status: approved by the author on 2026-11-24 (with the changes below); applied to the decision rows in `Docs/DesignDecisions.md` and in this file in the
step 0 `docs:` commit** (new D60; amendments to D5, D24, F3, D34, E6, C3 and D47).

### A. What was decided

| Item | Outcome |
|---|---|
| 1 Layout | Option A. One `role="status"` box, always on the page and never `hidden`; border and padding only when it has text (`:not(:empty)`), so a screen reader announces the first result. Hint is just "For example 1,234.50" (no rules, D6). `autocomplete="off"` (in the D5 amendment). No `<noscript>` |
| 2 Responses | The rule as proposed (D24 amendment). **15 second timeout is a new decision, D60.** A timeout or a network failure shows the normal fallback, "Something went wrong. Please try again."; no new wording, no Reference |
| 3 E6 | Button disabled **and the field `readOnly`** while the request runs (focus stays, no editing, so no stale result). "Converting…" in the box |
| 4 Favicon | As proposed (C3 amendment) |
| 5 Pipeline | Option A, unchanged (D34 amendment). Caching left as it is |
| 6 Tests | `SEC-005` as one theory over the four files (D34). `SEC-014` yes. **`SEC-013` dropped**: the console check in `UI-017` covers CSP violations |
| 7 Manual cases | `UI-001` to `UI-019`, with the changes in B. Dry run in **Chrome with the Chrome extension only; no other browser is opened.** The Outcome column stays empty for the author |

### B. Final manual cases (changes from the open-items list: `UI-009`, `UI-011`, `UI-014` reworded, `UI-019` new)

Run as in the open-items section 7: `dotnet run --project src/TechOneNumberToWordsApp.Web`, `https://localhost:5443` in Chrome with DevTools (Console and Network) open.
The author records the outcome by hand in `Docs/TestPlan.md`.

| ID | Decisions | Steps | Expected |
|---|---|---|---|
| `UI-001` | R0, R1, D5, D40 | Type `123.45`, click **Convert** | The box shows ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS. The label reads "Amount (AUD)"; no currency dropdown; the heading is "Amount to Words" |
| `UI-002` | D5 | Type `5`, press Enter without clicking | FIVE DOLLARS |
| `UI-003` | D58 | Convert `1`, then `0.01`, then `.5` | ONE DOLLAR, ONE CENT, FIFTY CENTS |
| `UI-004` | D6, D29 | Paste a 40-character string of digits. Check the field, then click **Convert** | The field keeps all 40 characters (no `maxlength`); the box shows the "amount is too long" message from the server |
| `UI-005` | D24, D19 | Convert `12a` | The server's message about the invalid character `'a' (U+0061)`, in error styling with a visible "Error" label; no words |
| `UI-006` | D19 | Leave the field empty, click **Convert** | "Please enter an amount." |
| `UI-007` | D22 | Convert `$5` | "Please enter the amount without a currency symbol. Amounts are in Australian dollars." |
| `UI-008` | C1, D37 | Paste about 2,000 digits, click **Convert** | The same "too long" message as `UI-004`; the Network tab shows a 413 |
| `UI-009` | D24, D34 | Convert `<b>x</b>` | The message appears as plain text naming `'<' (U+003C)`; no element is created from the input and nothing is bold (the "Error" label is its own element) |
| `UI-010` | E4 | Convert `5😀`, then a full-width `５` | Each message shows the character and its code point correctly |
| `UI-011` | E6 | Network throttling "Slow 3G". Convert `5`; while it is pending press Enter again and try to type in the field | The button is disabled, the field is read-only (focus stays, typing changes nothing), the old result is gone, "Converting…" shows, and only one request appears in the Network tab. When it finishes the button is enabled, the field is editable and the new result shows |
| `UI-012` | E6 | Convert `5`, then `12a`, then `5` again | Each result replaces the last: old words never sit beside an error, and the old error is gone after a success |
| `UI-013` | F3 | In the Console run `window.fetch = () => Promise.resolve(new Response(JSON.stringify({status:500,traceId:"00-abc-def-00"}), {status:500, headers:{"Content-Type":"application/problem+json"}}))`, then Convert `5` | "Something went wrong. Please try again. Reference: 00-abc-def-00"; the button is enabled again. Reload the page afterwards |
| `UI-014` | D24, D60 | DevTools Network "Offline" (or stop the server), Convert `5`; then go back online and Convert again | The fallback, "Something went wrong. Please try again." (no Reference); the button is enabled again and the field editable; the next conversion works without reloading |
| `UI-015` | R1 (accessibility basics) | Keyboard only: Tab through the page; click the label; press Enter in the field. Then Tab to the **Convert** button and start a conversion from it with Enter or Space, and press Tab again. In DevTools > Accessibility, inspect the box | Tab goes input then button; the focus outline is visible; the label click focuses the field; Enter submits; the box is a `status` live region and the hint is the input's description; after a conversion started from the Convert button with the keyboard, the next Tab goes to a sensible place, not back to the top of the page (added 2026-11-24, step 5) |
| `UI-016` | R1 | Zoom to 200%, then narrow the window to 320 px | No horizontal scrolling and nothing clipped; the input and button wrap |
| `UI-017` | D34, C3 | Reload with the Console and Network cleared | No console errors or CSP violation messages; no 404; the icon loads as `image/svg+xml`; `/` shows the four security headers |
| `UI-018` | D35 | Open `http://localhost:5080` | It redirects to `https://localhost:5443` and the page loads |
| `UI-019` | D60 | In the Console run `window.fetch = (u, o) => new Promise((_, reject) => o.signal.addEventListener('abort', () => reject(o.signal.reason)))`, then Convert `5` and wait 15 seconds. Reload the page afterwards | After about 15 seconds the fallback, "Something went wrong. Please try again."; the button is enabled again and the field editable |

`UI-019` only works if `app.js` passes the timeout signal in the options of its `fetch` call (the second argument), so the page must do that.

### C. Order for slice 10 (each code step: red `test:` then green `feat:`, then show; nothing pushed until the author says)

0. `docs:` this section and the decision rows (done in this step).
1. `refactor:` move the security-headers assertion into a shared test helper.
2. `test:` `SEC-005` (theory over four files; traits `Decision` D34, D38 and C3, so all three trace to it in the test plan) and `SEC-014` in `Api/StaticFilesApiTests.cs`. Red: 404s.
3. `feat:` `UseDefaultFiles` and `UseStaticFiles` in the D34 position, and `wwwroot` with `index.html`, `site.css`, `app.js` (15 second timeout, read-only field, the D24 response rule) and `favicon.svg`. Green.
4. Dry run in Chrome with the Chrome extension only; fixes as small commits.
5. `docs:` the `UI-xxx` table in `Docs/TestPlan.md` with the Outcome column empty.
6. Gate 3 housekeeping (below), then the author runs the cases, then self-review, the fresh-context reviewer, `/export` and the plan copy.

### D. Gate 3 housekeeping (author's list, 2026-11-24)
- **Readme:** fix line 10 ("not built yet"), fix line 35 (the 404 note for the launch-profile browser), and draft "Using the page" (D52: docs are drafted at each gate).
- **TestPlan:** update 5.3 (`UI-018` now covers the redirect by hand) and section 6.
- **DesignDecisions:** add the `02-` and `03-` AI files to the document map.
- **TestPlan section 5:** say that `app.js` has no automated tests, and that the manual `UI-xxx` cases and `SEC-014` cover it (the solution has no JavaScript tooling).
- Also the usual: counts, results and coverage refreshed; `SEC-005` and `SEC-014` added to the API section's ID table; the UI section with the manual cases.


## Slice 11 (phase 4, the documents): decisions (agreed 2026-11-25) and order of work

Phase 4 is on `phase/4-docs`, branched from `main` at `c0fd8f4` (PR 3 merged). Nothing is pushed until the author says. The decisions
below are applied to `Docs/DesignDecisions.md` as amendment notes (D20, D36, D46, D52, D54, D56, D57, D59) in step 1. The author
finishes the same evening, because the repository is made public at TechnologyOne's request and the link is sent that night.

### A. Decisions
| Item | Outcome |
|---|---|
| D20 | **No executable.** `dotnet run` stays; the v1.0.0 release (D59) has no attachment. The Readme and the design document each say in one line why an executable does not apply |
| D36 | **No CI.** The Readme's CI section says there is none. The rows with CI notes are D20, D46, D52 and D54. D36's status is "Not adopted" |
| Repository | **Public** at TechnologyOne's request (25 Nov); the submission is the link. Amends D54 (S6), D59 (order: release v1.0.0, make the repository public, email TechnologyOne) and the TestPlan S6 row. Nothing else may say "private" about the repository |
| Final gate | Same as before: self-review, fresh-context reviewer, triage, `/export` to `Docs/AI/05-docs.md` plus a plan copy (`05-docs-plan.md`). The author's claude.ai chat for this gate does not go into `Docs/AI/` |
| Readme | Quick start gains a `git clone` / `cd` step and a Build line (`dotnet build`). **No Hosting section** and no hosting check: one line says a real deployment is published with `dotnet publish -c Release` and needs a real certificate or a reverse proxy. Troubleshooting gains "port 5443 or 5080 in use" and "SDK not found". The CI section says there is none. The documentation and AI section and the limitations section are finished |
| D56 | Amended: the Linux line ("use the http URL") was wrong, because http only redirects (the Readme's Troubleshooting section already says `curl -k`) |
| Design document | `Docs/DesignDocument.md`, about 3 to 4 pages, links to `DesignDecisions.md` instead of copying it (D55), plain-text diagram. Outline: 1 summary; 2 reading the brief (D7 "string output parameter", D40 AUD, D4); 3 architecture; 4 the algorithm walkthrough tracing `123.45`; 5 input rules and errors (D10, D27, E4, D24/D24a, with the process lesson); 6 main alternatives rejected; 7 production quality; 8 testing approach; 9 AI use and constraints (S3, S4); 10 limitations and future work |

### B. Order for tonight (stop after each step so the author can review; `docs:` commits; nothing pushed)
1. Commit step 0 with the author's fixes (D57, D36, D56, D54, D59, TestPlan S6 row, this section).
2. Readme: clone/cd, Build line, Troubleshooting additions, CI says none, the docs and AI section, limitations and future work; the one-line note on why there is no executable and the one line on publishing.
3. `Docs/AI/README.md`: the three AI uses (Claude Opus 5.5 and Claude Sonnet 5.5 in Claude Code; Claude Opus 5.5 on claude.ai as a reviewer), what each was for, a list of the files that exist in `Docs/AI/`, that every decision was the author's, and the S3 evidence (`.claude/settings.json`).
4. `Docs/TestPlan.md`: remove "draft"; fill R2, R6, R7 and S2 in 3.1 and the S6 row; replace section 6; remove CI references; refresh the results from a fresh full `dotnet test`.
5. `Docs/DesignDocument.md` per the outline, about 3 to 4 pages, plain-text diagram, with a line on why no executable.
6. Consistency pass across Readme, TestPlan, DesignDecisions and DesignDocument (counts, IDs, section numbers, document-map states; nothing still says "private" or mentions CI as planned).
7. Final gate: self-review of `phase/4-docs` against the log; fresh-context reviewer (report only), given the brief's "Please include" list and the submission requirements as a checklist and asked to name the file that answers each item; one combined triage table; fixes the author approves; `/export` to `05-docs.md` and the plan copy; PR only when asked. After the merge: release v1.0.0, make the repository public, email TechnologyOne (D59).
