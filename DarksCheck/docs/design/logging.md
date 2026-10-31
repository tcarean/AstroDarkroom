# The diagnostic log

**Covers:** `src/DarksCheck.Core/Logging/LogSetup.cs`, `src/DarksCheck.Core/Logging/LogText.cs`, `src/DarksCheck.Core/Logging/RunClock.cs`, `src/DarksCheck.Cli/Program.cs`, `src/DarksCheck.Wpf/App.xaml.cs`, `src/DarksCheck.Wpf/Loggers.cs`, `tests/DarksCheck.Tests/LogCapture.cs`, `tests/DarksCheck.Tests/LogFiles.cs`, `tests/DarksCheck.Tests/LoggingTests.cs`, `tests/DarksCheck.Wpf.Tests/WindowLoggingTests.cs`, and the log lines of the seams the table below lists.

The behaviour is R-100 to R-103 and the decisions are ADR 0025, which takes LightsCheck's log
(its ADR 0029), itself FlatCheck's (its ADRs 0070 and 0072); this note is the mechanics.
FlatCheck's and LightsCheck's `docs/design/logging.md` describe the same parts under the same
names, and what FlatCheck measured about NLog (the rolling, the targets, the stray `NLog.config`)
holds here: the same packages, the same settings.

## How the parts fit

`LogSetup.Apply` is the first call of `Program.Main` and of `App`'s constructor, each in a `try`
that swallows anything it throws, because the log never stops the tool; `LogSetup.Close` is the
last of `Main` (in its `finally`) and of `App.OnExit`, guarded the same way. `Apply` runs once
per process:

1. `Plan` decides the place without configuring anything: the level from `DARKSCHECK_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the
   entry name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable, a probe file in it,
   `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, and opens for append the file
   NLog will write next when there is one (`NewestLog`: the highest-numbered `<name>_NN.log`,
   else `<name>.log`), with the share mode that lets another process of the same executable
   hold it open; when any of these fails it does the same under `%LOCALAPPDATA%\DarksCheck\log`,
   beside the window's `settings.json`.
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
   exception is such a one: written by `MainViewModel.LogUnhandled` before it leaves `OpenAsync`
   in a task the window never awaits.
4. The first line, `StartupLine`, at Info.

`Program.Run`, the console the tests call in-process, configures nothing; neither does the window
tests' fixture, which builds a plain `Application`, not `App`.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `darkscheck.log`, then past `FileSizeLimit`
`darkscheck_01.log`, `_02`, and so on. A file ends one line past the limit.
`R100_TheLogRollsAt1MiBAndKeepsAtMost5Files` writes 8 MiB of 4 KB lines through the console's
configuration; measured on 2026-10-30 it leaves:

| file | bytes | after a second process |
| --- | --- | --- |
| `darkscheck_03.log` to `darkscheck_06.log` | 1049286 each | the same |
| `darkscheck_07.log` | 980211, the last line in it | 984336 |

The second process appends to `_07`, which grows, and the five names stay. The byte counts move by
a few hundred from one run to the next, with the digits of the process and thread ids. The
window's files, rolled once, survive the console's rolls in the same folder
(`R100_OneExecutablesRollingLeavesTheOthersFilesAlone`): NLog matches its own name and number only,
and `DarksCheck.Wpf` begins with the same letters as `darkscheck` in another case.

In one process two factories on one file stand for two processes, each on a thread of its own
released by a `Barrier` so they do overlap: `R103_TwoProcessesWritingAtFullSpeedKeepEveryLine`
writes 4000 lines from each, crossing the 1 MiB roll, and asks for every line, whole, once, and
for lines of both writers interleaved both ways round. Measured on 2026-10-30 it left
`DarksCheck.Wpf.log` (1048832 bytes) and `DarksCheck.Wpf_01.log` (647036 bytes), 4000 whole lines
of each writer and none broken, on the console's `net8.0` build of the atomic target; the
window's tests load its `net8.0-windows` build, a binary of its own, which the window ships, and
`R103_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows` runs the same check
(`LogFiles.TwoWritersKeepEveryLine`) on it: 1048875 and 646993 bytes, none broken.

A darks run of the Smida session against the library (20 darks, 30 biases, 32 lights, the 30
darks of the library, all NEF) writes 495 lines, 197715 bytes at Debug, on 2026-10-30:

