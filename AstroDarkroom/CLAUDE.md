# AstroDarkroom

Astro Darkroom is the launcher and the release of the six astrophotography tools of the `photo`
repository: DarksCheck, FlatCheck, LightsCheck, ImageVisualCompare, CopyWcs (folder `WCS_Copy`)
and AstroPhotoAnalyzer. The launcher is a small WPF window that lists the six, says which is in
the suite folder beside it and starts the one the user picks; it computes nothing of its own.
Its `<Version>` is the suite's version and its `/release` publishes the six components' windows
and the launcher into one flat self-contained folder, zipped; the zip is shared from the owner's
Google Drive, its link and SHA-256 in `README.md` (ADR 0005) and, for the latest release, in the
repository root's `README.md`, the Bitbucket front page (ADR 0008), and the consoles stay in the
components' own releases (ADR 0002). It is for the owner and the few people who receive the
zip; it is not a seventh analysis tool and never changes a component.

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build AstroDarkroom.sln -c Release --nologo`
- Test: `dotnet test AstroDarkroom.sln -c Release --nologo`
- One test: `dotnet test AstroDarkroom.sln --filter "FullyQualifiedName~R001"`
- Format: `dotnet format AstroDarkroom.sln` (check only: `--verify-no-changes`)
- Window: `dotnet run --project src/AstroDarkroom.Wpf` (from the build folder every app is
  "missing"; the launcher is meant to run from a suite folder, `artifacts/<version>/`). Its log
  is `log\AstroDarkroom.log` beside the executable; another level for one run:
  `$env:ASTRODARKROOM_LOG = 'trace'` before it, in PowerShell
- Test data hashes after an intentional fixture change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of Core (minutes, not in the gate): `scripts/mutate.ps1 [-Mutate "**/File.cs"]`
- Release of the suite (verify, version, CHANGELOG, the six windows and the launcher
  published into `artifacts/<version>/`, `release.json`, the zip and its `.sha256` in
  `artifacts/`; no commit, no tag): `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`; the publish
  alone again, from any branch: `-PublishOnly`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `src/AstroDarkroom.Core/` - the catalogue of the apps, where each is in the suite folder,
  the plan of a start, the reading of `release.json`, the version, the licence, the
  prerequisites texts and the third-party list, and the diagnostic log's configuration and
  words (`Logging/`, ADR 0012). No Console, no UI, no entry point: a compile boundary.
- `src/AstroDarkroom.Wpf/` - the window, `AstroDarkroom.exe`: one row per app with its button,
  and the "Despre" window. A thin adapter over the core (ADR 0001); it has no test project. Its
  `App` starts and closes the diagnostic log.
- `tests/AstroDarkroom.Tests/` - the requirement tests (`R###_...`), the suite checks
  (`SuiteTests`: the catalogue against `kit.json`, the components' frameworks and packages and
  the launcher's against theirs, the icon copies against the components' files, R-010),
  repo rules (`RepoInvariantTests`, `LicenseHeaderTests`, `VersionTests`), the log's tests
  (`LoggingTests`; `LogCapture` configures the log at Trace for the whole assembly). It
  targets `net8.0-windows`, the window's framework (ADR 0012). `tests/testdata/` holds only the
  manifest.
- `THIRD-PARTY-NOTICES.md` - the licence of each component written by others that a program of
  the suite folder ships, whole, with its copyright notices and why it is compatible with the
  GPLv3; the launcher publishes it as `THIRD-PARTY-NOTICES.txt`, beside the .NET runtime's two
  texts (R-017, ADRs 0013 and 0014). `SuiteTests` binds its sections to the windows' lock files.
- `Icon/` - the launcher's icon, drawn per size by `make_icon.py`, and a copy of each
  component's icon, shown beside its row and kept equal to the component's file by
  `SuiteTests` (ADR 0011); never drawn here.
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs.
  `docs/validation.md` + `.json` - no measured figures: the launcher computes nothing.
  `docs/design/` - the design notes, read on demand. `docs/plans/` - approved plans; the plan
  of the whole suite is the kit's `docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`.
  `README.md` - the user documentation, English (ADR 0009).
- `kit.json` - what the kit's scripts, skills and agent need to know about AstroDarkroom,
  the suite's publish list included. `docs/skills.md` - AstroDarkroom's additions to the kit's
  skills. `scripts/`, `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process
  keys of `.claude/settings.json` are the kit's, the same here (the kit's ADRs 0017, 0018 and
  0028).
- The process rules are the kit's, in `.claude/rules/kit/process.md`; AstroDarkroom's own are
  in this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are AstroDarkroom's own; one that extends or
varies a kit rule names it.

- **Core has no UI.** `AstroDarkroom.Core` never references Console, WPF, file dialogs or
  anything with a UI; it plans a start, the window executes it.
- **The catalogue and kit.json agree.** Every app in `AppCatalogue` is a component of
  `release.publish` in `kit.json` (one entry whose `project` starts with `../`, its window
  project) and every component is in the catalogue, under the exe name its project file
  gives; `SuiteTests` checks it, and refuses a console entry (ADR 0002). A new app in the
  suite is one entry, one catalogue line, its icon copied into `Icon/` and that test run.
- **Two interface languages, English everywhere else** (varies the kit's rule "Languages",
  ADRs 0003, 0006 and 0009). Every text the user sees in the windows is a pair
  `T("english", "romana")` where it is composed, the Romanian without diacritics, never a single
  literal; the only single literals are the product name `Astro Darkroom`, the language
  selector's own labels (each language named in itself), symbols such as `?`, and a third-party
  component's name, licence identifier, copyright line and address (ADR 0013). The window
  switches between the two live and remembers the choice. The diagnostic log is English
  whatever the language: no log line goes through `T` (R-012, `LoggingTests`). `README.md`,
  code, comments, tests, docs, commits and the CHANGELOG are English; the Romanian wordings and
  the tests and docs that quote them are the one exception. Never leave a file in two languages
  otherwise.
- **Same .NET and same packages across the suite.** Every component targets .NET 8 and a
  package two components share resolves to one version, the launcher's included, so the suite
  folder holds one copy of each DLL: a package the launcher shares with a component takes the
  component's version. `SuiteTests` reads the components' lock files and the launcher's. A
  component that drifts is fixed in that component, never worked around here.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. AstroDarkroom adds:

- A release is tagged `astrodarkroom-vX.Y.Z`; it is the suite's release, cut from `main` with
  every component's folder clean. The components' own `/release` entries are unchanged.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.

## Read before touching

`docs/design/README.md` maps every area to its note. Read `docs/design/launcher.md` before
editing the core or the window, and `docs/design/logging.md` before touching `Logging/`,
`App.xaml.cs` or a log line.

## Style

- `.editorconfig` and the analyzers enforce style at build time; do not restate style here.
- Nullable enabled, warnings are errors, `AnalysisLevel` latest-recommended. Fix, do not
  suppress, unless the suppression carries a comment with the reason.
