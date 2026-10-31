# Add a diagnostic log with NLog

Plan for the task branch `claude/Log-AstroPhotoAnalyzer-with-NLog`, worktree
`.claude/worktrees/Log-AstroPhotoAnalyzer-with-NLog` (the sibling plans of 2026-10-30 claimed
`claude/Add-a-diagnostic-log-with-NLog`, and branch names are repository-wide). Written on
2026-10-31 for an implementer who has this file, `CLAUDE.md`, the repository and read access to
the siblings in `E:\Photografie\Code` (FlatCheck, LightsCheck, DarksCheck), and nothing else from
the planning session. Approved by the owner on 2026-10-31 as written, every row as recommended;
this file is the first commit of the branch.

## Context

A user reports "it crashed", "it said the file cannot be read" or "the verdict is wrong", and all
we have is the screen. The console prints `ERROR: <message>` without the exception's type, its
stack or its inner exceptions (`AnalyzerCli.RunInLanguage`, the two catches); the window shows
`MainViewModel.Describe(e)`, the messages alone, and drops the stack on purpose. Nothing records
which file was read and by which reader, what the FITS header or ImageSharp said about it, which
figures led to each recommendation, which parameters and profile the run had, how long each
stage took, or what the window did before it failed. Several places swallow an exception for good
reasons and leave no trace: a profile that cannot be read becomes a `Problem` text
(`IccPaperBlack.Read`, `GamutAnalyzer.Build`, `ProofProfile.Open`), the settings file that cannot
be read gives the defaults (`WindowSettings.Read`, `Write`), a link that does not open
(`AboutWindow.OpenLink`), the title bar that stays light (`DarkTitleBar.Apply`), a curves or
proof computation a newer one superseded. Nothing in the three projects logs today.

The owner's ask (2026-10-31, in Romanian): a logging system that lets a problem at a user be
diagnosed from the logs; NLog; text format; in the folder `log/` beside the executable; the file
name starting with the executable's name; at most 5 files per executable, rolling, at most 1 MB
each; done the way FlatCheck did it; what is logged is the implementer's call with one
criterion: a bug report with the log attached can be diagnosed from the log alone. A plan
detailed enough for Opus to implement.

## What the siblings did, and what is taken from them

FlatCheck built this log on 2026-10-27 (its ADR 0070, R-109 to R-112, design note
`docs/design/logging.md`), then fixed the 42 findings of its review (its ADR 0072). LightsCheck
(ADR 0029), DarksCheck (ADR 0025), CopyWcs (ADR 0009), ImageVisualCompare (ADR 0043) and the
AstroDarkroom launcher (ADRs 0012/0013) took it in turn, each reviewed twice; DarksCheck's copy
(`E:\Photografie\Code\DarksCheck`, `main` at 8832b474 or later) is the latest and the most
reviewed, and it is the model here: its `src/DarksCheck.Core/Logging/LogSetup.cs`, `LogText.cs`,
`RunClock.cs`, `src/DarksCheck.Cli/Program.cs`, `src/DarksCheck.Wpf/App.xaml.cs`, `Loggers.cs`,
`tests/DarksCheck.Tests/LogCapture.cs`, `LogFiles.cs`, `LoggingTests.cs`,
`tests/DarksCheck.Wpf.Tests/WindowLoggingTests.cs`, its `THIRD-PARTY-NOTICES.md` section
`## NLog`, its `ThirdParty.cs` NLog entry, the two package comments of its
`Directory.Packages.props`, its README `## Logs`, its ADR 0025 and `docs/design/logging.md`. The
implementer reads those before writing anything. FlatCheck's ADR 0070 and `docs/design/logging.md`
hold the measurements, cited as FlatCheck's and not measured again, except by the tests below:

- NLog's plain file target closed between lines lost 33 to 48 of 4000 lines written by two
  processes at full speed and cost 4.8 ms a line; kept open it lost one writer's whole log;
  `AtomicFileTarget` lost none at 0.09 ms a line (the owner chose it on 2026-10-27).
- NLog 6.2.2's `MaxArchiveFiles` counts the file being written: set to 4 it left 4 files.
- A logger held in a static field of the type that calls `Apply` makes NLog load a stray
  `NLog.config` beside the executable before `Apply` sets the configuration, and the process died
  in `TypeInitializationException` before `Main`.
- `NLog.Targets.AtomicFile` ships two binaries, `net8.0` and `net8.0-windows7.0`; a WPF window
  ships the second, so the window's tests, `net8.0-windows`, run the two-writer test on it.
- The AstroDarkroom suite publishes every tool into one flat folder and refuses a file of other
  bytes at a path another tool published: NLog must be 6.2.2 here as everywhere, and no tool ships
  a notices file of its own beside its exe; the credit is the About window's card.

What is AstroPhotoAnalyzer's own, each a decision below: the names (D3, D6), the seams (D4),
the figures (D5), what Trace adds (D7), the validation re-run (D8).

## The owner's decisions

