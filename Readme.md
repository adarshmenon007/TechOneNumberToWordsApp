# Number to Words

A C# web page and server routine that turn an Australian-dollar amount into words:

```
123.45  →  ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS
```

The routine is a class library with no third-party packages. A small ASP.NET Core Minimal API exposes it as
`POST /api/convert`, and a single plain HTML page, served by the same app, calls that API.

The reasoning behind every design choice is in [`Docs/DesignDecisions.md`](Docs/DesignDecisions.md); decision numbers such as D24
below refer to it. The narrative is in [`Docs/DesignDocument.md`](Docs/DesignDocument.md).

## 1. Quick start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`global.json` accepts any 10.0.x SDK) and git.

Get the code:

```
git clone https://github.com/adarshmenon007/TechOneNumberToWordsApp.git
cd TechOneNumberToWordsApp
```

**Trust the development certificate**, once per machine, so the browser and `curl` accept `https://localhost`:

```
dotnet dev-certs https --trust
```

Build (warnings are errors, D39), then run, both from the repository root:

```
dotnet build
dotnet run --project src/TechOneNumberToWordsApp.Web
```

The app listens on **https://localhost:5443**. Plain http on `http://localhost:5080` redirects to https (D35). It runs in the
Development environment (set by `launchSettings.json`).

With `dotnet run` no browser opens: open https://localhost:5443 yourself (section 2). Visual Studio does open it. You can also call
the API directly:

```
curl -X POST https://localhost:5443/api/convert -H "Content-Type: application/json" -d '{"amount":"123.45"}'
```

```json
{"words":"ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS"}
```

In PowerShell:

```powershell
Invoke-RestMethod -Method Post -Uri https://localhost:5443/api/convert -ContentType application/json -Body '{"amount":"123.45"}'
```

## 2. Using the page

Open **https://localhost:5443**. With `dotnet run` you open it yourself; only Visual Studio opens the browser for you.

1. Type an amount in the **Amount (AUD)** box, for example `1,234.50`. Commas, a decimal point and spaces around the amount are
   fine; the rules are the ones in section 3.
2. Click **Convert**, or press Enter.
3. The answer appears in the box under the form, for example `123.45` gives
   ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS.

If the amount is not valid, the box shows a message that says what is wrong, marked **Error:**. The page does not check the
amount itself; the server does, so the same rules and wording apply to the page and to the API (D6). Nothing you type is ever
shown as markup, only as text (D24).

While the request runs the Convert button is disabled, the amount box cannot be edited and the answer area says "Converting…" (E6). If the server does not
answer within 15 seconds, or cannot be reached, the page says "Something went wrong. Please try again." (D60). After an
unexpected server error the same sentence is followed by a **Reference**; to find that error in the server log, see section 7 (F3).

It can be used with the keyboard alone (`UI-015`). The answer area is a live region for screen readers; no screen reader was
tested. It is plain HTML, one style sheet and
one script, with no framework; the files are in `src/TechOneNumberToWordsApp.Web/wwwroot/`. The checks for it are the manual cases
in [`Docs/TestPlan.md`](Docs/TestPlan.md) (section 1.5).

## 3. API reference

### `POST /api/convert`

Request: `Content-Type: application/json`, body at most 1,024 bytes (D37).

```json
{ "amount": "1,234.50" }
```

`amount` must be a JSON **string**, so commas and exact digits survive. A JSON number is rejected (E5). Other fields are ignored.

An amount is Australian dollars (D40). It may have:

- digits `0`–`9` only (ASCII), with optional commas as thousands separators (`1,234,567`; the first group has 1–3 digits);
- an optional decimal point with at most 2 digits after it (`.5` is fifty cents, `5.` is five dollars);
- spaces or tabs before or after it (they are trimmed), and leading zeros;
- at most 30 characters in all, and a value of at most 999,999,999,999,999.99.

Negative amounts, a plus sign and currency symbols are rejected.

**Success: 200**, `application/json`:

```json
{ "words": "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS" }
```

