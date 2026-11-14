# Code review of the whole project, 2026-11-13

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0025.

## How it was done

The code was written by an earlier model and had almost no review. On 2026-11-13 it measured,
blank lines included, 3,061 lines (`src/AstroDarkroom.Core/` 1,485, `src/AstroDarkroom.Wpf/`
1,576 with its XAML) and 2,764 of tests: small enough for one session and one list, and still
better read by area, each area whole by a reviewer with a clean context (a subagent on this
session's model). A reviewer first read `CLAUDE.md`, the area's design note, its requirements
and its ADRs, so that what is deliberate was not reported, and read the area's tests for one
question: can this test fail, and does it check what its name says. A reviewer changed nothing
and built nothing. Every finding it returned was then checked by the session that wrote this
list: the cited lines were opened and the case followed through the callers. The seams between
the areas (what the core returns against how the window uses it, the exceptions `OpenApp`
catches, the start-up order in `App`) were read by the session itself while the reviewers
worked.

| # | Area | Reported | In this list as |
|---|------|---------:|-----------------|
| 1 | Core: the catalogue, the plan of a start, `release.json`, the setting, the version, the texts | 4, and 6 notes on tests | CR-002, 004, 012, 013, 020 |
| 2 | The log, the start and the stop | 2, and 7 notes on tests | CR-008, 011, 021 |
| 3 | The window | 5 | CR-003, 005, 006, 009, 010 |
| 4 | The suite's checks and what the release ships | 8 | CR-001, 014 to 019 |
| - | Seams, read by the session | 1 | CR-007 (CR-006 and CR-011 seen there too) |

19 findings were reported, besides the notes on tests, and none was rejected at the check. Two
reports of one defect became one entry (CR-011, seen from areas 1 and 2), two findings about
`LICENSE` became one (CR-017), and the notes on tests and on stale comments were grouped
(CR-020 to CR-022), which gives the 22 entries below. That no finding was rejected says the
reviewers were careful, not that every entry is proved: the check was a reading. Three claims
that a reading could not settle were measured, in a throw-away console program outside the
repository, on .NET 8: the exception a half surrogate escape raises (CR-002), the message of a
start Windows refuses (CR-006) and the replace over a read-only file (CR-012). The launcher
itself was not built or run, and its window was never looked at; what rests on a screen is
marked `to measure`.

Out of scope: `scripts/` and `.claude/` (the kit's; the owner left them to a review of the
kit), the six components (each has its own review), and how strong the tests are line by line
(`/mutate` measures that).

Line numbers are those of commit `07ffe328`. A task finds its lines again by the names the
entry gives.

## How to read an entry

- **Severity.** The launcher computes nothing, so the first level is not a wrong figure but a
  wrong statement the user acts on. 1 = the launcher or the released suite misleads the user or
  ships the wrong thing: a wrong program started, a tool or a prerequisite said present when it
  is not or the reverse, a wrong version shown, a licence text missing from the folder. 2 = a
  crash, a hang, or a file written or lost where it must not be. 3 = a wrong text or a wrong
  display. 4 = robustness and cleanliness; a check that cannot fail, or checks less than its
  name says, is here.
- **Reach.** Who meets it: *common* (an ordinary start, in either language), *some setups*
  (another screen, scaling or Windows account than the owner's), *slip* (a wrong click, a hand
  edit, a script switch), *rare*, `-` for an entry about the repository rather than a start.
- **Evidence.** *confirmed*: the lines were read and the case follows from them, or it was
  measured as said above. *to measure*: the code was read, but the case rests on something only
  a run or a screenshot can show; the task starts by showing it. *owner*: the code does what is
  described; whether that is a defect, or which of two texts is right, is the owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

An entry gives the direction of a fix, never the fix. A task takes one entry (or the group one
entry names), starts from the requirement and a failing test (the kit's rules "Requirements
first" and "Test before fix"); the window has no test project (ADR 0001), so a window entry
moves what it can into the core, where a test reaches it, and shows the rest in a screenshot. A
text an entry would change is shown to the owner first, original and proposal side by side, in
both languages (the rule "Two interface languages").

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | slip | confirmed | The window says 1.0.0 in a zip and a `release.json` that say 0.9.0 | rejected: the owner chose a rule, no code (ADR 0029) |
| CR-002 | 2 | rare | confirmed | A half surrogate escape in `release.json` or `settings.json` stops the launcher at start | fixed b8f5a26c |
| CR-003 | 3 | common | to measure | Each tool of a downloaded zip asks again before its first start, and "Don't run" is shown as a failure | fixed f548982b |
| CR-004 | 3 | common | owner | One Romanian word spelled two ways: `vinietarea` and `vignetarea` | fixed 395a788a |
| CR-005 | 3 | some setups | to measure | The main window is taller than the screen of a 1080p laptop at 125 % or 150 % | fixed 176c68e7 |
| CR-006 | 3 | rare | confirmed | The box of a failed start says the failure twice, the second time in English | open |
| CR-007 | 3 | rare | confirmed | About lists the launcher among the components when `release.json` has no `name` | open |
| CR-008 | 4 | slip | confirmed | Above `info` the log has no first line: no version, and a fallback is not announced | open |
| CR-009 | 4 | slip | to measure | A double click or a held Enter starts the same tool several times | open |
| CR-010 | 4 | common | to measure | The keyboard focus cannot be seen on the dark buttons; the language buttons have no name | open |
| CR-011 | 4 | rare | confirmed | The setting file's path is relative when Windows gives no local application data folder | open |
| CR-012 | 4 | rare | confirmed | A replace that fails leaves `settings.json.tmp` behind | open |
| CR-013 | 4 | - | confirmed | The copyright has a fallback in the code, with the year | open |
| CR-014 | 4 | - | confirmed | Five suite checks switch off together, without a word, when one component folder is missing | open |
| CR-015 | 4 | - | confirmed | Nothing checks the publish arguments of the six tools' entries | open |
| CR-016 | 4 | - | confirmed | Three sections of the notices file are bound to nothing | open |
| CR-017 | 4 | - | confirmed | `LICENSE`: the test passes on a cut text, nothing checks that the launcher publishes it, the note names the wrong publishers | open |
| CR-018 | 4 | - | confirmed | The requirements check drops a row it cannot parse and counts any method named `R###_` | open |
| CR-019 | 4 | - | owner | R-017 says every section holds its licence's whole text; lensfun's holds its address | open |
| CR-020 | 4 | - | confirmed | Core tests that cannot fail or check less than their name | open |
| CR-021 | 4 | - | confirmed | Log tests that cannot fail or check less than their name | open |
| CR-022 | 4 | - | confirmed | Notes and comments that describe other code | open |
| CR-023 | 4 | some setups | confirmed | The guard that keeps "Despre" on its screen is undone by WPF's own placement | open |

After the entries: the cleanup list, which holds what is not a defect.

## Severity 1: the user is misled, or the wrong thing ships

### CR-001 The window says 1.0.0 in a zip and a `release.json` that say 0.9.0

- Where: `Directory.Build.props:7` (`<Version>1.0.0</Version>`);
  `src/AstroDarkroom.Core/Version.cs:33` (`VersionInfo.Value`, read from the assembly);
  `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:76` and `AboutWindow.xaml.cs:68` (the banner);
  `scripts/release.ps1:63-65` (`-PublishOnly` leaves `Directory.Build.props` untouched, as
  documented).
- What happens: the suite's version comes from two places. The window shows the assembly's
  `<Version>`. The folder's name, the zip's name and the `version` of `release.json` come from
  the release script's `-Version`. A full release makes the two equal; `-PublishOnly` does not,
  and nothing in the launcher or in the tests compares them. The launcher reads the `version`
  of `release.json` and writes it to the log, and shows it nowhere.
- Case, present on 2026-11-13: `artifacts\AstroDarkroom-0.9.0-win-x64.zip` and
  `artifacts\0.9.0\`, made that day in the main checkout (untracked). `release.json` there has
  `"version": "0.9.0"` and the component `AstroDarkroom` at `"1.0.0"`; `AstroDarkroom.exe` there
  has the product version `1.0.0+f9f7178c...`. The main window and About say `Astro Darkroom
  1.0.0`. A user who received the zip named 0.9.0 reports a problem "in 1.0.0", and
  `artifacts\AstroDarkroom-1.0.0-win-x64.zip` of 2026-10-28 is another build that says the
  same.
- Should: the owner's call. One version for one zip: either a zip under a number other than
  `<Version>` is never handed out, or the launcher says so when the two differ, or the publish
  is given the number (that last one is in the kit's script, so the kit's).
- Test: none. `VersionTests` compares `VersionInfo.Value` with `Directory.Build.props` only. A
  core function that takes a `ReleaseInfo` and the assembly's version and says whether they
  agree can be tested.
- Evidence: confirmed (the file's product version and `release.json` were read). Whether the
  0.9.0 zip was given to anyone is not known here; if it was not, the entry is a 4.
- Status: rejected: the owner's call of 2026-11-14 (ADR 0029, 91f3beb0), the first of the three
  ways out and no code: a package published with `-PublishOnly` under another number than
  `<Version>` is a trial and is never handed out, and `docs/skills.md` says so at `/release` and
  `/wrap-up`. No release had been cut by then, so the 0.9.0 zip is such a trial.

## Severity 2: a crash, a hang, a file where it must not be

### CR-002 A half surrogate escape in `release.json` or `settings.json` stops the launcher at start

- Where: `src/AstroDarkroom.Core/ReleaseManifest.cs:118` (`Text`, `value.GetString()`), with the
  catch filter at `:104`; `src/AstroDarkroom.Core/Language/LanguageSetting.cs:66` (the call of
  `Text`), with the filter at `:77`; the callers `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:73`
  and `src/AstroDarkroom.Wpf/App.xaml.cs:71`.
- What happens: both `TryRead` methods catch `JsonException`, `IOException` and
  `UnauthorizedAccessException`. `JsonDocument.Parse` accepts a `\u` escape that is half of a
  surrogate pair; `JsonElement.GetString()` then throws `InvalidOperationException`, which
  neither filter names. The class summary of `ReleaseManifest` says "nothing here throws for a
  bad file".
- Case A: `release.json` holds `{"components":[{"name":"FlatCheck","version":"\ud83d"}]}`, the
  six characters `\ud83d` written in the file (a hand edit, a cut emoji escape). The exception
  leaves `TryRead`, the `MainWindow` constructor and `App.OnStartup`: a `FATAL` line, no window.
- Case B: `%LOCALAPPDATA%\AstroDarkroom\settings.json` holds `{"language":"\ud800"}`. The
  launcher stops at every start until the user finds that file and deletes it; nothing on the
  screen names it.
- Should: case A, a `WARN` line, null, the versions unknown (R-004); case B, a `WARN` line, no
  language, English (R-007).
- Test: `R004_AMissingOrInvalidReleaseJson_LeavesTheVersionsUnknown` and
  `R007_AMissingOrInvalidFile_GivesNoLanguage` have no such file; one line in each reproduces
  it.
- Evidence: confirmed. Measured on .NET 8 with the text of case A: `Parse` succeeds, the value's
  kind is `String`, `GetString()` throws `System.InvalidOperationException: Cannot read
  incomplete UTF-16 JSON text as string with missing low surrogate`, which is not a
  `JsonException`.
- Status: fixed b8f5a26c (`ReleaseManifest.Text` turns the exception into the `JsonException` both readers catch)

## Severity 3: a wrong text or a wrong display

### CR-003 Each tool of a downloaded zip asks again before its first start, and "Don't run" is shown as a failure

- Where: `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:172-177` (`UseShellExecute = true`) and
  `:185-190` (the `Win32Exception` box).
- What happens: the tools are started through the shell, which applies Windows' check of
  downloaded files to an executable that carries the mark of the web; a direct start
  (`UseShellExecute = false`) does not. The design note records `UseShellExecute` and gives no
  reason for it. The front page (`README.md:126` at the repository's root) and the project's
  `README.md:49` say SmartScreen asks "before the first start", once.
- Case: the zip is downloaded from the release page (ADR 0015) and extracted with Explorer,
  which marks every extracted file. The user accepts the prompt for `AstroDarkroom.exe`, then
  meets a prompt again at the first start of each of the six tools. A user who answers "Don't
  run" there gets the launcher's warning box, `... could not start: ... The operation was
  canceled by the user`, and an `ERROR` line in the log, for their own choice.
- Should: the owner's call. Either a direct start (the file is the launcher's neighbour, its
  full path already known), or the shell kept, a cancelled start (Windows error 1223) passed
  over in silence, and the READMEs saying the prompt comes once per program.
- Test: none can show the prompt. The task starts from a zip downloaded and extracted with
  Explorer on a machine that has not run the suite, and shows which prompts appear.
- Evidence: to measure. The lines were read; that the shell prompts for each marked executable
  and a direct start does not is how Windows is documented to behave, not something seen here.
- Status: fixed f548982b (the owner's call of 2026-11-14: a direct start, `Launcher.StartInfo`;
  measured first, ADR 0027: Windows' rule for a marked file started through the shell is "ask",
  and the same file started directly runs without a question; the prompt itself was not brought
  up on a screen)

### CR-004 One Romanian word spelled two ways: `vinietarea` and `vignetarea`

- Where: `src/AstroDarkroom.Core/AppCatalogue.cs:50` (`vinietarea fata de profilul
  obiectivului`); `src/AstroDarkroom.Core/Prerequisites.cs:77` (`compara vignetarea flat-ului
  tau`).
- What happens: in Romanian the row of FlatCheck in the main window says `vinietarea`, and the
  fourth paragraph of About says `vignetarea`. FlatCheck, which the row opens, writes
  `vignetar-` in its own window (the reviewer counted 78 uses, none of `vinietar-`).
- Should: one spelling in the launcher; which one is the owner's.
- Test: the row's wording is pinned by
  `R001_Catalogue_ListsTheSixAppsInWorkflowOrderWithTheirTexts`
  (`tests/AstroDarkroom.Tests/LauncherTests.cs:33`) and quoted in R-001
  (`docs/requirements.md:19`), so a change there is a requirement edit and a test edit, at the
  owner's word. Nothing pins the word in `Prerequisites`.
- Evidence: owner. That the two differ was read.
- Status: fixed 395a788a (the owner's call of 2026-11-14: `vignetarea`, ADR 0026)

### CR-005 The main window is taller than the screen of a 1080p laptop at 125 % or 150 %

- Where: `src/AstroDarkroom.Wpf/MainWindow.xaml:32-35` (`Width="680" SizeToContent="Height"`,
  `ResizeMode="CanMinimize"`, `CenterScreen`), `:43-48` (three `Auto` rows), `:74` (the rows'
  `ItemsControl`, no scroll viewer).
- What happens: the window is as tall as its content, cannot be resized and scrolls nothing.
  ADRs 0017 and 0018 measure it at 680 x 848 px at 100 %, and the runtime's warning (R-018)
  adds its lines when it shows. R-016 and ADR 0013 made About fit its screen; the main window
  was left as it was, and ADR 0017 then made it 105 px taller.
- Case: a 1920 x 1080 screen at 125 % has a work area about 825 units high and at 150 % about
  690 (ADR 0013's figures); a 1366 x 768 screen at 100 % about 728. At 125 % the bottom bar, with
  the language selector, the About button and the copyright, is partly under the taskbar. At
  150 % the bottom bar and the row of AstroPhotoAnalyzer are off the screen: the last tool is
  reached only with Tab and Enter, About only with F1, the language not at all by mouse.
- Should: the main window fits the work area of its screen as About does: no taller than it,
  the rows scrolling, the bottom bar in view. R-016 names About only, so this starts as a
  requirement.
- Test: `WindowFit` is the core's and is tested; the window itself is shown at a work area of
  825 and of 690 units, as ADR 0013 did for About.
- Evidence: to measure. That 848 is more than 825 is arithmetic on the ADRs' measurements; how
  WPF places and cuts the taller window was not seen. The same run can check the two-line
  descriptions and the one-line summary at 125 % and 150 %: ADRs 0017 and 0024 measured them at
  100 % only, and `Display` text formatting snaps the letters' widths to the pixels of each
  scale.
- Status: fixed 176c68e7 (measured first, ADR 0028: 848 DIP, 884 with the runtime's warning;
  R-020. The window was shown at work areas of 825 and 690 DIP by a measuring program, not on a
  real screen at 125 % or 150 %)

### CR-006 The box of a failed start says the failure twice, the second time in English

- Where: `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:188` (a tool) and `:263` (the runtime's
  link): the launcher's own sentence, then `ex.Message`; `:180-184`: the
  `InvalidOperationException` box, which shows `ex.Message` alone.
- What happens: the message of the `Win32Exception` that `Process.Start` throws on .NET 8 is
  not Windows' text alone: .NET puts its own English sentence before it, with the file and the
  folder. The launcher puts its sentence before that.
- Case: Romanian interface; `FlatCheck.Wpf.exe` is deleted after the window opened, or blocked.
  The box reads `FlatCheck.Wpf.exe nu a putut porni: An error occurred trying to start process
  'E:\suite\FlatCheck.Wpf.exe' with working directory 'E:\suite\'. The system cannot find the
  file specified.` A Romanian lead, an English sentence that names the file again, then
  Windows' text in Windows' language. In English the failure is still said twice. The second
  catch has a cousin: an `InvalidOperationException` thrown by `Process.Start` itself, not by
  the plan, is shown as .NET words it, with no sentence of the launcher's.
- Should: the launcher's sentence, then Windows' reason alone (the text of the error's number);
  the whole exception stays in the log. R-012 says the log holds "an exception's message as the
  user was shown it", so its sentence follows the change.
- Test: none; the window has no tests. The wording of the box can move to the core, as a
  function of the exe's name and Windows' text, where a test reaches both languages.
- Evidence: confirmed. Measured on .NET 8, a shell start of a file that is not there:
  `Win32Exception` 2 with the message `An error occurred trying to start process '<file>' with
  working directory '<folder>'. The system cannot find the file specified.`
- Status: open

### CR-007 About lists the launcher among the components when `release.json` has no `name`

- Where: `src/AstroDarkroom.Wpf/AboutWindow.xaml.cs:83-88` (the launcher's own entry is left
  out by `c.Name != release.Name`); `src/AstroDarkroom.Core/ReleaseManifest.cs:96` and `:100`
  (a missing `name` reads as `""`).
- What happens: the launcher's entry is recognised by its name being the release's name. When
  the file has no top-level `name`, that name is `""` and no component has it.
- Case: a `release.json` without the top-level `name`, its `components` whole (a hand edit).
  About lists `AstroDarkroom 1.0.0` as the first of the components, above DarksCheck. The
  reverse, a component without its `name`: a line that is a version alone, with no title.
- Should: the launcher never among its components; a component without a name left out or
  shown as unknown.
- Test: none; the list is built in the window. Built in the core, from a `ReleaseInfo` and the
  catalogue, it can be tested with those two files.
- Evidence: confirmed (read).
- Status: open

## Severity 4: robustness and cleanliness

### CR-008 Above `info` the log has no first line: no version, and a fallback is not announced

- Where: `src/AstroDarkroom.Core/Logging/LogSetup.cs:158` (the first line, at Info), `:199`
  (the last line, at Info), `:263` (the one rule, from the chosen level up); `README.md:111`
  ("its first line says so") and `:133` (`setx ASTRODARKROOM_LOG warn`, the README's example).
- What happens: the first line is the only place that holds the version, the executable's
  path, the log folder and the notice that the fallback folder was taken, and it goes through
  the same level rule as every other line.
- Case: `ASTRODARKROOM_LOG=warn`, the README's own example, and a suite folder under Program
  Files. The log goes to `%LOCALAPPDATA%\AstroDarkroom\log` and no line says so. A crash later
  leaves a `FATAL` line in a file that names no version and has no `starting` line before it.
- Should: R-011 ("the first line says so, naming both causes"), R-012 ("a first line per
  start") and the README set no limit to `info` and below. Either the first line is written at
  any level but `off`, or the requirement and the README say the limit.
- Test: none; `R011_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback` calls
  `StartupLine` directly. A configuration built at `Warn`, written through, shows it.
- Evidence: confirmed (read).
- Status: open

### CR-009 A double click or a held Enter starts the same tool several times

- Where: `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:160-191` (`OpenApp`);
  `src/AstroDarkroom.Wpf/App.xaml:117-125` (the button's template: a hover and a disabled
  state, none for a press).
- What happens: every click calls `Process.Start`; nothing holds a second one back, and the
  button does not show that it was pressed.
- Case: the user double-clicks Open, as on an icon: two windows of FlatCheck. Or clicks again
  because nothing seemed to happen in the second a tool takes to show. With the focus on an
  Open button, a held Enter clicks at the keyboard's repeat rate until a started tool takes the
  focus.
- Should: one start for one intended click; how (the row's button held back for a moment, a
  repeat ignored while the last start has no window) is the task's to propose.
- Test: none; the window has no tests.
- Evidence: to measure. That the launcher has no guard was read; how many tools a held Enter
  starts, and whether any tool refuses a second copy of itself, were not looked at.
- Status: open

### CR-010 The keyboard focus cannot be seen on the dark buttons; the language buttons have no name

- Where: `src/AstroDarkroom.Wpf/App.xaml:101-130` (the `Button` style) and `:135-164`
  (`LanguageChoice`): neither sets a `FocusVisualStyle`;
  `src/AstroDarkroom.Wpf/MainWindow.xaml:131-144` (each language button's content is a panel
  with a flag and a text).
- What happens: the styles replace the buttons' templates and leave the theme's focus mark, a
  thin dotted rectangle in the system's text colour, black by default, on a panel of `#2B2B2F`.
  A button whose content is a panel has no name for a screen reader.
- Case: the user moves through the six Open buttons with Tab and cannot see which one Enter
  will start. A screen reader says "radio button" twice for the two languages.
- Should: a focus mark in a colour of the palette; `AutomationProperties.Name` on the two
  language buttons.
- Test: none; a screenshot with Tab pressed, and Windows' Inspect for the names.
- Evidence: to measure (a reading of the styles; no screenshot).
- Status: open

### CR-011 The setting file's path is relative when Windows gives no local application data folder

- Where: `src/AstroDarkroom.Wpf/App.xaml.cs:69-70`; compare
  `src/AstroDarkroom.Core/Logging/LogSetup.cs:134-138`, which guards the same case; the comment
  at `src/AstroDarkroom.Wpf/NativeProbe.cs:50-51` knows it.
- What happens: `Path.Combine(GetFolderPath(LocalApplicationData), "AstroDarkroom",
  "settings.json")` with no check for an empty folder. `LogSetup.Apply` checks for exactly that
  and takes the temporary folder.
- Case: a Windows account for which `GetFolderPath` gives `""`. The path is
  `AstroDarkroom\settings.json`: the setting is looked for under the working folder, a change of
  language creates `<working folder>\AstroDarkroom\settings.json`, and a start from another
  working folder does not find it. The log's `settings=AstroDarkroom\settings.json` does not
  say where the file is.
- Should: the guard `LogSetup.Apply` has, or no setting file and a line that says so.
- Test: none (`App` has no tests). The path's choice can move to the core, as a function of
  the folder Windows gave.
- Evidence: confirmed for the code; such an account was not reproduced.
- Status: open

### CR-012 A replace that fails leaves `settings.json.tmp` behind

- Where: `src/AstroDarkroom.Core/Language/LanguageSetting.cs:103-105`, the catch at `:108`.
- What happens: the text goes to `settings.json.tmp`, then `File.Move` replaces the file. When
  the move throws, the catch logs and returns; the temporary file stays.
- Case: `settings.json` is read-only. The user clicks `Romana`: the temporary file is written,
  the move throws, the `WARN` is logged, and `settings.json.tmp` stays beside the setting,
  holding the language that was not remembered. Each later switch writes it again.
- Should: the temporary file removed when the replace fails, so "what it cannot write is
  forgotten" leaves no file.
- Test: `R007_AWriteThatCannotHappen_IsIgnored` fails earlier, at the folder, and never reaches
  the move. The same test with a read-only `settings.json`, asserting no `.tmp` is left.
- Evidence: confirmed. Measured on .NET 8: `File.Move(temp, path, overwrite: true)` over a
  read-only file throws `UnauthorizedAccessException` and the temporary file is still there.
- Status: open

### CR-013 The copyright has a fallback in the code, with the year

- Where: `src/AstroDarkroom.Core/Version.cs:51` (`"Copyright (C) 2026 Tudor Carean"`) against
  its own summary at `:23-25` ("nothing is hard-coded here");
  `tests/AstroDarkroom.Tests/VersionTests.cs:47-48`.
- What happens: when the assembly carries no copyright attribute, `VersionInfo.Copyright` is
  the literal. In 2026 the literal equals `Directory.Build.props`, so
  `TheCopyrightComesFromDirectoryBuildProps` stays green with the attribute gone. The version
  has the opposite fallback, `0.0.0`, which its test does catch.
- Case: a project setting stops the attribute (`GenerateAssemblyCopyrightAttribute` false).
  Nothing notices; after the year is rolled in `Directory.Build.props`, both windows go on
  showing 2026.
- Should: no year in the code; a fallback the test can tell from the real notice.
- Test: the existing one, once the fallback differs from the notice. Its second assertion
  (`:48`) repeats the year as a literal and has to be edited at each roll of the year; it is
  the owner's whether it stays (the kit's rule "Version and year are the owner's").
- Evidence: confirmed (read).
- Status: open

### CR-014 Five suite checks switch off together, without a word, when one component folder is missing

- Where: `tests/AstroDarkroom.Tests/SuiteTests.cs:64` (`SiblingsPresent`: all of the folders),
  the early returns at `:85-88`, `:114-117`, `:218-221`, `:311-314`, the `continue` at
  `:190-193`, and the class comment at `:33-34`.
- What happens: four tests return green at once when any one sibling folder is missing, and
  R-010 keeps only "the copy exists". The comment says they return "with the reason in the
  output"; the class writes nothing.
- Case: a component's folder is renamed (`WCS_Copy` to `CopyWcs`) while `kit.json` and the
  catalogue keep the old name. `EveryCatalogueApp_IsAComponentOfTheRelease_AndTheReverse`
  passes, both sides saying `WCS_Copy`. The exe names, the consoles, .NET 8, the shared
  versions, R-017 and R-018 are not checked for any of the six, and no icon's bytes are
  compared. The gate is green until the release script fails on the missing project.
- Should: the checks skipped only when no sibling is there (this folder cloned alone, which is
  what the comment means), a failure when some are missing, and the reason written.
- Test: the checks themselves; a function that takes the folders found and says skip, run or
  fail can be tested with three lists.
- Evidence: confirmed (read).
- Status: open

### CR-015 Nothing checks the publish arguments of the six tools' entries

- Where: `kit.json:18-23`; `tests/AstroDarkroom.Tests/SuiteTests.cs:50-58` (reads `project` and
  `output` only); `tests/AstroDarkroom.Tests/ThirdPartyTests.cs:298-301` (checks `args` for the
  launcher's entry alone).
- Case: an entry is added or edited with the arguments of the tool's own release; the reviewer
  read `-p:PublishSingleFile=true` in the window entries of DarksCheck's, LightsCheck's and
  CopyWcs's own `kit.json`. Copied here, the gate stays green, and the suite folder would hold
  one large exe with its own runtime inside, against ADR 0002. An entry without
  `--self-contained`, or with another `-r`, passes too.
- Should: every entry's `args` equal to the launcher's, `-r win-x64 --self-contained`, checked
  in `SuiteTests`.
- Test: that assertion.
- Evidence: confirmed that the check is absent; what the merge does with such a publish was not
  run.
- Status: open

### CR-016 Three sections of the notices file are bound to nothing

- Where: `tests/AstroDarkroom.Tests/SuiteTests.cs:253-257` (a section without a `Packages:`
  line is skipped); `THIRD-PARTY-NOTICES.md`, the sections `The lensfun database` (`Programs:
  FlatCheck`), `Lynds' Catalogue of Dark Nebulae` (`Programs: AstroPhotoAnalyzer`) and `.NET 8
  and WPF` (`Programs:` the seven, by hand), from line 1250, and its opening sentence at `:3`
  ("the launcher and the six tools").
- Cases: a seventh tool joins the suite; R-017 forces its title onto the `Programs:` lines of
  its packages, and the `.NET 8 and WPF` line and the opening sentence keep six. FlatCheck stops
  publishing its `lensfun` folder, or another window starts to; the section stays as it is.
  AstroPhotoAnalyzer drops the catalogue's text; the Lynds section stays.
- Should: the `.NET` section's `Programs:` equal to the launcher and the catalogue's titles;
  lensfun's and Lynds' following the project items that ship them, which `SuiteTests` already
  opens the project files for.
- Test: those assertions. A data file of others that is not a package and has no section yet
  cannot be seen by any check; that stays a step of adding a tool (`CLAUDE.md`, the rule "The
  catalogue and kit.json agree").
- Evidence: confirmed (read).
- Status: open

### CR-017 `LICENSE`: the test passes on a cut text, nothing checks that the launcher publishes it, the note names the wrong publishers

- Where: `tests/AstroDarkroom.Tests/LicenseHeaderTests.cs:138-145`;
  `src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj:36` (the item) and the comment above it;
  `tests/AstroDarkroom.Tests/ThirdPartyTests.cs:248-263` (the publish list is asked for the
  notices file and the runtime's two texts); `docs/design/launcher.md:230-233`;
  `src/AstroDarkroom.Core/LicenseInfo.cs:36` (About says the text is "in the LICENSE file beside
  the program").
- What happens, three things about one file:
  - `R005_TheRepoShipsTheFullGplText` asserts three phrases, all in the first lines of the
    text. A `LICENSE` cut anywhere after the terms' heading passes, and is what the folder
    would ship. The LGPL and Apache texts get more in `ThirdPartyTests`, which asserts their
    last terms.
  - The item of `LICENSE` in the window's project can be removed and no test turns red: the
    publish-list test does not ask for it.
  - The note and the project file's comment say every component publishes `LICENSE` with the
    same bytes. Two do, ImageVisualCompare and CopyWcs; the window projects of DarksCheck,
    FlatCheck, LightsCheck and AstroPhotoAnalyzer have no such item. Without the launcher's
    item the folder's `LICENSE` would rest on those two.
- Should: the test asserting the text's end as well (`END OF TERMS AND CONDITIONS`); the
  publish-list test asking for `LICENSE`; the note and the comment naming who publishes it.
- Test: as said. `LICENSE` is whole today, and `artifacts\0.9.0\LICENSE` is in the folder.
- Evidence: confirmed (read; the components' project files were searched for the item).
- Status: open

### CR-018 The requirements check drops a row it cannot parse and counts any method named `R###_`

- Where: `tests/AstroDarkroom.Tests/RepoInvariantTests.cs:136` (the row's pattern: the status
  is one of four lower-case words), `:105-120` (`ReadRequirements`), `:123-131`
  (`TestMethodIds`).
- Cases: a row typed `| R-020 | Implemented | ...`, or `done`, or `R-20`, is no row to the
  pattern; with no test it is green, though the kit's rule "Requirements first" says every
  implemented requirement has one. Two rows with one ID: the last one's status wins, silently.
  A public method named `R020_...` with no `[Fact]`, or with a skipped one, counts as the
  requirement's test.
- Should: a table line that starts `| R-` and does not match fails the check; a second row
  with a known ID fails; only a method that carries a `Fact` or a `Theory` that runs counts.
- Test: the check, given a small table as text. The table is clean today: R-001 to R-019 are
  all `implemented`, each with a test.
- Evidence: confirmed (read).
- Status: open

### CR-019 R-017 says every section holds its licence's whole text; lensfun's holds its address

- Where: `docs/requirements.md:35` (R-017: "Each section gives the copyright notices and the
  whole text of its licence"); `THIRD-PARTY-NOTICES.md`, the section `The lensfun database`;
  `tests/AstroDarkroom.Tests/ThirdPartyTests.cs:165-166` (the row of that section asks for the
  licence's name, the copyright line and the address).
- What happens: the lensfun section gives the licence's name, its address and the folder's own
  note, not the text of CC BY-SA 3.0, and the test follows the file. The reviewer notes that
  the licence allows its address in place of its text. The `.NET` section points at two files
  and Lynds' has no licence; both sections say so themselves.
- Should: the owner's call: the requirement and ADR 0014 say what the file does (the whole text,
  or its address where the licence allows that), or the file gets the text.
- Test: follows the choice.
- Evidence: owner. That the sentence and the file differ was read; what CC BY-SA 3.0 allows was
  not checked here.
- Status: open

### CR-020 Core tests that cannot fail or check less than their name

All in `tests/AstroDarkroom.Tests/`.

- `VersionTests.cs:52-62`, `R019_TheBottomBarsCopyright_..._TheSameInBothLanguages`:
  `VersionInfo.Copyright` is set once and never reads the language, so `:61` compares a value
  with itself; `:59` repeats the test above it. Nothing of the bottom bar is checked (the
  window is untested, ADR 0001).
- `LanguageTests.cs:42-45`, `R006_StartupDefault_IsEnglish`: compares a constant with a
  literal. That `Ui.Default` starts as `StartupDefault` is not checked: the tests' module
  initializer has already set it to Romanian.
- `LanguageTests.cs:136-143`, `R007_AWriteThatCannotHappen_IsIgnored`: its one assertion, that
  the blocking file exists, is always true. What it checks is that nothing throws, and only for
  a folder that cannot be created; the write and the move that fail are not reached (CR-012).
- `VersionTests.cs:64-74`, `NoProjectOverridesTheSharedVersionOrCopyright`: looks for the exact
  texts `<Version>` and `<Copyright>`. `<Version Condition=...>`, `<VersionPrefix>` or
  `<InformationalVersion>` in a project file pass.
- `VersionTests.cs:77-80`, `TheBannerIsTheNameAndTheVersion`: says the implementation again;
  it pins the product's name alone.
- R-001's "in at most two lines of its row" has no test: it was measured once (ADR 0017), and
  a longer description passes the gate. CR-005's run measures it again.
- `ThirdPartyTests.cs:237`: R-017's "and fails without" is checked by the presence of an
  `Error` element in the target; its condition is never exercised.
- Not covered at all: a `components` value that is not an array, a file that is locked (the
  `IOException` arm of both `TryRead`s), a scope of `Ui.Use` inside another, though R-006 says
  "the innermost".

- Evidence: confirmed (read).
- Status: open

### CR-021 Log tests that cannot fail or check less than their name

In `tests/AstroDarkroom.Tests/LoggingTests.cs` unless said.

- `:516-547`, `NoLogLineGoesThroughTheTranslation`: its comment and the design note say every
  logger is declared one way, so the scan sees every call. The declaration's pattern (`:567`)
  accepts any field name, while the call's pattern (`:558`) sees only a receiver that ends in
  `log` or `Log`. A logger field named `Diag` with `Diag.Info(T("a", "b"))` passes both. The
  text handed to `LogSetup.Fatal(` (`src/AstroDarkroom.Wpf/App.xaml.cs:50`) is not scanned
  either.
- `:111-129`, `R011_TheLogRollsAt1MiBAndKeepsAtMost5Files`: the 1 MiB of R-011 is pinned
  nowhere. The test writes eight times the constant and bounds each file by the constant, so
  it holds for another limit as well; `:243` compares the constant with itself. The five files
  are pinned, by their names. By the reviewer's arithmetic a limit of 512 KiB leaves every test
  green; that needs a run.
- `LogCapture.cs:28-32` and `:57-62`: the comment says a log line that throws fails the test
  that reaches it. `Install` leaves NLog's `ThrowExceptions` off, so an exception inside the
  rendering or the target is swallowed; only one raised while the caller composes the message
  fails a test, capture or no capture. (How NLog 6.2.2 treats the flag is from the reviewer's
  memory of it.)
- `:428-442`, `R013_TheLevelComesFromTheEnvironmentVariable`: tests `LevelFrom` on strings.
  The read of the variable (`src/AstroDarkroom.Core/Logging/LogSetup.cs:141`) runs in no test,
  which follows from `Apply` running in none; the name says more.
- UTF-8 (R-011): `:62` checks that the file has no byte order mark and reads a line of ASCII;
  `:238-251` does not assert the target's `Encoding`. Latin-1 at `LogSetup.cs:254` would pass
  every test.
- Invariant numbers (R-012): no test sets a culture with a decimal comma; `:420-424` and
  `:220-235` catch a dropped `InvariantCulture` only on a machine whose own format has one.
- `:474`, `R014_TwoProcessesWritingAtFullSpeedKeepEveryLine`: two factories on two threads of
  one process. The design note says so and why; the name says "processes".

- Evidence: confirmed (read), but for the two points marked as the reviewer's.
- Status: open

### CR-022 Notes and comments that describe other code

- `src/AstroDarkroom.Wpf/AboutWindow.xaml.cs:29`: `ComponentLine.Name` is documented as the
  component's folder name; it holds the catalogue's title (`TitleOf`, `:87`).
- `src/AstroDarkroom.Core/SuiteApp.cs:26` and `src/AstroDarkroom.Wpf/AppRow.cs:38`: the
  description is "one line"; it has been up to two since ADR 0017.
- `src/AstroDarkroom.Core/Version.cs:58`: the `<returns>` promises the form `1.0.1`; the
  fallback to the assembly's version at `:66` gives `1.0.1.0`, as the comment at `:63-64` says.
- `src/AstroDarkroom.Core/SuiteFolder.cs:23` and `Launcher.cs:24`: "the full path", but the
  value is the base directory "as given" combined with the exe's name; it is full only because
  the window passes a full base directory.
- `src/AstroDarkroom.Wpf/Loggers.cs:29`: "the main window, the about window and the title
  bar"; `ScreenArea` logs under `Window` too.
- `tests/AstroDarkroom.Tests/LogFiles.cs:28-30`: the summary lists "an elapsed time as the log
  writes it"; the file has no such member (left from LightsCheck's copy).
- `docs/design/logging.md:17`: `LogSetup.Apply` is "the first call of `App`'s constructor";
  the first statement reads the command line (`src/AstroDarkroom.Wpf/App.xaml.cs:37`).
- `docs/design/launcher.md:139-141`: the row's strings are "picked at the row's build";
  `OpenLabel` is, `VersionHint` and `ButtonHint` call `T` at each read
  (`src/AstroDarkroom.Wpf/AppRow.cs:47-56`). Harmless, since the rows are rebuilt at every
  switch.
- `tests/testdata/README.md:9` and `.gitattributes:2` cite "ADR 0014" for the line-ending rule;
  here ADR 0014 is the licence texts, and the one meant is the kit's.
- `src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj:13`: `ApplicationHighDpiMode` names
  `PerMonitorV2`, which Windows Forms reads and WPF does not; the launcher is system DPI aware,
  as `docs/design/launcher.md:201` says. Put to the owner once and not taken
  (`docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md:217-220`); the line still tells a
  reader of the project file something that is not so.
- The class comment of `SuiteTests` and the `LICENSE` sentences of `launcher.md` are in CR-014
  and CR-017.

- Evidence: confirmed (read).
- Status: open

### CR-023 The guard that keeps "Despre" on its screen is undone by WPF's own placement

Added on 2026-11-14 by the task that fixed CR-005, which measured it on the way.

- Where: `src/AstroDarkroom.Wpf/AboutWindow.xaml.cs`, the constructor (`Loaded += (_, _) =>
  FitInto(workArea)`) and `FitInto` (`Top = WindowFit.Top(...)`); R-016 and
  `docs/design/launcher.md`, which call the move a guard that holds the window's place.
- What happens: for a window sized to its content WPF raises `Loaded` during the first layout,
  before `SourceInitialized`, and then sets the window's place again from its own computation.
  A `Top` set in `Loaded` is replaced. So the guard never acts: "Despre" is where WPF puts a
  window centred on its owner, which is inside the owner screen's work area, as ADR 0013
  measured.
- Case: none seen. The requirement holds today by WPF's placement alone, which is what the
  guard was added not to rest on (ADR 0013).
- Should: the move made when the window has its handle (`SourceInitialized`), as the main
  window's fit is (ADR 0028), where a `Top` that is set holds; or the guard removed and the
  requirement saying it rests on WPF.
- Test: none; the window has no tests. Measured as ADR 0028 does.
- Evidence: confirmed by measurement on 2026-11-14, on .NET 8, on a plain window of the same
  kind (sized to its content, centred on an owner off the screen): `Loaded` came first, a `Top`
  set there was back at WPF's value by `SourceInitialized`, and one set in `SourceInitialized`
  held. "Despre" itself was not shown: WPF would have placed it on the owner's screen.
- Status: open

## Cleanup

Not defects; taken along when a task is in the file, or never.

- `src/AstroDarkroom.Wpf/AboutWindow.xaml:83-87` and `:88-92`: the `Run`s of the third-party
  lines are on separate lines, and XAML keeps the break between two inlines as a space, so
  with `Text=" - "` the lines likely show two spaces on each side of the dash.
  `MainWindow.xaml:66` knows the effect ("The runs touch"). Needs a screenshot.
- `src/AstroDarkroom.Wpf/App.xaml:87`: the tooltip's `FontSize` 12 likely never applies: a
  tooltip's text becomes a `TextBlock`, which takes the application's implicit `TextBlock`
  style, size 13. The `Button` style's `Foreground`, `FontFamily` and `FontSize` (`:102`,
  `:105-106`) are shadowed the same way, with equal values. Needs a screenshot.
- `src/AstroDarkroom.Wpf/MainWindow.xaml:34`: `MinWidth="520"` does nothing on a window of
  fixed width that cannot be resized.
- `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:98`: `RuntimeLinkText.Text` is a constant set at
  every relabel; it belongs in the constructor beside `NavigateUri` (`:74`).
- `src/AstroDarkroom.Wpf/MainWindow.xaml.cs:103` and `:105`: the rows and the presence line
  each ask `Locate` for the six apps, so the log's line is not computed from what the rows
  show.
- `src/AstroDarkroom.Wpf/App.xaml.cs:77-84` and `MainWindow.xaml.cs:232-239`: the same block,
  Info when nothing is missing, else Warn, over `LogText.VcRuntime`.
  `src/AstroDarkroom.Wpf/ScreenArea.cs:78` and `:91` repeat one message.
- After the "is missing" box (`MainWindow.xaml.cs:180-184`) the row keeps its enabled button,
  and a tool copied into the folder later stays disabled until a switch of language or a
  restart. The note says the folder is not watched; `Activated` already asks for the runtime
  again (`:68`) and could rebuild the rows.
- The product's name is written as a literal in `MainWindow.xaml:32`, `AboutWindow.xaml:57`
  and `MainWindow.xaml.cs:264`, beside `VersionInfo.Name`. The rule allows it.
- `src/AstroDarkroom.Core/ReleaseManifest.cs:117`: `Text`, the JSON helper both readers share,
  lives in the manifest's class. `src/AstroDarkroom.Core/Language/LanguageSetting.cs:104`
  serialises a dictionary by reflection for a file of one fixed key; fine while the publish is
  not trimmed.
- `src/AstroDarkroom.Core/Version.cs` holds the type `VersionInfo`.
- `src/AstroDarkroom.Wpf/NativeProbe.cs:81-84`: `TryMakeCurrent` swallows two exceptions with
  no log line, and it is not among the exemptions R-012 lists. No practical case: the
  executable's own folder can be made current.
- `src/AstroDarkroom.Core/Logging/LogSetup.cs:371-381`: an unknown word in `ASTRODARKROOM_LOG`
  means Debug, as R-013 says; the first line shows `level=Debug` and not that a value was
  ignored.
- `src/AstroDarkroom.Wpf/AboutWindow.xaml.cs:127`: a hand-written `"yes"`/`"no"`, because
  `LogText.Yes` is internal to the core.
- The suite zip carries the XML documentation files (`AstroDarkroom.xml`,
  `AstroDarkroom.Core.xml`, and those of DarksCheck, LightsCheck, CopyWcs and
  ImageVisualCompare; seen in `artifacts\0.9.0\`): `Directory.Build.props:41` generates them
  and nothing keeps them out of a publish. FlatCheck and AstroPhotoAnalyzer set
  `PublishDocumentationFiles` to false.
- `src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj:29` embeds every `.ico` of `Icon/`; an icon
  left after a tool leaves the catalogue would stay in the exe, and nothing compares the folder
  with the catalogue.
- `tests/AstroDarkroom.Tests/SuiteTests.cs:136` accepts any framework whose name starts with
  `net8.0`; `:130` with `:385-388`: a component whose code folders hold no lock file adds no
  problem, and `:173` is satisfied by the others.
- `tests/AstroDarkroom.Tests/RepoInvariantTests.cs:67-70` lists `fixtures` without its
  subfolders and reads the copy in the output folder, where a deleted fixture stays until a
  clean build. There is no fixture today.
- `tests/AstroDarkroom.Tests/TestData.cs:25-37`: `Fixture` and `Large` are used by no test;
  the test project copies `docs\validation.json` to its output, where no test reads it.
- `tests/AstroDarkroom.Tests/ThirdPartyTests.cs:55-66` and `:73-94` are covered again by the
  `NLog` row of `LicenceTexts` (`:136-137`).
- `Directory.Packages.props:23`: one "why" for the three test packages.
- For FlatCheck's list: the shipped `lensfun\LENSFUN-VERSION.txt` says "see LICENSE in this
  folder / THIRD-PARTY-NOTICES.md"; the folder has `LICENSE.txt` and the suite
  `THIRD-PARTY-NOTICES.txt`.

## What was looked for and found right

So a later reader does not look again without a reason: the `--lang=` argument, in the
one-token form all six tools accept; `release.json` as `scripts/release.ps1` writes it against
what the reader expects; every `T(en, ro)` pair of the core saying the same thing in both
languages, CR-004 apart, and no letter outside ASCII under `src/`; every text of the main window
assigned again in `Relabel`, and About and the message boxes modal, so none stays in the old
language; every binding naming a property that exists; the P/Invoke signatures and structures
of `ScreenArea` and `DarkTitleBar`; `ScreenArea`'s arithmetic for the system-aware process the
launcher is; the `Process` objects disposed; the log folder's probe and its fallback; the
parsing of `ASTRODARKROOM_LOG`; the unhandled-exception hooks, none of which swallows an
exception or shows a box; the start-up order in `App`; the bytes of the icon copies; the
packages of the notices file against the seven lock files, both ways; the catalogue against
`kit.json`.
