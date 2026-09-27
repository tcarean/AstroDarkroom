# 0049. The skills and the agent are the kit's too; FlatCheck's steps live in docs/skills.md

Date: 2026-09-27
Status: accepted 2026-09-27

## Context

ADR 0048 took `scripts/` from the kit byte for byte, with FlatCheck's values in `kit.json`. The
six skills and `.claude/agents/docs-auditor.md` were still FlatCheck's own edited copies. On
2026-09-26 the diff against the kit showed three kinds of difference: values `kit.json`
already held; eleven generic improvements FlatCheck had made and the kit never received; and
FlatCheck's own knowledge (where a golden or parity scenario goes, "measure before you design",
which paths are numeric code, two files the auditor may read). The kit's ADR 0018 now mirrors
`.claude/skills/` and `.claude/agents/` like `scripts/`, takes the eleven improvements, reads
values from `kit.json` by reference, and reads a project's steps from `docs/skills.md`.

## Decision

FlatCheck's skills and agent are the kit's, taken with `scripts/sync.ps1` and never edited here
(rule 19 now names the three folders). FlatCheck's own steps are in `docs/skills.md`, one section
per skill, at the step each applies to: the test projects, the golden and parity scenarios and
`docs/design/process.md` for `/new-feature`; the numeric paths of rule 16 and the parity goldens
for `/wrap-up`; the masters' folder for `/validate`; rule 17, the two publish targets and the
`flatcheck-v` tag for `/release`; the always-filter for `/mutate`. `kit.json` gains
`audit.alsoRead` with `THIRD-PARTY-NOTICES.md` and `FlatCheck.Tests/Golden/`, which the auditor
may read (expected output, not code). `ValidationTests` already carried `Category=Validation`,
the trait `/validate` now selects everywhere.

## Alternatives rejected

- **Keep FlatCheck's copies of the skills.** The eleven improvements are the measurement: they
  never reached the kit, and the next kit improvement would not reach FlatCheck.
- **Fold FlatCheck's steps into `CLAUDE.md`.** They are steps of a procedure, not rules; the
  rules they rest on (6, 16, 17) are already there and `docs/skills.md` cites them.

## Consequences

A change to a skill is made in the kit and arrives with `scripts/sync.ps1`, one small commit;
what FlatCheck learns about its own process goes into `docs/skills.md`, which is FlatCheck's to
edit. `/docs-health` was run once after the adoption to see the auditor read `Golden/` and no
code folder; its report is in the plan. The `docs-auditor` agent file registers at the next
session start; until then `/docs-health` falls back to `general-purpose` with the agent's text,
as the skill says.