**Every error is a 4xx or 5xx `application/problem+json` response** (RFC 9457) with the same fields: `type`, `title`, `status`,
`errorCode` and `traceId`. The errors about the amount also have a `detail`, which is a message for the person typing:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid amount",
  "status": 400,
  "detail": "Invalid character 'a' (U+0061). Use only digits, a decimal point and commas.",
  "errorCode": "InvalidCharacter",
  "traceId": "00-09e1ebf59cac0c70e99614fa3b0d6146-e6db6f79473c8eef-00"
}
```

**The `errorCode` names are part of the API contract** (D24d); they are written as text, never as numbers. When a request breaks
more than one rule, the first rule in the order below wins (D27).

#### Errors about the amount: 400, title "Invalid amount"

| `errorCode` | When |
|---|---|
| `AmountTooLong` | The raw input is more than 30 characters |
| `AmountRequired` | The amount is empty, only spaces or tabs, or has no digit (`.`, `,`) |
| `CurrencySymbolNotAllowed` | It contains a currency symbol such as `$` |
| `NegativeAmount` | It starts with `-` |
| `SignNotAllowed` | It starts with `+` |
| `ContainsWhitespace` | A space or tab is inside the amount |
| `InvalidCharacter` | Any other character that is not a digit, `.` or `,`; the message names it by code point |
| `MultipleDecimalPoints` | More than one `.` |
| `InvalidThousandsSeparator` | A comma in the wrong place, or after the decimal point |
| `TooManyDecimalPlaces` | More than 2 digits after the decimal point |
| `AmountTooLarge` | More than 15 digits before the decimal point (leading zeros do not count) |

#### Errors about the request: title "Invalid request" (no `detail`, except the 413)

| `errorCode` | Status | When |
|---|---|---|
| `InvalidRequest` | 400 | Empty body, malformed JSON, or `amount` is not a string |
| `MissingField` | 400 | `amount` is absent or `null` |
| `UnsupportedMediaType` | 415 | `Content-Type` is not JSON |
| `NotFound` | 404 | No such route |
| `MethodNotAllowed` | 405 | The route exists but not for this method (for example `GET /api/convert`) |
| `PayloadTooLarge` | 413 | The body is more than 1,024 bytes. Its `detail` is the same text as `AmountTooLong` (C1) |

#### Unexpected errors: 500, title "Server error"

`errorCode` `ServerError` and the fixed `detail` "An unexpected error occurred." Nothing from the exception is sent to the client;
the exception is written to the server log. Use the `traceId` to find it (section 7).

#### Headers on every response (D34)

`Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and `X-Frame-Options: DENY`. There is
no `Server` header. The page's files get the same headers.

## 4. Running the tests

```
dotnet test
```

No flags are needed. `Directory.Build.props` points at `tests/TechOneNumberToWordsApp.Tests/test.runsettings`, so the run also
writes a `.trx` result file and a Cobertura coverage report to `tests/TechOneNumberToWordsApp.Tests/TestResults/`.

Every test has an ID and a decision it traces to, so you can run part of the suite:

```
dotnet test --filter "Id=CENT-001"       # the brief's example (the library test and its HTTP twin)
dotnet test --filter "Decision=D8"       # everything about where "AND" goes
dotnet test --filter "Id~SEC-"           # the security tests
```

The test plan, with the results and the traceability from the brief to the tests, is [`Docs/TestPlan.md`](Docs/TestPlan.md).
The test project is the only place that uses packages (xUnit v3, `Microsoft.AspNetCore.Mvc.Testing`, the test SDK).

## 5. Project structure

```
TechOneNumberToWordsApp.sln
Directory.Build.props         build settings for every project: net10.0, nullable, warnings as errors
global.json                   SDK version
src/
  TechOneNumberToWordsApp.Core/    the library: AmountToWordsConverter and its parser, validator and wording
  TechOneNumberToWordsApp.Web/     ASP.NET Core Minimal API
    Program.cs                     composition root: services and request pipeline
    Endpoints/                     POST /api/convert
    Models/                        request and response records
    Errors/                        errorCode values and the ProblemDetails mapping
    Middleware/                    security headers
    wwwroot/                       the page: index.html, site.css, app.js, favicon.svg
    Properties/launchSettings.json ports and environment for dotnet run
    appsettings.json               logging
tests/
  TechOneNumberToWordsApp.Tests/
    Unit/                          library tests
    Api/                           HTTP tests (in-memory server, and real Kestrel for the body limit)
    TestData/conversion-cases.json one data file, run through the library and through HTTP
    test.runsettings, xunit.runner.json
Docs/                          design document, decision log, test plan, AI conversation history
```

`Core` has no dependency on `Web`, and neither has any NuGet package (D4).

## 6. Configuration

