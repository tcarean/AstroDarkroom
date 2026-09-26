# Retire the Python cross-check, freeze the prototype, keep its numbers as parity goldens

## Context

The owner no longer wants to maintain `flatcheck.py`. Today it is the numeric oracle: rule 16,
`/wrap-up`, `/new-feature` and `/release` demand `scripts/crosscheck.ps1` after any formula
change, `release.ps1` bumps `__version__`, two tests tie the Python version and copyright to the
C#, and the hooks treat `flatcheck.py` as code. The cross-check was never in `verify.ps1`
(ADR 0005 kept it out on purpose), so nothing changes in the gate itself; what changes is the
process around it.

Measured before planning (2026-09-26, `scripts/crosscheck.ps1` on `main` at 2.2.0):

```
scenarii              : 43
identice la byte      : 26
doar text (numere ok) : 16
divergente cunoscute  : 1
DIFERENTE NUMERICE    : 0 []
```

Decisions taken by the owner in this session:

1. `flatcheck.py` and `crosscheck.py` stay in the repository, frozen at 2.2.0, unmaintained.
2. All 43 cross-check scenarios become golden files, captured now while the prototype still
   agrees, so the goldens carry the oracle's numbers forward.
3. Rule 16 becomes "a formula change re-runs `/validate` on the real masters before the task
   ends" (40 cases in `validation.json`, several confirmed by the numpy bench).

Tests: two are deleted (`R097_PythonReportsTheSameVersionAsTheCSharp`,
`PythonReportsTheSameCopyrightAsTheCSharp`); one is converted (`CrossCheckFixtures` becomes the
fixture source of the new parity goldens); `TheScanSeesEveryProjectAndBothPythonFiles` and
R-096 stay unchanged because the files stay. Nothing else tests Python; the rest are comments.

## Branch

Worktree + branch `claude/Retire-the-cross-check-and-freeze-the-Python-prototype` under
`.claude/worktrees/` (ADR 0044); the shared checkout stays on `main`. After approval this plan
is saved as `docs/plans/2026-09-26-retire-the-cross-check.md`.

## Steps

### 1. Requirement and decision first (rule 1, rule 10)

- `docs/requirements.md` R-097: drop the clause "mirrored in `flatcheck.py`'s `__version__`; the
  two are identical". Add a row in the console contract block (next free ID) for the parity
  goldens: "43 scenarios of the four numeric modes on generated FITS reproduce the console text
  captured on 2026-09-26, the last day the Python prototype agreed numerically", status
  `implemented`, tested by `R###_...` in the new class.
