# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The window's `Split` tab (R-091 to R-093, ADR 0054): divides the flats of a session into 2
  or 4 consecutive groups, builds a temp session per group (every folder the script enters,
  at most 2 lights, the group's flats), runs the chosen Siril script in each, and compares the
  part masters with the whole session's master: the five tests with a verdict per cell, the
  vignetting depth, the difference from one reference profile (the whole's nearest), and a
  verdict per column, the worse of shape and depth against that reference.
  Saves `Rezultat split.txt` and `flatcheck-split.png` (the residual maps side by side on one
  scale, labelled with a built-in bitmap font, version and date in the title) in the session
  root; the original session is only read. Version 2.3.0 at the owner's request.

- `ParityGoldenTests` (R-098): the 43 scenarios the Python cross-check used to run are golden
  files now, `Golden/parity_*.txt`, captured on 2026-09-26 while `flatcheck.py` still agreed on
  every number (43 scenarios, 0 numeric differences). They carry the prototype's last confirmed
  figures into the gate; a re-approval there is a C#-only decision (ADR 0047).

### Changed

- `--help` names `--out` beside `-o` for the output map, as the README and R-006 do; the
  golden `help.txt` was re-approved for that one line.
- The Python prototype is frozen at 2.2.0 and no longer maintained (ADR 0047, supersedes 0005
  and 0038): rule 16 now asks for `/validate` after a formula change instead of the cross-check,
  `scripts/release.ps1` writes `<Version>` only, and `check-staged.ps1` and the Stop hook no
  longer treat `flatcheck.py` as code. `flatcheck.py` and `crosscheck.py` stay in the repository
  as prior art; `CrossCheckFixtures` can still dump the fixtures for a comparison by hand.

### Removed

- `scripts/crosscheck.ps1`, its permission line in `.claude/settings.json`, and the two tests
  that tied `__version__` and `__copyright__` in `flatcheck.py` to the C#.

### Internal

- ADR 0053 and rules 9 and 13 (the kit's ADR 0023): a commit message may be one line of up to
  100 characters, and a CHANGELOG line is a judgment made at `/wrap-up`, not a gate.
  `scripts/check-staged.ps1` and `CheckStagedTests` are gone; `pre-commit`, `pre-merge-commit`
  and the Claude commit gate run `verify.ps1` alone, and the Stop hook no longer asks for a
  changed `CHANGELOG.md`.
- ADR 0013 and ADR 0021 restate, in dated lines, the two reasons that still pointed at
  `crosscheck.py` (the centre convention, the ordinal sort of a `--light` series); the comments
  of `SeriesFinder.Find` and `FilesComeBackSortedOrdinallyByName` say why the sort is ordinal
  now that the cross-check is retired. No behaviour changed.
- Seven code comments pointed at `docs/lens-profiles.md`, `docs/smooth-validation.md` and
  `docs/radial-model-even-term.md`, where the notes lived before ADR 0001 moved them; they now
  name `docs/design/`. Comments only; no figure or test changed.
- `WorstRingStep` in `StructureAnalyzerTests` implements the ring-step definition of ADR 0018
  (80 profile bins, bins under 5000 px excluded) and reproduces the whole-library table of
  `radial-model-even-term.md`; ADR 0017 and ADR 0031 carry dated corrections of three figures
  copied wrongly from the notes, each figure named with the master it was measured on, and the
  comment of `VignettingComparison.cs` says what the "worst profile" rule would really have
  given (ACCEPTABIL / BUN, not REFA).
- The six skills and the `docs-auditor` agent are the kit's copies now, byte for byte, taken
  with `scripts/sync.ps1` (ADR 0049; kit ADR 0018). FlatCheck's own steps (test projects, golden
  and parity scenarios, the numeric paths, the masters' folder, the two publish targets, the
  always-filter for Stryker) are in `docs/skills.md`, one section per skill; `kit.json` gains
  `audit.alsoRead` so the auditor may read `THIRD-PARTY-NOTICES.md` and `Golden/`. Rule 19
  names the three folders.
- `scripts/` is now the kit's copy, byte for byte, taken with `scripts/sync.ps1` (ADR 0048,
  supersedes 0002; kit ADR 0017). FlatCheck's values (solution, `Category!=Validation`, the
  code and test folders, the test data folder and `FLATCHECK_TESTDATA_LARGE`, the two publish
  targets, the kit's path) live in `kit.json`; rule 19 says the scripts are never edited here.
  Step 0 of `verify.ps1` is red on any drift. `release.ps1 -PublishProject` is gone.
  `CheckStagedTests` copies the real `kit.json` beside the script.
- ADR 0044 and the CLAUDE.md workflow: a task branch lives in its own worktree; the shared
  checkout `E:\Photografie\Code` stays on `main` for the owner's merges.
- ADR 0050 and the CLAUDE.md workflow: a worktree the assistant makes is named after its branch
  without `claude/`, so the owner finds it to review before the commit (the kit's ADR 0019).
- `FlatCheck.Tests` now runs under `<InvariantGlobalization>`, like `flatcheck.exe` (ADR 0043,
  R-007). The switch is per executable and did not reach the test host, so the golden files and
  the report tests formatted with the machine's culture: they passed on `en-GB` and 63 of them
  failed under `ro-RO`. `InvariantGlobalizationTests` checks the console's runtimeconfig and the
  test host. No golden file changed.

- `scripts/check-staged.ps1` refused every commit made from a linked worktree that touched a
  FlatCheck file: git runs the hook with `GIT_DIR` set, `git rev-parse --show-toplevel` then
  named the FlatCheck folder as the repository root, and every staged file looked like console
  code. The script now finds the root by walking up to the nearest `.git`; `CheckStagedTests`
  runs it in a throwaway repository with and without `GIT_DIR`.

- Adopted the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1` as the one
  gate (manifest, restore, format, build with warnings as errors, tests), `check-staged.ps1`,
  Claude Code hooks and skills, `docs/requirements.md` with IDs, ADRs under `docs/decisions/`,
  `docs/validation.json` read by `ValidationTests` on the real masters, `RepoInvariantTests`.
  CLAUDE.md shrunk from 2709 lines to the rules; its content moved unchanged into
  `docs/design/`. Central package management with lock files; SDK pinned in `global.json`.
- Fixed the nine build warnings in the test projects (nullable annotations, one swapped
  `Assert.Equal` argument) and ran `dotnet format` once over the solution: whitespace, unused
  usings removed, object initialisers wrapped one member per line, 33 files. No behaviour
  changed; the golden files are untouched.
- `ValidationTests` (trait `Category=Validation`, not in the gate) record 40 figures measured on
  the real masters in `docs/validation.json`; `RepoInvariantTests` tie `docs/requirements.md` to
  the `R###_` test names, check the ADR numbering, the design-note index and the validation
  manifest; 64 existing tests renamed with the ID of the requirement they pin.
- Deleted `.github/copilot-instructions.md` (Azure rules Visual Studio had added).
- Branch names and clean-up, as in the kit's ADR 0013: a branch is named `claude/` plus its task
  in English, a generated name is renamed before the first commit, a branch merged into `main` is
  deleted with `git branch -d`, and `git branch -D` joins the `ask` list. CLAUDE.md Workflow,
  `/wrap-up` step 10, `.claude/settings.json`.

## [2.2.0] - 2026-09-23

The state of the tool when the changelog was started. What 2.x holds, in the order it was built:

- 2.0.0: the core became `FlatCheck.Core` and a second application, the WPF window, appeared
  over it; the console and the window call the same `Runs/` and print the same report.
- The `--histeq` mode (Siril's "Histogram" display), the `--smooth` mode (a second master with
  the diffuser texture removed) with `--scale auto`, the smoothed master listed in the window's
  Histogram tab, the "Ruleaza in Siril" button on the problem card, the master found in
  `masters/` as well as `process/`.
- The synthetic flat from the lens's published vignetting profile (`--synthetic`, `--lenses`,
  `--against`, the Sintetic tab), the sixth structure test (the master's vignetting against the
  lens profiles, `--raw` / `--lens`), the calibration crop as part of a lensfun lens's identity,
  the warning when a request is far from the profile used.
- The radial model on partial rings: symmetrisation, then the even 2θ + 4θ correction with one
  q per r² anchored on the full rings (`docs/design/radial-model-even-term.md`); `ROWORDER`
  honoured when naming the CFA positions of a FITS.

Earlier history (1.0.0 to 1.2.0: the four modes, the residual scale, the `--ratio` rewrite, the
`--light` validity guard and series mode, the fifth test, raw input through libraw, the camera
preview) is in `git log`; no changelog was kept then.
