# 2026-09-27 Share the skills and the agent the way scripts/ is shared

## Context

ADR 0017 made `scripts/` byte-identical in every project, with the values in `kit.json`. The six
skills under `.claude/skills/` and `.claude/agents/docs-auditor.md` still differ between the kit
and FlatCheck. Measured on 2026-09-26 with `diff` (212 lines in the kit), the differences fall in
three groups:

- **A. Values** already in `kit.json`: solution name, code folders, test filter, publish targets,
  large-data variable, test projects (`docs-health`, `validate`, `release`, `new-feature`,
  `docs-auditor`).
- **B. Generic improvements** made in FlatCheck only: `/validate` after numeric code, `Internal`
  in the CHANGELOG step, a new `R###` test moves its requirement, ADRs for choices settled in
  chat, README as user documentation (`wrap-up`); `git status -- .` and the owner picks the
  version (`release`); fallback to `general-purpose` (`docs-health`); "Files opened" and an
  English report (`docs-auditor`); exit code in the requirement, a "Figures" step, README at
  "Record" (`new-feature`); "on a large suite pass a filter" (`mutate`).
- **C. Project prose**: golden and parity scenarios, "measure before you design", the
  `Analysis/Report/Runs` order (`new-feature`); the formula paths and `Golden/parity_*`
  (`wrap-up`); `THIRD-PARTY-NOTICES.md` and `Golden/` as allowed reads (`docs-auditor`); the
  masters' path (`validate`).

`.claude/settings.json` differs only in its comment; the allowlist grows per project (GHID), so
it stays outside the mirror. The owner answered "merg pe recomandarile tale" on 2026-09-27 to the
four decisions below.

## Decisions taken by the owner

1. Project-specific prose goes in `docs/skills.md`, one section per skill, read by each skill
   before its first step; the kit ships the file with empty sections.
2. Validation tests carry the trait `Category=Validation` in the kit too, so `/validate` runs
   the same command everywhere; the kit's gate keeps running them (`testFilter` stays empty).
3. The extra files the auditor may read are a `kit.json` list, `audit.alsoRead` (optional).
4. `.claude/settings.json` stays per project, outside the mirror.

## Steps

1. Rewrite the six skills and the agent: group A through `kit.json` keys, group B ported, one
   line in each pointing at `docs/skills.md`; the agent reads `kit.json` first and never opens
   `codeFolders`/`codeFiles`, may read `audit.alsoRead`, reports in English with "Files opened".
2. `docs/skills.md` in the kit, with the six empty sections and what belongs there.
3. `[Trait("Category", "Validation")]` on the kit's `ValidationTests`.
4. `kit.json`: optional `audit.alsoRead`; `kit-config.ps1` documents it and defaults it to `[]`.
5. `sync.ps1` mirrors a set, `scripts/`, `.claude/skills/`, `.claude/agents/`, with the same
   kit-only exceptions under `scripts/`; verify step 0 and the Stop hook cover the set;
   `SyncTests` gains a case per new folder.
6. Docs: rule 15 names the three folders; `README.md`, `GHID.md`, `monorepo/README.md`,
   ADR 0018, CHANGELOG. Step 9 of `/wrap-up` states the rule instead of "probably belongs in
   the kit".
7. Verify green in the kit; `sync.ps1 -Check` from the kit lists FlatCheck's skills as drifted.

## Session 2, in FlatCheck (its own plan)

`sync.ps1` over skills and agent; `docs/skills.md` with group C; `audit.alsoRead` with
`THIRD-PARTY-NOTICES.md` and `FlatCheck.Tests/Golden/`; ADR completing 0048; rule 19 extended;
CHANGELOG `Internal`; `/docs-health` run once to see the auditor read `Golden/` and no code.

## Verification

Kit: `scripts/verify.ps1` green; `SyncTests` with skills and agents; `sync.ps1 -Check` from the
kit exits 1 naming FlatCheck's `.claude/skills/...` and `.claude/agents/docs-auditor.md`.

## What deviated

- The plan named step 8 of `/wrap-up`; the ported version has the README step as 9, so the
  rule landed there.
- `audit.alsoRead` normalises backslashes like the folders; a first version lost the backslash
  in the loader (an editing slip caught when reading the file back, not by a test, because the
  kit's list is empty). Session 2 exercises it with FlatCheck's two entries.
- Verified by running the kit's gate from the FlatCheck session by hand, as for ADR 0017
  (30 tests green), plus a Stop hook smoke test on a throwaway project with an edited skill.
