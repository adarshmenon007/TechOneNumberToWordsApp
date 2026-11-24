# Number to Words

A C# web page and server routine that turn an Australian-dollar amount into words:

```
123.45  →  ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS
```

The routine is a class library with no third-party packages. A small ASP.NET Core Minimal API exposes it as
`POST /api/convert`, and a single HTML page (not built yet) calls that API.

> **Status: draft.** This Readme describes what exists after the API phase. Sections marked *To be written in slice 11* are
> filled in when the page and the rest of the project are finished. The reasoning behind every design choice is in
> [`Docs/DesignDecisions.md`](Docs/DesignDecisions.md) (decision numbers such as D24 below refer to it).

## 1. Quick start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`global.json` accepts any 10.0.x SDK).

**Trust the development certificate first**, once per machine, so the browser and `curl` accept `https://localhost`:

```
dotnet dev-certs https --trust
```

Then, from the repository root:

```
dotnet run --project src/TechOneNumberToWordsApp.Web
```

The app listens on **https://localhost:5443**. Plain http on `http://localhost:5080` redirects to https (D35). It runs in the
Development environment (set by `launchSettings.json`).

The launch profile opens a browser tab, which shows a 404 until the page is built. For now try the API:

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

*To be written in slice 11.*

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
the exception is written to the server log. Use the `traceId` to find it (section 8).

#### Headers on every response (D34)

`Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and `X-Frame-Options: DENY`. There is
no `Server` header.

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
    Properties/launchSettings.json ports and environment for dotnet run
    appsettings.json               logging
tests/
  TechOneNumberToWordsApp.Tests/
    Unit/                          library tests
    Api/                           HTTP tests (in-memory server, and real Kestrel for the body limit)
    TestData/conversion-cases.json one data file, run through the library and through HTTP
    test.runsettings, xunit.runner.json
Docs/                          decision log, test plan, AI conversation history
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

## 7. Logging

A rejected amount writes one Information line from the endpoint with only its `errorCode`; what the user typed is never logged
(D33). A successful conversion writes nothing of its own. An unexpected exception is logged once, as an Error with the exception.

## 8. Troubleshooting

- **The browser or `curl` complains about the certificate.** Run `dotnet dev-certs https --trust`. On Linux that command does not
  trust the certificate system-wide; use `http://localhost:5080` (it redirects to https) or tell your client to skip
  verification (`curl -k`).
- **"Failed to determine the https port for redirect" in the log.** The app was started without the launch profile (for example
  with `ASPNETCORE_URLS` set to an http address), so it does not know where to redirect. Plain http still works. Run with
  `dotnet run --project src/TechOneNumberToWordsApp.Web` to use both ports.
- **Finding the log entry for an error you were shown.** The `traceId` in a response is the full W3C id,
  `00-<TraceId>-<SpanId>-<flags>`. The log shows only the 32-character `TraceId`, which is the middle part: search the log for
  that.

## 9. Continuous integration

*To be written in slice 11.*

## 10. Documentation and AI usage

| Document | What it is |
|---|---|
| [`Docs/DesignDecisions.md`](Docs/DesignDecisions.md) | Every design decision, with the reason and the alternatives that were rejected |
| [`Docs/TestPlan.md`](Docs/TestPlan.md) | The test plan, traceability and results |
| [`Docs/AI/`](Docs/AI) | The AI conversation history, one export per phase, with the plan files |

*The design document, and the account of which AI models were used and how, are to be written in slice 11.*

## 11. Limitations and future work

*To be written in slice 11.*
