# AI usage and conversation history

This is brief item 7: which AI models were used, what for, and the conversation history. The short version: AI helped to
challenge the design, write the tests first and then the code, and review the result. **Every design decision was made by the
author.** The AI set out options with trade-offs and a recommendation; the author chose, and each choice is a row in
[`../DesignDecisions.md`](../DesignDecisions.md) (decision numbers such as D40 refer to it).

## Models and what each was used for

| Tool | Model | What it was used for |
|---|---|---|
| Claude Code (terminal) | Claude Opus 5.5 | The sessions began on it: the banner at the top of every export shows `Opus 5.5`. Design-phase work: reading the brief, challenging it, and setting out options and trade-offs for the author to decide |
| Claude Code (terminal) | Claude Sonnet 5.5 | The model named in the `Co-Authored-By` trailer of every commit that Claude Code made (90 commits). Test-first implementation, the code, running the tests, self-review, and drafting the documents |
| claude.ai (web chat) | Claude Opus 5.5 | A reviewer, in a separate chat from Claude Code. It explained the technical points and the options and trade-offs before decisions such as D40, checked Claude Code's replies against the decision log and the repository files (read-only), and drafted replies for the author to send |

## How the work was done

- **Phases and gates.** The project was built in phases, each on its own branch and merged by pull request with a merge commit
  (D53): library, API, UI, then the documents. Each phase ended in a review gate: a self-review against the decision log, then a
  review by a fresh-context reviewer (a subagent given only the decision log, the plan and the diff, and told to report, not to
  edit), then a combined list of findings that the author triaged. Only the fixes the author approved were made.
- **Tests first.** For code, a red `test:` commit came before the green `feat:` commit (D52, D53), so the test history shows the
  order. The exceptions are recorded in the decision log and in the pull request descriptions.
- **Decisions.** The author decided; the AI challenged and implemented. A decision that changed was amended, never deleted, so the
  log keeps the history. Where the AI made a mistake that the author caught, the log or the commit history says so.
- **No internet solutions (S3).** The committed [`.claude/settings.json`](../../.claude/settings.json) denies Claude Code the
  `WebSearch` and `WebFetch` tools, and has done since the first commit. It governs Claude Code only; the claude.ai chats are the
  files listed below.
- **No third-party libraries (S4).** `Core` and `Web` have no `PackageReference`; only the test project has packages (D4).

## What is in this folder

| File | What it is |
|---|---|
| `01-design-phase.md` | Claude Code export, design phase |
| `01-design-phase-plan.md` | The plan file (design and decision log) at the end of the design phase |
| `01-design-phase-claude-ai-review.md` | claude.ai reviewer chat, design phase |
| `02-library.md` | Claude Code export, library phase (gate 1) |
| `02-library-plan.md` | The plan file at the end of the library phase |
| `02-library-claude-ai-review.md` | claude.ai reviewer chat, library phase |
| `03-api.md` | Claude Code export, API phase (gate 2) |
| `03-api-plan.md` | The plan file at the end of the API phase |
| `03-api-claude-ai-review.md` | claude.ai reviewer chat, API phase |
| `04-ui.md` | Claude Code export, UI phase (gate 3) |
| `04-ui-plan.md` | The plan file at the end of the UI phase |

The Claude Code exports are the `/export` output of the sessions. Every export begins with the same first prompt, so a later
export repeats the earlier part of the session. The claude.ai chat files are edited transcripts: the header of each says what was
condensed or removed.