| lines | bytes | share | what |
| --- | --- | --- | --- |
| 112 | 44 KB | 22 % | `loaded`, one per file read whole (`ImageLoader`) |
| 112 | 35 KB | 18 % | libraw's figures of each decode (`RawReader`) |
| 82 | 30 KB | 15 % | `read the header of`, the inventory's metadata reads (`ImageLoader`) |
| 20 | 26 KB | 13 % | each dark's figures, four planes and their residuals (`DarkSeriesRun`) |
| 80 | 19 KB | 10 % | `decoded frame` with the working set (`FrameStack`) |
| 30 | 16 KB | 8 % | each bias's figures (`DarkSeriesRun`) |
| 32 | 13 KB | 7 % | each light's figures (`LightsAnalyzer`) |
| 27 | 14 KB | 7 % | the rest: the first and last lines, the clocks, the series, the verdicts, the partner |

So a 1 MiB file holds five such runs and the five files twenty-six; the readers' three lines per
file are more than half of it. ADR 0025 has the run times with the log on and off. Two real
console processes started together wrote 130 lines into one file, none broken, interleaved both
ways round; and the published single-file console, started from another folder, wrote its log
beside its executable, since `AppContext.BaseDirectory` is the executable's folder for that
publish (the native libraries alone are extracted, not the managed code). Two real windows
started together on two libraries wrote 336 lines into one `DarksCheck.Wpf.log`, 168 each, none
broken, interleaved both ways round.

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the
level padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant numbers
with six significant digits, `none` for a missing value, sizes in KiB and MiB, an elapsed time as
`Ms`, `1141 ms`, or `Seconds`, `12.9 s`, an enum by its name) and logged as plain strings, so
NLog never parses them as message templates and a `{` in a path is harmless. A line whose message
costs something to build is guarded by `IsDebugEnabled` or `IsTraceEnabled`. A line never
throws: a value it may not have is written `none`, a file whose size cannot be read `?`.

