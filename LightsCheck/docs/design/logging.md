# The diagnostic log

**Covers:** `src/LightsCheck.Core/Logging/LogSetup.cs`, `src/LightsCheck.Core/Logging/LogText.cs`, `src/LightsCheck.Core/Logging/RunClock.cs`, `src/LightsCheck.Wpf/Loggers.cs`, `tests/LightsCheck.Tests/LogCapture.cs`, `tests/LightsCheck.Tests/LogFiles.cs`, `tests/LightsCheck.Tests/LoggingTests.cs`, `tests/LightsCheck.Wpf.Tests/WindowLoggingTests.cs`, and the log lines of the seams the table below lists.

The behaviour is R-070 to R-073 and the decisions are ADR 0029, which takes FlatCheck's (its ADRs
0070 and 0072); this note is the mechanics. FlatCheck's `docs/design/logging.md` describes the
same parts under the same names, and what it measured about NLog (the rolling, the targets, the
stray `NLog.config`) holds here: the same packages, the same settings.

## How the parts fit

`LogSetup.Apply` is the first call of `Program.Main` and of `App`'s constructor, each in a `try`
that swallows anything it throws, because the log never stops the tool; `LogSetup.Close` is the
last of `Main` (in its `finally`, after the code page is put back) and of `App.OnExit`, guarded
the same way. `Apply` runs once per process:

1. `Plan` decides the place without configuring anything: the level from `LIGHTSCHECK_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the
   entry name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable, a probe file in it,
   `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, and opens for append the file
   NLog will write next when there is one (`NewestLog`: the highest-numbered `<name>_NN.log`,
   else `<name>.log`), with the share mode that lets another process of the same executable
   hold it open; when any of these fails it does the same under `%LOCALAPPDATA%\LightsCheck\log`.
2. `Build` makes the configuration, one `AtomicFileTarget` and one rule from the level up, and
   `Apply` sets it before any logger exists: `LogSetup`'s own logger, `Program`'s and `App`'s
   are properties fetched on use, not static fields, because those three types run before the
   configuration is set and a logger created by their type initialiser could make NLog load an
   `NLog.config` it finds beside the executable. `Off` gives a configuration without targets,
   and `Apply` returns there.
3. The hooks: `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`; the
   window adds its `DispatcherUnhandledException` in `App`'s constructor. `LogSetup.Fatal`
   writes the exception, and names the same exception seen a second time instead of writing it
   again (the dispatcher's hook, then the process's). `LogSetup.Unobserved` does the same for a
   task nobody awaited: an exception a line already wrote where it was met, and recorded with
   `LogSetup.Wrote` in a weak table, is named, any other written whole. The window's unhandled
   exception is such a one: written by `MainViewModel` before it leaves `OpenAsync` in a task the
   window never awaits.
4. The first line, `StartupLine`, at Info.

