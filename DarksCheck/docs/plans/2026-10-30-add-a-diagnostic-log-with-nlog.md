# Add a diagnostic log with NLog

Plan for the task branch `claude/Add-a-diagnostic-log-with-NLog`, worktree
`.claude/worktrees/Add-a-diagnostic-log-with-NLog`. Written on 2026-10-30 for an implementer
who has this file, `CLAUDE.md`, the repository and read access to FlatCheck
(`E:\Photografie\Code\FlatCheck`, `main` at 9511bfd or later), and nothing else from the
planning session. Approved by the owner on 2026-10-30 as written, every row as recommended
("Approve as written"); this file is the first commit of the branch.

## Context

A user reports "it failed" or "the verdict is wrong" and all we have is the screen. The console
prints `ERROR: <message>` without the exception's type, its stack or its inner exceptions
(`src/DarksCheck.Cli/Program.cs`, the catch at the end of `RunInLanguage`); the window puts
the same message on the problem card (`MainViewModel.OpenAsync`) and drops the stack. Nothing
records which files were read, which frames failed to decode and why, how much memory the
decoding took (the 14 GB working-set trap of ADR 0007 was found by hand), which figures led to
a verdict, which options and partner the run had, or how long each stage took. Several places
swallow an exception on purpose and leave no trace: a frame that cannot be read becomes a
message in a record (`FrameInfo.Read`, `FrameStack.Load`, `LightsAnalyzer.Analyze`), the
settings file that cannot be read gives the defaults (`UserSettings.Load`), the clipboard held
by another process does nothing (`MainWindow.CopyLabel`), the title bar stays light
(`DarkTitleBar.Apply`), a link that does not open (`AboutWindow`). Nothing in the three
projects logs today.

The owner's ask (2026-10-30): a log, with NLog, text format, in `log/` beside the executable,
the file names starting with the executable's name, at most 5 files per executable, rolling, at
most 1 MB each; "like FlatCheck"; what is logged is the implementer's call, with one criterion:
a bug report with the log attached can be diagnosed from the log.

## What FlatCheck did, and what is taken from it

FlatCheck built the same log on 2026-10-27 (its ADR 0070, requirements R-109 to R-112, plan
`docs/plans/2026-10-27-add-a-diagnostic-log-with-nlog.md`), then fixed the 42 findings of its
review (its ADR 0072, plan `docs/plans/2026-10-27-fix-the-diagnostic-log-review-findings.md`),
and its design note `docs/design/logging.md` holds the mechanics and the measurements. Its ADR
0070 ends with "The other tools of the suite can take `LogSetup` as it is, under their own
names." This plan does that. The implementer reads those four FlatCheck documents and these
FlatCheck files before writing anything:

- `FlatCheck.Core/Logging/LogSetup.cs`, `LogText.cs`, `RunClock.cs`: the configuration, the
  words, a run's two lines. Taken whole, rewritten in DarksCheck's style (below).
- `FlatCheck/Program.cs`, `FlatCheck.Wpf/App.xaml.cs`, `FlatCheck.Wpf/Loggers.cs`: the two
  entry points and the window's logger names.
- `FlatCheck.Tests/LogCapture.cs`, `LoggingTests.cs`, `FlatCheck.Wpf.Tests/WindowLoggingTests.cs`:
  the capture and the tests; the test names below map to FlatCheck's.
- `FlatCheck/THIRD-PARTY-NOTICES.md`, section `## NLog`; `FlatCheck.Core/ThirdParty.cs`, the
  NLog component; `Directory.Packages.props`, the two package comments; `README.md`, `## Logs`.