Each row comes with a recommendation (the kit's rule "Questions to the owner"); the plan is
written as if every recommendation were taken, so an approval "as written" settles every row.

| # | Decision | Recommendation and why |
| --- | --- | --- |
| D1 | Where NLog lives | As FlatCheck's ADR 0070: the core references the packages and logs at its seams; each application configures NLog **in code** at its entry point through `LogSetup.Apply` first and `LogSetup.Close` last, each in a `try` that swallows anything, since the log never stops the tool. No `NLog.config`: nothing to ship, nothing the user can break. Without a configuration (a library use) every call is a no-op; the test assemblies configure a capture at Trace. `Program.Main` stops being the one-line shim its file comment describes; `AnalyzerCli.Run` configures nothing, so the tests and the golden tests keep calling it in-process. |
| D2 | Two packages, `NLog` 6.2.2 and `NLog.Targets.AtomicFile` 6.2.2 (the kit's rule "Packages": no new package without asking; the project's rule "Package notices") | **Take both**, the versions the suite pins: two windows opened on two images write one `AstroPhotoAnalyzer.Wpf.log`, and NLog's plain target lost lines either way (FlatCheck's measurement). Same project, same commit, same BSD-3-Clause licence, pure managed, so the console stays one copyable single file. One component `NLog` in `ThirdParty.cs` naming both packages (as `Lynds' Catalogue` names a resource), one `## NLog` section in `THIRD-PARTY-NOTICES.md` with the BSD text, one row in the README's table; no notices file beside an exe (the suite's rule). `ThirdPartyTests` then counts four components: that is this task's change, named at wrap-up, not a test edited to pass. |
| D3 | The level variable's name | **`APA_LOG`**: the project's own variables are `APA_UPDATE_GOLDEN`, `APA_DUMP_SCREENS`, `APA_TESTDATA_LARGE`, and it is what a user types in `set`. The alternative, `ASTROPHOTOANALYZER_LOG` on the siblings' pattern (`FLATCHECK_LOG`, `DARKSCHECK_LOG`), is 22 characters for the same thing. Values `off`, `fatal`, `error`, `warn`, `info`, `debug`, `trace`, any case; anything else means Debug, the default. No console option, so `Options`, `--help` and the golden files do not move. |
| D4 | Where the core logs | **`Runs/` (the three runs and `CurveExport`) and the four files of `Input/` that touch a file: `ImageLoader.cs`, `FitsReader.cs`, `ImageSharpReader.cs`, `IccPaperBlack.cs`.** Nothing in `Analysis/`, `Report/`, `Thresholds.cs`, `Parameters.cs`, `Language.cs` or the rest of `Input/` (`Wcs.cs`, `Astrometry.cs`, `DarkNebulaCatalogue.cs`, `FitsWriter.cs`, `AtomicFile.cs`, the maths): the runs log the figures the analyzers return, the writers' outcome is logged by the run that calls them. Two classes of `Analysis/` swallow an exception into a `Problem` text and lose the stack, `GamutAnalyzer.Build` and `ProofProfile.Open`: each gains an `internal Exception Cause { get; init/set; }` set in its catch, no logger, and `CurveRun` / `ProofRun` log the problem at Warn **with** that exception. The alternative, logging the text alone, loses the only stack of a damaged profile. The invariant test names the files. |
| D5 | The figures behind every recommendation | **One rule, `LogText.Figures(record, skip...)`, by reflection, as DarksCheck's ADR 0025**, adapted to this core, whose result records expose `internal` properties and some classes `internal` fields: every instance property and field, public or internal, in declaration order, `name=value`; a number with six significant digits, an integer, a flag as `yes`/`no`, an enum by its name, a tuple in parentheses, a stored string as it is (a path, a format, a note as the reader wrote it, so in the run's language), a record or class held by the record in parentheses (depth 6), a list in brackets whole up to 24 items (the simulation's ladder, the size table, the JPEG table, the star regions), else `[first, ..., last] (n items)` (the histograms, the patch arrays, the stars, the noise tiles), a delegate skipped, a missing value `none`, a property that throws `?`. Two guards this core needs: **a property with a hand-written getter is written only when its type is neither `string` nor a sequence of strings** (on this core every computed string is a report sentence through `Tr.Pick`: `DustClassDefinition`, `SkyDescription`, `MethodNote`, `ClassFractions`, `SourceText`, `DustDescription`, `DustMap.ClassNames`; a computed number, flag or enum such as `Insufficient`, `Chroma`, `IsUpscaled`, `Method` is written), and **the rule descends only into types of the core's own assembly**: a value of another assembly's type, past the primitives, strings, enums, tuples, `DateTime` and sequences, is written as its type name in angle brackets (ImageSharp's and Unicolour's objects stay out). The eleven result records, `Parameters`, `CurveAnchors` and `CurveEvidence` are written this way; the proof's line is composed by hand from `ProofSet`'s scalars, since `ProofProfile` holds the package's objects. The alternative, one hand-written line per record (FlatCheck's way), is over 150 figures to keep in step by hand. |
| D6 | The folder when `log` beside the executable cannot be written (Program Files, a read-only drive) | `%LOCALAPPDATA%\AstroPhotoAnalyzer\log`, beside the window's `settings.json` (ADR 0053), and the first line says so. A log that silently does not exist defeats the purpose. |
| D7 | What Trace adds | Every FITS header card, of the analysed FITS and of a `--wcs` FITS, one line per card (`FitsReader`). Trace is otherwise Debug: nothing else here is bulk (a run reads one image). |
| D8 | The validation re-run and the rule's letter | The rule "Formula changes re-validate" names `Input/` and `Analysis/`, which this task touches with log lines and a `Cause` property, though no formula moves. **Run `/validate` once at step 7 and paste the drift table (expected: none)**, and add to the rule "Computed only in the core" the sentence LightsCheck's and DarksCheck's owner settled: a diagnostic log line is no figure of the analysis and no text the user sees; it is English, composed by `LogText`, never through `Tr.Pick`. |
| D9 | The version | The owner's (the kit's ADR 0029): nothing on the branch; a minor bump (1.0.0 to 1.1.0) seems warranted at the next `/release`, for a user-visible addition. |

## The design

### The files

New, in the core, namespace `AstroPhotoAnalyzer` (block namespaces, as every file of the core):

- `AstroPhotoAnalyzer.Core/Logging/LogSetup.cs`: `LogPlace` (record), `LogSetup` (static):
  the constants `FileSizeLimit` (1 MiB), `FilesPerExecutable` (5), `LevelVariable`
  (`APA_LOG`), `FolderName` (`log`), `FallbackFolder` (`AstroPhotoAnalyzer`), `DefaultLevel`
  (Debug), `LineLayout`; the methods `Apply(string entryName, IReadOnlyList<string> args)`,
  `Plan(...)`, `Close(int exitCode)`, `Fatal(Exception e, string where)`, `Wrote(Exception e)`,
  `Unobserved(AggregateException e)`, `Build(string executableName, string folder, LogLevel
  minimum)`, `ResolveFolder(...)`, `CanWrite(...)` (the probe file plus the append probe on the
  newest file), `NewestLog(...)`, `LevelFrom(string value)`, `ExecutableName(string
  processPath, string entryName)`, `StartupLine(...)`. DarksCheck's file, member for member; its
  own logger a property fetched on use, never a static field; `LogManager.ThrowExceptions =
  false`; the configuration set before any logger exists; `Off` gives a configuration without
  targets and `Apply` returns before creating the folder or hooking anything. The first line's
  version is `VersionInfo.Value` (`Version.cs`).
- `AstroPhotoAnalyzer.Core/Logging/LogText.cs`: `Num`, `Int`, `Ms`, `Seconds`, `Str`, `Yes`,
  `MiB`, `KiB`, `FileMiB`, `FileKiB`, `List`, `Wrote(path)`, `Figures(object value, params
  string[] skip)` with the rule of D5 (`BindingFlags.Instance | Public | NonPublic`, properties
  then fields, `ListItems` 24, `Depth` 6), `FitsKeys(IReadOnlyDictionary<string, string>
  header)` (the keys that decide the reading and the WCS: `SIMPLE`, `BITPIX`, `NAXIS`, `NAXIS1`
  to `NAXIS3`, `BZERO`, `BSCALE`, `ROWORDER`, `DATAMIN`, `DATAMAX`, `CTYPE1`, `CTYPE2`,
  `CRPIX1`, `CRPIX2`, `CRVAL1`, `CRVAL2`, `CD1_1`, `CD1_2`, `CD2_1`, `CD2_2`, `CDELT1`, `CDELT2`,
  `CROTA2`, `PC1_1`, `OBJECT`, `INSTRUME`, `EXPTIME`, `STACKCNT`), `Source(SourceInfo s)` (the
  loaded line's facts: format, bit depth, float, planes in the file, compression, the ICC
  description, the file size, whether a WCS was read, the note as the reader wrote it). Invariant
  culture, `none` for a missing value, an enum by its name, never through `Tr.Pick`.
- `AstroPhotoAnalyzer.Core/Logging/RunClock.cs`: DarksCheck's, unchanged in behaviour:
  `Start(Logger, run, inputs)` writes `<run> start <inputs>` at Info; `Lap(stage)`;
  `Done(outcome)` writes `<run> done <outcome> in N ms (stage N ms, ...)`; `Dispose` without
  `Done` writes `<run> ended without an outcome after N ms (<stages>)`. Held in a `using`.

New, in the window: `AstroPhotoAnalyzer.Wpf/Loggers.cs`, the names `App`, `Window`,
`Window.Curves` and `Window.SoftProof` as constants: the two tabs that run the core number
their runs under a name of their own, as FlatCheck's tabs do, so a curves run and a proof run
of the same analysis are told apart.

New, in the tests: `AstroPhotoAnalyzer.Tests/LogCapture.cs` and `LogFiles.cs` (both linked into
`AstroPhotoAnalyzer.Wpf.Tests.csproj` as `TempWorkspace.cs` and `Scenes.cs` are),
`AstroPhotoAnalyzer.Tests/LoggingTests.cs`, `AstroPhotoAnalyzer.Wpf.Tests/WindowLoggingTests.cs`.

Style: AstroPhotoAnalyzer's, not DarksCheck's. The three application projects have `Nullable`
disabled and `ImplicitUsings` disabled (ADR 0003): no `?` on a reference type (`string Folder`,
`Exception e`; `double?` stays), explicit `using System; using System.Collections.Generic; using
System.IO; using System.Linq; using System.Threading; ...`, block namespaces, the GPL header
(`LicenseHeaderTests`), an XML documentation comment on every type and method (the kit's rule
"Documentation comments"; the build writes the XML file, so a `cref` that does not resolve or a
`<param>` naming no parameter fails the gate, ADR 0038), English comments. The tests have
`Nullable` enabled, file-scoped namespaces and implicit usings, and a `[ModuleInitializer]`
there needs the `CA2255` pragma `RunsInRomanian.cs` carries. The measured constants and the
mechanics do not change.

### The configuration

One `AtomicFileTarget` named `file`: `FileName` = `<folder>\<executableName>.log` as a literal
layout, `Layout` = `LineLayout`, UTF-8 without BOM, `ArchiveAboveSize = FileSizeLimit`,
`MaxArchiveFiles = FilesPerExecutable`, `ConcurrentWrites = true`, `KeepFileOpen = true`,
`AutoFlush = true`, `CreateDirs = true`; one rule from the level to Fatal for every logger;
`DefaultCultureInfo = CultureInfo.InvariantCulture`; no console target ever (stdout and stderr are
the console's contract, R-001 to R-007, and the golden files pin them). The executable's name
comes from `Environment.ProcessPath`: `AstroPhotoAnalyzer` (the console's `AssemblyName`) and
`AstroPhotoAnalyzer.Wpf`; the entry name when the path is the `dotnet` host. NLog 6's rolling:
`AstroPhotoAnalyzer.log`, then past the limit `AstroPhotoAnalyzer_01.log`, `_02`, ...; the oldest
deleted past five; the next process appends to the newest. **The console's name is a prefix of
the window's**: `AstroPhotoAnalyzer_01.log` and `AstroPhotoAnalyzer.Wpf_01.log` share a folder,
and NLog's archive matching and `LogSetup.NewestLog` (`<name>_*.log`, the number parsed) must
each keep to their own name; `R170_OneExecutablesRollingLeavesTheOthersFilesAlone` runs on these
two names, not on invented ones.

### The line

`${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}`
and, when there is an exception, `${newline}${exception:format=ToString}`. Messages are composed
from `LogText` and passed as one plain string (`Log.Info(string)`, `Log.Warn(Exception,
string)`), never as a template with arguments, so a `{` in a path is harmless. A message that
costs something to build (a figures line, the header cards) is guarded by `IsDebugEnabled` /
`IsTraceEnabled`. Every logger is a `Logger` named `Log` taken from `LogManager.GetLogger` with
`using NLog`, `static readonly` everywhere but in `LogSetup` and `App`, where it is a property
fetched on use; the invariant test scans for that shape. Logger names: the class's for the core
(`ImageLoader`, `FitsReader`, `ImageSharpReader`, `IccPaperBlack`, `AnalysisRun`, `CurveRun`,
`CurveExport`, `ProofRun`), `Process` for the first and last lines and the crashes, `Cli` for the
console (`AnalyzerCli`, a static field: its type initialiser runs after `Main` called `Apply`),
and the four names of `Loggers` for the window.

### The two entry points

`Program.Main` becomes: `try { LogSetup.Apply("AstroPhotoAnalyzer", args); } catch (Exception) {
}`; `int code = 1; try { code = AnalyzerCli.Run(args, Console.Out, Console.Error); return code; }
finally { try { LogSetup.Close(code); } catch (Exception) { } }`, with the comments DarksCheck's
`Program.Main` carries (why guarded, what a throw past the catch does). The file comment of
`Program.cs` stops calling `Main` a one-line shim.

`App` gains a constructor: the arguments from `Environment.GetCommandLineArgs()[1..]`; `try {
LogSetup.Apply("AstroPhotoAnalyzer.Wpf", args); } catch (Exception) { }`;
`DispatcherUnhandledException += (s, e) => LogSetup.Fatal(e.Exception, "unhandled exception on
the window's thread, the window closes")`. `OnStartup` stays as it is and adds its two lines
(below); `OnExit` calls `LogSetup.Close(e.ApplicationExitCode)` in the same guard. `App`'s own
logger is a property fetched on use. The window's tests build a plain `Application`
(`StaFixture`), not `App`, so they never call `Apply`.

`LogSetup.Apply` hooks `AppDomain.CurrentDomain.UnhandledException` (Fatal, flushed) and
`TaskScheduler.UnobservedTaskException` (Error); `Fatal` names an exception it already wrote
instead of writing it twice (the window's dispatcher hook, then the process's); `Wrote` records
an exception a line wrote with its stack, so `Unobserved` names it.

### What is logged where

| Seam | Logger | Level | What |
| --- | --- | --- | --- |
| `LogSetup` | `Process` | Info, Fatal, Error | The first line: `AstroPhotoAnalyzer 1.0.0 starting; exe=...; args=["..."]; cwd=...; log=<folder> (fallback: ...)?; level=Debug; os=...; cpus=N; memory=N GiB; runtime=.NET 8...; culture=en-US/en-US; tz=... (UTC+02:00); pid=N`. The last line: `exit N after N.N s`. The unhandled exceptions; a task nobody awaited. |
| `AnalyzerCli` | `Cli` | Info, Warn, Error | `mode=help` for `--help`; `refused: <message as written to stderr>` at Warn for every return of 1 before the analysis (a bad `--lang`, an `Options.Error`, a folder, a missing file, `--icc`'s problem, `Validate`'s message) and for a `RefusedImageException` (a linear image); `run failed` at Error **with the exception** in the analysis catch for anything else (the only place the stack of a reader's exception exists); `wrote <path> (<KiB>)` after `ReportWriter.SaveFull`; `the report could not be written` at Error with the exception. `mode=analysis image=<path> lang=en|ro` once the options are parsed (the arguments are in the first line already; the parameters are in the run's start line). Nothing more on stdout or stderr. |
| `ImageLoader.Load` | `ImageLoader` | Info | `loaded <path> (<MiB>) reader=FITS|ImageSharp <W>x<H> planes=.. <LogText.Source> in <ms>`, after the reader returned; a missing file or an unknown extension throws as today, logged by the application's catch with its stack. |
| `FitsReader.Load`, `ReadImageHeader` | `FitsReader` | Debug, Trace | `header of <path>: <LogText.FitsKeys>` at Debug for the analysed FITS and for the `--wcs` FITS (`ReadImageHeader`); `card <KEY> = <value>` at Trace, one per card, in the file's order (D7); a clipping count and the ROWORDER case are the loader line's `note`. Nothing for the exceptions it throws (the caller logs them). |
| `ImageSharpReader.Load` | `ImageSharpReader` | Debug | `imagesharp <path>: format=<detected> bits=.. color=yes|no compression=.. frames=N icc=<bytes|none>` once the facts are read, before the pixels. |
| `IccPaperBlack.Read(path)`, `Read(bytes, path)`, `Installed` | `IccPaperBlack` | Info, Warn, Debug | `profile <path> (<KiB>): description=.. class=.. space=.. dmax=.. blackLstar=.. note=<as shown|none> problem=<as shown|none>` at Info per profile read (`Read(bytes, path)`, so the picker's listing logs each); a file that cannot be read at Warn **with the exception** (the catch in `Read(path)`), a damaged profile's exception at Warn **with the exception** (the catch in `Read(bytes, path)`); `profiles in <folder>: N printer profiles of M files in <ms>` at Info; a folder that cannot be listed at Warn with the exception; a file skipped for an exception at Debug with the exception. |
| `AnalysisRun.Execute` (both overloads, one private body) | `AnalysisRun` | Info, Debug | A `RunClock` named `analysis` started **before** the load, inputs `path=<path> <LogText.Figures(p)>` (every parameter: medium, dmax, the profile and its path, distance, sizes, aspect, target, passepartout, frame, wcs, seed); stages `load`, `wcs` (when borrowed), `file`, `tones`, `simulation`, `noise`, `background`, `stars`, `size`, `quantization`, `jpeg`, `report`; done line `analysis done verdict=<Overall> recommendations=N <W>x<H>`; a linear image: `Done("refused: the image looks linear, median=<..>")` then the throw as today. At Debug, after each analyzer, one line per result: `file: <Figures>`, `bitDepth: ...`, `crop: ...`, `tones: ...`, `simulation: ...` (`Map` and `AsIsDust` are classes with fields and arrays: the rule writes the method, the sky reason, the latitude span, the thresholds, the cloud count and the arrays by their ends and counts), `noise: ...`, `background: ...`, `stars: ...`, `size: ...`, `quantization: ...`, `jpeg: ...`; then one line per recommendation: `recommendation topic=<Topic> verdict=<Verdict> text=<as shown> because=<as shown>`. The `wcs` borrow's refusals throw as today, with the stack at the application's catch. |
| `CurveRun.Execute` | `CurveRun` | Info, Debug, Warn | A `RunClock` named `curves`, inputs `purpose=<Print|Screen> preview=<maxWidth>x<maxHeight> stride=N`; stages `anchors`, `gamut` (when built), one per suggestion by its name; done `curves done suggestions=N problem=<as shown|none>`; at Debug `anchors: <Figures(anchors)> labels=[...] method=<..>` and per suggestion `curve <name>: <Figures(evidence)>`; a gamut table that cannot be built at Warn with `gamut.Cause` (D4), its note as shown. |
| `CurveExport.Save` | `CurveExport` | Info | `wrote <target> (<MiB>) curve=<name> source=<input FITS|none>` after `FitsWriter.Save`; every refusal and write error throws as today (the window logs it with the stack). |
| `ProofRun.Prepare`, `Render` | `ProofRun` | Info, Debug, Warn | A `RunClock` named `proof`, inputs `profile=<path|none> stride=N`; stages `profile`, `gamut`, `mask`; done `proof done outside=<pct> % problem=<as shown|none>`; a profile or table that cannot be opened at Warn with its `Cause` (D4); `Render`: `proof rendered intent=<ProofIntent> simulate=yes|no in <ms>` at Debug. |
| `App` | `App` | Info, Warn | In `OnStartup`: `refused: <languageError>; the window goes on in English` at Warn; `language=en|ro file from the command line: <path|none, the window asks for one>` at Info. |
| `MainViewModel` | `Window` | Info, Warn, Error, Debug | `run N started: path=<path>` (N counts the runs of this view-model, from 1) in `RunAsync` after `IsBusy = true`; `refused: <bad field>` at Warn when `TryBuild` fails; `run N done in <ms>: verdict=<Overall>` after `Populate`; `run N refused after <ms>: <message>` at Warn for a `RefusedImageException`; `run N failed after <ms>; the window shows the message` at Error **with the exception** otherwise; the whole of `RunAsync` under DarksCheck's exception filter `catch (Exception e) when (LogUnhandled(run, e))`, which logs at Error, calls `LogSetup.Wrote(e)` and returns false, since `Start` and the buttons discard the task (`_ = _vm.OpenAsync(file)`) and the runtime's hook would otherwise be the only trace; `profile chosen: <path>` / `profile cleared` / `profile restored from the settings: <path> readable=yes|no` at Info; `wrote <file> (<KiB>)` after `SaveReport`, `the report could not be written` at Warn with the exception. |
| `WindowSettings` | `Window` | Debug, Warn | `settings read from <path>: profile=<path|none>`; `settings file could not be read; the defaults are used` at Warn with the exception; `settings written to <path>` at Debug; `settings could not be written` at Warn with the exception. (A static logger in a public class: fine, `App` configures first.) |
| `CurvesTab` | `Window.Curves` | Info, Error, Debug, Warn | `run N started` / `run N done in <ms>` around `CurveRun.Execute` (`Compute` and `ComputeAsync`), `run N failed after <ms>; the tab shows the message` at Error with the exception (the catch of `ComputeAsync`), `run N superseded` at Debug; a set with a `Problem` shown: `refused: <problem as shown>` at Warn; `curve save failed: <target>` at Warn with the exception (the catch of `Save`); the core's lines say the rest. |
| `SoftProofTab` | `Window.SoftProof` | Info, Error, Warn, Debug | `run N started` / `run N done in <ms>` around `Prepare`; `intent=<..>` and `simulate=yes|no` at Info on a change; `run N failed after <ms>; the tab shows the message` at Error with the exception (`ComputeCore`'s catch); a set with a `Problem` shown: `refused: <problem as shown>` at Warn; `render failed: <message>` at Warn with the exception (`RenderAsync`'s catch; an `ArgumentException` is a table the core refuses, as shown); a superseded outcome at Debug. |
| `MainWindow`, `IccPickerWindow` | `Window` | Info, Debug | `open dialog: chose <path>` / `open dialog: cancelled`; `save dialog: ...`; `curve save dialog: ...`; `profile dialog: chose <path>` / `cancelled`; `profile file dialog: ...`; `About opened`; `map window opened`, `preview window opened: <row>`, `curve window opened: <name>`, `soft proof full screen opened`; `rerun asked` (the Analyze button). |
| `DarkTitleBar`, `AboutWindow` | `Window` | Debug | `the title bar cannot be darkened on this system` with the exception; `the link <uri.OriginalString> could not be opened` with the exception. |

What is **not** logged, and the requirement says so: a computation a newer one superseded is one
Debug line, not an error; the log's own probes, whose outcome is the first line's `fallback`
note; nothing in `Analysis/`, `Report/`, `Thresholds.cs`, `Parameters.cs`, `Language.cs` or the
rest of `Input/`.

The words: English, invariant numbers, whatever `--lang` says; a message the user was shown (an
exception's, a refusal's, a reader's note, a recommendation's text and because, a profile's
problem) stays as shown, so it can be in Romanian; an enum by its name; the stage names of
`RunClock` are the English ones above, never the translated progress stages.

## The steps

Each step ends with `scripts/verify.ps1` green and one commit on the branch, message in
English, one line, at most 100 characters, saying why. No builds in the main checkout: every
`dotnet` command runs in the worktree.

0. **The worktree and the plan.** From the main checkout `E:\Photografie\Code\AstroPhotoAnalyzer`,
   on `main`, clean: `git worktree add .claude/worktrees/Log-AstroPhotoAnalyzer-with-NLog -b
   claude/Log-AstroPhotoAnalyzer-with-NLog main`. Copy this file into the worktree's
   `docs/plans/2026-10-31-add-a-diagnostic-log-with-nlog.md` and commit it: "Plan the diagnostic
   log with NLog, so a reported bug can be diagnosed from the log". Every later command runs in
   the worktree.

1. **Requirements and packages.** In `docs/requirements.md`, a new block before "Dropped",
   `## The diagnostic log` (a new block of ten for a new area), with R-170 to R-173 as
   `accepted` (they turn `implemented` in step 7, when their tests exist: `RepoInvariantTests`
   refuses an `implemented` row without a test). Their texts, adapted from DarksCheck's R-100
   to R-103:
   - R-170: the files. Each application writes a plain-text log, UTF-8, in a folder `log`
     beside its executable, or in `%LOCALAPPDATA%\AstroPhotoAnalyzer\log` when that folder
     cannot be created or written (the run's first line says which); the names begin with the
     executable's name, `AstroPhotoAnalyzer` and `AstroPhotoAnalyzer.Wpf`; the first file
     `AstroPhotoAnalyzer.log`, past 1 MiB the next `AstroPhotoAnalyzer_01.log`, `_02`, ...; at
     most 5 files per executable, the oldest deleted first, the next process goes on in the
     newest; one executable's rolling never deletes the other's files, though one name is the
     other's prefix; two processes of the same executable write into the same file, each line
     whole. Example: 8 MiB of lines through the console's configuration leave
     `AstroPhotoAnalyzer_03.log` to `_07.log`, none above 1 MiB plus one line.
   - R-171: the content. The table above in requirement form: the first line; one line per
     image loaded with its reader's facts, one per profile read; the `analysis`, `curves` and
     `proof` runs' start and done lines with the stage times; the figures of every result and
     every recommendation with its evidence at Debug; every file written with its size; every
     exception with its type, message and stack, those the program recovers from included (a
     profile that cannot be read, the settings file, a link, the title bar, a save that fails),
     except a computation a newer one superseded and the log's own probes; the window's runs
     numbered, its dialogs and actions; a last line with the exit code and the elapsed time;
     English with invariant numbers whatever `--lang`, a message the user saw as shown; nothing
     added to stdout or stderr. The example is written in step 7 from the reference image's
     run.
   - R-172: the level. Debug unless `APA_LOG` names another (`off`, `fatal`, `error`, `warn`,
     `info`, `debug`, `trace`, any case); any other value means Debug; Trace adds every FITS
     header card; `off` creates neither the folder nor a file.
   - R-173: two processes of the same executable writing the same file at full speed both keep
     every line, each whole and once, across the 1 MiB roll too, through
     `NLog.Targets.AtomicFile`, on both builds of it (the console's `net8.0`, the window's
     `net8.0-windows7.0`). The example is the test's figures.

   Then the packages: `Directory.Packages.props` gets `NLog` 6.2.2 and
   `NLog.Targets.AtomicFile` 6.2.2 with DarksCheck's two comments (the why, the measurement
   cited as FlatCheck's, "keep its version equal to NLog's", the suite's bundle naming the same
   version); `AstroPhotoAnalyzer.Core.csproj` the two `PackageReference`s with a comment like
   DarksCheck's; `dotnet restore AstroPhotoAnalyzer.sln --force-evaluate` updates the five
   `packages.lock.json` (core, console, window, two test projects), all committed;
   `ThirdParty.cs` a fourth component `NLog` with DarksCheck's role in both languages through
   `Tr.Pick`, licence `BSD-3-Clause`, DarksCheck's copyright line, `https://nlog-project.org/`,
   packages `new[] { "NLog", "NLog.Targets.AtomicFile" }`, with the comment on the two
   packages of one commit (`d9ebf82`); `THIRD-PARTY-NOTICES.md` a section `## NLog` after
   `## Wacton.Unicolour` and before `## Test components`, separated by `---` as the others,
   DarksCheck's section word for word with AstroPhotoAnalyzer's names and ADR 0069 in place of
   ADR 0025, the BSD text included, the file's "How to add an entry" followed; a row in the
   README's Licence table (by hand, the rule "Package notices"): `| NLog and
   NLog.Targets.AtomicFile 6.2.2 | writing the diagnostic log (see "Logs"), two windows into
   one file | BSD-3-Clause |`; `ThirdPartyTests.R122_EveryComponentNamesItsLicenceAndUrl`'s
   `Assert.Equal(3, ...)` becomes 4, and R-122's example in `docs/requirements.md` names NLog
   among the packages. Commit: "Take NLog and its atomic file target for the diagnostic log,
   and state the log's four requirements".

2. **`LogSetup`, `LogText`'s basics, `RunClock`, `LogCapture`, `LogFiles`, the file tests.**
   The three core files and the two test helpers as described; `LogCapture` has a
   `[ModuleInitializer]` (beside `RunsInRomanian`'s, with its `CA2255` pragma) that sets one
   `CaptureTarget` at Trace rendering `LogSetup.LineLayout` and `LogManager.ThrowExceptions =
   true` (ImageVisualCompare's addition: a layout or target exception fails the test that
   reaches it), an `AsyncLocal` scope opened by `LogCapture.Start()` (one per call tree,
   `Dispose` closes it), `Events` (level, logger, message, the rendered line, the exception),
   `Messages`, `Text`.
   `LogFiles` holds the line-prefix and elapsed-time regexes, `Factory`, `FilesOf` and
   `TwoWritersKeepEveryLine(folder, executable, output)`. The file tests of `LoggingTests` build
   the applications' configuration through `LogSetup.Build` into a private `LogFactory` per
   "process", in a `TempWorkspace`, and never touch the assembly's configuration:
   - `R170_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName`
   - `R170_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback` (a file where the folder
     should be)
   - `R170_TheLogRollsAt1MiBAndKeepsAtMost5Files` (8 MiB of 4 KB lines; the five names; the
     next factory appends to the newest; the file sizes written to the test output)
   - `R170_OneExecutablesRollingLeavesTheOthersFilesAlone` (`AstroPhotoAnalyzer.Wpf` rolled
     once survives `AstroPhotoAnalyzer`'s eight rolls, and the other way round: the prefix case)
   - `R170_TheFilesBeginWithTheExecutablesName` (theory: a path, null, the `dotnet` host)
   - `R170_AReadOnlyNewestLogFileSendsTheLogToTheFallback` (the append probe)
   - `R170_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe` (a stream open with the atomic
     target's share mode)
   - `R171_TheFirstLineCarriesTheFacts` (`StartupLine` on known values)
   - `R171_TheSameCrashSeenByTwoHooksIsWrittenOnce` (through `LogSetup.Fatal` and the capture)
   - `R171_ATaskExceptionAlreadyWrittenIsNamedNotWrittenAgain` (`Wrote` then `Unobserved`)
   - `R171_TheConfigurationHasOneFileTargetAndNoConsole`
   - `R171_ARunsClockWritesItsStagesWithItsOutcomeOrWithoutOne`
   - `R171_TheFiguresOfARecordFollowOneRule` (a probe record with every kind of value: a
     number, a missing one, a flag, an enum, a tuple, a nested record, an internal property, a
     field, a list of 3, a list of 30, a computed string, a delegate, a throwing property)
   - `R172_TheLevelComesFromTheEnvironmentVariable` (theory over the eight values and junk)
   - `R172_OffCreatesNeitherTheFolderNorAFile`
   - `R173_TwoProcessesWritingAtFullSpeedKeepEveryLine` (two factories on one file, two
     threads released by a `Barrier`, 4000 lines each crossing the roll, every line whole and
     once; the first file's end written to the test output)
   - `TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal`
   Commit: "Configure the log in code: the folder, the rolling, the level and the atomic target".

3. **The core's seams.** `ImageLoader`, `FitsReader`, `ImageSharpReader`, `IccPaperBlack`,
   `AnalysisRun` (the clock in a private body both overloads share, started before the load),
   `CurveRun`, `CurveExport`, `ProofRun`, the `Cause` of `GamutTable` and `ProofProfile` (D4),
   and the `LogText` members they need (`Source`, `FitsKeys`). Tests, in `LoggingTests`, each
   on a scene of `Scenes.cs` or a fixture of `PngTestData` / `TiffTestData` / `FitsTestData` /
   `IccPrinterTestData` with a `LogCapture`:
   - `R171_AnAnalysisLogsItsStartEveryResultTheRecommendationsAndTheStageTimes` (the
     `analysis start` line with the parameters, one figures line per result, one line per
     recommendation, the `analysis done` line with every stage)
   - `R171_ALoadedImageIsOneLineWithItsReadersFacts` (a PNG and a FITS: the `loaded` line,
     the FITS `header` line at Debug)
   - `R171_ALinearImageIsARefusedDoneLineNotAFailure`
   - `R171_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached` (a truncated FITS: `analysis
     ended without an outcome after N ms (load N ms)` is in the capture, the exception is not,
     since the run does not catch it)
   - `R171_AProfileThatCannotBeReadIsLoggedWithItsException` (junk bytes: the Warn line
     carries the exception; `Installed` on a folder of mixed files lists its count)
   - `R171_TheCurvesRunLogsItsAnchorsEachSuggestionAndAGamutThatCannotBeBuilt`
   - `R171_TheProofRunLogsItsProfileAndItsOutcome`
   - `R171_ASavedCurveIsOneLineWithItsSize`
   - `R171_TheLogIsTheSameInBothLanguages` (the same scene under `Tr.Use(English)` and
     `Tr.Use(Romanian)`: the loggers, levels and messages equal once the values the user's
     language reaches are blanked (`note=`, `text=`, `because=`, `problem=`, `axis=`, and the
     refusal lines); and the English run's whole text holds no word of `RomanianWords`)
   - `R172_TraceHoldsEveryFitsHeaderCard` (a FITS under the capture: its `BITPIX` card is a
     Trace line of `FitsReader`'s logger)
   Commit: "Log the core's seams: every image and profile read, the three runs' stages and
   figures".

4. **The console.** `Program.Main` and the lines of `AnalyzerCli`. Tests:
   - `R171_TheConsoleLogsAFailureWithItsStack_AndStderrKeepsOneLine` (a truncated FITS: stderr
     has the one `ERROR:` line it has today, the capture has `run failed` with the exception's
     type and stack)
   - `R171_ARefusedOptionIsLoggedAtWarnAsShown` (`--medium x`, `--lang fr`, a folder, a missing
     file)
   - `R171_TheConsoleAddsNothingToStdoutOrStderr`: the golden tests already pin both streams
     with the capture active; this test runs one golden scene and asserts the capture holds
     lines while stdout equals the golden file, so the claim is named by a test (make
     `GoldenOutputTests`' scrubbing and golden folder reachable, `internal`, as DarksCheck did).
   - `R171_TheRealConsoleWritesItsFirstAndItsLastLine` and
     `R172_TheRealConsoleTakesItsLevelFromAPA_LOG`: the built console beside the tests
     (`AstroPhotoAnalyzer.exe` in the test output; check it is copied there by the project
     reference, else reference the apphost path), started with `--help` in a temp working
     folder, its level variable removed or set; its lines read from `log\` beside the test
     output by its process id while the test holds the process (DarksCheck's `RunTheConsole`
     and `ConsoleLinesOf`).
   Run the golden tests and read their diff: there must be none; if a golden changes, stop,
   something leaked into a stream. Commit: "Start and close the log around the console's run,
   and log its mode, refusals and failures".

5. **The window.** `App`, `Loggers`, `MainViewModel`, `MainWindow`, `WindowSettings`,
   `CurvesTab`, `SoftProofTab`, `IccPickerWindow`, `DarkTitleBar`, `AboutWindow`.
   `WindowLoggingTests` (collection `fereastra` like the other window tests, `LogCapture` and
   `LogFiles` linked; the view-model alone where it can be, as `ViewModelTests` runs it):
   - `R173_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows`
     (`typeof(AtomicFileTarget).Assembly`'s `TargetPlatformAttribute` is `Windows7.0`, then
     `LogFiles.TwoWritersKeepEveryLine` on `AstroPhotoAnalyzer.Wpf`)
   - `R171_AnOpenLogsTheRunItsPathAndItsOutcome`
   - `R171_AFailedRunIsLoggedWithTheExceptionAndTheScreenKeepsTheMessage`
   - `R171_ARefusedFieldIsLoggedAtWarnAsShown`
   - `R171_AProfileChosenAndClearedIsLogged`
   - `R171_ASavedReportIsOneLineWithItsSize`
   - `R171_ASettingsFileThatCannotBeReadIsLoggedAndTheDefaultsAreUsed` (`WindowSettings.Read`
     on a junk file in a `TempWorkspace`)
   - `R171_AnExceptionTheWindowDoesNotHandleIsWrittenOnceAndTheHookNamesIt`
   - `R171_TheWindowsLinesAreTheSameInBothLanguages`
   - `R171_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays` (the About window on the UI
     thread, a `RequestNavigate` with a scheme nothing handles)
   Commit: "Start and close the log with the window, and log its runs, dialogs and quiet
   failures".

6. **The invariants.** In `LoggingTests`, over the source tree (`TestData.RepoDir()`), bin and
   obj excluded:
   - `OnlyTheTwoEntryPointsConfigureTheLog`: `LogSetup.Apply(` and `.Close(` only in
     `AstroPhotoAnalyzer/Program.cs` and `AstroPhotoAnalyzer.Wpf/App.xaml.cs`;
     `LogManager.Configuration =` or `LogManager.Setup(` only in `LogSetup.cs` and
     `LogCapture.cs`.
   - `NoLogLineGoesThroughTheTranslation`: DarksCheck's scan over the three code folders: no
     `Log.<Level>(` call, `RunClock.Start(`, `.Lap(` or `.Done(` whose text up to `);` contains
     `Tr.Pick(`; every logger declared as `static readonly Logger Log = LogManager.GetLogger(`
     or `static Logger Log => LogManager.GetLogger(`; no qualified `NLog.Logger`; no `Logger`
     variable of another name.
   - `NothingThatComputesOrWordsTheReportLogs`: no `NLog`, `Logger` or `LogManager` in
     `AstroPhotoAnalyzer.Core/Analysis/`, `Report/`, `Thresholds.cs`, `Parameters.cs`,
     `Language.cs`, and in `Input/` except `ImageLoader.cs`, `FitsReader.cs`,
     `ImageSharpReader.cs`, `IccPaperBlack.cs` (D4); and those four are the files of `Input/`
     that do log, no more.
   Commit: "Pin where the log is configured, that it never goes through the translation, and
   where it never writes".

7. **Measure, document, finish.** In the worktree, copy the reference images into
   `AstroPhotoAnalyzer.Tests/testdata/large/` (ADR 0004; `docs/validation.md` says where), then
   `dotnet build -c Release`, then the console on `de_printat.png` three times with the log on
   and three with `APA_LOG=off`, elapsed times written down; the log's size and line count per
   run; the window opened once on the same image with a profile (`--lang ro`), and twice at the
   same time, the second's lines present in the same file, each closed with `WM_CLOSE`
   (`CloseMainWindow`), no input, and only while the owner is not using the PC (the memory rule
   "live input only to a foreground app"); `/validate` run, the drift table kept (D8). Publish
   once with `scripts/release.ps1 -Version 1.0.0 -PublishOnly -WhatIf` first, then without
   `-WhatIf`, and run `artifacts\1.0.0\console\AstroPhotoAnalyzer.exe --help` from another
   folder: `log\AstroPhotoAnalyzer.log` must appear beside the single-file executable
   (`AppContext.BaseDirectory` is the executable's folder for a single-file publish; check it,
   do not assume it); hash `artifacts\1.0.0\wpf\NLog.dll` and `NLog.Targets.AtomicFile.dll`
   against `..\AstroDarkroom\artifacts\1.0.0\` when that folder exists (the suite's bundle);
   delete `artifacts\` after. Then the documents, in one commit:
   - `docs/decisions/0069-a-diagnostic-log-with-nlog-as-flatchecks-written-at-the-seams.md`:
     Context (the ask, FlatCheck's ADR 0070 taken through DarksCheck's), Decision (D1 to D9 as
     taken, the measured figures of this step, the seams table in short, FlatCheck's
     measurements cited as such), Alternatives rejected (FlatCheck's, plus `ASTROPHOTOANALYZER_LOG`,
     hand-written figure lines, logging inside `Analysis/`), Consequences (the fourth component,
     the invariant tests, the log is the first thing to ask a reporter for, the validation
     re-run on a log-only change). Check `main`'s latest ADR number before the merge (0068 on
     2026-10-31); renumber on the branch if it moved.
   - `docs/design/logging.md`: DarksCheck's note's shape (how the parts fit, the files
     measured, the line, a run's lines, what is logged where, reading a log, in the tests),
     AstroPhotoAnalyzer's content, with `**Covers:**` naming the three `Logging/` files,
     `AstroPhotoAnalyzer/Program.cs`, `AstroPhotoAnalyzer.Wpf/App.xaml.cs`,
     `AstroPhotoAnalyzer.Wpf/Loggers.cs`, the four test files; a row in `docs/design/README.md`'s
     index ("The diagnostic log: the configuration, the files, the line, what each seam writes,
     the capture in the tests", read before editing anything in `Logging/`, a log line anywhere,
     or the two entry points); the notes whose Covers already name a touched file get a line
     each: `pipeline.md` (the clock in `AnalysisRun`), `input.md` (the four readers' lines),
     `console.md` (`Program.Main`, `AnalyzerCli`'s lines), `window.md` (the two loggers, the
     lines), `dependencies.md` (the two packages), `testing.md` (`LogCapture`, the capture per
     call tree), `code-map.md` (one line per new file), `soft-proof.md` (`ProofRun`'s clock).
     `scripts/check-design.ps1 -Since main` must pass.
   - `README.md`: a section `## Logs` after "How the colour is judged" and before "Building",
     DarksCheck's text with AstroPhotoAnalyzer's names and `APA_LOG`, the sizes measured here
     (the suite's README already promises the fallback folder under `%LOCALAPPDATA%`); the
     Licence table's NLog row (step 1); one sentence in "The window" where it names
     `settings.json` as what the window writes, if it says so.
   - `CHANGELOG.md`, `[Unreleased]`: under Added, the log (DarksCheck's bullet adapted: where,
     the five files, what it holds, `APA_LOG`); under Internal, NLog as the fourth third-party
     component with its atomic target, the core logging at its seams, every test running every
     log line, `GamutTable` and `ProofProfile` carrying their cause.
   - `docs/requirements.md`: R-170 to R-173 `implemented`, R-171's example cut from the
     reference image's log (the first line, the `loaded` line, the `analysis done` line, one
     recommendation line, the last line).
   - `CLAUDE.md` (186 lines; at most 200, `RepoInvariantTests`): `Logging/` in the core's
     layout line; the sentence of D8 in the rule "Computed only in the core"; `LogSetup`,
     `RunClock`: `docs/design/logging.md` and ADR 0069 in "Read before touching"; `APA_LOG`
     on the Run line of Commands. Six lines at most.
   - `docs/skills.md`: under `/new-feature`, at step 5, one line: a new seam (a run, a file
     read or written, a dialog) logs as `docs/design/logging.md` says.
   - `docs/validation.md`: the re-run's date and "no drift" line, as the skill writes it.
   - This plan's "What deviated" section.
   Commit: "Record the log's decision, mechanics and user guide, with the figures measured on
   the reference image".

8. **Wrap-up.** `/wrap-up`: build once with `dotnet build AstroPhotoAnalyzer.sln -c Release
   --no-incremental -warnaserror --no-restore` (an incremental build hides warnings), then
   `scripts/verify.ps1`; read the whole diff back and summarise it; offer the owner the subagent
   review of the branch (`git diff main...HEAD`), then one of the fix commit alone (the owner's
   habit, two rounds); propose the merge, which the owner decides; after the merge, `git branch
   -d` and the worktree removed.

## Verification

- `scripts/verify.ps1` green at every commit; `dotnet format AstroPhotoAnalyzer.sln
  --verify-no-changes` clean; the `--no-incremental -warnaserror` build clean (CS8632 on a `?`
  with Nullable off, a doc comment `cref` that does not resolve).
- The golden files unchanged: `git diff --stat main -- AstroPhotoAnalyzer.Tests/Golden/` empty.
- `scripts/check-design.ps1 -Since main` green; `ThirdPartyTests`, `LicenseHeaderTests`,
  `RepoInvariantTests` green with the four new source files and the four requirement rows.
- `/validate`: every case `MEASURED`, no drift (D8), pasted in the ADR.
- The figures pasted in the ADR and in R-171's example, from step 7: the elapsed times with the
  log on and off (three each), the log's size and lines per run, the two windows' lines in one
  file, the published executable's `log\` folder, the DLL hashes against the suite's bundle.
- `R173_TwoProcessesWritingAtFullSpeedKeepEveryLine` (core) and
  `R173_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows` (window) passing are the
  measurement of D2 on both builds; their output pasted in the ADR.

## Traps

- `AnalyzerCli.Run` and `MainViewModel` must stay free of `LogSetup.Apply`: the tests call them
  in-process, and the capture would be overwritten by the file configuration.
- The logger of `LogSetup` and of `App` is a property fetched on use; a `static readonly
  Logger` there loads a stray `NLog.config` before `Apply` (FlatCheck's measurement).
- Every message is one plain string: `Info(string, params object[])` parses `{}` templates,
  `Info(string)` does not; a path with `{` would be mangled or throw.
- Lines that cost something are behind `IsDebugEnabled` / `IsTraceEnabled`; `Figures` must not
  touch shared state (the proof's mask runs in `Parallel.For`, the runs on pool threads).
- `Figures` over `internal` members needs `BindingFlags.NonPublic`. "A hand-written getter" is
  one whose `GetMethod` does not carry `CompilerGeneratedAttribute` (an auto-property's does):
  a string there is skipped, a number or flag written. `Recommendation.Text` and `Because`,
  `FileFactsResult.ReaderNote`, `CropResult.Axis` are auto-properties and are written as shown,
  which is wanted (the user's language, the "same in both languages" test blanks them).
- `AstroPhotoAnalyzer.Core/Input/AtomicFile.cs` is the project's own atomic writer, unrelated to
  the package `NLog.Targets.AtomicFile`, whose type is `NLog.Targets.AtomicFileTarget`: the
  names do not collide, but a file with `using NLog.Targets;` inside namespace
  `AstroPhotoAnalyzer` must be read with that in mind.
- `RepoInvariantTests.ClaudeMd_StaysShort` fails `CLAUDE.md` above 200 lines; it has 186. The
  additions of step 7 fit in six lines or less (one in Layout, one sentence in a rule, one in
  "Read before touching", the variable on the Run line).
- `ThirdPartyTests` claims packages from the core's csproj only and pins the component count at
  three: NLog is referenced by the core (D1), and the count moves to four with the component.
- The console's exe reaches the test output through the test project's reference to the console
  project; the real-console tests start it from `AppContext.BaseDirectory` and leave a `log\`
  there, which the root `.gitignore` already ignores (`*.log`).
- `Raster` and `DustMap` are classes with fields and large arrays: the array rule (ends and
  count past 24 items) keeps a figures line to a few hundred bytes; measure the longest line on
  the reference image in step 7 and write it in the ADR.
- `RestorePackagesWithLockFile` is on: a restore without `--force-evaluate` after adding the
  packages fails in locked mode; commit the five lock files.
- `InvariantGlobalization` is on in the core and the console, off in the window: the log's
  numbers go through `LogText`, which pins the invariant culture, so both agree.
- The window's `App` is not built by the tests (`StaFixture` builds a plain `Application`);
  `WindowSettings` is bound to a path, so the settings test uses a file of its own.
- The console's log name is a prefix of the window's (`AstroPhotoAnalyzer`,
  `AstroPhotoAnalyzer.Wpf`): the rolling test and `NewestLog` are checked on the real names.
- The window tests' collection is `fereastra`, Romanian on purpose (ADR 0048); the test
  assemblies run in Romanian by default (`RunsInRomanian`), so a test of English lines sets
  `Tr.Use(UiLanguage.English)` itself.
- The Stop hook refuses a turn that edited code until `verify.ps1` ran green after the last
  edit; run `/wrap-up` when it fires.
- ADR numbers collide at merge when `main` moved; check before proposing the merge.
- A real window run at step 7 only while the owner is away from the PC; two console processes
  on one file stand in otherwise, and the plan's "What deviated" says which was done.

## What deviated

Written at the end of step 7, 2026-10-31. The session that planned it implemented it, on Opus,
after the owner's approval.

- **The statuses.** R-170 to R-173 turned `implemented` in step 2, with their first tests, not in
  step 7: `RepoInvariantTests.EveryTestedRequirement_IsMarkedImplemented` refuses a row tested
  by name and left `accepted`. R-171's example was written in step 7, as planned.
- **The figures' rule.** `LogText.Figures` walks the types of the assembly of the record it is
  given, not the core's alone, so its test can use a probe record of the test assembly; and the
  names it skips are skipped at every depth (DarksCheck's at the top only), because the region
  names that carry the user's language sit in records held by the results. `LogText.Nums` (the
  curves' class levels) and `LogText.WorkingSet` (the analysis's done line, `workingSet=`, as
  DarksCheck writes it per frame) were added.
- **Two inner exceptions.** `ImageSharpReader.Load` and `AnalysisRun.BorrowWcs` wrapped the
  reader's exception into a new one without it; they pass it on as the inner exception now, so
  its stack reaches the log. The user's message is unchanged (it held the inner message
  already, and `Describe` skips an inner message the outer one contains).
- **The words.** The console's refusals go through one helper, `Refuse`; the plan's names of the
  window's secondary windows became `opened the map window` and the like; a settings file read
  without trouble writes nothing (the view-model's `profile restored from the settings` says
  what was read) and each write is a Debug line.
- **The tabs.** `Window.Curves` and `Window.SoftProof` write no `run N started` or `done` lines:
  `CurveRun` and `ProofRun` write the start and done lines with the stages already, so the tabs
  log only what the core cannot see (ADR 0069, "Alternatives rejected").
- **The tests.** As planned, with these names: `R171_ALinearImageIsARefusedDoneLineNotAFailure`,
  `R171_TheReportTheConsoleWritesIsOneLineWithItsSize` (added), and in the window
  `R171_ALinearImageAndABadFieldAreRefusalsNotFailures`,
  `R171_ASavedReportIsOneLineWithItsSize_AndAFailedSaveItsException`,
  `R171_ASettingsFileThatCannotBeReadOrWrittenIsLoggedAndTheDefaultsAreUsed`.
  `GoldenOutputTests.RunCli` and `GoldenDir` became internal for
  `R171_TheConsoleAddsNothingToStdoutOrStderr`. No golden file moved.
- **The About window** grows by NLog's card to 804 DIP of content in English and 819 in Romanian,
  measured offscreen; it has no scroll bar, so on a small work area its bottom falls off the
  screen. Recorded in `known-limitations.md`, not fixed: a task of its own if the owner wants it.
- **Step 7, the window.** The owner had been idle ten minutes with only the Claude app in front,
  so the real window was started: once on `de_printat.png` with `--lang ro` (41 lines, the
  analysis, the curves and the proof, exit 0 after 25.5 s), then twice at once on two images,
  42 whole lines each in one `AstroPhotoAnalyzer.Wpf.log`; every window closed with `WM_CLOSE`,
  no input sent.
- **Names.** The ADR is `0069-a-diagnostic-log-with-nlog-as-darkschecks-written-at-the-seams.md`
  (the plan said "as-flatchecks"): DarksCheck's code is the one taken.
