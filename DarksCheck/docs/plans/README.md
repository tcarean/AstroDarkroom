# Plans

One file per non-trivial task, `YYYY-MM-DD-slug.md`, saved after the plan is approved in plan
mode and before the first line of code. At wrap-up, a short "What deviated" section is added at
the end. Plans are history, not documentation: they are never updated to match the code later.

Suggested shape:

```markdown
# 2026-09-24 Add --json output

## Goal
R-005: machine-readable output for the CLI.

## Steps
1. Add `JsonReport` in Core; unit test with a fixed sample.
2. Wire `--json` in Program.Run; acceptance test compares the exact text.
3. CHANGELOG, requirements status.

## Verification
`scripts/verify.ps1` green; `darkscheck --json numbers-small.txt | jq .` parses.

## What deviated
(filled at wrap-up)
```
