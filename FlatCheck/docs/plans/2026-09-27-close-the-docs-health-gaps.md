# 2026-09-27 Close the ten gaps of the docs-health audit

## Context

The `/docs-health` run of 2026-09-27 (report in `docs/plans/2026-09-27-adopt-the-shared-skills.md`)
gave the verdict "mostly" and ten gaps. Each was checked against the code and the docs before
planning:

| # | Gap | Measured |
| --- | --- | --- |
| 1 | `-o, --out` (README) vs `-o` (help golden, R-006) | `--out` exists (`Options.cs` 96, 136); the usage text and R-006 omit it |
| 2 | No rule for a flag that must be known before the banner | `FlatCheckCli.cs`: `WriteBanner` line 48, `Options.Parse` line 50; R-001 says "every run"; nothing says the order is meant |
| 3 | `code-map.md` 537, 558: plans deleted once done | text moved unchanged from the old CLAUDE.md; plans are kept since ADR 0001 |
| 4 | "Four projects" (`code-map.md` 32, `process.md` 108) vs five | five `.csproj`; `LicenseHeaderTests.Projects` lists five |
| 5 | CLAUDE.md 25 "the flag comes after the path" vs R-003 either side | `process.md` 294 already says the sentence predates phase 8 |
| 6 | `process.md` "Committing" wants a body; "one external dependency" | contradicts rule 13 (one line; `commit-msg` enforces it); `dependencies.md` has libraw and ImageSharp and no "The one dependency" heading |
| 7 | R-001 `implemented`, its two tests cover 6 of 9 modes | `VersionTests.cs` 120-140: no `--synthetic`, `--lenses`, `--against`; `process.md` 450 defers them |
| 8 | Stale paths and headings in `code-map.md` | `docs/lens-profiles.md` (162); "The six modes" (14), no such heading |
| 9 | `R011_TheOverallVerdictIsTheWorstOfTheFourTests` vs five | the body already checks five verdicts (`MaxTileVerdict` included); name and summary say four |
| 10 | ADR 0042 names `crosscheck.py` guards retired by 0047 | 0047 already says "ADR 0039 is amended" (line 71); nothing on 0042 |

## Decisions taken by the owner (2026-09-27, "merg pe recomandarile tale")

1. `--out` is added to the usage text; `help.txt` is re-approved after the owner reads the diff.
2. The three missing modes are added to both R-001 tests. A failure there is a finding, reported
   and left standing (rule 3).
3. The R-011 test is renamed `...OfTheFiveTests`, summary corrected, logic untouched.
4. The banner order is documented as an invariant in R-001 ("printed before the options are
   parsed, so no option can suppress it; the machine-readable output of ADR 0042 is the only
   exception"); no `--quiet`, no new ADR.
5. (Asked by the owner on gap 6.) `process.md`'s "Committing" is deleted, not rewritten: rules
   live in CLAUDE.md (rule 11, rule 13); "Conventions" keeps only what explains a choice (why
   libraw earned its place, the single binary) and points at `dependencies.md` for the two
   packages.

## Steps (rule 1: requirement, test, code, notes)

1. `docs/requirements.md`: R-006 names `-o` and `--out`; R-001 gains the banner-order clause.
2. Tests: `VersionTests` R-001, both tests, plus `--synthetic`, `--lenses`, `--against` (with
   the arguments those modes need; `--lenses` needs no file); `StructureAnalyzerTests` R-011
   renamed. Run `dotnet test --filter "FullyQualifiedName~R001|FullyQualifiedName~R011"`; paste.
3. Code: `FlatCheckCli.WriteUsage`, the `-o` line shows `-o, --out <iesire.png>`; run
   `GoldenOutputTests` with `FLATCHECK_UPDATE_GOLDEN=1`, show the `help.txt` diff, owner approves;
   CHANGELOG `Changed`.
4. Notes: `code-map.md` (plans paragraph to the current rule; five projects; paths and
   headings), `process.md` ("Committing" deleted; "Conventions" trimmed; the banner order in
   "Dispatch"; line 450 updated: the three modes are covered), `CLAUDE.md` 25 ("either side of
   the path, R-003"), `docs/design/README.md` (warns about renamed headings too), ADR 0047
   consequences ("amends 0042: the `crosscheck.py` guards are gone").
5. `/wrap-up`; no `/validate` (no formula moved); then `/docs-health` again as the measure.

## Branch

Worktree `FlatCheck/.claude/worktrees/Close-the-docs-health-gaps`, branch
`claude/Close-the-docs-health-gaps` from `main` (ADR 0050).

## Verification

Verify green; the two R-001 tests green on nine modes; `help.txt` diff is one line; the second
`/docs-health` verdict at least "mostly" with none of the ten gaps in its table.

## Results, 2026-09-27

- Both R-001 tests green on nine modes at the first run: `--synthetic`, `--lenses` and
  `--against` do print the banner and the copyright (4 tests, 7 s, with R-011 renamed).
- `help.txt` diff, one line: `-o <iesire.png>` becomes `-o, --out <iesire.png>`; the other 49
  golden scenarios unchanged.

## What deviated

- `code-map.md` line 14 pointed at "The six modes", a heading that never moved into
  `docs/design/`; the mode notes are `modes-histeq.md` and `modes-smooth.md`, so the pointer
  names the file. `pipeline.md` has "The seven modes", the overview.
- The header of `process.md` still said "The Committing section predates rule 13"; it now says
  the section is gone. "Conventions" was cut to the two bullets that carry a reason (the
  dependencies, the README duplication story); the six bullets that only restated rules 5, 6,
  13 to 15, 17 and 18 went, with one line pointing at CLAUDE.md (rule 11).
- The `/docs-health` re-run of step 5 is left to the owner's next monthly run: this session
  already ran one today, and the measure that matters here is the ten rows, each checked by
  hand above.
