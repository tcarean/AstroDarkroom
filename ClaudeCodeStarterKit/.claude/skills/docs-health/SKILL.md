---
name: docs-health
description: Check whether docs/ alone would let a fresh model understand and change the project - the rebuild-readiness test. Use monthly, after a model change, or when the user says /docs-health.
---

# Docs health

Answer the question: could a model that has never seen this code, given only `docs/`, `CLAUDE.md`,
`CHANGELOG.md` and the test names, do the work? Do this without opening `src/`.

1. Read `CLAUDE.md`, `docs/requirements.md`, `docs/decisions/*`, `docs/validation.md`,
   `docs/ai-workflow.md`, `docs/design/*`, and the list of test names
   (`dotnet test Starter.sln --list-tests`). Do not read `src/`.
2. For each requirement: is there a test name carrying its ID? Is the example concrete enough to
   write a test from? Is the status believable given the CHANGELOG?
3. Pick the two most recent CHANGELOG entries. From docs alone, explain how each was implemented
   and where. Where you would have to guess, write the guess down.
4. Pick one plausible small change (the user may name one). Write the plan for it from docs alone.
   List every place the plan says "probably" or "somewhere".
5. Report: a table of gaps (what is missing, which file should hold it, how much work to add),
   then the three gaps that most reduce rebuild-readiness. Do not fix anything in this pass.
6. Keep CLAUDE.md under 200 lines: if it is longer, name the sections that should move to `docs/`.
