# Add a diagnostic log with NLog

Plan for the task branch `claude/Log-LightsCheck-with-NLog`, worktree
`.claude/worktrees/Log-LightsCheck-with-NLog` (the repository root is `E:\Photografie\Code`,
so the project is under `LightsCheck\` in the worktree; open the session there, not at the
worktree's root, which loads no `CLAUDE.md`). The plan was drafted for
`claude/Add-a-diagnostic-log-with-NLog`; DarksCheck's plan of the same day names that branch too,
and branch names are shared by the whole repository, so this task took the name of
ImageVisualCompare's (`claude/Log-ImageVisualCompare-with-NLog`). Written for an implementer who
has this file, `CLAUDE.md`, the repository and the FlatCheck checkout beside it
(`E:\Photografie\Code\FlatCheck`), and nothing else from the planning session. Approved by the
owner on 2026-10-30 as written, every row as recommended ("Do this task here").

## Context

A bug report from a user carries only what was on the screen, and the screen is thin on purpose:
the console prints `ERROR: <message>` without the exception's type, stack or inner exceptions
(`src/LightsCheck.Cli/Program.cs:161-165`); the window puts the same bare message on the problem
card (`MainViewModel.OpenAsync`) or in a frame's cut-out note (`LoadCropsAsync`); a frame that
could not be measured keeps only `e.Message` in its row (`SeriesRun.Run`, `FrameInfo.Read`).
Nothing records which files were read and what their headers said, which figures led to a verdict,
how long each stage took, or what the window did before it failed. Several places swallow an
exception for good reasons and leave no trace: the read noise (`SeriesRun.ReadNoise` returns null
on any exception), the plate-solved FITS (`SeriesRun.ReadWcs` skips a file it cannot read), the
raw's lens block (`RawReader.FillShootingInfo`), the stored arrows (`UserSettings`), the dark title
bar, the About window's link. Nothing in the five projects logs today: no `Trace`, no
`Debug.WriteLine`, no `ILogger`.

The owner's ask (2026-10-30): a log, with NLog, as text, in `log/` beside the executable, file
names starting with the executable's name, at most 5 files per executable, rolling, at most 1 MB
each, done the way FlatCheck does it, with one criterion: a bug report with the log attached can
be diagnosed from the log.

FlatCheck built exactly this on 2026-10-27 (its ADR 0070, its ADR 0072 after a 42-point review,
its `docs/design/logging.md`, its plans `2026-10-27-add-a-diagnostic-log-with-nlog.md` and
`2026-10-27-fix-the-diagnostic-log-review-findings.md`), and measured the choices that mattered:
NLog's plain file target lost lines between two processes (33 to 48 of 4000 closed between
lines, 67 to 72 kept open) and `NLog.Targets.AtomicFile` lost none at 0.09 ms a line; NLog 6.2.2's
`MaxArchiveFiles` counts the file being written; a logger held in a static field of the type that
configures NLog makes it load a stray `NLog.config` before the configuration is set. This plan
takes those results as measured and does not repeat the measurements; it repeats the tests that
pin them, on this project's configuration.

## Decisions

The owner asked for FlatCheck's design. The rows D1 to D8 are FlatCheck's decisions (its ADRs 0070
and 0072), taken here as they are; the rows L1 to L9 are the choices LightsCheck has to make on
its own, each with a recommendation. Approving this plan as written settles every row as
recommended; a row the owner answers otherwise is implemented as answered, without asking again.

| # | Decision | Taken from FlatCheck |
| --- | --- | --- |
| D1 | Where NLog lives | The core references the packages and logs at its seams; each application configures NLog **in code** at its entry point through `LogSetup.Apply` first and `LogSetup.Close` last. No `NLog.config`. Without a configuration (a library use) every call writes nothing. |
| D2 | The files | `<executable>.log` in `log` beside the executable, rolled forward by NLog past 1 MiB to `<executable>_01.log`, `_02`, ...; `MaxArchiveFiles = 5` keeps 5 files, the file being written included; the next process goes on in the newest file; UTF-8 without BOM. |
| D3 | Two processes on one file | `AtomicFileTarget` from `NLog.Targets.AtomicFile`, kept open, `ConcurrentWrites`: two windows on one session each keep every line. The second package is taken on FlatCheck's measurement (L7 says what is still tested here). |
| D4 | The level | Debug by default; an environment variable sets `off`, `fatal`, `error`, `warn`, `info`, `debug` or `trace`. No console option, so the console's contract, `--help` and the goldens do not move. |
| D5 | The folder cannot be written | Fall back to a folder under `%LOCALAPPDATA%` and say so in the first line. The probe creates the folder, a file deleted on close, and opens for append the file NLog will write next, so a read-only newest file falls back instead of losing every line. |
| D6 | The words | English, invariant numbers, a verdict by its key, never through `T(english, romanian)`; an exception's or a refusal's message is logged as the user saw it, so it can be Romanian under `--lang ro`. |
| D7 | Crashes | `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and the window's `DispatcherUnhandledException` log at Fatal (Error for the unobserved task) and flush; the same exception seen twice is written once; the crash behaviour does not change. |
| D8 | The log never stops the tool | `Apply` and `Close` run in a `try` in both applications; `LogSetup`'s and `App`'s loggers are fetched on use, so the configuration is set before any logger exists. A run that throws leaves its stages through a disposable `RunClock`. |

| # | Decision | Recommendation and why |
| --- | --- | --- |
| L1 | Whose code `Logging/` is | **LightsCheck's own, adapted from FlatCheck in LightsCheck's style** (nullable enabled, file-scoped namespaces, braces, the kit's documentation comments), marked "adapted from FlatCheck" in the file comment, not a copy under the rule "Copied files stay copies", which covers `Imaging/` only. FlatCheck's ADR 0070 says the other tools "can take `LogSetup` as it is, under their own names"; the names, constants and method shapes stay FlatCheck's so a fix found in one tool is easy to carry to the other. |
| L2 | The FITS reader, a copy | **`FitsReader.cs` stays untouched**; `ImageLoader.cs` (adapted, LightsCheck's to change) writes the `loaded` line, the header keywords at Debug and every card at Trace from `LoadedFrame.Cards`, which it already holds; `RawReader.cs` (adapted) writes libraw's figures at Debug and its lens-block catch at Warn. FlatCheck's `FitsReader` now logs from inside, but taking that hunk would make the copy depend on a `LogText.FitsKeys` of its own and change the copy's stated contents (2.3.0 plus the edits ADR 0003, 0018, 0021, 0025 and 0027 list) for no line the loader cannot write itself. |
| L3 | The variable and the fallback | **`LIGHTSCHECK_LOG`** and **`%LOCALAPPDATA%\LightsCheck\log`**: the folder `UserSettings` already writes `settings.json` into. |
| L4 | The series' per-frame lines | **One Info line per frame measured, written from the worker thread** (`frame 12/87 DSC_0012.NEF ... in 1830 ms`) and that frame's figures at Debug, plus one Debug line per frame registered. About 3 lines per frame, 180 KB for an 87-frame night at Debug, well inside the 5 MiB; the thread id separates the interleaved frames and the frame's index is in every line. The progress ticks themselves are not logged: the frame lines are the progress. |
| L5 | The window's loggers | **Two names, `App` and `Window`**, in a `Loggers` class: the window has one view-model and its tabs are views that bind it, so FlatCheck's `Window.<Tab>` has nothing to name here. |
| L6 | The console's mode | **An `Options.Mode` enum (`Help`, `Error`, `Frame`, `Inventory`, `Series`) decided once in `Options.Parse` by the dispatch's precedence**; `RunIn` switches on it and the log's `mode=` line reads it, so the two cannot drift (FlatCheck's ADR 0072). The usage text, the goldens and every exit code stay as they are. |
| L7 | What is measured again here | **The tests only, not the alternatives**: the rolling (5 files, the newest grown by the next process, one executable's roll leaving the other's files), the two-writer test crossing the roll, the read-only newest file, the file another process holds open. The plain target is not measured again: FlatCheck's three runs settled it. The cost of the log is measured once on the Veil session (L8). |
| L8 | The cost | **Measure one series run on the Veil session with `LIGHTSCHECK_LOG=off` and at Debug, three runs each, and paste the times** in the plan's outcome (the kit's rule "Measured numbers"). Expected: no measurable difference (FlatCheck: 3.6 to 4.0 s off, 3.7 to 3.9 s at Debug); the per-frame lines are 0.09 ms each. |
| L9 | The notices | **`THIRD-PARTY-NOTICES.md` stays Romanian** (the rule "Languages"): a Romanian `## NLog` section with FlatCheck's compatibility argument translated and the BSD-3-Clause text quoted verbatim in English, as a licence text is. |

## Design

### The packages

- `NLog` **6.2.2** and `NLog.Targets.AtomicFile` **6.2.2** (BSD-3-Clause, netstandard2.0, purely
  managed), the versions FlatCheck pinned; the two move together. Only the core references them
  (`src/LightsCheck.Core/LightsCheck.Core.csproj`, two `PackageReference` lines with a comment
  naming ADR 0029 and the seams); the console, the window and the two test projects get them
  transitively, which is what `ThirdPartyTests` expects (it reads the core csproj only).
- `Directory.Packages.props`: two `PackageVersion` lines, each with its "why" (the rule
  "Packages"): the log beside each executable, configured in code, BSD; the atomic target so two
  windows writing one log lose no line, FlatCheck's measurement cited, "keep its version equal to
  NLog's".
- Lock files: `dotnet restore LightsCheck.sln --force-evaluate` regenerates the five
  `packages.lock.json`; commit them with the package (a branch that adds a package and skips this
  fails the gate's locked restore).
- `src/LightsCheck.Core/ThirdParty.cs`: a third `ThirdPartyComponent`, `Name: "NLog"`, a `T(...)`
  role ("writing the diagnostic log beside the program (the log folder), two windows into one
  file" / "scrierea jurnalului de diagnostic langa program (folderul log), doua ferestre in acelasi
  fisier"), `Licence: "BSD-3-Clause"`, the copyright line copied from FlatCheck's `ThirdParty.cs`
  (the first line of NLog's `LICENSE.txt` at the commit 6.2.2 was built from, `d9ebf82`:
  `Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian
  Verdurmen`), `Url: "https://nlog-project.org/"`, `Packages: ["NLog", "NLog.Targets.AtomicFile"]`,
  with the comment that one component claims two packages as LibRaw does.
  `ThirdPartyTests.EveryComponentNamesItsLicenceAndUrl` asserts 2 components: it becomes 3, the
  intended change of that test, with a comment saying why.
- `THIRD-PARTY-NOTICES.md`: a `## NLog` section between LibRaw's and "Componente de test" (L9):
  Rol (the log, decides and computes nothing, two packages of one project, the atomic one for two
  windows), Licenta (BSD-3-Clause, both packages, version 6.2.2, no native file), Compatibilitate
  (permissive, the FSF lists the 3-clause BSD as GPL-compatible; of its three conditions the
  source one does not arise, the binary one is met by the About window and by the text reproduced
  here, the third forbids endorsement, which nothing does), the quoted licence text word for word
  from FlatCheck's section, the package's second notice line, the Project and Licence links. The
  About window lists `ThirdParty.Components`, so NLog appears there by itself (`AboutTests`
  compares the lists, no count to change).

### `src/LightsCheck.Core/Logging/LogSetup.cs` (new)

FlatCheck's `LogSetup` in LightsCheck's style (L1): `internal static class LogSetup` plus the
record `LogPlace(string ExecutableName, string? Folder, bool FellBack, LogLevel Level)`.

```csharp
internal const long FileSizeLimit = 1024 * 1024;        // 1 MiB (R-070)
internal const int FilesPerExecutable = 5;              // the file being written included (NLog 6.2.2 counts it)
internal const string LevelVariable = "LIGHTSCHECK_LOG";
internal const string FolderName = "log";
internal const string FallbackFolder = "LightsCheck";   // under %LOCALAPPDATA%
internal static readonly LogLevel DefaultLevel = LogLevel.Debug;
internal const string LineLayout = "${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}${onexception:inner=${newline}${exception:format=ToString}}";

private static Logger Log => LogManager.GetLogger("Process");   // fetched on use, never a static field (D8)

internal static void Apply(string entryName, IReadOnlyList<string> args);
internal static LogPlace Plan(string entryName, string? processPath, string baseDirectory, string fallbackRoot, string? levelValue);
internal static void Close(int exitCode);
internal static void Fatal(Exception? e, string where);
internal static LoggingConfiguration Build(string executableName, string folder, LogLevel minimum);
internal static string ResolveFolder(string baseDirectory, string fallbackRoot, string executableName, out bool fellBack);
internal static string? NewestLog(string folder, string executableName);
internal static LogLevel LevelFrom(string? value);
internal static string ExecutableName(string? processPath, string entryName);
internal static string StartupLine(string executableName, IReadOnlyList<string> args, string folder, bool fellBack, LogLevel level);
```

Behaviour, line for line as FlatCheck's (read its `FlatCheck.Core/Logging/LogSetup.cs` before
writing this one):

- `Apply` runs once per process (`Interlocked.CompareExchange` on the stopwatch); `Plan` decides
  the place; `LogManager.ThrowExceptions = false`; the configuration is assigned **before any
  logger exists**; `Off` gives a configuration without targets and `Apply` returns there; then the
  two process hooks and the first line at Info. The arguments go in verbatim: the paths are the
  diagnosis.
- `Build`: one `AtomicFileTarget` named `file`, `FileName = <folder>\<executable>.log`,
  `Layout = LineLayout`, UTF-8 without BOM, `WriteBom = false`, `ArchiveAboveSize = FileSizeLimit`,
  `MaxArchiveFiles = FilesPerExecutable`, `ConcurrentWrites = true`, `KeepFileOpen = true`,
  `AutoFlush = true`, `CreateDirs = true`; one rule `*` from `minimum` to Fatal;
  `DefaultCultureInfo = InvariantCulture`. No console target, ever: stdout and stderr are the
  console's contract (R-001).
- `ResolveFolder` and its private `CanWrite`: the folder, the probe `<name>.probe-<pid>-<guid>.tmp`
  with `DeleteOnClose`, the append probe on `NewestLog` with `FileShare.ReadWrite | Delete`; on
  `IOException`, `UnauthorizedAccessException`, `NotSupportedException` or `ArgumentException` the
  fallback, probed the same way (if even that fails NLog drops the lines).
- `StartupLine`: `lightscheck 1.0.0 starting; exe=...; args=["D:\astro\veil", "--lang", "ro"];
  cwd=...; log=... (fallback: the folder beside the executable cannot be written); level=Debug;
  os=...; cpus=12; memory=31.9 GiB; runtime=.NET 8.0.x X64; culture=ro-RO/ro-RO; tz=... (UTC+02:00);
  pid=1234`. `VersionInfo.Value` gives the version.
- `Close`: `exit 0 after 12.2 s` then `LogManager.Shutdown()`; nothing when `Apply` did not run.
- `Fatal`: the same exception a second time is named (`...: the exception logged above`), not
  written again.

### `src/LightsCheck.Core/Logging/LogText.cs` (new)

How a value is written: `Num(double)` as `G6`, `Num(double?)` with `none`, `Int`, `Ms` (`1141
ms`), `Seconds` (`12.9 s`, one decimal, never whole seconds), `Str` (`none` for empty), `KiB`,
`MiB`, `FileKiB`, `FileMiB` (a `?` when the size cannot be read: the log never throws for a
size), `List` (`[a, b]`), `Yes`, `Time(DateTime?)` as `yyyy-MM-dd HH:mm:ss` invariant or `none`.
Then one method per result, every figure a verdict rests on, no report wording:

- `Frame(string path, LoadedFrame f, string reader)`: the loaded line's text: path, `FileMiB`,
  `reader=raw|fits`, `WxH` or `no pixels`, `planes`, `bitpix`, `cfa=<letters> (<source>)` or
  `none`, `scale=<white>/<black> (<source>)` or `none`, `camera`, `focal`, `fnumber`, `exposure`,
  `iso`, `time`, `pixelSize`, `focal35`, `lens=<id>/<mount>/<name>` and `range` as FlatCheck's
  `Lens` and `Range` write them.
- `FitsKeys(IReadOnlyDictionary<string, string> header)`: `NAXIS NAXIS1 NAXIS2 NAXIS3 BITPIX BZERO
  BSCALE BAYERPAT ROWORDER XBAYROFF YBAYROFF INSTRUME FOCALLEN XPIXSZ EXPTIME ISOSPEED DATE-OBS
  CTYPE1 CRVAL2` as `KEY=value`, `none` for the missing.
- `Info(FrameInfo)`: the inventory's view of a frame (camera, focal, fnumber, exposure, iso, time,
  pixelSize, focal35, size, lens, range, `error=<message>` or `none`).
- `Session(SessionFolder)`: `root=... lights=<dir> (<n> <ext>) biases=<n> darks=<n> raws=yes|no`.
- `Measurement(FrameMeasurement m)`: `WxH scale=<white>/<black> channels=[<name>:median/saturated/
  min/hp]` background median/min/max/centre/corners, noiseSigma, detected/bright/saturatedStars,
  fwhm median/p90, elongation median/p90, elongatedFraction, angle/coherence, zones centre/mid/edge
  (fwhm, elongation, stars), halo, `trails=<n>` with the first five as `len@x,y`,
  `registrationStars=<n>`. Never the shapes list and never a pixel.
- `Registration(RegistrationResult)`: `ok matches votes dx dy rotation scale rms`.
- `Cadence(Cadence)`, `Dither(DitherAnalysis)` (every scalar of the record, the size classes, the
  walking stretch, the runs' counts, the spans; never the per-frame lists, which are in the
  report), `Pattern(SensorPattern)` (status, frames, cropSize, pixels, hot, hotPerMpx, per
  position `name: pattern/frame sigma`), `Series(SeriesAnalysis)` (the counts, sessionFwhm and
  stars, the four trends, focusHeld, dewSuspected, the drift figures and axis, the walking figures,
  the rotation figures, raAxis and its source, trailAngle and trailOn, the read noise per position,
  the swamping of the first frame, the polar figures, and every enum by its name), `Wcs(Wcs?)`,
  `ReadNoise(IReadOnlyList<ReadNoise>?)`.

Enums (`FrameClass`, `WalkingNoise`, `PolarAlignment`, `PatternStatus`, `Axis`, `TrailAxis`,
`PixelPitchSource`) are written by `ToString()`: English keys, the report's words stay in
`Report/`.

### `src/LightsCheck.Core/Logging/RunClock.cs` (new)

FlatCheck's `RunClock`, disposable: `Start(Logger, run, inputs)` writes `<run> start <inputs>` at
Info; `Lap(stage)`; `Done(outcome)` writes `<run> done <outcome> in N ms (open 3 ms, measure 1830
ms, ...)`; `Dispose` without `Done` writes `<run> ended without an outcome after N ms (<stages>)`.
Every run starts its clock before it opens the session, so a folder that is refused still leaves
the start line.

### `src/LightsCheck.Wpf/Loggers.cs` (new)

`internal static class Loggers { internal const string App = "App"; internal const string Window
= "Window"; }` (L5).

### The applications

- `src/LightsCheck.Cli/Program.cs`: `Main` wraps the run as FlatCheck's does: `try {
  LogSetup.Apply("lightscheck", args); } catch (Exception e) when (e is not
  OperationCanceledException) { }` first (before the code page switch, so a failure there is
  logged too), `int code = 1; try { code = Run(...); return code; } finally { try {
  LogSetup.Close(code); } catch ... }`. `Run` and `RunIn` configure nothing, so the tests and the
  goldens call them in-process as today. `Run` gains `Log.Warn("refused: " +
  LanguageArgument.BadValueMessage(...))` for the bad `--lang`; `RunIn` gains `Log.Info("mode=help")`,
  `Log.Warn("refused the arguments: " + o.Error)`, after the parse `Log.Info("mode=" + o.Mode +
  " input=" + LogText.Str(o.Folder) + " frame=" + ... + " out=" + ... + " threads=" + ... +
  " pixel=" + ... + " east=" + ... + " north=" + ... + " lang=" + (Ui.IsRomanian ? "ro" : "en"))`,
  one `Log.Info("wrote " + p + " (" + LogText.FileKiB(p) + ")")` per file of `--out`, and in the
  catch `Log.Error(e, "run failed: mode=" + o.Mode)` before the stderr line: the stack lives in the
  log, stderr keeps its one line. The logger: `private static readonly Logger Log =
  LogManager.GetLogger("Cli");`.
- `src/LightsCheck.Cli/Options.cs`: `Mode` (L6): an enum `RunMode { Help, Error, Frame,
  Inventory, Series }` and a property set once at the end of `Parse` by the precedence `RunIn`
  uses today (`Help`, then `Error`, then `Frame`, then `Inventory`, else `Series`); `RunIn`
  becomes a `switch` on it. Behaviour unchanged; `OptionsTests` (or wherever the console's
  option tests live) gains one theory that the mode follows the precedence.
- `src/LightsCheck.Wpf/App.xaml.cs`: the constructor calls `LogSetup.Apply("LightsCheck.Wpf",
  args)` in the same guarded `try` **before** `ApplyCommandLine`, hooks
  `DispatcherUnhandledException += (s, e) => LogSetup.Fatal(e.Exception, "unhandled exception on
  the window's thread, the window closes")` (not handled: D7). `Log` is a property fetched on use
  (`static Logger Log => LogManager.GetLogger(Loggers.App)`), never a static field, with the comment
  saying why (D8). `OnStartup` logs `Log.Warn("refused: " + message + "; the window goes on in
  English")` for a bad `--lang` and `Log.Info("language=<en|ro> folder from the command line:
  <folder or "none, the window asks for one">")`. `OnExit` calls `LogSetup.Close(e.ApplicationExitCode)`
  in a guarded `try`.
- `src/LightsCheck.Wpf/MainWindow.xaml.cs` (`Loggers.Window`, a `static readonly` field): `folder
  dialog: chose <folder>` or `cancelled`; `export dialog: chose <folder>` or `cancelled`; `About
  opened`; `stop pressed`.
- `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs` (`Loggers.Window`): `OpenAsync` logs `session
  <folder>: run started pixel=<typed or none> threads=<n or default> east=<arrow> north=<arrow>`
  at Info, then one of: `session <folder>: done in 12.3 s, 87 frames, 2 with errors` (Info),
  `session <folder>: cancelled after 4.1 s` (Info), `session <folder>: refused, the card shows the
  message` with the exception at Warn (its type and stack: the same catch covers a missing folder
  and an IO failure mid-run), and a superseded run's outcome at Debug (`...: ended after a newer
  run started; nothing is shown`). `Export`: one `wrote <path> (<KiB>)` per file at Info, the
  failure at Warn with the exception (the status line keeps its message). `LoadCropsAsync`: `crops
  of frame <n>: <k> cut-outs in <ms>` at Debug, the decoder's failure at Warn with the exception,
  a superseded selection at Debug. `CancelPending`'s `ObjectDisposedException` stays silent (a race
  at the window's close, R-071).
- `src/LightsCheck.Wpf/UserSettings.cs` (`Loggers.Window`): a `Load` or `Save` that fails writes
  one Debug line with the exception; a file that is missing writes nothing.
- `DarkTitleBar` and `AboutWindow.OpenLink`: one Debug line each with the exception; behaviour
  unchanged.

### What is logged where

Every line English, invariant numbers, paths verbatim, composed through `LogText`, logged as a
plain string (so a `{` in a path is never a message template), guarded by `IsDebugEnabled` or
`IsTraceEnabled` when it costs something to build. Loggers are `private static readonly Logger Log
= LogManager.GetLogger(nameof(<Class>))` in the core (`using NLog;`), the two exceptions of D8 aside.

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` | Info, Fatal | the first and last lines; the unhandled exceptions |
| `Program` (`Cli`) | Info, Warn, Error | `mode=... input=...`; the refusals (`--lang`, the arguments) with the message as shown; `wrote <file> (<KiB>)`; `run failed` with the exception |
| `SessionFolder.Open` | Info | `session <root>: lights=<dir> (87 .NEF) biases=30 darks=0` after the lists are built; the refusals are exceptions and are the caller's |
| `ImageLoader.Load` / `LoadInfo` | Info, Debug, Trace | `loaded <LogText.Frame> in <ms>` (Info) or `read the header of ... in <ms>` (Debug) for a metadata read; the FITS keywords (`LogText.FitsKeys`) at Debug and every card at Trace, from `LoadedFrame.Header` and `Cards`, for a FITS only (L2); no line for the fallback dispatch itself |
| `RawReader.Decode` | Debug | `libraw on <path>: visible WxH margins top/left filters=... maximum=... black=... cblack=[...]` |
| `RawReader.FillShootingInfo` catch | Warn | `the raw's lens block could not be read; the lens stays unknown` with the exception |
| `RawReader.Missing` | Error | the libraw load failure with the inner exception, before it is thrown |
| `FrameInfo.Read` catch | Warn | `frame <n> <path> could not be read; its row carries the error` with the exception |
| `InventoryRun` | Info, Debug | `inventory start <folder>`; `frame <n>: <LogText.Info>` per light at Debug; `inventory done lights=87 errors=1 in <ms> (open, read, cadence)` plus `LogText.Cadence` at Debug |
| `FrameRun.Run` / `Measure` | Info, Debug | `frame start <folder> index=<n>`; after the measure, `frame <n> <file>: <LogText.Measurement>` at Debug; `frame done detected=<n> fwhm=<v> elongation=<v> in <ms> (open, load, measure, crop)`; `Measure` alone (called by the series) writes the Debug line and the per-frame Info line of L4 |
| `SeriesRun.Run` | Info, Debug, Warn | `series start <folder> pixel=<um or none> threads=<n> arrows=<east>/<north>`; per frame the L4 lines; a frame that failed: `frame <n> <file> failed; its row carries the error` at Warn with the exception; per registered frame `registered frame <n>: <LogText.Registration>` at Debug; `reference frame=<n>`; `cadence`, `dither`, `pattern`, `pixel size=<um> (<source>) focal=<mm> scale=<arcsec/px>`, `wcs <file>: ...` or `wcs: none`, `read noise: <per position>` or why none, `series: <LogText.Series>`, each at Debug; `series done frames=87 good=80 marginal=4 bad=3 unknown=0 walking=<enum> polar=<enum> in <ms> (open, measure, register, analyze)` |
| `SeriesRun.ReadNoise` catch | Warn | `the read noise could not be measured from <bias1> and <bias2>; the report says none` with the exception |
| `SeriesRun.ReadWcs` catch | Debug | `<file> skipped as a WCS source` with the exception; and `wcs: <file> has no RA axis` at Debug for a file skipped on its header |
| `StarCrops.Build(string, ...)` | Debug | nothing: it is `Analysis/`; the window logs around it |
| `App`, `MainWindow`, `MainViewModel`, `UserSettings` | as above | the start's choices, the dialogs, the session run and its outcome, the files written, the cut-outs, the stored arrows |

Never logged: pixel data, a FITS card at Debug (Trace only), anything from outside the session
folder, nothing through `T()`. `Analysis/`, `Report/`, `Thresholds.cs` and `Imaging/Stats.cs`
hold no logger and never name NLog; the copied files of `Imaging/` (everything but `ImageLoader.cs`
and `RawReader.cs`) stay as they are.

### Requirements

New section `## Diagnostics` in `docs/requirements.md`, between `## Language` and `## Repository
and legal`, the same table header; the block R-070 to R-079 is this area's. All four
`implemented` (`RepoInvariantTests` requires it once the `R###_` tests exist).

- **R-070** The files. Each application writes a plain-text log, UTF-8 without BOM, in a folder
  `log` beside its executable, or in `%LOCALAPPDATA%\LightsCheck\log` when that folder cannot be
  created or written (the run's first line says which). The file names begin with the
  executable's name, `lightscheck` for the console and `LightsCheck.Wpf` for the window: the
  first file is `lightscheck.log`, and when the file being written passes 1 MiB the next one is
  started (`lightscheck_01.log`, `lightscheck_02.log`, ...); at most 5 files per executable are
  kept, the oldest deleted first, and the next process goes on in the newest. One executable's
  rolling never deletes the other's files when both share a folder. Example: 8 MiB of lines
  through the console's configuration leave `lightscheck_03.log` to `lightscheck_07.log`, none
  above 1 MiB plus one line, the last line in `lightscheck_07.log`, where the next run writes too.
- **R-071** What is written. The log holds what a bug report needs, in English with invariant
  numbers whatever `--lang` says (a verdict or a class by its key, `Bad`, `Suspect`; an
  exception's or a refusal's message as the user was shown it), and adds nothing to stdout or
  stderr: a first line per process with the executable, its version, the arguments verbatim, the
  working folder, the log folder, the level, the OS, the .NET runtime, the culture, the time zone
  and the process id; the session's folders and counts; one line per file read (path, size,
  reader, dimensions, BITPIX, planes, CFA pattern, black and white levels, camera, focal length,
  f-number, exposure, ISO, time, lens); one line when a run starts, with its inputs and options,
  and one when it ends, with its outcome and the milliseconds of each stage; one line per frame
  measured, with its time; the figures behind every verdict (the frame measurements, the
  registration, the cadence, the dithering, the pattern, the series analysis, the pixel scale and
  its source, the WCS); every file written, with its size; every exception with its type, message
  and stack trace, those the program recovers from included, except a cancellation the user
  asked for (a stop, another folder, the window closing), a race at the window's close and the
  log's own probes, whose outcome is their own line; the window's session runs, dialogs and
  buttons; and a last line with the exit code and the elapsed time. Example, measured on the Veil
  session of 2026-10-06 (to be filled in at wrap-up with the real lines): `lightscheck 1.0.0
  starting; ...`, `loaded ...\DSC_0001.NEF (23.9 MiB) reader=raw 6016x4016 ...`, `frame 1/87
  DSC_0001.NEF detected=12308 fwhm=3.85 elongation=1.68 class=... in 1830 ms`, `series done
  frames=87 ... in 142 s (...)`, `exit 0 after 143.2 s`.
- **R-072** The level. `Debug` unless the environment variable `LIGHTSCHECK_LOG` names another,
  in any case: `off`, `fatal`, `error`, `warn`, `info`, `debug` or `trace`; any other value means
  `Debug`. `Trace` adds every FITS header card; `off` creates neither the folder nor a file.
  Example: with `LIGHTSCHECK_LOG=off` a run leaves the folder beside the executable as it was,
  and with `LIGHTSCHECK_LOG=trace` the log of a FITS run holds its `BITPIX` card.
- **R-073** Two processes. Two processes of the same executable writing the same file at full
  speed both keep every line, each whole and once, across the 1 MiB roll too: the log is written
  through `NLog.Targets.AtomicFile`, which leaves the operating system to place every write at
  the end of the file (ADR 0029, FlatCheck's measurement of 2026-10-27). Example: two writers of
  4000 lines each, released together, leave 8000 whole lines in two files, interleaved both ways
  round.

### ADR, design note, CHANGELOG, README

- `docs/decisions/0029-log-with-nlog-as-flatcheck-does-at-the-seams.md` (check `main`'s latest
  number again before the merge; renumber on the branch on a `DUPLICATE`). Context: what a
  report carries today, the owner's ask of 2026-10-30, FlatCheck's precedent and measurements.
  Decision: D1 to D8 as taken, L1 to L9 as decided, the values (the variable, the folders, the
  counts, the layout, the loggers). Alternatives rejected: `Microsoft.Extensions.Logging` with a
  provider; an `NLog.config`; logging inside `Analysis/` and `Report/`; a `--log` option; taking
  FlatCheck's `FitsReader` hunk (L2); the plain file target (FlatCheck's numbers, not measured
  again); one file per process; a line per progress tick. Consequences: a third component with
  two packages; the log is the first thing to ask a reporter for; every test runs every log line
  on its path; the other tools of the suite (DarksCheck, the launcher) can take `Logging/` the
  same way.
- `docs/design/logging.md`: `**Covers:** src/LightsCheck.Core/Logging/LogSetup.cs,
  src/LightsCheck.Core/Logging/LogText.cs, src/LightsCheck.Core/Logging/RunClock.cs,
  src/LightsCheck.Wpf/Loggers.cs, tests/LightsCheck.Tests/LogCapture.cs,
  tests/LightsCheck.Tests/LoggingTests.cs, tests/LightsCheck.Wpf.Tests/WindowLoggingTests.cs`
  and, by name, the log lines of the seams. Sections as FlatCheck's note: how the parts fit (the
  four steps of `Apply`), the files as measured by the rolling test (the file list pasted), the
  line, a run's lines, the seam table above kept in step with the code, reading a log, in the
  tests. A row in `docs/design/README.md`'s index. `session-and-console.md` (covers `Program.cs`,
  `Options.cs`, `SessionFolder.cs`, `InventoryRun.cs`) and `window.md` (covers `src/LightsCheck.Wpf/`)
  each get one sentence saying their files' log lines are described in `logging.md`;
  `imaging.md` says which two files of the copied folder log and that `FitsReader` does not (L2);
  `frame-analysis.md` and `series-analysis.md` name the runs' clock stages in one sentence each.
  `scripts/check-design.ps1 -Since main` must pass.
