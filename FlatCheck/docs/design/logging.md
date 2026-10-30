# The diagnostic log

> Added 2026-10-27 with ADR 0070; the review fixes of 2026-10-27 (ADR 0072, and ADR 0071 for
> the lens without a profile). **Covers:** `FlatCheck.Core/Logging/LogSetup.cs`,
> `FlatCheck.Core/Logging/LogText.cs`, `FlatCheck.Core/Logging/RunClock.cs`,
> `FlatCheck/Program.cs`, `FlatCheck.Wpf/App.xaml.cs`, `FlatCheck.Wpf/Loggers.cs`,
> `FlatCheck.Tests/LogCapture.cs`, `FlatCheck.Tests/LoggingTests.cs`,
> `FlatCheck.Tests/UnlistableFolder.cs`, `FlatCheck.Wpf.Tests/WindowLoggingTests.cs`, and the
> log lines of the seams the table below
> lists. A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains. The decisions and the figures (the target, the counts,
> the levels, the words, the Fatal line written once) are ADR 0070's, and the review's choices
> (refusals, a run that throws, the log never stopping the tool) ADR 0072's; this note is the
> mechanics.

## How the parts fit

`LogSetup.Apply` is the first call of `Program.Main` and of `App`'s constructor, each in a
`try` that swallows anything it throws, because the log never stops the tool; `LogSetup.Close`
is the last of `Main` (in a `finally`) and of `App.OnExit`, guarded the same way. `Apply` runs
once per process:

1. `Plan` decides the place without configuring anything: the level from `FLATCHECK_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the
   entry name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable, a probe file in it,
   `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, and opens for append the file
   NLog will write next when there is one (`NewestLog`: the highest-numbered `<name>_NN.log`,
   else `<name>.log`), with the share mode that lets another process of the same executable
   hold it open; when any of these fails it does the same under `%LOCALAPPDATA%\FlatCheck\log`.
   Without the append probe a read-only or locked newest file passed, and NLog dropped every
   line in silence (the review of 2026-10-27, A16).
2. `Build` makes the configuration, one `AtomicFileTarget` and one rule from the level up, and
   `Apply` sets it **before any logger exists**: `LogSetup`'s own logger is a property fetched
   on use, not a static field. Measured on 2026-10-27 with a scratch console beside an
   `NLog.config` that names an unknown target with `throwConfigExceptions="true"`: with the
   logger in a static field, creating it made NLog load that file and the process died in
   `TypeInitializationException` (`NLogConfigurationException: Failed loading NLog
   configuration ... unknown type-alias`) before anything ran; with the logger fetched on use
   after the configuration is set, the process ran. `Off` gives a configuration without
   targets, and `Apply` returns there.
3. The hooks: `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`; the
   window adds its `DispatcherUnhandledException`. `LogSetup.Fatal` writes the exception
   (ADR 0070 says why once).
4. The first line, `StartupLine`, at Info.

`FlatCheckCli.Run` configures nothing, so the tests can call it in-process. The window's test
fixture builds a plain `Application`, not `App`, for the same reason.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `flatcheck.log`, then past `FileSizeLimit`
`flatcheck_01.log`, `_02`, and so on (`ArchiveSuffixFormat`'s default `_{0:00}`; past 99 the
number simply grows). A file ends one line past the limit (1049286 bytes for 4 KB lines). After
8 MiB written the folder holds `flatcheck_03.log` to `flatcheck_07.log`, and the next process
appends to `flatcheck_07.log`: the same five names, the newest grown
(`R109_TheLogRollsAt1MiBAndKeepsAtMost5Files`). The window's files, rolled once, survive the
console's eight rolls in the same folder (`R109_OneExecutablesRollingLeavesTheOthersFilesAlone`):
NLog matches its own name and number only.

