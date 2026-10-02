# AstroPhotoAnalyzer

Print-readiness diagnostic for a *finished* astrophoto: a stretched TIFF, PNG or FITS as it
comes out of Siril. It measures the image and reports, in decision order, how to send it to
print: file format, whether the background survives paper, colour cast and gradient, the
largest size the real detail supports, how much denoise the print version needs. It **exports
nothing**: recommendations only, each with the number and the threshold behind it; exports are
made cleanly from Siril. Two applications, a console (`AstroPhotoAnalyzer.exe`) and a WPF
window, over one core library that hand-parses FITS and takes ImageSharp for TIFF/PNG/JPEG;
neither application computes a number or writes a sentence of diagnosis. .NET 8. A sibling of
FlatCheck (`../FlatCheck`) with the same conventions; where the two differ, this file says so.

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, about 30 s):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build AstroPhotoAnalyzer.sln -c Release --nologo` (Windows only: the WPF
  project; the console alone builds anywhere:
  `dotnet build AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj -c Release`)
- Test: `dotnet test AstroPhotoAnalyzer.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test AstroPhotoAnalyzer.sln --filter "FullyQualifiedName~R002"`
- Format: `dotnet format AstroPhotoAnalyzer.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project AstroPhotoAnalyzer -- <image> [--medium baryta|canvas|acrilic]
  [--dmax n] [--distance m] [--sizes cm,cm,...] [--aspect w:h] [--target-bg 0..1]
  [--passepartout cm] [--frame cm] [--wcs file.fit] [-o file]`; flags before or after the image
  (R-003).
  Window: `dotnet run --project AstroPhotoAnalyzer.Wpf -- <image>`
- Golden console texts, re-approved after the assistant has read and explained the diff (ADR 0027):
  `APA_UPDATE_GOLDEN=1 dotnet test AstroPhotoAnalyzer.sln`
- Window renders as PNGs without running it:
  `APA_DUMP_SCREENS=<dir> dotnet test AstroPhotoAnalyzer.Wpf.Tests --filter WindowSmokeTests`
- Figures on the reference image (seconds per case, not in the gate): `/validate`, or
  `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"`; the image is a
  git-ignored copy in `AstroPhotoAnalyzer.Tests/testdata/large/` (ADR 0004)
- Test data hashes after an intentional change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of one core file: `scripts/mutate.ps1 -Mutate "**/StarAnalyzer.cs"`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `AstroPhotoAnalyzer.Core/` - everything that computes and everything that formats. No
  Console, no UI, no entry point: a compile boundary. `Input/` reads (readers, `Raster`, colour
  maths, statistics), `Analysis/` decides numbers (one class per measurement family), `Report/`
  decides words (Romanian) and the recommendations, `Runs/` chains them; both applications call
  `Runs/AnalysisRun.Execute` only. `Thresholds.cs`, `Parameters.cs`.
- `AstroPhotoAnalyzer/` - the console: `Program.cs`, `Options.cs`, `AnalyzerCli.cs`. Nothing else.
- `AstroPhotoAnalyzer.Wpf/` - the window, `net8.0-windows`, dark theme: `ParameterFields` in, six
  tabs out. Formats, never decides; view-models are public and cannot expose a core type.
- `AstroPhotoAnalyzer.Tests/` (core + console, portable; `Golden/` holds the console snapshots;
  `testdata/manifest.json` hashes the reference image, which stays outside git) and
  `AstroPhotoAnalyzer.Wpf.Tests/` (window, Windows-only, one STA thread through `StaFixture`).
  Every other fixture is generated or hand-built in the tests.
- `Icon/` - `make_icon.py` generates the icon both exes share. `Directory.Build.props` -
  `<Version>` and `<Copyright>`, the single place for both.