- `CHANGELOG.md` under `[Unreleased]` / `### Added`: the log (R-070 to R-073, ADR 0029), where it
  is, what it holds, what to send with a bug report, the variable; under `### Internal`: NLog
  6.2.2 and its atomic-file target as the third component, the core logging at its seams and never
  in `Analysis/` or `Report/`, every test running every log line.
- `README.md`: a `## Logs` section after "The window" and before "Building", FlatCheck's text
  adapted (where the log is, the fallback, the rolling and the 5 MiB, what is in it and what is
  not, "attach the whole `log` folder to a bug report", `LIGHTSCHECK_LOG` with `setx` for the
  window started from a shortcut or the launcher and `set` for one cmd window); the "Licence"
  section's sentence on the notices mentions NLog beside the readers.

## Steps, in order

1. **Worktree and branch.** From `E:\Photografie\Code\LightsCheck` on `main`, clean:
   `git worktree add .claude/worktrees/Add-a-diagnostic-log-with-NLog -b claude/Add-a-diagnostic-log-with-NLog main`.
   Work, build and test only in `<worktree>\LightsCheck`. Copy this plan from the main checkout
   (`E:\Photografie\Code\LightsCheck\docs\plans\2026-10-30-add-a-diagnostic-log-with-nlog.md`,
   untracked there) to the same path in the worktree and commit it first; before the merge the
   owner deletes the untracked copy in the main checkout, or the merge refuses to overwrite it.
