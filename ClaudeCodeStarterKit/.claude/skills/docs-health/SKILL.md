---
name: docs-health
description: Check whether docs/ alone would let a fresh model understand and change the project - the rebuild-readiness test, run by the docs-auditor subagent with a clean context. Use monthly, after a model change, or when the user says /docs-health.
---

# Docs health

The audit must run without the code in context, which your own context does not have (you
have read the code folders by now, or will). So delegate it. Before step 1, read
`docs/skills.md` if it exists: its `## /docs-health` section adds project-specific steps and
says where they go.

1. Read `kit.json` for the solution name. Launch the `docs-auditor` subagent (Agent tool,
   `subagent_type: docs-auditor`) with this prompt: "Audit this project. Solution:
   `<solution>`. Small change to plan in step 4: `<one the user named, or pick a plausible one
   from the CHANGELOG>`." Do not add anything you know about the code; that would defeat the
   test. If the agent type is not registered yet (a new agent file appears at the next session
   start), use `general-purpose` with the text of `.claude/agents/docs-auditor.md` as the prompt.
2. Wait for its report. Do not run the procedure yourself in parallel.
3. Relay the report to the user in full: verdict, gaps table, top three, guesses, what is good,
   files opened. The user does not see the subagent's output, only what you pass on.
4. Do not fix anything in this pass. If the user picks gaps to close, that is a normal task:
   requirement or ADR or design note first, then `/wrap-up`.
5. If the report's list of files opened holds anything under a `codeFolders` entry or one of
   the `codeFiles` of `kit.json`, say so and discard the report; the audit is invalid.