In one process two factories on one file stand for two processes, each on a thread of its own
released by a `Barrier` so they do overlap (their lines interleave both ways round):
`R112_TwoProcessesWritingAtFullSpeedKeepEveryLine` writes 4000 lines from each, crossing the
1 MiB roll (two files, the first ending at 1048936 bytes), and asks for every line, whole, once.

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the
level padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant
numbers with six significant digits, `none` for a missing value, sizes in KiB and MiB, an
elapsed time as `Ms`, `1141 ms`, or `Seconds`, `12.9 s`, never whole seconds) and logged as
plain strings, so NLog never parses them as message templates and a `{` in a path is harmless.
A line whose message costs something to build is guarded by `IsDebugEnabled` /
`IsTraceEnabled`. A channel without a colour (no `BAYERPAT`, or mono) is written by its label
(`CFA[0,0]`, `mono`), never by the analyzer's `'\0'`, which would put a NUL byte in the file.

Every logger is a `Logger` named `Log`, taken from `LogManager.GetLogger` with `using NLog`
(`NoLogLineGoesThroughTheTranslation` checks it, so its scan of the calls sees them all): a
`static readonly` field everywhere but in three places. A tab's is an instance field, named
after the tab (below); `LogSetup`'s and `App`'s are properties fetched on use, because those two
types run before the configuration is set and a static logger there would make NLog load a
stray `NLog.config` (step 2 above). Loggers are named, not taken from the stack: the class name for the core
(`ImageLoader`, `FitsReader`, `RawReader`, `PreviewReader`, `ProfileLocator`, `LensfunReader`,
`LensCatalog`, `LensIdentification`, every run in `Runs/`), and for the applications `Process`
(the first and last lines, the crashes), `Cli`, and the window's names from `Loggers`: `App`,
`Window` (the main view-model, the main window, the dialogs), `Window.<Tab>` (each tab's own,
an instance field the base class gives it from its type's name, so a tab's lines carry no
prefix), `Siril` (the installation, the script runs, siril-cli's output) and `Split` (its temp
folder).

## A run's lines

`RunClock` writes a run's start line and, through `Done`, its done line with the stages timed by
`Lap`. It is held in a `using`: a run that throws leaves the clock without `Done`, and `Dispose`
writes `<run> ended without an outcome after N ms (<stages>)`, so the inputs and the stages
reached are in the log beside the exception its caller logs. The clock starts **before** the
load in every run, the path overloads of `SmoothRun` and `SyntheticRun` included (a private
body takes the clock from either overload, so a run has one start line): a refusal decided after
the load, a raw or a cube for `--smooth`, a lens without a vignetting profile for `--synthetic`
(ADR 0071), is a done line `refused: <why>` with the load stage, and a file that does not load
still leaves the start line. A light series times `find` and the first frame's `load` as stages
of their own, and the first frame's own line counts that load as the others count theirs.