2. **Requirements.** R-070 to R-073 in the new `## Diagnostics` section, `implemented`.
3. **The packages.** `Directory.Packages.props`, the core csproj, `dotnet restore
   LightsCheck.sln --force-evaluate`, `ThirdParty.cs`, `THIRD-PARTY-NOTICES.md`, the
   `ThirdPartyTests` count 2 to 3. Build green and `ThirdPartyTests` green before any logging
   code. Commit: `Take NLog 6.2.2 and its atomic-file target as the third component`.
4. **The failing tests** of `LoggingTests` that need only `LogSetup`, `LogText` and `RunClock`
   (the R-070, R-072 and R-073 tests, the first-line test, the elapsed-time forms), then the three
   files until they pass. `LogCapture` comes with them. Commit.
5. **The applications.** `Program.cs`, `Options.cs` (the mode), `App.xaml.cs`, `Loggers.cs`,
   `MainWindow.xaml.cs`, `MainViewModel.cs`, `UserSettings.cs`, the two small catches. Run the
   console from the worktree on a fixture session (`dotnet run --project src/LightsCheck.Cli --
   <folder>`) and read `src/LightsCheck.Cli/bin/Debug/net8.0/log/lightscheck.log`; run the window
   on the same folder and read `LightsCheck.Wpf.log`. Commit.
