# 2026-10-09 Adopt the ClaudeCodeStarterKit process

Status: **approved** by the owner on 2026-10-09 ("Aprob planul, trei commit-uri, publish
framework-dependent"), with two instructions, both of which confirm the body below:

- **Three commits**, one per layer, as the body proposes.
- **The release publishes framework-dependent**, as ADR 0002 recommends; the self-contained
  `win-x64` alternative is not taken, so the kit's ADR 0034 does not apply.

Written on 2026-10-09 in the main checkout (`main` at 34db8b9; this project's last commit is
2be5361 of 2026-09-14) for an implementing session that is **not** the one that wrote it.
Everything the implementing session needs is in this file or in a file this one names; it does
not need the conversation that produced it. Where this plan and a file it names disagree, the
file wins and the disagreement goes under "What deviated".

The implementing session fills "What deviated" at its wrap-up. The file was written in the main
checkout and is moved into the task's worktree before the first step.

## Goal

Bring MindfulnessReminder onto the process the kit at `E:\Photografie\Code\ClaudeCodeStarterKit`
defines (`GHID.md`, "Migrarea unui proiect existent"), in the three layers the guide describes,
exactly as FlatCheck was migrated on 2026-09-25 and AstroPhotoAnalyzer and ImageVisualCompare on
2026-10-02, and bring the documentation to the state it would have had if the process had been
used from the start: `CLAUDE.md` holds only the project's rules; the behaviour is in
`docs/requirements.md` with IDs; the decisions are in `docs/decisions/`; the explanations are in
`docs/design/`; `docs/validation.json` exists and says, honestly, that this project has no figure
measured on data. **Nothing written today is deleted: it is moved.**

The difference from the three earlier migrations: the document to split is `CLAUDE.md` itself
(451 lines), as in ImageVisualCompare, and it is already organised by area: a file map
("Structure"), a "Conventions" list, seven topical sections that each record the decisions of one
feature and the defect behind each ("The periodic reminder", "Pausing for what the user is
doing", "Two reminders, two switches, two messages", "The tray tooltip", "Windows the app
shows", "The dark theme", "Settings storage"), "Build & test", "Versioning", "Licensing", and a
"Committing" rule the kit supersedes. There is no `docs/` folder, no test data at all (every test
runs on fakes: `FakeRegistryStore`, the fakes inside `ReminderCoordinatorTests`), and **every
file is already English, the README included**. The build has 0 warnings and `dotnet format`
finds nothing. The version is already in `Directory.Build.props`, where the kit's `release.ps1`
reads it. There is nothing to measure on real data, so there is no `ValidationTests` and no
`TestData.cs` here, and no large file to copy. This is the smallest of the four migrations in
code and the second largest in documentation (451 lines of `CLAUDE.md` plus a 291-line README
with its own "Design decisions" table): one long session, or two.

## Read first (the implementing session, before any edit)

1. `../ClaudeCodeStarterKit/.claude/rules/kit/process.md` - the rules the project follows from
   the first commit. Every rule is cited by its name in quotes, never by a number.
2. `../ClaudeCodeStarterKit/README.md` - "Ce e unde" and "Ce e impus mecanic".
3. `../ClaudeCodeStarterKit/GHID.md`, section "Migrarea unui proiect existent" (Romanian).
4. `../ImageVisualCompare/CLAUDE.md` - the template for the new `CLAUDE.md` (Appendix A here
   is already written in that shape).
5. `../ImageVisualCompare/docs/plans/2026-10-02-adopt-the-starter-kit.md`, whole, with its
   "What deviated" and the docs-health report at the end: the same migration, the same shape,
   one project over. Its ADRs `../ImageVisualCompare/docs/decisions/0001` to `0007` are the
   models for ADRs 0001 to 0006 here; its `0017` (a retroactive one, with the "Retroactive:"
   opening and the `Source:` line) is the model for 0007 to 0033.
6. `../ImageVisualCompare/docs/design/README.md` (the index and the Covers rule),
   `../ImageVisualCompare/docs/design/shell.md` (the header boilerplate of a moved note), and
   the header of `../ClaudeCodeStarterKit/scripts/check-design.ps1` (the exact Covers grammar
   the gate parses).
7. `../ImageVisualCompare/docs/requirements.md` (the header and the row format),
   `../ImageVisualCompare/docs/validation.md` and `validation.json` (the shape; here they
   describe zero cases), `../ImageVisualCompare/tests/ImageVisualCompare.Tests/RepoInvariantTests.cs`
   (the model for the test file of layer 3), `../ImageVisualCompare/docs/skills.md`,
   `../ImageVisualCompare/kit.json`, `../ImageVisualCompare/.claude/settings.json` (its
   `$comment`).
8. `../ClaudeCodeStarterKit/scripts/kit-config.ps1` (every `kit.json` key),
   `check-decisions.ps1` header (the ADR header format the gate checks), `check-testdata.ps1`
   (what it does with a manifest that lists no file), `release.ps1` header (it reads `<Version>`
   from `Directory.Build.props`, which is already the case here), `sync.ps1` header.
   `../ClaudeCodeStarterKit/docs/decisions/0000-template.md`, `0025`, `0029`, `0031`, `0032`,
   `0034`.
9. This project's `CLAUDE.md` (451 lines; read it twice: once as a whole, once section by
   section against the tables of sections 2.2 and 2.3 below), `README.md` (291 lines),
   `Directory.Build.props`, `.gitignore` (note the `!src/MindfulnessReminder.App/` line and its
   comment: the root `.gitignore` has a `*.app` rule that would hide the application project),
   the three `.csproj` files, and every `.cs` and `.xaml` file under `src/` and `tests/` (about
   7700 lines with the licence headers; the requirement rows are written from them, and the
   Covers sentences name them).

## Measured before planning (2026-10-09, main at 34db8b9)

| | MindfulnessReminder |
| --- | --- |
| build Release | **0 warnings**, 15 s; `TreatWarningsAsErrors`, `Nullable` and `ImplicitUsings` are set in each of the three `.csproj` files, not in `Directory.Build.props`; no `EnforceCodeStyleInBuild`, no `AnalysisLevel` |
| tests | **215** (xUnit `Fact` and `Theory` rows), green, 70 ms; one test project, `net8.0`, references Core only |
| `dotnet format --verify-no-changes`, no `.editorconfig` | **0 findings** |
| the same with a whitespace-only `.editorconfig` plus `IDE0005` (Appendix D.4) | **6 IDE0005** findings (unused usings) in 5 files, all in `src/MindfulnessReminder.App/`: `App.xaml.cs`, `BreakWindow.xaml.cs`, `DarkMenuRenderer.cs`, `TrayIconHost.cs`, `WindowsActivityMonitor.cs`; nothing else, and the kit's full `.editorconfig` finds the same 6 and nothing more |
| build with `GenerateDocumentationFile` | **394 CS1591** (missing XML comment) and nothing else; the kit mutes CS1591 for this reason |
| build with `AnalysisLevel=latest-recommended` | **374** warnings: 352 CA1707 (underscores in test names, the project's naming style), 6 CA1861, 4 CA1001, 4 CA1822, 4 CA1859, 2 CA1305, 2 CA1716; not taken (ADR 0003) |
| line endings | 63 of the 65 tracked files are LF in the index and on disk; `MindfulnessReminder.sln` is CRLF on disk and LF in the index (the root `.gitattributes` says `* text=auto eol=lf`, so git shows no difference and a fresh worktree is LF); `Assets/app.ico` is binary |
| `CLAUDE.md` | 451 lines, 13 sections: "Structure" (a file map of the three projects, one line per file), "Conventions" (five), "The periodic reminder" (four load-bearing decisions and two smaller rules), "Pausing for what the user is doing" (two signals, five rules, `--detect`), "Two reminders, two switches, two messages" (four decisions and `--preview`), "The tray tooltip", "Windows the app shows" (a table and the Enter rule), "The dark theme" (three surfaces, six rules, About), "Settings storage" (eight values), "Build & test", "Versioning", "Licensing", "Committing" |
| `docs/` | none; no ADRs, no requirements with IDs, no changelog, no validation |
| `README.md` | 291 lines, **English**: the user documentation ("Behaviour", "Two reminders, two messages", "Periodic reminders", "Pausing for what you are doing" with its `--detect` timeline, a "Design decisions" table of fourteen rows, "Layout", "Build and run", "Deployment note", "Versioning", "Licence"). It states behaviour with the exact tooltip strings and the `--detect` output, and `CLAUDE.md` names it as "**the** user-facing documentation, and the single source for it" |
| version | `<Version>1.4.0</Version>` in `Directory.Build.props` with `<Copyright>` and `<PackageLicenseExpression>`, **already where the kit's `release.ps1` reads it**; no tag. History: 1.0.0 (d3ccd6c, 2026-09-01, licensing and versioning), 1.1.0 (b6554d5, 2026-09-08, the periodic reminder), 1.2.0 (89de6c8, 2026-09-08, pausing for a call or full screen), 1.3.0 (432edfd, 2026-09-08, the dark theme), 1.4.0 (2be5361, 2026-09-14, two switches and two messages). Eight commits in all; the two before 1.0.0 are 61a0121 (2026-08-28, the base) and 51cb690 (2026-09-08, the reminder on the primary screen) and f3c8cba (2026-09-08, the message) |
| packages | three test packages with versions in the test csproj (`Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2); **none** in Core or App (the rule "Zero external dependencies"); no `Directory.Packages.props`, no lock files; `global.json` says SDK `9.0.0` with `rollForward: latestMajor` |
| target framework | `net8.0` (Core, Tests) and `net8.0-windows` (App) with `UseWPF` and `UseWindowsForms`; the installed SDK is 9.0.318 and builds it. **Stays `net8.0`** (the guide: "Un proiect pe .NET 8 ramane pe .NET 8") |
| comment language | English in every `.cs` and `.xaml` file, in the csproj comments, in `Directory.Build.props` and in `README.md` (the project's rule "All text is in English"); nothing to translate |
| citations of `CLAUDE.md` in code | one: the comment in `src/MindfulnessReminder.App/AboutWindow.xaml` (lines 33 to 37) says `See "The dark theme" in CLAUDE.md.` Repointed in layer 2 (section 2.8). `grep -rn CLAUDE.md src tests README.md` finds nothing else; `README.md` does not name it |
| git hooks | installed at the root of the `photo` repository (`.githooks/`, `core.hooksPath` absolute); they pick up this project as soon as `scripts/verify.ps1` exists |
| stray files | `.vs/` only, ignored by the root `.gitignore`; no `.github/`; the project's own `.gitignore` (four lines plus the `!src/MindfulnessReminder.App/` exception) is kept and extended |
| test data | none, and nothing to measure: the application has no numeric output and reads nothing from a file. Every test runs on fakes. The numbers the code carries (the 63-character tray limit, the 50-character message, 1 to 480 minutes, 5 to 300 seconds, 2 s debounce, 10 s refresh, 600 ms show delay) are constants pinned by unit tests, not figures measured on data (ADR 0005) |
| Claude memory | no note about this project; nothing to fold in |
| worktrees | two other sessions' worktrees exist under `AstroPhotoAnalyzer/` and `SirilCleanup/`; none under this project |

## Decisions (each an ADR, accepted with the plan)

The migration's own, ADR 0001 to 0006:

1. **0001 Adopt the kit's process; `CLAUDE.md` splits into requirements, decisions and design
   notes.** The analog of ImageVisualCompare's 0001. The seven topical sections and the
   "Conventions", "Versioning" and "Licensing" sections become retroactive ADRs (0007 to 0033)
   and design notes in `docs/design/` (moved unchanged); "Structure" becomes
   `docs/design/code-map.md`; the behaviour the README and the code describe becomes
   `docs/requirements.md` with IDs; `CLAUDE.md` is cut to the rules, the commands and the
   pointers (Appendix A). The section "Committing" ("**Do not commit.** Make the changes, run
   the tests, and hand over a commit message ... a short lowercase subject line, then a body
   that explains *why*") is superseded by the kit's rule "Commits and merges": the assistant
   commits on its `claude/` branch, one line, no body; the ADR quotes the old text in its
   Context. The convention "Never bump the version and never commit" splits: the version half
   is the kit's rule "Version and year are the owner's" (the kit's ADR 0029) and is not written
   again; the commit half is the superseded text. The convention "All text is in English" stays
   a `CLAUDE.md` rule (ADR 0004).
2. **0002 The layout stays `src/` and `tests/`; the kit's values for this project are in
   `kit.json`; a release publishes the application framework-dependent, as the README's
   deployment note does.** The layout already matches the kit's default. Values (Appendix B):
   the solution, no test filter (there is no validation category to keep out of the gate, ADR
   0005), code folder `src/`, test folder `tests/`, test data under `tests/testdata/` with
   `MINDFULNESS_TESTDATA_LARGE` as the override `kit-config.ps1` requires a name for (nobody
   sets it; there is no large file), Stryker from `tests/MindfulnessReminder.Tests/`, one
   publish target: `src/MindfulnessReminder.App/MindfulnessReminder.App.csproj` into
   `artifacts/<version>/` itself with **no extra arguments**, which is `dotnet publish -c
   Release` of a framework-dependent, any-CPU build, the same publish the README's "Deployment
   note" documents (`dotnet publish src\MindfulnessReminder.App -c Release -o ...`). Because no
   runtime is named, the kit's ADR 0034 does not apply: no `<RuntimeIdentifiers>` and no
   `<EnableSingleFileAnalyzer>` are declared, and the lock files carry no runtime section. The
   auditor may also read `README.md` (the user documentation, which states behaviour and is not
   code). Alternative, for the owner to choose at approval if they prefer it: a self-contained
   `win-x64` publish (`"args": ["-r", "win-x64", "--self-contained"]`), which then needs the two
   lines of the kit's ADR 0034 in `Directory.Build.props` and a sentence in the README's
   deployment note. Recommended: framework-dependent, because the README already documents it,
   a tray application is installed once on one machine that has the runtime, and it keeps the
   lock files and `Directory.Build.props` untouched by the publish.
3. **0003 Gate strictness: what is on stays on; `dotnet format` adds whitespace and `IDE0005`;
   the build writes the documentation file; the kit's code-style rules and the analyzers stay
   off; the SDK is pinned.** `TreatWarningsAsErrors` and `Nullable` are on in every csproj and
   the build has 0 warnings, so nothing is relaxed; they move to `Directory.Build.props` (one
   place, the kit's shape) with `EnforceCodeStyleInBuild`, `GenerateDocumentationFile` and
   `NoWarn` CS1591 (394 missing-comment warnings on 2026-10-09; comments here are for the
   "why", as the kit says), and the csproj files lose the lines that are now inherited. The
   `.editorconfig` of Appendix D.4 holds whitespace rules and `IDE0005`: 6 findings in 5 files
   on 2026-10-09, removed by one `dotnet format` pass whose file count goes in the ADR. The
   kit's `csharp_prefer_braces`, `var` and namespace rules are not taken (not measured as
   findings here, since the kit's full `.editorconfig` reported only the same 6 IDE0005; they
   would still be new rules nobody asked for). `AnalysisLevel=latest-recommended` is not
   enabled: 374 findings, 352 of them CA1707 for the underscores every test name in this project
   uses; a style pass is a task of its own. Reproducibility from the kit in full: `global.json`
   pinned to `9.0.318` with `latestPatch` (it said `9.0.0` with `latestMajor`; the target
   framework stays `net8.0`, which that SDK builds), `Directory.Packages.props` with the three
   test packages at their current versions, `packages.lock.json` per project, `Deterministic`.
4. **0004 English everywhere, README included: the project's rule "All text is in English"
   extends the kit's rule "Languages".** The kit allows a Romanian README; this project's
   README is English and stays English, by the convention its `CLAUDE.md` has carried since the
   base version (61a0121): "All text is in English - UI strings, comments, documentation. (The
   sibling `FlatCheck` tool in this repo is Romanian; that convention is its own, do not carry
   it over.)" Nothing is translated in this task. The rule stays in `CLAUDE.md`, naming the kit
   rule it extends; this ADR is both the migration's confirmation and the retroactive record,
   so it is not repeated among 0007 to 0033. The development section added to the README
   (Appendix D.11) is therefore English too, unlike ImageVisualCompare's.
5. **0005 No validation figures: `docs/validation.json` lists no case, and there is no
   `ValidationTests`.** The application has no numeric output and no input data: it shows a
   window, keeps a deadline, reads two registry keys. The numbers in the code are constants
   (`TrayTooltip.MaxLength` 63, `ReminderSettings.MaxMessageLength` 50, the bounds 1 to 480 and
   5 to 300, `DefaultDebounce` 2 s, `DefaultActivityRefresh` 10 s, `DefaultShowDelay` 600 ms)
   and the unit tests pin them; a `validation.json` case would restate a unit test. The file
   exists with `"cases": []` so that the kit's skills (`/validate`, `/release` step 3,
   `/wrap-up` step 2) find it and do nothing, `docs/validation.md` says why it is empty and what
   would become a case (a figure measured on a machine, such as the registry walk time of
   `WindowsActivityMonitor.Read`, if one is ever needed), `tests/testdata/manifest.json` lists
   no file, and `kit.json` has no `testFilter`. `RepoInvariantTests` keeps a test that reads
   the file and checks the case IDs are unique, so the shape is guarded when the first case
   arrives. Alternative rejected: a `ValidationTests` class with no cases, which xUnit would
   report as a test class that runs nothing; and a case on the `--detect 60` timeline, which
   needs a human to launch a game.
6. **0006 The changelog starts at 1.4.0; the git hooks are not copied; root CI is deferred;
   nothing is deleted.** `CHANGELOG.md` (Appendix D.5) opens with `[Unreleased]` and one
   section `[1.4.0] - 2026-09-14` summarising what the eight commits built, with the five
   version steps named; earlier detail is in `git log`. The git hooks are the root's (the kit's
   ADR 0016). CI at the root of `photo` is a repository change, left for a task of its own
   (FlatCheck ADR 0007, AstroPhotoAnalyzer ADR 0006, ImageVisualCompare ADR 0007). There is no
   stray file to delete; the project's `.gitignore` keeps its `!src/MindfulnessReminder.App/`
   line, without which the root's `*.app` rule hides the application (its own comment says so).

Retroactive, ADR 0007 to 0033, written from the sources named (each says "Retroactive:
recorded on 2026-10-09 from <source>; the decision itself dates from <commit>" in its Context,
and ends with a `Source:` line). The sources are this project's `CLAUDE.md` as it is today
(section names below are its headings), the README (its "Design decisions" table is cited by
its first column), and the doc comments of the files named. The commit each decision dates
from is given by version step; confirm with `git log -S'<a phrase of the text>' -- CLAUDE.md`
where it matters:

| ADR | Title (short statement of the decision) | Sources |
| --- | --- | --- |
| 0007 | Zero external dependencies: WPF for the windows and Windows Forms purely for `NotifyIcon`, both from the WindowsDesktop shared framework; WPF having no numeric spinner is not a reason to add a package, so the settings dialog hand-validates a `TextBox` | `CLAUDE.md` first paragraph (".NET 8, **zero external dependencies**") and "Conventions" ("Zero external dependencies"); README "Design decisions" rows "UI" and "Settings window"; the comment in `MindfulnessReminder.App.csproj` ("The hybrid"); 61a0121 |
| 0008 | Logic in Core, adapters in App: every rule lives in `MindfulnessReminder.Core`, which has no UI dependency and is what the test project references; the App is a thin adapter layer that decides nothing and is not unit-tested; each seam (`IPeriodicTimer`, `IClock`, `ISessionMonitor`, `IReminderPresenter`, `IRegistryStore`, `IAutoStartManager`, `IActivityMonitor`) exists so a rule can be tested off a real machine and has one production implementation | "Structure" (the two project paragraphs and the seams bullet), "Conventions" ("Logic in Core, adapters in App"); README "Layout"; 61a0121 |
| 0009 | Every source file, `.cs` and `.xaml` alike, starts with the GPL notice, `//` for C# and one `<!-- -->` block for XAML, kept alive by `LicenseHeaderTests` with an explicit project list; XML comments cannot contain `--`, so the em-dash of C# comments is reworded in XAML | "Conventions" ("Every source file starts with the GPL notice"), "Licensing" first bullet; `LicenseHeaderTests` class summary; d3ccd6c |
| 0010 | GPLv3-or-later: About displays the copyright and the warranty disclaimer (§5(d)) because the reminder window cannot; `<Copyright>` in `Directory.Build.props` is the one source, read back through `AssemblyCopyrightAttribute` by `LicenseInfo.Copyright`, with a literal fallback; `LICENSE` is copied next to the executable by the App csproj so About's "next to the executable" stays true after a publish | "Licensing" (the three other bullets); README "Licence"; `LicenseInfo.cs` comments; the `None Include="..\..\LICENSE"` item and its comment in `MindfulnessReminder.App.csproj`; `LicenseHeaderTests` (`The_repo_ships_the_full_gpl_text`, `The_app_copies_the_license_next_to_the_executable`); d3ccd6c |
| 0011 | One source of truth for the version: `<Version>` in `Directory.Build.props`, read back from `AssemblyInformationalVersion` with the `+sourcerevision` suffix trimmed, no literal in code, no `<Version>` in any csproj (`No_project_overrides_the_shared_version`); shown in the tooltip, About and the crash dialog; bumped by the owner (the kit's ADR 0029, cited, not rewritten) | "Versioning"; README "Versioning"; `Version.cs` comments; `VersionTests`; d3ccd6c |
| 0012 | One timer, ticking every second, driving a deadline: `IPeriodicTimer` is not armed for the interval; it ticks at `DefaultTickInterval` (1 s) and `IntervalSchedule` holds a deadline each tick is compared against, because the tooltip countdown needs the tick anyway and a one-shot timer would have needed a second timer beside it | "The periodic reminder", first decision; README "Periodic reminders" first bullet and "Design decisions" row "Periodic schedule"; `ReminderCoordinator.cs` comment on `DefaultTickInterval`; b6554d5 |
| 0013 | The deadline is on the wall clock, never a count of elapsed ticks: a `DispatcherTimer` does not tick while the machine sleeps, so a reminder that came due during sleep fires on the first tick after waking; the clock may move backwards, so `TryConsumeDue` restarts on a deadline further out than a whole armed span could put it, compared against `_armedSpan` and not the interval (a retry gap longer than the interval is not a clock jump); the other direction has no guard and must not get one | "The periodic reminder", decisions two and four; "Pausing", rule "`IntervalSchedule` compares the backwards-clock guard against `_armedSpan`"; README "Periodic reminders" second bullet; `IntervalSchedule.cs` comments; b6554d5, 89de6c8 |
| 0014 | A lock suspends the schedule and an unlock restarts it in full, whatever the lock lasted; the restart in `HandleUnlock` comes before the debounce and the already-showing check; `TryConsumeDue` re-arms as it returns true, so a due reminder that cannot be shown leaves a sane schedule; `ApplySettings` restarts the countdown from zero | "The periodic reminder", decision three, the two smaller rules and the settings paragraph; README "Periodic reminders" third bullet; `IntervalSchedule.cs` (`Suspend`, `Resume`, `Apply`); b6554d5 |
| 0015 | The coordinator is the single place that decides when a reminder appears: both sources (an unlock and the periodic schedule) go through it, so the debounce (`DefaultDebounce`, 2 s), the already-showing raise instead of a second window, and the lock that never shows a reminder are written once | "Structure" (`ReminderCoordinator.cs`); `ReminderCoordinator.cs` class summary and the `DefaultDebounce` comment; `ISessionMonitor.cs` comment on `Locked`; 61a0121, b6554d5 |
| 0016 | Being on a call is read from the device, not the window title: the per-app capture usage Windows records under `HKCU\...\CapabilityAccessManager\ConsentStore\{microphone,webcam}` with `LastUsedTimeStop == 0` while the stream is open, the same record its own privacy indicator reads; a table of window titles was considered and rejected (an entry per application, broken by each release, misses a Slack huddle) and is not to be reintroduced | "Pausing for what the user is doing", second paragraph; README "How it detects them" first two paragraphs and "Design decisions" row "In a call"; `WindowsActivityMonitor.cs` comments; 89de6c8 |
| 0017 | Full screen is `SHQueryUserNotificationState`, one P/Invoke, mapped in Core by `UserNotificationState.FromShellState` so the table is testable; `QUNS_NOT_PRESENT` must not suppress (it is what Windows reports behind a lock screen and for a moment after an unlock); `QUNS_QUIET_TIME` is mapped and unused; an unknown state holds nothing back; the fallback if it proves lossy is `GetForegroundWindow` plus `GetWindowRect`, and `--detect` is how to find out | "Pausing", third paragraph and the first rule; README "How it detects them" last three paragraphs and "Design decisions" row "Full screen"; `ForegroundState.cs` comments; 89de6c8 |
| 0018 | A periodic reminder that is held back is postponed by the retry gap and looked at again, with no cap on how long; an unlock reminder that is held back is skipped, because the unlock already restarted the schedule; the tooltip carries the reason so a quiet icon can be told from a broken one | "Pausing", the rules "A periodic reminder is POSTPONED" and "No cap"; README "Pausing" paragraphs three and five, "What it will and will not catch", "Design decisions" row "Held-back reminder"; 89de6c8 |
| 0019 | The activity monitor is read when a reminder is due, once per retry, and otherwise once every `DefaultActivityRefresh` (10 s) to keep the tooltip current, never on the one-second tick (1800 registry walks per reminder); without the idle refresh the feature was invisible until the interval happened to elapse, which is how it was first written and was wrong | "Pausing", the rule "The activity monitor is read when..."; README "Pausing" fourth paragraph and "Design decisions" row "Tooltip freshness"; `ReminderCoordinator.cs` comment on `DefaultActivityRefresh`; 89de6c8 |
| 0020 | `--detect` shows the raw readings and exits; `--detect <seconds>` samples once a second and renders a timeline through `DetectionLog`, in Core and tested, because the full-screen state cannot be measured from a terminal; the message box is shown only after the run; the duration is clamped to ten minutes and an unparseable one falls back to the single reading; neither `--detect` nor `--preview` claims the single-instance mutex | "Pausing", the last two paragraphs; "Build & test" (`--detect`); README "Checking it on your machine"; `App.xaml.cs` comments on `DetectSwitch`, `MaxDetectDuration`, `DetectDuration`, `ShowDetection`; `DetectionLog.cs`; 89de6c8 |
| 0021 | Two reminders, two switches, two messages, independent of each other and of the pauses: `UnlockEnabled`, `UnlockMessage`, `PeriodicMessage` as init-only properties so `new ReminderSettings(true, 30)` keeps its meaning; the unlock gate sits after the schedule restart and before everything else in `HandleUnlock`, so with the switch off nothing below it runs and the registry is not walked | "Two reminders, two switches, two messages", first three paragraphs; "Structure" (`ReminderSettings.cs`); README "Two reminders, two messages" first two paragraphs; `ReminderSettings.cs` and `ReminderCoordinator.HandleUnlock` comments; 2be5361 |
| 0022 | The coordinator picks the text and the presenter draws it: `IReminderPresenter.Show` takes the message; `BringToFront` takes nothing, so a window already open, or already in its 600 ms queue, keeps the text it opened with | "Two reminders", fourth paragraph; `IReminderPresenter.cs` comments; 2be5361 |
| 0023 | Messages are normalized like numbers are clamped: `NormalizeMessage` turns line breaks and tabs into spaces, trims, reads nothing as `DefaultMessage` and cuts at `MaxMessageLength` (50); the registry goes through it silently, the dialog refuses a blank message with `IsMessageAcceptable` but only for a reminder that is switched on; the `TextBox` `MaxLength` reads the constant through `x:Static`; `--preview "text"` shows a message through the same normalization without saving it | "Two reminders", fifth paragraph and the `--preview` paragraph; "Settings storage" last paragraph; "Build & test" (`--preview`); README "Two reminders, two messages"; `ReminderSettings.NormalizeMessage` comments; `SettingsWindow.xaml` comments on `MaxLength`; 2be5361 |
| 0024 | The reminder window stays a fixed size (440 x 260) and the text scales down to fit: a `Viewbox` with `StretchDirection="DownOnly"` around a `TextBlock` with an explicit `Width`, so a 50-character message that would wrap to three lines at 30 px shrinks and every shorter message is drawn exactly as before; growing the window would have meant placing it after layout rather than in `OnSourceInitialized` | "Two reminders", sixth paragraph; README "Two reminders" third paragraph and "Design decisions" row "Reminder text"; `BreakWindow.xaml` comment on the `Viewbox`; 2be5361 |
| 0025 | `TrayTooltip` is a separate pure type because `NotifyIcon.Text` throws above 63 characters, and the one thing worth testing is that the longest string fits; minutes are rounded up and never below 1; while a reminder is paused the tooltip shows the reason instead of the countdown; `App.UpdateTooltip` writes only when the rendered string changed, because every write is a `Shell_NotifyIcon` call | "The tray tooltip" whole; "Structure" (`TrayTooltip.cs`); README "Behaviour" tooltip bullet, "Pausing" fourth paragraph; `TrayTooltip.cs` comments; `App.xaml.cs` comment on `UpdateTooltip`; b6554d5, 89de6c8 |
| 0026 | The windows and their modality: `BreakWindow` modeless, `Topmost`, takes the focus, centred on the primary monitor's work area, shown after a `DefaultShowDelay` of 600 ms because a window created into a freshly unlocked desktop lands behind everything; its Close button is not `IsDefault` and Enter is swallowed in `OnPreviewKeyDown` (not `KeyDown`), Esc and Space close it; `SettingsWindow` and `AboutWindow` are modeless (a modal `ShowDialog` would disable the reminder's Close button), not topmost, single-instance (reopening raises the existing one), their lifetimes in `App`, and About's Close button is `IsDefault` | "Windows the app shows" whole; "Structure" (`App.xaml.cs` on the two lifetimes); README "Behaviour" (primary monitor, Esc, Enter), "Periodic reminders" fourth bullet, "Design decisions" row "Settings window"; `BreakWindow.xaml.cs` comments on `OnSourceInitialized` and `OnPreviewKeyDown`; `WpfReminderPresenter.cs` comment on `DefaultShowDelay`; `WindowPlacement.cs`; 61a0121, 51cb690, b6554d5, 432edfd |
| 0027 | Settings are eight string values under `HKCU\Software\MindfulnessReminder`, the key `StartupRegistrar` already keeps `AutoStartInitialized` under, strings rather than DWORDs so `IRegistryStore` keeps its four methods; reading is forgiving (a missing or corrupt value is the default, an out-of-range one is clamped, a blank or over-long message is the default or the cut text) and saving clamps too; the dialog is the exception and refuses an out-of-range value, because there is a user to tell | "Settings storage" whole; README "Design decisions" row "Settings storage"; `RegistrySettingsStore.cs` comments; `ReminderSettingsStoreTests`; b6554d5, 89de6c8, 2be5361 |
| 0028 | Auto-start is one value under `HKCU\...\CurrentVersion\Run` (no elevation, maps onto a checkbox); the truth also reads `Explorer\StartupApproved\Run`, where Task Manager vetoes an entry without deleting it, so the checkbox reads "disabled in Task Manager" instead of "on"; the app registers itself on the very first run only (`AutoStartInitialized` marker), so unticking sticks; each launch repairs a stale path without re-registering or lifting a veto | README "Behaviour" second bullet, "Deployment note", "Design decisions" rows "Auto-start" and "Auto-start truth"; "Structure" (`RegistryAutoStartManager.cs` / `StartupRegistrar.cs`, `AutoStartStatus.cs`); `RegistryAutoStartManager.cs` and `StartupRegistrar.cs` comments; `TrayIconHost.cs` strings; 61a0121 |
| 0029 | Unlock detection is `SystemEvents.SessionSwitch` behind `ISessionMonitor`, a few lines a `WTSRegisterSessionNotification` implementation could replace; `Locked` is raised for the schedule's suspend and nothing else, and no reminder is ever shown on lock | README "Design decisions" row "Unlock detection"; "Structure" (`SystemEventsSessionMonitor.cs`); "The periodic reminder" decision three ("`ISessionMonitor` raises `Locked` for this and nothing else"); `ISessionMonitor.cs` and `SystemEventsSessionMonitor.cs` comments; 61a0121, b6554d5 |
| 0030 | The dark theme is merged per window, never into `Application.Resources`, so `BreakWindow` stays a light card by structure (a dark rectangle over dark work is easy to miss); three surfaces, three mechanisms (`DarkTheme.xaml` for the client area of Settings and About, `ImmersiveDarkMode.Apply` for the title bar, `DarkMenuRenderer` for the Windows Forms menu) and nothing shared; the palette is repeated in `DarkMenuRenderer` as `Color` literals and is not moved into Core, because a palette is presentation | "The dark theme", first three paragraphs and the table; README "Behaviour" last bullet, "Design decisions" row "Dark theme"; `DarkTheme.xaml`, `DarkMenuRenderer.cs`, `ImmersiveDarkMode.cs` comments; `AboutWindow.xaml` comment on the merge; 432edfd |
| 0031 | The template rules of the dark theme: every template carries its own disabled trigger (the stock greying belongs to the stock template); `TextBlock`'s implicit style has no `Foreground` setter, only a trigger, with `HintTextStyle` the exception; `Window.Background` and `Window.Foreground` are `DynamicResource` because attributes on the `Window` tag parse before `Window.Resources`; focus is a border colour, never a thickness; two button styles rather than one; `DwmSetWindowAttribute` failing is not an error (attribute 20, then 19, then give up) | "The dark theme", the six "Rules that are easy to break"; `DarkTheme.xaml` comments; `ImmersiveDarkMode.cs` comment on the attribute numbers; 432edfd |
| 0032 | About is a window, not a `MessageBox` (a Win32 dialog cannot be themed), showing `LicenseInfo.AboutText` in one `TextBlock` because that property is what two tests describe as everything About shows; `TrayIconHost` raises `AboutRequested` as it raises `SettingsRequested`, and window lifetimes belong to `App`; the icon is decoded to its largest frame, not its first, and `LoadTrayIcon` does the same the other way; the crash dialog and the `--detect` timeline stay `MessageBox`, since both are terminal | "The dark theme", the last four paragraphs; README "Design decisions" row "About"; `App.xaml.cs` comment on About; `AboutWindow.xaml.cs` and `TrayIconHost.LoadTrayIcon` comments; 432edfd |
| 0033 | One instance: a named mutex (`Local\MindfulnessReminder.SingleInstance`) claimed in `OnStartup`, a second copy exits quietly; `ShutdownMode` is `OnExplicitShutdown` and there is no main window, so closing the reminder does not quit; the crash dialog shows the error and the banner; `--preview` and `--detect` run before the mutex is claimed | "Structure" (`App.xaml.cs`); README "Checking it on your machine" (the mutex sentence), "Versioning" (crash dialog); `App.xaml.cs` comments on `ShutdownMode`, `SingleInstanceMutexName` and the crash handler; 61a0121, 89de6c8 |

If, while writing, two of these turn out to be one decision, merge them and renumber before
the commit (numbers are assigned once only after they are on `main`); if a source holds a
decision not listed here, add it at the end and say so under "What deviated". Thirty-three is
the expected count, not a target.

## Ground rules for the implementing session

- **Where.** Open the session in `E:\Photografie\Code\MindfulnessReminder` (the project folder
  of the main checkout, never the git root, never the desktop app's worktree option: the kit's
  ADR 0027). Before anything, `git worktree list` and `git status --short .`: another session on
  the same task, or a dirty tree, is a stop. Then make the task's worktree and branch in one
  command, from the project folder:

  ```bash
  git worktree add .claude/worktrees/Adopt-the-starter-kit-process -b claude/Adopt-the-starter-kit-process main
  ```

  Every edit, build, test and commit happens under
  `E:\Photografie\Code\MindfulnessReminder\.claude\worktrees\Adopt-the-starter-kit-process\MindfulnessReminder\`
  (the worktree holds the whole `photo` repository; `../ClaudeCodeStarterKit` inside it is the
  kit at the same `main`). Copy this plan there first, to
  `docs/plans/2026-10-09-adopt-the-starter-kit.md`, and delete the copy in the main checkout
  (`git status` there must be clean, so the owner's merge is clean). The root `.gitignore`
  already ignores `**/.claude/worktrees/`.
- **Which `CLAUDE.md` is loaded.** From the worktree, Claude Code loads the worktree's
  `CLAUDE.md` and may also load the main checkout's, which until the merge is the old one with
  "Do not commit". The kit's rules in this plan win; the old "Committing" section is
  superseded by ADR 0001 (ImageVisualCompare saw the same, its docs-health gap 11).
- **The git hooks are live from the first commit that adds `scripts/verify.ps1`:** the root
  `pre-commit` runs `scripts/verify.ps1` in this project (expect about 30 s: the suite is 70 ms,
  restore and build dominate), refuses a session's commit on a `claude/` branch in the main
  checkout (hence the worktree), and `commit-msg` refuses a message that is not one line of at
  most 100 characters. The Claude Code hooks of `.claude/settings.json` are loaded at session
  start, so they are not live in the implementing session; the git hooks gate the commits anyway.
- **Never edit** `scripts/`, `.claude/skills/`, `.claude/agents/` or `.claude/rules/kit/` in
  this project, nor the process keys of `.claude/settings.json`: they are the kit's, copied. If
  a script cannot do what this project needs, that is a kit change, a separate task; say so.
- **Nothing is deleted, it is moved.** Every paragraph of today's `CLAUDE.md` has a destination
  in the tables of sections 2.3 and 2.6. Do not improve the prose while moving it; do not
  de-duplicate between a requirement row, a design note and an ADR in this task (FlatCheck did
  that two days later, its ADR 0051; it is a task of its own). Pointers are the only new
  sentences in moved text. The README is not restructured: it is the user documentation and the
  tool's behaviour did not change; it gains one section (Appendix D.11) and nothing else.
- **Code changes in this task are exactly these**, and nothing else under `src/` or `tests/`:
  the `dotnet format` pass (the six unused usings); the three csproj files losing the
  properties `Directory.Build.props` now carries (ADR 0003) and the test csproj losing its three
  `Version` attributes; the one comment in `AboutWindow.xaml` repointed (section 2.8); the new
  `RepoInvariantTests.cs` of layer 3 and the `R###_` renames. Anything else is a checkpoint.
  `dotnet test` must report 215 passed after every one of these, 222 after layer 3.
- **Never bump the version, never roll the year, never commit on `main`.** The branch's commits
  are the session's (the kit's ADR 0021): one line, English, at most 100 characters, saying
  why; no body, no trailers. The merge is proposed and asked at the end.
- **Languages.** English in everything, the README included (ADR 0004). A new source file
  (`RepoInvariantTests.cs`) starts with the GPL notice: `LicenseHeaderTests` scans the test
  project and fails otherwise; copy the sixteen lines from any existing `.cs` file. Test names in
  this project are sentences with underscores (`A_lock_stops_the_countdown`); new tests follow
  that, with the `R###_` prefix in front.
- **Editing long files.** Do not rewrite a 200-line file through a heredoc: a Python or
  PowerShell edit script written to the scratchpad first, or the Edit tool, keeps `\n` and
  backticks intact. `CLAUDE.md` is replaced whole (Appendix A) only after every section has been
  copied into its note or ADR. The design notes are cut out of the old `CLAUDE.md` by a script
  that copies line ranges (the section headings are at lines 12, 76, 92, 135, 191, 234, 253,
  268, 337, 368, 412, 426 and 442 of today's file), and the script then confirms every other
  line of the old file is accounted for.
- **Subagents.** The retroactive ADRs (27) and the design notes (9) are independent writing
  jobs from fixed sources; ImageVisualCompare used three subagents for its 19 ADRs. Give each
  subagent this plan's table rows, the ADR template, one finished retroactive ADR of
  `../ImageVisualCompare/docs/decisions/` (0017) as the model, and the exact source text; review
  every file before it is committed, against the source, for a claim the source does not make.
  Requirement rows are better written by the main session, in one pass, so the IDs stay
  consistent with the tests renamed in layer 3.
- **Commits.** Three on the branch, one per layer, each with the gate green (the owner may say
  otherwise at approval). Proposed messages, each under 100 characters:
  1. `Adopt the kit's gate: synced scripts, kit.json, changelog, lock files, strictness in Build.props`
  2. `Split CLAUDE.md into requirements, decisions and design notes; CLAUDE.md keeps the rules`
  3. `Tie requirements to tests: RepoInvariantTests and R IDs on every test`
- **Checkpoints that need the owner** (stop and ask; everything else is decided here): the
  plan's approval (which accepts the 33 ADRs and the publish choice of ADR 0002); a
  `dotnet format` change that is not a removed using; a `CLAUDE.md` paragraph with no
  destination in the mapping tables; a test whose name cannot be tied to any row (report, do not
  invent a row); the `/docs-health` report (relayed, nothing fixed); the merge.

## Layer 1: the gate (about an hour)

Every path below is relative to the worktree's `MindfulnessReminder/` folder.

1. **Bootstrap the shared folders.** Copy `../ClaudeCodeStarterKit/scripts/` to `scripts/`
   without the kit-only files (`rename.ps1`, `install-hooks.ps1`, `hooks/pre-commit`,
   `hooks/pre-merge-commit`, `hooks/commit-msg`); keep `hooks/claude-commit-gate.ps1`,
   `hooks/claude-track-edits.ps1`, `hooks/claude-stop-gate.ps1`. Write `kit.json` (Appendix B,
   verbatim). Then run, in the project:

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/sync.ps1
   ```

   It mirrors `scripts/` (kit-only files are neither copied nor deleted), copies
   `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and, since the project has no
   `.claude/settings.json`, the kit's copy of it. Then `scripts/sync.ps1 -Check` must exit 0.
2. **`.claude/settings.json`**: replace its `$comment` with the project's (Appendix D.1). Add
   one `allow` entry of the project's own, for the two switches the README documents:
   `"PowerShell(src/MindfulnessReminder.App/bin/Release/net8.0-windows/MindfulnessReminder.exe --*)"`
   and the same for `Bash(...)`, so `--preview` and `--detect` run without a prompt (they take
   no mutex and touch no registry). `sync.ps1 -Check` must still exit 0 (the comment and the
   non-git `allow` entries are the project's).
3. **Reproducibility files and the strictness move (ADR 0003).**
   - `global.json`: Appendix D.2 (`9.0.318`, `latestPatch`; `dotnet --version` prints 9.0.318
     on this machine).
   - `Directory.Build.props`: replace with Appendix D.3. It keeps `<Version>1.4.0</Version>`,
     `<Copyright>` and `<PackageLicenseExpression>` with their comments word for word, and
     gains the strictness group (`Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`,
     `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`, `NoWarn` CS1591) and the
     reproducibility group (`Deterministic`, `ContinuousIntegrationBuild`,
     `ManagePackageVersionsCentrally`, `RestorePackagesWithLockFile`, `RestoreLockedMode`).
     `SatelliteResourceLanguages` stays in the App csproj, where it is (it matters to the App
     only). **No `TargetFramework` here**: the three projects differ (`net8.0`,
     `net8.0-windows`).
   - `src/MindfulnessReminder.Core/MindfulnessReminder.Core.csproj`: remove `ImplicitUsings`,
     `Nullable`, `TreatWarningsAsErrors`; what remains is `TargetFramework` and the
     `InternalsVisibleTo` item.
   - `src/MindfulnessReminder.App/MindfulnessReminder.App.csproj`: remove the same three; keep
     everything else, comments included (the hybrid comment, the `LICENSE` item and its
     comment, the icon, the manifest, `SatelliteResourceLanguages`, `ApplicationHighDpiMode`).
   - `tests/MindfulnessReminder.Tests/MindfulnessReminder.Tests.csproj`: remove the same three
     (keep `IsPackable`), and remove the `Version="..."` attribute from the three
     `PackageReference` lines (central package management refuses a version in a csproj).
   - `Directory.Packages.props`: new, Appendix D.6, the three packages at the versions the
     csproj had (17.11.1, 2.9.2, 2.8.2). **Do not upgrade them** in this task; a package
     version is the owner's call (the kit's rule "Packages").
   - `dotnet restore MindfulnessReminder.sln` writes a `packages.lock.json` in each of the
     three project folders (Core and App get one with no packages); all three are committed.
   - `.editorconfig`: Appendix D.4. `xaml` is not in the two-space group: this project's XAML
     is four-space indented and `dotnet format` does not touch XAML either way.
   - `.gitattributes`: Appendix D.8.
   - `.gitignore`: Appendix D.9 (the existing five lines kept, the kit's added).
   - `.config/dotnet-tools.json`: the kit's verbatim (Stryker 4.16.0).
     `tests/MindfulnessReminder.Tests/stryker-config.json`: the kit's with
     `"project": "MindfulnessReminder.Core.csproj"`.
4. **The build with the new properties.** `dotnet build MindfulnessReminder.sln -c Release`
   must still report 0 warnings and `dotnet test` 215 passed before the format pass; with
   `EnforceCodeStyleInBuild` and the `.editorconfig` the six IDE0005 are now **errors**, so the
   build is red until step 6 runs. Do step 6 before judging step 4; the point of step 4 is that
   nothing but IDE0005 appears. If CS1573 or any other new diagnostic appears, it is a
   checkpoint (ImageVisualCompare met six CS1573 from its documentation file; this project's
   measurement found none).
5. **`CHANGELOG.md`**: Appendix D.5.
6. **Format pass.** `dotnet format MindfulnessReminder.sln`, then `git diff --stat` and read the
   diff: removed `using` lines in the five files named in the measurements table and nothing
   else. The on-disk CRLF of the `.sln` does not appear (the index is LF already, and a fresh
   worktree is LF on disk). Record the file count and the kinds for ADR 0003. Anything else is
   a checkpoint.
7. **Test data (ADR 0005).** Create `tests/testdata/` with `README.md` (Appendix D.10) and
   `manifest.json` (Appendix D.7, an empty list). Run

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1
   ```

   and expect exit 0 with no `ok` line (nothing to check). No `fixtures/` and no `large/`
   folder are created: there is nothing to put in them.
8. **Gate.** `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`. Steps 0
   to 7 green: `kit files` (sync -Check), `test data manifest` (nothing listed), `decisions`
   (yellow or passing: no folder yet), `design notes` (passes without `docs/design/`), restore,
   format, build with `-warnaserror`, 215 tests. Paste the last ten lines in the session. Also
   `scripts/release.ps1 -Version 1.4.1 -WhatIf`: in a linked worktree it refuses before it reads
   the version (ImageVisualCompare's deviation), so it proves only that `kit.json` parses; run
   its two regexes by hand (`<Version>` once in `Directory.Build.props`, the floor from
   `CHANGELOG.md` is 1.4.0) and say so.
9. **ADRs 0001 to 0006** go into this commit (`kit.json`, `.editorconfig`,
   `Directory.Build.props` and the CHANGELOG cite them). Copy
   `../ClaudeCodeStarterKit/docs/decisions/0000-template.md` first. Header format, letter for
   letter (`check-decisions.ps1` is a gate step from this commit on): line 1 `# NNNN. <title>`,
   line 2 blank, line 3 `Date: 2026-10-09`, line 4 `Status: accepted 2026-10-09`, line 5 blank;
   no `Supersedes` line. Sections `## Context`, `## Decision`, `## Alternatives rejected`,
   `## Consequences`; exactly one `## Decision` per file. Numbers cited as `ADR 0009` mean this
   project's; the kit's are always `the kit's ADR NNNN`. File names are lowercase words joined
   by hyphens, for example `0001-adopt-the-kits-process-claude-md-splits.md`. Run
   `scripts/verify.ps1` again: step 2 now checks six headers.
10. **Commit 1** on the branch (the git pre-commit hook runs verify again; the commit-msg hook
    checks the line). Report the hash.

## Layer 2: the documentation (one long session, no code)

### 2.1 `docs/requirements.md`

Format: ImageVisualCompare's exactly (the header with the rules for entries, then one table
per area, columns `ID | Status | Requirement`). Rules of this file:

- IDs in blocks of ten, one block per area, assigned once; gaps are deliberate.
- Status `accepted` for every row in layer 2 (the behaviour is agreed and in the code); layer 3
  moves the rows whose tests get an `R###_` name to `implemented`. No row is `draft`.
- A row states observable behaviour: inputs, what is on screen or in the registry, the constants
  **by their name in the code and their value on 2026-10-09**, and one concrete example with
  numbers. The examples are the README's (its tooltip strings, its `--detect` timeline, its
  lock-for-three-hours case). No adjectives.
- A row says *what*; the design note of the area keeps *how* and *why*. The README paragraph
  that describes the same behaviour is the user's version and stays where it is.
- Constants are quoted as `` `ReminderSettings.MaxIntervalMinutes` (480) ``.
- The header says that the WPF layer (`src/MindfulnessReminder.App/`) is not unit-tested by
  design (ADR 0008), so its rows stay `accepted` and are checked by launching the application
  with `--preview` or from the tray; and that there are no validation figures (ADR 0005).

The blocks and the rows (write them in this order; the titles below are the content each row
must carry, not its text; a row may be split in two when it would exceed about ten lines, and
two may be merged when one example covers both; keep the block):

**The tray, one instance, auto-start (R-001 to R-009)**, from README "Behaviour" and
"Deployment note", `App.xaml.cs`, `TrayIconHost.cs`, `RegistryAutoStartManager.cs`,
`StartupRegistrar.cs`, `AutoStartStatus.cs`:
- R-001 the application lives in the notification area with no main window and no taskbar
  entry; the tray menu has four items in this order: `Settings...`, the checkbox
  `Run at Windows startup`, `About`, `Exit`; the icon is the embedded `Assets/app.ico` decoded
  at `SystemInformation.SmallIconSize`.
- R-002 one instance: the named mutex `Local\MindfulnessReminder.SingleInstance`; a second
  copy exits without a message; `--preview` and `--detect` do not claim it and run beside the
  tray copy.
- R-003 the very first run registers the executable under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` as the value `MindfulnessReminder`
  (the quoted path, `RegistryAutoStartManager.Command`) and writes the marker
  `AutoStartInitialized` under `HKCU\Software\MindfulnessReminder`; a balloon says
  `Running in the tray. It now starts with Windows -- untick that in the tray menu to stop it.`;
  later runs never re-register (`StartupRegistrar.EnsureRegisteredOnFirstRun` returns false).
- R-004 `GetStatus` is `Disabled` with no Run entry or a blank one, `Enabled` with an entry
  whose `StartupApproved\Run` flag is absent or even, `BlockedByWindows` when the flag's first
  byte is odd; a veto with no Run entry is `Disabled`; the checkbox reads
  `Run at Windows startup (disabled in Task Manager)` when blocked.
- R-005 ticking the checkbox writes the entry and clears a Task Manager veto; unticking deletes
  the entry; a registry refusal shows `Windows refused the startup setting change. Group policy
  may be managing it.` and the checkbox goes back.
- R-006 each launch repairs a stale path (`RepairPathIfNeeded`: a registered entry whose path
  differs from the running executable, compared without case, is rewritten; an unregistered
  app is not registered; a veto is kept); the README's deployment note follows from this.

**The unlock reminder (R-010 to R-019)**, from README "Behaviour", `ReminderCoordinator.cs`
(`HandleUnlock`), `WpfReminderPresenter.cs`, `SystemEventsSessionMonitor.cs`:
- R-010 on `SessionUnlock` the reminder window appears with `UnlockMessage`, after
  `WpfReminderPresenter.DefaultShowDelay` (600 ms); unlocks before `Start` are ignored.
- R-011 a second unlock within `ReminderCoordinator.DefaultDebounce` (2 s) of the previous
  reminder is ignored; one after it shows the reminder again; an unlock just after a periodic
  reminder is debounced the same way.
- R-012 an unlock while a reminder is open brings it to the front (`BringToFront`) instead of
  opening a second, and its text is not changed.
- R-013 with `UnlockEnabled` false an unlock shows nothing, reads the activity monitor not at
  all, leaves an open reminder where it is, and still restarts the periodic countdown
  (R-024).
- R-014 an unlock reminder that `SuppressionPolicy` would hold back is skipped, not postponed;
  the periodic countdown it restarted runs on (example: unlock during a call, the next
  reminder is one full interval later).
- R-015 `ForegroundState.UserAway` (`QUNS_NOT_PRESENT`, 1) never skips an unlock reminder.
- R-016 a lock never shows a reminder by itself.

**The periodic reminder (R-020 to R-029)**, from README "Periodic reminders",
`ReminderCoordinator.cs`, `IntervalSchedule.cs`, `ReminderSettings.cs`:
- R-020 `PeriodicEnabled` default true, `IntervalMinutes` default
  `ReminderSettings.DefaultIntervalMinutes` (30), accepted `MinIntervalMinutes` (1) to
  `MaxIntervalMinutes` (480); the countdown starts with the application.
- R-021 one timer ticking at `ReminderCoordinator.DefaultTickInterval` (1 s); the reminder is
  due when `IClock.UtcNow` reaches the deadline, exactly at the interval and not before; it
  then repeats every interval; `CountdownChanged` fires on every tick and on every reset.
- R-022 a deadline that passed while the machine slept fires on the first tick after waking
  (the schedule compares the wall clock, not a tick count).
- R-023 a periodic reminder due while a reminder is open brings it to the front without
  changing its text and still re-arms a full interval.
- R-024 a lock suspends the countdown (`IsSuspended`, `Remaining` null, timer stopped) and an
  unlock restarts a whole interval, whatever the lock lasted and however many cycles there
  were; an unlock without a lock restarts it too; a disabled schedule is not restarted.
  Example: 10 minutes left, a lock, three hours, an unlock: the next reminder is 30 minutes
  after the unlock.
- R-025 a clock that jumps backwards restarts the countdown instead of postponing the reminder
  (`TryConsumeDue` sees a deadline further out than the armed span); a postponement longer than
  the interval (300 s against 1 minute) is not read as a jump; `Remaining` never goes negative.
- R-026 `ApplySettings` restarts the countdown from zero; switching the feature off stops the
  timer and the countdown at once; switching it back on starts a whole interval and the timer.
- R-027 with `PeriodicEnabled` false no periodic reminder ever appears and no timer runs; the
  unlock reminder is unaffected; with both reminders off nothing ever appears and no timer runs.
- R-028 an out-of-range interval given to `IntervalSchedule` is clamped, not taken literally.

**Pausing for a call or full screen (R-030 to R-039)**, from README "Pausing for what you are
doing", `SuppressionPolicy.cs`, `ForegroundState.cs`, `ActivitySnapshot.cs`,
`ReminderCoordinator.cs`, `WindowsActivityMonitor.cs`:
- R-030 a live microphone or camera (`ActivitySnapshot.CaptureInUse`) holds a reminder back
  with the reason `MicrophoneInUse` when `PauseWhileMicrophoneInUse` is true; the reading is
  a key under `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone`
  or `\webcam` (including `NonPackaged`) whose `LastUsedTimeStop` is 0.
- R-031 `ForegroundState.FullScreen` or `Presenting` holds a reminder back with the reason
  `FullScreen` when `PauseWhileFullScreen` is true; the mapping of `UserNotificationState`:
  5 `Normal`, 2/3/7 `FullScreen`, 4 `Presenting`, 6 `QuietHours`, 1 `UserAway`, anything else
  `Unknown`; only `FullScreen` and `Presenting` hold back; the constants equal the Windows
  header's.
- R-032 the two switches are independent: each turns off only its own check; with both off
  nothing is ever held back and the monitor is never read (`PausesForAnything`); when both
  apply the reason given is the microphone; both ship on; the pauses apply with the periodic
  reminder off.
- R-033 a held-back periodic reminder is postponed by `RetrySeconds` (default
  `DefaultRetrySeconds` 20, `MinRetrySeconds` 5 to `MaxRetrySeconds` 300) and looked at again
  after each gap, for as long as the condition lasts, with no cap; it appears on the first
  retry after the condition ends, and a full interval follows it.
- R-034 the activity monitor is read when a reminder is due, once per retry, and otherwise every
  `DefaultActivityRefresh` (10 s), never on every tick; `Suppression` reflects the last
  reading, so joining a call shows in the tooltip within ten seconds and leaving it clears the
  tooltip as fast, without waiting for a reminder.
- R-035 an unlock, a lock and `ApplySettings` forget that a reminder was being held back;
  `ApplySettings` takes a fresh reading on the next tick.

**Messages and the Settings window (R-040 to R-049)**, from README "Two reminders, two
messages", `ReminderSettings.cs`, `SettingsWindow.xaml(.cs)`, `App.xaml.cs`:
- R-040 both messages default to `ReminderSettings.DefaultMessage` (`Ground yourself`), each
  up to `MaxMessageLength` (50) characters; the `TextBox` `MaxLength` is the same constant.
- R-041 `NormalizeMessage`: `\r`, `\n` and `\t` become spaces, the text is trimmed, empty or
  null reads as the default, longer than 50 is cut at 50 and trimmed again; `Normalize` applies
  it to both messages and clamps the two numbers.
- R-042 the Settings window: two checkboxes `Remind me when I unlock the computer` and
  `Remind me while I work`, a `Message` box under each, `Every N minutes` with the hint
  `Between 1 and 480 minutes. The countdown restarts when you save.`, the two pause checkboxes
  `Pause while the microphone or camera is in use` and `Pause during full screen and
  presentations`, `Check again every N seconds` with the hint `Between 5 and 300 seconds. The
  reminder waits, then looks again.`, `Save` and `Cancel`; a message box and the interval are
  greyed while their reminder is off; the retry box is editable while either pause is ticked.
- R-043 `Save` refuses an interval or a retry gap that is not a whole number in range
  (`Enter a whole number of minutes between 1 and 480` / `... seconds between 5 and 300`) and
  a blank or over-long message for a reminder that is switched on (`Enter what the reminder
  should say, up to 50 characters.`); `IsIntervalInRange`, `IsRetryInRange` and
  `IsMessageAcceptable` are the checks and they match the clamp.
- R-044 saving applies the settings (R-026) and the new messages take effect on the next
  reminder; the window is modeless and single-instance (R-073).
- R-045 `--preview` shows the reminder window once with the default message and exits: no tray
  icon, no registry, no session hook, no timer; `--preview "text"` shows that text after
  `NormalizeMessage`.

**Settings storage (R-050 to R-059)**, from `CLAUDE.md` "Settings storage",
`RegistrySettingsStore.cs`, `ReminderSettingsStoreTests`:
- R-050 the key `HKCU\Software\MindfulnessReminder`, eight string values:
  `PeriodicRemindersEnabled`, `PeriodicIntervalMinutes`, `PauseWhileMicrophoneInUse`,
  `PauseWhileFullScreen`, `PauseRetrySeconds`, `UnlockReminderEnabled`, `UnlockMessage`,
  `PeriodicMessage`; flags are `"1"` / `"0"`, numbers decimal strings, messages as typed after
  normalization; saving does not disturb `AutoStartInitialized`.
- R-051 a missing value is its default (both reminders on, 30, both pauses on, 20, `Ground
  yourself` twice); a corrupt flag or number is its default; an out-of-range number is clamped
  on load and on save; a blank message reads as the default and an over-long one is cut.
- R-052 a round trip of every field through the store gives the same settings back; a save
  with the periodic reminder off keeps the flag off.

**The tray tooltip (R-060 to R-069)**, from README "Behaviour", `TrayTooltip.cs`,
`App.xaml.cs` (`UpdateTooltip`):
- R-060 the tooltip is `<banner> - next in <N> min` while counting down, the banner alone with
  no countdown, `<banner> - paused while your mic is on` or `<banner> - paused for full screen`
  while a reminder is held back, the reason replacing the countdown; the banner is
  `Mindfulness Reminder 1.4.0`.
- R-061 minutes are rounded up and never below 1: 29 min 1 s reads `30 min`, the last minute
  reads `1 min`, never `0 min`.
- R-062 the text is cut at `TrayTooltip.MaxLength` (63) rather than allowed to throw; the
  longest string the application can produce fits.
- R-063 the icon's text is written only when the rendered string changed (one write a minute
  off a one-second tick).

**The windows (R-070 to R-079)**, from README "Behaviour", `BreakWindow.xaml(.cs)`,
`WindowPlacement.cs`, `SettingsWindow.xaml.cs`, `AboutWindow.xaml(.cs)`, `App.xaml.cs`:
- R-070 the reminder is a 440 x 260 window, `Topmost`, not in the taskbar, centred in the
  primary monitor's work area (`WindowPlacement.CenterIn`: the centre of the area, offset by
  the area's own position on the virtual desktop, kept on screen when wider than the area),
  with one `Close` button; it takes the focus.
- R-071 `Close`, Esc and Space close it; Enter does not, and is swallowed in
  `OnPreviewKeyDown` so it reaches neither the button nor the application underneath.
- R-072 the message is drawn at 30 px in a 332-wide `TextBlock`; a message that would not fit
  is scaled down (`Viewbox`, `StretchDirection="DownOnly"`) and the window keeps its size and
  place.
- R-073 Settings and About are modeless, not topmost, single-instance: opening one again raises
  the existing one; a reminder falling due while either is open appears with a working `Close`.
- R-074 About is titled `About Mindfulness Reminder`, 480 wide, shows the icon at 48 px and
  `LicenseInfo.AboutText` in one `TextBlock`; its `Close` is `IsDefault` and `IsCancel`.
- R-075 Settings, About and the tray menu are dark whatever the system theme (client area,
  title bar, menu); the reminder window is light.
- R-076 the crash dialog shows the error and then the banner.

**The command line (R-080 to R-089)**, from README "Checking it on your machine", `App.xaml.cs`,
`DetectionLog.cs`, `WindowsActivityMonitor.cs`:
- R-080 `--detect` shows one reading in a message box: `Microphone in use: ` / `Camera in use: `
  with the application holding it (`yes -- <app>`), `Shell notification state: ` with the raw
  number and what it mapped to, and the verdict under the saved settings; then exits.
- R-081 `--detect <seconds>` and `--detect=<seconds>` take one reading a second for that long,
  clamped to `MaxDetectDuration` (10 minutes), and show the `DetectionLog.Render` timeline
  after the run: one line per run of identical readings, `m:ss - m:ss   mic off, camera off,
  <state>   ->  <verdict>` (quote the README's three-line example), consecutive identical
  readings collapsed, a reading that returns is a second run, runs over a minute readable;
  then `A reminder would have been held back for N of the M readings.` or `A reminder would
  have been shown at any point in this run.`; no readings: `No readings taken.`
- R-082 an unparseable or non-positive duration falls back to the single reading.

**Version and licence (R-090 to R-099)**, from `CLAUDE.md` "Versioning" and "Licensing",
`Version.cs`, `LicenseInfo.cs`, `VersionTests.cs`, `LicenseHeaderTests.cs`:
- R-090 the version is `<Version>` of `Directory.Build.props` (1.4.0 on 2026-10-09), read from
  `AssemblyInformationalVersion`, three parts, no `+revision`, no `<Version>` in any csproj;
  `VersionInfo.Banner` is `Mindfulness Reminder 1.4.0`.
- R-091 `LicenseInfo.AboutText` is the banner, the copyright (`Copyright (C) 2026 Tudor
  Carean`, from `<Copyright>`), then `LicenseInfo.Warranty` (quote it: `This program comes
  with ABSOLUTELY NO WARRANTY ... https://www.gnu.org/licenses/gpl-3.0.html`).
- R-092 every `.cs` and `.xaml` file of the three projects starts with the sixteen-line
  notice (`//` or one `<!-- -->` block); `LICENSE` is the verbatim GPL v3 text;
  `<PackageLicenseExpression>` is `GPL-3.0-or-later`.
- R-093 `LICENSE` is copied next to the executable in the build output and in a publish.

About 55 rows. The file's header says that `CLAUDE.md` was split on 2026-10-09 (ADR 0001),
that the README is the user's version of the same behaviour, and that `accepted` is the state
of the rows whose tests do not carry an ID yet, as ImageVisualCompare's does.

### 2.2 `docs/decisions/`

ADRs 0001 to 0006 exist since commit 1. Write 0007 to 0033 from the table above (subagents,
reviewed), with the same header format. A retroactive ADR's Context starts with "Retroactive:
recorded on 2026-10-09 from <file and section>; the decision itself dates from <commit>" (the
commits: 61a0121 the base of 2026-08-28, d3ccd6c licensing and versioning of 2026-09-01,
51cb690 the primary screen, b6554d5 the periodic reminder, 89de6c8 pausing, 432edfd the dark
theme, all of 2026-09-08, 2be5361 the two switches of 2026-09-14) and the file ends with
`Source: <files and sections>`. Consequences name the tests that guard the decision
(`IntervalScheduleTests`, `ReminderCoordinatorTests`, `SuppressionPolicyTests`,
`UserNotificationStateTests`, `DetectionLogTests`, `ReminderSettingsStoreTests`,
`TrayTooltipTests`, `RegistryAutoStartManagerTests`, `StartupRegistrarTests`,
`WindowPlacementTests`, `VersionTests`, `LicenseHeaderTests`) or say "none by test; launch the
application with `--preview`, or from the tray" where the decision lives in the App layer
(0024, 0026 in part, 0030, 0031, 0032, 0033). Where a decision has an alternative the text
names (a one-shot timer, a tick count, a window-title table, `GetForegroundWindow`, a one-shot
retry beside the periodic deadline, a growing window, `Application.Resources`, a palette in
Core, a `MessageBox` for About, DWORDs), that is the "Alternatives rejected" section; the
defect that taught it (the feature "invisible until the interval happened to elapse", the Enter
that dismissed an unread reminder, the window behind the lock screen) is the Context.

### 2.3 `docs/design/`

The index `README.md` first (ImageVisualCompare's text adapted: the first paragraph says the
notes were `CLAUDE.md`'s sections until 2026-10-09, ADR 0001, moved unchanged; the Covers rule;
the table with three columns `Area | Note | Read before editing`; the closing paragraph
pointing at `../decisions/`, `../requirements.md`, `../validation.md`). Every note starts with
the quote-block boilerplate ImageVisualCompare's notes use:

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "<section>". Where a paragraph
> became a decision record, its number is given beside it. **Covers:** `path`, `path`, ... . A
> change to the code this describes means this note is updated in the same task, or deleted
> with the decision it explains.

The Covers sentence: paths in backticks, relative to the project root (`src/MindfulnessReminder.Core/IntervalSchedule.cs`;
`check-design.ps1` also resolves a path from a code folder, `MindfulnessReminder.Core/IntervalSchedule.cs`);
a folder only when the note describes its files; no "e.g." and no other full stop before its
end (the sentence ends at the first `. `); a class or method name is allowed but is not a path.
`check-design.ps1` fails on a path git does not have, on a note missing from the index and on a
note without the sentence. The kit's ADR 0032: a Covers sentence names only what its note
describes, so trim the lists below to the files the moved text actually talks about, and say
under "What deviated" what was trimmed.

| Note | Moves in, unchanged | Covers |
| --- | --- | --- |
| `code-map.md` | "Structure" whole (the intro line, the Core list, the App list, the tests line, `README.md`, `Directory.Build.props`, `LICENSE`) | `src/MindfulnessReminder.Core/`, `src/MindfulnessReminder.App/`, `tests/MindfulnessReminder.Tests/` (the note has one line per file of the three folders), `Directory.Build.props` |
| `periodic-reminder.md` | "The periodic reminder" whole (ADRs 0012, 0013, 0014 beside the four decisions and the two smaller rules; 0014 beside the `ApplySettings` paragraph) | `src/MindfulnessReminder.Core/ReminderCoordinator.cs`, `src/MindfulnessReminder.Core/IntervalSchedule.cs`, `src/MindfulnessReminder.Core/IPeriodicTimer.cs`, `src/MindfulnessReminder.Core/IClock.cs`, `src/MindfulnessReminder.Core/ISessionMonitor.cs`, `src/MindfulnessReminder.App/DispatcherPeriodicTimer.cs` |
| `pausing.md` | "Pausing for what the user is doing" whole (ADRs 0016, 0017, 0018, 0019, 0013, 0020 beside the paragraphs and rules) | `src/MindfulnessReminder.Core/SuppressionPolicy.cs`, `src/MindfulnessReminder.Core/ForegroundState.cs`, `src/MindfulnessReminder.Core/ActivitySnapshot.cs`, `src/MindfulnessReminder.Core/IActivityMonitor.cs`, `src/MindfulnessReminder.Core/DetectionLog.cs`, `src/MindfulnessReminder.Core/ReminderCoordinator.cs`, `src/MindfulnessReminder.Core/IntervalSchedule.cs`, `src/MindfulnessReminder.App/WindowsActivityMonitor.cs`, `src/MindfulnessReminder.App/App.xaml.cs` |
| `two-reminders.md` | "Two reminders, two switches, two messages" whole (ADRs 0021, 0022, 0023, 0024 beside them) | `src/MindfulnessReminder.Core/ReminderSettings.cs`, `src/MindfulnessReminder.Core/ReminderCoordinator.cs`, `src/MindfulnessReminder.Core/IReminderPresenter.cs`, `src/MindfulnessReminder.App/WpfReminderPresenter.cs`, `src/MindfulnessReminder.App/BreakWindow.xaml`, `src/MindfulnessReminder.App/SettingsWindow.xaml`, `src/MindfulnessReminder.App/SettingsWindow.xaml.cs`, `src/MindfulnessReminder.App/App.xaml.cs` |
| `tray-tooltip.md` | "The tray tooltip" whole (ADR 0025 beside it) | `src/MindfulnessReminder.Core/TrayTooltip.cs`, `src/MindfulnessReminder.App/TrayIconHost.cs`, `src/MindfulnessReminder.App/App.xaml.cs` |
| `windows.md` | "Windows the app shows" whole, the table included (ADR 0026 beside it) | `src/MindfulnessReminder.App/BreakWindow.xaml`, `src/MindfulnessReminder.App/BreakWindow.xaml.cs`, `src/MindfulnessReminder.App/SettingsWindow.xaml.cs`, `src/MindfulnessReminder.App/AboutWindow.xaml`, `src/MindfulnessReminder.App/AboutWindow.xaml.cs`, `src/MindfulnessReminder.App/WpfReminderPresenter.cs`, `src/MindfulnessReminder.App/App.xaml.cs`, `src/MindfulnessReminder.Core/WindowPlacement.cs` |
| `dark-theme.md` | "The dark theme" whole, the two tables included (ADRs 0030, 0031, 0032 beside the paragraphs) | `src/MindfulnessReminder.App/Theme/DarkTheme.xaml`, `src/MindfulnessReminder.App/ImmersiveDarkMode.cs`, `src/MindfulnessReminder.App/DarkMenuRenderer.cs`, `src/MindfulnessReminder.App/SettingsWindow.xaml`, `src/MindfulnessReminder.App/AboutWindow.xaml`, `src/MindfulnessReminder.App/AboutWindow.xaml.cs`, `src/MindfulnessReminder.App/TrayIconHost.cs`, `src/MindfulnessReminder.App/App.xaml.cs` |
| `settings-storage.md` | "Settings storage" whole, the table included (ADR 0027 beside it) | `src/MindfulnessReminder.Core/RegistrySettingsStore.cs`, `src/MindfulnessReminder.Core/ISettingsStore.cs`, `src/MindfulnessReminder.Core/IRegistryStore.cs`, `src/MindfulnessReminder.Core/StartupRegistrar.cs`, `src/MindfulnessReminder.Core/ReminderSettings.cs`, `src/MindfulnessReminder.App/WindowsRegistryStore.cs` |
| `versioning-and-licence.md` | "Versioning" whole and "Licensing" whole (ADRs 0011, 0009, 0010 beside them; the sentence "The user bumps the version manually" gets "(the kit's rule "Version and year are the owner's", the kit's ADR 0029)" beside it and is otherwise unchanged) | `src/MindfulnessReminder.Core/Version.cs`, `src/MindfulnessReminder.Core/LicenseInfo.cs`, `Directory.Build.props`, `tests/MindfulnessReminder.Tests/VersionTests.cs`, `tests/MindfulnessReminder.Tests/LicenseHeaderTests.cs`, `src/MindfulnessReminder.App/MindfulnessReminder.App.csproj`, `LICENSE` |

Where the rest of `CLAUDE.md` goes: the first two paragraphs (the one-paragraph description
and ".NET 8, **zero external dependencies**") are the new `CLAUDE.md`'s first paragraph and
ADR 0007. "Conventions": "All text is in English" is the rule of the same name and ADR 0004;
"Zero external dependencies" is the rule "Zero external dependencies" and ADR 0007; "Logic in
Core, adapters in App" is the rule "Logic in Core" and ADR 0008; "Never bump the version and
never commit" is the kit's rules "Version and year are the owner's" and "Commits and merges"
(ADR 0001); "Every source file starts with the GPL notice" is the rule "Licence headers" and
ADR 0009. "Build & test" is the new `CLAUDE.md`'s "Commands" (the four commands, with the
kit's beside them) and the README's "Build and run", which already carries every sentence of
it. "Committing" goes to ADR 0001's Context as the superseded text. Nothing else of `CLAUDE.md`
exists today, so after the nine notes and the ADRs the walk of section 2.6 should find every
paragraph placed.

"Read before touching" rows for the index (the ones most likely to catch a careless edit):
`periodic-reminder.md` for `IntervalSchedule.cs` or the tick (ADR 0012 to 0014: one timer, the
wall clock, suspend and restart); `pausing.md` before touching `ReminderCoordinator.HandleTick`,
`SuppressionPolicy` or `WindowsActivityMonitor` (ADR 0016 to 0019: the device not the title,
`QUNS_NOT_PRESENT`, postponed versus skipped, the 10 s refresh); `two-reminders.md` before
`ReminderSettings` or `HandleUnlock` (ADR 0021 to 0023: the unlock gate's position, who picks the
text, normalization); `dark-theme.md` before any XAML or a new window (ADR 0030, 0031: per-window
merge, the disabled triggers); `windows.md` before `BreakWindow` (ADR 0026: Enter);
`tray-tooltip.md` before the tooltip (ADR 0025: 63 characters); `settings-storage.md` before a
new setting (ADR 0027: strings, forgiving read).

### 2.4 `docs/validation.md` and `docs/validation.json`

`validation.json`: Appendix D.12, `"cases": []` with a `$comment` saying what a case would be
and why there is none (ADR 0005). `validation.md`: Appendix D.13: the head paragraph (what the
file is in a project that has figures, and that this one has none), "What would become a case"
(a figure measured on a machine rather than asserted: the time `WindowsActivityMonitor.Read`
takes to walk the `ConsentStore` keys, if the 10 s refresh ever needs defending with a number;
the gate's own duration), "History" with one line, 2026-10-09. No `ValidationTests` class.

### 2.5 `CLAUDE.md`

Replace it with Appendix A only when every row of the table in 2.3 is done. Target 140 to 170
lines; the hard limit is 200 (`RepoInvariantTests`, layer 3). What leaves it and where it goes:
"Structure" -> Layout (short) and `code-map.md`; "Conventions" -> the project's Rules (one line
each, citing the ADR) and the kit's rules; the seven topical sections -> the Rules (one line per
load-bearing decision, citing the ADR) and the seven notes; "Build & test" -> Commands;
"Versioning" -> ADR 0011, the kit's rule and `versioning-and-licence.md`; "Licensing" -> the
rule "Licence headers", ADR 0009, 0010 and the same note; "Committing" -> the kit's rule
"Commits and merges" (superseded; quoted in ADR 0001).

