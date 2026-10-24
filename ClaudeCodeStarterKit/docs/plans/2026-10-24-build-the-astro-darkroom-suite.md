# Build the Astro Darkroom suite - five parts in two sessions

Saved, once approved, as `ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`
and committed on `main` by the owner (see "After approval"), so every task's worktree has it.

## Context

The owner wants one download for the five astrophotography tools in the `photo` monorepo
(`E:\Photografie\Code`): AstroPhotoAnalyzer, DarksCheck, FlatCheck, ImageVisualCompare and
LightsCheck, plus a small WPF launcher that lists them and starts them. The suite is called
**Astro Darkroom**. Today each project has its own `/release` (the kit's `scripts/release.ps1`),
no project has ever been tagged, and the repo has no root pipeline: releases are built locally
and the zip goes to Bitbucket Downloads by hand. The projects cannot share one folder yet: three
target .NET 9 (DarksCheck, LightsCheck, ImageVisualCompare) and two target .NET 8
(AstroPhotoAnalyzer, FlatCheck), and a self-contained publish of two runtimes into one folder
would overwrite `coreclr.dll`, `System.*.dll` and WPF with the other version. The shared NuGet
packages already resolve to the same versions everywhere (ImageSharp 3.1.11, Sdcb.LibRaw
0.21.1.7 / 0.21.1); only test tooling differs, and it does not ship.

Facts measured on 2026-10-24 (read-only, this session):
- `System.Threading.Lock` is the only .NET 9-only API in the three projects: `DarksCheck/src/DarksCheck.Cli/Program.cs:105`, `DarksCheck/src/DarksCheck.Core/Analysis/FrameStack.cs:108`, `DarksCheck/src/DarksCheck.Core/Analysis/LightsAnalyzer.cs:82`, `LightsCheck/src/LightsCheck.Cli/Program.cs:131`.
- Every project pins SDK `9.0.318` with `rollForward: latestPatch` in `global.json`; this machine has SDK 9.0.319 and 10.0.402 and the 8.0.32 desktop runtime. SDK 9 builds net8 with C# 13 (`LangVersion latest`), so `global.json` stays.
- The test packages (Test.Sdk 18.10.1 / 17.12.0, xunit 2.9.x, runner 3.1.5 / 2.8.2) all support net8; nothing changes there.
- No Wpf project references a console project; each references its Core. So the only files two publishes can both write are runtime files, shared package DLLs, `lensfun/` (FlatCheck console and window) and copied `LICENSE` / notices files.
- `release.ps1` publishes `release.publish[]` entries with `dotnet publish ... -o artifacts/<version>/<output>`, straight into the target, so a second entry silently overwrites the first.

## The decisions (owner, in chat, 2026-10-24)

- **.NET 8 for now.** The suite's projects target net8.0 / net8.0-windows. .NET 10 was offered and declined for the moment; it is a later task, one per project.
- **The suite:** AstroPhotoAnalyzer, DarksCheck, FlatCheck, ImageVisualCompare, LightsCheck, and a launcher. One flat folder: every exe and its files beside each other, one copy of the runtime and of every library. Same package versions enforced by a test; same file bytes enforced at assembly.
- **Name:** "Astro Darkroom" for people; `AstroDarkroom` as folder, project, namespace and exe; tag `astrodarkroom-vX.Y.Z` (what `release.ps1` derives from the folder name); zip `AstroDarkroom-X.Y.Z-win-x64.zip` with a `.sha256` beside it.
- **The launcher is a kit project** in `E:\Photografie\Code/AstroDarkroom`, like the others; its `<Version>` is the suite's version and its `/release` is the suite's release.
- **A suite release takes the components from `main` as they are** (clean tree required) and records each component's `<Version>`, last commit and whether it has unreleased changes. Components are not required to be released first.
- **The app list is compiled in** (`AstroDarkroom.Core`), with Romanian titles and one-line descriptions; a test keeps it in step with `kit.json`.
- **The .NET 8 moves are parts 1-3 of this plan**, one ADR per project.
- **Two sessions** (owner, 2026-10-24, after the plan was first presented as five): the
  mechanical parts 1-4 in one session on one branch, the launcher in a second session on a
  fresh context once the first is merged. One session for everything is technically possible
  (a worktree is a full `photo` checkout) but would build the launcher, the largest part, on a
  long summarized history, and would give the owner one large merge instead of two.

## The five parts, in two sessions

**Session A** - branch `claude/Prepare-the-Astro-Darkroom-suite`, one worktree made from the kit
folder (a change spanning the kit and projects goes there; FlatCheck's ADR 0044 is the
precedent): in `E:\Photografie\Code\ClaudeCodeStarterKit`,
`git worktree add .claude/worktrees/Prepare-the-Astro-Darkroom-suite -b claude/Prepare-the-Astro-Darkroom-suite main`.
Open the session in that kit folder of the main checkout; the worktree `<wt>` holds every
project. Parts in this order, each part's commits in its own project folder
(`git -C <wt>/<Project> commit ...`), each commit gated by that project's `verify.ps1`:

1. **DarksCheck to .NET 8** - ADR 0018 there.
2. **LightsCheck to .NET 8** - ADR 0017 there.
3. **ImageVisualCompare to .NET 8** - ADR 0037 there.
4. **Kit: a release stages every publish, merges with a hash check, bundles sibling projects, writes `release.json` and zips** - kit ADR 0036; synced to the six projects, one commit each.

Then `/wrap-up` (it runs the gate of every project the branch touched: the kit and the six
projects) and the merge proposal; the owner merges by hand (`--no-ff`), the assistant deletes
the branch and the worktree.

**Session B** - branch `claude/Start-AstroDarkroom-from-the-kit`, a new worktree from the kit
folder, after session A is merged into `main`:

5. **AstroDarkroom, the launcher and the suite's release** - ADRs 0001-0003 there; the first
   measured suite publish.

### Rules that bite in these tasks (the kit's `.claude/rules/kit/process.md`)

- A worktree per session, never the main checkout. It is a full `photo` checkout; work in
  `<wt>/<Project>` and commit with `git -C <wt>/<Project> commit`; a commit holds files of one
  project. Keep worktree names under about 40 characters (Windows path limit).
- Commits: one line, English, at most 100 characters, saying why; the gate (`scripts/verify.ps1`)
  must be green; never `--no-verify`, never `-c core.hooksPath`. Expect the hook's `== OK` line.
- Requirements first, then the test, then the code; every type and method in code gets an XML
  doc comment; a decision made here gets its ADR in the same task; `CHANGELOG.md` gets a line
  under `[Unreleased]` when the version history should mention it.
- Never edit `scripts/`, `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` in a project;
  part 4 edits them in the kit and syncs.
- Never bump a `<Version>`; say when a change seems to warrant it.

## Part 1 - DarksCheck to .NET 8 (session A)

Work in `<wt>/DarksCheck`; one or two commits there.

1. **Guard first** (the rule "Requirements first" has no requirement here; the guard is a repo
   invariant): add to `tests/DarksCheck.Tests/RepoInvariantTests.cs` a test
   `TargetFramework_IsNet8_InEveryProjectThatShips` asserting `Directory.Build.props` holds
   `<TargetFramework>net8.0</TargetFramework>` and `src/DarksCheck.Wpf/DarksCheck.Wpf.csproj`
   holds `net8.0-windows`. It fails now.
2. **Framework:** `Directory.Build.props` line 9 `net9.0` -> `net8.0`;
   `src/DarksCheck.Wpf/DarksCheck.Wpf.csproj` and `tests/DarksCheck.Wpf.Tests/DarksCheck.Wpf.Tests.csproj`
   `net9.0-windows` -> `net8.0-windows`, and their comments that say "net9.0-windows + UseWPF".
3. **Code:** replace the four `Lock` fields/locals with `object`
   (`private readonly object _lock = new();`, `object gate = new();`); the `lock (...)`
   statements stay. Files: `src/DarksCheck.Cli/Program.cs:105`,
   `src/DarksCheck.Core/Analysis/FrameStack.cs:108`, `src/DarksCheck.Core/Analysis/LightsAnalyzer.cs:82`.
4. **Lock files:** `dotnet restore DarksCheck.sln --force-evaluate`; `git diff --stat` must show
   the five `packages.lock.json` with `net9.0` keys renamed to `net8.0` and no package version
   changed. Any version change is a finding: stop and report.
5. **Docs:** `CLAUDE.md:51` (`net9.0-windows` -> `net8.0-windows`); the comment at
   `tests/DarksCheck.Tests/RepoInvariantTests.cs:110`; grep `net9\|\.NET 9` over `*.md`,
   `*.csproj`, `*.cs` and fix every live mention (ADRs and plans are history: leave them).
   `README.md:368` names the SDK 9.0.3xx, which stays true.
6. **ADR 0018** `docs/decisions/0018-the-projects-target-net-8-so-the-suite-ships-one-runtime.md`,
   `Status: accepted 2026-10-24`. Context: the Astro Darkroom suite puts the five tools in one
   self-contained folder, one runtime; AstroPhotoAnalyzer and FlatCheck are on .NET 8; the owner
   chose .NET 8 for now on 2026-10-24 (`ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`).
   Decision: net8.0 / net8.0-windows; SDK pin unchanged. Alternatives rejected: .NET 10 LTS for
   all (declined by the owner for now, a later task per project); .NET 9 for all (the two .NET 8
   projects would move instead, and .NET 9 leaves support in November 2026); a single-file exe
   per app with its own runtime (five copies of the runtime; the owner chose one folder).
   Consequences: `System.Threading.Lock` is gone (plain `object` locks); C# 13 stays through
   `LangVersion latest` on SDK 9; guard: the test of step 1. Cite the kit's ADR 0034 for the
   runtime declaration, unchanged.
7. **CHANGELOG** `[Unreleased]` / `### Changed`: "The projects target .NET 8 instead of .NET 9,
   so the Astro Darkroom suite ships one runtime (ADR 0018)." Say at wrap-up that a Changed line
   may warrant a minor bump; the owner decides.
8. `scripts/verify.ps1` green (it also runs the Wpf smoke tests on net8); commit.

## Part 2 - LightsCheck to .NET 8 (session A)

Same steps as part 1 in `<wt>/LightsCheck`, with these values:
`Directory.Build.props` line 9; `src/LightsCheck.Wpf/LightsCheck.Wpf.csproj` and
`tests/LightsCheck.Wpf.Tests/LightsCheck.Wpf.Tests.csproj`; one `Lock` at
`src/LightsCheck.Cli/Program.cs:131`; comment at `tests/LightsCheck.Tests/RepoInvariantTests.cs:110`;
`README.md:255` names the SDK (stays). ADR 0017 with the same title and content as DarksCheck's
0018 (each project records its own deviation, the kit's ADR 0025). Guard test in
`tests/LightsCheck.Tests/RepoInvariantTests.cs`.

## Part 3 - ImageVisualCompare to .NET 8 (session A)

Same shape in `<wt>/ImageVisualCompare`. Differences:
- No central framework: edit the three csproj files,
  `src/ImageVisualCompare.Core/ImageVisualCompare.Core.csproj` (`net8.0`),
  `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj` and
  `tests/ImageVisualCompare.Tests/ImageVisualCompare.Tests.csproj` (`net8.0-windows`).
- No `Lock` to replace. Lock files: three, two of them empty, all keyed `net9.0`.
- Live mentions to fix: `CLAUDE.md` lines 6, 36, 44, 49; `README.md:8` ("Aplicatie WPF pe
  .NET 9" -> .NET 8) and `README.md:166` (the SDK line: say .NET SDK 9.0.3xx, targets .NET 8);
  `docs/design/code-map.md` lines 13, 48, 92.
- ADR 0037, same content; the guard test asserts the three csproj files.

## Part 4 - Kit: stage, merge, bundle, `release.json`, zip (session A)

Work in `<wt>/ClaudeCodeStarterKit`. Kit ADR **0036**. The kit's commits come after parts 1-3,
so the sync commits land on projects already on .NET 8.

### What changes

**`scripts/merge-publish.ps1` (new).** `-From <dir> -Into <dir>`. Pass 1 compares every file
under `From` with the same relative path under `Into` by SHA-256 (`Get-FileHash`); pass 2 copies
only when pass 1 found no difference. A file that exists in both with equal bytes is skipped; one
with different bytes is a collision. On any collision: print each as
`collision  <relative path>  (<From> differs from <Into>)`, copy nothing, exit 1. Header comment
in the kit's style (WHAT IT DOES / WHO CALLS IT / OPTIONS / RESULT), like `release.ps1`.

**`scripts/release.ps1`.**
- Step 1 gains, for each `release.publish` entry: resolve `project` against `$root`; find its
  project root with `Find-KitProject` (from `kit-config.ps1`); if that root is not `$root`, it is
  a *component*: it must have a readable `kit.json` (`Read-KitConfig`), a `<Version>` in its
  `Directory.Build.props` and a `CHANGELOG.md`, and `git status --porcelain -- <root>` must be
  empty, else throw before anything changes (`Component X is not clean; commit or stash first`).
  The checkout's own cleanliness is checked as today.
- Step 5 publishes each entry to `artifacts/<version>/.stage/<index>/` and then runs
  `merge-publish.ps1 -From that -Into artifacts/<version>/<output>`; a collision throws with the
  script's output, after the version files are changed (same situation as a failed publish today;
  the message says so and names `-PublishOnly`). `.stage/` is removed after the last merge.
- After the merges: write `artifacts/<version>/release.json`, UTF-8 without BOM:
  `{ "name": "<folder leaf>", "version": "X.Y.Z", "date": "YYYY-MM-DD", "commit": "<HEAD, full>",
  "components": [ { "name": "<folder leaf>", "version": "<its <Version>>", "commit": "<git log -1 --format=%H -- <root>>", "unreleased": true|false } ] }`.
  Components are the distinct project roots of the entries, this project first; `unreleased`
  is whether that CHANGELOG's `[Unreleased]` has content (the regex the script already uses).
  Always written, also for a plain project (then one component: itself).
- If `release.zip` is set: `artifacts/<name>.zip` with `{version}` replaced, built with
  `[System.IO.Compression.ZipFile]::CreateFromDirectory(<out>, <zip>, Optimal, $false)` after
  `Add-Type -AssemblyName System.IO.Compression.FileSystem` (not `Compress-Archive`: PowerShell 5.1
  writes backslash entry names and had a 2 GB limit). Beside it `<name>.zip.sha256` holding
  `<lowercase hex>  <zip file name>` and a newline. Print both sizes. The zip lives in
  `artifacts/`, not in `artifacts/<version>/`, so it never contains itself.
- New switch `-PublishOnly`: runs only step 5 (stage, merge, `release.json`, zip) for the given
  `-Version`, with no checks and no file edits. For a retry after a collision, and for the
  measurement in part 5 (where a task branch cannot pass the branch check).
- `-WhatIf` also lists the components it would bundle and whether each is clean.

**`scripts/kit-config.ps1`.** `release.zip`: optional string, default `''`; documented in the
header: "optional; the file name of the zip of artifacts/<version>/, written to artifacts/ with
a .sha256 beside it; `{version}` is replaced; absent or empty: no zip". The header's
`release.publish` line gains: "project may be in a sibling project (`../X/...`), which is then
a component: bundled from its clean checkout and listed in release.json".

**Tests (`tests/Starter.Tests`).** New `MergePublishTests`: equal files are skipped and the rest
copied, subfolders preserved; a differing file is reported by relative path, exit 1, nothing
copied (also the non-colliding files). In `ReleaseTests` (they run with `-NoPublish`, so the
preconditions are what they test): an entry `../Proj2/p.csproj` where `Proj2` has no `kit.json`
is refused with the folder named; where `Proj2` is a kit project with an uncommitted file it is
refused as not clean; where it is clean, `-WhatIf` lists it. `ScriptRunner.KitJson` gets an
optional parameter for extra publish entries or the tests write the JSON themselves.

**Docs.** ADR 0036 "A release stages each publish and merges it with a hash check; it may bundle
sibling projects, writes release.json and may zip" (Context: the Astro Darkroom suite, the
overwrite problem measured above, the owner's 2026-10-24 decisions; Alternatives rejected:
publishing straight into the folder as before, with the collision unseen; building components
from their tags in temporary worktrees, which is far more machinery for a single developer who
keeps `main` green; a suite script outside the kit, which the rule "Shared folders" forbids in a
project). `kit.json` `$comment`; `.claude/skills/release/SKILL.md` step 5 (components, the
collision case: the owner decides, usually an `output` subfolder for that entry; `release.json`;
the zip and its hash); `README.md` and `CLAUDE.md` rows about `kit.json`; `GHID.md` (Romanian):
the Release paragraph (around line 295) and the `kit.json` row (line 50) gain the bundle, the
zip and `release.json`; the monorepo table may note the suite zip. `CHANGELOG.md` `[Unreleased]`
/ `### Added`: the bundle, `release.json`, the zip, `-PublishOnly` (ADR 0036). Kit ADR 0017's key
list is history; it is not edited (the kit's ADR 0024).

**Sync.** From `<wt>/ClaudeCodeStarterKit`: `scripts/sync.ps1`, then one commit per project
(`git -C <wt>/<Project> commit -m "Sync the kit's staged publish, bundle and zip into release.ps1"`),
six commits; each runs that project's gate through the hook (minutes each; run them in the
background). `scripts/verify.ps1` step 0 must be green in every project afterwards. Then
`/wrap-up` for session A and the merge proposal.

## Part 5 - AstroDarkroom (session B)

Branch `claude/Start-AstroDarkroom-from-the-kit`, from `E:\Photografie\Code\ClaudeCodeStarterKit`:
`git worktree add .claude/worktrees/Start-AstroDarkroom-from-the-kit -b claude/Start-AstroDarkroom-from-the-kit main`.
The new folder is `<wt>/AstroDarkroom`; open the session in the kit folder of the main checkout
and work there. Session A must be merged into `main` first (check `git log` for its merge).

### 5.1 Make the project (commit 1: "Start AstroDarkroom from the kit as the suite's launcher and release")

Follow the kit's `README.md` "Pornirea unui proiect nou" and what LightsCheck's first commit
`e857e2a` kept and dropped:
1. Copy: `robocopy <wt>\ClaudeCodeStarterKit <wt>\AstroDarkroom /E /XD bin obj .vs .git worktrees StrykerOutput artifacts TestResults /XF .verify-stamp settings.local.json` (exit code below 8 is success).
2. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name AstroDarkroom` in the copy.
3. Delete: `GHID.md`, `monorepo/`, `AI Prompt to Generate Starter Project.md`, `.github/`,
   `bitbucket-pipelines.yml`, `scripts/rename.ps1`, `scripts/install-hooks.ps1`,
   `scripts/hooks/pre-commit`, `scripts/hooks/pre-merge-commit`, `scripts/hooks/commit-msg`,
   the kit's ADRs 0001-0035 (keep `0000-template.md`), the kit's plans (keep `docs/plans/README.md`),
   the kit-only tests `GitHookTests.cs`, `HookTests.cs`, `SyncTests.cs`, `ReleaseTests.cs`,
   `DecisionsCheckTests.cs`, `DesignCheckTests.cs`, `MergePublishTests.cs`, `ScriptRunner.cs`,
   and in `RepoInvariantTests.cs` the kit-only tests `KitGuideAndReadme_NameOnlyPathsThatExist`
   and `SyncedFiles_CiteTheKitsAdrsAndNoRuleNumbers` (take LightsCheck's
   `tests/LightsCheck.Tests/RepoInvariantTests.cs` as the model of what stays).
   The sample: `src/AstroDarkroom.Cli/` (whole project, removed from the `.sln` with
   `dotnet sln AstroDarkroom.sln remove`), `Stats.cs` and `NumberFile.cs` in Core,
   `StatsTests.cs`, `AcceptanceTests.cs`, `PropertyTests.cs`, `ValidationTests.cs`, the fixtures
   under `tests/testdata/fixtures/` (keep the folder's `README.md` and a `manifest.json` with an
   empty `files` list, as `check-testdata.ps1` expects), `FsCheck.Xunit` from
   `Directory.Packages.props`, R-001..R-004 and V-001 from `docs/requirements.md`,
   `docs/validation.json` (`"cases": []`) and `docs/validation.md` (say the launcher has no
   measured figures).
4. `kit.json`: `"kit": "../ClaudeCodeStarterKit"`; `solution`, folders and `largeEnv` as rename
   left them; `mutate.testProject` `tests/AstroDarkroom.Tests/`; `release.publish` and
   `release.zip` as in 5.4; `audit.alsoRead` `[]`.
5. `Directory.Build.props`: `<Version>0.1.0</Version>` (the first suite release, 1.0.0, is the
   owner's call), `<TargetFramework>net8.0</TargetFramework>`, and the kit's ADR 0034 lines
   `<RuntimeIdentifiers Condition="!$(MSBuildProjectName.EndsWith('.Tests'))">win-x64</RuntimeIdentifiers>`
   (no single-file publish, so no `EnableSingleFileAnalyzer`). Copyright line unchanged.
6. `CLAUDE.md`: the first paragraph ("Astro Darkroom is the launcher and the release of the five
   tools ..."), Layout (Core, Wpf, Tests, `Icon/`), Rules: "Core has no UI"; "The catalogue and
   kit.json agree" (every app in `AppCatalogue` is a component in `release.publish` and the
   reverse; `SuiteTests` checks it); "Romanian without diacritics in what the user sees, English
   everywhere else" (as AstroPhotoAnalyzer); "Same .NET and same packages across the suite"
   (`SuiteTests` checks the components' lock files). Read before touching: `docs/design/launcher.md`.
7. `README.md` in Romanian: what the suite is, the five tools in one folder, how a release is
   made (`/release`, the zip in `artifacts/`, upload to Bitbucket Downloads by hand), the tag name.
8. `CHANGELOG.md`: fresh, `[Unreleased]` / `### Added`: "The project, started from
   `ClaudeCodeStarterKit` on 2026-10-24 (ADR 0001)".
9. ADR 0001 "Adopt the starter kit process for the suite's launcher and release": what was
   dropped (sample, console, kit-only tests), why a kit project (one `/release` for the suite,
   the suite's version is `<Version>` here). ADR 0003 "Romanian without diacritics for the user,
   English for the process" (LightsCheck's 0005 is the model).
10. `tests/AstroDarkroom.Tests/`: copy and adapt LightsCheck's `LicenseHeaderTests.cs` (projects
    list: Core, Wpf, Tests; `.cs` and `.xaml`) and `VersionTests.cs` (`VersionInfo` in Core);
    `ThirdPartyTests` not needed (no packages).
11. `scripts/verify.ps1` green (step 0 compares with the kit: run `scripts/sync.ps1` if rename
    touched a shared file). Commit with `git -C <wt>/AstroDarkroom`.

### 5.2 Requirements, then tests, then Core (commit 2)

`docs/requirements.md`, status `accepted` until the test passes, then `implemented`:
- **R-001** The catalogue lists the five apps in workflow order with a Romanian title and a
  one-line description: DarksCheck ("Verifica dark-urile unei sesiuni sau ale unei biblioteci"),
  FlatCheck ("Verifica flat-urile unui obiectiv"), LightsCheck ("Verifica light-urile unei
  sesiuni: focus, trailing, dithering, drift"), ImageVisualCompare ("Compara doua variante ale
  aceleiasi procesari, pixel cu pixel"), AstroPhotoAnalyzer ("Verifica o imagine finala inainte
  de print"). Each names its window exe and, where one exists, its console exe.
- **R-002** An app is available when its window exe exists in the suite folder (the launcher's
  own folder); otherwise it is unavailable and the reason names the missing file.
- **R-003** Launching an available app starts its window exe with the suite folder as working
  directory; Core only plans the start (file name, working directory), the window executes it.
- **R-004** `release.json` beside the launcher gives the suite version and each component's
  version and `unreleased` flag; a missing or invalid file leaves them unknown, and the launcher
  still runs.
- **R-005** Every source file starts with the GPL notice (the copied `LicenseHeaderTests`).

Core (`src/AstroDarkroom.Core/`, no UI reference, every type and method with an XML doc comment):
`SuiteApp` (record: `Folder`, `Title`, `Description`, `WindowExe`, `ConsoleExe?`),
`AppCatalogue.Apps` (static, ordered as R-001), `SuiteFolder` (constructed with the base
directory; `Locate(SuiteApp)` -> `AppLocation(Path, IsAvailable, Reason)`), `LaunchPlan`
(`FileName`, `WorkingDirectory`) from `Launcher.Plan(SuiteApp, SuiteFolder)` (throws for an
unavailable app), `ReleaseInfo` + `ReleaseManifest.TryRead(path)` (System.Text.Json, tolerant),
`VersionInfo` (`Name = "Astro Darkroom"`, banner from `AssemblyInformationalVersion`, as
`MindfulnessReminder/src/MindfulnessReminder.Core/Version.cs`) and `LicenseInfo`.
Tests `R001_...`-`R004_...` in `tests/AstroDarkroom.Tests/` use a temp folder with empty files
named like the exes; `SuiteTests` (not requirement-numbered) adds:
- every `AppCatalogue` app's `Folder` is a component of `kit.json`'s `release.publish` (an entry
  whose `project` starts with `../`) and every component is in the catalogue;
- each app's `WindowExe` / `ConsoleExe` equals the `<AssemblyName>` (default: the project file
  name) of the component's publish csproj plus `.exe`;
- in every component, every `packages.lock.json` under its `kit.json` `codeFolders` has only
  keys starting with `net8.0`, and a package id resolved in two or more components has one
  resolved version. The test reads `../<Component>/kit.json` with System.Text.Json.
  Test-project lock files are excluded: they do not ship.

### 5.3 The window (commit 3)

`src/AstroDarkroom.Wpf/` (`WinExe`, `net8.0-windows`, `UseWPF`, `AssemblyName AstroDarkroom`,
`ApplicationHighDpiMode PerMonitorV2`, icon lines as in `LightsCheck/src/LightsCheck.Wpf/LightsCheck.Wpf.csproj`
17-18 and 32, `LICENSE` linked and copied as in MindfulnessReminder's App csproj). Do not name
the project `.App`: the root `.gitignore` ignores `*.app`.
- `MainWindow`: `Title="Astro Darkroom"`, one row per catalogue app: title, description,
  console hint ("consola: darkscheck.exe"), the component version from `release.json` when known,
  a button "Deschide" (disabled with tooltip "Lipseste <exe>" when unavailable). Click:
  `Process.Start(new ProcessStartInfo(plan.FileName) { UseShellExecute = true, WorkingDirectory = plan.WorkingDirectory })`.
  Bottom: "Despre" (F1) opening `AboutWindow` "Despre Astro Darkroom": banner, copyright, the GPL
  warranty sentence, the suite date and the component versions (with "modificari nelansate" when
  `unreleased`), Close `IsDefault` + `IsCancel`. Model: `MindfulnessReminder/src/MindfulnessReminder.App/AboutWindow.xaml(.cs)`.
- `Icon/make_icon.py` copied from `LightsCheck/Icon/make_icon.py` with a new drawing (a dark
  red-brown rounded square, the darkroom's safelight, with a white four-spike star); it writes
  `Icon/astrodarkroom.ico`. Run it once; commit the `.ico` and the script.
- No Wpf test project: the window is a thin adapter over Core (MindfulnessReminder's ADR 0008 is
  the precedent; say so in ADR 0001).
- `docs/design/launcher.md` with a `**Covers:**` sentence naming the Core and Wpf files; index
  row in `docs/design/README.md`.

### 5.4 The suite's release (commit 4, with the measurement)

`kit.json` `release`:
```json
"release": {
  "publish": [
    { "project": "src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../DarksCheck/src/DarksCheck.Cli/DarksCheck.Cli.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../DarksCheck/src/DarksCheck.Wpf/DarksCheck.Wpf.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../FlatCheck/FlatCheck/FlatCheck.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../FlatCheck/FlatCheck.Wpf/FlatCheck.Wpf.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../LightsCheck/src/LightsCheck.Cli/LightsCheck.Cli.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../LightsCheck/src/LightsCheck.Wpf/LightsCheck.Wpf.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../ImageVisualCompare/src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../AstroPhotoAnalyzer/AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] },
    { "project": "../AstroPhotoAnalyzer/AstroPhotoAnalyzer.Wpf/AstroPhotoAnalyzer.Wpf.csproj", "output": "", "args": ["-r", "win-x64", "--self-contained"] }
  ],
  "zip": "AstroDarkroom-{version}-win-x64.zip"
}
```
No single-file publish in the suite: a single-file exe carries its own runtime, which the shared
folder already has; the components' own releases keep their single-file entries.

**Measure** (the kit's rule "Measured numbers"), in the worktree cut after session A merged:
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.1.0 -PublishOnly`
and paste its output into ADR 0002 and this plan's deviations: collisions (expected none; the
candidates are `LICENSE`, `THIRD-PARTY-NOTICES.md`, `lensfun/`), file count, folder size, zip
size, `release.json` content. A collision: stop and report to the owner with the file and the
two components; the usual answer is an `output` subfolder for that entry (then the catalogue's
exe name carries the subfolder, e.g. `flatcheck/FlatCheck.Wpf.exe`) or, for a notices file, one
suite-level file instead. Then start `artifacts/0.1.0/AstroDarkroom.exe`: every row shows its
version, every "Deschide" opens its app; rename one exe away and check the row turns unavailable.

**ADR 0002** "The suite is one flat self-contained folder on .NET 8, built from main as it is":
the owner's decisions, the measured numbers, the alternatives (a subfolder per app: two or three
copies of the runtime and no shared libraries; single-file exes: five runtimes; components from
their tags: see the kit's ADR 0036; a scan of the folder instead of a catalogue: no descriptions
and stray exes listed).

**`docs/skills.md`**: `## /release` - at step 2, list the components whose `release.json`
would say `unreleased` (read their CHANGELOGs) and say so to the owner, who may release them
first; at step 5, the flat folder, `release.json`, the zip in `artifacts/`, the tag
`astrodarkroom-vX.Y.Z`; at step 7, the zip and its `.sha256` go to Bitbucket Downloads by hand.
`## /new-feature` - a new app in the suite is one `release.publish` entry, one `AppCatalogue`
line and the `SuiteTests` run.

`CHANGELOG.md` `[Unreleased]` / `### Added`: the launcher (R-001..R-004) and the suite's release
(ADR 0002). Requirements to `implemented`. `/wrap-up`, propose the merge. The first real
release, `/release` for 1.0.0 on `main`, is the owner's next step, in a session opened in
`E:\Photografie\Code\AstroDarkroom`.

## Verification

- Parts 1-3: `scripts/verify.ps1` green in the project, lock files diff shows only the key
  rename, the Wpf app starts from `dotnet run --project src/<X>.Wpf` (IVC: `dotnet run --project src/ImageVisualCompare.Wpf`).
- Part 4: kit `scripts/verify.ps1` green including `MergePublishTests` and the new `ReleaseTests`;
  `scripts/sync.ps1 -Check` clean in all six projects; a manual `release.ps1 -Version 0.2.0 -WhatIf`
  in the kit's worktree is refused by the branch check (unchanged behaviour), and
  `-Version 0.2.0 -PublishOnly` in the kit worktree publishes `src/Starter.Cli` to
  `artifacts/0.2.0/` with a `release.json` of one component (delete `artifacts/` after).
- Session A's wrap-up: the gate green in the kit and in all six projects of the worktree.
- Part 5: `scripts/verify.ps1` green; the measurement of 5.4 pasted; the launcher opens each app
  from `artifacts/0.1.0/`.

## Known and left out

- `DarksCheck/bitbucket-pipelines.yml` and `LightsCheck/bitbucket-pipelines.yml` use the
  `sdk:9.0` image, which has no .NET 8 runtime for `dotnet test`; they are inert (Bitbucket reads
  only a root file, which does not exist). Left as they are; a root pipeline is a task of its own.
- Test-package versions differ between projects (Test.Sdk 17.8.0 / 17.12.0 / 18.10.1); they do
  not ship, so the suite does not align them.
- No code signing: the exes will trip SmartScreen on other machines. A certificate is the owner's
  purchase; a later task adds the signing step to `release.ps1`.
- Uploading the zip to Bitbucket Downloads stays by hand.
- .NET 10 for all projects: later, one task per project, when the owner says.
- The components' own `/release` entries (single-file consoles, `console/` and `wpf/`) are unchanged.

## After approval

Save this file as `ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`
in the main checkout and ask the owner to commit it on `main` by hand
(`git -C E:\Photografie\Code\ClaudeCodeStarterKit add docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`
then `git -C E:\Photografie\Code\ClaudeCodeStarterKit commit -m "Plan the Astro Darkroom suite: .NET 8 moves, bundled release, launcher"`;
the hook runs the kit's gate), so both sessions' worktrees, cut from `main`, carry it. Each
session appends its "What deviated (session A|B)" section at wrap-up.

## What deviated

### Session A (2026-10-24, branch `claude/Prepare-the-Astro-Darkroom-suite`)

Parts 1-4 done as planned: DarksCheck (ADR 0018), LightsCheck (ADR 0017) and
ImageVisualCompare (ADR 0037) on net8.0 / net8.0-windows, each gate green and each window
started from its net8 build; the lock files of all three changed only their framework keys
(`net9.0...` -> `net8.0...`), no package version moved, so nothing stopped the work. Kit ADR 0036,
`merge-publish.ps1`, the `release.ps1` changes and the sync, one commit per project. Deviations:

- **The zip is written entry by entry.** Measured in this session: under Windows PowerShell 5.1
  `ZipFile.CreateFromDirectory`, which the plan chose, writes backslashes into the entry names
  (`lensfun\a.xml`), as `Compress-Archive` does. `release.ps1` opens a `ZipArchive` and adds
  each file with `CreateEntryFromFile` under a forward-slash name; the ADR says so.
- **Step 5 empties `artifacts/<version>/` first.** Not in the plan: a previous run's files would
  be compared with this run's and reported as collisions, and would end up in the zip.
- **Paths in step 5 use the platform separator.** The kit's Bitbucket template runs the gate
  under pwsh on Linux, and the new `-PublishOnly` test runs a real publish; the old code's
  `artifacts\` would be part of a file name there. Not verified on Linux: no pwsh on this
  machine.
- **One more `ReleaseTests` case:** `-PublishOnly` publishes a one-file library
  (`net$(BundledNETCoreAppTargetFrameworkVersion)`, offline) into an `output` subfolder, and
  checks `release.json`, the zip's entry names and its `.sha256`. The tests write their
  `kit.json` themselves; `ScriptRunner.KitJson` is unchanged.
- **`-WhatIf` lists a component that is not ready as `NOT READY`, then refuses**, so the
  listing and the refusal come from one run; a plain run refuses at once.
- **The release skill** also gained, at step 4, naming the components with unreleased changes,
  and at step 6, that each component's folder must still be clean.
- **Docs name `artifacts/X.Y.Z/release.json`**, not a bare `release.json`, which
  `KitGuideAndReadme_NameOnlyPathsThatExist` reads as a file the kit should have.
- **Names:** kit ADR 0036's file name is shortened
  (`0036-a-release-stages-merges-by-hash-bundles-and-zips.md`) for the Windows path limit in
  nested worktrees; ImageVisualCompare's guard test is one theory over its three project files.
- **Manual checks in the kit worktree:** `-Version 0.2.0 -WhatIf` refused as a linked worktree;
  `-PublishOnly` published `src/Starter.Cli` (10 files, 0.2 MB) with a `release.json` of one
  component. With a temporary `kit.json` publishing the CLI twice into one folder
  (framework-dependent, then self-contained) it refused with four collisions
  (`starter.deps.json`, `starter.dll`, `starter.pdb`, `starter.runtimeconfig.json`) and copied
  nothing; with `output: "sc"` on the second it merged 203 files, 74.7 MB, zip 33.3 MB, and the
  `.sha256` matched `sha256sum`. `kit.json`, the lock files and `artifacts/` were restored after.

**After `/code-review` (2026-10-25, ten findings, all fixed on the branch at the owner's word):**

- `-PublishOnly -WhatIf` published half for real (dotnet publish, the child merge script,
  `release.json` and the zip ignore `-WhatIf`): it now lists the components and what it would
  publish, and stops.
- Under Windows PowerShell 5.1 git's stderr, even sent to `$null`, stopped the script (measured:
  `git -C C:\ log ... 2>$null` threw): git runs through `Invoke-GitText`, which leaves the commits
  empty outside git.
- The zip and the size line skipped hidden files that the merge copied: `-Force`.
- A `release.json` an entry published at the root is a collision instead of being overwritten.
- `merge-publish.ps1` exits 2 on an error that is not a collision, and a failed publish is no
  longer reported with the collision advice.
- `release.json` gains `clean` per component, the two version files a full release has changed
  aside. Writing it found a PowerShell trap: `if ... else { @() }` yields `$null`, and `$null`
  piped through `ForEach-Object` made one bare `:(exclude)` pathspec that excluded everything.
- Tests: ten more `ReleaseTests` and `MergePublishTests` cases (a second component in
  `release.json`, two entries that collide through `release.ps1`, and the cases above); each
  failed before its fix except the two-entry collision, which is coverage.
- The `<Version>` and `[Unreleased]` patterns are defined once; the component check reuses
  `Get-ComponentInfo`; `merge-publish.ps1` hashes only files of equal length.
- **Measured on .NET 8** (`docs/validation.md`, History, of the three projects): DarksCheck 10/10,
  LightsCheck 13/13, ImageVisualCompare 4/4 cases pass, no figure moved, IVC's memory within
  3.3 MB. DarksCheck's working set, same code on both runtimes, two runs each: library 3.13 /
  3.16 GB (.NET 9) and 3.22 / 3.29 GB (.NET 8), 24-27 s; Smida 5.75 / 6.09 GB and 5.72 / 5.87 GB,
  63-65 s. Smida is above ADR 0007's 4.5-4.7 GB and 35 s on both runtimes alike: the console
  does more on that folder than it did on 2026-10-11; not looked into, ADR 0007 left as it is.