6. **The seams**, with their tests: `ImageLoader`, `RawReader`, `FrameInfo`, `SessionFolder`,
   `InventoryRun`, `FrameRun`, `SeriesRun`, in that order. Keep every `catch` as it is; add the
   log line only. The repository invariants (below) last. Commit.
7. **Docs.** ADR 0029, `docs/design/logging.md` and the index row, the sentences in the four
   existing notes, `CHANGELOG.md`, `README.md`. Commit.
8. **Measure** (L8) on the Veil session, paste the times into this plan's outcome section.
9. **Gate and wrap-up.** `dotnet build LightsCheck.sln -c Release --no-incremental -warnaserror
   --no-restore` first (the gate's incremental build has hidden a CS0419 before), then
   `scripts/verify.ps1` green, `scripts/check-design.ps1 -Since main`; read the whole diff; the
   goldens must not move (say so); `/validate` is not needed, no formula moved (say so); offer the
   owner a subagent review of the branch before the merge is proposed (the owner has asked for one
   on every task since 2026-10-27), then a second review of the fix commit; propose the merge.

## Tests

`tests/LightsCheck.Tests/LogCapture.cs` (new, FlatCheck's in LightsCheck's style, linked into
`LightsCheck.Wpf.Tests` by a `Compile Include` beside `SessionFixture.cs`): a `[ModuleInitializer]`
sets `LogManager.Configuration` once per test assembly to one `CaptureTarget` at Trace that
renders every event through `LogSetup.LineLayout`, so every test runs every log line on its path
and a line that throws fails a test, not a user's run; `LogCapture.Start()` opens an `AsyncLocal`
scope (the same mechanism as `Ui.Use`) that sees only its own call tree, one at a time per tree;
`Events` (`Captured(Level, Logger, Message, Line, Exception)`), `Messages`, `Text`. The tests never
call `LogSetup.Apply`; the file tests build the applications' configuration with `LogSetup.Build`
into a private `LogFactory`, which stands for one process, and never touch the assembly's
configuration. The test projects run in parallel: the `AsyncLocal` scope is what keeps the
captures apart.

`tests/LightsCheck.Tests/LoggingTests.cs` (new; FlatCheck's `LoggingTests` is the model, read it):

- `R070_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName`: `ResolveFolder(ws.Dir,
  fallback, "lightscheck")` gives `ws.Dir\log`, created, empty (the probe is gone), no fallback
  folder; one line through `Build` leaves `lightscheck.log` alone, no BOM, the line matching the
  layout and ending with `[pid:thread] Test hello`.
- `R070_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback`: a file named `log` where
  the folder should be; the result is `<fallbackRoot>\LightsCheck\log`, created; `StartupLine`
  with `fellBack` says `(fallback`.
- `R070_TheLogRollsAt1MiBAndKeepsAtMost5Files`: 8 MiB of 4 KB lines through one factory; exactly
  the five names `lightscheck_03.log` to `_07`, each at most `FileSizeLimit + 4200` bytes, the last
  line in `_07`; a second factory writes 1000 bytes more: the same five names, the newest grown,
  its last line the new one. The file list goes to the test output and into `logging.md`.
- `R070_OneExecutablesRollingLeavesTheOthersFilesAlone`: `LightsCheck.Wpf` rolled once, then
  `lightscheck` rolled eight times in the same folder; the window's two files are untouched.
- `R070_AReadOnlyNewestLogFileSendsTheLogToTheFallback`; `R070_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe`
  (a second factory holds `lightscheck.log` open; no fallback).
- `R070_TheFilesBeginWithTheExecutablesName` (theory): `C:/Tools/lightscheck.exe` gives
  `lightscheck`; `.../dotnet.exe` and `/usr/share/dotnet/dotnet` give the entry name; null gives
  the entry name. Forward slashes, so the test is portable.
- `R071_TheFirstLineCarriesTheFacts`: `StartupLine` starts with `lightscheck <VersionInfo.Value>
  starting; exe=`, holds the arguments quoted, `cwd=`, `log=`, `level=Debug`, `os=`, `cpus=`,
  `memory=`, `runtime=`, `culture=`, `tz=`, `(UTC`, ends with `pid=<pid>`, no `fallback`.
- `R071_ASeriesRunLogsTheSessionEveryFrameTheFiguresAndTheTimings`: a `SessionFixture` star series
  of eight frames with two biases under `LogCapture`, through `Cli.Run(root, "--threads", "2")`;
  assert `Cli mode=Series input=<root>`, `SessionFolder session ...: lights=... (8 .fit)`, eight
  `ImageLoader loaded ...` lines, eight `SeriesRun frame <n>/8 ... in \d+ ms` lines at Info,
  `registered frame`, `dither`, `series:`, and `series done frames=8 ... in \d+ ms \(open \d+ ms,
  measure \d+ ms, register \d+ ms, analyze \d+ ms\)`; every line matches the layout's prefix.
- `R071_AnInventoryAndAFrameRunHaveTheirStartAndDoneLines`: `--inventory` and `--frame 1` on the
  same fixture; `inventory done lights=8 errors=0 in ... (open ..., read ..., cadence ...)` and
  `frame done ... (open ..., load ..., measure ..., crop ...)`.
- `R071_AFrameThatCannotBeReadIsAWarnLineWithItsException_AndTheRowKeepsItsMessage`: a fixture
  whose third light is a text file named `.fit`; the series run's log has one Warn with an
  `InvalidDataException` and the stack (`   at LightsCheck.Core.FitsReader.`); stdout's row holds
  the message as the golden of R-003 expects; the console still exits 1.
- `R071_AFailedRunLogsTheExceptionWithItsStackAndStderrKeepsOneLine`: `Cli.Run` on a missing
  folder: exit 1, stderr one `ERROR:` line, one Error event whose `Line` holds
  `System.IO.DirectoryNotFoundException` and `   at `; the run's `ended without an outcome` line
  is present with its start line.
- `R071_ARefusedFolderLeavesTheRunsStartLine`: `--inventory` on an empty `lights/`: the
  `inventory start` line and `ended without an outcome` with the `open` stage reached or none.
- `R071_TheSameCrashSeenByTwoHooksIsWrittenOnce`: three `LogSetup.Fatal` calls as FlatCheck's test.
- `R071_TheLogIsTheSameInBothLanguages` (theory over `series`, `inventory`, `frame`, and the
  missing-folder refusal): the messages after normalising times and `lang=` are equal under
  `--lang en` and `--lang ro`, the refusal's message excluded (it is the user's, D6).
- `R071_TheConfigurationHasOneFileTargetAndNoConsole`: `Build` has one `AtomicFileTarget` with
  the size, the count, `KeepFileOpen`, `ConcurrentWrites`; the rule takes Debug and not Trace.
- `R071_EveryFileWrittenIsOneLineWithItsSize`: `--out` on the fixture: three `Cli wrote ...
  (<n>.<d> KiB)` lines.
- `TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal`: `LogText.Seconds` and `Ms`.
- `R072_TheLevelComesFromTheEnvironmentVariable` (theory): null, empty, nonsense give Debug; `OFF`,
  ` fatal `, `Error`, `warn`, `info`, `debug`, `TRACE` give their level.
- `R072_OffCreatesNeitherTheFolderNorAFile`: `Plan(..., "off")` has a null folder and leaves the
  workspace empty; `Build(..., Off)` has no target; `Plan(..., "trace")` gives Trace and the folder.
- `R072_TraceHoldsEveryFitsHeaderCard`: `ImageLoader.Load` on a fixture FITS under a capture: Trace
  events with `card BITPIX  =` and `card NAXIS1  =`, and no Debug event containing `card `.
- `R073_TwoProcessesWritingAtFullSpeedKeepEveryLine`: FlatCheck's test as it is (two factories on
  `LightsCheck.Wpf`, two threads released by a `Barrier`, 4000 lines each crossing the roll;
  every line whole and once; the writers interleave both ways round). Strict: it fails on the
  plain target.
