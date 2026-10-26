# 2026-10-26 Adopt the kit and build the WPF window

Status: **proposed**, waiting for the owner's answers to the questions under "Decisions put to
the owner" and for the approval of the plan. Written on 2026-10-26 in the main checkout
(`main` at 8bf8320; this project's last commit is d3ccd6c) for implementing sessions that are
**not** the one that wrote it. Everything they need is in this file or in a file it names.
The file is moved into the first task's worktree before its first step, and the second task
reads it from `main` after the first is merged. Each task fills its own "What deviated" at its
wrap-up.

The owner's request (2026-10-26, in Romanian): the functionality of this console application
in a WPF application, dark theme, an About window, built on the kit, the interface in English
and Romanian chosen by `--lang` on the command line exactly as FlatCheck does, and a plan first.

## Goal

Two tasks, two branches, in this order:

1. **Adopt the kit** (`ClaudeCodeStarterKit`, `GHID.md` "Migrarea unui proiect existent") in one
   session: the gate, the layout `src/` and `tests/`, a core library without any console
   (`CopyWcs.Core`) under the console (`CopyWcs.Cli`), English comments and documentation
   comments, the two interface languages in the console (`--lang en|ro`, English when absent),
   and the documentation the process wants: requirements with IDs, ADRs, design notes, one
   validation case on a real Siril plate solve, `CLAUDE.md` cut to the project's rules.
2. **Build the window** (`CopyWcs.Wpf`) in a second session: one dark window that picks the
   two files, previews the cards the console's `--dry-run` would list, copies them, shows the
   same warnings and refusals in the language `--lang` chose, with an About window (the icon,
   the banner, the copyright, the GPLv3 warranty) and a dark title bar. The window computes
   nothing of its own: it calls the core's run and shows its words, as DarksCheck's window does
   (DarksCheck ADR 0009).

Out of scope, named so nobody widens the work: adding CopyWcs to the Astro Darkroom suite (one
`release.publish` entry and one catalogue line there; a task of the owner's in `AstroDarkroom`
once the window exists), a live language switch in the window (AstroDarkroom has one; the owner
asked for the command line, as FlatCheck), a version bump (the owner's), and any change to
what the tool copies or how it writes a FITS.

## Read first (each implementing session, before any edit)

1. `../ClaudeCodeStarterKit/.claude/rules/kit/process.md`: the rules the project follows from
   the first commit, cited by name in quotes.
2. `../ClaudeCodeStarterKit/GHID.md`, sections "Cum se foloseste kit-ul" and "Migrarea unui
   proiect existent" (Romanian; the layers below follow them).
3. `../DarksCheck/CLAUDE.md`, `kit.json`, `Directory.Build.props`, `Directory.Packages.props`,
   `global.json`, `.editorconfig`, `docs/requirements.md` (header and row format),
   `docs/design/README.md` (the index and the Covers rule), `docs/decisions/0001-*.md` and
   `0021-*.md`, `tests/DarksCheck.Tests/RepoInvariantTests.cs`, `VersionTests.cs`,
   `LicenseHeaderTests.cs`, `RomanianByDefault.cs`, `LanguageTests.cs`: the closest sibling,
   a console and a window over one core on .NET 8, two languages, the same owner's wording.
4. `../FlatCheck/FlatCheck.Core/Language/Ui.cs` and `LanguageArgument.cs` (the language
   mechanism to copy, API unchanged), `../FlatCheck/FlatCheck.Wpf/App.xaml.cs` (how the window
   reads `--lang` before its first view), `../DarksCheck/src/DarksCheck.Cli/Program.cs` lines
   50 to 70 (the console's `--lang` flow) and its `PrintUsage` (the usage written whole per
   language).
5. For task 2 only: `../AstroDarkroom/src/AstroDarkroom.Wpf/App.xaml` (the smallest dark
   palette and the button style), `AboutWindow.xaml` and `.cs`, `DarkTitleBar.cs`;
   `../DarksCheck/src/DarksCheck.Wpf/Views/AboutWindow.xaml`, `ViewModels/AboutViewModel.cs`,
   `Presentation/TExtension.cs`, `Presentation/Mvvm.cs`, `App.xaml.cs`;
   `../DarksCheck/tests/DarksCheck.Wpf.Tests/` (`StaFixture.cs`, `WindowFixture.cs`,
   `AboutTests.cs`, `WindowSmokeTests.cs`, `IconTests.cs`, the csproj); `../DarksCheck/Icon/make_icon.py`.
6. `../ClaudeCodeStarterKit/scripts/kit-config.ps1` (every `kit.json` key),
   `check-decisions.ps1` header (the ADR header format the gate checks), `check-design.ps1`
   header (the Covers grammar), `check-testdata.ps1` header (the manifest), `release.ps1`
   header (it reads `<Version>` from `Directory.Build.props`).
7. This project's `CLAUDE.md`, the two csproj files, `copy_wcs.py` (prior art, not maintained)
   and every `.cs` file (2016 lines with the licence headers; the requirement rows are written
   from them).

## Measured before planning (2026-10-26, main at 8bf8320)

| | CopyWcs today |
| --- | --- |
| layout | `CopyWcs/` (the console, `copywcs.exe`, 7 files, 582 lines) and `CopyWcs.Tests/` (9 files, 1434 lines) at the root, `CopyWcs.sln`, `copy_wcs.py`, `LICENSE`, `CLAUDE.md`; no `docs/`, no `README.md`, no `Directory.Build.props`, no `global.json`, no `.editorconfig` |
| tests | `dotnet test`: 87 passed, 0.26 s; xunit 2.5.3, `Microsoft.NET.Test.Sdk` 17.8.0, `coverlet.collector` 6.0.0, versions in the test csproj, no lock files |
| SDK | no `global.json`; `dotnet --version` here says 10.0.403; the siblings pin 9.0.318 (`rollForward latestPatch`), which builds net8.0 |
| build under the kit's strictness | a scratch copy with DarksCheck's `Directory.Build.props` (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel latest-recommended`, `GenerateDocumentationFile`) and its `.editorconfig`, warnings not as errors: the build log lists each finding twice (the two compile passes); halved: **app code 20 findings, all IDE0011** (braces on one-line `if`s: `FitsHeader` 12, `CopyWcsCli` 3, `Options` 3, `VersionInfo` 1, `WcsMetadataKeys` 1); **tests 101**: CA1707 62 (underscores in test names), CA1861 19 (constant arrays in `InlineData` and assertions), IDE0011 10, CA1305 8 (`ToString` without a culture in `FitsTestData` and `FitsHeaderTests`), CA1310 2 (`StartsWith` without a `StringComparison`). Re-measure in layer 1 and record the counts in ADR 0002 |
| `dotnet format --verify-no-changes` with DarksCheck's `.editorconfig` | IDE0011 only; no whitespace findings |
| user-visible texts | all Romanian without diacritics: the usage (10 lines), the warranty paragraph, 5 parse errors, 10 run messages, 2 reader errors, the HISTORY card `WCS copiat din <file>`; the full list with its English is in Appendix C |
| comments | Romanian in all 16 `.cs` files (file headers, XML docs, line comments) and in the csproj; test assertion messages Romanian; the kit's rule "Languages" wants English |
| documentation comments | some types and methods have a `<summary>`, most do not; the rule "Documentation comments" wants one on every type and method |
| version | `<Version>1.0.0</Version>`, `<Copyright>`, `<PackageLicenseExpression>` in `CopyWcs/CopyWcs.csproj`; `VersionTests` and `LicenseNoticeTests` read that file; the kit's `release.ps1` reads `Directory.Build.props` |
| `CLAUDE.md` | 101 lines: structure, conventions (Romanian texts, raw card copied verbatim, `Main` a one-line shim, never bump the version, every file starts with the GPL notice), build and test, versioning, licensing |
| test data | none on disk: `FitsTestData` builds every FITS in memory; `TempWorkspace` writes them to a temp folder |
| a real plate solve for validation | Siril 1.4.4 solves of the Veil session of 2026-10-06, `E:\Photografie\Astro\Veil\2026-10-06-3h\process\pp_light_000NN.fit` (DitherCalculator's `CLAUDE.md`, "Measured on the Veil session: plate solves of 2026-10-12"); a stretched plate-solved reference in `E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida\...\starless_rbf0.5_stretch0.17.fit`. The header alone (every 2880-byte block up to the one holding END) is a few KB: a fixture in the repo, not a large file |
| git hooks | `core.hooksPath` is the absolute `E:/Photografie/Code/.githooks`; the pre-commit hook runs `scripts/verify.ps1` of every touched project that has one, so the gate is live from the commit that adds `scripts/` |
| Claude memory | no note about this project |

## Decisions put to the owner (answer before the first task starts)

Each comes with a recommendation (the kit's rule "Questions to the owner"); the first option
is the recommended one. An answer becomes an ADR of task 1 (the numbers below).

- **Q1. Layout and names.** Move to the kit's `src/` and `tests/`: `src/CopyWcs.Core` (the
  FITS engine, the keys, the run, the texts, the version and licence plumbing; no Console),
  `src/CopyWcs.Cli` (`copywcs.exe`, kept: options, dispatch, exit codes), later
  `src/CopyWcs.Wpf` (`CopyWcs.Wpf.exe`, the siblings' naming), `tests/CopyWcs.Tests`, later
  `tests/CopyWcs.Wpf.Tests`; `copy_wcs.py` to `prototype/copy_wcs.py` as DarksCheck keeps
  its Python. Alternative: keep the two folders at the root as FlatCheck does and list them in
  `kit.json`. Recommended: move; the project is 16 files and `git mv` keeps the history.
  (ADR 0001)
- **Q2. English is the default language of the console too.** Every sibling (FlatCheck ADR
  0062, DarksCheck ADR 0021, LightsCheck ADR 0022, AstroPhotoAnalyzer ADR 0048) made the
  console English by default and Romanian under `--lang ro`; the window must read `--lang`
  anyway, and one language mechanism serves both. The 87 tests keep asserting Romanian through
  a module initializer that sets the test assembly's default to Romanian (DarksCheck's
  `RomanianByDefault.cs`), so no test wording changes. Alternative: the console stays Romanian
  and only the window takes `--lang`, which would be the one tool in the folder whose console
  differs from its window. (ADR 0004)
- **Q3. The HISTORY card.** The tool writes `HISTORY WCS copiat din <source file>` into the
  destination. A header card is data read by other programs, not interface text: write
  `WCS copied from <source file>` in both languages, as DarksCheck's `--out` names are English
  in both. Alternatives: a card per language; or leave the Romanian card. (ADR 0004)
- **Q4. The braces rule.** Take the kit's full `.editorconfig`, `csharp_prefer_braces` included,
  and fix the 30 one-line `if`s in the format commit (ImageVisualCompare declined at 38 findings
  because a style pass over its code base was a task of its own; here it is one commit over
  five files). Alternative: the whitespace rules only, as ImageVisualCompare. (ADR 0002)
- **Q5. Test packages.** `Directory.Packages.props` with the kit's current versions
  (`Microsoft.NET.Test.Sdk` 18.10.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5, the
  ones DarksCheck has), lock files, and **`coverlet.collector` dropped**: nothing reads a
  coverage file and the kit has no coverage step. Alternative: keep coverlet in the props with
  a "why". (ADR 0002)
- **Q6. The validation fixture.** One real Siril-solved header in the repo, in the gate:
  the owner names one solved file (recommended: a `pp_light_000NN.fit` of the Veil session,
  Siril 1.4.4, one of the four solves DitherCalculator records); the implementing session
  takes its header blocks only (up to and including the block that holds END), checks that no
  card gives away the observing site (`SITELAT`, `SITELONG`, `SITEELEV`; a DSLR light solved
  by Siril normally carries none, and the fixture is refused if it does, the owner then picks
  another file or blanks nothing: an edited header is not a real one), hashes it in the
  manifest, and measures V-001: the keys the tool selects from it, with and without the
  optical ones, their count and their list. Alternative: no validation case, as AstroDarkroom
  ("computes nothing"); but this tool's whole value is which cards a real solve yields, and
  the in-memory fixtures of the tests are hand-written. (ADR 0005)
- **Q7. `README.md`.** The project has none; the kit expects the user documentation there.
  Recommended: English, since the interface defaults to English and the repository's front
  page (`Code/README.md`, AstroDarkroom ADR 0008 and 0009) is English; a short file: what the
  tool does, the console's options, the window, `--lang`, the flip warning, the licence.
  Alternative: Romanian, as DarksCheck's and FlatCheck's are. (ADR 0006)
- **Q8. The icon.** Every sibling has `Icon/make_icon.py` (Pillow, run once, the `.ico`
  committed) drawing each size on its own. Proposed drawing: a dark frame with a few stars and
  a thin celestial grid (the solve) and a short arrow to a second, empty frame; the small sizes
  keep the grid and the arrow only. The owner may describe another drawing or supply an `.ico`.
  (Task 2, ADR 0007)
- **Q9. The window's command line.** Besides `--lang`, the first two arguments that are
  existing files fill the source and the destination boxes (FlatCheck's window takes a folder
  the same way); anything else is ignored. Recommended yes; it is twenty lines and a test.
  (Task 2, R-034)
- **Q10. Comments and documentation comments now.** Translate every comment to English and
  add the missing `<summary>`, `<param>`, `<returns>` in task 1, as commits of their own, one
  per project. The siblings did their documentation comments in tasks of their own because
  they are tens of files; here it is 16, and the Core/Cli split touches most of them anyway.
  Alternative: translate only what the split touches and leave the rest to a later task
  (the rule allows it). (ADR 0004 records the languages, the plan records the choice)

Assumed, stated and not asked (each matches the owner's earlier answers in the siblings):

- Both wordings sit where the text is composed, `T("english", "romana")`, Romanian without
  diacritics (FlatCheck ADR 0062; the whole folder does this).
- A wrong `--lang` value: the console prints an English `ERROR:` line and exits 1; the window
  shows an English message once it is up and goes on in English (DarksCheck R-092, R-094).
- Numbers never change with the language; no culture is set; the console keeps
  `InvariantGlobalization`, the window pins the invariant culture by hand (FlatCheck ADR 0037).
- .NET 8 stays (the suite's rule "Same .NET across the suite" in AstroDarkroom, should the tool
  join it); `global.json` pins SDK 9.0.318 like the siblings.
- The version stays 1.0.0 and the copyright year 2026 (the kit's rule "Version and year are
  the owner's"); a release is the owner's, from `main`.
- The console's Romanian output is byte-identical to today's except for `--help`, which gains
  its `--lang` line, and the HISTORY card if Q3 is answered as recommended.

## Task 1: adopt the kit, split the core from the console, two languages in the console

Branch `claude/Adopt-the-kit-and-split-the-core`, worktree
`.claude/worktrees/Adopt-the-kit-and-split-the-core` (under 41 characters, the Windows path
limit the DarksCheck ADR names taught), from `main`. One session; about six commits, each with
the gate green. The git pre-commit hook refuses a session's commit in the main checkout, so
nothing happens there.

### Step 0: the worktree and the plan

`git worktree add .claude/worktrees/Adopt-the-kit-and-split-the-core -b claude/Adopt-the-kit-and-split-the-core main`
from `E:\Photografie\Code\WCS_Copy`; move this file into the worktree as
`docs/plans/2026-10-26-adopt-the-kit-and-build-the-window.md` (it is uncommitted on `main`,
so it is a plain move, nothing to revert).

### Commit 1: the gate and the layout (layer 1 of the guide)

The kit's files, copied byte for byte from `../ClaudeCodeStarterKit` (the rule "Shared
folders"; `scripts/sync.ps1 -Check` must say "no difference" afterwards): `scripts/` without
`rename.ps1`, `install-hooks.ps1`, `hooks/pre-commit`, `hooks/pre-merge-commit`,
`hooks/commit-msg`; `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/`;
`.claude/settings.json` with the process keys and the `allow` entries DarksCheck has (its
`$comment` reworded for CopyWcs). Not copied: `monorepo/`, `bitbucket-pipelines.yml`,
`.github/` (no CI here, as the siblings), `.config/dotnet-tools.json` and `stryker-config.json`
(no mutation testing until someone asks; `mutate.testProject` still names the test project so
the key is valid).

The project's own files, new: `kit.json` (Appendix B), `docs/skills.md` (the kit's empty
sections), `CHANGELOG.md` (`[Unreleased]` with the Internal lines of this task, then `[1.0.0]`
for the three existing commits: the tool, its tests, the licensing and versioning),
`global.json` (SDK 9.0.318, `rollForward latestPatch`), `Directory.Build.props` (DarksCheck's,
with `<Version>1.0.0</Version>`, `<Copyright>`, `<Authors>`, `<PackageLicenseExpression>`
moved in from the csproj; `RuntimeIdentifiers win-x64` for the projects that ship, the kit's
ADR 0034), `Directory.Packages.props` (Q5), `.editorconfig` (Q4), `tests/testdata/manifest.json`
with no files and `tests/testdata/README.md`, the `docs/` skeleton (`requirements.md` with the
header and empty area tables, `decisions/0000-template.md` from the kit, `design/README.md`
with the Covers rule and an empty index, `validation.md` and `validation.json` with no case,
`plans/README.md`).

The move (Q1): `git mv CopyWcs src/CopyWcs.Cli`, `git mv CopyWcs.Tests tests/CopyWcs.Tests`,
`git mv copy_wcs.py prototype/copy_wcs.py`, the solution's paths updated, a new
`src/CopyWcs.Core/CopyWcs.Core.csproj` (`net8.0`, no output type, `InternalsVisibleTo` the two
test projects and the console) that takes `FitsHeader.cs`, `WcsMetadataKeys.cs`,
`VersionInfo.cs`, `LicenseInfo.cs` by `git mv`; the console keeps `Program.cs`, `CopyWcsCli.cs`,
`Options.cs` and references the core. The csproj files shrink to what is theirs (output type,
assembly name, `InvariantGlobalization`, references); the version and copyright comments move
with the elements into `Directory.Build.props`, translated.

The strictness: fix the 20 app findings and the test findings (Q4, Q5); CA1707 and CA1305 are
silenced in the test csproj with the comment DarksCheck's test csproj carries (underscores in
test names are the convention; `FitsTestData` formats cards without a culture under
`InvariantGlobalization`), CA1861 fixed by `static readonly` arrays, CA1310 by a
`StringComparison`. One `dotnet format` pass. Record the counts before and after in ADR 0002.

The tests follow the move: `LicenseHeaderTests` walks `src/`, `tests/` and `prototype/`
(and, from task 2, finds the `.xaml` files too, as DarksCheck's does); `VersionTests` and
`LicenseNoticeTests` read `Directory.Build.props` instead of the csproj (`TheVersionComesFromTheBuildProps`,
`TheCopyrightComesFromTheBuildProps`, `TheBuildPropsDeclareTheLicence`, the same assertions);
`RepoDir()` climbs one level more.

`scripts/verify.ps1` green (step 0 says "no difference"), 87 tests, commit:
`Adopt the kit's gate and layout: src and tests, the core split out, the version in the props`.

### Commit 2: the run moves into the core

`CopyWcsCli.Run` today reads both files, checks the geometry, selects the keys, lists or
copies them, writes, and prints as it goes. The window needs the same sequence without the
printing, so it moves into the core as `src/CopyWcs.Core/WcsCopy.cs`:

- `WcsCopyRequest(string Source, string Dest, string? Output, bool IncludeOptical, bool Force, bool DryRun)`.
- `WcsCopyOutcome`: `Kind` (`Copied`, `Previewed`, `Refused`), `Error` (the one line of a
  refusal, null otherwise), `Warnings` (the lines that begin with `WARNING:` today, in order),
  `Cards` (the raw 80-character cards selected, in the source's order; what `--dry-run`
  prints), `Target` (the file written, null otherwise), `Notes` (the closing lines: the count
  copied, the flip reminder).
- `WcsCopy.Run(WcsCopyRequest)`: the sequence of `CopyWcsCli.Run` from the reads to the write,
  every message composed exactly as today (Romanian), returned, never printed; the reads'
  exceptions become the refusal line with the same wording. Nothing in the core references
  `Console` or a `TextWriter`: a compile boundary, the project rule "Core has no UI".
- `CopyWcsCli.Run` becomes: banner, copyright, options, `WcsCopy.Run`, print the outcome's
  lines on stdout and stderr exactly where they go today, return 0, 1 or 2 as today. The 87
  tests prove the output is unchanged; the acceptance tests stay on `CopyWcsCli.Run` and new
  unit tests cover the outcome (`WcsCopyTests`: the kinds, the order of warnings, a dry run
  that writes nothing).

Commit: `Move the copy run into the core and leave the console to print its outcome`.

### Commit 3: the two languages in the console

`src/CopyWcs.Core/Language/Ui.cs` and `LanguageArgument.cs`: FlatCheck's two files, in this
project's style (file-scoped namespace, nullable on), the API unchanged (`UiLanguage`,
`Ui.T(en, ro)` with `using static`, `Ui.Current`, `Ui.Default`, `Ui.Use(lang)`,
`LanguageArgument.Extract`, `BadValueMessage`). Every user-visible literal of Appendix C
becomes a pair at its use; the usage is written whole per language (its columns are aligned by
hand) and gains `--lang en|ro`; the warranty paragraph takes the standard English wording.
`CopyWcsCli.Run`: banner, copyright, then `LanguageArgument.Extract`; a bad value is
`ERROR: ` + `BadValueMessage` on stderr and exit 1; the rest runs inside `Ui.Use(chosen)` or
under `Ui.Default` when no `--lang` was given. The HISTORY card per Q3.

`tests/CopyWcs.Tests/RomanianByDefault.cs` (a module initializer, `Ui.Default = Romanian`), so
every existing assertion stands; `LanguageTests` (R-020 to R-023): the forms of `--lang`, the
last one counting, case, English when absent (`Ui.StartupDefault`), a bad value refused with
exit 1 after the banner, and an English run of every scenario of `CopyWcsCliTests` with the
same exit code, the same numbers and no Romanian word from a short list (`ATENTIE`, `Nu pot`,
`Chei`, `Copiate`, `Dimensiuni`, `Nicio`, `Eroare`, `sursa`, `destinatie`).

Commit: `Speak English or Romanian, chosen by --lang, English when it is absent`.

### Commit 4 and 5: English comments and documentation comments (Q10)

One commit per project (`src/`, then `tests/`): every comment translated, meaning kept, not
word for word; every type and method with its `<summary>`, `<param>`, `<returns>`
(constructors and private members included; tests exempt, but their class summaries are
translated); the Romanian assertion messages in the tests translated. Nothing else changes
in these commits (read the diff for a stray behaviour change). The `copy_wcs.py` prototype keeps
its Romanian docstrings: unmaintained prior art, as DarksCheck's `prototype/` (ADR 0001 says
so).

### Commit 6: the documentation (layer 2 and layer 3)

- `docs/requirements.md`: the rows of Appendix D, status `implemented` where a test is renamed
  `R###_...` in the same commit (layer 3 done at once: every row of Appendix D already has
  its test among the 87, the rename is mechanical), `accepted` for the window rows of task 2.
- `tests/CopyWcs.Tests/RepoInvariantTests.cs`: the kit's shape as DarksCheck's (every
  `implemented` requirement has an `R###_` test and every such test names a real requirement;
  the version declared once in `Directory.Build.props`; net8.0 in the props; the manifest
  lists every fixture; no rule cited by number in `CLAUDE.md`).
- `docs/decisions/0001` to `0006` (Appendix A), in the kit's header format.
- `docs/design/README.md` and two notes: `fits-header.md` (Covers: `FitsHeader.cs`,
  `WcsMetadataKeys.cs`: the card and block model, the data offset, the in-place write through
  the temporary file, the key lists and the SIP pattern, what a verbatim card copy preserves;
  the explanations that are in `CLAUDE.md` and in the comments today move here) and
  `run-and-console.md` (Covers: `WcsCopy.cs`, `Language/`, `CopyWcsCli.cs`, `Options.cs`,
  `Program.cs`, `VersionInfo.cs`, `LicenseInfo.cs`: the outcome's kinds and lines, the exit
  codes, the language flow, the version and copyright plumbing).
- `docs/validation.md` and `validation.json`: V-001 per Q6; `ValidationTests` (in the gate,
  no `Category=Validation` trait needed since the fixture is in the repo; `testFilter` stays
  `""`); `tests/testdata/fixtures/<name>.fits` and its hash in the manifest;
  `scripts/check-testdata.ps1 -Update`.
- `CLAUDE.md` rewritten (Appendix E); `README.md` new (Q7); `CHANGELOG.md` lines.
- `/wrap-up`: gate green, the diff read whole, commit, the merge proposed to the owner. After
  the merge the owner cuts nothing: 1.0.0 stays until they decide.

### Verification of task 1

- `scripts/verify.ps1` green in the worktree; `scripts/sync.ps1 -Check` no difference.
- `dotnet run --project src/CopyWcs.Cli -- --help` prints the banner, the copyright, the English
  usage with `--lang`, the English warranty; with `--lang ro` the Romanian usage of today plus
  the `--lang` line; with `--lang xx` the banner, the copyright, `ERROR: --lang expects en or
  ro, got 'xx'`, exit 1.
- A dry run on the validation fixture as source and a test FITS as destination lists the keys
  V-001 records, in both languages, with identical cards.
- An in-place copy on a copy of a real pair of the owner's (a solved light and an unsolved
  stack of the same frame size) gives a file Siril opens with the solve (the owner's check,
  named in the wrap-up; the session cannot open Siril's window).
- `git diff main --stat` shows no file under `scripts/`, `.claude/skills/`, `.claude/agents/`,
  `.claude/rules/kit/` differing from the kit's.

## Task 2: the window

Branch `claude/Build-the-WPF-window`, worktree `.claude/worktrees/Build-the-WPF-window`, from
`main` after task 1 is merged. One session; about five commits.

### Commit 1: requirements and the decision

`docs/requirements.md`: the window block R-030 to R-037 of Appendix D moves from `accepted` to
`draft` and is refined with the implementing session's facts; ADR 0007 (Appendix A).

### Commit 2: the projects and the pieces copied

- `src/CopyWcs.Wpf/CopyWcs.Wpf.csproj`: `WinExe`, `net8.0-windows`, `UseWPF`, assembly
  `CopyWcs.Wpf`, `ApplicationHighDpiMode PerMonitorV2`, `AllowUnsafeBlocks` for the
  `LibraryImport` of `DarkTitleBar`, `ApplicationIcon` and the `<Resource>` for
  `Icon/copywcs.ico`, `LICENSE` copied to the output (AstroDarkroom's reason: the About window
  says the text is beside the program), `<Using Include="System.IO" />`, `InternalsVisibleTo`
  the window's tests; no `InvariantGlobalization` (FlatCheck ADR 0037). `RuntimeIdentifiers`
  come from the props. The solution gains it and `tests/CopyWcs.Wpf.Tests`.
- Copied from the siblings, each with its "copied from" line, a fix going there first (the
  rule DarksCheck's ADR 0009 states; ADR 0007 here): `Presentation/DarkTitleBar.cs`
  (AstroDarkroom's), `Presentation/TExtension.cs` and `Presentation/Mvvm.cs` (DarksCheck's,
  FlatCheck's originally), `Theme/Dark.xaml` (AstroDarkroom's `App.xaml` palette and button
  style, plus the `TextBox`, `CheckBox`, `RadioButton`, `ListBox` and `ScrollViewer` styles
  taken from LightsCheck's `Theme/Dark.xaml`, trimmed to the controls this window uses).
- `Icon/make_icon.py` and `Icon/copywcs.ico` (Q8), the console taking the same icon through
  `ApplicationIcon` as DarksCheck's console does.
- `App.xaml` (the theme merged, `ShutdownMode OnMainWindowClose`) and `App.xaml.cs`: the
  constructor sets `Ui.Default` from `--lang` before the first view loads (FlatCheck's
  `ApplyCommandLine`), `OnStartup` pins the invariant culture, shows the window, says a bad
  `--lang` in an English message box, and hands the window the files of Q9
  (`FilesFromArgs`: the first two existing files among `LanguageArgument.Extract(args).Rest`).
- `tests/CopyWcs.Wpf.Tests`: DarksCheck's csproj shape (`net8.0-windows`, `UseWPF`, CA1707 and
  CA1305 silenced with the comment), `StaFixture.cs` and `WindowFixture.cs` copied, `RomanianByDefault.cs`,
  `FitsTestData.cs` and `TempWorkspace.cs` linked from `tests/CopyWcs.Tests` (not copied: one
  generator).

`verify.ps1` green with the empty window loading; commit.

### Commit 3: the view-model and the window

`ViewModels/MainViewModel.cs` (`INotifyPropertyChanged` through the copied `Mvvm.cs`): the
source path, the destination path, `WriteInPlace` (true) or the output path, `IncludeOptical`
(true), `Force` (false); the commands `Preview` (builds a `WcsCopyRequest` with `DryRun` and
shows the outcome) and `Copy` (the same without `DryRun`); the outcome shown as the core gives
it: `Error` in the warn colour with the heading `T("Refused", "Refuzat")`, `Warnings` in amber,
`Cards` in a monospace list, `Notes` under it; the facts read from the two headers before any
run (`NAXIS1 x NAXIS2` of each, whether `CTYPE1` is present), refreshed when a path changes to
an existing file; both commands disabled until both paths name existing files, `Copy` also
until the output path is set when `WriteInPlace` is false. The view-model never composes a
sentence about the copy itself: every line is the core's. The tests come first
(`MainViewModelTests`, R-030 to R-033, R-036): the preview's cards equal the console's dry-run
lines for the same files; the copy writes bytes identical to the console's in-place copy on a
sibling temp copy; a dimension mismatch without `Force` leaves the destination untouched and
shows the refusal; with `Force` copies and shows the warning; the texts follow `Ui.Current`
in both languages.

`MainWindow.xaml`: one column, 720 x 560 minimum, resizable; the three file rows (a label, a
`TextBox`, a `Browse...` button; `OpenFileDialog` with the filter `FITS (*.fit;*.fits;*.fts)`,
`SaveFileDialog` for the output); the two check boxes; the row of buttons `Preview`, `Copy`,
and `About` at the right; the result card. Every label a `{l:T En='...', Ro='...'}` pair
(Appendix C, "Window"). `MainWindow.xaml.cs`: `DarkTitleBar.Apply` on `SourceInitialized`,
the dialogs, `Start(string? source, string? dest)` from `App`, the About button opening the
About window modally.

### Commit 4: the About window

`Views/AboutWindow.xaml` and `ViewModels/AboutViewModel.cs`: DarksCheck's shape without the
third-party card (no package here): the icon's largest frame, `CopyWcs`, the tagline, the
banner (`VersionInfo.Banner`), the copyright (`LicenseInfo.Copyright`), the licence card with
the warranty paragraph joined from the console's lines (`LicenseInfo.Warranty`, a pair, moved
into the core in this commit so `--help` and the window print the same words) and the link to
`https://www.gnu.org/licenses/gpl-3.0.html`, `Close`; modal, Enter and Esc close it, dark
title bar, made anew at every open. `AboutTests` as DarksCheck's (R-035).

### Commit 5: smoke tests, docs, wrap-up

- `WindowSmokeTests`: the window loads with a real pair from `FitsTestData`, renders in both
  languages with no binding error in the trace and no Romanian word in the English pass
  (R-037); `IconTests` (the `.ico` has the sizes `make_icon.py` draws, the window and the
  executable use it); `AppTests` (R-034: `--lang` read, the files from the arguments, a bad
  value reported).
- `docs/design/window.md` (Covers: `src/CopyWcs.Wpf/`, `tests/CopyWcs.Wpf.Tests/`,
  `Icon/make_icon.py`), a row in the index; `README.md` gains the window section;
  `kit.json` gains the window's publish entry (`"output": "wpf"`, win-x64 self-contained single
  file, as DarksCheck); `CHANGELOG.md` Added; `docs/validation.md` unchanged (the window shows
  V-001's cards through the same core).
- `/wrap-up`; the merge proposed. The owner's check named in the wrap-up: the window on a
  real pair, both languages, the copied file opened in Siril.

### Verification of task 2

- `scripts/verify.ps1` green (both test projects).
- `dotnet run --project src/CopyWcs.Wpf -- --lang ro <solved.fit> <stack.fit>` opens the
  window in Romanian with both boxes filled; `Preview` lists the same cards as
  `copywcs --dry-run --lang ro <solved.fit> <stack.fit>`; `Copy` with "Write a new file"
  produces a file byte-identical to the console's `-o` output (compare with `fc /b`).
- The title bar is dark on this Windows 10 (19045); `About` shows the banner `copywcs 1.0.0`,
  the copyright line and the warranty, and `Close`, Enter and Esc close it.
- A screenshot of the window in each language in the wrap-up message, for the owner.

## Follow-ups, not in this plan

- The Astro Darkroom suite: one `release.publish` entry in `../AstroDarkroom/kit.json`, one
  catalogue line, `SuiteTests`; the owner decides whether CopyWcs is a tool of the suite.
- A live language switch in the window with the choice remembered (AstroDarkroom ADR 0006), if
  the owner wants it after using `--lang`.
- A version bump to 1.1.0 for the window seems warranted (the kit's rule "Version and year are
  the owner's"): said here, decided by the owner.

## What deviated

### Task 1 (2026-10-26, branch `claude/Adopt-the-kit-and-split-the-core`, six commits)

- **The validation fixture is the session's solved stack, not a `pp_light` solve.** The four
  `pp_light_000NN.fit` files DitherCalculator names turned out unsolved on disk (27 cards, no
  `CTYPE1`): the solves of 2026-10-12 were run on private copies. A scan of
  `E:\Photografie\Astro` found 150 solved FITS files, none with a `SITE*` key; the fixture is
  the header of `Veil\2026-10-06-3h\result_final.fit`, Siril 1.4.4, 3 blocks, 8640 bytes
  (ADR 0005). V-001: 72 keys with the optical ones, 61 without, 5601 x 3692.
- **CA1305 and CA1310 were not silenced by `NoWarn`**: `InvariantGlobalization` in the test
  csproj, which the console has too, switches both analyzers off truthfully (ADR 0002).
- **The language tests were named `R020_` to `R023_` in commit 3**, before their rows were
  written in commit 6; the rows and the tests say the same thing, and `RepoInvariantTests`
  arrived with the rows.
- **The kit's `.gitignore` was added in commit 1** (the plan did not list it): without it the
  `.verify-stamp` the gate writes was staged.
- **R-023 compares figures with the temporary folder's path removed**, since the folder names
  differ between the two runs and carry digits.
- **Two Romanian test file names** (`nu-exista.fit`, `nici-asta.fit`) were translated with the
  comments; the plan named only comments and assertion messages.
- Commit 1 also prefixed nothing and renamed only the three tests the move broke
  (`...FromTheBuildProps`); the `R###` prefixes came in commit 6 as planned.
- The `docs/skills.md` sections were filled (new texts, V-001, the release's one target,
  what `/mutate` would need); the plan said "the kit's empty sections".
- Nothing else deviated: the console's Romanian output is byte-identical except `--help`'s
  `--lang` line and the HISTORY card; 87 tests before, 125 after (9 `WcsCopyTests`, 20
  `LanguageTests` cases, 2 `ValidationTests`, 6 `RepoInvariantTests`, 1 renamed).

- **The review (a subagent, 2026-10-26) found one regression and six precision items, all
  fixed in a seventh commit.** The regression: the catch filters that replaced `main`'s
  `catch (Exception)` left out `ArgumentException`, so an empty or blank path (`copywcs ""
  dest.fit`, `-o ""`) crashed the process with a stack trace where `main` refused it with one
  line and exit 1; `ArgumentException` joined the three filters, with four tests first
  (`R004_Blank*`, `R014_BlankOutputPath*`). The rest: R-023 now also runs a missing
  destination, a non-FITS source and a truncated source, and its Romanian word list has the
  reader's words; R-003 and `Options.Parse`'s doc no longer say `-h` works "wherever it is"
  (an unknown option before it is still a usage error, as on `main`); the design note's "up to
  72 cards" became the real bound (47 fixed keys plus the SIP coefficients); the two
  `Regex.Matches(...).Count` became `Regex.Count` (CA1875 under SDK 10, outside the pin);
  `InvariantGlobalization` left the core csproj (no effect in a library); the reader's and the
  writer's XML docs list `UnauthorizedAccessException` and `ArgumentException`. Noted and
  left as it was on `main`: `-o` naming the destination in another letter case is not seen as
  in place and fails with a sharing violation, exit 1.

### Task 2

(filled at its wrap-up)

## Appendix A: the ADRs

Task 1:

- **0001 Adopt the kit's process; the layout is the kit's `src/` and `tests/`; the core is a
  compile boundary under the console and the window.** Context: the three commits of
  2026-09 to 2026-10, the Python prototype, the request of 2026-10-26. Decision: Q1; the
  kit's shared folders byte for byte, `kit.json` of Appendix B; the prototype frozen under
  `prototype/`, prior art with its Romanian docstrings; the kit's decisions cited, not
  rewritten. Rejected: the root layout (FlatCheck's), the window project in the console's
  folder.
- **0002 Gate strictness: the kit's full `.editorconfig`, the braces rule on, the kit's test
  packages, coverlet dropped.** The counts measured before and after (the table above and the
  re-measurement in layer 1). Q4, Q5.
- **0003 `<Version>`, `<Copyright>` and the licence expression live in `Directory.Build.props`.**
  The kit's `release.ps1` reads them there; `VersionTests` and `LicenseNoticeTests` follow;
  the version number does not change. (ImageVisualCompare ADR 0004's reasoning.)
- **0004 Two interface languages, English by default, chosen by `--lang`; English in the code,
  the comments, the docs and the HISTORY card; the tests run in Romanian.** Q2, Q3, Q10; the
  shape and the alternatives rejected of DarksCheck ADR 0021 (a text table, `.resx`, the
  language as a parameter), cited not copied. Supersedes the `CLAUDE.md` convention "User-facing
  text in Romanian", quoted in its Context.
- **0005 Validation on the header of a real Siril plate solve, a fixture in the repo, in the
  gate.** Q6: the file chosen, its Siril version, why the header alone is a real measurement
  (the tool reads nothing past the header), the site-key check, the figures of V-001.
- **0006 `README.md` is the user documentation, in English.** Q7.

Task 2:

- **0007 The window is a thin adapter over the core and computes no line of its own; the
  theme, the title bar and the MVVM pieces are copies of the siblings'; the window's tests are
  a second project.** The DarksCheck ADR 0009 analog: what the view-model may do (build a
  request, read two header facts, show the outcome), what it may not (compose a sentence about
  the copy, touch a card); the copies and their origins; the icon (Q8); the command line (Q9);
  no live language switch (the owner's request, FlatCheck's way).

## Appendix B: `kit.json` (task 1; task 2 adds the second publish entry)

```json
{
  "$comment": "What the shared scripts, skills and agent need to know about CopyWcs. With docs/skills.md, the only file edited here to use them; scripts/, .claude/skills/, .claude/agents/ and .claude/rules/kit/ are byte-identical with ../ClaudeCodeStarterKit's (scripts/sync.ps1 keeps them so; the kit's ADRs 0017, 0018 and 0028). The one validation case reads a fixture in the repo, so the gate runs every test (ADR 0005). See scripts/kit-config.ps1 for every key.",
  "solution": "CopyWcs.sln",
  "testFilter": "",
  "codeFolders": ["src/"],
  "codeFiles": [],
  "testFolders": ["tests/"],
  "testData": {
    "folder": "tests/testdata/",
    "largeEnv": "COPYWCS_TESTDATA_LARGE"
  },
  "mutate": {
    "testProject": "tests/CopyWcs.Tests/"
  },
  "release": {
    "publish": [
      {
        "project": "src/CopyWcs.Cli/CopyWcs.Cli.csproj",
        "output": "console",
        "args": ["-r", "win-x64", "--self-contained", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true"]
      }
    ]
  },
  "audit": {
    "alsoRead": ["README.md"]
  },
  "kit": "../ClaudeCodeStarterKit"
}
```

Task 2 adds, after the console's entry:

```json
      {
        "project": "src/CopyWcs.Wpf/CopyWcs.Wpf.csproj",
        "output": "wpf",
        "args": ["-r", "win-x64", "--self-contained", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true"]
      }
```

## Appendix C: every user-visible text, Romanian as today and its English

The Romanian column is the text as the code has it on 2026-10-26 (it stays, as the `ro`
wording); the English column is the `en` wording, to be read for meaning before use.

Console, the usage (`Options.PrintUsage`; aligned by hand in each language):

| Romanian (today) | English |
| --- | --- |
| `Copiaza WCS + metadate optice dintr-un FITS sursa in unul destinatie.` | `Copies the WCS and the optical metadata from a source FITS into a destination one.` |
| `copywcs <sursa.fit> <destinatie.fit> [optiuni]` | `copywcs <source.fit> <destination.fit> [options]` |
| `Optiuni:` | `Options:` |
| `-o, --output <fisier>  Scrie intr-un fisier nou in loc sa modifice destinatia in loc` | `-o, --output <file>  Write a new file instead of changing the destination in place` |
| `--dry-run  Arata ce s-ar copia, fara sa scrie nimic` | `--dry-run  Show what would be copied, without writing anything` |
| `--force  Continua chiar daca dimensiunile imaginilor difera` | `--force  Continue even when the images' dimensions differ` |
| `--no-optical  Copiaza doar cheile WCS, fara metadatele optice` | `--no-optical  Copy only the WCS keys, not the optical metadata` |
| (new) `--lang en\|ro  limba textelor: en (engleza, implicit) sau ro (romana)` | `--lang en\|ro  the language of the texts: en (English, the default) or ro (Romanian)` |
| `-h, --help  Afiseaza acest mesaj` | `-h, --help  Show this message` |
| the warranty paragraph (five lines, GPLv3 5(d)) | `This program comes with ABSOLUTELY NO WARRANTY, not even the implied warranty of merchantability or fitness for a particular purpose. It is free software: you may redistribute and modify it under the terms of the GPLv3 (or, at your option, any later version). The full text is in the LICENSE file, or at <https://www.gnu.org/licenses/gpl-3.0.html>.` (wrapped the same way) |

Console, the parse errors (`Options.Parse`):

| Romanian (today) | English |
| --- | --- |
| `--output cere un nume de fisier.` | `--output needs a file name.` |
| `Optiune necunoscuta: X` | `Unknown option: X` |
| `Sunt necesare exact doua fisiere: sursa si destinatie.` | `Exactly two files are needed: the source and the destination.` |

Console and core, the run (`CopyWcsCli.Run`, moving into `WcsCopy.Run`):

| Romanian (today) | English |
| --- | --- |
| `Nu pot citi sursa 'X': msg` | `Cannot read the source 'X': msg` |
| `Nu pot citi destinatia 'X': msg` | `Cannot read the destination 'X': msg` |
| `Dimensiuni diferite: sursa (a, b) vs destinatie (c, d).` | `Different dimensions: source (a, b) vs destination (c, d).` |
| ` WCS-ul ar fi gresit. Foloseste --force daca stii ce faci.` | ` The WCS would be wrong. Use --force if you know what you are doing.` |
| `ATENTIE: {msg} Continui oricum (--force).` | `WARNING: {msg} Continuing anyway (--force).` |
| `ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1).` | `WARNING: the source does not look plate-solved (CTYPE1 is missing).` |
| `Nicio cheie relevanta gasita in sursa. Nimic de copiat.` | `No relevant key found in the source. Nothing to copy.` |
| `Chei care ar fi copiate (N):` | `Keys that would be copied (N):` |
| `Eroare la scriere: msg` | `Write error: msg` |
| `Copiate N chei in T.` | `Copied N keys into T.` |
| `Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare.` | `Check the annotation by eye: if the image was mirrored or flipped during processing, the copied WCS will be wrong without any error.` |
| HISTORY card `WCS copiat din X` | `WCS copied from X` in both languages (Q3) |

Core, the reader (`FitsHeader.ReadFrom`):

| Romanian (today) | English |
| --- | --- |
| `Fisier trunchiat sau nu este FITS valid (cardul END lipseste).` | `Truncated file, or not a valid FITS (the END card is missing).` |
| `Fisierul nu incepe cu SIMPLE - nu pare FITS.` | `The file does not begin with SIMPLE - it does not look like FITS.` |

Window (task 2, both new; the English first as the code writes the pair):

| English | Romanian |
| --- | --- |
| `CopyWcs` (the title and the product name, the same in both) | `CopyWcs` |
| `Plate-solved source (FITS)` | `Sursa plate-solved (FITS)` |
| `Destination (FITS)` | `Destinatia (FITS)` |
| `Browse...` | `Alege...` |
| `Write into the destination` | `Scrie in destinatie` |
| `Write a new file` | `Scrie un fisier nou` |
| `Copy the optical metadata too (FOCALLEN, XPIXSZ, ...)` | `Copiaza si metadatele optice (FOCALLEN, XPIXSZ, ...)` |
| `Continue even when the dimensions differ` | `Continua chiar daca dimensiunile difera` |
| `Preview` | `Previzualizare` |
| `Copy` | `Copiaza` |
| `About` | `Despre` |
| `Refused` (the heading over the outcome's error line) | `Refuzat` |
| `Source: 6016 x 4016, plate-solved` / `not plate-solved` (the header facts) | `Sursa: 6016 x 4016, plate-solved` / `fara plate solve` |
| `Destination: 6016 x 4016` | `Destinatia: 6016 x 4016` |
| `FITS (*.fit;*.fits;*.fts)` (the dialogs' filter) | `FITS (*.fit;*.fits;*.fts)` |
| `About CopyWcs` | `Despre CopyWcs` |
| `Copies the plate solve from one FITS into another.` | `Copiaza plate solve-ul dintr-un FITS in altul.` |
| `LICENCE` | `LICENTA` |
| `The full text of the licence (GPLv3)` (the link's label) | `Textul complet al licentei (GPLv3)` |
| `Close` | `Inchide` |

## Appendix D: the requirements (draft rows; the implementing session refines the examples)

Blocks of ten per area, the kit's way. Every row of the first three blocks has a test among
today's 87 (named in the last column, to be renamed `R###_...`).

| ID | Area | Requirement | Test today |
| --- | --- | --- | --- |
| R-001 | console | Line 1 of every run is `copywcs <version>`, line 2 the copyright, in every mode, at `--help` and on failures | `VersionTests`, `LicenseNoticeTests` |
| R-002 | console | A usage error (not exactly two files, an unknown option, `--output` without a value) prints the error on stderr and the usage on stdout and exits 2 | `Run_BadArguments_...`, `OptionsTests` |
| R-003 | console | `--help` prints the usage and the warranty paragraph and exits 0, nothing on stderr | `Run_HelpFlag_...`, `PrintUsage_...` |
| R-004 | run | A source or destination that cannot be read gives one line on stderr naming which and why, exit 1, nothing written | `Run_SourceFileMissing_...`, `Run_DestFileMissing_...` |
| R-005 | run | Different `NAXIS1`/`NAXIS2` refuse the copy with exit 1 and both sizes named, unless `--force`, which copies and prints a warning with both sizes | `Run_DimensionMismatch*` |
| R-006 | run | A source without `CTYPE1` prints a warning and continues | `Run_SourceMissingCtype1_...` |
| R-007 | run | The keys copied are the WCS list, every `A_i_j`, `B_i_j`, `AP_i_j`, `BP_i_j` present, and the optical list unless `--no-optical`; only those present in the source; each once | `WcsMetadataKeysTests`, `Run_NoOpticalFlag_...`, `Run_WithoutNoOpticalFlag_...` |
| R-008 | run | No key present: one line on stderr, exit 1, nothing written | `Run_NoRelevantKeysInSource_...` |
| R-009 | run | `--dry-run` lists each card that would be copied, trimmed, one per line, and leaves the destination byte-identical | `Run_DryRun_...` |
| R-010 | write | A copied card is the source's 80 bytes, not a reformatted value | `Run_CopiedCards_AreByteIdentical...` |
| R-011 | write | One HISTORY card naming the source file is appended (at most 72 characters of text) | `FitsHeaderTests` (AddHistory) |
| R-012 | write | Without `--output` the destination is rewritten in place through a temporary file; with it a new file is written and the destination untouched | `Run_SuccessfulInPlaceCopy_...`, `Run_SuccessfulCopyViaOutputOption_...` |
| R-013 | write | The data area is copied unchanged, the header padded to the next 2880-byte boundary, also when the header grows past its original blocks | `FitsHeaderTests` (WriteWithDataFrom) |
| R-014 | write | A write failure gives one line on stderr, exit 1 | `Run_WriteTargetInNonExistentDirectory_...` |
| R-015 | read | A file without SIMPLE, or truncated before END, is refused with the reader's message | `FitsHeaderTests` |
| R-020 | language | `--lang <code>` and `--lang=<code>`, anywhere on the line, case-insensitive, the last one counting; `en` and `ro` | new `LanguageTests` |
| R-021 | language | Without `--lang` the texts are English | new |
| R-022 | language | A value that is not `en` or `ro`: the banner, the copyright, an English `ERROR:` line, exit 1 | new |
| R-023 | language | Every scenario of R-002 to R-015 in English has the same exit code and the same figures as in Romanian and no Romanian word | new |
| R-024 | validation | On the Siril-solved header fixture the selected keys are the list V-001 records (N with the optical keys, M without) | new `ValidationTests` |
| R-030 | window | The window takes a source, a destination, an output file or in place, the two options, and offers `Preview` and `Copy` | task 2 |
| R-031 | window | `Preview` shows exactly the cards the console's `--dry-run` lists for the same files | task 2 |
| R-032 | window | `Copy` writes bytes identical to the console's for the same request | task 2 |
| R-033 | window | A refusal or a warning of the core is shown with the core's words; nothing is written on a refusal | task 2 |
| R-034 | window | `--lang` is read before the first view; a bad value is said in English and the window goes on in English; the first two existing files among the other arguments fill the boxes | task 2 |
| R-035 | window | The About window shows `VersionInfo.Banner`, `LicenseInfo.Copyright`, the console's warranty words and the licence link | task 2 |
| R-036 | window | Every label and heading is a pair; the window renders in both languages with no Romanian word in English | task 2 |
| R-037 | window | The window and the About window load with no binding error, the dark palette as background, the icon of `Icon/copywcs.ico` | task 2 |

## Appendix E: the new `CLAUDE.md` (task 1; task 2 adds the window lines)

```markdown
# CopyWcs

Copies the plate solve (WCS) and the optical metadata from a Siril-solved FITS into another
FITS of the same pixel grid, by reading and writing the FITS header cards by hand: no FITS
library, no package. Two applications over one core: a console (`copywcs.exe`) and a WPF
window (`CopyWcs.Wpf.exe`, task 2). It copies each selected card's 80 bytes verbatim and
copies the destination's data unchanged; it never reads a pixel.

## Commands

- Gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build CopyWcs.sln -c Release --nologo`
- Test: `dotnet test CopyWcs.sln -c Release --nologo`
- One test: `dotnet test CopyWcs.sln --filter "FullyQualifiedName~R005"`
- Format: `dotnet format CopyWcs.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/CopyWcs.Cli -- <solved.fit> <destination.fit> [--dry-run] [--lang ro]`
- Window: `dotnet run --project src/CopyWcs.Wpf -- [--lang ro] [<solved.fit> <destination.fit>]`
- Test data hashes after an intentional fixture change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched: `scripts/check-design.ps1 -Since main`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`

## Layout

- `src/CopyWcs.Core/` - the FITS header engine (`FitsHeader`), the key lists
  (`WcsMetadataKeys`), the run (`WcsCopy`), the texts' language (`Language/`), the version
  and the copyright read from the assembly. No Console, no UI: a compile boundary.
- `src/CopyWcs.Cli/` - the console: options, dispatch, exit codes, printing.
- `src/CopyWcs.Wpf/` - the window: a thin adapter over the core (ADR 0007). `Icon/` - the icon.
- `tests/CopyWcs.Tests/` - unit, acceptance on `CopyWcsCli.Run`, the language, the validation
  case, repo rules. `tests/CopyWcs.Wpf.Tests/` - the window's tests, `net8.0-windows`.
- `tests/testdata/` - the one fixture (a real Siril-solved header) and the manifest.
- `prototype/copy_wcs.py` - the Python/astropy prototype the tool was ported from, frozen.
- `docs/requirements.md`, `docs/decisions/`, `docs/design/`, `docs/validation.md` + `.json`,
  `docs/plans/`, `kit.json`, `docs/skills.md`: the kit's layout; `README.md` the user
  documentation, English (ADR 0006).

## Rules

The kit's process rules are in `.claude/rules/kit/process.md`. CopyWcs's own:

- **Core has no UI.** `CopyWcs.Core` never references Console, WPF or a dialog.
- **A card is copied verbatim.** `dest.SetCard(key, source.GetCard(key))` copies the raw
  80-byte card; never a reparsed or reformatted value (R-010).
- **`Main` is a one-line shim** to `CopyWcsCli.Run(args, stdout, stderr)`; new console
  behaviour goes in `CopyWcsCli` or `Options`, testable in process.
- **The window computes nothing.** It builds a request, reads two header facts and shows the
  core's outcome; every sentence about the copy is the core's (ADR 0007).
- **Two interface languages, English everywhere else** (varies the kit's rule "Languages",
  ADR 0004). Every text the user sees is `T("english", "romana")` where it is composed, Romanian
  without diacritics; the HISTORY card is English in both. Code, comments, docs, tests and
  commits are English; the Romanian wordings and the tests that quote them are the exception.
- **Every source file starts with the GPL notice**, `.xaml` and `.py` too; `LicenseHeaderTests`
  walks `src/`, `tests/` and `prototype/`.
- **The banner and the copyright are lines 1 and 2 of every run** (R-001), read from
  `Directory.Build.props` through the assembly (ADR 0003); no `--version` flag.

## Read before touching

`docs/design/README.md` maps the areas: `fits-header.md` before `FitsHeader` or the key lists,
`run-and-console.md` before the run, the console or the language, `window.md` before the window.

## Style

- `.editorconfig` and the analyzers enforce style at build time; do not restate style here.
- Nullable enabled, warnings are errors, `AnalysisLevel` latest-recommended. Fix, do not
  suppress, unless the suppression carries a comment with the reason.
```
