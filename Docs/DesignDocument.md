# Design Document — Number to Words Web Page

This is the short story of the design. The full record is [`DesignDecisions.md`](DesignDecisions.md): every decision, the reason,
and the alternatives that were rejected. Numbers such as D7 or E4 below are rows in that file. The author made every decision;
section 9 says how AI was used.

## 1. Summary

A web page takes an amount in Australian dollars and shows it in words:

```
123.45  ->  ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS
```

The work is done by a small C# class library with no packages. A thin ASP.NET Core minimal API exposes it as `POST /api/convert`.
One plain HTML page, served by the same app, calls the API. All the rules about the amount are on the server, in one place. The
page checks nothing itself (D6).

## 2. Reading the brief

- **"Passes these words as a string output parameter" (D7).** In C# an `out` parameter is one specific feature. I read the
  sentence in its general sense: the routine's output is the words, as a string. The routine returns a small result object. It
  holds either the words or an error with a code and a message. I rejected `TryConvert(..., out words, out error)`. Adding an
  output later would break every caller, `out` does not work in `async` methods, and it is awkward in lambdas.
- **Dollars means Australian dollars (D40).** The brief says "dollars" and never names a currency. I assumed AUD. The page says
  "Amount (AUD)" and has no currency list. A design for many currencies would have cost more code for no user today. D40 says how
  to add a second currency later.
- **"No third-party libraries" (D4).** I applied this to the shipped code, `Core` and `Web`. They have no packages at all. The
  test project uses xUnit and `Microsoft.AspNetCore.Mvc.Testing`. Test tools are not libraries the product depends on.
- **No executable.** The brief says "executable if applicable". It does not apply (D20). The app runs with `dotnet run`. A
  published build would be tied to one operating system and would have no launch profile, so no port for the HTTPS redirect.

## 3. Architecture

```
  Browser: index.html + app.js
        |  POST /api/convert   {"amount":"123.45"}
        v
  Web (ASP.NET Core minimal API)
    security headers -> HTTPS redirect -> exception handler -> static files
    -> status-code pages -> ConvertEndpoint
        |  converter.Convert("123.45")
        v
  Core (class library, no packages)
    AmountToWordsConverter
       1. AmountValidator   check every rule, return the first error
       2. AmountParser      text -> dollars and cents (whole numbers)
       3. MoneyWords        choose DOLLAR/CENT, place the AND
       4. NumberWords       spell a whole number
        |  ConversionResult: words, or an error code and message
        v
  Web turns it into  200 {"words": "..."}  or  400 problem+json {errorCode, detail}
```

There are three projects. `Core` knows nothing about HTTP. `Web` knows nothing about how words are made. `Tests` calls the public
class and the real HTTP pipeline (D2, D50). `ConvertEndpoint` only checks that a body and an amount exist, then maps the result
(D23).

## 4. The algorithm, traced on 123.45

1. **The page** sends `{"amount":"123.45"}`. It sends the text as typed. It does not trim or check it (D6).
2. **The validator** runs twelve checks in a fixed order (D27): too long, empty, a currency symbol, a minus, a plus, a space
   inside, any other bad character, more than one point, no digits, misplaced commas, more than two decimals, too many digits.
   `123.45` is 6 characters and passes them all.
3. **The parser** reads one character at a time. There is no `decimal.Parse` and no floating point (D11). Each digit before the
   point shifts the total one place left and adds itself: `1`, then `12`, then `123`. The `.` switches to cents. The first digit
   after it is tens of cents, so `4` adds 40. The second is single cents, so `5` adds 5. The result is 123 dollars, 45 cents.
4. **`MoneyWords`** sees that cents are not zero, so it spells both parts. Cents: `FORTY-FIVE` plus `CENTS`, because only exactly
   one uses the singular (D58). Dollars: `123` plus `DOLLARS`. It joins them with `AND` (D8).
5. **`NumberWords`** spells 123. It is under 1,000, so it makes one group. The hundreds digit gives `ONE HUNDRED`. The rest, 23, is
   not zero, so `AND` goes between them (D8). 23 is `TWENTY-THREE`, with a hyphen (O1).
6. **The answer** is `ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS`. The API returns it as `{"words": "..."}`. The
   page shows it with `textContent`, as plain text (D24).

For larger numbers the same spelling is done in groups of three digits, from the left. `AND` goes before the last group only when
it is 1 to 99 and a larger group came first. So 1,005 is `ONE THOUSAND AND FIVE`, and 1,100 is `ONE THOUSAND ONE HUNDRED`.

## 5. Input rules and errors

- **Only ASCII digits (D10).** The check is `'0'` to `'9'`, never `char.IsDigit`, which also accepts full-width and Arabic-Indic
  digits. Commas must sit in the right places (E3). Spaces and tabs are allowed only at the ends. Negative amounts, a plus sign
  and currency symbols are refused with their own messages (D12, D22).
- **One error, the first match (D27).** Specific problems come before general ones: characters, then structure, then precision,
  then size. The order is tested for every pair of rules that could be confused.
