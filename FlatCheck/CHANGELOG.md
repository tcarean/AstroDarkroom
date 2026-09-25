# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props` and, as a mirrored copy, in `flatcheck.py`; the owner bumps it.

## [Unreleased]

### Internal

- ADR 0044 and the CLAUDE.md workflow: a task branch lives in its own worktree; the shared
  checkout `E:\Photografie\Code` stays on `main` for the owner's merges.
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