- `kit.json` - what the kit's scripts, skills and agent need to know about this project.
  `docs/skills.md` - this project's additions to the kit's skills. `scripts/`,
  `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process keys of
  `.claude/settings.json` are the kit's, the same here (ADR 0002; the kit's ADRs 0017, 0018, 0028).
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs. `docs/validation.md`
  + `.json` - figures on the reference image. `docs/design/` - the explanations, per area, read on
  demand. `docs/plans/` - approved plans. `README.md` - the user documentation, Romanian.
- The process rules are the kit's, in `.claude/rules/kit/process.md`; this project's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).
- This folder lives inside the `photo` monorepo (`E:\Photografie\Code`); the git root is the
  parent. Sample images never enter the repository: the root `.gitignore` covers `*.tiff` but not
  `*.tif` or `*.png`, so keep them out by hand.

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are AstroPhotoAnalyzer's own; one that extends
or varies a kit rule names it.

- **Golden files** (extends the kit's rule "No test edited to pass"). A golden file is never
  changed to make something pass either. After a regeneration the assistant reads the whole diff
  of `Golden/*.txt`, ties every changed line to its cause in the change and reports them in the
  wrap-up; a line that moved without a cause is a finding, asked about, not re-approved (ADR 0027).
- **Package notices** (extends the kit's rule "Packages"). A package also needs an entry in
  `THIRD-PARTY-NOTICES.md` with its GPLv3 compatibility argued, one in `ThirdParty.cs` (what
  "Despre" shows; `ThirdPartyTests` enforces both) and a row in the README's table, by hand. The
  console stays one copyable file, so pure-managed packages only. ImageSharp stays pinned to
  3.1.11 (ADR 0009); the FITS reader stays hand-written.
- **Measured on the reference image** (extends the kit's rule "Measured numbers"). The real data
  is `de_printat.png` (ADR 0004) and, for anything that reads a WCS or the catalogue, the
  plate-solved FITS `starless_rbf0.5_stretch0.17.fit` (ADR 0025); a claim about a measurement
  on a real image comes with the pasted output, and the synthetic scenes of `Scenes.cs` are no
  substitute for it.
- **Languages** (varies the kit's rule "Languages", ADR 0005). English in everything the model
  reads or writes about the process: docs, tests of invariants, scripts, commits, CHANGELOG.
  Romanian in everything the user sees (CLI text, `--help`, window labels, the report,
  `README.md`, `THIRD-PARTY-NOTICES.md`) and in the code comments, which stay as they are:
  without diacritics in code and messages, with diacritics in Markdown for people. Never leave a
  file in two languages.
- **Computed only in the core.** Nothing is computed outside `AstroPhotoAnalyzer.Core`: a new
  number, threshold or sentence of diagnosis goes in `Analysis/`, `Thresholds.cs` or `Report/`,
  never in a view-model or the console (ADR 0007).
- **Explanation is not diagnosis.** The window's explanatory texts (a row's `Description`, a
  table's `GuideParagraph`s, a tile's `Check`, a field's `*Help`) are the one kind of sentence a
  view-model writes; every number in them is interpolated from `Thresholds`, `Parameters` or the
  result, never typed. A literal a text needs is lifted into `Thresholds` first, with its
  comment, even one no rule reads (ADR 0012, ADR 0020).
- **"Fundal" names one thing, the sky, and two measurements of it.** Tonuri measures its level
  (`BackgroundMedian`, "Nivelul fundalului"), Culoare its colour (`BackgroundResult.Levels`,
  "Culoarea fundalului"). Every label, row, appendix and `Because` that shows one says which; no
  tab is called just Fundal (ADR 0013).
- **Every recommendation carries its evidence.** A `Recommendation` has a `Because`: the metric
  and the threshold. "Do X" without a number is not a recommendation here (ADR 0011).
- **Thresholds are empirical rules of thumb, not physical constants.** Each one in
  `Thresholds.cs` has a comment saying where it came from; most came from the reference image.
  Change one, change its comment and its requirement row (ADR 0012).
- **The image is display-referred and final.** The tool assumes a stretched image; linear data
  is refused with a message, never mis-measured; the print-background simulation is a curve on
  stretched data, labelled as a simulation (ADR 0008).
- **Formula changes re-validate.** A change in `Input/`, `Analysis/`, `Report/`, `Parameters.cs`
  or `Thresholds.cs` re-runs `/validate` on the reference image before the task ends, with the
  drift table pasted.
- **License headers.** Every source file (`.cs`, `.xaml`, `.py`) starts with the GPL notice;
  `LicenseHeaderTests` fails otherwise, and its project list is explicit, so adding a project is
  a decision (ADR 0023). Scripts carry the SPDX line.
- **User documentation lives in `README.md`, and only there.** Code comments explain why the code
  is the way it is; the README explains how to use the tool; `docs/` explains what it does and
  how, for whoever maintains it. That is how FlatCheck's README once came to describe a tool that
  did not exist: the usage had a second copy, and the two drifted.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. AstroPhotoAnalyzer adds:

- `/wrap-up` also runs `/validate` when a formula moved (the rule "Formula changes
  re-validate"), and checks `README.md`, the user documentation.
- `R###_...` tests may live in either test project; `RepoInvariantTests` looks in both.
- A release is tagged `astrophotoanalyzer-vX.Y.Z`, the name `scripts/release.ps1` prints.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.
  A worktree has no copy of the reference image; a task that runs `/validate` copies it into the
  worktree's `AstroPhotoAnalyzer.Tests/testdata/large/` (ADR 0004).

## Read before touching

`docs/design/README.md` maps every area to its note. The ones most likely to catch a careless
edit:

- `StarAnalyzer`, `PrintSizeAnalyzer`: `docs/design/measurements.md` and ADR 0016, ADR 0017 (the
  candidate threshold is not the script's; the natural distance cannot pick a size).
- `BackgroundAnalyzer`: ADR 0014, ADR 0018 (`<=` on the percentile; coloured versus neutral
  regions).
- `DustAnalyzer`, `PrintStretchSimulator`, `Wcs`, `DarkNebulaCatalogue`: ADR 0025 (the dust is the
  catalogued clouds' 10 % percentile, the disc is the catalogue's, SIP is ignored, the fallback
  is the 1 % rule, the simulation is a gamma curve that a real stretch undercuts).
- `NoiseAnalyzer`, `QuantizationAnalyzer`: ADR 0015 (two tile criteria on purpose).
- `JpegAnalyzer`: ADR 0019 (the mean error votes nothing).
- Any text in a view-model: `docs/design/window.md` and ADR 0020.
- The ImageSharp version or a 16-bit fixture: `docs/design/dependencies.md` (four measured
  behaviours the code depends on).
- A change of any kind: `docs/design/extending.md` lists the places it touches.
- A "bug" in `docs/design/known-limitations.md` is probably a known limit; read it first.

## Style

- `.editorconfig` holds whitespace only; `dotnet format` enforces it, `IDE0005` removes unused
  usings. Warnings are errors in the gate. Code style analyzers and `Nullable` in the three
  application projects are off on purpose (ADR 0003); enabling them is a task of its own.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