- **Messages name the character by its code point (E4).** For `12a` the message is `Invalid character 'a' (U+0061)`. A full-width
  `５` looks like `5` but is U+FF15. A non-breaking space is invisible, so only its code point can be shown. The cost is a message
  that is a little technical. I accepted it because a user can then see what is wrong.
- **One error format (D24, D24a).** Every error, ours or the framework's, is `application/problem+json` with an `errorCode`.
  There are two reasons. A program calling the API handles one shape. And a support team can search tickets and logs for a code
  such as `InvalidCharacter`, which does not change when the wording does.
- **The code names are a public contract (D24d).** They are written as text, never as numbers, and renaming one would break
  callers. The table is in one place: [Readme, API reference](../Readme.md#3-api-reference).
- **A lesson about rules (D41, D37, D24).** "Every error has this shape" was agreed for the errors we write. It was not checked
  against the errors the framework writes. They arrived with empty bodies: 404, 405, 413, and a 400 for bad JSON. Under
  `dotnet run` bad JSON even became a 500 with a stack trace. Each needed its own fix. The lesson is to ask, when a rule is agreed,
  what else it touches. It is cheaper to find that out then.

## 6. Main alternatives rejected

| Choice | Rejected | Why (decision) |
|---|---|---|
| Result of the routine | `out` parameters | Breaks callers when it grows; no `async` (D7) |
| Reading the amount | `decimal.Parse` | Depends on the machine's culture; our own parser is short and exact (D11) |
| Currency | Many currencies, a registry, a dropdown | Not asked for; more code and more tests for no user (D40) |
| A leading `$` | Accept it | Brings many edge cases (`$ 5`, `-$5`, `5$`); the label already says AUD (D22) |
| Error shape | A custom error record | There would be two shapes once the framework answers (D24) |
| Body size limit | Our own middleware | Kestrel's limit also covers chunked bodies; we map the 413 (D37) |
| Test data | `[InlineData]` or CSV | One JSON file feeds both layers; CSV breaks on commas and Excel changes data (D43) |
| Page | A framework or a build step | One style sheet and one script are enough for one form (D5) |

## 7. Production quality

- Warnings are errors, nullable is on, and the project builds clean (D39).
- Security headers on every response, including the page's files and error responses; a strict Content-Security-Policy with no
  inline script or style; no `Server` header (D34).
- A 1 KB limit on every request body (D37). HTTPS redirect always, HSTS outside Development (D35).
- A user never sees a stack trace. A 500 gives a fixed message and a reference. The log has the exception under the same id
  (D24, F3, C2).
- Logs hold only the error code, never what the user typed (D33).
- The converter holds no state, so one instance serves every request (D23). Nothing depends on the machine's culture.
- The page puts server text in with `textContent` only. A request gives up after 15 seconds (D24, D60).

## 8. Testing approach

Tests came first. For each step there is a red `test:` commit and then a green `feat:` commit (D52, D53). Two exceptions are
recorded in [`AI/README.md`](AI/README.md).

- **One data file, two layers.** `conversion-cases.json` has 162 rows. Each runs through the library and through real HTTP
  (D43, D44). So the two layers cannot drift apart.
- **IDs trace to decisions.** Every test has an ID and the decisions it checks (D47). The test plan maps the brief to decisions to
  tests.
- **364 automated tests pass.** Coverage is 99.2% of lines in `Core` and 97.4% in `Web`. The few uncovered lines are guards, and
  the test plan explains each one.
- **The page has 19 manual cases**, all passed in Chrome. `app.js` has no automated tests, because the solution has no
  JavaScript tooling.

See [`TestPlan.md`](TestPlan.md) for the numbers, the traceability and what is not tested.

## 9. AI use and constraints

- **AI use.** Claude Opus 5.5 planned and set out options, and Claude Sonnet 5.5 wrote the tests and the code, in Claude Code. A
  separate Claude Opus 5.5 chat on claude.ai reviewed the replies. Each phase ended in a review: mine, a fresh-context reviewer
  agent, then a triage that I decided. Every design decision was mine. [`AI/README.md`](AI/README.md) has the detail and the
  conversation history.
- **No internet (S3).** The committed [`.claude/settings.json`](../.claude/settings.json) denies `WebSearch` and `WebFetch` to
  Claude Code, and has since the first commit.
- **No packages in the product (S4).** `Core` and `Web` have no `PackageReference`. Check with
  `grep -c PackageReference src/*/*.csproj`; it prints 0 for both. The parser, validator and wording are hand-written.

## 10. Limitations and future work

It handles Australian dollars only, from 0 to 999,999,999,999,999.99, in English capitals. The page needs a current browser and was
checked in Chrome only. There is no login, no rate limiting, no CI and no executable. The Readme lists the limits
([section 10](../Readme.md#10-limitations-and-future-work)). Next steps would be more currencies (D40), rate limiting, a health
check, versioned routes and translated messages.
