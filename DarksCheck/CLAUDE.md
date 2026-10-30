# DarksCheck

Quality check for the dark frames of a DSLR astrophotography session (the author's Nikon
D3300, Siril workflow). It reads a Siril session folder (`darks/` required, `biases/` and
`lights/` optional) or a bare folder of darks (a library), repeats the nine checks of the hand
analysis of the Smida session of 2026-08-12 (metadata and cadence, light leaks, thermal drift,
noise, hot pixels, fixed pattern, dead pixels, how many darks, the lights against the dark),
and computes the index: a number for how warm the sensor ran, on darks, on a library and on
lights, so a library master can be matched to a session, with the label the owner puts in the
folder name. Two applications, a console (`darkscheck.exe`) and a WPF window
(`DarksCheck.Wpf.exe`), over one core library; neither computes a number itself. It never
modifies, moves or writes anything into the frames' folders.

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build DarksCheck.sln -c Release --nologo`
- Test: `dotnet test DarksCheck.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test DarksCheck.sln --filter "FullyQualifiedName~R002"`
- Format: `dotnet format DarksCheck.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/DarksCheck.Cli -- <session or darks folder>`
- Golden console texts, after an intended output change (the rule "Golden files"):
  `DARKSCHECK_UPDATE_GOLDEN=1 dotnet test DarksCheck.sln --filter GoldenOutputTests`
- Figures on the real sessions (not in the gate): `/validate`, or
  `dotnet test DarksCheck.sln -c Release --filter "Category=Validation"` with
  `DARKSCHECK_TESTDATA_LARGE` pointing at the folder that holds the sessions
- Decode of a real raw (gated, no raw is in the repo): `DARKSCHECK_NEF=<file.NEF> dotnet test DarksCheck.sln --filter RawReaderTests`
- Test data hashes after an intentional fixture change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of one core file: `scripts/mutate.ps1 -Mutate "**/File.cs"`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `src/DarksCheck.Core/` - everything that computes and everything that formats. No Console,
  no UI, no entry point: a compile boundary. `Imaging/` reads raws and FITS (copied from
  LightsCheck, which copied FlatCheck, ADR 0003), `Session/` finds the frames, `Analysis/`
  decides numbers, `Thresholds.cs` holds every constant a verdict depends on, `Report/`
  decides words (English and Romanian; `Language/` holds the pick), `Runs/` chains them; both
  applications call `Runs/` only, and start and close the diagnostic log of `Logging/`, which
  the seams write (ADR 0025).
- `src/DarksCheck.Cli/` - the console, `darkscheck.exe`: options, dispatch, exit code.
- `src/DarksCheck.Wpf/` - the window, `DarksCheck.Wpf.exe`: the theme and presentation pieces
  copied from LightsCheck, one view-model fed by the report's rows, the views (ADR 0009).
  `Icon/` - the one icon of both executables and the script that draws it.
- `tests/DarksCheck.Wpf.Tests/` - the window's tests (view-model, rendering of every tab,
  culture, icon), a project of their own since they need `net8.0-windows`.
- `tests/DarksCheck.Tests/` - unit, acceptance (`AcceptanceTests`), golden texts
  (`GoldenOutputTests`, `Golden/`), figures (`ValidationTests`), repo rules
  (`RepoInvariantTests`, `LicenseHeaderTests`, `ThirdPartyTests`, `VersionTests`). Every frame
  is generated in the tests (`SessionFixture`, `FitsTestData`, `SyntheticFrame`);
  `tests/testdata/` holds only the manifest and its README. The real sessions stay outside git
  (`docs/validation.md`).
- `prototype/` - the Python scripts of the 2026-10-11 analysis of the Smida session, frozen:
  the numeric oracle of the validation figures, neither maintained nor run by the gate
  (ADR 0003). `docs/validation/2026-08-12-smida/` holds their report, JSON, CSV and figures.
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs.
  `docs/validation.md` + `.json` - figures on the real sessions. `docs/design/` - the
  explanations, per area, read on demand. `docs/plans/` - approved plans. `README.md` - the
  user documentation, English (ADR 0022).
- `kit.json` - what the kit's scripts, skills and agent need to know about DarksCheck.
  `docs/skills.md` - DarksCheck's additions to the kit's skills. `scripts/`, `.claude/skills/`,
  `.claude/agents/`, `.claude/rules/kit/` and the process keys of `.claude/settings.json` are
  the kit's, the same here (the kit's ADRs 0017, 0018 and 0028).
- The process rules are the kit's, in `.claude/rules/kit/process.md`; DarksCheck's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are DarksCheck's own; one that extends or
varies a kit rule names it.

- **Core has no UI** (the kit's ADR 0002). `DarksCheck.Core` never references Console, WPF,
  file dialogs or anything with a UI.
- **Computed only in the core.** Nothing is computed outside `DarksCheck.Core`: a new number,
  threshold or sentence of diagnosis goes in `Analysis/`, `Thresholds.cs` or `Report/`, never
  in a view-model or the console. A diagnostic log line is no figure of the analysis: the
  applications write and time their own lines (ADR 0025).
- **Read-only input.** Nothing is ever written into the session folder or the library folder:
  the frames are read, never modified, moved or renamed; the label is shown, never written
  (ADR 0002).
- **Golden files** (extends the kit's rule "No test edited to pass", the kit's ADR 0033). The
  golden files are the console snapshots `tests/DarksCheck.Tests/Golden/*.txt`, regenerated
  with `DARKSCHECK_UPDATE_GOLDEN=1` and `--filter GoldenOutputTests`. None carries confirmed
  figures; every changed line is read and tied to the task's change at wrap-up.
- **Package notices** (extends the kit's rule "Packages"). A package also needs an entry in
  `THIRD-PARTY-NOTICES.md` with its GPLv3 compatibility argued, and one in `ThirdParty.cs`
  (`ThirdPartyTests` enforces both). The FITS reader and the PNG encoder stay hand-rolled.
- **Measured on the sessions** (extends the kit's rule "Measured numbers"). Here the real data
  are the sessions listed in `docs/validation.md`; a formula change, a changed constant of
  `Thresholds.cs` included, re-runs `/validate` on them before the task ends, with the drift
  table pasted; a figure that moves changes only the owner's way (the kit's rule "No test
  edited to pass"), and an ADR that states the old value gets a dated note.
- **Languages** (varies the kit's rule "Languages", ADRs 0005, 0021 and 0022). English in
  everything the model reads or writes: docs, tests, scripts, commits, CHANGELOG, `README.md`
  and `THIRD-PARTY-NOTICES.md` (ADR 0022), and every comment in every file, the copied ones
  included (ADR 0019). What the user sees in the console and the window (texts, `--help`,
  labels, the reports) is in English and Romanian, chosen by `--lang`: each text a
  `T("english", "romana")` pair where it is composed, a `{l:T En='...', Ro='...'}` pair in XAML,
  the Romanian without diacritics (ADR 0021). Those Romanian wordings, and the tests and docs
  that quote them, are the one exception; never leave a file in two languages otherwise.
- **Copied files stay copies** (ADR 0003). A file under `src/DarksCheck.Core/Imaging/` is
  not improved here: a fix goes to its source (LightsCheck, or FlatCheck through it) first,
  then the copy is refreshed. `RawReader.cs` and `ImageLoader.cs` carry LightsCheck's
  adaptations and are refreshed from LightsCheck. Two exceptions: the comments, which are
  DarksCheck's own and describe what the files do here (ADR 0019), and the English-or-Romanian
  pairs of the messages (ADR 0021); a refresh takes the code and keeps both.
- **License headers.** Every source file (`.cs`, `.xaml`, `.py`) starts with the GPL notice;
  `LicenseHeaderTests` fails otherwise. Scripts carry the SPDX line.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. DarksCheck adds:

- A release is tagged `darkscheck-vX.Y.Z`.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.
- The plan of the whole build, step by step, is `docs/plans/2026-10-11-build-darkscheck.md`;
  each step is a task on its own branch, built on the previous step's branch.

## Read before touching

`docs/design/README.md` maps every area to its note. Read the note before editing that area.

## Style

- `.editorconfig` and the analyzers enforce style at build time; do not restate style here.
  The copied folder `src/DarksCheck.Core/Imaging/` keeps FlatCheck's style and nullable off,
  by a section of `.editorconfig` and a `#nullable disable` line per file (ADR 0003).
- Nullable enabled, warnings are errors, `AnalysisLevel` latest-recommended. Fix, do not
  suppress, unless the suppression carries a comment with the reason.
