# The diagnostic log

> Added 2026-10-31 with ADR 0069 (R-170 to R-173), after FlatCheck's log (its ADRs 0070 and
> 0072) as DarksCheck took it (its ADR 0025). **Covers:** `AstroPhotoAnalyzer.Core/Logging/LogSetup.cs`,
> `AstroPhotoAnalyzer.Core/Logging/LogText.cs`, `AstroPhotoAnalyzer.Core/Logging/RunClock.cs`,
> `AstroPhotoAnalyzer/Program.cs`, `AstroPhotoAnalyzer.Wpf/App.xaml.cs`,
> `AstroPhotoAnalyzer.Wpf/Loggers.cs`, `AstroPhotoAnalyzer.Tests/LogCapture.cs`,
> `AstroPhotoAnalyzer.Tests/LogFiles.cs`, `AstroPhotoAnalyzer.Tests/LoggingTests.cs`,
> `AstroPhotoAnalyzer.Wpf.Tests/WindowLoggingTests.cs`, and the log lines of the seams the table
> below lists. A change to the code this describes means this note is updated in the same task,
> or deleted with the decision it explains. The decisions and the figures (the package, the
> name of the level variable, where the core logs, the figures' rule, the measured cost) are
> ADR 0069's; this note is the mechanics.

## How the parts fit

`LogSetup.Apply` is the first call of `Program.Main` and of `App`'s constructor, each in a `try`
that swallows anything it throws, because the log never stops the tool; `LogSetup.Close` is the
last of `Main` (in a `finally`) and of `App.OnExit`, guarded the same way. `Apply` runs once per
process:

1. `Plan` decides the place without configuring anything: the level from `APA_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the
   entry name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable
   (`AppContext.BaseDirectory`, which is the executable's own folder for the single-file
   console too), a probe file in it, `<name>.probe-<pid>-<guid>.tmp`, opened with
   `DeleteOnClose`, and opens for append the file NLog will write next when there is one
   (`NewestLog`: the highest-numbered `<name>_NN.log`, else `<name>.log`), with the share mode
   that lets another process of the same executable hold it open; when any of these fails it
   does the same under `%LOCALAPPDATA%\AstroPhotoAnalyzer\log`, beside the window's
   `settings.json`.
2. `Build` makes the configuration, one `AtomicFileTarget` and one rule from the level up, and
   `Apply` sets it **before any logger exists**: `LogSetup`'s own logger and `App`'s are
   properties fetched on use, not static fields, because creating a logger makes NLog look for
   an `NLog.config` beside the executable (FlatCheck measured a broken one killing the process
   in `TypeInitializationException` before `Main`). `Off` gives a configuration without
   targets, and `Apply` returns there.
3. The hooks: `AppDomain.UnhandledException` (Fatal, flushed) and
   `TaskScheduler.UnobservedTaskException` (Error); the window adds its
   `DispatcherUnhandledException` in `App`'s constructor. `LogSetup.Fatal` names an exception it
   already wrote instead of writing it twice; `LogSetup.Wrote` marks an exception a line wrote
   with its stack, so `Unobserved` names it later.
4. The first line, `StartupLine`, at Info.

`AnalyzerCli.Run` configures nothing, so the tests and the golden tests call it in-process. The
window's test fixture (`StaFixture`) builds a plain `Application`, not `App`, for the same
reason. The console's `Cli` logger is a static field of `AnalyzerCli`, a type first used after
`Main` called `Apply`.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `AstroPhotoAnalyzer.log`, then past
`FileSizeLimit` `AstroPhotoAnalyzer_01.log`, `_02`, and so on. A file ends one line past the
limit (1049286 bytes for 4 KB lines). After 8 MiB written the folder holds
`AstroPhotoAnalyzer_03.log` to `AstroPhotoAnalyzer_07.log`, and the next process appends to
`AstroPhotoAnalyzer_07.log` (`R170_TheLogRollsAt1MiBAndKeepsAtMost5Files`). The console's name
begins the window's: in one folder the window's files survive the console's eight rolls and the
console's survive the window's, and `NewestLog` takes only a number after the underscore
(`R170_OneExecutablesRollingLeavesTheOthersFilesAlone`).

Two factories on one file stand for two processes, each on a thread of its own released by a
`Barrier` so they do overlap: `LogFiles.TwoWritersKeepEveryLine` writes 4000 lines from each,
crossing the 1 MiB roll, and asks for every line, whole, once, interleaved both ways round. It
runs twice: in the core's tests on the `net8.0` build of `NLog.Targets.AtomicFile`, the
console's, and in the window's tests on the `net8.0-windows7.0` build, the window's, which a
`TargetPlatformAttribute` check pins (`R173_...`). The two files' sizes vary by a few bytes from
run to run (the process id is on every line).

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the level
padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant
numbers with six significant digits, `none` for a missing value, sizes in KiB and MiB, an
elapsed time as `Ms` or `Seconds`) and logged as plain strings, so NLog never parses them as
message templates and a `{` in a path is harmless. A Debug or Trace line whose message costs
something to build is guarded by `IsDebugEnabled` / `IsTraceEnabled`; the Info lines are built
always, and cheap. `LogText.Str`, which every stored text goes through (a FITS value, an ICC
profile's name, a record's text, a recommendation's text and because, a profile's problem), and
the Trace line of a FITS card write a control character (C0, DEL, C1) or a Unicode line or
paragraph separator as `?` (`LogText.Printable`), so a damaged file breaks no line and puts no
control byte in the log.

`LogText.Figures(record, skip...)` writes a result record by one rule: every instance property,
then every field, that is not private (so neither a record's `EqualityContract`), in declaration
order, as `camelCaseName=value`; a property whose getter is written by hand and gives a text or
texts is left out, because on this core every such getter is a sentence through `Tr.Pick`; a
delegate is left out; a value of a type from another assembly than the record's (an ImageSharp
or Unicolour object) is its type's name in angle brackets; a list of at most 24 items is written
whole, a longer one as `[first, ..., last] (n items)`; a record held by the record is written in
parentheses, six deep at most; a getter that throws is `?`. The names in `skip` are left out at
every depth: `AnalysisRun` skips the texts a record stores in the user's language (`Name` of the
regions, `Axis` of the crop, `Compression` and `ReaderNote` of the file, on the loaded line
already) and `DmaxProfileNote` of the parameters, so the figures read the same in both
languages.

Every logger is a `Logger` named `Log`, taken from `LogManager.GetLogger` with `using NLog`: a
`static readonly` field everywhere but in `LogSetup` and `App`, where it is a property fetched on
use. Loggers are named, not taken from the stack: the class name in the core (`ImageLoader`,
`FitsReader`, `ImageSharpReader`, `IccPaperBlack`, `AnalysisRun`, `CurveRun`, `CurveExport`,
`ProofRun`), `Process` for the first and last lines and the crashes, `Cli` for the console, and
the window's four from `Loggers`: `App`, `Window`, `Window.Curves`, `Window.SoftProof`.

## A run's lines

`RunClock` writes a run's start line and, through `Done`, its done line with the stages timed by
`Lap`. It is held in a `using`: a run that throws leaves the clock without `Done`, and `Dispose`
writes `<run> ended without an outcome after N ms (<stages>)`, so the inputs and the stages
reached are in the log beside the exception its caller logs. Where the core rewords an
exception for the user and drops the original (a file ImageSharp cannot identify, a `--wcs` file
that cannot be read, a save the system refuses over the input), it writes the original with its
stack at Warn where it catches it and throws the user's exception without an inner one: the
window's `Describe` joins every inner message into what it shows, and an inner exception added a
paragraph there that the user never saw before the log (the review of 2026-10-31).
`AtomicFile.Write`, which rewords a temporary file it cannot create, keeps the original as the
inner exception instead, since `Input/AtomicFile.cs` holds no logger: its caller logs the
exception with both stacks, and `Describe` skips an inner message the outer one already holds. `AnalysisRun.Execute` starts its
clock before the load (both overloads, one private body, `Run`), so a file that does not load
still leaves the start line; a linear image is a done line `refused: ...` with its median, then
the throw. `CurveRun.Execute` and `ProofRun.Prepare` write their refusals (a problem, no profile)
as done lines too, as the tab shows them.

## What is logged where

| Seam | Level | What |
| --- | --- | --- |
| `LogSetup` | Info, Fatal, Error | the first line (version, arguments, folders, level, machine, runtime, culture, time zone, process id), the last line (exit code, elapsed time); the unhandled exceptions; a task nobody awaited |
| `AnalyzerCli` | Info, Warn, Error | `mode=help`, or `mode=analysis image=... out=... lang=...`; every refusal as stderr shows it, the linear image included; `run failed: mode=analysis` with the exception; the report written with its size; a report that could not be written, with the exception |
| `ImageLoader` | Info | `loaded <path> (<MiB>) reader=<type> <W>x<H> planes=N` and what the reader recorded (`LogText.Source`: format, bit depth, float, planes in the file, compression, ICC profile, WCS, note), with the time |
| `FitsReader` | Debug, Trace | the keywords that decide the reading and the WCS (`LogText.FitsKeys`) for the analysed FITS and for a `--wcs` file; every card at Trace, HISTORY and COMMENT included |
| `ImageSharpReader` | Debug, Warn | what `Image.Identify` found (format, size, bits per pixel, frames) and the facts read from it; a file it cannot identify, with ImageSharp's exception |
| `IccPaperBlack` | Info, Warn, Debug | one line per profile read with its figures or its problem; a file that cannot be read, and the package's exception on a damaged table, with the exception; a folder listed with its counts and time, a folder that cannot be listed, a file skipped |
| `AnalysisRun` | Info, Debug, Warn | the clock `analysis`, inputs the path and every parameter, stages `load`, `wcs`, `file`, `tones`, `simulation`, `noise`, `background`, `stars`, `size`, `quantization`, `jpeg`, `recommendations`, `report`, done `verdict=... recommendations=N <W>x<H> workingSet=...`; at Debug the WCS when there is one, every result's figures as soon as it is measured, one line per recommendation with its text and because as shown; a `--wcs` file that cannot be read, with the reader's exception |
| `CurveRun`, `CurveExport` | Info, Debug, Warn | the clock `curves`, stages `anchors`, `gamut`, `suggestions`; the anchors with the method and the class levels, every suggestion's points and evidence; a gamut table that cannot be built, with `GamutTable.Cause`; every FITS written with its size; a save over the input the system refuses, with the system's exception |
| `ProofRun` | Info, Debug, Warn | the clock `proof`, stages `profile`, `gamut`, `mask`, done with the share outside the paper's gamut and the profile's figures; a profile or a table that cannot be used, with `ProofProfile.Cause` or `GamutTable.Cause`; each render at Debug with its intent and time |
| `App` | Info, Warn | the language and the file from the command line; a `--lang` refused |
| `MainViewModel` (`Window`) | Info, Warn, Error | `run N started`, `done in` with the verdict, `refused after` (a linear image), `failed after` with the exception; a field refused; an exception that leaves the run (`LogUnhandled`, an exception filter, marked with `LogSetup.Wrote`); the profile restored, in use, cleared; the report written, or not with the exception |
| `MainWindow`, `IccPickerWindow` (`Window`) | Info | each dialog's answer, the buttons, the secondary windows opened |
| `WindowSettings`, `DarkTitleBar`, `AboutWindow` (`Window`) | Warn, Debug | a settings file that cannot be read or written, with the exception; each write at Debug; a title bar that cannot be darkened; a link that cannot be opened, by its `OriginalString` |
| `CurvesTab` (`Window.Curves`) | Error, Warn, Debug | a computation that failed, with the exception; one a newer computation superseded; a save that failed |
| `SoftProofTab` (`Window.SoftProof`) | Info, Error, Warn, Debug | the intent, the paper gamut warning, the original and the margin when changed; a computation or a render that failed, a table the core refuses, one superseded |

## Reading a log

A process begins at its `Process <name> <version> starting` line and ends at `Process exit <code>
after <s>`; a crash ends instead at a `FATAL` line. Two processes writing at once interleave,
and the `[pid:thread]` field separates them. A run is found by its `analysis start` and `analysis
done` lines, whose stage times say where the time went, or its `ended without an outcome` line
when it threw; an error by `ERROR` and the stack under it. In the window the runs are numbered
from 1 under `Window`, and the curves and the proof follow each run on the pool's threads, under
the core's loggers.

## In the tests

`LogCapture` configures NLog once per test assembly, through a module initializer, with one
target at Trace that renders every event and `ThrowExceptions` on: every test thus runs every
log line on its path, and a line that throws fails the test that reaches it. A test that reads
the lines opens a capture with `LogCapture.Start`, an `AsyncLocal` scope like `Tr.Use`, which
sees only its own call tree, so the tests running beside it add nothing. The file tests never
touch that configuration: they build the applications' configuration with `LogSetup.Build` into
a private `LogFactory`, which stands for one process. Both helpers are linked into the window's
test project. `R171_TheRealConsoleWritesItsFirstAndItsLastLine` and
`R172_TheRealConsoleTakesItsLevelFromAPA_LOG` start the built console beside the tests and read
its lines, by process id, from the `log` folder of the test's output, which other tests'
processes share. Three invariants scan the sources: `OnlyTheTwoEntryPointsConfigureTheLog`,
`NoLogLineGoesThroughTheTranslation` (no `Tr.Pick` in a log call, every logger declared one
way) and `NothingThatComputesOrWordsTheReportLogs` (no NLog name in `Analysis/`, `Report/`,
`Thresholds.cs`, `Parameters.cs`, `Language.cs`, and of `Input/` in the four readers only).