### 2.6 The walk

When every row of the two mapping tables above is done, open today's `CLAUDE.md` (from `git
show main:MindfulnessReminder/CLAUDE.md` in the worktree) and tick each heading and each
paragraph against its destination; a paragraph with no destination is a checkpoint, not a
deletion. The script of the ground rule "Editing long files" does the mechanical half: for
each note, the line range it copied; the lines outside every range must be exactly the
headings, the first two paragraphs, "Conventions", "Build & test" and "Committing". Then write
Appendix A over `CLAUDE.md`.

### 2.7 `docs/skills.md`

Appendix C, verbatim.

### 2.8 Pointers that cite the old places

- `src/MindfulnessReminder.App/AboutWindow.xaml`, the comment at lines 33 to 37: `See "The
  dark theme" in CLAUDE.md.` becomes `See docs/design/dark-theme.md (ADR 0030).` This is the
  one code file layer 2 touches, and it is a comment; the licence header is not affected and
  `dotnet test` still reports 215.
- `README.md`: add the section "## Development: the gate, the documentation and Claude Code"
  from Appendix D.11 **after "## Deployment note" and before "## Versioning"**, in English;
  and in "Build and run" add one sentence after the two commands: "The full gate, the one the
  `pre-commit` hook runs, is `scripts/verify.ps1` (see Development below)." Nothing else in
  the README changes: it is the user documentation and the tool's behaviour did not. Its
  "Design decisions" table stays as it is; the ADRs are the record and the table is the
  user's summary of the same choices (as ImageVisualCompare's README kept its own paragraphs).