`Program.Run`, the console the tests call in-process, configures nothing; neither does the window
tests' fixture, which builds a plain `Application`, not `App`.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `lightscheck.log`, then past `FileSizeLimit`
`lightscheck_01.log`, `_02`, and so on (`ArchiveSuffixFormat`'s default `_{0:00}`). A file ends
one line past the limit. `R070_TheLogRollsAt1MiBAndKeepsAtMost5Files` writes 8 MiB of 4 KB lines
through the console's configuration; measured on 2026-10-30 it leaves:

| file | bytes | after a second process |
| --- | --- | --- |
| `lightscheck_03.log` to `lightscheck_06.log` | 1049286 each | the same |
| `lightscheck_07.log` | 980211, the last line in it | 984336 |

The second process appends to `_07`, which grows, and the five names stay. The byte counts move
by a few hundred from one run to the next: every line carries the process and thread ids, whose
digits vary (the review's run of the same test left 1049028 and 979969 bytes).
The window's files, rolled once, survive the console's rolls in the same folder
(`R070_OneExecutablesRollingLeavesTheOthersFilesAlone`): NLog matches its own name and number only.

In one process two factories on one file stand for two processes, each on a thread of its own
released by a `Barrier` so they do overlap: `R073_TwoProcessesWritingAtFullSpeedKeepEveryLine`
writes 4000 lines from each, crossing the 1 MiB roll, and asks for every line, whole, once, and
for lines of both writers interleaved both ways round; measured on 2026-10-30 it left
`LightsCheck.Wpf.log` (1048915 bytes) and `LightsCheck.Wpf_01.log` (646953 bytes) with 4000 whole
lines of each writer and none broken. The console's tests load the `net8.0` build of the atomic
target, the window's tests its `net8.0-windows` build, a binary of its own, which the window
ships: `R073_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows` runs the same check
(`LogFiles.TwoWritersKeepEveryLine`) on it.

A line costs the same on the hard disk the sessions live on as on the SSD: 0.03 to 0.08 ms,
measured on 2026-10-30 through the same configuration, one writer and four at once. `AutoFlush`
hands each line to the operating system and does not wait for the disk, so a series' 540 lines on
the Veil session cost under 0.1 s (ADR 0029 has the run times).

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the
level padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant numbers
with six significant digits, `none` for a missing value, sizes in KiB and MiB, an elapsed time as
`Ms`, `1141 ms`, or `Seconds`, `12.9 s`, an enum by its name) and logged as plain strings, so
NLog never parses them as message templates and a `{` in a path is harmless. A line whose message
costs something to build is guarded by `IsDebugEnabled` or `IsTraceEnabled`.

A line never throws: a value it may not have is written `none`. A line that threw would change
what a run does at one level only, at the default one among them: libraw's line once read the
CFA order of every raw, and a raw without a Bayer mosaic (an X-Trans RAF, a monochrome DNG) has
none, so every such frame became unreadable at Debug and not at Info. `RawReader.FiguresLine`
composes that line from plain values, so a test reaches it without such a raw.

`LogText` never writes a text the report words in the run's language: a FITS frame's scale is
`none` in its `loaded` line (`FrameRun` assumes it from BITPIX, in the run's language) and its
white and black alone in the measurement line; the RA axis's source is left out of the series
line; an unreadable frame's flag has no detail in the classified line, its own Warn line carries
the exception. A raw's scale source, `libraw (exact)`, and a CFA order's, `libraw` or
`BAYERPAT`, are not translated, so they stay.

Every logger is a `Logger` named `Log`, taken from `LogManager.GetLogger` with `using NLog`
(`NoLogLineGoesThroughTheTranslation` checks it, so its scan of the calls sees them all): a
`static readonly` field everywhere but in `LogSetup`, `Program` and `App`, where it is a property
fetched on use (step 2 above). The names: the class name in the core (`ImageLoader`, `RawReader`,
`SessionFolder`, `FrameInfo`, `InventoryRun`, `FrameRun`, `SeriesRun`), `Process` for the first
and last lines and the crashes, `Cli` for the console, and the window's two from `Loggers`: `App`
(the start's choices) and `Window` (the main window, the view-model, the stored setting, the
title bar, the About window). `RunClock` writes through the logger of the run that holds it.

## A run's lines

`RunClock` writes a run's start line and, through `Done`, its done line with the stages timed by
`Lap`. It is held in a `using`: a run that throws leaves the clock without `Done`, and `Dispose`
writes `<run> ended without an outcome after N ms (<stages>)`, so the inputs and the stages
reached are in the log beside the exception its caller logs. Every run starts its clock before
it opens the session, so a refused folder still leaves the start line. The stages: `open`,
`read`, `cadence` for the inventory; `open`, `load`, `measure`, `crop` for `--frame`, the last
three timed inside `FrameRun.Measure` by the clock it is handed; `open`, `measure`, `register`,
`dither`, `pattern`, `noise`, `analyze` for the series, whose frames are measured in parallel, so
`Measure` gets no clock there and each frame's time is in its own line.

## What is logged where

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` (`Process`) | Info, Fatal, Error | the first line, the last line; the unhandled exceptions, the window thread's included; a task that failed and nobody awaited |
| `Program` (`Cli`) | Info, Warn, Error, Debug | `mode=<Mode> input=<folder> lang=<en/ro>`, or `mode=Help`; a refused `--lang` and refused arguments, with the message as shown; `wrote <file> (<KiB>)` per file of `--out`, as soon as the file is written (`SeriesOutputs.WriteAll`'s callback); `run failed: mode=<Mode>` with the exception, before the one `ERROR:` line of stderr; the code page that could not be switched or put back, at Debug |
| `SessionFolder.Open` | Info | `session root=... lights=<dir> (<n> frames, <ext>) biases=<dir> (<n>) darks=<dir> (<n>)`; a refused folder is its caller's exception |
| `ImageLoader.Load` / `LoadInfo` | Info, Debug, Trace | `loaded <LogText.Frame> in <ms>` for a full load (Info), `read the header of ...` for a metadata read (Debug); the FITS keywords that decide the reading (`LogText.FitsKeys`) at Debug; every card at Trace, from a full load only |
| `RawReader` | Debug, Warn | through `FiguresLine`, libraw's visible and raw sizes, margins, pitch, filters, `cdesc`, the CFA order or `none`, the maximum, the black and the per-channel blacks and their median, before the scale is judged; the lens block that could not be read, with the exception |
| `FrameInfo.Read` | Warn | a frame whose metadata could not be read, with the exception; its row carries the message; not when its caller takes the exception back to write it itself (the series, through the overload with an `out` parameter) |
| `InventoryRun` | Info, Debug | through `RunClock`, `inventory done lights=<n> errors=<n>`; the cadence at Debug |
| `FrameRun` | Info, Debug | through `RunClock`, `frame done detected=<n> fwhm=<v> elongation=<v>`; in `Measure`, every figure of the frame's measurement (`LogText.Measured`) at Debug, for the series' frames too |
| `SeriesRun` | Info, Debug, Warn | through `RunClock`, `series done frames= measured= errors= good= marginal= bad= unknown= walking= polar=`; `frame <i>/<n> <file> detected= fwhm= elongation= in <ms>` per frame measured, from its worker thread; a frame that could not be measured, with the exception, in one line that also says whether its metadata could be read (`LogFailedFrame`), and the metadata read's exception on a line of its own when it is another error than the measurement's (another type or message); at Debug the reference frame, each frame's registration, the cadence, the dithering, the pattern, the pixel size and its source (`PixelPitchSource`), the read noise, the WCS with the flip the analysis uses, the series analysis and each frame's class with its flags and the mount's move; the read noise that could not be measured (Warn) or had fewer than two biases (Debug); each WCS candidate at the root skipped and why (no RA axis, a zero matrix row, a file that could not be read) and the one read (Debug) |
| `App` | Info, Warn | the language and the folder from the command line; a `--lang` refused; it hooks the window thread's unhandled exception, which `LogSetup` writes |
| `MainWindow` | Info | the folder and export dialogs, chosen or cancelled; Stop; About |
| `MainViewModel` | Info, Warn, Debug, Error | `session <folder>: run started pixel= threads= east= north=`; `done in`, `stopped after`, or `refused after ...; the card shows the message` with the exception; a run a newer one replaced, at Debug; an exception the window does not handle, at Error, from an exception filter that returns false and records it with `LogSetup.Wrote`; every file the export writes, as soon as it is written, and an export that failed; the cut-outs of a frame, and a decode that failed (Warn), at Debug when another frame was selected meanwhile |
| `UserSettings`, `DarkTitleBar`, `AboutWindow` | Debug | the settings file written, with its size, and one that could not be read or written; a window manager that cannot be called (no `dwmapi.dll` or entry point), the title bar staying light; a link that could not be opened; each failure with its exception |

Not logged: pixels, a FITS card below Trace, a progress tick (the frame lines are the progress),
the cancellation of `CancelPending` racing the window's close.

## Reading a log

A process begins at its `Process <name> <version> starting` line and ends at `Process exit <code>
after <s>`. A crash writes a `FATAL` line with the exception first; whether an `exit` line still
follows it is the runtime's, as it runs the entry point's `finally` or not, so a crash is read
from its `FATAL` line. Two processes writing at once interleave, and
the `[pid:thread]` field separates them; within a series, the frames' worker threads interleave
too, and each frame's lines carry its index and file. A run is found by its `<run> start` and
`<run> done` lines, whose stage times say where the time went, or its `ended without an outcome`
line when it threw; an error by `ERROR` or `WARN` and the stack under it. A verdict is read back
from the `series:` line and the `classified frame` lines, which hold the figures and the
thresholds the report words.

## In the tests

`LogCapture` configures NLog once per test assembly, through a module initializer, with one target
at Trace that renders every event: every test thus runs every log line on its path, and a line
that throws fails the test that reaches it. A test that reads the lines opens a capture with
`LogCapture.Start`, an `AsyncLocal` scope like `Ui.Use`, which sees only its own call tree, so the
tests running beside it add nothing; one capture at a time per call tree. It is linked into the
window's test project, and so is `LogFiles`, the file side: the start of a line, the files of one
executable, a private factory, an elapsed time, the two-writer check. The file tests never touch
the assembly's configuration: they build the applications' configuration with `LogSetup.Build`
into a private `LogFactory`, which stands for one process. `R071_TheLogIsTheSameInBothLanguages`
and `R071_TheWindowsLinesAreTheSameInBothLanguages` sort the messages before comparing them,
because the frames' threads write in any order.

What only the entry points do, `Apply` and `Close`, is tested on the real console started as a
process: `R071_TheRealConsoleWritesItsFirstAndItsLastLine` and
`R072_TheRealConsoleTakesItsLevelFromLIGHTSCHECK_LOG` start it in a working folder of their own,
which its first line names, and take its lines by its process id from that line on, read while
they still hold its handle: a process id of an older run may come back, and no newer process can
be given it before the handle is closed. Those processes, and the one of
`R060_WithoutLangTheProgramSpeaksEnglish`, log as any other, so every test run leaves their lines
in `log\lightscheck.log` beside the copy of the executable in the test project's build folder,
rolled like any log. `App`'s constructor, `OnStartup` and `OnExit`, `MainWindow`'s handlers, the
title bar's catch and the wiring of the hooks run in no test: no test builds `App`, crashes a
process or lacks `dwmapi.dll`. The hooks' bodies, `LogSetup.Fatal` and `LogSetup.Unobserved`, and
the view-model's filter for an exception it does not handle, `MainViewModel.LogUnhandled`, are
tested directly. The About window's catch runs in
`R071_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays`, which raises `RequestNavigate` on
the window, on the tests' UI thread, with a relative link: asking for its absolute address throws
before any browser is started, so the test opens nothing.
