# The diagnostic log

> Added 2026-10-27 with ADR 0070. **Covers:** `FlatCheck.Core/Logging/LogSetup.cs`,
> `FlatCheck.Core/Logging/LogText.cs`, `FlatCheck.Core/Logging/RunClock.cs`,
> `FlatCheck/Program.cs`, `FlatCheck.Wpf/App.xaml.cs`, `FlatCheck.Tests/LogCapture.cs`,
> `FlatCheck.Tests/LoggingTests.cs`, `FlatCheck.Wpf.Tests/WindowLoggingTests.cs`, and the log
> lines of the seams the table below lists. A change to the code this describes means this note
> is updated in the same task, or deleted with the decision it explains.

## How the parts fit

`LogSetup.Apply` is the first call of `Program.Main` and of `App`'s constructor; `LogSetup.Close`
is the last of `Main` (in a `finally`) and of `App.OnExit`. `Apply` runs once per process:

1. `Plan` decides the place without configuring anything: the level from `FLATCHECK_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the
   entry name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable and a probe file in it,
   `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, so no log file is touched and
   nothing is left; when either fails it does the same under `%LOCALAPPDATA%\FlatCheck\log`.
2. `Build` makes the configuration: one `FileTarget` and one rule from the level up. `Off` gives
   an empty configuration, which also keeps NLog from loading an `NLog.config` it might find.
3. The hooks: `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`; the
   window adds its `DispatcherUnhandledException`. `LogSetup.Fatal` writes an exception once:
   the same object seen by the dispatcher and then by the process is named the second time, not
   written again.
4. The first line, `StartupLine`, at Info.

`FlatCheckCli.Run` configures nothing, so the tests can call it in-process. The window's test
fixture builds a plain `Application`, not `App`, for the same reason.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `flatcheck.log`, then past `FileSizeLimit` (1
MiB) `flatcheck_01.log`, `_02`, and so on (`ArchiveSuffixFormat`'s default `_{0:00}`; past 99
the number simply grows). `MaxArchiveFiles` counts every file of the sequence, the one being
written included: at 4 it left 4 files, so it is `FilesPerExecutable`, 5. A file ends one line
past the limit (1049286 bytes for 4 KB lines). After 8 MiB written the folder holds
`flatcheck_03.log` to `flatcheck_07.log`, and the next process appends to `flatcheck_07.log`
(`R109_TheLogRollsAt1MiBAndKeepsAtMost5Files`). The window's files, rolled once, survive the
console's eight rolls in the same folder (`R109_OneExecutablesRollingLeavesTheOthersFilesAlone`):
NLog matches its own name and number only.

`KeepFileOpen` is `false`: ADR 0070 has the measurements behind it (two windows, two processes at
full speed, the cost per line) and R-112 the question of `NLog.Targets.AtomicFile`. In one process
two factories on one file stand for two processes; with the file kept open, one of them lost all
its lines, which `R109_TwoProcessesWriteIntoTheSameFile` would catch.

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the level
padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant numbers
with six significant digits, `none` for a missing value, sizes in KiB and MiB) and logged as plain
strings, so NLog never parses them as message templates and a `{` in a path is harmless. A line
whose message costs something to build is guarded by `IsDebugEnabled` / `IsTraceEnabled`.

Loggers are named, not taken from the stack: the class name for the core (`ImageLoader`,
`FitsReader`, `RawReader`, `PreviewReader`, `ProfileLocator`, `LensfunReader`, `LensCatalog`,
`LensIdentification`, every run in `Runs/`), and for the applications `Process` (the first and
last lines, the crashes), `Cli`, `App`, `Window` (the main view-model, the main window and every
tab), `Siril` (the installation, the script runs, siril-cli's output) and `Split` (its temp
folder).

## What is logged where

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` | Info, Fatal | the first line, the last line; the unhandled exceptions |
| `FlatCheckCli` | Info, Warn, Error | `mode=` with the input, the language and the options that differ from their defaults; every refusal that is not an exception (the user's message as shown); `run failed` with the exception; every file written with its size |
| `ImageLoader` | Info, Debug | `loaded <path> (<MiB>) reader=... <W>x<H> planes bitpix cfa scale camera focal fnumber lens range preview in <ms>`; the metadata reads at Debug |
| `FitsReader` | Debug, Trace | the keywords that decide the reading (`LogText.FitsKeys`); every card at Trace |
| `RawReader` | Debug, Warn | libraw's visible and raw sizes, margins, filters, maximum, black and per-channel black, thumbnail count; the lens block and the preview capture that failed |
| `PreviewReader` | Warn | a preview that did not decode, with the exception |
| `ProfileLocator`, `LensfunReader`, `LensCatalog` | Info, Warn, Debug | the folders searched (missing ones marked), the counts, lensfun's version and the lenses of each source, with the time; the folders and files that could not be read |
| `LensIdentification` | Debug | `identified` with the focal length, the f-number and the crop and where each came from, the raw's lens and the candidates by catalogue key |
| `Runs/` | Info, Debug, Warn | through `RunClock`, `<run> start <inputs>` and `<run> done <outcome> in <ms> (<stage> <ms>, ...)`; at Debug the result's figures (`LogText.Structure`, `Comparison`, `Light`, `LightSeries`, `Exposure`, `HistEq`, `Smooth`, `Advice`, `Synthetic`, `Against`, `Split`), one line per frame of a series; the raw beside the master that could not be read |
| `App`, `MainWindow` | Info, Warn | the language and the folder from the command line, a `--lang` refused; the folder dialog, About, the Siril run asked and its end, the answer to the close question during a split |
| `MainViewModel` | Info, Debug | the session usable or refused with the problem; at Debug the master (and an older one ignored), the counts of flats, raw flats and lights, the reference raw |
| `TabViewModel` | Info, Error, Debug | `<Tab> tab, run <n> started`, `done in`, `cancelled after`; `failed` with the exception; a superseded run's failure at Debug |
| the tabs | Info, Warn, Debug | every file written; a write or a save that failed; an overwrite the user declined; the Synthetic prefill; the Split phases, parts, failures and stop; the conveniences that failed quietly (lens details, format note, free space) at Debug |
| `SirilInstall`, `SirilScriptRun`, `SirilRunViewModel`, `PreprocessViewModel` | Info, Debug, Trace, Warn, Error | where siril-cli, the config and the scripts were found; the command line started with the settings copy; every output line at Debug, the progress lines at Trace; the end with the exit code, the time and the command reached; a run that could not start; a script that could not be read |

## Reading a log

A process begins at its `Process <name> <version> starting` line and ends at `Process exit <code>
after <s>`; a crash ends instead at a `FATAL` line. Two processes writing at once interleave, and
the `[pid:thread]` field separates them. A run is found by its `<run> start` and `<run> done`
lines, whose stage times say where the time went; an error by `ERROR` and the stack under it.
In the window each tab's run is numbered, so a run that a newer one replaced can be told apart.

## In the tests

`LogCapture` configures NLog once per test assembly, through a module initializer, with one target
at Trace that renders every event: every test thus runs every log line on its path, and a line
that throws fails the test that reaches it. A test that reads the lines opens a capture with
`LogCapture.Start`, an `AsyncLocal` scope like `Ui.Use`, which sees only its own call tree, so
the tests running beside it add nothing; a capture opened inside another feeds both. The file
tests never touch that configuration: they build the applications' configuration with
`LogSetup.Build` into a private `LogFactory`, which stands for one process.