- `grep -rn -i "CLAUDE.md" src tests README.md docs` after the two edits must find only the
  README's development section and the design notes' own boilerplate.

### 2.9 Gate and commit 2

`scripts/verify.ps1` green: step 2 `decisions` now checks 33 headers, step 3 `design notes`
checks the index and every Covers path. Also run `scripts/check-design.ps1 -Since main` in the
worktree once: it lists `versioning-and-licence.md` and `code-map.md` (their Covers name
`Directory.Build.props` and the csproj files, which layer 1 changed) and `dark-theme.md` and
`windows.md` (`AboutWindow.xaml`, the repointed comment); read each listed note against the
diff and confirm the text still holds (the "Structure" lines for the csproj files say nothing
the move of three properties contradicts; say so). Commit 2.

## Layer 3: requirements tied to tests (about an hour)

1. **`tests/MindfulnessReminder.Tests/RepoInvariantTests.cs`**: ImageVisualCompare's, with the
   names changed and no `TestData` class (there is none here): `RepoDir()` from
   `[CallerFilePath]` exactly as `LicenseHeaderTests.RepoDir` does (three `GetDirectoryName`
   calls), `TestProjects` = `{ Path.Combine("tests", "MindfulnessReminder.Tests") }`. Seven
   tests, named in this project's style:
   - `Every_implemented_requirement_has_a_test_carrying_its_id`
   - `Every_test_id_exists_in_requirements`
   - `Every_tested_requirement_is_marked_implemented`
   - `Requirement_ids_are_unique`
   - `The_design_notes_have_their_index` (`docs/design/README.md` exists and the folder holds
     more than one `.md`)
   - `The_validation_cases_parse_and_have_unique_ids` (reads `docs/validation.json`, its
     `cases` array exists; IDs unique; with zero cases it passes, and it is what guards the
     shape when the first case arrives, ADR 0005)
   - `Claude_md_stays_short` (200 lines)
   The `TestMethod` regex matches `public void R###_` and `public async Task R###_` (no test
   here is async; keep the regex the kit's anyway). GPL header; English summary, saying what
   each test enforces and citing ADR 0001.
2. **Rename the tests** to `R###_...` (the method name only; nothing else cites them: no doc,
   no ADR, no comment names a test method on 2026-10-09 except `CLAUDE.md`, whose sentences
   name five tests, `The_longest_tooltip_the_app_can_produce_fits_the_tray_limit`,
   `A_postponement_longer_than_the_interval_is_not_read_as_a_clock_jump`,
   `Switching_the_periodic_reminder_off_leaves_the_unlock_reminder_alone`,
   `Switching_the_unlock_reminder_off_shows_nothing_on_unlock` and
   `No_project_overrides_the_shared_version`; those sentences moved into the notes and the ADRs
   in layer 2, and each gets its new name there in this step: `grep -rn` the five old names
   under `docs/` and `CLAUDE.md` and prefix them). The mapping (test -> ID; when a test pins
   two rows, the first):

   | File | Test | ID |
   | --- | --- | --- |
   | `DetectionLogTests` | all eight | R-081 |
   | `IntervalScheduleTests` | `A_fresh_schedule_is_not_counting_until_it_is_started` | R-020 |
   | | `Nothing_is_due_before_the_interval_has_passed`, `The_reminder_is_due_exactly_at_the_interval`, `Becoming_due_re_arms_for_another_full_interval` | R-021 |
   | | `A_deadline_that_passed_while_the_machine_slept_fires_on_the_first_tick_after` | R-022 |
   | | `Restart_puts_the_whole_interval_on_the_clock`, `A_lock_stops_the_countdown`, `A_lock_longer_than_the_interval_still_grants_the_full_interval_after_unlock`, `Any_number_of_lock_unlock_cycles_leaves_the_full_interval_after_the_last_one`, `An_unlock_without_a_lock_still_restarts_the_countdown`, `An_unlock_does_not_restart_a_disabled_schedule` | R-024 |
   | | `A_clock_that_jumps_backwards_restarts_instead_of_postponing_the_reminder`, `Remaining_never_goes_negative`, `A_postponement_longer_than_the_interval_is_not_read_as_a_clock_jump`, `A_clock_jumped_backwards_during_a_postponement_still_restarts_the_countdown` | R-025 |
   | | `Applying_settings_restarts_the_countdown_from_zero`, `Switching_the_feature_off_stops_the_countdown_immediately`, `Switching_the_feature_back_on_starts_a_whole_interval` | R-026 |
   | | `A_disabled_schedule_never_counts_and_never_fires` | R-027 |
   | | `An_out_of_range_interval_is_clamped_rather_than_taken_literally` | R-028 |
   | | `Postpone_moves_the_deadline_to_the_gap_it_is_given`, `A_postponed_deadline_comes_due_when_the_gap_has_passed`, `Postpone_does_nothing_while_the_feature_is_off`, `Postpone_does_nothing_while_the_workstation_is_locked`, `A_restart_puts_the_whole_interval_back_over_a_postponement` | R-033 |
   | `LicenseHeaderTests` | `The_copyright_comes_from_the_build_props`, `The_copyright_names_the_author_and_the_year`, `About_carries_the_warranty_disclaimer_and_points_at_the_license` | R-091 |
   | | `Every_source_file_starts_with_the_license_notice`, `The_scan_sees_all_three_projects_and_the_xaml`, `The_repo_ships_the_full_gpl_text` | R-092 |
   | | `The_app_copies_the_license_next_to_the_executable` | R-093 |
   | `RegistryAutoStartManagerTests` | `Enable_writes_the_quoted_executable_path`, `Constructor_rejects_a_blank_executable_path` | R-003 |
   | | `GetStatus_returns_disabled_when_no_run_entry_exists`, `GetStatus_returns_disabled_for_a_blank_run_entry`, `GetStatus_reports_enabled_when_the_approval_flag_is_even`, `GetStatus_reports_blocked_when_the_approval_flag_is_odd`, `GetStatus_ignores_a_Task_Manager_veto_when_no_run_entry_exists` | R-004 |
   | | `GetStatus_returns_enabled_after_Enable`, `Enable_clears_a_Task_Manager_veto`, `Disable_removes_the_run_entry`, `Disable_is_a_no_op_when_nothing_is_registered` | R-005 |
   | | the four `RepairPathIfNeeded_...` | R-006 |
   | `ReminderCoordinatorTests` | `Start_starts_the_monitor`, `Start_twice_subscribes_only_once`, `An_unlock_shows_the_reminder`, `Unlocks_are_ignored_before_Start`, `Dispose_unsubscribes_and_disposes_the_monitor`, `Dispose_is_idempotent`, `The_unlock_reminder_says_the_unlock_message` | R-010 |
   | | `A_duplicate_unlock_within_the_debounce_window_is_ignored`, `A_later_unlock_shows_the_reminder_again`, `An_unlock_just_after_a_periodic_reminder_is_debounced` | R-011 |
   | | `An_unlock_while_a_reminder_is_open_raises_it_instead_of_opening_a_second` | R-012 |
   | | `Switching_the_unlock_reminder_off_shows_nothing_on_unlock`, `Switching_the_unlock_reminder_off_still_restarts_the_countdown_on_unlock`, `An_unlock_with_the_unlock_reminder_off_does_not_read_the_activity_monitor`, `An_unlock_with_the_unlock_reminder_off_leaves_an_open_reminder_where_it_is`, `Applying_settings_that_switch_the_unlock_reminder_off_takes_effect_on_the_next_unlock` | R-013 |
   | | `The_unlock_reminder_is_skipped_while_the_microphone_is_live`, `A_skipped_unlock_reminder_still_leaves_a_full_interval_running` | R-014 |
   | | `An_unlock_still_shows_its_reminder_when_windows_says_the_user_is_away` | R-015 |
   | | `A_lock_never_shows_a_reminder_by_itself` | R-016 |
   | | `The_countdown_starts_with_the_app` | R-020 |
   | | `A_tick_before_the_interval_shows_nothing`, `The_reminder_appears_once_the_interval_has_passed`, `The_periodic_reminder_repeats_every_interval`, `The_countdown_change_event_fires_on_every_tick_and_on_every_reset`, `Dispose_stops_and_disposes_the_timer`, `The_periodic_reminder_says_the_periodic_message` | R-021 |
   | | `A_due_reminder_over_an_open_one_raises_it_and_still_re_arms`, `A_periodic_reminder_due_over_the_unlock_reminder_raises_it_without_changing_its_text` | R-023 |
   | | `An_unlock_restarts_the_countdown`, `A_lock_longer_than_the_interval_postpones_the_reminder_to_a_full_interval_after_unlock`, `Many_lock_unlock_cycles_leave_a_full_interval_after_the_last_unlock`, `A_lock_stops_the_timer_and_clears_the_countdown` | R-024 |
   | | `A_retry_gap_longer_than_the_interval_is_not_mistaken_for_a_clock_jump` | R-025 |
   | | `Applying_settings_restarts_the_countdown_from_zero`, `Switching_the_feature_off_stops_the_timer`, `Switching_the_feature_back_on_starts_the_timer_again` | R-026 |
   | | `A_disabled_feature_never_shows_a_periodic_reminder_and_runs_no_timer`, `Switching_the_periodic_reminder_off_leaves_the_unlock_reminder_alone`, `With_both_reminders_off_nothing_ever_appears_and_no_timer_runs` | R-027 |
   | | `A_due_reminder_is_held_back_while_the_microphone_is_live` | R-030 |
   | | `A_due_reminder_is_held_back_by_a_full_screen_application` | R-031 |
   | | `With_both_pauses_switched_off_nothing_is_ever_held_back`, `With_both_pauses_switched_off_the_monitor_is_never_read` | R-032 |
   | | `A_held_back_reminder_is_looked_at_again_after_the_retry_gap`, `A_reminder_stays_held_back_for_as_long_as_the_call_lasts`, `The_reminder_appears_on_the_first_retry_after_the_call_ends`, `The_full_interval_returns_once_a_held_back_reminder_has_been_shown` | R-033 |
   | | `Idle_ticks_refresh_the_reading_at_the_refresh_interval_not_on_every_tick`, `Joining_a_call_shows_up_in_the_tooltip_without_waiting_for_a_reminder`, `Leaving_a_call_clears_the_tooltip_without_waiting_for_a_reminder` | R-034 |
   | | `An_unlock_forgets_that_a_reminder_was_being_held_back`, `A_lock_forgets_that_a_reminder_was_being_held_back`, `Applying_settings_forgets_that_a_reminder_was_being_held_back`, `Applying_settings_takes_a_fresh_reading_on_the_next_tick` | R-035 |
   | | `Applying_new_messages_takes_effect_on_the_next_reminder` | R-044 |
   | `ReminderSettingsStoreTests` | `The_defaults_have_the_unlock_reminder_on_and_the_same_message_on_both` | R-040 |
   | | `Saving_normalizes_the_messages_before_they_reach_the_registry`, `Message_normalization_keeps_what_it_can`, `A_message_cut_at_the_limit_is_trimmed_after_the_cut` | R-041 |
   | | `The_dialog_range_check_matches_the_clamp`, `The_dialog_message_check_refuses_blank_and_accepts_text`, `The_dialog_message_check_matches_the_length_limit` | R-043 |
   | | `The_settings_live_under_the_key_the_autostart_marker_already_uses`, `Saving_the_settings_does_not_disturb_the_autostart_marker`, `The_unlock_switch_is_written_as_the_same_one_and_zero_strings_as_the_others`, `The_pause_flags_are_written_as_the_same_one_and_zero_strings_as_the_others` | R-050 |
   | | `The_defaults_are_on_and_thirty_minutes`, `An_empty_registry_gives_the_defaults`, `A_corrupt_interval_falls_back_to_the_default_rather_than_failing`, `An_unrecognised_enabled_flag_falls_back_to_the_default`, `An_out_of_range_interval_in_the_registry_is_clamped`, `Saving_clamps_too_so_the_registry_never_holds_an_impossible_value`, `A_registry_without_the_pause_values_lands_on_the_defaults`, `A_hand_edited_pause_flag_falls_back_to_the_default`, `A_corrupt_retry_gap_falls_back_to_the_default`, `An_out_of_range_retry_gap_is_clamped`, `Saving_clamps_the_retry_gap_before_it_reaches_the_registry`, `A_registry_without_the_unlock_values_lands_on_the_defaults`, `A_blank_message_in_the_registry_reads_as_the_default`, `An_over_long_message_in_the_registry_is_cut_at_the_limit` | R-051 |
   | | `Settings_round_trip`, `The_enabled_flag_survives_a_disabled_save`, `Both_pauses_and_the_retry_gap_survive_a_round_trip`, `The_unlock_switch_and_both_messages_survive_a_round_trip` | R-052 |
   | `StartupRegistrarTests` | `First_run_registers_auto_start_and_records_the_marker`, `Later_runs_do_not_re_register_after_the_user_turned_auto_start_off` | R-003 |
   | | `Later_runs_repair_the_path_when_the_app_has_moved`, `Later_runs_do_not_lift_a_Task_Manager_veto` | R-006 |
   | `SuppressionPolicyTests` | `An_idle_desktop_suppresses_nothing`, `A_live_microphone_holds_the_reminder_back`, `A_live_camera_counts_the_same_as_a_microphone` | R-030 |
   | | `A_full_screen_or_presenting_desktop_holds_the_reminder_back`, `No_other_foreground_state_holds_anything_back` | R-031 |
   | | the other seven | R-032 |
   | `TrayTooltipTests` | `Without_a_countdown_the_tooltip_is_just_the_banner`, `With_a_countdown_the_tooltip_says_how_many_minutes_are_left`, `A_held_back_reminder_says_why_instead_of_counting_down`, `Nothing_held_back_leaves_the_countdown_exactly_as_it_was` | R-060 |
   | | `Part_minutes_round_up`, `The_last_minute_reads_as_one_rather_than_zero` | R-061 |
   | | `The_longest_tooltip_the_app_can_produce_fits_the_tray_limit`, `An_over_long_banner_is_truncated_rather_than_allowed_to_throw` | R-062 |
   | `UserNotificationStateTests` | all three | R-031 |
   | `VersionTests` | `The_version_is_a_three_part_number`, `The_version_comes_from_the_build_props`, `The_version_is_not_the_four_part_or_source_tagged_form`, `No_project_overrides_the_shared_version`, `The_banner_is_the_name_and_the_version` | R-090 |
   | | `About_opens_with_the_banner_and_the_copyright` | R-091 |
   | `WindowPlacementTests` | all three | R-070 |

   Every test method gets a name (the suite is small and every test pins a row); the methods of
   the fakes inside `ReminderCoordinatorTests` (`Start`, `RaiseUnlocked`, `Show`, `Advance`,
   ...) and of `FakeRegistryStore` are not tests and are not renamed. The rows with no test
   (R-001, R-002, R-042, R-045, R-063, R-071 to R-076, R-080, R-082: the App layer) stay
   `accepted`.
3. **Statuses**: every row with an `R###_` test moves to `implemented`; `RepoInvariantTests`
   fails on either direction being off. Expect about 42 `implemented` rows of about 55.
4. **Gate.** `scripts/verify.ps1` green (now with `RepoInvariantTests`, 222 tests;
   `LicenseHeaderTests` sees the new file). Commit 3.

## Close

1. `/docs-health` once, through `general-purpose` with the text of
   `.claude/agents/docs-auditor.md` as the prompt (the agent type registers at the next
   session start). Relay the report in full; fix nothing; its gaps are the next tasks, listed
   at the end of this file as ImageVisualCompare's plan does.
2. `/wrap-up`: verify, `CHANGELOG.md` already has its `Internal` line (Appendix D.5; adjust the
   counts to what was done), requirements statuses, the 33 ADRs, this plan's "What deviated",
   README checked, `check-design.ps1 -Since main`. Then propose the merge: `git merge --no-ff
   claude/Adopt-the-starter-kit-process` in the main checkout, and ask who runs it. After the
   merge, `git branch -d` and `git worktree remove`.

## Verification

- `scripts/verify.ps1` green after each layer; the last ten lines pasted each time.
- `scripts/sync.ps1 -Check` exit 0; `scripts/check-decisions.ps1` and `check-design.ps1` exit 0
  on their own; `scripts/check-testdata.ps1` exit 0 with nothing listed.
- `wc -l CLAUDE.md` under 200; `RepoInvariantTests` green (the counts: `implemented` rows and
  `R###` IDs in the sources agree both ways).
- `git diff main...HEAD --stat -- src tests`: the format pass (five files, usings only), the
  three csproj files (three properties gone from each; three `Version` attributes gone from the
  test csproj), `AboutWindow.xaml` (one comment), `RepoInvariantTests.cs` (new), every test
  file (renamed methods), three `packages.lock.json`; nothing else.
- `dotnet test` reports 215 passed after layers 1 and 2, 222 after layer 3.
- The application launched once from the worktree with `--preview "Adopted the kit"` (the
  build output under `src/MindfulnessReminder.App/bin/Release/net8.0-windows/`) and the window
  looked at: the text drawn, Esc closes it. Nothing in this task touched what it draws, but
  `Directory.Build.props` changed and the App csproj lost three lines, and the WPF layer is
  checked by launching (ADR 0008).
- The old `CLAUDE.md`'s every line accounted for by the script of section 2.6; its output
  pasted under "What deviated" if anything was outside the ranges.

## What deviated

Implemented on 2026-10-09 in one session, the one that wrote the plan, after the owner approved
it and switched the session's model; on `claude/Adopt-the-starter-kit-process`, in three
commits, one per layer, each with the gate green.

- **The format pass also dropped a byte order mark.** Rewriting `App.xaml.cs` to remove its two
  unused usings saved it as UTF-8 without the mark it had, as `charset = utf-8` asks; it was the
  only source file with one (`MindfulnessReminder.sln` keeps its own, which `dotnet format` does
  not touch). The plan made any format change other than a removed using a checkpoint; this one
  changes no character of the file's text, so it was kept and recorded in ADR 0003 and here
  rather than stopping the task. Putting the mark back is one byte, if the owner wants it.
- **The gate takes about 20 s**, not 30: 18 to 19 s measured. `CLAUDE.md` and the README say
  so.
- **The settings comment** is the kit's text with "the project's" made "MindfulnessReminder's",
  `ADR 0002` added to its last citation and one sentence on the two `allow` entries of the
  project's own, rather than ImageVisualCompare's text, whose last sentence lost its verb.
- **Documents were written with the Write tool, not shell heredocs.** The shell this session
  runs through passes the command through a Windows layer that expands `%...%` pairs
  (`%LOCALAPPDATA%` in the README's deployment note, Python's `%` formatting), mangles quotes
  after a backslash and collapses a doubled backslash; three heredocs failed to parse and did
  nothing. No file was damaged.
- **The design notes' Covers sentences name only what each note describes** (the kit's ADR
  0032), which differs from the table in section 2.3: `pausing.md` leaves out
  `ActivitySnapshot.cs`; `two-reminders.md` adds `RegistrySettingsStore.cs` ("the registry goes
  through it silently") and `BreakWindow.xaml.cs` (`OnSourceInitialized`); `windows.md` leaves
  out `AboutWindow.xaml.cs` and `WindowPlacement.cs`; `dark-theme.md` adds
  `SettingsWindow.xaml.cs` (`OnSourceInitialized`); `settings-storage.md` leaves out
  `ISettingsStore.cs` and `WindowsRegistryStore.cs` and adds `SettingsWindow.xaml.cs` (the
  dialog that refuses); `code-map.md` names the two code folders, the test csproj and
  `Directory.Build.props`, since its test line describes the project, not each file. A script cut
  the nine notes out of the old `CLAUDE.md` and confirmed that, pointers aside, they are its
  lines unchanged, and that the lines in no note are exactly the title and opening paragraphs,
  "Conventions", "Build & test" and "Committing". The pointers are "See ADR NNNN." sentences
  after the paragraphs; `code-map.md`'s header also says which note each section it names in
  quotation marks is now. Two moved phrases the code had outgrown before today carry a pointer
  beside them: the dark theme "merged into `SettingsWindow` **alone**" (`AboutWindow` too since
  432edfd, ADR 0030), and the capture record under `HKCU` (`HKLM` is read too, ADR 0016).
- **`CLAUDE.md` is 151 lines.** Two rule names differ from Appendix A so that `docs/skills.md`
  can cite them: "The reminder window is light and swallows Enter" and "Settings are strings,
  read forgivingly". Its Commands keep the one fact of "Build & test" no other line held: neither
  switch claims the single-instance mutex.
- **Requirements: 55 rows, as planned.** Checking them against the code changed three details
  of the plan's sketch: R-002 is one copy per Windows session (the mutex is `Local\`), R-030
  reads both `HKEY_CURRENT_USER` and `HKEY_LOCAL_MACHINE`, and R-070's clamp keeps both the left
  and the top edge inside the work area.
- **ADRs 0001 to 0006 and 0034 were written by this session, 0007 to 0033 by three
  subagents** from the plan's table, the old `CLAUDE.md` and README, the code and `git log`,
  then read against their sources by this session. No row was merged or split; where two rows overlap, the ADRs cite each
  other.
- **ADR 0034 was added, so there are 34 ADRs, 28 of them retroactive.** The subagent that wrote
  ADR 0007 to ADR 0015 found one decision of the old `CLAUDE.md` that no row held: `README.md`
  as the user documentation and its single source ("Structure"). The plan says such a decision
  is added at the end; `CLAUDE.md`'s rule "User documentation lives in `README.md`, and only
  there" cites it.
- **ADR 0004 is dated from the right commit.** It said `CLAUDE.md` had carried the English
  convention since the base version; `CLAUDE.md` was first written in b6554d5 (2026-09-08), and
  the README has been English since 61a0121. Corrected before commit 2. The plan's decision 4
  says the same and is left as approved.
- **176 test methods renamed for 42 rows; 13 rows stay `accepted`**: R-001, R-002, R-042,
  R-045, R-063, R-071 to R-076, R-080 and R-082, all in the App layer, which has no unit tests
  (ADR 0008). A script applied the plan's mapping, which covered every test method once; the
  test files' diff is the 176 renames and nothing else. `RepoInvariantTests` has seven tests and
  fails, as it should, when a tested row is set back to `accepted` (tried on R-024, then
  restored). The suite is 222 tests.
- **The renamed tests are named in nine documents**, not only in the five sentences the plan
  listed: ADR 0011, 0013, 0021 and 0025, four design notes, and the README's "Versioning", which
  names `No_project_overrides_the_shared_version`. All nine carry the new names; that sentence
  is the one README change besides the development section and the gate sentence.
- **The application was launched once** from the worktree, with `--preview "Adopted the kit"`:
  the light card, the dot, the text and the Close button, 440 x 260 at 740, 390 on a 1920 x 1040
  working area, R-070's own example. Only that window was captured.
- **Findings, left for tasks of their own** (the ADRs state what the code does, and say so):
  - comments that describe an earlier state of the code: the summary of `IActivityMonitor`
    (read "ONLY" when a reminder is due; the idle refresh reads it every 10 s, ADR 0019), the
    summary of `RegistrySettingsStore` ("Both values"; there are eight, ADR 0027), the header of
    `DarkTheme.xaml` (merged into `SettingsWindow` "ALONE"; About too, ADR 0030), the comment on
    `ReminderSettings.MaxMessageLength` (50 characters are "two lines"; they can be three, which
    the `Viewbox` shrinks, ADR 0024), the comment on `IntervalSchedule.Restart` ("the only place
    a deadline is ever set"; `Postpone` sets one too, ADR 0014), the summary of `IPeriodicTimer`
    ("The one timer the whole app owns"; the presenter and `--detect` have their own, ADR 0012),
    the comment on the fallback in `LicenseInfo` ("the csproj is the truth"; it is
    `Directory.Build.props`, ADR 0010), the summary of the backwards-jump test in
    `IntervalScheduleTests` ("a whole interval"; the armed span, ADR 0013), and the App csproj's
    "Windows Forms purely for NotifyIcon" (`BreakWindow` also takes `Screen` from it, ADR 0007);
  - the README: the 10 s refresh is "200× cheaper than every tick" (ten times fewer reads, ADR
    0019); the `--detect 60` example pads its columns, starts at 0:00 and counts 61 readings, where
    the code pads nothing and takes its first reading after one second (R-081); the capture
    record is named under `HKCU` only (`HKLM` is read too, ADR 0016);
  - `ReminderSettings.IsRetryInRange` has no test, although R-043 is `implemented` through the
    tests of the interval and message checks;
  - 2be5361 also moved the default message within the window (centred between the dot and the
    Close button), where the sources say it is drawn "exactly as before"; not compared on screen
    with an older build (ADR 0024);
  - three small choices are explained only in code comments and have no ADR: the per-monitor DPI
    mode set before any window exists, `EnableVisualStyles` for the tray menu, and hiding the
    tray icon before disposing it;
  - a few small rules sit in the App with no test (ADR 0008 lists them).
- **The audit corrected six statements of this task's own text**: ADR 0001 named ADR 0007 to
  ADR 0033 as the retroactive ones (0034 now); `docs/design/README.md` said the notes were moved
  unchanged without the bracketed pointers; `docs/skills.md` counted 215 tests (222); ADR 0005
  said every test runs on fakes (the seven `RepoInvariantTests` read the documents); and R-031
  and ADR 0017 called `QUNS_QUIET_TIME` Focus Assist, which no reading shows and Microsoft's
  documentation contradicts: R-031 no longer says it, ADR 0017 says it is unverified. The README
  still says it; that is gap 2 of the docs-health report below.
- **The audit ran before commit 3**, so the three commits the owner asked for carry it.
- **A code review followed** (`/code-review`, maximum effort, ten finder angles and a verifier
  per finding), and the owner had its fifteen findings applied on the branch, in a fourth
  commit. Three fixed behaviour the code had before the migration, each with a failing test
  first: a skipped unlock no longer leaves "paused" in the tooltip when there is no countdown
  (R-060); a registry the account may not read or write no longer stops the start (R-003,
  R-006, R-051); a tray-menu error reaches the application's own crash dialog (R-076). The
  others: `R090_No_project_overrides_the_shared_version` no longer scans `.claude/`, where it
  found two projects of a task worktree and would have refused the owner's merge; the test
  project keeps real source paths in a CI build (`DeterministicSourcePaths` false), which would
  otherwise have failed 14 tests; `RepoInvariantTests` reads test IDs from the compiled test
  methods and fails on a row with a mis-written status; a publish leaves out the XML
  documentation files; the project's `.gitattributes` is gone, since its `*` line overrode the
  root's CRLF rule for `.cmd` and `.bat`; `.claude/settings.json` no longer allows `dotnet run`
  or the executable without asking, and `docs/skills.md` no longer sends a session to start a
  tray copy of a worktree build, which would repoint the owner's auto-start entry (R-006);
  `CLAUDE.md`, `docs/skills.md` and ADR 0022 say what `--preview` does and does not run; and
  five documents were corrected: the SDK requirement (README, `CLAUDE.md`), the 127-character
  limit of `NotifyIcon.Text` (R-062, ADR 0025, the note), Windows Forms' uses (`CLAUDE.md`),
  and the constants, which the tests use by name, not by value (`docs/requirements.md`,
  `docs/validation.md`, ADR 0005).

## The docs-health report of 2026-10-09

Run once, before commit 3, through `general-purpose` with the text of
`.claude/agents/docs-auditor.md` (the agent type registers only at the next session start). Its
list of files opened holds nothing under `src/` and no test source; the audit is valid. The
small change it planned from the docs alone: a third pause, "Pause while Focus Assist is on",
behind its own switch.

Verdict: **mostly**. From the docs alone a fresh model understands the project and can place
every file of that change; it cannot write the first test without opening the test fakes, and
the change rests on a claim no document shows was measured.

Gaps (what is missing, where it should go, effort):

1. How the tests are built: how `ReminderCoordinator` is constructed with its fakes, how a test
   raises an unlock, ticks the timer, moves the clock, sets an `ActivitySnapshot` and reads what
   the presenter was asked; the signatures of `SuppressionPolicy.Evaluate`, `ActivitySnapshot`,
   `DetectionLog.Render` and `WindowPlacement.CenterIn`. A new note in `docs/design/` whose
   Covers names `ReminderCoordinatorTests.cs` and `FakeRegistryStore.cs`, pointed to from
   `docs/skills.md` (/new-feature step 2). An hour.
2. No reading shows that Focus Assist is `QUNS_QUIET_TIME` (6); Microsoft documents that state as
   the first hour after a first sign-in following an upgrade. A `--detect 60` run while switching
   Focus Assist on and off, recorded as a dated note in ADR 0017; the README corrected if the
   claim is wrong. Minutes, a session if it is wrong. (This task removed the claim from R-031 and
   marked it unverified in ADR 0017; the README still makes it.)
3. Which sentences of an `implemented` row a test proves: R-005, R-030, R-031, R-040, R-043, R-044,
   R-070 and R-081 hold App-only parts no test covers, and R-043's retry check
   (`IsRetryInRange`) has no test at all. Split each into a Core `implemented` row and an App
   `accepted` row; add the missing retry test. `docs/requirements.md`, an hour.
4. Where the App's small rules live is stated two ways: `CLAUDE.md` "Layout" says the App decides
   nothing, ADR 0008 lists four exceptions and misses two more (the tray item's click rule, ADR
   0028; the greying in Settings). `CLAUDE.md`, a dated note in ADR 0008. Minutes.
5. How to look at a change "in the copy in the tray" (`docs/skills.md`) while the owner's
   auto-started copy holds `Local\MindfulnessReminder.SingleInstance`, so a worktree build exits
   silently (R-002): exit the owner's copy, or ask first. `docs/skills.md`. Minutes.
6. Known errors with no follow-up: the README's "200× cheaper", and the stale comments ADR 0019
   and ADR 0027 record. The README; this plan's findings list (they are under "What deviated").
   Minutes.
7. This plan's "What deviated" was still empty when the audit ran (34 ADRs, the Covers lists, the
   bracketed pointers in three "moved unchanged" notes). Filled at wrap-up, above. Minutes.
8. 27 of the 55 rows have no `Example:` with numbers; it matters for R-034 (its ten seconds are
   tested as 10 or 11 reads in 100 idle ticks, a bound only ADR 0019 gives), R-082, R-032 (both
   conditions at once) and R-005 (a click per status). `docs/requirements.md`, an hour.
9. The design notes repeat their ADRs nearly paragraph by paragraph (`periodic-reminder.md`
   against ADR 0012 to 0014, `tray-tooltip.md` against ADR 0025), against the kit's rule "Design
   notes"; ADR 0001 and `docs/design/README.md` already defer it. `docs/design/`, a session.
10. Two `RepoInvariantTests` rules are stated only in this plan: `Claude_md_stays_short` (200
    lines) and `The_design_notes_have_their_index`. `CLAUDE.md` "Style" or the README's
    "Development". Minutes.
11. "Eight" settings values is written in four places (the `CLAUDE.md` rule "Settings are strings,
    read forgivingly", R-050, `settings-storage.md`, ADR 0027), so a new setting edits all four.
    Drop the count from `CLAUDE.md`. Minutes.
12. Dated counts went stale with layer 3: `docs/skills.md` /mutate said 215 tests, and ADR 0005
    said every test runs on fakes. (Both corrected in this task.)
13. The Settings window's layout: whether it grows with a new row, and whether the greying is
    bound in XAML or set in code. `docs/design/windows.md`. Minutes.
14. `CLAUDE.md` "Workflow" repeats `docs/skills.md` (/wrap-up step 9, /release step 5).
    `docs/skills.md` only. Minutes.

Top three, in the auditor's words: no document says how to build or drive the coordinator's
fakes, so the first failing test of any change needs the code; the Focus Assist change depends
entirely on `QUNS_QUIET_TIME` meaning Focus Assist, which three documents assert with no reading
on record; and a reader cannot tell which sentences a green suite proves, or where the App-side
rules live.

Guesses it made (each a gap by definition), in short: that the plan's appendices are what was
applied; how `RepoInvariantTests` reads the IDs and the status column; that the moved notes
differ only by pointers; the signature of `IsMessageAcceptable`; how `--preview` collects its
text; whether the greying is bound or coded; how the presenter keeps the text of a window still
in its delay; and, for the Focus Assist change, that the adapter needs no change, where the check
and its precedence go in `SuppressionPolicy`, that `PausesForAnything` must gain the third
switch, the names, that the tooltip's length test covers the new reason, which tests a default of
"on" would break, how the fake reports quiet time, where the checkbox goes, and how to see the
tray copy while the owner's holds the mutex.

What is good: requirements and tests match both ways (42 `implemented` rows, each with an `R###_`
test; 13 `accepted` rows, none with one; no test names an unknown ID), and the core rows' examples
check by hand (R-024, R-025, R-033, R-070, R-081); the ADRs name members, constants, the
alternatives, the guards and how to extend each decision, enough to place every file of the
change; `CLAUDE.md` is 151 lines, every rule cites its ADR and names the kit rule it extends; no
ADR is `proposed`, and `docs/validation.md` says plainly why there are no figures.

None of the gaps is closed in this task beyond the corrections to its own text (gaps 2, 7 and 12,
and ADR 0001's range); each is a task of its own (`/docs-health` step 4).

---

## Appendix A: the new `CLAUDE.md`

The text below is the target; the implementing session adjusts a path, a count or a timing
that turned out otherwise, and nothing else.

````markdown
# Mindfulness Reminder

A tray-resident break reminder for Windows. It shows an always-on-top reminder window on
every workstation unlock, and at a configurable interval while the user works -- unless the
user is on a call or in a full-screen application, in which case it waits. The two reminders
have a switch and a message each, and both say "Ground yourself" until changed. .NET 8,
**zero external dependencies**: WPF for the windows, Windows Forms purely for `NotifyIcon`,
both from the WindowsDesktop shared framework (ADR 0007).

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, about 30 s):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build MindfulnessReminder.sln -c Release --nologo` (Windows only: WPF)
- Test: `dotnet test MindfulnessReminder.sln -c Release --nologo`
- One test: `dotnet test MindfulnessReminder.sln --filter "FullyQualifiedName~R024"`
- Format: `dotnet format MindfulnessReminder.sln` (check only: `--verify-no-changes`)
- See the reminder without locking the machine:
  `src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --preview ["text"]`
  (no tray icon, no registry, no timer; with a text, that text after the same normalization a
  saved one gets, R-045)
- See what the pause checks detect: `... MindfulnessReminder.exe --detect [seconds]`; with a
  duration it reports a timeline, the only way to test the full-screen check (R-080 to R-082)
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of Core (minutes, not in the gate): `scripts/mutate.ps1 [-Mutate "**/IntervalSchedule.cs"]`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)
- There are no validation figures: `docs/validation.json` lists no case (ADR 0005), so
  `/validate` has nothing to run.

## Layout

- `src/MindfulnessReminder.Core/` - `net8.0`, no UI dependency: every rule the app has
  (`ReminderCoordinator.cs`, `IntervalSchedule.cs`, `ReminderSettings.cs`,
  `SuppressionPolicy.cs`, `TrayTooltip.cs`, `DetectionLog.cs`, the settings store, auto-start,
  placement, version and licence) and the seams the adapters fill. All of it unit tested.
- `src/MindfulnessReminder.App/` - `net8.0-windows`, `WinExe`, the binary is
  `MindfulnessReminder.exe`: the three windows, the tray icon, the dark theme, and one Windows
  API per file behind a Core interface. Nothing here decides anything; none of it is
  unit-tested (ADR 0008).
- `tests/MindfulnessReminder.Tests/` - `net8.0`, references **Core only**: every rule on
  fakes, `RepoInvariantTests`, `LicenseHeaderTests`, `VersionTests`. `tests/testdata/` holds
  an empty manifest: there is no test data.
- `Directory.Build.props` - `<Version>` and `<Copyright>`, the single place for both (ADR
  0011), and the strictness every project builds with (ADR 0003).
- `kit.json` - what the kit's scripts, skills and agent need to know about this project.
  `docs/skills.md` - this project's additions to the kit's skills. `scripts/`,
  `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process keys of
  `.claude/settings.json` are the kit's, the same here (ADR 0002; the kit's rule "Shared
  folders").
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs.
  `docs/validation.md` + `.json` - no figures, and why. `docs/design/` - the explanations, per
  area, read on demand. `docs/plans/` - approved plans. `README.md` - the user documentation,
  English.
- The process rules are the kit's, in `.claude/rules/kit/process.md`; this project's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).
- This folder lives inside the `photo` monorepo (`E:\Photografie\Code`); the git root is the
  parent. The root `.gitignore` has a `*.app` rule that would hide `src/MindfulnessReminder.App/`;
  this project's `.gitignore` un-ignores it, keep that line.

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are Mindfulness Reminder's own; one that
extends or varies a kit rule names it.

- **All text is in English** (extends the kit's rule "Languages"): UI strings, comments,
  documentation and the README too. The sibling `FlatCheck` tool is Romanian; that convention
  is its own, do not carry it over (ADR 0004).
- **Zero external dependencies** (extends the kit's rule "Packages"). `NotifyIcon` and
  everything else come from the shared framework; WPF having no numeric spinner is not a
  reason to add a package, the settings dialog hand-validates a `TextBox` (ADR 0007).
- **Logic in Core, adapters in App.** If a new rule cannot be tested because it sits in the
  App project, it is in the wrong project; the test project deliberately does not reference
  App, so the build enforces it (ADR 0008).
- **Licence headers.** Every `.cs` and `.xaml` file starts with the GPL notice, new files
  included; `LicenseHeaderTests` fails otherwise. XML comments cannot contain `--`, so the
  em-dash of C# comments is reworded in XAML (ADR 0009).
- **One timer, one deadline, on the wall clock.** The timer ticks every second and
  `IntervalSchedule` compares a deadline against `IClock.UtcNow`; never a timer armed for the
  interval, never a count of ticks. A lock suspends the schedule and an unlock restarts it in
  full, before the debounce and the already-showing check (ADR 0012 to ADR 0014).
- **Read the device, not the window title.** A call is the `ConsentStore` capture record;
  full screen is `SHQueryUserNotificationState` mapped in Core, and `QUNS_NOT_PRESENT` must
  not suppress. Do not reintroduce a table of window titles (ADR 0016, ADR 0017).
- **A periodic reminder is postponed, an unlock reminder is skipped**, and there is no cap on
  how long one is held back; the tooltip carries the reason (ADR 0018).
- **The activity monitor is never read on the one-second tick**: when a reminder is due, once
  per retry, and every 10 s idle so the tooltip is current (ADR 0019).
- **The coordinator picks the text, the presenter draws it**; `BringToFront` takes nothing;
  messages are normalized like numbers are clamped, and the dialog refuses a blank one only
  for a reminder that is on (ADR 0022, ADR 0023).
- **`BreakWindow` is not dark and swallows Enter** in `OnPreviewKeyDown`; its Close button is
  not `IsDefault`. Settings and About are modeless, never `ShowDialog` (ADR 0026, ADR 0030).
- **The dark theme is merged per window, never into `Application.Resources`**; every template
  carries its own disabled trigger; the palette is repeated in `DarkMenuRenderer` and never
  moves into Core (ADR 0030, ADR 0031).
- **Settings are strings under `HKCU\Software\MindfulnessReminder`**, read forgivingly and
  clamped, never rejected; the dialog is the one place that refuses (ADR 0027).
- **The tray tooltip fits 63 characters**; `TrayTooltip` is the pure function that proves it
  (ADR 0025).
- **User documentation lives in `README.md`, and only there.** It is the single source for it;
  `docs/` explains what the code does and why, for whoever maintains it.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. Mindfulness Reminder adds:

- `/wrap-up` checks `README.md`, the user documentation, and launches the application with
  `--preview` when a file under `src/MindfulnessReminder.App/` changed (the App layer is not
  unit-tested, ADR 0008).
- A release is tagged `mindfulnessreminder-vX.Y.Z`, the name `scripts/release.ps1` prints; it
  publishes the application framework-dependent into `artifacts/X.Y.Z/` (ADR 0002).
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.

## Read before touching

`docs/design/README.md` maps every area to its note. The ones most likely to catch a careless
edit:

- `IntervalSchedule.cs`, `ReminderCoordinator.cs` or the tick: `docs/design/periodic-reminder.md`
  and ADR 0012 to ADR 0014.
- `HandleTick`, `SuppressionPolicy`, `WindowsActivityMonitor`, `--detect`:
  `docs/design/pausing.md` and ADR 0016 to ADR 0020.
- `ReminderSettings`, `HandleUnlock`, the messages: `docs/design/two-reminders.md` and ADR
  0021 to ADR 0024.
- Any XAML, a new window: `docs/design/dark-theme.md` and `docs/design/windows.md`, ADR 0026,
  ADR 0030 to ADR 0032.
- A new setting: `docs/design/settings-storage.md` and ADR 0027.
- `Version.cs`, `LicenseInfo.cs`, `LicenseHeaderTests`: `docs/design/versioning-and-licence.md`,
  ADR 0009 to ADR 0011.
- A file you have not opened before: `docs/design/code-map.md`.

## Style

- `.editorconfig` holds whitespace rules and `IDE0005`; `dotnet format` enforces them, and so
  does the build, which writes the XML documentation file for that reason (CS1591 muted).
  Warnings are errors, `EnforceCodeStyleInBuild` and `Nullable` are on, in
  `Directory.Build.props` (ADR 0003). The kit's code-style rules and the analyzers are not
  enabled: 374 analyzer findings on 2026-10-09, 352 of them the underscores in test names; a
  style pass is a task of its own.
- Test names are sentences with underscores, `R###_` first.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
````

## Appendix B: `kit.json`

```json
{
  "$comment": "What the shared scripts, skills and agent need to know about MindfulnessReminder. With docs/skills.md, the only file edited here to use them; scripts/, .claude/skills/, .claude/agents/ and .claude/rules/kit/ are byte-identical with ../ClaudeCodeStarterKit's (scripts/sync.ps1 keeps them so; the kit's ADRs 0017, 0018 and 0028; ADR 0002). The layout is the kit's default, src/ and tests/. There is no test data and no validation case (ADR 0005): the manifest lists nothing, testFilter is empty, and MINDFULNESS_TESTDATA_LARGE is the override kit-config.ps1 requires a name for, which nobody sets. The release publishes the application framework-dependent, as the README's deployment note does, so no runtime is declared (ADR 0002; the kit's ADR 0034 does not apply). See scripts/kit-config.ps1 for every key.",
  "solution": "MindfulnessReminder.sln",
  "testFilter": "",
  "codeFolders": ["src/"],
  "codeFiles": [],
  "testFolders": ["tests/"],
  "testData": {
    "folder": "tests/testdata/",
    "largeEnv": "MINDFULNESS_TESTDATA_LARGE"
  },
  "mutate": {
    "testProject": "tests/MindfulnessReminder.Tests/"
  },
  "release": {
    "publish": [
      {
        "project": "src/MindfulnessReminder.App/MindfulnessReminder.App.csproj",
        "output": "",
        "args": []
      }
    ]
  },
  "audit": {
    "alsoRead": ["README.md"]
  },
  "kit": "../ClaudeCodeStarterKit"
}
```

## Appendix C: `docs/skills.md`

````markdown
# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018; ADR 0002). What Mindfulness Reminder needs on top of them is here, one section per
skill; each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a requirement about a reminder says which of the two it is (unlock or periodic),
  what the schedule does (restart, suspend, postpone) and what the tooltip reads; one about a
  setting names the registry value, its default and its bounds (the rule "Settings are
  strings"); one about a window says what is on screen and which keys do what.
- At step 2: every test lives in `tests/MindfulnessReminder.Tests`, on the fakes the suite
  already has (`FakeRegistryStore`, the fakes inside `ReminderCoordinatorTests`: a clock, a
  timer, a session monitor, a presenter, an activity monitor). A rule gets its test in Core;
  if it cannot be tested there, it is in the wrong project (the rule "Logic in Core, adapters
  in App"). Test names are sentences with underscores, `R###_` first.
- At step 5: `MindfulnessReminder.Core` first, then the adapter or window. A change under
  `src/MindfulnessReminder.App/` is not done until the application was launched (`--preview`
  for the reminder window, the tray copy for Settings and About) and looked at.
- At step 6: there is no numeric code and no validation case (ADR 0005); skip, unless the
  feature measures something on a machine, in which case the first case and `ValidationTests`
  are written then.
- At step 8: `README.md` is English and is the user documentation; its "Behaviour" list, the
  section of the feature and the "Design decisions" table are where a user-visible change
  goes.

## /wrap-up

- At step 2: there is no numeric code and no golden file; nothing to do.
- At step 9: a change under `src/MindfulnessReminder.App/` also launches the application once
  with `--preview` before the commit; a change to the tray menu, Settings or About is looked
  at in the tray copy (no switch opens them).

## /validate

- At step 1: there is no large file and no case; `docs/validation.json` has `"cases": []`
  (ADR 0005). Say so and stop after step 1; steps 2 to 5 have nothing to run. When the first
  case is added, this section is rewritten.

## /release

- At step 3: no cases; skip.
- At step 5: the application is published framework-dependent into `artifacts/X.Y.Z/` itself
  (`dotnet publish -c Release` of `src/MindfulnessReminder.App`, no runtime); the tag is
  `mindfulnessreminder-vX.Y.Z`. The README's "Deployment note" says where the owner publishes
  for themselves (`%LOCALAPPDATA%\MindfulnessReminder`); a release does not go there.
- At step 7: the README's "Build and run" section names the build and test commands and the
  two switches; `artifacts/` is what a release produces.

## /mutate

- At step 1: Core is small (26 files, 215 tests on 2026-10-09), so a whole-core run is
  reasonable; `scripts/mutate.ps1 -Mutate "**/IntervalSchedule.cs"` is the shape for one
  file. No run has been measured yet; write the first one's time and score here.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `README.md`, the user
documentation, which states behaviour and is not code)
````

## Appendix D: the small files

### D.1 `.claude/settings.json`, the `$comment`

ImageVisualCompare's comment with "ImageVisualCompare's" replaced by "MindfulnessReminder's"
(read it in `../ImageVisualCompare/.claude/settings.json`). The rest of the file is the kit's
and is not edited, except the two `allow` entries of layer 1 step 2, which are the project's.

### D.2 `global.json`

```json
{
  "sdk": {
    "version": "9.0.318",
    "rollForward": "latestPatch"
  }
}
```

### D.3 `Directory.Build.props`

```xml
<Project>

  <!-- Shared by the three projects. The single place for the version, the copyright and the
       strictness (ADR 0003). Do not move <Version> or <Copyright> into a csproj: VersionTests
       reads this file, and scripts/release.ps1 writes <Version> here. No TargetFramework here:
       the three projects differ (net8.0, net8.0-windows). -->
  <PropertyGroup>
    <!-- The app version. Bumped MANUALLY, by hand, per feature or fix. MSBuild turns it
         into AssemblyInformationalVersion, which is where VersionInfo.Value reads it from
         and what Windows shows as the .exe's file version - so this is the only place to
         change it. See "Versioning" in README.md (ADR 0011; the kit's ADR 0029). -->
    <Version>1.4.0</Version>

    <!-- Copyright and licence, for every project in the solution at once. <Copyright>
         ends up in AssemblyCopyrightAttribute, which is where LicenseInfo.Copyright
         reads it from and what Windows shows under the .exe's Properties > Details -
         so this is the only place to change it. The program is GPLv3; the full text
         is in LICENSE, next to this file (ADR 0010). -->
    <Copyright>Copyright (C) 2026 Tudor Carean</Copyright>
    <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>

    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <!-- IDE0005 (unused usings) only runs at build when the documentation file is generated.
         CS1591 (missing XML comment) is muted: comments here are for the "why", not for every
         member (394 of them on 2026-10-09). -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <!-- Reproducibility (ADR 0003): the same sources, the same bytes; every package version once,
       in Directory.Packages.props; the full graph in packages.lock.json, which CI restores in
       locked mode. -->
  <PropertyGroup>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>
  </PropertyGroup>

</Project>
```

### D.4 `.editorconfig`

```ini
# Whitespace rules, enforced by `dotnet format --verify-no-changes` in scripts/verify.ps1.
# EnforceCodeStyleInBuild, Nullable and warnings-as-errors are on in Directory.Build.props; the
# kit's code-style rules (braces, var) and the analyzers are deliberately not enabled here
# (ADR 0003): a style pass over the code base is a task of its own.
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# XAML is not in this group: this project's XAML is four-space indented.
[*.{json,yml,yaml,props,targets,csproj,sln}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.{ps1,sh}]
end_of_line = lf

[*.cs]
# unnecessary using
dotnet_diagnostic.IDE0005.severity = warning
```

### D.5 `CHANGELOG.md`

```markdown
# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and the strictness every project builds with
  moved from the three csproj files to `Directory.Build.props`. `CLAUDE.md` was split into
  `docs/requirements.md` (about 55 rows with IDs), `docs/decisions/` (33 ADRs, 27 of them
  retroactive) and `docs/design/` (9 notes, moved unchanged); `CLAUDE.md` holds only the
  project's rules. `RepoInvariantTests` ties `implemented` rows to tests named `R###_...`
  (215 tests renamed for about 42 rows). There are no validation figures, and
  `docs/validation.md` says why. No behaviour changed; one `dotnet format` pass removed six
  unused usings in five files.

## [1.4.0] - 2026-09-14

The state of the tool when the changelog was started, built between 2026-08-28 and 2026-09-14
in eight commits:

- 1.0.0 (2026-09-01, after the base of 2026-08-28): the reminder window on every unlock,
  centred on the primary monitor; the tray icon with its four-item menu; auto-start under the
  Run key, registered on the first run only, with the Task Manager veto read and a stale path
  repaired; the GPL notice on every source file, the version and copyright from
  `Directory.Build.props`, About.
- 1.1.0 (2026-09-08): a periodic reminder every N minutes while working, one timer driving a
  wall-clock deadline, suspended across a lock; the tooltip countdown; Settings in the tray
  menu; Enter no longer closes the reminder.
- 1.2.0 (2026-09-08): a reminder waits for a call (the microphone or camera in use) or a
  full-screen application; the two pauses and the retry gap in Settings; the tooltip says why
  a reminder is held back; `--detect` and `--detect <seconds>`.
- 1.3.0 (2026-09-08): the Settings window, About and the tray menu are dark; About is a
  window; the application icon.
- 1.4.0 (2026-09-14): a switch and a message for each of the two reminders; messages
  normalized and capped at 50 characters, scaled down to fit the window; `--preview "text"`.

No earlier version was released; the eight commits are in `git log`.
```

### D.6 `Directory.Packages.props`

```xml
<Project>
  <!-- Central Package Management: every package version lives here, once (ADR 0003). The rule
       "Zero external dependencies" (CLAUDE.md, ADR 0007): Core and the application reference
       no package at all; the test project has the three below and nothing else. No new package
       without asking (the kit's rule "Packages"), and every package gets a "why" here. -->
  <ItemGroup>
    <!-- Test host and runner. -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

### D.7 `tests/testdata/manifest.json`

```json
{
  "$comment": "Every test data file outside git would be listed here with its SHA-256; MindfulnessReminder has none (ADR 0005): every test runs on fakes, and the application reads no file. scripts/check-testdata.ps1 reads this file and finds nothing to check. When a file is ever needed, it goes under large/ (git-ignored) with an entry here, and MINDFULNESS_TESTDATA_LARGE may name another folder.",
  "files": []
}
```

### D.8 `.gitattributes`

```gitattributes
# Normalise line endings in the repo. The sources are LF and .editorconfig keeps them so
# (ADR 0003; the photo repository's root .gitattributes says the same, the kit's ADR 0014).
* text=auto eol=lf
*.sh text eol=lf
*.ico binary
*.png binary
```

### D.9 `.gitignore`

```gitignore
bin/
obj/
.vs/
*.user

# The repository root ignores *.app (a macOS bundle rule), which happens to match the
# MindfulnessReminder.App project directory and hides the whole app from git.
!src/MindfulnessReminder.App/

# The kit's process files (ADR 0001).
.verify-stamp
artifacts/
TestResults/
StrykerOutput/
tests/testdata/large/
.claude/settings.local.json
```

### D.10 `tests/testdata/README.md`

````markdown
# Test data

Mindfulness Reminder has none (ADR 0005). Every test runs on fakes: `FakeRegistryStore` for
the registry, and the clock, timer, session monitor, presenter and activity monitor inside
`ReminderCoordinatorTests`. The application reads no file, decodes nothing and measures
nothing on data; the numbers it carries are constants pinned by unit tests.

`manifest.json` lists no file, so `scripts/check-testdata.ps1` has nothing to check and
passes. If a large file is ever needed (a registry export, a recording of `--detect` readings),
it goes under `large/`, which git ignores, with an entry in the manifest; `MINDFULNESS_TESTDATA_LARGE`
may name another folder, as the script allows. Then:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```
````

### D.11 `README.md`, the new section (English, after "## Deployment note")

````markdown
## Development: the gate, the documentation and Claude Code

Since 2026-10-09 the project follows the process of `ClaudeCodeStarterKit` (the folder beside
this one in the `photo` repository; the guide with the reasoning behind each piece is `GHID.md`
there, in Romanian), like FlatCheck, AstroPhotoAnalyzer and ImageVisualCompare. In short, what
is enforced mechanically:

- **`scripts/verify.ps1` is the gate**: the scripts identical with the kit's, the test data
  manifest, the ADR headers, the design notes (each in the index, with its files named in its
  `Covers:` sentence), restore, `dotnet format` (check only), the build with warnings as errors,
  all the tests. About 30 seconds. The git `pre-commit` hook at the repository root runs it on
  every commit that touches the project, the `pre-merge-commit` hook on every clean merge, the
  Claude Code hook before any `git commit` of the model, and `scripts/release.ps1`. Without it
  green no commit goes in.

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
  ```

- **The commit message** is one line, in English, of at most 100 characters.
- **`scripts/`, `.claude/skills/`, `.claude/agents/` and `.claude/rules/kit/` are the kit's**,
  byte for byte, and are not edited here; what is specific to the project as values is in
  `kit.json`, the steps it adds to the skills are in `docs/skills.md`.
- **The behaviour is in `docs/requirements.md`**, with `R-NNN` IDs carried in the test names;
  `RepoInvariantTests` requires every `implemented` requirement to have a test with its ID and
  the reverse. The old `CLAUDE.md` was split on 2026-10-09 into requirements, decisions and
  design notes; nothing was lost, it was moved.
- **The decisions are in `docs/decisions/`**, one ADR per decision, with the alternatives
  rejected; those taken before 2026-10-09 (the periodic reminder's timer and deadline, reading
  the device instead of window titles, the dark theme's per-window merge, among others) were
  written retroactively. The "Design decisions" table above is the short form of the same
  choices.
- **There are no validation figures**: the application has no numeric output and no input
  data, and `docs/validation.json` lists no case; `docs/validation.md` says what would become
  one.
- **The explanations behind the code** are in `docs/design/`, with the index in
  `docs/design/README.md`; `CLAUDE.md` holds only the project's rules, the process rules are
  the kit's, in `.claude/rules/kit/process.md`.
- **The version** is bumped by the owner, in `Directory.Build.props`. A release is made on
  `main`, with `scripts/release.ps1 -Version X.Y.Z`, which runs the gate, closes the CHANGELOG
  section and publishes the application into `artifacts/X.Y.Z/`. The commit and the tag stay
  the owner's.
- In Claude Code: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`.
````

### D.12 `docs/validation.json`

```json
{
  "$comment": "Figures measured on real data, machine-readable, read by ValidationTests (trait Category=Validation, not in the gate) in the projects that have them. MindfulnessReminder has none (ADR 0005): the application has no numeric output and reads no data; the numbers in the code are constants pinned by unit tests. The list is empty so the kit's skills find the file and do nothing; RepoInvariantTests checks it parses and that case IDs are unique. docs/validation.md says what would become a case. When the first one is added, ValidationTests is written with it and kit.json's testFilter becomes Category!=Validation.",
  "cases": []
}
```

### D.13 `docs/validation.md`

```markdown
# Validation

In the projects made from the kit this file is the memory of "how well it works, measured on
what": every case lives in `validation.json`, read by `ValidationTests`, and is explained here.
Mindfulness Reminder has no such case (ADR 0005). The application shows a window, keeps a
deadline on the wall clock and reads two registry keys; it has no numeric output and no input
data to measure on. The numbers it carries are constants, each pinned by a unit test:
`TrayTooltip.MaxLength` (63), `ReminderSettings.MaxMessageLength` (50), the interval bounds 1
to 480 minutes, the retry bounds 5 to 300 seconds, `ReminderCoordinator.DefaultDebounce`
(2 s), `DefaultTickInterval` (1 s), `DefaultActivityRefresh` (10 s),
`WpfReminderPresenter.DefaultShowDelay` (600 ms). A case here would restate a test.

`validation.json` therefore lists no case. `/validate` stops after its first step, `/release`
skips its figures step, and `RepoInvariantTests` checks only that the file parses and that case
IDs are unique, so the shape is guarded when the first case arrives.

## What would become a case

A figure measured on a machine rather than asserted by a test, if one is ever needed to defend
a design choice with a number:

- the time `WindowsActivityMonitor.Read` takes to walk the `ConsentStore` keys, which is the
  reason the reading is taken every 10 s and never on the one-second tick (ADR 0019);
- the time between an unlock and the reminder window being on top, which is the reason for the
  600 ms show delay (ADR 0026).

Either would need a `ValidationTests` class with the trait `Category=Validation`, a case in
`validation.json` with a `tolerance`, `kit.json`'s `testFilter` set to `Category!=Validation`,
and a dated line under History. Until then there is nothing to re-measure, and this file says
so rather than pretending otherwise (the kit's rule "Measured numbers").

## History

- 2026-10-09: the file created with no case (ADR 0001, ADR 0005).
```
