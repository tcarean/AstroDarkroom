---
name: docs-health
description: Check whether docs/ alone would let a fresh model understand and change the project - the rebuild-readiness test, run by the docs-auditor subagent with a clean context. Use monthly, after a model change, or when the user says /docs-health.
---

# Docs health

The audit must run without the code in context, which your own context does not have (you
have read `src/` by now, or will). So delegate it.

1. Launch the `docs-auditor` subagent (Agent tool, `subagent_type: docs-auditor`) with this
   prompt: "Audit this project. Solution: `<name>.sln`. Small change to plan in step 4:
   `<one the user named, or pick a plausible one from the CHANGELOG>`." Do not add anything
   you know about the code; that would defeat the test.
2. Wait for its report. Do not run the procedure yourself in parallel.
3. Relay the report to the user in full: verdict, gaps table, top three, guesses, what is good.
   The user does not see the subagent's output, only what you pass on.
4. Do not fix anything in this pass. If the user picks gaps to close, that is a normal task:
   requirement or ADR or design note first, then `/wrap-up`.
5. If the subagent's report shows it opened `src/`, say so and discard the report; the audit is
   invalid.