## What is logged where

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` | Info, Fatal | the first line, the last line; the unhandled exceptions |
| `FlatCheckCli` | Info, Warn, Error | `mode=<m> input=<path> lang=<l>` (the arguments are in the first line already); every refusal that is not an exception, with the user's message as shown; `run failed` with the exception; `--lenses` as a run with `locate` and `list` stages; every file written with its size |
| `ImageLoader` | Info, Debug | `loaded <path> (<MiB>) reader=... <W>x<H> planes bitpix cfa scale camera focal fnumber lens range preview in <ms>`; the FITS keywords that decide the reading (`LogText.FitsKeys`) at Debug, for a series' first frame only (`LoadImage(path, logHeader: false)` for the others, which come from the same session); the metadata reads at Debug |
| `FitsReader` | Trace | every card |
| `RawReader` | Debug, Warn | libraw's visible and raw sizes, margins, filters, maximum, black and per-channel black, thumbnail count; the lens block and the preview capture that failed |
| `PreviewReader` | Warn | a preview that did not decode, with the exception |
| `ProfileLocator`, `LensfunReader`, `LensCatalog` | Info, Warn, Debug | the folders searched (missing ones marked), the counts, lensfun's version and the lenses of each source, with the time; an Adobe root, or the lensfun folder, that could not be listed (a subfolder of a root without list rights is skipped by the enumeration itself, `lens-profiles.md`); an Adobe file that could not be read, and one that parsed to no vignetting profile (Debug) |
| `LensIdentification` | Debug | `identified` with the focal length, the f-number and the crop and where each came from, the raw's lens and the candidates by catalogue key; `mount of <camera> unknown to lensfun` when a lensfun copy was read but its camera table gives the body no mount, so the camera's first word alone filtered the candidates (R-116) |
| `Runs/` | Info, Debug, Warn | through `RunClock`, as above; at Debug the result's figures (`LogText.Structure`, `Comparison`, `Light`, `LightSeries`, `Exposure`, `HistEq`, `Smooth`, `Advice`, `Synthetic`, `Against`, `Split`), one line per frame of a series; the raw beside the master that could not be read |
| `App`, `MainWindow` | Info, Warn | the language and the folder from the command line, a `--lang` refused; the folder dialog, About, the Siril run asked and its end, the answer to the close question during a split |
| `MainViewModel` | Info, Error, Debug | the session usable or refused with the problem; an opening that failed past the check (a folder that cannot be listed), at Error, with the card it shows; at Debug, after the card or the tabs are up, the master (and an older one ignored), the counts of flats, raw flats and lights (`unreadable (<Type>)` for a folder that cannot be listed), the reference raw; a chain cancelled while it waited for the gate |
| `TabViewModel` | Info, Error, Debug | `run <n> started`, `done in` or `refused in` (a run in which `Refuse` ran; a failure the tab worded itself through `Fail` is "done", its own line said what happened), `cancelled after`; `failed` with the exception; a superseded run's failure at Debug; `Refuse`: `refused: <why>; the tab shows "<title>"` for the refusals a tab decides itself |
| the tabs | Info, Warn, Debug | every file written, one line each with its size; a write or a save that failed; an overwrite the user declined; the Synthetic prefill, and its cancellation; the Split phases, parts, failures and stop (times in seconds with one decimal); the conveniences that failed quietly (lens details, format note, free space, a file's size) at Debug |
| `SirilInstall`, `SirilScriptRun`, `SirilRunViewModel`, `PreprocessViewModel` | Info, Debug, Trace, Warn, Error | where siril-cli, the config and the scripts were found, a path that could not be normalised; the command line started, as `ProcessStartInfo` holds it, with the settings copy; every output line at Debug, the progress lines at Trace (built only when Trace is on); the end with the exit code, the time and the command reached, as the window shows it (the script's text, or the verb Siril printed when the script could not be read); a run that could not start; a script that could not be read |

## Reading a log

A process begins at its `Process <name> <version> starting` line and ends at `Process exit <code>
after <s>`; a crash ends instead at a `FATAL` line. Two processes writing at once interleave, and
the `[pid:thread]` field separates them. A run is found by its `<run> start` and `<run> done`
lines, whose stage times say where the time went, or its `ended without an outcome` line when it
threw; an error by `ERROR` and the stack under it. In the window each tab's run is numbered under
the tab's own logger, so a run that a newer one replaced can be told apart.

## In the tests

`LogCapture` configures NLog once per test assembly, through a module initializer, with one target
at Trace that renders every event: every test thus runs every log line on its path, and a line
that throws fails the test that reaches it. A test that reads the lines opens a capture with
`LogCapture.Start`, an `AsyncLocal` scope like `Ui.Use`, which sees only its own call tree, so
the tests running beside it add nothing; one capture at a time per call tree. The file tests
never touch that configuration: they build the applications' configuration with
`LogSetup.Build` into a private `LogFactory`, which stands for one process.
`UnlistableFolder` makes a folder the current user may not list, for the tests of what an
unreadable folder does (Windows only; the test returns elsewhere).