- Repository invariants (plain `[Fact]`s, no R number): `OnlyTheTwoEntryPointsConfigureTheLog`
  (`LogSetup.Apply(` and `.Close(` appear in exactly `src/LightsCheck.Cli/Program.cs` and
  `src/LightsCheck.Wpf/App.xaml.cs`; `LogManager.Configuration =` and `LogManager.Setup(` only in
  `LogSetup.cs` and `LogCapture.cs`); `NoLogLineGoesThroughTheTranslation` (no `Log.<Level>(` call,
  up to its `;`, contains `T(`; every logger is declared one way, no inline `GetLogger(` and no
  `NLog.Logger` qualification; the sources are `src/` without `bin/` and `obj/`, filtered on the
  relative path); `NothingThatComputesOrWordsTheReportLogs` (`src/LightsCheck.Core/Analysis/`,
  `Report/`, `Thresholds.cs`, `Imaging/Stats.cs`, and every file of `Imaging/` but `ImageLoader.cs`
  and `RawReader.cs`, match none of `\bNLog\b|\bLogger\b|\bLogManager\b`).

`tests/LightsCheck.Wpf.Tests/WindowLoggingTests.cs` (new, in the STA collection as
`MainViewModelTests` is):

- `R071_ASessionRunLogsItsStartAndItsOutcome`: `OpenAsync` on an eight-frame fixture; `Window
  session ...: run started ...` and `session ...: done in \d+(\.\d)? s, 8 frames, 0 with errors`.
