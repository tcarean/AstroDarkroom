# The diagnostic log

**Covers:** `src/CopyWcs.Core/Logging/LogSetup.cs`, `src/CopyWcs.Core/Logging/LogText.cs`,
`src/CopyWcs.Core/Logging/RunClock.cs`, `src/CopyWcs.Cli/Program.cs`,
`src/CopyWcs.Wpf/App.xaml.cs`, `src/CopyWcs.Wpf/Loggers.cs`, `tests/CopyWcs.Tests/LogCapture.cs`,
`tests/CopyWcs.Tests/LogFiles.cs`, `tests/CopyWcs.Tests/LoggingTests.cs`,
`tests/CopyWcs.Wpf.Tests/WindowLoggingTests.cs`, and the log lines of the seams the table below
lists.

The behaviour is R-040 to R-046; the decisions are ADR 0009, which takes FlatCheck's (its ADRs
0070 and 0072) and its measurements; this note is the mechanics. `Logging/` is adapted from
FlatCheck's `FlatCheck.Core/Logging/` with its names and shapes, so a fix carries between the two.

## How the parts fit

`LogSetup.Apply` is the first call of `Program.Main` and of `App`'s constructor, each in a `try`
that swallows anything it throws, because the log never stops the tool; `LogSetup.Close` is the
last of `Main` (in a `finally`) and of `App.OnExit`, guarded the same way. `Apply` runs once per
process:

1. `Plan` decides the place without configuring anything: the level from `COPYWCS_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the
   entry name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable, a probe file in it,
   `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, and opens for append the file
   NLog will write next when there is one (`NewestLog`: the highest-numbered `<name>_NN.log`,
   else `<name>.log`), with the share mode that lets another process of the same executable hold
   it open; when any of these fails it does the same under `%LOCALAPPDATA%\CopyWcs\log`.
