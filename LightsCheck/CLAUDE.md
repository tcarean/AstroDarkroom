# LightsCheck

Quality check for astrophotography light frames, for a DSLR on a star tracker (the author's
Nikon D3300 with a Samyang 135 on a Star Adventurer GTi, Siril workflow). It reads a Siril
session folder (the raws in `lights/`, the biases in `biases/`), measures every frame (focus,
elongation and trailing, sky level and noise, star counts, satellite trails), registers the
frames to measure the dithering (how often, how far, in which directions, where positions
repeat) and the drift, and lists the frames to leave out of the stack, each with its reason.
Two applications, a console (`lightscheck.exe`) and a WPF window, over one core library;
neither computes a number itself. It never modifies, moves or writes anything into the raws.

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build LightsCheck.sln -c Release --nologo`
- Test: `dotnet test LightsCheck.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test LightsCheck.sln --filter "FullyQualifiedName~R002"`
- Format: `dotnet format LightsCheck.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/LightsCheck.Cli -- <session folder>`
- Window: `dotnet run --project src/LightsCheck.Wpf -- [session folder]`
- Golden console texts, after an intended output change (the rule "Golden files"):
  `LIGHTSCHECK_UPDATE_GOLDEN=1 dotnet test LightsCheck.sln --filter GoldenOutputTests`
- Figures on the real sessions (minutes, not in the gate): `/validate`, or
  `dotnet test LightsCheck.sln -c Release --filter "Category=Validation"` with
  `LIGHTSCHECK_TESTDATA_LARGE` pointing at the folder that holds the sessions
- Decode of a real raw (gated, no raw is in the repo): `LIGHTSCHECK_NEF=<file.NEF> dotnet test LightsCheck.sln --filter RawReaderTests`
- Test data hashes after an intentional fixture change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of one core file: `scripts/mutate.ps1 -Mutate "**/File.cs"`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `src/LightsCheck.Core/` - everything that computes and everything that formats. No Console,
  no UI, no entry point: a compile boundary. `Imaging/` reads raws and FITS (copied from
  FlatCheck, ADR 0003), `Session/` finds the frames, `Analysis/` decides numbers, `Report/`
  decides words (Romanian), `Runs/` chains them; both applications call `Runs/` only.
- `src/LightsCheck.Cli/` - the console, `lightscheck.exe`: options, dispatch, exit code.
- `src/LightsCheck.Wpf/` - the window, `LightsCheck.Wpf.exe`: the theme and the small pieces
  copied from FlatCheck (`Theme/`, `Presentation/`), the view-models (`ViewModels/`), the XAML
  (`Views/`, `MainWindow.xaml`). It binds the core's report rows and computes nothing (ADR
  0008). `Icon/`, at the project root, holds the one icon of both executables, drawn per size
  by `make_icon.py`.
- `tests/LightsCheck.Tests/` - unit, acceptance (`AcceptanceTests`), golden texts
  (`GoldenOutputTests`, `Golden/`), figures (`ValidationTests`), repo rules
  (`RepoInvariantTests`, `LicenseHeaderTests`, `ThirdPartyTests`, `VersionTests`). Every frame
  is generated in the tests (`SessionFixture`, `FitsTestData`); `tests/testdata/` holds only
  the manifest. The real sessions stay outside git (`docs/validation.md`).
- `tests/LightsCheck.Wpf.Tests/` - the window's tests, Windows-only: view-models, the window
  rendered with real results and no binding error (`WindowSmokeTests`,
  `LIGHTSCHECK_DUMP_SCREENS=<folder>` saves a PNG per screen), culture, icon. It links the
  fixtures of `LightsCheck.Tests`.
- `prototype/` - the Python scripts of the 2026-10-09 analysis of the Veil session, frozen:
  the numeric oracle of the validation figures, neither maintained nor run by the gate
  (ADR 0003). `docs/validation/2026-10-06-veil/` holds their report and CSV.
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs.
  `docs/validation.md` + `.json` - figures on the real sessions. `docs/design/` - the
  explanations, per area, read on demand. `docs/plans/` - approved plans. `README.md` - the
  user documentation, Romanian.
- `kit.json` - what the kit's scripts, skills and agent need to know about LightsCheck.
  `docs/skills.md` - LightsCheck's additions to the kit's skills. `scripts/`, `.claude/skills/`,
  `.claude/agents/`, `.claude/rules/kit/` and the process keys of `.claude/settings.json` are
  the kit's, the same here (the kit's ADRs 0017, 0018 and 0028).
- The process rules are the kit's, in `.claude/rules/kit/process.md`; LightsCheck's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are LightsCheck's own; one that extends or
varies a kit rule names it.

- **Core has no UI.** `LightsCheck.Core` never references Console, WPF, file dialogs or
  anything with a UI.
- **Computed only in the core.** Nothing is computed outside `LightsCheck.Core`: a new number,
  threshold or sentence of diagnosis goes in `Analysis/`, `Thresholds.cs` or `Report/`, never
  in a view-model or the console.
- **Read-only input.** Nothing is ever written into the session folder's `lights/` or
  `biases/`; the raws are read, never modified, moved or renamed.
- **Golden files** (extends the kit's rule "No test edited to pass", the kit's ADR 0033). The
  golden files are the console snapshots `tests/LightsCheck.Tests/Golden/*.txt`, regenerated
  with `LIGHTSCHECK_UPDATE_GOLDEN=1` and `--filter GoldenOutputTests`. None carries confirmed
  figures; every changed line is read and tied to the task's change at wrap-up.
- **Package notices** (extends the kit's rule "Packages"). A package also needs an entry in
  `THIRD-PARTY-NOTICES.md` with its GPLv3 compatibility argued, and one in `ThirdParty.cs`
  (`ThirdPartyTests` enforces both). The FITS reader and the PNG encoder stay hand-rolled.
- **Measured on the sessions** (extends the kit's rule "Measured numbers"). Here the real data
  are the sessions listed in `docs/validation.md`; a formula change, a changed constant of
  `Thresholds.cs` included, re-runs `/validate` on them before the task ends, with the drift
  table pasted; a figure that moves changes only the owner's way (the kit's rule "No test
  edited to pass"), and an ADR that states the old value gets a dated note.
- **Languages** (varies the kit's rule "Languages", ADR 0005). English in everything the model
  reads or writes about the process: docs, tests, scripts, commits, CHANGELOG, code comments
  of new files. Romanian in everything the user sees: console text, `--help`, window labels,
  the reports, `README.md`, `THIRD-PARTY-NOTICES.md`. The files copied from FlatCheck under
  `Imaging/` keep its Romanian comments as they are (ADR 0003). Never leave a file in two
  languages.
- **Copied files stay copies** (ADR 0003). A file under `src/LightsCheck.Core/Imaging/` marked
  "copied from FlatCheck unchanged" is not improved here: a fix goes to FlatCheck first, then
  the copy is refreshed. Only `RawReader.cs` and `ImageLoader.cs`, marked "adapted", are
  LightsCheck's to change.
- **License headers.** Every source file (`.cs`, `.xaml`, `.py`) starts with the GPL notice;
  `LicenseHeaderTests` fails otherwise. Scripts carry the SPDX line.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. LightsCheck adds:

- A release is tagged `lightscheck-vX.Y.Z`.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.
- The plan of the whole build, step by step, is `docs/plans/2026-10-09-build-lightscheck.md`;
  each step is a task on its own branch, built on the previous step's branch.

## Read before touching

`docs/design/README.md` maps every area to its note. Read the note before editing that area.

## Style

- `.editorconfig` and the analyzers enforce style at build time; do not restate style here.
  The copied folder `src/LightsCheck.Core/Imaging/` keeps FlatCheck's style and nullable off,
  by a section of `.editorconfig` and a `#nullable disable` line per file (ADR 0003).
- Nullable enabled, warnings are errors, `AnalysisLevel` latest-recommended. Fix, do not
  suppress, unless the suppression carries a comment with the reason.
