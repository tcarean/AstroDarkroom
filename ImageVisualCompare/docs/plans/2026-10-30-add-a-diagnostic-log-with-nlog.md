# Add a diagnostic log with NLog

Plan for the task branch `claude/Log-ImageVisualCompare-with-NLog`, worktree
`.claude/worktrees/Log-ImageVisualCompare-with-NLog` (made with
`git worktree add .claude/worktrees/Log-ImageVisualCompare-with-NLog -b claude/Log-ImageVisualCompare-with-NLog main`
from `E:\Photografie\Code\ImageVisualCompare`; never build or test in the main checkout).
Written for an implementer who has this file, `CLAUDE.md`, the repository and FlatCheck's
checkout beside it (`E:\Photografie\Code\FlatCheck`), and nothing else from the planning
session. Approved by the owner on 2026-10-30, every decision below as recommended; this file
is the first commit of the branch. The plan as approved named the branch
`claude/Add-a-diagnostic-log-with-NLog`; DarksCheck's and LightsCheck's log plans of the same
day name that branch too, and a branch name is the whole repository's, so this one names the
project.

## Context

When a user reports that the tool failed, showed the wrong thing or ran out of memory, all we
have is what was on the screen: the `Cannot compare` dialog shows an exception's message
without its type, its stack or its inner exception (`MainWindow.xaml.cs:189`, `:272`;
`ImagePairLoader.cs:194`), an unhandled exception takes the window down with Windows' own
dialog and no trace, and nothing records which files were opened, what WIC or the FITS reader
made of them, whether the pair was padded, which display mode was asked for, how long each
decode took or what the process held in memory. Nothing in the three projects logs today: no
`Trace`, no `Debug.WriteLine`, no `ILogger`.

The owner's ask (2026-10-30): a log, with NLog, text format, in `log/` beside the executable,
file names starting with the executable's name, at most 5 files per executable, rolling, at most
1 MB each, done the way FlatCheck did it (FlatCheck's ADR 0070, ADR 0072, R-109 to R-112,
`docs/design/logging.md`, `FlatCheck.Core/Logging/`). What is logged is the implementer's call,
with one criterion: **a bug report with the `log` folder attached can be diagnosed from it.**

FlatCheck already measured the hard parts (its ADR 0070, 2026-10-27): NLog's plain file target
loses lines when two processes write one file (33 to 72 of 4000), `NLog.Targets.AtomicFile`
loses none at 0.09 ms a line, NLog 6.2.2's `MaxArchiveFiles` counts the file being written,
and a logger created before the configuration is set makes NLog load a stray `NLog.config`
and can kill the process. This plan takes those measurements as given and does not repeat them;
the ADR cites FlatCheck's.

## Decisions put to the owner