- New ADR `docs/decisions/0047-python-prototype-frozen-parity-goldens-replace-the-cross-check.md`:
  context (the crosscheck output above), decision (frozen files, 43 parity goldens,
  `/validate` as the formula-change check, `crosscheck.ps1` deleted, hooks stop treating
  `flatcheck.py` as code), alternatives rejected (delete the files; snapshot only the ~10
  scenarios without C# coverage; keep the cross-check optional), consequences (no independent
  implementation any more; a parity golden re-approval is a C#-only decision from now on).
  Mark ADR 0005 and ADR 0038 `superseded by 0047`; ADR 0039 stays accepted, 0047 says it amends
  the Python clause.

### 2. The parity goldens (the tests, before the code)

- New `FlatCheck.Tests/ParityFixtures.cs`: the body of `CrossCheckFixtures.DumpsFixturesWhenAsked`
  moved into `internal static void WriteAll(string dir)` (same scenes, same comments, same file
  names). `CrossCheckFixtures.cs` keeps only the env-gated dump test calling `WriteAll`, so the
  frozen prototype can still be compared by hand; its summary says that.
- New `FlatCheck.Tests/ParityGoldenTests.cs`: `[Theory]` + `MemberData` over the 43 tuples of
  `SCENARIOS` in `crosscheck.py` (name, args), an `IClassFixture` that writes the fixtures once
  into a `TempWorkspace`, runs `FlatCheckCli.Run` with absolute paths and `-o <slug>.png`,
  scrubs like `GoldenOutputTests.RunCli`, compares against `Golden/parity_<slug>.txt`. Reuse
  `AssertGolden`, `RunCli`, `ScrubVersion` by making them `internal static` in
  `GoldenOutputTests` (no second copy). The class header says why these 43 exist and that they
  are not to be trimmed like the main set.
- Capture with `FLATCHECK_UPDATE_GOLDEN=1 dotnet test --filter ParityGoldenTests`; the owner
  reads the 43 new files once (rule 3). Cross-check sanity: the scenarios that share bytes with
  an existing golden (`structure clean`, `expo cfa BAYERPAT`, ...) must print the same numbers.
- `VersionTests.cs`: delete the two Python tests and the Python sentence in the class summary.

### 3. Scripts, hooks, settings

- Delete `scripts/crosscheck.ps1`; remove its line from `.claude/settings.json` permissions.
- `scripts/release.ps1`: remove the `flatcheck.py` read, check and write (lines ~62-67, ~99-100),
  fix the header and the "changed" message.
- `scripts/check-staged.ps1` and `scripts/hooks/claude-stop-gate.ps1`: drop `flatcheck.py` from
  the code lists and headers.
- `scripts/verify.ps1`: no change (never called crosscheck).

### 4. CLAUDE.md and skills

- CLAUDE.md: Commands, drop the "Parity with the Python oracle" line and `flatcheck.py` from the
  release line; Layout, `flatcheck.py` becomes "the Python prototype, frozen at 2.2.0, and
  `crosscheck.py`, its comparison tool; neither is maintained"; rule 9 drops "or in
  `flatcheck.py`"; rule 16 becomes the `/validate` rule and names `ParityGoldenTests`.
- `.claude/skills/wrap-up/SKILL.md` step 2, `new-feature/SKILL.md` steps 2 and 6,
  `release/SKILL.md` steps 3 and 5 (and its description): cross-check replaced by `/validate`
  and a parity golden scenario. `docs-health/SKILL.md` and `agents/docs-auditor.md` keep the
  file names in their hard limits (the auditor still must not read them).

### 5. Docs and README

- `docs/design/python-parity.md`: rewritten as the note on the frozen prototype: what it was,
  the last cross-check result, where the parity goldens are, how to add one (moved from
  `process.md`), the intentional divergences kept as a description of the frozen file. Same
  filename; its row in `docs/design/README.md` updated, and the `process.md` row.
- `docs/design/process.md`: "Adding a cross-check scenario" becomes "Adding a parity golden
  scenario"; the Versioning table and text drop the Python rows.
- Present-tense parity claims in `pipeline.md`, `radial-model-even-term.md`, `modes-*.md`,
  `png-output.md`, `code-map.md`, `known-issues.md`: reworded to past tense or to the parity
  goldens (one-line edits, same meaning). Code comments in `FlatCheck.Core` that state
  provenance ("mirrors `load_image_with_header` in `flatcheck.py`") stay (ADR 0006); only
  `Version.cs`, `Directory.Build.props` and `FlatCheck.Wpf.Tests.csproj` comments that describe
  an ongoing check are corrected.
- `README.md`: intro (lines 21-23) says the prototype is frozen; "Verificarea față de Python"
  becomes "Golden-urile de paritate"; the release bullet drops `flatcheck.py`; "Diferențe față
  de versiunea Python" keeps its content with a first line saying the prototype is frozen at
  2.2.0.
- `CHANGELOG.md`: header line 5 drops the mirrored copy; under `[Unreleased]`: Added (parity
  goldens), Changed (rule 16, release script), Removed (`crosscheck.ps1`, the two version-sync
  tests).

### 6. Verification

1. `scripts/verify.ps1` green (format, build with warnings as errors, all tests incl. the 43
   new ones and `RepoInvariantTests` for the new R-### row and ADR statuses).
2. `git diff --stat` shows no change under `Golden/*.txt` other than the 43 new `parity_*.txt`.
3. `scripts/release.ps1 -Version 9.9.9 -WhatIf` runs without touching or mentioning
   `flatcheck.py` (dry run only, nothing written).
4. `/validate` is not run in this task: no formula changed.
5. Version: no bump needed by the console's behaviour; the process change alone does not
   warrant one. Said to the owner, not done (rule 17).

## Deviations, noted at wrap-up (2026-09-26)

- The cross-check was run once more on the branch before capture; same result as on `main`.
- `ParityGoldenTests` got one extra test, a count (43) and slug-uniqueness check, so a dropped
  or duplicated row is caught without reading 43 files.
- `scripts/release.ps1 -Version 9.9.9 -WhatIf` could not run to its preview on the dirty task
  branch (its first check refuses a dirty tree); it parses and reaches that check, and the
  Python read/write it used to do is gone. The preview itself runs at the next `/release`.
- `README.md` keeps the sentence "Spre deosebire de versiunea Python nu există figură cu 4
  panouri" in the `--ratio` section: still true of the frozen file.
- The first capture of the four `--ratio` parity goldens pinned a "Could not find file" error
  with the worktree's path in it: the test made only the first argument absolute, so the B side
  was resolved against the test host's folder. Caught by the pre-commit gate on the merge into
  `main` (the goldens passed only from the worktree they were captured in). Fixed by making
  every argument that names a fixture absolute, guarded by two assertions (no current-directory
  path, no missing-file error in the output), and the four files recaptured; the other 39 did
  not change.
