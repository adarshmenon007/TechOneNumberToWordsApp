# claude.ai review chat — build phase 2 (API), gate 2

**Model:** Claude Opus 5.5 on claude.ai, used as a reviewer alongside Claude Code (which did the building).
**Date:** 24 November 2026.
**Role of this chat:** review each reply from Claude Code against `Docs/DesignDecisions.md`, the plan file and the actual repo files (shared read-only), explain technical points with small examples and diagrams, set out options with trade-offs and a recommendation, and draft short replies for the author to send to Claude Code. Every decision was the author's.

**How to read this file**
- "Author" is the developer; "Reviewer" is Claude on claude.ai.
- Replies pasted from Claude Code are not repeated here; they are in the Claude Code export (`Docs/AI/03-api.md`). Each is marked *[Claude Code reply pasted]* with a one-line summary.
- Drafts the author sent to Claude Code are also in that export; here they are summarised.
- The reviewer's file checks (reading repo files, comparing the decision log with the plan file by script, reading a commit's files from the git objects) are summarised as "What I checked", not reproduced.
- Inline diagrams drawn in the chat are described in words.
- Personal details (names, local paths, other appointments and projects) have been removed. Reviewer replies are condensed; decisions, options, trade-offs and recommendations are kept as given.

---

## Starting phase 2