- `R071_ARefusedFolderIsAWarnLineWithTheExceptionAndTheCardKeepsTheMessage`: `OpenAsync` on a
  missing folder; `HasProblem`, `ProblemText` as today; one Warn whose exception is a
  `DirectoryNotFoundException` and whose `Line` holds `   at `.
- `R071_ACancelledRunSaysSoAndLogsNoException`: start, `CancelPending`, await; a `cancelled after`
  line, no Warn or Error.
- `R071_EveryFileTheWindowWritesIsOneLineWithItsSize`: `Export` after a run; three `wrote` lines.
- `R071_ASupersededRunsOutcomeIsKeptAtDebugThoughTheScreenShowsNothing`: two `OpenAsync` calls
  (the pattern of `MainViewModelTests`); the first's outcome at Debug.

Golden files: no diff (nothing reaches stdout or stderr). `ThirdPartyTests`: the count is the one
intended change. `AboutTests`: the list comparison takes NLog by itself; the About smoke screen
changes, which is no golden.

Analyzer notes for the implementer: the project is nullable-enabled with warnings as errors; a
guard around `Apply` or `Close` is `catch (Exception e) when (e is not OperationCanceledException)`
(the project's own pattern, and the one CA1031 accepts); every new type and method gets its XML
documentation comment (the kit's rule); every new file starts with the GPL header
(`LicenseHeaderTests`); `IsDebugEnabled` guards wrap the lines that build a `LogText` of a whole
record; string concatenation with `LogText` keeps every number invariant, so CA1305 does not fire.

## Verification

- `scripts/verify.ps1` green in the worktree after each group; the clean build of step 9 before
  the last one; `scripts/check-design.ps1 -Since main`.
- By hand, pasted at wrap-up (`E:\Photografie\Astro\Veil\2026-10-06-3h`, the session of V-001, or
  any session under `LIGHTSCHECK_TESTDATA_LARGE`): the console on the session (the first line, the
  session line, one `loaded` line, one `frame n/87` line, `series done`, `exit 0`) and on a folder
  that is not a session (the `run failed` Error with its stack, `ended without an outcome`); the
  window opened on the session (the `App` lines, `session ...: run started`, `done`, `exit 0`);
  two windows started at once on it, both pids in one `LightsCheck.Wpf.log`; `LIGHTSCHECK_LOG=off`
  leaves `log/` unchanged; `LIGHTSCHECK_LOG=trace` on a FITS session shows the cards.
- The measurement of L8: three series runs with the log off and three at Debug, wall time each,
  pasted; the log file's size for one run at Debug.
- The rolling test's measured file list pasted into `docs/design/logging.md`.
- `dotnet publish` of the console as `kit.json` publishes it (single file, self-contained), the
  executable copied to a scratch folder and run there: `log\lightscheck.log` appears beside it
  (`AppContext.BaseDirectory` of a single-file executable is its folder; verify, do not assume).

## Out of scope

- DarksCheck, CopyWcs and the AstroDarkroom launcher: `Logging/` is written so each can take it
  under its own names; a task of its own per tool.
- Sending or zipping the log from the window: the README asks the user to attach the folder.
- Any change to what the console prints, to the window's cards, or to the crash dialogs (D7).
- Taking FlatCheck's `FitsReader` log lines into the copy (L2).

## Files

Code: new `src/LightsCheck.Core/Logging/{LogSetup,LogText,RunClock}.cs`, `src/LightsCheck.Wpf/Loggers.cs`;
edited `src/LightsCheck.Core/LightsCheck.Core.csproj`, `ThirdParty.cs`, `Imaging/{ImageLoader,RawReader}.cs`,
`Session/{SessionFolder,FrameInfo}.cs`, `Runs/{InventoryRun,FrameRun,SeriesRun}.cs`,
`src/LightsCheck.Cli/{Program,Options}.cs`, `src/LightsCheck.Wpf/{App.xaml.cs,MainWindow.xaml.cs,UserSettings.cs}`,
`ViewModels/MainViewModel.cs`, `Presentation/DarkTitleBar.cs`, `Views/AboutWindow.xaml.cs`,
`Directory.Packages.props`, the five `packages.lock.json`.
Tests: new `tests/LightsCheck.Tests/{LogCapture,LoggingTests}.cs`,
`tests/LightsCheck.Wpf.Tests/WindowLoggingTests.cs`; edited `ThirdPartyTests.cs`, the console's
option tests, `tests/LightsCheck.Wpf.Tests/LightsCheck.Wpf.Tests.csproj` (the link).
Docs: `docs/requirements.md` (R-070 to R-073), new `docs/decisions/0029-...md`, new
`docs/design/logging.md`, `docs/design/{README,session-and-console,window,imaging,frame-analysis,series-analysis}.md`,
`CHANGELOG.md`, `README.md`, `THIRD-PARTY-NOTICES.md`, this plan under `docs/plans/`.

## Outcome and deviations (2026-10-30)

Implemented as planned, on `claude/Log-LightsCheck-with-NLog`: the packages with the requirements
as `accepted` (116678f); the code and the tests, the requirements `implemented` (5fa4362); the
docs (f10093f); `Logging/` named in `CLAUDE.md`'s layout (345ec8a); the gate green each time. ADR
0029 and `docs/design/logging.md` hold the result. What differs from the plan, and why:

- **The branch and the worktree are `Log-LightsCheck-with-NLog`.** DarksCheck's plan of the same
  day names `claude/Add-a-diagnostic-log-with-NLog` too, and branch names are shared by the whole
  repository. The plan was moved into the worktree, not copied, so the main checkout holds no
  untracked copy to delete before the merge.
- **The ADR's file is `0029-a-diagnostic-log-with-nlog-as-flatchecks-written-at-the-seams.md`.**
- **The series has seven stages**, `open`, `measure`, `register`, `dither`, `pattern`, `noise`,
  `analyze`, where the plan's examples named five; R-071's example says seven.
- **No line in `RawReader.Missing`.** Every caller logs the exception it throws, the libraw
  failure inside as the inner exception; a line there would write the stack twice. The ADR lists
  it among the alternatives.
- **No per-frame line of the inventory's own.** `ImageLoader`'s `read the header of` line is that
  line; a frame that cannot be read is `FrameInfo`'s Warn with the exception.
- **`Options.Mode` is a computed property**, not a value set at the end of `Parse`: the same
  precedence in one place, with nothing to keep in step.
- **The window writes a bug's exception through an exception filter** (`LogUnhandled`), which the
  plan did not name: `OpenAsync` runs in a task nobody awaits, so an exception outside the three
  types the card shows would surface only when the task is collected, if ever. The filter returns
  false, so nothing else changes.
- **The window's done line says `done in 12.3 s; the tabs show the result`**, not the frame counts
  the plan's test named: counting frames in the view-model would be a figure computed outside the
  core, and the core's `series done` line has the counts.
- **The stopped line says `stopped after`**, not `cancelled after`: the button is Stop.
- **`DarkTitleBar`'s two empty catches became one filtered catch** with FlatCheck's Debug line; the
  About window's two the same way.
- **The README's settings sentence was corrected**: `%LOCALAPPDATA%\LightsCheck` holds the log too
  when the folder beside the program cannot be written.
- **One more test than the plan**, `R071_TheArgumentsTheConsoleRefusesAreLoggedAsTheUserSawThem`.
- **The console's `mode=` line carries the mode, the input and the language only**, not the
  plan's `frame=`, `out=`, `threads=`, `pixel=`, `east=` and `north=`: FlatCheck dropped the same
  options from its line (its ADR 0072), since the first line has the arguments verbatim and each
  run's start line its options.
- **Measured (L8)**, 2026-10-30, the Release console on `E:\Photografie\Astro\Veil\2026-10-06-3h`
  (87 NEF lights, 30 biases, 4 threads), runs alternated, the owner using the PC meanwhile:

  | run | `LIGHTSCHECK_LOG` | wall time | log grew by |
  | --- | --- | --- | --- |
  | 1 | off | 154.1 s | 0 bytes, no folder |
  | 2 | unset (Debug) | 173.5 s | 172720 bytes |
  | 3 | off | 133.0 s | 0 |
  | 4 | unset (Debug) | 163.7 s | 173309 bytes |
  | 5 | off | 111.4 s | 0 |
  | 6 | unset (Debug) | 96.7 s | 173257 bytes |

  The first four runs suggested a cost at Debug, so it was measured where it would arise: the time
  between libraw's line and the loader's line of each file, a little decode work and one write,
  is 4.9 ms at the median and 1.8 s summed over the three Debug runs; and a scratch program
  writing through the same configuration measured 0.079 ms a line (600 lines, one writer) and
  0.032 (2400 lines, four writers) on the hard disk `E:`, 0.032 and 0.033 on the SSD `C:`. Run 6,
  at Debug, was then the fastest of the six. The log costs under 0.1 s of a run; the spread is the
  disk cache filling and the PC in use.
- **By hand**, 2026-10-30, the same Release build:
  - the console on the Veil session: 540 lines, from `Process lightscheck 1.0.0 starting; ...
    cpus=8; memory=31.9 GiB; runtime=.NET 8.0.33 X64; culture=invariant/invariant; tz=GTB
    Standard Time (UTC+02:00); pid=9700`, `SessionFolder session root=E:\Photografie\Astro\Veil\2026-10-06-3h
    lights=...\lights (87 frames, .NEF) biases=...\biases (30) darks=none (0)`, `ImageLoader loaded
    ...\DSC_0001.NEF (21.6 MiB) reader=raw 6016x4016 ... scale=4095/150 (libraw (exact))
    camera=Nikon D3300 focal=135 fnumber=2.8 exposure=120.2 iso=1600 time=2026-10-06 23:35:03
    pixel=none focal35=202 lens=74/Nikon F/none ...`, `SeriesRun frame 1/87 DSC_0001.NEF
    detected=12308 fwhm=3.85188 elongation=1.6833 in 6790 ms` (V-002's figures), `SeriesRun
    classified frame 1: Bad flags=[Trailed value 1.6833 threshold 1.6 angle 83 on Ra]`, `SeriesRun
    pixel size 3.9976 um source=Raw focal=135 mm scale=6.10789 arcsec/px`, to `series done frames=87
    measured=87 errors=0 good=48 marginal=17 bad=22 unknown=0 walking=Insufficient polar=Unknown in
    171549 ms (open 3 ms, measure 167094 ms, register 121 ms, dither 17 ms, pattern 1639 ms, noise
    2640 ms, analyze 19 ms)` and `Process exit 0 after 171.8 s`; no Warn, no Error;
  - the console on a folder that does not exist: stderr `ERROR: cannot find the folder ...`, exit 1,
    and in the log `series start`, `series ended without an outcome after 0 ms`, `ERROR ... Cli run
    failed: mode=Series` with `System.IO.DirectoryNotFoundException` and its stack through
    `SessionFolder.Open`, `SeriesRun.Run` and `Program.RunIn`, then `Process exit 1 after 0.2 s`;
  - `LIGHTSCHECK_LOG=off`: runs 1, 3 and 5 wrote nothing, and run 1 created no `log` folder (it
    appeared with run 2); `LIGHTSCHECK_LOG=trace` with `--frame 1` on the session's `process/`
    (Siril's FITS): the 26 cards of `bias_00001.fit` at Trace, its keys at Debug
    (`BAYERPAT=RGGB ROWORDER=BOTTOM-UP` beside `cfa=GBRG (BAYERPAT)` in the loaded line);
  - the console published as `kit.json` publishes it (single file, self-contained), the one
    `lightscheck.exe` copied alone into a scratch folder and run from another working folder on
    the session's `--inventory`: `log\lightscheck.log` appeared beside the executable, `inventory
    done lights=87 errors=0 in 214 ms (open 2 ms, read 211 ms, cadence 1 ms)`; the lock files did
    not move;
  - **not done**: the window by hand (its start and exit lines, two windows into one file). Both
    times it was checked, the owner was at the PC (Firefox, then Claude in the foreground, under 5 s
    since the last input), and a window started over their work is an action on their desktop.
    The view-model's lines are tested in process by `WindowLoggingTests`, and the two-processes
    file on the window's build of the atomic target since the review (below). No test runs
    `App`'s constructor, `OnStartup` and `OnExit`, `MainWindow`'s handlers, `MainViewModel`'s
    filter for an unhandled exception, the title bar's and the About window's catches, or the
    wiring of the crash hooks (corrected after the review: this line first named `App`'s
    constructor and `OnExit` alone).
- **The review of 2026-10-30** (a subagent on `git diff main...HEAD`) reported ten findings; each
  was checked in the code and held; eight were applied, one was kept with its reason, and one is
  the owner's to judge:
  - a bug: libraw's Debug line read the CFA order of every raw, and a raw without a Bayer mosaic
    (X-Trans, mono) has none, so at the default level every such frame became unreadable and
    `--frame` crashed; the line is now `RawReader.FiguresLine`, which writes `none`, and
    `R071_LibrawsLineNamesNoCfaForARawWithoutABayerMosaic` failed with a `NullReferenceException`
    before the fix;
  - the WCS candidates skipped on their header left no line, and the file's line said
    `framesFlipped=yes` before the run set the flip: each skip now says why, the file's line names
    the file alone, the run's `wcs:` line has the values
    (`R071_ThePlateSolveCandidatesSayWhichWasReadAndWhyTheOthersWereSkipped`, red before);
  - an export that failed part-way named none of the files it had written: `WriteAll` now tells a
    callback each file as it is written (`R071_AnExportThatFailsPartWayNamesTheFilesItWrote`, red
    before); the window's `settings.json` written and the console's code-page catches now have
    their lines;
  - what only the entry points do had no test: two tests run the real console, its first and last
    lines and its level from `LIGHTSCHECK_LOG` written out by name; R-073 also runs on the
    window's `net8.0-windows` build of the atomic target, a binary of its own;
  - a frame the series cannot measure wrote two stacks (the measurement's, then the metadata
    read's): one line now, which says whether the metadata could be read; and an unhandled
    exception of the window is named, not written again, when its task is collected
    (`LogSetup.Wrote`, `LogSetup.Unobserved`);
  - the translation check now scans the run clocks' texts too, and the window's lines are compared
    in both languages at run time;
  - docs: Trace adds the cards of the frames read whole, not of the inventory's header reads
    (R-072, README); the `App` row of the seam table, the crash wording, the byte counts, which
    move with the digits of the ids, the title bar's summary, the `mode=` line above;
  - left to the owner: CLAUDE.md's rule "Computed only in the core" against the applications'
    own timings and lines; ADR 0029 now records the reading FlatCheck already lives by, that a
    log line is no figure of the analysis, and CLAUDE.md is unchanged;
  - kept as it is: the test that starts the real console writes into the test build folder's
    `log`, where every real process of the tests logs by design.