2. `Build` makes the configuration, one `AtomicFileTarget` and one rule from the level up, and
   `Apply` sets it **before any logger exists**: `LogSetup`'s own logger and `App`'s are
   properties fetched on use, not static fields, because a logger created first would make NLog
   load an `NLog.config` it finds beside the executable (FlatCheck measured a stray one killing
   the process before `Main`'s body). `Off`, or no folder, gives a configuration without targets,
   and `Apply` returns there.
3. The hooks: `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`; the
   window adds its `DispatcherUnhandledException`, which logs and does not handle. `LogSetup.Fatal`
   writes an exception at Fatal and flushes; the same exception seen a second time is named, not
   written again.
4. The first line, `StartupLine`, at Info.

`CopyWcsCli.Run` configures nothing, so the tests call it in process; the window's test fixture
(`StaFixture`) builds a plain `Application`, not `App`, for the same reason. No test therefore
runs `Program.Main`, `App`'s constructor (with its dispatcher hook), `OnStartup` and `OnExit`, the
dialogs' handlers of `MainWindow` or `ShowAbout`; the tests cover the text `App.StartLine` builds,
`LogSetup.Fatal` called directly, and `AboutWindow.OpenLink`'s catch through a raised navigation to
a relative link (its `InvalidOperationException`). The runs by hand of 2026-10-30 (the plan's
Outcome) wrote the process's first and last lines of both applications, `App`'s start line and
`About opened`. The dialogs' lines, a refused `--lang` in the window, the window's dispatcher hook,
`LogSetup`'s "a task failed and nobody awaited it" and a link with no program to open it
(`OpenLink`'s `Win32Exception`) have not run anywhere yet, nor has a link that opens.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `copywcs.log`, then past `FileSizeLimit`
`copywcs_01.log`, `_02`, and so on (`ArchiveSuffixFormat`'s default `_{0:00}`). A file ends one
line past the limit. Measured on 2026-10-30 by `R040_TheLogRollsAt1MiBAndKeepsAtMost5Files`,
8 MiB of 4 KB lines through the console's configuration:

```
copywcs_03.log 1049286 bytes
copywcs_04.log 1049286 bytes
copywcs_05.log 1049286 bytes
copywcs_06.log 1049286 bytes
copywcs_07.log 980211 bytes
after a second process: copywcs_07.log 984336 bytes, the four others unchanged
```

The window's two files, rolled once, survive the console's rolls in the same folder
(`R040_OneExecutablesRollingLeavesTheOthersFilesAlone`; `CopyWcs.Wpf` begins with the letters of
`copywcs` in another case, and NLog matches its own name and number only). Two factories on one
file stand for two processes, each on a thread of its own released by a `Barrier` (`LogFiles`),
4000 lines from each, crossing the roll, once through each build of `NLog.Targets.AtomicFile`.
Measured on 2026-10-30, the lines the two tests print, from one run:

```
R045_TwoProcessesWritingAtFullSpeedKeepEveryLine, the console's net8.0 build:
 copywcs.log 1048731 bytes
 copywcs_01.log 647137 bytes
 whole lines: 4000 from A, 4000 from B; broken: 0
R045_TwoWindowsWritingAtFullSpeedKeepEveryLine, the window's net8.0-windows7.0 build:
 CopyWcs.Wpf.log 1048841 bytes
 CopyWcs.Wpf_01.log 647027 bytes
 whole lines: 4000 from A, 4000 from B; broken: 0
```

The sizes change from run to run: the split with the interleaving, the total with the digits of
the `[pid:thread]` field every line carries (a test process whose id has one digit more writes
8000 bytes more; the third review's runs gave 1695868 and 1687868 bytes in all). What holds in
every run is what the tests assert: 4000 and 4000 whole lines, none broken, interleaved both ways
round.

The cost, measured on 2026-10-30 with the Release console copying the Veil fixture's solve into
the 290 MB stack `procesate\result.fit` of the same session (6016 x 4016, so with `--force`, `-o`
into a scratch folder), the runs alternated: 1.52 s (the cold first run), 0.49 s and 0.65 s with
`COPYWCS_LOG=off`; 0.63 s, 0.56 s and 0.63 s at Debug, each writing 12194 bytes of log. The log
costs nothing measurable: a copy's time is the data stream (`write 309 ms` of a `328 ms` run, the
done line the plan's Outcome quotes).
The single-file console published as `kit.json` publishes it, copied alone into a folder and run
from another, wrote `log\copywcs.log` beside itself; the window's published `NLog.dll` and
`NLog.Targets.AtomicFile.dll` hash equal to those in AstroDarkroom's 1.0.0 folder.

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the level
padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` and logged as plain
strings, so NLog never parses them as message templates and a `{` in a path is harmless; a line
whose message costs a loop is guarded by `IsDebugEnabled` or `IsTraceEnabled`. A card is written
by `LogText.Card` as the header holds it: no trailing spaces, a control character and a byte
above 0x7F each as `\xNN`, so a NUL in a malformed header never reaches the log and a byte the
standard forbids, which the copy keeps as it is (R-010, ADR 0010), is named by its code with the
log itself staying ASCII (a card's characters are its bytes one to one, so the code is the byte's;
`R042_AByteAboveAsciiInACardIsLoggedAsItsHexCode` reads `Orion \xC5`). The HISTORY card the tool
builds is ASCII before it is logged (`AsciiText`, R-011). A value is written by `LogText.Value` (the text of a value card up to its comment, a quoted
string whole). Neither feeds the copy, which stays the card as read (R-010).

Every logger is a `Logger` named `Log`, taken from `LogManager.GetLogger` with `using NLog`: a
`private static readonly` field everywhere but `LogSetup` and `App`, whose are fetched on use
(above). The names: `Process` (the first and last lines, the crashes), `FitsHeader`, `WcsCopy`,
`Cli`, and the window's from `Loggers`: `App` (the start, the exit) and `Window` (the main window,
the view-model, the About window).

## A copy's lines

`WcsCopy.Run` holds a `RunClock` in a `using`: `copy start <request>` before the source is read,
then a `Lap` after each stage, `source`, `dest`, `select` (the size check, the `CTYPE1` check, the
mirror check, the selection and the keys to drop) and `write` (the old cards dropped, the cards
set, the `HISTORY` card appended, the file written), and `Done` with the outcome and the times of
the stages reached. A refusal laps the stage it happens in and says why (`refused: cannot read
the source`, `cannot read the destination`, `dimensions differ (source 100x80, destination
200x80)`, `mirrored plate solve (source det -2.9641E-6, destination det +2.9641E-6)`, `no
relevant key in the source`, `the write failed`), after a Warn line (an Error one for the write)
holding the exception when there is one. The mirror's Warn line, `mirrored plate solve: source
det ..., destination det ...; not forced, nothing is written` or `...; forced, the copy goes
on`, writes both determinants through `LogText.Signed`, so the log alone says which way each
solve faced (R-017). A run that
throws leaves the clock without `Done`, and `Dispose` writes `copy ended without an outcome after
N ms (<stages>)`. `LogSelection` runs before any card is dropped or set: the counts per list
(`WcsKeys`, `OpticalKeys`, the rest being SIP coefficients), then per key the source's card and
what the destination held (`none`, `same`, or its card), counted into the done line's `(new,
replaced, identical, dropped)`, then one `drop <key>: <card>` line per card of each key the copy
drops (R-016), every card of a keyword the destination holds twice, while `dropped` counts the
keys; in a dry run too. The four counts are always written, `dropped 0` included: the version
does not change with a fix, so that count is what tells a log of a build that drops from one of
a build that does not.

## What is logged where

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` | Info, Fatal, Error | the first line, the last line (`exit <code> after <s> s`); the unhandled exceptions; a task nobody awaited |
| `FitsHeader.ReadPrimary` | Info, Trace | `read <path> (<size>) bytes cards blocks dataOffset data` and the keys of `LogText.Keys` (`size unknown`, `bytes=none`, `data=none` when `FitsHeader.LengthOf` has no length: a stream that cannot seek, a named pipe, is not asked, and whatever a seekable file's query throws is taken as none); at Trace `<path> card <n>: <card>`, every card from 1 |
| `FitsHeader.AddHistory` | Debug | `appended <card>` |
| `FitsHeader.WriteWithDataFrom` | Debug, Trace, Info | `write <target>[ in place through <target>.tmp], the data from byte <offset> of <source>` before the file is created, every card to be written at Trace, `wrote <target> (<size>) bytes cards blocks data` after the move |
| `WcsCopy.Run` | Info, Warn, Error, Debug | the start and done lines; `cannot read the source` / `the destination <path>` with the exception; the two warnings and the two refusals without one; `the write of <target> failed` with the exception; at Debug `selected <n> keys: <w> WCS, <s> SIP, <o> optical` and `card <KEY>: <card> \| dest: <none, same or card>` |
| `CopyWcsCli` | Warn, Info | `refused (exit 1): <the --lang message>`, `usage error (exit 2): <the line on stderr>`, `help shown (exit 0)` |
| `App` | Info, Warn | `language=<en or ro> source from the command line: <path or none> dest: <path or none>` (`StartLine`); a `--lang` refused, before its message box |
| `MainWindow` | Info, Debug | the file picked in each of the three dialogs, or the dialog closed without one; `About opened` |
| `MainViewModel.Facts` | Debug | no facts under a box: the header could not be read (with the exception), or has no `NAXIS1` or `NAXIS2` |
| `AboutWindow.OpenLink` | Debug | a link that could not be opened, with the exception |

The window's Preview and Copy add no line of their own: the core's start line holds the request,
its done line the outcome, and a copy from the window writes what the console's writes
(`R043_ACopyFromTheWindowWritesTheLinesOfTheConsolesCopy`).

## Reading a log

A process begins at its `Process <name> <version> starting` line and ends at `Process exit <code>
after <s> s`. A crash writes a `FATAL` line with its stack; what follows depends on the error mode
the process inherits from what started it (measured by the second review of 2026-10-30 with a
probe): started from cmd, PowerShell or Explorer, Windows Error Reporting takes the crash and the
log ends at the `FATAL` line; started from Git Bash, whose children inherit the crash dialog
turned off, Windows unwinds the stack, the console's `finally` blocks run, and the `FATAL` line is
followed by the run's `ended without an outcome` line when the crash happened inside a copy run,
then by `exit -532462766`, the code the crashed process gets (`Program.CrashExitCode`, never the
refusal's 1). Two processes writing at once interleave,
and the `[pid:thread]` field separates them. A run is found from `copy start` to `copy done`,
whose stage times say where the time went, or to `ended without an outcome` when it threw; a wrong
WCS from the `card` lines and the two `read` lines (`ROWORDER`, `NAXIS`, the solve's `CTYPE`); an
error by `WARN` or `ERROR` and the stack under it. At Trace the `card <n>` lines rebuild both
headers and the one written.

## In the tests

`LogCapture` configures NLog once per test assembly, through a module initializer, with one
target at Trace that renders every event through `LineLayout`: every test thus runs every log
line on its path, and a line whose message throws while it is composed fails the test that
reaches it (an error inside NLog's own rendering is swallowed, as in a user's run). A test that
reads the lines opens a capture with `LogCapture.Start`, an `AsyncLocal` scope like `Ui.Use`,
which sees only its own call tree (a `Task.Run` inside it included), so the tests running beside
it add nothing; one capture at a time per call tree. The window's test project links the same
file. The file tests never touch that configuration: they build the applications' configuration
with `LogSetup.Build` into a private `LogFactory`, which stands for one process. `LogFiles`, linked
into both test projects too, reads the files and runs the two writers of R-045, so each assembly
tests the build of `NLog.Targets.AtomicFile` its application ships: `net8.0` for the console,
`net8.0-windows7.0` for the window, two different files (8192 and 7168 bytes), each test first
checking which one it loaded.