What FlatCheck measured is not measured again, and is cited as FlatCheck's: the plain file
target lost 33 to 48 lines of 4000 closed between lines and 67 to 72 kept open, the atomic
target none, at 0.09 ms a line; `MaxArchiveFiles` counts the file being written (set to 4, it
left 4 files); a logger held in a static field of the type that calls `Apply` makes NLog load a
stray `NLog.config` and kills the process before `Main`. The two-process test below re-measures
the first on DarksCheck's build, as a test, because a figure a requirement states must hold
here too (the kit's rule "Measured numbers").

Three things are DarksCheck's own and differ from FlatCheck, each a decision below: the readers
are copies, two of them refreshed from LightsCheck, which is taking a log of its own the same
day (D3), the decoding loops live in `Analysis/` (D4), and the per-frame figures (D8).

## The owner's decisions

Each row comes with a recommendation (the kit's rule "Questions to the owner"); the plan is
written as if every recommendation were taken, so an approval "as written" settles every row.

| # | Decision | Recommendation and why |
| --- | --- | --- |
| D1 | Where NLog lives | As FlatCheck's ADR 0070: the core references the packages and logs at its seams; each application configures NLog **in code** at its entry point through `LogSetup.Apply` first and `LogSetup.Close` last, in a `try` that swallows anything, since the log never stops the tool. No `NLog.config`: nothing to ship, nothing the user can break. Without a configuration (a library use) every call is a no-op; the test assemblies configure a capture at Trace. |
| D2 | A second package, `NLog.Targets.AtomicFile` 6.2.2 (the kit's rule "Packages": no new package without asking) | **Take it**, at NLog's version, as FlatCheck did on the owner's word of 2026-10-27: two windows opened on two sessions write one `DarksCheck.Wpf.log`, and NLog's plain target lost lines either way (FlatCheck's measurement above). Same project, same commit, same BSD-3-Clause licence, no native file. The owner asked for NLog by name; this is NLog's own file target. |
| D3 | The readers under `src/DarksCheck.Core/Imaging/` are copies (ADR 0003, the rule "Copied files stay copies"); `RawReader.cs` and `ImageLoader.cs` carry LightsCheck's adaptations and are refreshed from LightsCheck. New fact of 2026-10-30: the shared checkout holds an untracked plan of the same day for LightsCheck's own log (`LightsCheck/docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`, 11:12), whose decision L2 has `ImageLoader.cs` write the `loaded` line, the FITS keywords at Debug and every card at Trace, and `RawReader.cs` libraw's figures at Debug and its lens-block catch at Warn, while `FitsReader.cs` stays untouched | **LightsCheck's task first, then DarksCheck's step 3 refreshes `ImageLoader.cs` and `RawReader.cs` from LightsCheck's merged `main`**, log lines included, which is the rule as written (a change goes to the source, then the copy is refreshed); DarksCheck's `LogText` then carries the members those two files call, under LightsCheck's names (`Frame`, `FitsKeys`, whatever L2 settles on), and the invariant test lets those two files of `Imaging/` log and no other. DarksCheck's own callers write only what the readers do not know: the exception of a file that cannot be read, where `FrameInfo.Read` catches it; the decode's geometry, levels, time and working set, in the two loops (D4). If the owner runs the two tasks side by side instead, step 3 writes the readers' line in `FrameInfo.Read` itself (`read <path> ... camera=... exposure=... iso=... time=... lens=...`) and the FITS header at Trace from `LoadedFrame.Header` in `FrameStack`, and the later refresh from LightsCheck removes both again; say which in the ADR. The third way, FlatCheck's log hunk taken into `FitsReader.cs` here, is rejected as LightsCheck rejected it: the copy would change for a line its loader writes itself. |
| D4 | Two decoding loops live in `Analysis/`, where FlatCheck's rule says nothing logs: `FrameStack.Load` (the darks, the biases, the partner) and the loop in `LightsAnalyzer.Analyze` (the lights, through `FrameStack.DecodeOne`) | **The two loops log, live, under their files' loggers; the rest of `Analysis/` never does.** A frame that fails to decode is logged at Warn with its exception where it is caught, the only place the stack exists (the records keep the message alone); a decoded frame leaves one Debug line with its size, geometry, levels, time and the process's working set after the forced collection, so a report "it ate all my memory" or a crash in the middle of a group says which frame, how far, and how much memory. The alternative, records that carry the exception and a run that logs after the group, loses the live lines and the per-frame memory, which is the figure ADR 0007's trap needed. The invariant test names the two files as the only ones of `Analysis/` with a logger; the maths of both files (`ToUshort`, `Measure`, the statistics) hold no log line, and `/mutate`'s string mutants in the log text are survivors to expect, not tests to write. |
| D5 | The default level and how to change it | **Debug**, set by the environment variable `DARKSCHECK_LOG` = `off`, `fatal`, `error`, `warn`, `info`, `debug`, `trace`, in any case; anything else means Debug. No console option, so `Options`, the usage and the golden files do not move (as FlatCheck). |
| D6 | The folder when `log` beside the executable cannot be written (Program Files, a read-only drive) | `%LOCALAPPDATA%\DarksCheck\log`, beside the window's `settings.json`, and the first line says so. A log that silently does not exist defeats the purpose. |
| D7 | What Trace adds | Every FITS header card, as `ImageLoader.cs` writes them after the refresh of D3 (one line per card, for every FITS read, the metadata reads of the inventory included); every raw's libraw figures are at Debug already. Trace is otherwise Debug: nothing else in DarksCheck is bulk. The inventory reads every frame's header, so a Trace log of a 50-frame FITS session is long; that is what Trace is for, and Debug stays the default. |
| D8 | The per-frame figures | **One Debug line per dark, bias and light with its figures** (`DarkFrameFigures`, `BiasFrameFigures`, `LightFigures`), beside the one line per decoded frame of D4: a wrong verdict is diagnosed from the frames' figures, and the tables of the report are not in the log otherwise. Cost: about 110 frames of 200 to 300 bytes each on the Smida session, 30 to 45 KB a run with the decode lines; a 1 MiB file holds 25 to 30 runs, the five files 120 or more. Measured in step 7 and written into the ADR. |

## The design

### The files

New, in the core, namespace `DarksCheck.Core`:

- `src/DarksCheck.Core/Logging/LogSetup.cs`: `LogPlace` (record), `LogSetup` (static): the
  constants `FileSizeLimit` (1 MiB), `FilesPerExecutable` (5), `LevelVariable`
  (`DARKSCHECK_LOG`), `FolderName` (`log`), `FallbackFolder` (`DarksCheck`), `DefaultLevel`
  (Debug), `LineLayout`; the methods `Apply(string entryName, IReadOnlyList<string> args)`,
  `Plan(...)`, `Close(int exitCode)`, `Fatal(Exception? e, string where)`,
  `Build(string executableName, string? folder, LogLevel minimum)`, `ResolveFolder(...)`,
  `CanWrite(...)` (the probe file plus the append probe on the newest file), `NewestLog(...)`,
  `LevelFrom(string? value)`, `ExecutableName(string? processPath, string entryName)`,
  `StartupLine(...)`. FlatCheck's file, member for member; its own logger a property fetched on
  use, never a static field (FlatCheck's measurement); `LogManager.ThrowExceptions = false`;
  the configuration set before any logger exists; `Off` gives a configuration without targets
  and `Apply` returns before creating the folder or hooking anything.
- `src/DarksCheck.Core/Logging/LogText.cs`: `Num`, `Int`, `Ms`, `Seconds`, `Str`, `MiB`,
  `KiB`, `FileMiB`, `FileKiB`, `List`, `Yes` as FlatCheck's, then DarksCheck's own lines (the
  table "What is logged where" says which); invariant culture, six significant digits, `none`
  for a missing value, a verdict by its enum name (`Verdict`, `MatchVerdict`, `LeakCause`,
  `MapRegion`, `MapSide`), never through `T(english, romanian)`.
- `src/DarksCheck.Core/Logging/RunClock.cs`: FlatCheck's, unchanged in behaviour: `Start(Logger,
  run, inputs)` writes `<run> start <inputs>` at Info; `Lap(stage)`; `Done(outcome)` writes
  `<run> done <outcome> in N ms (stage N ms, ...)`; `Dispose` without `Done` writes `<run> ended
  without an outcome after N ms (<stages>)`, so a run that throws leaves its stages beside the
  exception its caller logs. Held in a `using`.

New, in the window: `src/DarksCheck.Wpf/Loggers.cs`, the names `App` and `Window`, as
constants, so every class of the window takes its logger from one place.

New, in the tests: `tests/DarksCheck.Tests/LogCapture.cs` (linked into
`DarksCheck.Wpf.Tests.csproj` as the other shared files are), `tests/DarksCheck.Tests/LoggingTests.cs`,
`tests/DarksCheck.Wpf.Tests/WindowLoggingTests.cs`.

Style: DarksCheck's, not FlatCheck's. Nullable is enabled in the core (`string? Folder`,
`Exception? e`), namespaces are file-scoped, every `if` has braces, `var` only when the type is
apparent, the GPL header on every file (`LicenseHeaderTests`), an XML documentation comment on
every type and method (the kit's rule "Documentation comments"), English comments that say what
the member does here. The measured constants and the mechanics do not change. The WPF project
has no implicit `System.IO`, so `System.IO.Path` is written out there.

### The configuration

One `AtomicFileTarget` named `file`: `FileName` = `<folder>\<executableName>.log` as a literal
layout, `Layout` = `LineLayout`, UTF-8 without BOM (`Encoding = new UTF8Encoding(false)`,
`WriteBom = false`), `ArchiveAboveSize = FileSizeLimit`, `MaxArchiveFiles = FilesPerExecutable`
(NLog 6.2.2 counts the file being written; FlatCheck measured it), `ConcurrentWrites = true`,
`KeepFileOpen = true`, `AutoFlush = true`, `CreateDirs = true`; one rule from the level to
Fatal for every logger; `DefaultCultureInfo = CultureInfo.InvariantCulture`; no console target
ever (stdout and stderr are the console's contract, R-001 to R-005, and the golden files pin
them). The executable's name comes from `Environment.ProcessPath` (`darkscheck`,
`DarksCheck.Wpf`), the entry name when the path is the `dotnet` host or unknown. NLog 6's
rolling: `darkscheck.log`, then past the limit `darkscheck_01.log`, `_02`, ...; the oldest
deleted past five; the next process appends to the newest. One executable's rolling matches
its own name and number only, so the console's and the window's files share the folder.

### The line

`${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}`
and, when there is an exception, `${newline}${exception:format=ToString}`: type, message, stack
and inner exceptions on the lines below. Messages are composed from `LogText` and passed as one
plain string (`Log.Info(string)`, `Log.Warn(Exception, string)`), never as a template with
arguments, so a `{` in a path is harmless. A message that costs something to build (a frame's
figures, a header) is guarded by `IsDebugEnabled` / `IsTraceEnabled`. Every logger is a
`Logger` field named `Log` taken from `LogManager.GetLogger` with `using NLog`, `static
readonly` everywhere but in `LogSetup` and `App`, where it is a property fetched on use; the
invariant test scans for that shape. Logger names are the class's for the core (`FrameInfo`,
`InventoryRun`, `FrameStack`, `LightsAnalyzer`, `DarkSeriesRun`, `DarkOutputs`), `Process` for
the first and last lines and the crashes, `Cli` for the console, and `Loggers.App` and
`Loggers.Window` for the window.

### The two entry points

`Program.Main` becomes: `try { LogSetup.Apply("darkscheck", args); } catch (Exception) { }`;
`int code = 1; try { code = Run(args, Console.Out, Console.Error); return code; } finally { try {
LogSetup.Close(code); } catch (Exception) { } }`. `Program.Run` configures nothing: the tests
call it in-process (`Cli.Run` in `AcceptanceTests`), and so do the golden tests.

`App()` becomes: the arguments as today; `try { LogSetup.Apply("DarksCheck.Wpf", args); } catch
(Exception) { }`; `DispatcherUnhandledException += (s, e) => LogSetup.Fatal(e.Exception,
"unhandled exception on the window's thread, the window closes")`; then the language as today.
`OnExit` calls `LogSetup.Close(e.ApplicationExitCode)` in the same guard. `App`'s own logger is
a property fetched on use (the stray `NLog.config` trap). The window's tests build a plain
`Application` (`StaFixture`), not `App`, so they never call `Apply`.

`LogSetup.Apply` hooks `AppDomain.CurrentDomain.UnhandledException` (Fatal, flushed) and
`TaskScheduler.UnobservedTaskException` (Error); `Fatal` names an exception it already wrote
instead of writing it twice (the window's dispatcher hook, then the process's).

### What is logged where

| Seam | Logger | Level | What |
| --- | --- | --- | --- |
| `LogSetup` | `Process` | Info, Fatal | The first line: `darkscheck 1.0.0 starting; exe=...; args=["..."]; cwd=...; log=<folder> (fallback: ...)?; level=Debug; os=...; cpus=N; memory=N GiB; runtime=.NET 8...; culture=en-US/en-US; tz=... (UTC+02:00); pid=N`. The last line: `exit N after N.N s`. The unhandled exceptions. |
| `Program` (console) | `Cli` | Info, Warn, Error | `mode=inventory|darks input=<folder> against=<folder|none> out=<folder|none> threads=<N|auto> maxFrames=N lang=en|ro` once the options are parsed; `refused: <message as shown>` at Warn for a bad `--lang`, an `Options.Error`, a refused `--out` (`CheckOutDir`'s exception, which is also the run's failure: log it once, at Warn, with the exception); `run failed: mode=<m>` at Error with the exception (an `AggregateException` whole: `ToString` lists the inner ones) when the catch at the end of `RunInLanguage` fires; `wrote <path> (<KiB>)` per file `DarkOutputs.WriteAll` returns, one line each. Nothing more on stdout or stderr. |
| `ImageLoader.cs`, `RawReader.cs` (LightsCheck's adaptations, refreshed in step 3, D3) | their class names | Info, Debug, Trace, Warn | As LightsCheck's L2 writes them: `loaded <path> (<MiB>) reader=... <W>x<H> ... camera=... focal=... lens=...` at Info for a decode and `read the header of ...` at Debug for a metadata read, the FITS keywords at Debug and every card at Trace, libraw's visible and raw sizes, margins, filters, maximum and blacks at Debug, the lens block that could not be read at Warn with the exception. Not DarksCheck's to word: they come with the refresh. |
| `FrameInfo.Read` | `FrameInfo` | Warn | `cannot read <path>: <message>` **with the exception** for a file that does not read (this catch is the only place the stack exists); `not a FITS file: <path>` without an exception. Nothing for a file that reads: `ImageLoader` wrote its line. |
| `InventoryRun.Run` | `InventoryRun` | Info, Debug | A `RunClock` named `inventory` started before `SessionFolder.Open`, with `folder=<path>` as its inputs; stages `open`, `darks`, `biases`, `lights`, `cadence`; done line `inventory done darks=N biases=N lights=N errors=N`; at Debug, after the analyzers return: `session root=... darksDir=... biasesDir=... lightsDir=...`, `cadence darks: <LogText.Cadence>` and the lights' (every scalar of `Cadence`, the long pauses by frame and seconds), `match: <LogText.Match>` (each `GroupSettings`, the two verdicts by name). `ReadAll` itself logs nothing; `FrameInfo.Read` does. `DarkSeriesRun` calls `InventoryRun.Run` and gets its lines for free. |
| `FrameStack.Load` and `DecodeOne` | `FrameStack` | Info, Debug, Warn, Trace | Per decoded frame, at Debug: `decoded <path> (<MiB>) <W>x<H> black=<..> white=<..> (<source>) norm=<..> in <ms>; working set <MiB>` (`Environment.WorkingSet` after the forced collection); a frame kept out for its size or its settings: the record's message at Warn (no exception); the catch: `cannot decode <path>: <message>` at Warn with the exception; a skipped frame: nothing (the summary counts them). At the end, at Info: `stack of N files: D decoded, F failed, S skipped, <W>x<H> black=<..> white=<..> (<source>) norm=<..> in <ms>`, or `stack of N files: none decoded` with the first errors. `DecodeOne`, which the lights call, writes the same `decoded` line, so the lights' frames are logged the same way. The frame's metadata and the FITS header are `ImageLoader`'s lines (D3, D7), not repeated here. |
| `LightsAnalyzer.Analyze` (the loop only) | `LightsAnalyzer` | Warn, Debug | The catch: `cannot decode <path>: <message>` at Warn with the exception; a light measured: `light <index> <file>: <LogText.Light>` at Debug (every scalar of `LightFigures`, the lists `SkyMedian`, `SkySigma`, `LocalSigma` whole, they have one value per plane); `assumed settings` named in that line. `Measure` and the rest of the file hold no log line. |
| `DarkSeriesRun.Run` | `DarkSeriesRun` | Info, Debug, Warn | A `RunClock` named `darks` started first, inputs `folder=<path> threads=N maxFrames=N against=<path|none> isoFactor=<..> doubling=<..>`; stages `inventory`, `biases`, `darks`, `measure`, `verdicts`, `lights`, `partner` (only the stages reached); done line `darks done leak=<Verdict> drift=<..> noise=<..> pattern=<..> deadSat=<..> enough=<..> index=<..> label=<..> lightsIndex=<..|none> errors=<count of DarkSeriesResult.HasErrors's parts> frames darks=<decoded/total> biases=<..> lights=<..> partner=<..>`; a group that fails (`biasError`, `lightsError`, `partnerError`) at Warn with the exception where it is caught; the partner that stands in for the darks (R-054) at Info; at Debug after each analyzer: `series: <LogText.Series>` (width, height, black, white, source, norm, decoded/total, the masters' `MasterFigures`, `HotStability`, `NoiseVsN`'s scalars and the curve's points, `Warming`'s scalars, the `Regions` of each map, `BandingFigures`), one `frame <index> <file>: <LogText.DarkFrame>` per dark (every scalar of `DarkFrameFigures`; `PlaneFigures` and `ResidualFigures` per plane), `biases: <LogText.Biases>` and one line per bias, `darkCurrent: median=.. p1=.. p99=..`, `verdicts: <LogText.Verdicts>` (every scalar of the six verdict records, `LeakVerdict`'s `FrameLight`, `EdgeSplit`s and `SideLight`s included), `index: <LogText.Index>`, `lights: <LogText.Lights>` (the analysis's scalars, the overlap per level, the persistence histogram), `partner: <LogText.Partner>` (every scalar of `PartnerAnalysis`, the two `MatchFigures`). The rule for a figures line: every scalar property of the record by its name in camelCase, a list by its count and its items when it has at most 12 (the planes, the overlap levels, the noise curve), else its count and first and last. |
| `DarkOutputs.WriteAll`, `WriteMap` | `DarkOutputs` | Info | `wrote <path> (<KiB>)` per file written, one line each, in the core, so the console and the window get it once; a map not written (`MapRaster` null) at Debug with why. `CheckOutDir` logs nothing: its exception is logged by the caller. |
| `App` | `App` | Info, Warn | `language=en|ro folder from the command line: <path|none, the window asks for one>`; `refused: <BadValueMessage>; the window goes on in English` at Warn. |
| `MainViewModel` | `Window` | Info, Error, Debug | `run N started: folder=<path> options=<threads, maxFrames, isoFactor, doubling, against>` (N counts the opens of this process, from 1) in `OpenAsync` after `IsRunning = true`; `run N done in <ms>` after `Populate`; `run N cancelled after <ms>` in the `OperationCanceledException` catch; `run N failed after <ms>; the problem card shows the message` at Error with the exception, in the other catch, when the run is still current; `run N failed after a newer run started; nothing is shown` at Debug with the exception otherwise; `run N superseded` at Debug when a newer open cancels it; `partner=<path|none>` at Info when `Partner` changes; `export to <dir>: ` the written lines come from `DarkOutputs`, a failed export `export to <dir> failed` at Warn with the exception; `label copied` at Info. |
| `MainWindow` | `Window` | Info, Debug | `folder dialog: chose <path>` / `cancelled`; `partner dialog: ...`; `export dialog: ...`; `About opened`; `rerun asked`; `stop asked`; the clipboard `COMException` at Debug with the exception. |
| `UserSettings` | `Window` | Debug | `settings read from <path>` / `settings file could not be read; the defaults are used` with the exception; `settings could not be saved` with the exception. `UserSettings` is `public static` with a settable `Path` the tests use; the logger is a static field in it. |
| `DarkTitleBar`, `AboutWindow` | `Window` | Debug | `the title bar cannot be darkened on this system` with the exception; `the link <uri> could not be opened` with the exception. |

What is **not** logged, and the requirement says so: a cancellation the user asked for (`Stop`,
another folder, the window closing) is one Info line, not an exception; the `ObjectDisposedException`
of `CancelPending` at the window's close (a race, nothing to act on); the log's own probes,
whose outcome is the first line's `fallback` note. `Analysis/` except the two loops, `Report/`,
`Thresholds.cs`, `Language/` and the whole of `Imaging/` hold no logger and no `NLog` name.

The words: English, invariant numbers, whatever `--lang` says; a message the user was shown (an
exception's, a refusal's, a frame's error) stays as shown, so it can be in Romanian; a verdict
by its enum name; the phase names of `RunProgress` are not logged (they are translated), the
stage names of `RunClock` are the English ones above.

## The steps

Each step ends with `scripts/verify.ps1` green and one commit on the branch, message in
English, one line, at most 100 characters, saying why. No builds in the main checkout: every
`dotnet` command runs in the worktree.

0. **The worktree and the plan.** From the main checkout, on `main`, clean:
   `git worktree add .claude/worktrees/Add-a-diagnostic-log-with-NLog -b claude/Add-a-diagnostic-log-with-NLog main`.
   Copy this file into the worktree's `docs/plans/` (a worktree starts from the commit, not
   from the main checkout's untracked files) and commit it: "Plan the diagnostic log with NLog,
   as FlatCheck's, with the readers' copies left alone". Every later command runs in the
   worktree; the main checkout stays on `main` with this file untracked until the merge, when
   the owner deletes the copy there.

1. **Requirements and packages.** In `docs/requirements.md`, a new block after "The interface
   language", `## The diagnostic log`, with R-100 to R-103 as `accepted` (they turn
   `implemented` in step 7, when their tests exist: `RepoInvariantTests` refuses an
   `implemented` row without a test). Their texts, adapted from FlatCheck's R-109 to R-112 with
   DarksCheck's names, D3 and D4 written in:
   - R-100: the files. Each application writes a plain-text log, UTF-8, in a folder `log`
     beside its executable, or in `%LOCALAPPDATA%\DarksCheck\log` when that folder cannot be
     created or written (the run's first line says which); the names begin with the
     executable's name, `darkscheck` and `DarksCheck.Wpf`; the first file `darkscheck.log`,
     past 1 MiB the next `darkscheck_01.log`, `_02`, ...; at most 5 files per executable, the
     oldest deleted first, the next process goes on in the newest; one executable's rolling
     never deletes the other's files; two processes of the same executable write into the
     same file, each line whole. Example: 8 MiB of lines through the console's configuration
     leave `darkscheck_03.log` to `darkscheck_07.log`, none above 1 MiB plus one line.
   - R-101: the content. The table above in requirement form: the first line, one line per
     file read and per frame decoded with its working set, the `inventory` and `darks` runs'
     start and done lines with the stage times, the figures behind each verdict and each
     frame's figures at Debug, every file written with its size, every exception with its type,
     message and stack, those the program recovers from included (a frame that cannot be read
     or decoded, a group that fails, the settings file, the clipboard, the title bar, a link),
     except a cancellation the user asked for, a race at the window's close and the log's own
     probes; the window's runs numbered, its dialogs and actions; a last line with the exit
     code and the elapsed time; English with invariant numbers whatever `--lang`, a message the
     user saw as shown; nothing added to stdout or stderr. The example is written in step 7
     from the Smida run.
   - R-102: the level. Debug unless `DARKSCHECK_LOG` names another (`off`, `fatal`, `error`,
     `warn`, `info`, `debug`, `trace`, any case); any other value means Debug; Trace adds every
     FITS header card; `off` creates neither the folder nor a file.
   - R-103: two processes of the same executable writing the same file at full speed both keep
     every line, each whole and once, across the 1 MiB roll too, through
     `NLog.Targets.AtomicFile`. The example is the test's figures.

   Then the packages: `Directory.Packages.props` gets `NLog` 6.2.2 and
   `NLog.Targets.AtomicFile` 6.2.2 with FlatCheck's two comments (the why, the measurement
   cited as FlatCheck's, "keep its version equal to NLog's"); `DarksCheck.Core.csproj` the two
   `PackageReference`s with a comment like FlatCheck's; `dotnet restore DarksCheck.sln
   --force-evaluate` updates the five `packages.lock.json` (core, console, window, two test
   projects), all committed; `ThirdParty.cs` a third component `NLog` with FlatCheck's role in
   both languages, licence `BSD-3-Clause`, FlatCheck's copyright line, `https://nlog-project.org/`,
   packages `["NLog", "NLog.Targets.AtomicFile"]`; `THIRD-PARTY-NOTICES.md` a section `## NLog`
   between LibRaw's and the test components, FlatCheck's section word for word with
   DarksCheck's names and ADR 0025 in place of ADR 0070, the BSD text included;
   `ThirdPartyTests.EveryComponentNamesItsLicenceAndUrl` counts 3 (this is the task's change,
   named at wrap-up, not a test edited to pass). `AboutTests` compares the window's list to
   `ThirdParty.Components` and needs no change. Commit: "Take NLog and its atomic file target
   for the diagnostic log, and state the log's four requirements".

2. **`LogSetup`, `LogText`'s basics, `RunClock`, `LogCapture`, the file tests.** The three
   core files and the capture as described; `LogCapture` has a `[ModuleInitializer]` that sets
   one `CaptureTarget` at Trace rendering `LogSetup.LineLayout`, an `AsyncLocal` scope opened
   by `LogCapture.Start()` (one per call tree, `Dispose` closes it), `Events` (level, logger,
   message, the rendered line, the exception), `Messages`, `Text`. The file tests of
   `LoggingTests` build the applications' configuration through `LogSetup.Build` into a private
   `LogFactory` per "process", in a `TempWorkspace`, and never touch the assembly's
   configuration:
   - `R100_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName`
   - `R100_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback` (a file where the folder
     should be)
   - `R100_TheLogRollsAt1MiBAndKeepsAtMost5Files` (8 MiB of 4 KB lines; the five names; the
     next factory appends to the newest)
   - `R100_OneExecutablesRollingLeavesTheOthersFilesAlone` (`DarksCheck.Wpf` rolled once
     survives `darkscheck`'s eight rolls)
   - `R100_TheFilesBeginWithTheExecutablesName` (theory: a path, null, the `dotnet` host)
   - `R100_AReadOnlyNewestLogFileSendsTheLogToTheFallback` (the append probe)
   - `R100_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe` (a stream open with the atomic
     target's share mode)
   - `R102_TheLevelComesFromTheEnvironmentVariable` (theory over the eight values and junk)
   - `R102_OffCreatesNeitherTheFolderNorAFile`
   - `R103_TwoProcessesWritingAtFullSpeedKeepEveryLine` (two factories on one file, two
     threads released by a `Barrier`, 4000 lines each crossing the roll, every line whole and
     once; the first file's end written to the test output)
   - `R101_TheConfigurationHasOneFileTargetAndNoConsole`
   - `R101_TheSameCrashSeenByTwoHooksIsWrittenOnce` (through `LogSetup.Fatal` and the capture)
   - `TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal`
   Commit: "Configure the log in code: the folder, the rolling, the level and the atomic target".

3. **The readers' refresh and the core's seams.** First the refresh of D3: with LightsCheck's
   log task merged into `main`, copy `ImageLoader.cs` and `RawReader.cs` from
   `E:\Photografie\Code\LightsCheck\src\LightsCheck.Core\Imaging` over DarksCheck's, change the
   namespace, keep DarksCheck's comments and the English-or-Romanian pairs of the messages (ADR
   0019, 0021: a refresh takes the code and keeps both), and add to `LogText` the members the
   two files call, named as LightsCheck names them; `docs/design/imaging.md` gets its
   "refreshed from LightsCheck `main` <hash>" line and says which two files of the folder log.
   Read the diff of the two files whole: nothing but the log lines, the namespace and the
   comments may differ from LightsCheck's. If the LightsCheck task has not merged, the fallback
   of D3 instead, and this step's commit message says so. Then DarksCheck's own seams:
   `FrameInfo.Read`, `InventoryRun.Run` (with its `RunClock`), `FrameStack.Load` and
   `DecodeOne`, the loop of `LightsAnalyzer.Analyze`, `DarkSeriesRun.Run` (with its
   `RunClock`, started before `InventoryRun.Run`), `DarkOutputs`, and the `LogText` lines they
   need. Tests, in `LoggingTests`, each over the synthetic fixtures (`SessionFixture`,
   `SyntheticDarks`, `SyntheticLights`) with a `LogCapture`:
   - `R101_ADarksRunLogsEveryFrameTheVerdictsTheIndexAndTheStageTimes` (the `darks start` and
     `darks done` lines with the seven stages, one `decoded` line per frame with `working set`,
     one `frame` line per dark, the `verdicts:` and `index:` lines)
   - `R101_AnInventoryRunLogsEveryFrameReadAndTheCadence`
   - `R101_AFrameThatCannotBeReadIsLoggedWithItsExceptionAndTheRunGoesOn` (a truncated FITS
     among the darks: the Warn line carries the exception, the `done` line counts the error)
   - `R101_AFrameThatCannotBeDecodedIsLoggedWhereItIsCaught` (the stack frames of `FrameStack`
     and of the lights' loop)
   - `R101_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached` (a lights-only session
     without a partner: `darks ended without an outcome after N ms (inventory N ms)`)
   - `R101_AGroupThatFailsIsLoggedWithItsExceptionAndTheRunGoesOn` (biases of another size)
   - `R102_TraceHoldsEveryFitsHeaderCard` (a FITS dark under the capture: its `BITPIX` card is
     a Trace line of `ImageLoader`'s logger)
   - `R101_EveryFileWrittenIsOneLineWithItsSize` (`DarkOutputs.WriteAll` into a temp folder)
   - `R101_TheLogIsTheSameInBothLanguages` (the same run under `Ui.Use(English)` and
     `Ui.Use(Romanian)`: the messages equal once the lines that carry a user's message, the
     `cannot read` and `cannot decode` ones, are taken out)
   Commit: "Log the core's seams: every frame read and decoded, the runs' stages, the figures".

4. **The console.** `Program.Main` and the lines of `RunInLanguage`. Tests:
   - `R101_TheConsoleLogsTheModeAndAFailureWithItsStack_AndStderrKeepsOneLine` (a session folder
     that does not exist: stderr has the one `ERROR:` line it has today, the capture has the
     `run failed` line with `DirectoryNotFoundException` and its stack)
   - `R101_ARefusedOptionIsLoggedAtWarnAsShown` (`--threads x`, `--lang fr`)
   - `R101_TheConsoleAddsNothingToStdoutOrStderr`: the golden tests already pin both streams
     with the capture active; this test runs the darks scenario and asserts the capture holds
     lines while stdout equals the golden file, so the claim is named by a test.
   Run the golden tests and read their diff: there must be none; if a golden changes, stop,
   something leaked into a stream. Commit: "Start and close the log around the console's run,
   and log its mode, refusals and failures".

5. **The window.** `App`, `Loggers`, `MainViewModel`, `MainWindow`, `UserSettings`,
   `DarkTitleBar`, `AboutWindow`. `WindowLoggingTests` (collection `Sta` like the other window
   tests, `LogCapture` linked):
   - `R101_AnOpenLogsTheRunItsOptionsAndItsOutcome`
   - `R101_AProblemCardIsLoggedWithTheExceptionAndTheScreenKeepsTheMessage`
   - `R101_ACancelledRunIsLoggedAsCancelledNotAsAFailure`
   - `R101_ASupersededRunsFailureIsKeptInTheLogThoughTheScreenShowsNothing`
   - `R101_AnExportLogsEveryFileAndAFailedExportItsException`
   - `R101_ASettingsFileThatCannotBeReadIsLoggedAndTheDefaultsAreUsed` (a `TestSettings.Own`
     file holding junk, restored on dispose)
   Commit: "Start and close the log with the window, and log its runs, dialogs and quiet
   failures".

6. **The invariants.** In `LoggingTests`, over the source tree (`TestData.FindRepoRoot()`):
   - `OnlyTheTwoEntryPointsConfigureTheLog`: `LogSetup.Apply(` and `.Close(` only in
     `src/DarksCheck.Cli/Program.cs` and `src/DarksCheck.Wpf/App.xaml.cs`;
     `LogManager.Configuration =` or `LogManager.Setup(` only in `LogSetup.cs` and
     `LogCapture.cs`.
   - `NoLogLineGoesThroughTheTranslation`: FlatCheck's scan, over `src/`: no `Log.<Level>(`
     call whose text up to `);` contains `T(`; every logger declared as `static readonly Logger
     Log = LogManager.GetLogger(` or `static Logger Log => LogManager.GetLogger(`; no qualified
     `NLog.Logger`.
   - `NothingThatComputesOrWordsTheReportLogs`: no `NLog`, `Logger` or `LogManager` in
     `src/DarksCheck.Core/Analysis/` except `FrameStack.cs` and `LightsAnalyzer.cs` (D4), in
     `Report/`, `Thresholds.cs`, `Language/`, and in `Imaging/` except `ImageLoader.cs` and
     `RawReader.cs` (D3: the copies take no line of DarksCheck's; the two adapted files log as
     LightsCheck's do, and `FitsReader.cs` never).
   Commit: "Pin where the log is configured, that it never goes through the translation, and
   where it never writes".

7. **Measure, document, finish.** In the worktree, `dotnet build -c Release`, then the console
   on the Smida session (`DARKSCHECK_TESTDATA_LARGE`, the session `docs/validation.json` names
   for V-003, with its lights and `--against` the library of V-005), three runs with the log on
   and three with `DARKSCHECK_LOG=off`, elapsed times written down; the log's size per run; the
   window opened once on the same session and once with two windows at the same time, the
   second's lines present in the same file. Publish once with `scripts/release.ps1 -Version
   1.0.0 -PublishOnly -WhatIf` first, then without `-WhatIf`, and run `artifacts\1.0.0\console\darkscheck.exe
   --help` from another folder: `log\darkscheck.log` must appear beside the single-file
   executable (`AppContext.BaseDirectory` is the executable's folder for a single-file publish;
   check it, do not assume it); delete `artifacts\` after. Then the documents, in one commit:
   - `docs/decisions/0025-log-with-nlog-configured-in-code-at-the-seams-the-readers-copies-left-alone.md`:
     Context (the ask, FlatCheck's ADR 0070 taken), Decision (D1 to D8 as taken, the measured
     figures of this step, the seams table in short, FlatCheck's measurements cited as such),
     Alternatives rejected (FlatCheck's, plus the two of D3 and D4), Consequences (the third
     component, the invariant tests, the log is the first thing to ask a reporter for, the
     readers' lines come with a LightsCheck refresh). Check `main`'s latest ADR number before
     the merge; renumber on the branch if it moved.
   - `docs/design/logging.md`: FlatCheck's note's shape (how the parts fit, the files
     measured, the line, a run's lines, what is logged where, reading a log, in the tests),
     DarksCheck's content, with `**Covers:**` naming `src/DarksCheck.Core/Logging/LogSetup.cs`,
     `LogText.cs`, `RunClock.cs`, `src/DarksCheck.Cli/Program.cs`,
     `src/DarksCheck.Wpf/App.xaml.cs`, `src/DarksCheck.Wpf/Loggers.cs`,
     `tests/DarksCheck.Tests/LogCapture.cs`, `tests/DarksCheck.Tests/LoggingTests.cs`,
     `tests/DarksCheck.Wpf.Tests/WindowLoggingTests.cs`; a row in `docs/design/README.md`'s
     index ("The diagnostic log: the configuration, the files, the line, what each seam
     writes, the capture in the tests", read before editing anything in `Logging/`, a log
     line anywhere, or the two entry points). `scripts/check-design.ps1 -Since main` must pass:
     a file may be named by two notes (`Program.cs` is in `session-and-console.md` too).
   - `README.md`: a section `## Logs` between "The window" and "Building", FlatCheck's text
     with DarksCheck's names and `DARKSCHECK_LOG`, and one sentence in "The window" after the
     settings file's sentence, since that paragraph says `settings.json` is "the only file the
     window writes outside the folder you export to", which stops being true.
   - `CHANGELOG.md`, `[Unreleased]`: under Added, the log (FlatCheck's bullet adapted: where,
     the five files, what it holds, `DARKSCHECK_LOG`); under Internal, NLog as the third
     third-party component with its atomic target, the core logging at its seams and never in
     the readers' copies, every test running every log line.
   - `docs/requirements.md`: R-100 to R-103 `implemented`, R-101's example cut from the Smida
     log (the first line, a `decoded` line, the `darks done` line, the last line).
   - `docs/skills.md`: under `/new-feature`, at step 5, one line: a new seam (a run, a file
     read or written, a dialog) logs as `docs/design/logging.md` says.
   - This plan's "What deviated" section.
   Commit: "Record the log's decision, mechanics and user guide, with the figures measured on
   the Smida session".

8. **Wrap-up.** `/wrap-up`: build once with `dotnet build DarksCheck.sln -c Release
   --no-incremental -warnaserror --no-restore` (an incremental build hides warnings), then
   `scripts/verify.ps1`; read the whole diff back and summarise it; ask the owner for the
   subagent review of the branch (the owner's habit: one review of the branch, then one of
   the fix commit only); propose the merge, which the owner decides.

## Verification

- `scripts/verify.ps1` green at every commit; `dotnet format DarksCheck.sln
  --verify-no-changes` clean.
- The golden files unchanged: `git diff --stat main -- tests/DarksCheck.Tests/Golden/` empty.
- `scripts/check-design.ps1 -Since main` green.
- The figures pasted in the ADR and in R-101's example, from step 7: the elapsed times with
  the log on and off (three each), the log's size per run, the number of lines, the two
  windows' lines in one file, the published executable's `log\` folder.
- `R103_TwoProcessesWritingAtFullSpeedKeepEveryLine` passing is the measurement of D2 on this
  build; its output (the first file's size, the lines kept) is pasted in the ADR.

## Traps

- `Program.Run` and `MainViewModel` must stay free of `LogSetup.Apply`: the tests call them
  in-process, and the capture would be overwritten by the file configuration.
- The logger of `LogSetup` and of `App` is a property fetched on use. A `static readonly
  Logger` there makes NLog load an `NLog.config` found beside the executable before `Apply`
  sets the configuration, and FlatCheck measured the process dying in
  `TypeInitializationException` before `Main`.
- Every message is one plain string. NLog's `Info(string, params object[])` overload parses
  `{}` templates; `Info(string)` does not. A path with `{` would otherwise be mangled or throw.
- Lines that cost something to build are behind `IsDebugEnabled` / `IsTraceEnabled`; the
  per-frame lines run inside `Parallel.For`, so `LogText` must not touch shared state.
- `TreatWarningsAsErrors` and `AnalysisLevel latest-recommended`: CA1305 wants a culture on
  every `ToString` (use `LogText`), CA1310 an ordinal `StartsWith`; the Imaging folder's
  relaxed rules do not apply to `Logging/`.
- `RestorePackagesWithLockFile`: a restore without `--force-evaluate` after adding the
  packages fails in locked mode; commit the five lock files.
- `InvariantGlobalization` is on in the core and the console, off in the window: the log's
  numbers go through `LogText`, which pins the invariant culture, so both agree.
- The window's `App` is not built by the tests (`StaFixture` builds a plain `Application`);
  `UserSettings.Path` is static and shared, so the settings test uses `TestSettings.Own`.
- The refresh of step 3 is from LightsCheck's `main` after its log task merged, not from its
  task branch or worktree: a branch may still change. `docs/design/imaging.md` names the hash
  taken. The `using NLog` and the logger field of the two files come with the code; the GPL
  header and the file comment stay DarksCheck's.
- The Stop hook refuses a turn that edited code until `verify.ps1` ran green after the last
  edit; run `/wrap-up` when it fires.
- ADR numbers collide at merge when `main` moved; check before proposing the merge.
- The per-frame `working set` is `Environment.WorkingSet`, read after the forced collection
  that follows each decode, so the figure is the frames kept plus those in flight, as ADR 0007
  describes the peak.

## What deviated

Written at wrap-up, 2026-10-30. LightsCheck's log task (its ADR 0029, plan of the same day) was
merged into `main` (ad721aaf) before this task began, so D3 took its main path, and LightsCheck's
files, FlatCheck's already in the style DarksCheck shares and fixed by two reviews, were the model
of `Logging/`, `LogCapture`, `LogFiles` and the tests rather than FlatCheck's directly.

- **D3, the readers, as a hunk.** Step 3 said to copy the two files over DarksCheck's. LightsCheck's
  copies held two more hunks besides the log, its `InvalidDataException` for a raw libraw refuses
  (321ab4aa, for its `--frame` mode) and the fields of its pixel size from the 35 mm-equivalent
  focal length (its R-066, 7fa82a12); a whole copy would have brought both, the first changing a
  report's words. The log hunk alone was taken (LightsCheck's 5fa43622 and 80ac6867), byte for
  byte in the code, as ADR 0023 took FlatCheck's lens hunk; `LogText.Frame` leaves out the fields
  DarksCheck's `LoadedFrame` does not have. `docs/design/imaging.md` lists the hunk and the two
  left out.
- **D7, Trace.** The cards are the full load's: a metadata read keeps none, so the inventory's
  reads add no card at Trace, and R-102 says so (LightsCheck's R-072 says the same).
- **`DarkOutputs` holds no logger.** The table had it write `wrote <path>`, the invariant of step
  6 keeps `Report/` free of loggers; LightsCheck's way was taken: `WriteAll` tells a callback each
  file as soon as it is written, and the console and the window write the line. The Debug line
  for a map not written was dropped.
- **The figures by one rule.** The named members (`LogText.Series`, `DarkFrame`, `Verdicts`, ...)
  became one, `LogText.Figures`, which writes every public property of a record by the plan's own
  rule; the call sites name what they leave out (a frame's `Info`, `Error` and `Skipped`, the
  masters, a text in the run's language: the series' `ScaleSource`, the partner's `FirstError`).
- **The words.** `SessionFolder.Open` writes the folder line at Info (LightsCheck's), for the
  partner too; the plan had it at Debug in `InventoryRun`. A frame left out for its size or its
  settings is written in the log's own English words, not as the row's message. The console's
  refused `--out` is its run's failure, one Error line `run failed: mode=darks` with the
  exception, not a Warn `refused:` line. The window's failed run is `run N failed after ...; the
  problem card shows the message` at Error, as planned; the label copied names the label; a
  settings file read or written without trouble writes nothing (a `wrote` line per save was
  dropped after the first review: the boxes save on every keystroke).
- **The console names its mode** in `Program` (`help`, `darks`, `inventory`); `Options` is
  unchanged. `GoldenOutputTests.Scrub` and `GoldenDir` became internal so
  `R101_TheConsoleAddsNothingToStdoutOrStderr` compares stdout with the golden file itself.
- **The tests.** Renamed: `R101_ASupersededRunsFailureIsKeptInTheLogThoughTheScreenShowsNothing`
  is `R101_ASupersededRunIsKeptInTheLogThoughTheScreenShowsNothing` (a superseded run's failure
  depends on a race; its stop does not). `R101_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached`
  uses a session whose darks are all unreadable, so the run throws after two stages. Added, most
  from LightsCheck: the first line's facts, the clock's two endings, a task exception already
  written, the frames left out, the export's callback, the figures' rule, libraw's line without a
  CFA order, the real console's first and last lines and its level, the window's exception
  filter and its lines in both languages.
- **CLAUDE.md** names the log in the layout and adds to the rule "Computed only in the core" the
  sentence LightsCheck's owner settled the same day (its ADR 0029): a diagnostic log line is no
  figure of the analysis.
- **The size (D8).** Measured: 495 lines, about 198 KB, a Smida run against the library, not
  30 to 45 KB: a dark's figures line is 1.3 KB (four planes and their residuals), and the readers'
  three lines per file are 55 % of the bytes. The five files hold about twenty-six such runs.
- **Step 7, the window.** The owner was at the PC (idle under a second, the Claude app in the
  foreground), so no window was opened at first, and two console processes writing one file at
  once stood for two windows. At the owner's word after the wrap-up the window was started for
  real: once in Romanian on a library, twice at once on two libraries into one file, once on an
  empty folder; ADR 0025 has the figures.
- **Names.** The ADR is `0025-a-diagnostic-log-with-nlog-as-lightschecks-written-at-the-seams.md`
  and step 0's commit message "Plan the diagnostic log with NLog, so a reported bug can be
  diagnosed from the log": the planned names said "the readers' copies left alone", written before
  D3 was rewritten.