| Where | What |
|---|---|
| `src/…/Web/Properties/launchSettings.json` | `https://localhost:5443` and `http://localhost:5080`, environment Development. Used by `dotnet run` and Visual Studio only |
| `src/…/Web/appsettings.json` | Log levels (Information, with `Microsoft.AspNetCore` at Warning) and `IncludeScopes` for the console logger, so each log line shows the request's `TraceId` (C2) |
| `Directory.Build.props` | Target framework, nullable, warnings as errors (D39), and the test run settings (D45) |
| `Program.cs` | The 1 KB request body limit and removal of the `Server` header (D34, D37) |

Nothing about the conversion is configurable: the limits (30 characters, 15 digits, 2 decimal places) are part of the rules.
Outside Development the app also sends an HSTS header (D35).

**There is no executable or installer** (D20): the app is built and run with the .NET SDK, as above. A real deployment would be
published with `dotnet publish -c Release` and needs a real certificate or a reverse proxy; that is not covered here.

**Logging.** A rejected amount writes one Information line from the endpoint with only its `errorCode`; what the user typed is
never logged (D33). A successful conversion writes nothing of its own. An unexpected exception is logged once, as an Error with
the exception.

## 7. Troubleshooting

- **`dotnet` says it cannot find an SDK, or the .NET 10 SDK.** Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
  `dotnet --list-sdks` shows what is installed; `global.json` needs 10.0.100 or a later 10.0 SDK.
- **"Failed to bind to address … address already in use" (port 5443 or 5080).** Another program, or another copy of this app,
  already uses the port. Stop it, or run on other ports (the plain-http port is the one that redirects):
  `dotnet run --project src/TechOneNumberToWordsApp.Web --urls "https://localhost:5444;http://localhost:5081"`, then open
  https://localhost:5444.
- **The browser or `curl` complains about the certificate.** Run `dotnet dev-certs https --trust`. On Linux that command does not
  trust the certificate system-wide, and `http://localhost:5080` only redirects to the https address, so it does not help. Tell
  your client to skip certificate verification (`curl -k`, or the equivalent in your HTTP tool).
- **"Failed to determine the https port for redirect" in the log.** The app was started without the launch profile (for example
  with `ASPNETCORE_URLS` set to an http address), so it does not know where to redirect. Plain http still works. Run with
  `dotnet run --project src/TechOneNumberToWordsApp.Web` to use both ports.
- **The browser console shows errors about `aspnetcore-browser-refresh.js`.** The browser has cached a copy of the page from a
  Visual Studio run (Visual Studio adds that script). Press Ctrl+Shift+R to reload without the cache.
- **Finding the log entry for an error you were shown.** The `traceId` in a response is the full W3C id,
  `00-<TraceId>-<SpanId>-<flags>`. The log shows only the 32-character `TraceId`, which is the middle part: search the log for
  that.

## 8. Continuous integration

There is none (D36). The tests are run with `dotnet test` (section 4); the test plan records the date, the commit and the result
of the run it reports.

## 9. Documentation and AI usage

| Document | What it is |
|---|---|
| [`Docs/DesignDocument.md`](Docs/DesignDocument.md) | The design rationale: how the brief was read, the architecture, the algorithm traced on `123.45`, the alternatives rejected |
| [`Docs/DesignDecisions.md`](Docs/DesignDecisions.md) | Every design decision, with the reason and the alternatives that were rejected |
| [`Docs/TestPlan.md`](Docs/TestPlan.md) | The test plan, traceability from the brief to the tests, results and coverage, and the manual page cases |
| [`Docs/AI/README.md`](Docs/AI/README.md) | Which AI models were used and for what, and an index of the conversation history |
| [`Docs/AI/`](Docs/AI) | The conversation history: one Claude Code export and plan file per phase, and the claude.ai review chats |

**AI usage.** Claude Opus 5.5 and Claude Sonnet 5.5 were used in Claude Code to challenge the design, write the tests and the code,
and review it; Claude Opus 5.5 on claude.ai was used as a reviewer, to explain options and trade-offs. Every design decision was
made by the author. The details are in [`Docs/AI/README.md`](Docs/AI/README.md).

## 10. Limitations and future work

**Limitations**

- Australian dollars only, with cents always two digits (D40).
- Amounts from 0 to 999,999,999,999,999.99; no negative amounts. Output is English, in capitals.
- The page needs a current browser (2022 or later). It was checked in Chrome only, and no screen reader was tested.
- It is a sample application: no authentication, no rate limiting, development certificate only, and no executable or CI (D20, D36).

**Future work**

- More currencies: extract an interface and move the unit words into currency data (D40).
- Rate limiting, a health-check endpoint, API versioning (`/api/v1`), and localised messages.