`LogText` never writes a text the report words in the run's language: the stack's line says where
its black and white come from in the log's own words (`the file's`, `the biases'`, `the median of
frame N`, `from BITPIX`), and the series' line leaves out `ScaleSource`, the report's sentence for
the same; the partner's line leaves out `FirstError`, and a frame's figures leave out its `Error`,
whose frame wrote its own Warn line with the exception. A raw's scale source, `libraw (exact)`,
and a CFA order's, `libraw` or `BAYERPAT`, are not translated, so they stay. An exception's own
message is in the run's language, as the user saw it, on the lines below a message, never in it.

Every logger is a `Logger` named `Log`, taken from `LogManager.GetLogger` with `using NLog`
(`NoLogLineGoesThroughTheTranslation` checks it, so its scan of the calls sees them all): a
`static readonly` field everywhere but in `LogSetup`, `Program` and `App`, where it is a property
fetched on use (step 2 above). The names: the class name in the core (`ImageLoader`, `RawReader`,
`SessionFolder`, `FrameInfo`, `InventoryRun`, `FrameStack`, `LightsAnalyzer`, `DarkSeriesRun`),
`Process` for the first and last lines and the crashes, `Cli` for the console, and the window's
two from `Loggers`: `App` (the start's choices) and `Window` (the main window, the view-model,
the stored settings, the title bar, the About window). `RunClock` writes through the logger of the
run that holds it; `FrameStack.LogFailed` through the logger its caller hands it, the darks'
loop's or the lights'.

## The figures of a record

`LogText.Figures` writes a result record by one rule, so a figure the analyzers gain is in the
log without a change here: each public property, in declaration order (`MetadataToken`), by its
name in camelCase, `=`, and its value; a number with six significant digits, an integer, a flag
as `yes` or `no`, an enum by its name, a time, a text as it is, a missing value as `none`; a
record held by the record in parentheses, down to six levels; a tuple in parentheses; a list in
brackets, whole up to twelve items, else `[first, ..., last] (n items)`, so a master of 24 million
pixels costs two values. The properties are read once per type into a concurrent cache, since the
lights' loop calls it from its worker threads; a property whose getter throws is written `?`.
The caller names the properties it leaves out: a frame's `Info` (its file has its own lines),
its `Error` and `Skipped` (only decoded frames get a figures line), the series' masters and the
lists that have their own lines (`Frames`, `MedianMaster`, `MeanMaster`).

## A run's lines

`RunClock` writes a run's start line and, through `Done`, its done line with the stages timed by
`Lap`. It is held in a `using`: a run that throws leaves the clock without `Done`, and `Dispose`
writes `<run> ended without an outcome after N ms (<stages>)`, so the inputs and the stages
reached are in the log beside the exception its caller logs. `DarkSeriesRun` starts its clock
first, so a refused folder still leaves its start line; its stages are `inventory` (with the
partner's darks when they stand in for the session's), `biases`, `darks` (the decode), `measure`,
`verdicts`, `lights` and `partner`, only those reached, and `DarkSeriesRun.Outcome` writes its done
line: the six verdicts, the index, the label, the lights' index, each group's frames decoded out
of its files, and whether the run met an error. `InventoryRun`, which `DarkSeriesRun` calls first,
writes its own clock's lines inside them, with `open`, `darks`, `biases`, `lights` and `cadence`.
After each group's frames are released and collected, `DarkSeriesRun` writes the working set at
Debug, beside the decode lines' own (ADR 0007's memory trap).

A frame that a loop cannot decode is one Warn line with the decode's exception, which also says
whether its metadata could then be read (`FrameStack.LogFailed`, through the `out` overload of
`FrameInfo.Read`, which writes nothing itself): the metadata read's exception follows on a line
of its own only when it is another error than the decode's (another type or message), so each
exception is written once with its stack.

## What is logged where

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` (`Process`) | Info, Fatal, Error | the first line, the last line; the unhandled exceptions, the window thread's included; a task that failed and nobody awaited |
| `Program` (`Cli`) | Info, Warn, Error | `mode=help`, or `mode=<darks or inventory> input= against= out= threads= maxFrames= lang=`; a refused `--lang` and refused arguments, with the message as shown; `wrote <file> (<KiB>)` per file of `--out`, as soon as the file is written (`DarkOutputs.WriteAll`'s callback); `run failed: mode=<mode>` with the exception, before the one `ERROR:` line of stderr, a refused `--out` among them |
| `SessionFolder.Open` | Info | `opened root= kind=<session, library or lights without darks> darks=<dir> (<n> frames, <ext>) biases=<dir> (<n>) lights=<dir> (<n>)`, for the session and the partner; a refused folder is its caller's exception |
| `ImageLoader.Load` / `LoadInfo` | Info, Debug, Trace | `loaded <LogText.Frame> in <ms>` for a full load (Info), `read the header of ...` for a metadata read (Debug); the FITS keywords that decide the reading (`LogText.FitsKeys`) at Debug; every card at Trace, from a full load only |
| `RawReader` | Debug, Warn | through `FiguresLine`, libraw's visible and raw sizes, margins, pitch, filters, `cdesc`, the CFA order or `none`, the maximum, the black and the per-channel blacks and their median, before the scale is judged; the lens block that could not be read, with the exception |
| `FrameInfo.Read` | Warn | a frame whose metadata could not be read, with the exception, its row carrying the message; a file that is no FITS, without one; not when a loop takes the exception back to write it itself (the `out` overload) |
| `InventoryRun` | Info, Debug | through `RunClock`, `inventory done darks= biases= lights= errors=`; at Debug the cadence of the darks and of the lights and the match of check 1 (`LogText.Figures`) |
| `FrameStack` | Info, Debug, Warn | per frame decoded, at Debug, `decoded frame <i>/<n> <path>: <W>x<H> black= white= norm= in <ms>; working set <MiB>`; a frame left out for its size or its settings, at Warn, in the log's words; a frame that failed (`LogFailed`); the stack's counts, size, scale and normalisation, at Info (Warn when none decoded); `DecodeOne`'s light of another size, at Warn |
| `LightsAnalyzer` (the loop) | Debug, Warn | `light <i>/<n> <path> measured in <ms>; working set <MiB>; <figures>`; a light that failed (`FrameStack.LogFailed`) |
| `DarkSeriesRun` | Info, Debug, Warn | through `RunClock` the start line with the options and the done line; the partner's darks standing in (R-054), at Info; a group that fails (the biases, the dark current, the lights, the partner), at Warn with the exception; at Debug the biases, each bias's and each dark's figures, the series, the dark current, the verdicts, the index with the label, the lights and the partner (`LogText.Figures`), and the working set after each group is released |
| `App` | Info, Warn | the language and the folder from the command line; a `--lang` refused; it hooks the window thread's unhandled exception, which `LogSetup` writes |
| `MainWindow` | Info, Debug | the folder, partner and export dialogs, chosen or cancelled; Stop, Rerun, About; the clipboard another process holds, at Debug with the exception |
| `MainViewModel` | Info, Error, Warn, Debug | `run <n> started: folder= threads= maxFrames= isoFactor= doubling= against=`, the opens of the view-model counted from 1; `done in`, `stopped after`, or `failed after ...; the problem card shows the message` with the exception; a run a newer one replaced, at Debug, stopped or failed; an exception the window does not handle, at Error, from an exception filter that returns false and records it with `LogSetup.Wrote`; the partner chosen or cleared; every file the export writes, as soon as it is written, and an export that failed; the label copied |
| `UserSettings`, `DarkTitleBar`, `AboutWindow` | Debug | a settings file that could not be read or written (a save that works writes nothing: the boxes save on every keystroke); a window manager that cannot be called (no `dwmapi.dll` or entry point), the title bar staying light; a link the system could not open; each failure with its exception |

Not logged: pixels, a FITS card below Trace, a progress tick (the phases are the report's words;
the frame lines are the progress), the cancellation of `CancelPending` racing the window's close.

## Reading a log

A process begins at its `Process <name> <version> starting` line and ends at `Process exit <code>
after <s>`. A crash writes a `FATAL` line with the exception first; whether an `exit` line still
follows it is the runtime's, as it runs the entry point's `finally` or not, so a crash is read
from its `FATAL` line. Two processes writing at once interleave, and the `[pid:thread]` field
separates them; within a group the decoding threads interleave too, and each frame's lines carry
its index and file. A run is found by its `darks start` and `darks done` lines, whose stage times
say where the time went, or its `ended without an outcome` line when it threw; an error by
`ERROR` or `WARN` and the stack under it. Memory is read from the `working set` of the `decoded`
lines and of the lines after each group is released. A verdict is read back from the `verdicts:`
line and the `series:`, `dark` and `light` lines, which hold the figures the report words; a
frame's file from its `loaded` and libraw lines.

## In the tests

`LogCapture` configures NLog once per test assembly, through a module initializer, with one target
at Trace that renders every event: every test thus runs every log line on its path, and a line
that throws fails the test that reaches it. A test that reads the lines opens a capture with
`LogCapture.Start`, an `AsyncLocal` scope like `Ui.Use`, which sees only its own call tree, so the
tests running beside it add nothing; one capture at a time per call tree. It is linked into the
window's test project, and so is `LogFiles`, the file side: the start of a line, the files of one
executable, a private factory, an elapsed time, the two-writer check. The file tests never touch
the assembly's configuration: they build the applications' configuration with `LogSetup.Build`
into a private `LogFactory`, which stands for one process. `R101_TheLogIsTheSameInBothLanguages`
and `R101_TheWindowsLinesAreTheSameInBothLanguages` sort the messages before comparing them,
because the decoding threads write in any order, and make the times and the working sets equal.

What only the entry points do, `Apply` and `Close`, is tested on the real console started as a
process: `R101_TheRealConsoleWritesItsFirstAndItsLastLine` and
`R102_TheRealConsoleTakesItsLevelFromDARKSCHECK_LOG` start it in a working folder of their own,
which its first line names, and take its lines by its process id from that line on, read while
they still hold its handle. Those processes log as any other, so every test run leaves their lines
in `log\darkscheck.log` beside the copy of the executable in the test project's build folder,
rolled like any log. `App`'s constructor, `OnStartup` and `OnExit`, `MainWindow`'s handlers, the
title bar's and the About window's catches and the wiring of the hooks run in no test: no test
builds `App`, crashes a process, lacks `dwmapi.dll` or follows a link. The hooks' bodies,
`LogSetup.Fatal` and `LogSetup.Unobserved`, and the view-model's filter for an exception it does
not handle, `MainViewModel.LogUnhandled`, are tested directly. The three invariants of
`LoggingTests` pin the shape: `OnlyTheTwoEntryPointsConfigureTheLog`,
`NoLogLineGoesThroughTheTranslation`, and `NothingThatComputesOrWordsTheReportLogs`, which also
asks that the files of `Analysis/` and `Imaging/` that do log are exactly the two of each.
