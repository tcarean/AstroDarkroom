# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and the reference image is a git-ignored copy in
  `AstroPhotoAnalyzer.Tests/testdata/large/`. `docs/specification.md` was split into
  `docs/requirements.md` (65 rows with IDs), `docs/decisions/` (24 ADRs, 18 of them retroactive)
  and `docs/design/` (12 notes, moved unchanged); the acceptance record became
  `docs/validation.md`, and `CLAUDE.md` holds only the project's rules. `RepoInvariantTests`
  ties `implemented` rows to tests named `R###_...` (82 tests renamed for 28 rows), and
  `ValidationTests` checks three cases on the reference image outside the gate. No behaviour
  changed; one `dotnet format` pass over 10 files.

## [1.0.0] - 2026-09-21

The state of the tool when the changelog was started, built on 2026-09-20 and 2026-09-21 in
seven phases (`docs/plans/2026-09-20-implementation-plan.md`):

- The core: readers (hand-written FITS; TIFF, PNG and JPEG through ImageSharp 3.1.11), the
  raster, colour maths, eleven analyzers (file facts, bit depth, crop, tonal distribution, the
  print-background simulation, noise, background colour and edge regions, stars, print size,
  quantization, JPEG), the recommendations in decision order with their evidence, the report.
- The console `AstroPhotoAnalyzer.exe`: options, exit codes 0/1/2/3, `-o`, golden tests on
  synthetic scenes.
- The window `AstroPhotoAnalyzer.Wpf.exe`: parameters with help tooltips, six tabs (Raport,
  Tonuri, Export, Rezolutie, Culoare, Preview) with a description under every number,
  `Raport.txt` byte-identical to the console's output, "Despre".
- The acceptance run on `de_printat.png` against the hand-made numbers; the "Fundal" naming
  pass (the tab became Culoare).

No earlier version was released; the eleven commits are in `git log`.