### Author
Phase 1 is merged (`1617a64`). Starting phase 2, the API: slice 9 on `phase/2-api`, then gate 2. Read the status doc, the tech-test notes, `DesignDecisions.md` in full, the library section of `TestPlan.md`, and skim the gate 1 review. Confirm the decision log and the plan file agree. Same way of working as gate 1 (Claude Code builds, the reviewer reviews and explains, every decision is the author's; check files, not summaries; ask before drafting; never write to the repo; ask before changing project docs; lean, nothing speculative; watch the schedule and cut order; the gate 2 merge checklist). Slice 9 open items: the 413's shared message (C1), the 413 approach (preference: built-in middleware plus the mapping), the Development-mode 500 risk (D37), and the logging test (preference: automated).

### Reviewer
**What I checked:** the repo (on `phase/2-api` at `1617a64`, same as `main`; `Web` holds only the empty composition root), `DesignDecisions.md` in full, the test plan, the Core files relevant to C1. Not checked: the plan file (not pasted yet).

- **Development mode:** `RouteHandlerOptions.ThrowOnBadRequest` defaults to true in Development, so binding failures throw instead of returning a status. Our tests run in `Testing`, so they would not notice; `dotnet run` uses Development. Recommended setting it to false.
- **C1 options:** (A) a public read-only message property on the converter, used by the validator and the web layer — recommended; (B) copy the text plus a drift test; (C) `InternalsVisibleTo` — against D25.
- **413:** agreed with the author's preference (Kestrel limit plus the D41 mapping).
- **Logging test:** agreed; a small `ILoggerProvider` in the test project, no package.
- **Log gaps:** D41 lists only 404/405 although C1/D37 rely on it for the 413; D37's status still says "verify at setup".

### Author
Pasted the plan file.

### Reviewer
**What I checked:** compared all 80 decision rows, F-items and C-items between the plan file and `DesignDecisions.md` by script: identical. Differences are only the design-phase sections deliberately left out of the log. One stale note (real-Kestrel `UseKestrel()` "unverified", already confirmed in slice 0). Gap: none of the slice 9 open items are in the plan file, so Claude Code wouldn't know about them.

### Author
*[Claude Code reply pasted]* `phase/2-api` created from `1617a64`; it recommends a new Claude Code chat for phase 2 rather than `/compact`. Asked for a draft.

### Reviewer
Agreed with a new chat. Its list of open points missed C1, logging and the Development issue; "E5 first" was a suggestion, not a decision; a new session might create a new plan file, so tell it to keep the existing one. Draft: read the docs, keep the plan file, add a "Slice 9 open items" section with options and recommendations, then stop.

---

## Slice 9 open items

### Author
*[Claude Code reply pasted]* The open-items section: C1 option A; 413 via the Kestrel limit and an amended D41; a scratch app confirms the Development issue for malformed JSON and E5 only (415 and 413 unaffected), fixed by `ThrowOnBadRequest = false`; logging via an in-test provider; success-path logging undecided.

### Reviewer
- Accepted the narrower Development scope.
- **D41 must also list 400** (malformed JSON, E5) and must not overwrite the endpoint's own amount errors.
- Development tests must check the ProblemDetails body, not only the status; keep the Development runs to the binding-failure cases; the factory taking an environment amends D45; the manual `dotnet run` check repeats the automated test.
- The 500 log test must compare the middle part of the W3C trace id with the log scope (C2).
- Recommended that successful conversions log nothing (D33 amendment).

### Author
Agreed with all four items. Asked where the web server and middleware sit in the pipeline (diagrams preferred), and whether Testing and Development both use `appsettings.Development.json`.

### Reviewer
- **Diagram:** browser → Kestrel (HTTP, body limit) → middleware in `Program.cs` order (exception handler, HSTS/redirect, status code pages, security headers, static files, routing) → `ConvertEndpoint` → `AmountToWordsConverter`; the response travels back up; `TestServer` replaces Kestrel in tests, which is why the 413 needs real Kestrel.
- Middleware runs code on the way in and on the way out (`await next(context)`), so order matters.
- A table of which component creates each error and which writes its body.
- **Configuration:** neither environment uses `appsettings.Development.json`, because it doesn't exist; `appsettings.{Environment}.json` is optional. What changes per environment is code: the `ThrowOnBadRequest` default, the developer exception page, HSTS.
- Five questions: drop the manual check?; D41 with 400 and a guard?; security headers on error responses?; trace-id middle part?; amendment wording before tests?

### Author
1 drop it; 2 review D41 again (was "don't overwrite" decided?); 3 review D34 first; 4 yes (pasted C2); 5 yes.

### Reviewer
- **D41:** "don't overwrite" was not decided. "Empty-body" in D41 protects against status code pages, but `CustomizeProblemDetails` runs for every ProblemDetails, including the endpoint's own `TypedResults.Problem` (diagram: both paths converge on the customizer). Mapping by status alone would turn an amount error into `InvalidRequest` / "Invalid request". Proposed amendment: status code pages map 400/404/405/413/415; the customizer fills in `errorCode` and title only when missing.
- **D34:** the exception handler clears response headers when it writes a 500, so headers must be added with `Response.OnStarting`, in middleware before the exception handler and before static files. Test 200, 400, 404 and 500 now and `index.html` in slice 10; check the missing `Server` header on real Kestrel only. Proposed amendment.
- **C2:** the trace id is in the log scope, not the message, so the test provider must capture scopes.

### Author
Approved both amendments as written; asked for a draft.

### Reviewer
Summary of all slice 9 decisions, then the draft: decisions 1–4, the D41/D24e and D34 amendments, and exact wording for every amendment before any test.

---

## Amendment wording, steps 0 and 1

### Author
*[Claude Code reply pasted]* Wording for 11 amendments, the new test IDs, the step order and three points of its own.

### Reviewer
- Gaps: the 500 mapping was missing from D41; D37 was marked "verified" before its tests existed (and the status should use the legend's wording); D44 needs an amendment if `messageContains` is checked over HTTP.
- Redundant tests: API-001/002 repeat the HTTP theory (move the content-type check into it); SEC-009 is proven by LIM-012 plus SEC-006.
- Its points: agreed no detail on framework errors (as a D24 amendment), `messageContains` over HTTP, and the 1,024-byte assumption.

### Author
Asked for the draft; sent.

### Author
*[Claude Code reply pasted]* Steps 0 and 1 done: docs commit, LIM-012 red with a throwing stub, then green.

### Reviewer
**What I checked:** the three commits; all 12 changed decision entries by script (only appended notes); the Core change and LIM-012. LIM-012 passes by construction now, which is its purpose (it guards against the wording being typed out again). For the HTTP theory: compare only the media type, because ASP.NET adds `charset=utf-8`.

---

## Steps 2 and 3

### Author
*[Claude Code reply pasted]* Step 2 red: the factory, the in-memory log provider and the HTTP theory over 162 rows, all failing with 404.

### Reviewer
**What I checked:** the three test files. Explained `ISupportExternalScope` (scopes live in one shared store handed to providers, so `BeginScope` returning null is correct). For later steps: a class fixture shares one log list across tests; Development factories created by tests must be disposed; C2's console `IncludeScopes` isn't covered by automated tests and the test plan should say so.

### Author
Make sure every `IDisposable` is disposed properly. Asked for the draft; sent.

### Author
*[Claude Code reply pasted]* Step 3 green: endpoint, models, composition root; 327 tests pass; a temporary null guard until step 4.

### Reviewer
The handler takes a non-nullable `ConvertRequest`, but D24e (and its slice 9 note) says `ConvertRequest?` with an explicit null check. Once the status-code mapping exists, API-003 would pass either way, so the difference would go unnoticed. Options: (A) follow D24e — recommended, easier to explain line by line; (B) let the framework reject it and amend D24e.

### Author
Before deciding, review why we drifted from a minimal API.

### Reviewer
- The style hasn't changed: endpoints are mapped functions, `Program.cs` is the composition root, the web project will be roughly 130–150 lines.
- The growth comes from one root decision, D24a ("every error carries an errorCode", including framework errors), and its chain of consequences (diagram): D41 status code pages → customizer with a guard → `ThrowOnBadRequest` → Development test runs → factory takes an environment; and separately C1 → a public message in Core.
- Inventory table of every slice 9 piece, its source decision, whether the page can trigger it and its rough size. Options: keep the plan (recommended); built-in ProblemDetails without codes for framework errors; or also drop the Development runs.

### Author
"Every error carries an error code" was his decision: the aim was useful error codes in the logs for a support team, originally rooted in the multi-currency plan. Keep the plan.

### Reviewer
That leaves D24e as planned (option A for the empty body). Gap between the intent and the plan: only amount rejections and 500s are logged; framework errors, `MissingField` and the 413 reach the response but not the log. Options: (A) leave D33 as decided; (B) log every 4xx code in one place. The multi-currency reason was superseded by D40, so the design doc should give the other reason: one error format for API callers and searchable codes for support.

### Author
Chose B, but only if it is a few lines with no knock-on effects.

### Reviewer
About six lines in the customizer, but with knock-on effects: the endpoint's own log line must go (or amount errors are logged twice), 500s are excluded, D33's note is reworded, one more test, and it relies on the endpoint's problems passing through the customizer; production 404 noise from bots.

### Author
Chose A: no extra logging; stay with the MVP as decided.

---

## Side questions

### Author
What would the pipeline look like if every conversion were saved to a database?

### Reviewer
Diagram: the middleware is unchanged; the endpoint becomes async (D21: async only for real I/O), calls the synchronous converter, then a scoped store, then the database. The store goes into the endpoint, not the converter, because a singleton must never hold a scoped service (captive dependency). Decisions it would touch: the brief's no-NuGet rule (database drivers are packages), privacy of the raw input, saving rejections, behaviour when the database is down, and faking the store in tests.

### Author
Without a database, how would the service behave if the converter were scoped instead of a singleton?

### Reviewer
Identical behaviour (diagram: one shared instance versus one per request), because the converter holds no state; scoped only allocates one small object per request. Lifetime matters when a class holds state or depends on other services. The captive-dependency rule, and that ASP.NET Core checks lifetimes at startup in Development. Singleton remains right (D23); the 500 test's replacement works with any lifetime.

---

## Steps 4 to 8

### Author
*[Claude Code reply pasted]* Step 4: API-003 to API-011 red under both environments, then the endpoint's null checks, the D41 mapping with its guard, and `ThrowOnBadRequest = false`. Development failed as 400 with a stack trace, not 500.

### Reviewer
**What I checked:** the four commits, the endpoint, the three `Errors/` files, the test helpers. The order of null checks follows D27; the guard is proven by the 162 rows staying green. Decisions: approve rewording the D24 note (the developer exception page answered, not a 500); and no detail on the endpoint's own `InvalidRequest`/`MissingField`, recorded by title ("errors titled 'Invalid request' carry no detail; the 413 is the exception"). Both recommended.

### Author
Approved both; aim to finish everything today.

### Author
*[Claude Code reply pasted]* Steps 5 and 6: the generic 500 and the security headers, red then green.

### Reviewer
**What I checked:** the commits, `Program.cs`, the middleware, the customizer, both test files. Correct; the exception handler is skipped in Development (the developer exception page is for Development only). **Gap:** D35 (HSTS and HTTPS redirect) was not in `Program.cs` or in the slice 9 order; add it before step 7 so the real-Kestrel tests run against the final pipeline.

### Author
*[Claude Code reply pasted]* D35 added as step 6b; step 7 red then green (body limit, no `Server` header, the 413 mapping); D37 marked verified.

### Reviewer
**What I checked:** the commits, `Program.cs`, the 413 branch, the real-Kestrel tests, D37's row. Correct. One fix: the "Verified" note cut off part of the earlier note; restore it and append.

### Author
*[Claude Code reply pasted]* D37 restored; step 8 (logging) red then green.

### Reviewer
**What I checked:** the commits, D37's row, the endpoint's log line (structured, code only), the logging tests (own factory each, marker test asserts something was logged first, trace-id middle part compared). Slice 9 complete; listed the gate 2 steps.

---

## Gate 2

### Author
*[Claude Code reply pasted]* Gate 2 documents (test plan API section, Readme first draft, D52 note, `Program.cs` comment) and its self-review: one Medium (the "redirect doesn't get in the way" claim is overclaimed; with both ports a plain-http request gets a 307), several Lows.

### Reviewer
**What I checked:** the docs commits, the whole Readme, the new test plan sections, the D52 note. Two misses: the test plan's section 5.5 reintroduced the manual check the author dropped; and exports belong last, just before the merge. Triage proposal for the self-review findings.

### Author
Run the fresh-context reviewer subagent first, as at gate 1.

### Author
*[Claude Code reply pasted]* The fresh reviewer's report: no High or Medium; Lows on the untested D35, section 5.5, the disposal order in helpers, contradictory Linux advice in the Readme; nits.

### Reviewer
Checked the one claim that looked wrong by reading the red commit `3db9bfe` from the git objects: it contains a throwing stub, so it was red at run time, not by compile error. Combined triage — fix: disposal order in helpers and `StartKestrel`; reword the redirect claims (D52 correction appended); stale comments (customizer summary, C2 trace flags `-00`/`-01`); remove section 5.5; fold the Readme's Logging section into Configuration and fix the Linux advice; one-line log notes for `AllowedHosts`, the log category and public web types; two tidy-ups; refresh results. Leave: no HSTS/redirect test, the logger, JSON key handling, charset, other limits.

### Author
Approved.

### Author
*[Claude Code reply pasted]* All eight fixes, 359 tests green.

### Reviewer
**What I checked:** the eight commits; disposal in the test helpers and `StartKestrel`; the appended D52, C2, D51, D33 and D50 notes; the customizer summary; section 5.5 removed; the Readme's sections and Linux line. All correct. Listed the remaining gate 2 steps: export, plan copy, this file, commit, push, PR with history notes, merge with a merge commit.

### Author
Create a .md file of this chat to share, removing personal references.

### Reviewer
Produced this file.