Each with a recommendation (the kit's rule "Two readings"). Approving the plan as written
settles every row as recommended.

| # | Decision | Recommendation and why |
| --- | --- | --- |
| D1 | Where NLog lives | **The application only.** `ImageVisualCompare.Wpf` references `NLog` and `NLog.Targets.AtomicFile`; `ImageVisualCompare.Core` stays with no package at all. Core is records, the FITS reader and the renderer: every fact it computes comes back to a loader in `Imaging/`, which logs it. So the rule "Core has no UI" and the mutation runs stay as they are, and the CLAUDE.md rule changes by one clause. FlatCheck put NLog in its core because two applications share one `LogSetup`; this project has one. |
| D2 | The file target | **`AtomicFileTarget` from the start**, on FlatCheck's measurement (two windows of this tool on one machine is an ordinary case: the AstroDarkroom launcher can start it twice). The in-process two-writer test is ported, so the claim is re-proven here. |
| D3 | Level and its switch | **`Debug` by default; the environment variable `IMAGEVISUALCOMPARE_LOG`** takes `off`, `fatal`, `error`, `warn`, `info`, `debug`, `trace`, any case; anything else is `Debug`. No command-line option: `--lang` is the only option today and the arguments are the pair (R-002); a `--log` would have to be taken out of the paths too. |
| D4 | The fallback folder | **`%LOCALAPPDATA%\ImageVisualCompare\log`** when `log` beside the executable cannot be written (Program Files, a read-only drive); the first line says so. |
| D5 | The third-party notice | **`THIRD-PARTY-NOTICES.md` at the project root, shipped beside the executable like `LICENSE`** (a `None` link in the csproj, `PreserveNewest`), with NLog's BSD-3-Clause text copied from FlatCheck's. About (R-072) stays its nine lines: the licence's binary clause asks for the notice "in the documentation and/or other materials provided with the distribution", which the file beside the executable is. The README's "Licence" section names it. |
| D6 | What the comparison surface logs | **The four zoom commands at Debug** (`ZoomIn`, `ZoomOut`, `ZoomToFit`, `ZoomToActualSize`, reached from the toolbar and the keyboard alike) with the scale they leave, and **one line when a pair is set or replaced** (canvas, viewport in physical pixels, DPI scale, the scale chosen). Nothing per wheel tick, drag, divider move or scroll: those fire many times a second and a log of them says nothing a screenshot does not. |
| D7 | Crashes | **Logged at Fatal, flushed, then as today.** `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and `Application.DispatcherUnhandledException` write the exception once (the dispatcher's hook and the process's see the same one); the process still dies as WPF lets it. No "friendly crash dialog": that is a feature of its own. |
| D8 | Configuration | **In code, through `LogSetup.Apply` first and `LogSetup.Close` last, in `App`; no `NLog.config`** (nothing to ship, nothing the user can break, nothing to go stale). Both calls in a `try` that swallows everything: the log never stops the tool (FlatCheck's ADR 0072). |
| D9 | Version | **NLog 6.2.2 and NLog.Targets.AtomicFile 6.2.2**, FlatCheck's, so the suite carries one NLog; the two versions move together. |
| D10 | The FITS header at Trace | **The loader reads the cards itself**: the header's length is `FitsImageLayout.DataOffset`, a card is 80 bytes, so `FitsImageLoader` can read and log every card at Trace without Core exposing anything new (D1). |

## What the log holds (the design)

### The files

`ImageVisualCompare.Wpf.log` in `log` beside `ImageVisualCompare.Wpf.exe`; past 1 MiB
(`1024 * 1024`) NLog starts `ImageVisualCompare.Wpf_01.log`, `_02`, ... and deletes the oldest
past 5 files (`MaxArchiveFiles = 5`, which counts the file being written in 6.2.2). The next
process appends to the newest file. UTF-8 without BOM. One `AtomicFileTarget`, `ConcurrentWrites`,
`KeepFileOpen`, `AutoFlush`, `CreateDirs`; one rule from the level up for `*`; no console target.

### The line

`${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}${onexception:inner=${newline}${exception:format=ToString}}`,
`DefaultCultureInfo = InvariantCulture`. Messages are plain strings composed from `LogText`
(invariant numbers, `none` for a missing value, `12.3 MiB`, `1141 ms`, `12.9 s`), never message
templates: a `{` in a path is harmless. Every line in English; `UiText` never composes a log
line. The one Romanian thing a log may hold is a message the user was shown, quoted as shown
(`cannot compare: <message>`), the same rule as FlatCheck's R-110.

### Loggers (`Logging/Loggers.cs`, string constants)

`Process` (LogSetup: first line, last line, crashes), `App`, `Window` (MainWindow,
MessageDialog, FileDialogService), `Surface` (CompareView), `Load` (ImagePairLoader,
RoutingImageLoader), `Wic`, `Fits`, `Chrome` (DarkTitleBar). Every logger is a
`private static readonly Logger Log = LogManager.GetLogger(Loggers.X);` with `using NLog`, except
`LogSetup`'s and `App`'s, which are properties fetched on use (`static Logger Log =>
LogManager.GetLogger(...)`) because those two run before the configuration is set (FlatCheck's
ADR 0072, measured). An invariant test checks both forms.

### What is logged where

| Seam | Level | Lines |
| --- | --- | --- |
| `LogSetup` (`Process`) | Info, Error, Fatal | `ImageVisualCompare.Wpf <version> starting; exe=…; args=["…", "…"]; cwd=…; log=… (fallback: …); level=Debug; os=…; cpus=…; memory=… GiB; runtime=…; culture=…; tz=…; pid=…` (`StartupLine`, ported); `exit <code> after <s>`; `unhandled exception, the process ends` at Fatal; `a task failed and nobody awaited it` at Error |
| `App` | Info, Warn, Fatal | `language=en (default)` / `language=ro (--lang)`; `refused: <UnknownLanguage message>; the interface is English` at Warn (the message as the dialog will show it); `command line names the pair A=<path> B=<path>` or `command line names no pair: the dialogs ask`; the dispatcher's unhandled exception at Fatal through `LogSetup.Fatal` |
| `MainWindow` (`Window`) | Info, Warn, Debug | `open asked (button|Ctrl+O|startup)`; `open cancelled; closing` / `open cancelled; the pair on screen stays`; `padding asked: A <W>x<H>, B <W>x<H>, canvas <W>x<H>` and `padding answered yes|no`; `shown A=<file> <facts> B=<file> <facts> canvas=<W>x<H> padded=yes|no mode=Linear|Histogram|FalseColours keepView=yes|no` where `<facts>` is `LogText.Image(ImageInfo)`: `<W>x<H> bits=16 channels=Rgb float=no range=none asStored=no` (never `FormatDescription`, which is translated); `cannot compare: <message as shown>` at Warn (the Error with the stack is the loader's); `mode switch asked <mode>: refused (opening|no pair|not allowed)` at Debug, `mode switch <from> -> <to>` at Info; `About opened`; `unknown language dialog shown`; `memory after show: managed <MiB>, process <MiB>` at Debug, and `released the outgoing pair: process <before> -> <after>` at Debug (read `MemoryHousekeeping.Snapshot()` before and after `ReleaseImageBuffers`) |
| `MessageDialog` (`Window`) | Info | `dialog "<title>" shown`; `dialog "<title>" answered yes|no` |
| `FileDialogService` (`Window`) | Info | `dialog A: chose <path>` / `dialog A: cancelled`; the same for B |
| `CompareView` (`Surface`) | Debug | `pair set: canvas <W>x<H>, viewport <w>x<h> px, dpi <scale>, scale <s>` on `SetPair` and `pair replaced: …` on `ReplacePair`; `zoom in -> <scale>`, `zoom out -> <scale>`, `zoom fit -> <scale>`, `zoom 100% -> <scale>` (D6) |
| `ImagePairLoader` (`Load`) | Info, Debug, Error | through `RunClock` (ported): `open start A=<path> B=<path> mode=<mode>`; `probed A: <path> (<MiB>) <facts> in <ms>` and the same for B; `sizes differ: A <W>x<H>, B <W>x<H>, canvas <W>x<H>: asking` / `: refused, no one to ask` / `: declined`; `mode falls back to Linear: not both shown as stored` at Debug; `decoded A in <ms>`, `decoded B in <ms>`; `open done mode=<mode> padded=yes|no in <ms> (probe A <ms>, probe B <ms>, decode A <ms>, decode B <ms>)`; a caught failure: `open failed on <path>; the user is told: <message>` at Error **with the exception** (`Log.Error(ex, …)`, so the type, the stack and the inner `COMException` are in the file); a cancellation: `open cancelled on <path>` at Info, no stack; a run that throws past the catch leaves RunClock's `open ended without an outcome after <ms> (<stages>)` |
| `RoutingImageLoader` (`Load`) | Debug | `<file> starts as a FITS: fits loader` / `<file>: wic loader` |
| `WicImageLoader` (`Wic`) | Debug | at probe and at load: `<file>: decoder=<TiffBitmapDecoder> format=<Rgb48> <W>x<H>`; `float samples by tags: SampleFormat=3 BitsPerSample=32 SamplesPerPixel=3` when the TIFF path decides it; `float samples count in 0..65535: <above> of <samples> above <threshold>` or `… count in 0..1`; the exceptions are not logged here (the pair loader logs the one it catches, inner exception included) |
| `FitsImageLoader` (`Fits`) | Debug, Trace | `<file>: layout <W>x<H> planes=<n> bitpix=<b> bzero=<v> bscale=<v> data at <offset>` at probe and at load; the rescale decision as for WIC; at Trace every header card, `card <80 chars trimmed>`, read by the loader from the first `DataOffset` bytes (D10), guarded by `IsTraceEnabled` |
| `DarkTitleBar` (`Chrome`) | Debug | `dark caption: build <n>, attribute <19|20>, colours <yes|no>` or `dark caption: build <n> has none` |

Nothing logs per row, per band or per pixel; a line whose message costs something to build
is guarded by `IsDebugEnabled` / `IsTraceEnabled`. `MinimapView`, `Bitmaps`, `Viewport` and
everything in Core log nothing.

### Reading a log

A process begins at `… starting` and ends at `exit <code> after <s>` or at a `FATAL` line. An
open is `open start` to `open done` (or `ended without an outcome`); the stage times say where
the time went; an `ERROR` line carries the stack under it. Two windows interleave in one file,
told apart by `[pid:thread]`. This paragraph goes in `docs/design/logging.md` and, for the
user, in the README's "Logs" section.

## Steps

Order: requirements, tests, code (the kit's rules "Requirements first", "Test before fix").
Commit small, on the branch, with the gate green.

### 1. Requirements and the notice file

- `docs/requirements.md`: a new area at the end, `## Diagnostic log`, with the sentence that
  the figures are FlatCheck's measurements taken over (ADR 0043), and four rows, `draft` until
  their tests exist, then `implemented`:
  - **R-120** the files: folder, fallback, names, 1 MiB roll, 5 kept, the next process goes
    on in the newest, UTF-8 without BOM, two processes write one file. Example: 8 MiB of lines
    leave `ImageVisualCompare.Wpf_03.log` to `_07.log`, none above 1 MiB plus one line.
  - **R-121** what the log holds, English with invariant numbers whatever `--lang` says, nothing
    on stdout: the first line's facts, one line per file probed and decoded with the facts of
    the table above, the open's start and done lines with the stage times, the padding question
    and its answer, the mode switches, the dialogs and the user's actions of the table, every
    exception with type, message and stack (those turned into a message included; a
    cancellation and the log's own probe excepted), the last line. Example: the real pair
    `de_printat.png` against `de_printat_PRINT_preview_16bit.tif` writes `open start A=… B=…
    mode=Linear`, `probed A: … 5938x3888 bits=16 channels=Rgb …`, `open done mode=Linear
    padded=no in <ms> (probe A …, probe B …, decode A …, decode B …)` (fill the numbers from the
    run in step 9).
  - **R-122** the level and `IMAGEVISUALCOMPARE_LOG`; `off` creates neither the folder nor a file;
    `trace` adds every FITS header card.
  - **R-123** two processes at full speed keep every line, each whole and once, across the roll
    (`NLog.Targets.AtomicFile`; FlatCheck's figures quoted with their date).
- Under `## Version and licence`, **R-075**: `THIRD-PARTY-NOTICES.md` sits beside the
  executable in the build and publish output, byte for byte the root file, and names every
  third-party component shipped (NLog and NLog.Targets.AtomicFile, BSD-3-Clause), with the
  licence text. Example as R-074's.
- `THIRD-PARTY-NOTICES.md` at the project root: a short head (what the file is, GPLv3 for the
  program itself, `LICENSE`), then the NLog section taken from
  `E:\Photografie\Code\FlatCheck\THIRD-PARTY-NOTICES.md` lines 100 to 162, reworded for this
  project (ADR 0043 instead of ADR 0070, R-123 instead of R-112, no About mention: D5), the
  BSD text verbatim.

### 2. The packages

- `Directory.Packages.props`: the two `PackageVersion`s, 6.2.2, each with its "why" (copy the
  spirit of FlatCheck's comments; cite ADR 0043 and FlatCheck's ADR 0070 for the measurement);
  rewrite the head comment: Core references no package; the application references NLog and
  its atomic file target and nothing else; the test project the four below and NLog.
- `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`: `PackageReference` for both;
  the `None` link of `THIRD-PARTY-NOTICES.md` beside the `LICENSE` one.
- `tests/ImageVisualCompare.Tests/ImageVisualCompare.Tests.csproj`: `PackageReference
  Include="NLog"` (LogCapture subclasses `TargetWithLayout`; declared, not taken transitively).
- `dotnet restore ImageVisualCompare.sln --force-evaluate` so the three `packages.lock.json`
  are rewritten (Core's must not change); commit the lock files with the csproj changes.

### 3. The logging layer, `src/ImageVisualCompare.Wpf/Logging/`

Port from `E:\Photografie\Code\FlatCheck\FlatCheck.Core\Logging\`, into the namespace
`ImageVisualCompare.Wpf.Logging`, file-scoped namespaces, `Nullable` on (`string?` where
FlatCheck has a nullable reference), the GPL header, XML comments on every type and method
(the rule "Documentation comments"):

- `LogSetup.cs`: `LogPlace`, `FileSizeLimit`, `FilesPerExecutable`, `LevelVariable =
  "IMAGEVISUALCOMPARE_LOG"`, `FolderName`, `FallbackFolder = "ImageVisualCompare"`,
  `DefaultLevel`, `LineLayout`, `Apply(string entryName, IReadOnlyList<string> args)`,
  `Plan`, `Close(int exitCode)`, `Fatal(Exception?, string)`, `Build`, `ResolveFolder`,
  `CanWrite` (the folder probe **and** the append probe of the newest file), `NewestLog`,
  `LevelFrom`, `ExecutableName`, `StartupLine` (uses `VersionInfo.Value`). `internal`, as
  FlatCheck's; the test project already has `InternalsVisibleTo`.
- `LogText.cs`: `Num`, `Int`, `Ms`, `Seconds`, `Str`, `MiB`, `KiB`, `FileMiB`, `Yes(bool)`,
  and this project's own `Image(ImageInfo)` (the `<facts>` form above), `Size(int w, int h)`
  (`5938x3888`) and `Mode(DisplayMode)` (the enum's name). Drop FlatCheck's lens and frame
  helpers.
- `RunClock.cs`: as FlatCheck's (`Start`, `Lap`, `Done`, `Dispose`).
- `Loggers.cs`: the eight names.

### 4. The entry point and the crash hooks

`src/ImageVisualCompare.Wpf/App.xaml.cs`:

- A constructor (`App.xaml` keeps its `StartupUri`; the constructor runs before the main
  window is built, so a failure while `Theme.xaml` loads is in the log too):
  `try { LogSetup.Apply("ImageVisualCompare.Wpf",
  Environment.GetCommandLineArgs().Skip(1).ToArray()); } catch (Exception) { }` with the
  comment that the log never stops the window; then
  `DispatcherUnhandledException += (_, e) => LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");`
  (do not set `e.Handled`: D7).
- `static Logger Log => LogManager.GetLogger(Loggers.App);` fetched on use (comment why).
- `ApplyCommandLine` logs the language, the refused `--lang` and the pair or its absence.
- `OnExit`: `try { LogSetup.Close(e.ApplicationExitCode); } catch (Exception) { }`.

### 5. The seams

Add the lines of the table, in this order, each with its test from step 7 written first:
`ImagePairLoader` (wrap the body in `using RunClock clock = RunClock.Start(Log, "open", …)`;
`Lap` after each probe and decode; `Done` with the outcome; the `catch` logs the exception
with `Log.Error(ex, …)` before returning the failure; `OperationCanceledException` logs
`open cancelled on <path>` at Info and rethrows), `RoutingImageLoader`, `WicImageLoader`
(`Describe` is the one place that sees the format and the decoder: log there, at Debug, once per
probe and once per load; the float decisions in `FloatSamplesOf` and `LoadFloat`),
`FitsImageLoader` (`Probe` and `Load` log the layout; `Load` reads the cards at Trace),
`MainWindow`, `MessageDialog`, `FileDialogService`, `CompareView`, `DarkTitleBar`.

Where a message is shown to the user, log it as shown (`result.Error!`); where a fact is
logged, use the invariant `LogText` form, never `ImageInfo.FormatDescription`,
`UiText.Current.*` or an interpolated double.

### 6. The tests, `tests/ImageVisualCompare.Tests/`

- `LogCapture.cs`: port FlatCheck's (`[ModuleInitializer]` configures one `CaptureTarget` at
  Trace; `Start()` opens an `AsyncLocal` scope; `Events`, `Messages`, `Text`). Every test of
  the suite then runs every log line on its path, and a line that throws fails a test.
- `LoggingTests.cs` (port from `FlatCheck.Tests/LoggingTests.cs`, names on the new IDs; a
  `TempWorkspace`-like helper: `TempImages` already makes a temp folder, reuse or add a small
  `TempFolder`):
  `R120_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName`,
  `R120_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback`,
  `R120_TheLogRollsAt1MiBAndKeepsAtMost5Files`, `R120_TheFilesBeginWithTheExecutablesName`
  (theory: a path, the `dotnet` host, null), `R120_AReadOnlyNewestLogFileSendsTheLogToTheFallback`,
  `R120_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe`,
  `R121_TheFirstLineCarriesTheFacts`, `R121_TheConfigurationHasOneFileTargetAndNoConsole`,
  `R121_TheSameCrashSeenByTwoHooksIsWrittenOnce`,
  `R121_AnOpenLogsBothFilesTheCanvasAndTheTimings` (an `ImagePairLoader` over
  `RoutingImageLoader` on two `TempImages` PNGs: the start line, two `probed`, two `decoded`,
  the done line with four stages), `R121_AFailedOpenLogsTheExceptionWithItsStackAndTheMessageShown`
  (a truncated PNG, as `WicImageLoaderTests` makes one: one Error event whose `Exception` is
  not null and whose line holds the type and ` at `), `R121_ADeclinedPaddingIsLoggedAsDeclined`,
  `R121_AMismatchWithNoOneToAskIsLoggedAsRefused`, `R121_AFitsLoadLogsItsLayout` (`FitsFixture`),
  `R121_TheLogIsTheSameInBothLanguages` (the same open under `InRomanian`: identical messages
  but the quoted user message), `R122_TheLevelComesFromTheEnvironmentVariable` (theory),
  `R122_OffCreatesNeitherTheFolderNorAFile`, `R122_TraceHoldsEveryFitsHeaderCard`,
  `R123_TwoProcessesWritingAtFullSpeedKeepEveryLine` (two `LogFactory`s, a `Barrier`, 4000
  lines each, every line whole and once, both interleavings seen), the invariants
  `OnlyTheApplicationConfiguresTheLog` (`LogSetup.Apply|Close(` only in `App.xaml.cs`;
  `LogManager.Configuration =` only in `LogSetup.cs` and `LogCapture.cs`),
  `NoLogLineGoesThroughTheTranslation` (no `UiText.` inside a `Log.<Level>(` call; every
  logger declared one of the two ways), `CoreHoldsNoLoggerAndNoPackage` (no `NLog` in
  `src/ImageVisualCompare.Core/**/*.cs` and no `PackageReference` in its csproj: D1),
  `TheElapsedTimeHasTwoForms`.
- `WindowLoggingTests.cs`, on `WpfThread` with the doubles of `MainWindowTests` (a dialog
  double and a loader that returns a 4×4 pair, as `ModeLoader` does):
  `R121_AnOpenFromTheButtonLogsTheAskAndThePairShown`,
  `R121_ACannotCompareMessageIsLoggedAsShownBesideTheLoadersError` (a loader whose `Probe`
  throws `FileFormatException`; the dialog cannot open on a never-shown window, so give the
  window a message sink: simplest is to assert the loader's Error line and the window's
  Warn line through the capture, and skip the dialog by a dialogs double; if the modal
  `MessageDialog.Show` blocks the test thread, route the window's "say" through a small
  internal `Func`/interface as the dialogs already are, in the same spirit as ADR 0032),
  `R121_AModeSwitchRefusedIsLoggedWithItsReason` (no pair: `refused (no pair)`),
  `R121_ThePadQuestionsAnswerIsLogged`.
- `LicenseHeaderTests.cs` (where `R074_TheLicenceSitsNextToTheApplication` lives, line 143):
  `R075_TheThirdPartyNoticesSitBesideTheExecutable` mirroring it.
- `LicenseHeaderTests` will fail on any new `.cs` without the header: add it.

### 7. Decision, design notes, CLAUDE.md, README, CHANGELOG

- `docs/decisions/0043-a-diagnostic-log-with-nlog-in-the-application-the-core-untouched.md`
  (check `main`'s latest number before the merge; renumber on a collision): Context (the
  owner's ask, FlatCheck's measurements taken over), Decision (the D rows that were approved,
  in prose: where NLog lives, the files, the target, the level, the words, the lines, the
  crashes, the notice), Alternatives rejected (NLog in Core; `NLog.config`; a `--log` option;
  the plain target; a line per wheel tick; a crash dialog; About listing NLog), Consequences
  (a package in the application, the rule reworded; the log is the first thing to ask a
  reporter for; every test runs every log line; the tests named).
- `docs/design/logging.md`, new: a `Covers:` sentence naming `src/ImageVisualCompare.Wpf/Logging/*.cs`,
  `App.xaml.cs`, `tests/.../LogCapture.cs`, `LoggingTests.cs`, `WindowLoggingTests.cs`, and the
  files of the seam table; the sections of FlatCheck's note adapted: how the parts fit (the
  four steps of `Apply`), the files as measured (the roll test's figures, printed by the test),
  the line, the loggers, an open's lines, what is logged where (the table), reading a log, in
  the tests. Add its row to `docs/design/README.md`.
- `docs/design/code-map.md`: `Logging/` and its four files; `THIRD-PARTY-NOTICES.md` under
  the csproj's links. `imaging.md`, `shell.md`, `language.md` (the log is English whatever
  `--lang` says), `testing.md` (`LogCapture`; every test runs the log lines): one paragraph
  each pointing to `logging.md`; `rendering.md` covers `CompareView.xaml.cs` (its `Covers:`,
  line 6), so it gets the same paragraph (the pair-set line and the four zoom lines).
- `CLAUDE.md` (stay under 200 lines: `ClaudeMd_StaysShort`): the first paragraph's "no NuGet
  package outside the test project" becomes "one NuGet package in the application, NLog, for
  the diagnostic log (ADR 0043); none in Core"; the rule "No imaging dependency" gains the
  clause; a rule **"The log is English and never stops the tool"** (every line through
  `LogText`, never `UiText`; `LogSetup.Apply`/`Close` only in `App`, in a `try`; loggers
  declared the two ways; nothing in Core logs; ADR 0043); Layout: `Logging/`; Commands: the
  environment variable; "Read before touching": `docs/design/logging.md` for `Logging/`, `App`.
- `README.md`: a `## Logs` section after "Starting it" (where the folder is, the fallback, the
  roll, what is in it, that it holds paths and figures and no pixels, the variable, "attach the
  whole `log` folder to a report"), adapted from FlatCheck's README lines 1380 to 1405; the
  "Licence" section names NLog, BSD-3-Clause and `THIRD-PARTY-NOTICES.md`; "Building"/"For
  developers" mention the env var for `trace`.
- `CHANGELOG.md`, `[Unreleased]`, Added: the log (one entry, R-120 to R-123, ADR 0043) and the
  notices file (R-075).
- `docs/skills.md`: `/wrap-up` step 9 also reads the run's log (first line, `open done`,
  `exit 0`); `/release` step 5: `THIRD-PARTY-NOTICES.md` ships beside the executable.
- Say at wrap-up that a release warrants a minor bump (a feature); the owner bumps.

### 8. Gate and build hygiene

`dotnet build ImageVisualCompare.sln -c Release --no-incremental -warnaserror --no-restore`
once before the final `scripts/verify.ps1` (an incremental build hides warnings);
`dotnet format` clean; `scripts/check-design.ps1 -Since main` lists the notes touched.

### 9. Run the application (the rule "Run the application")

Only when the owner is not using the PC (no live input otherwise). From the worktree, with
the real pair copied into `tests/testdata/large/` if the run needs it, or any two files:

```
dotnet run --project src/ImageVisualCompare.Wpf -c Release -- --lang=ro "E:\Photografie\Claude\de_printat.png" "E:\Photografie\Claude\de_printat_PRINT_preview_16bit.tif"
```

Close it without input: `(Get-Process ImageVisualCompare.Wpf).CloseMainWindow()`. Read
`src/ImageVisualCompare.Wpf/bin/Release/net8.0-windows/log/ImageVisualCompare.Wpf.log`: the
first line, `language=ro`, the pair from the command line, the two `probed`, the two `decoded`,
`open done` with four stages, `shown …`, `exit 0 after …`. Then: two instances at once (both
pids in one file); `IMAGEVISUALCOMPARE_LOG=off` (no folder created in a clean output);
`trace` on a FITS (the cards); a truncated copy of a PNG as B (the `ERROR` line with the
`COMException` inside, and the `cannot compare` line with the message as shown). Paste the
lines into R-121's example and the design note. `/validate` is not needed: no numeric code
changed and nothing logs inside a decode loop.

## Files

New: `src/ImageVisualCompare.Wpf/Logging/{LogSetup,LogText,RunClock,Loggers}.cs`,
`tests/ImageVisualCompare.Tests/{LogCapture,LoggingTests,WindowLoggingTests}.cs`,
`THIRD-PARTY-NOTICES.md`, `docs/design/logging.md`, `docs/decisions/0043-….md`,
`docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`.

Changed: `Directory.Packages.props`, the Wpf and Tests csproj and lock files, `App.xaml.cs`,
`MainWindow.xaml.cs`, `MessageDialog.xaml.cs`, `Services/FileDialogService.cs`,
`Controls/CompareView.xaml.cs`, `Interop/DarkTitleBar.cs`, `Imaging/{ImagePairLoader,
RoutingImageLoader,WicImageLoader,FitsImageLoader}.cs`, `VersionTests.cs` (R-075),
`docs/requirements.md`, `docs/design/{README,code-map,imaging,shell,language,testing,rendering}.md`,
`CLAUDE.md`, `README.md`, `CHANGELOG.md`, `docs/skills.md`.

Untouched: everything in `src/ImageVisualCompare.Core/` (D1), `kit.json`, `scripts/`,
`.claude/`, `stryker-config.json`.

## Verification

- `scripts/verify.ps1` green; the new tests print the roll's file sizes and the two-writer
  counts to the test output (as FlatCheck's do), pasted into the design note.
- The application run of step 9, its log read and quoted.
- The things that can go wrong, to check by hand: `CS8` nullable warnings in the port
  (warnings are errors); `IDE0005` on an unused `using NLog`; the `LICENSE` link pattern
  copied for the notices file; `LicenseHeaderTests` on the new files; `RepoInvariantTests`
  on the four IDs and R-075 (`draft` until the tests exist, `implemented` after);
  `ClaudeMd_StaysShort`; `check-design.ps1` on the new note's `Covers:` paths.
- Wrap-up with `/wrap-up`; propose the merge; do not merge, push or tag.

## Outcome and deviations (2026-10-30)

Implemented on `claude/Log-ImageVisualCompare-with-NLog`, the gate green at every commit: the log
with its tests and documents (711ae32), the validation re-run and a dated note on ADR 0032
(0834cbb), the fixes of the first review of the branch (6873c14), then those of the second review,
with ADR 0044 (7ae1cad), the live run of its key changes (0d6196f8, c494e48), and NLog listed in
About in place of the notices beside the executable (5d7063f). The branch's commits were squashed
into one before the merge, at the owner's word (2026-10-30), so these hashes are the branch's, not
`main`'s. What differs from the plan, and why:

- **The log starts in `Program.Main`, not in `App`.** The WPF test thread (ADR 0032) builds an
  `App`, and WPF's `Application` constructor queues the application's start, which ran there:
  `App.OnStartup` ran, `StartupUri` opened a real `MainWindow` (`IsVisible` true, maximised),
  and with `Apply` in `OnStartup` the test host wrote `log\testhost.log` beside itself and took
  the tests' capture away, so the first window tests saw no line. `Program.Main`, the project's
  `StartupObject`, starts the log before `App`, hooks the dispatcher's crashes and closes the log
  after `Run`; no test runs it. Put to the owner after the review, as the kit's rule "Questions to
  the owner" asks of a fact that contradicts an approved answer (D8): the owner chose
  `Program.Main` on 2026-10-30.
- **Found, not fixed: the test fixture opens a real window.** Every test run that uses the WPF
  test thread shows a maximised `MainWindow` on the desktop for as long as the test host lives,
  and its `Loaded` opens the real Open dialog, the tests running on inside the dialog's loop;
  `App.OnStartup` also sets `UiText.Current` from the test host's arguments, which can race the
  language tests. Since 2026-10-22. ADR 0032 has a dated correction note; `WpfThread.cs` and its
  comment are as they were. The owner chose on 2026-10-30 to have it fixed in a task of its own,
  in this session, after this branch's merge. The window tests of the log run one empty body on
  the thread before they open a capture, so they hold either way.
- **NLog is listed in About, and no notices file ships beside the executable (D5).** The suite's
  release puts every tool in one folder and refuses two different files at one path, and no other
  tool ships its notices beside its executable; the owner first chose, on 2026-10-30, to ship them
  as `ImageVisualCompare-THIRD-PARTY-NOTICES.md`, then, told before the merge that CopyWcs lists
  NLog in its About window as FlatCheck does, chose the same here: About ends with the components
  of `ThirdParty.cs` (R-072, R-075), `ThirdPartyTests` ties them to the csproj and to the notices
  file, and `THIRD-PARTY-NOTICES.md` stays beside the source. About's nine lines were fourteen;
  since e3836cc9 About is FlatCheck's window and the components a card of it
  (`docs/plans/2026-10-30-the-about-window-as-flatchecks.md`, ADR 0045).
- **Found on the way, not fixed:** `UiTextTests.R101_TheMainWindow_InRomanian` fails when it runs
  alone, before any other test has used the WPF thread, as it does on c494e48: the `App` start WPF
  runs on that thread sets the interface back to English from the test host's arguments, after
  the test set Romanian (the race ADR 0043's Consequences name). In the whole suite an earlier
  test runs that start first. The fixture's task, next, covers it.
- **The names of the lines.** The plan's `open start` and `open done` are `load start` and
  `load done`, since a switch of display mode goes through the same load; its
  `open asked (button|Ctrl+O|startup)` is `open asked by the start`, `by the Open button`,
  `by Ctrl+O`.
- **`App`'s logger is a field, not a property**, since `App` no longer starts the log; only
  `LogSetup`'s is fetched on use.
- **The ADR's file name** is `0043-a-diagnostic-log-with-nlog-started-by-the-entry-point.md`, not
  the plan's `…-the-core-untouched.md`; it supersedes ADR 0003 and ADR 0008 in part, whose
  decisions said the application had no package.
- **The dialogs' answers are logged by `MainWindow`, not by `FileDialogService`** (one line, what
  the dialogs chose or that they were cancelled), which the window tests can check with their
  dialog double; **`MessageDialog` writes nothing**, since its title is the interface's: each
  caller says what it shows, and the failure itself, with its exception and the message as shown,
  is the pair loader's `ERROR` line. There is no `cannot compare` line of the window's own.
- **`RoutingImageLoader` writes nothing**: the logger column, `Wic` or `Fits`, says which loader a
  file went to.
- **`DarkTitleBar` writes the HRESULTs** of its DWM calls, which it still does not act on. No test
  reaches it: a window never shown has no handle.
- **`/validate` was run after all.** The plan said it was not needed; `/wrap-up` asks for it
  whenever `Imaging/` changes, and the loaders gained lines around their passes. All six cases
  passed against their recorded figures, which stay as they are (`docs/validation.md`, History,
  2026-10-30).
- **The review of the branch** (two subagents, one on the code and tests, one on the documents,
  2026-10-30) found six points in the code and nine in the documents; every one was checked in
  the code and found true, the TIFF format by a measurement (`format=Default` for the 96 bpp float
  RGB of Siril's exports). The owner took them all, with the two lines it proposed beside them:
  - at `Trace` the FITS cards are written from `Probe`, in a `finally`, by a walk of the headers
    to their `END` cards, so a refused file shows its cards too; `Load` writes none;
  - the WIC line writes `unnamed` for a format WPF does not name, a TIFF's SampleFormat,
    BitsPerSample and SamplesPerPixel, and how many colour profiles the file carries, the last
    one of the two lines the review proposed;
  - the main window writes how WPF renders, its tier, the other of the two;
  - BZERO and BSCALE are written exactly (`LogText.Exact`), not to six digits;
  - the last line is `LogSetup.ExitLine`, tested, and says `ended by an unhandled exception` when
    `Run` never returned and the runtime still runs `Main`'s `finally`;
  - an invariant test reads in `Program.cs` that the log starts before the `App` is built, closes
    after `Run` and hooks the dispatcher, and in `LogSetup.cs` the process's two hooks;
  - `LogCapture` turns `LogManager.ThrowExceptions` on, so a failing layout or target fails the
    test, as its summary said;
  - a repeat of a held key writes no zoom line, and a repeat of `Ctrl+O`, `H` or `F` while a pair
    is being opened is not passed on (no test: a `KeyEventArgs` needs a presentation source);
  - the README's level instructions name `cmd.exe` and PowerShell, say that `set` holds for the
    window and `setx` for the programs started after it, the launcher included, and how to go
    back; the 5 MiB is per program; `trace` may put the observer and the site in the log;
  - the notices say "the packages the application references", which is what R-075 checks; the
    .NET runtime a self-contained release carries is a question for the suite, left apart;
  - the wording that the review found false or contradictory: `OneLine`, a file's end with two
    writers (R-120), the logger declarations in `CLAUDE.md` and in a test's comment, R-121's
    words of an open, `CLAUDE.md`'s "without showing anything"; and `logging.md` and `testing.md`
    no longer repeat ADR 0043.
- **A second review, of the fix commit alone** (a new subagent, 2026-10-30, at the owner's word),
  found nine points, all checked in the code and found true; the owner took them all, and chose
  to fix here the older defect the first of them uncovered:
  - a held `H` or `F` switches once: its repeats do nothing (R-114, ADR 0044); on a FITS pair a
    held key had switched back and forth, a decode of the pair each time, and on a PNG pair it
    had written a refused switch about thirty times a second. `OnModeKeyAsync` takes the repeat as
    an argument, and its test failed on the code before;
  - the FITS card walk goes on past the data of a unit to the next extension header (random
    groups, a table), and writes a card with a byte that is not printable ASCII with that byte as
    `?` instead of stopping there; two tests, each failing on the walk before;
  - the WIC line's two helpers read everything inside their `try` and catch every exception, since
    their reads are the log's alone;
  - R-121 promises the crash's last line only when the runtime still runs the entry point's last
    step; the README counts NLog's two packages, and says that a tool of the suite that keeps a log
    too keeps it in the same folder, FlatCheck for one, not that every tool does;
  - ADR 0043 keeps the decision of where the log starts, its mechanics in `logging.md`; `logging.md`
    no longer restates `CLAUDE.md`'s rule on loggers;
  - the entry-point test also reads that `Apply` is in a `try` and `Close` in a `finally`;
  - a held zoom key's release writes the zoom its repeats left; `Probe`'s summary says that it
    reads the headers once more at `Trace`.
- **Found and fixed on the way:** six double-encoded characters in three lines of the README
  (`Histogramă` and `·`), already on `main`.
- **Not tested on the WPF thread**: the lines of a failed open, a declined padding and About,
  whose dialogs need a shown owner; the pair loader's lines of the same events are tested.
- **The application was launched after the review, at the owner's word** (2026-10-30; before it,
  Siril was in the foreground with the owner's input 0 s before). Three runs of the branch's
  Release build, each opened on a pair from the command line and closed by its window's close
  message, without a click or a key: with `--lang=ro` on the PNG and TIFF pair, the log held the
  real first line, `language=ro`, the render tier, every line of the load, the pair set at 24.3 %
  and `exit 0 after 8.3 s`, all in English; two windows at once at `trace` on the FITS pair
  interleaved in one file, each with its 137 header cards and its `exit 0`; with `off`, a copy of
  the build folder got no `log` folder. R-121's example quotes the first run. The second review's
  changes to the key handlers of the window and of the surface were not launched at 14:29, the
  owner being at the PC, and were at 14:41, at the owner's word: the Release build of 0d6196f8 at
  `trace` on the FITS pair, `H` held 20 s by 422 key-downs, then the numpad's `+` held 1.5 s by
  32, each sent only while the window owned the foreground. The log held one
  `mode switch Linear -> Histogram`, its load done 2.2 s later and no other line in the 17.7 s of
  repeats after it; `zoom in -> 25%` at the first step and `zoom after the held key -> 1600%` at
  the release; 274 card lines, 137 for each load of the pair, as before the walk changed;
  `exit 0 after 26.9 s` and no error. A screenshot showed `Histogram` lit, `1600%`, and
  `· Histogram` on the status bar.
