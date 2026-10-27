# Add a diagnostic log with NLog

Plan for the task branch `claude/Add-a-diagnostic-log-with-NLog`, worktree
`.claude/worktrees/Add-a-diagnostic-log-with-NLog`. Written for an implementer who has this file,
`CLAUDE.md` and the repository, and nothing else from the planning session. Approved by the
owner on 2026-10-27.

## Context

A user reports "it failed" or "the verdict is wrong" and all we have is the screen: the console
prints `ERROR: <message>` without the exception type or the stack (`FlatCheck/FlatCheckCli.cs:128`),
the window shows the message and the inner messages on the tab and deliberately drops the stack
(`FlatCheck.Wpf/ViewModels/TabViewModel.cs:195`), and nothing records what was loaded, what was
measured, which lens profile was chosen or how long each stage took. Several places swallow an
exception for good reasons (the preview decoder, the profile folders, the raw beside the master)
and leave no trace at all. Nothing in the three projects logs today: no `Trace`, no
`Debug.WriteLine`, no `ILogger`.

The owner's ask (2026-10-27): a log, with NLog, text format, in `log/` beside the executable, file
names starting with the executable's name, at most 5 files per executable, rolling, at most 1 MB
each. What is logged and how much is the implementer's call, with one criterion: a bug report with
the log attached can be diagnosed from the log.

## Owner's decisions (2026-10-27)

Put to the owner with the plan, each with its recommendation; the owner approved the plan as
written on 2026-10-27, which settled every row as recommended.

| # | Decision | Recommendation and why |
| --- | --- | --- |
| D1 | Where NLog lives | The core references the package and logs at its seams; each application configures NLog **in code** at its entry point through one call, `LogSetup.Apply`. No `NLog.config` file: nothing to ship, nothing to go stale, nothing the user can break. |
| D2 | Default level and how to change it | File level **Debug** by default (a run is 60 to 150 lines, about 10 KB, so 5 MB keeps hundreds of runs). **Trace** holds the bulk lines (every FITS card, every Siril output line). One environment variable, `FLATCHECK_LOG` = `off`, `error`, `warn`, `info`, `debug`, `trace`, read by `LogSetup`; no new console option, so `Options`, the usage and the goldens stay untouched. |
| D3 | Two processes on the same file | `KeepFileOpen = false`: NLog 6 removed `ConcurrentWrites`; with the file closed between writes, two instances of the window append to the same file without a lock. The cost (open and close per line) is nothing at this volume. No second package (`NLog.Targets.AtomicFile`). Every line carries the process id so interleaved runs separate. |
| D4 | Where the log calls go | At the **seams**: `Runs/`, the loaders' and locator's catch sites, the two applications. Never in `Analysis/`, `Report/`, `Metrics.cs`, `Stats.cs`: the maths stay pure (and Stryker's mutation reports stay about the maths). A run logs the result's figures after the analyzer returns them. |
| D5 | Log folder not writable (exe under Program Files, read-only media) | Fall back to `%LOCALAPPDATA%\FlatCheck\log\` and say so in the first line. A log that silently does not exist defeats the purpose. |
| D6 | File naming and rolling | NLog 6's default roll-forward: `flatcheck.log`, `flatcheck_01.log`, `flatcheck_02.log` ... (`ArchiveSuffixFormat` default `_{0:00}`), `ArchiveAboveSize` 1 MiB, `MaxArchiveFiles` tuned by the rolling test so **5 files at most** exist. No `ArchiveFileName`, so NLog never moves a file a viewer may hold open. The user is told to attach the whole `log/` folder. |
| D7 | Crash behaviour | Unchanged. The unhandled-exception hooks log at Fatal and flush; they do not mark the exception handled. |
| D8 | Language of the log | English only, never through `T(en, ro)`; verdicts logged as their key strings (`OK`, `SUSPECT`, `PROBLEMA`, ...); numbers in the invariant culture. The log is read by the maintainer and the model (the kit's rule "Languages"). |

## Design

### The package

- `NLog` **6.2.2** (nuget, 2026-10-15; BSD-3-Clause; netstandard2.0, so net8.0 is fine). Only the
  core project references it (`FlatCheck.Core/FlatCheck.Core.csproj`); the console and the window
  get the assembly transitively through their project reference, which is what `ThirdPartyTests`
  expects (it reads the core csproj only).
- `Directory.Packages.props`: one `PackageVersion` with its "why" comment (diagnostic log; chosen
  for the in-code configuration, the size-based rolling file target and the BSD licence; no native
  code).
- Lock files: `dotnet restore --force-evaluate` regenerates all five `packages.lock.json`
  (the three application projects and the two test projects take NLog transitively).
- `FlatCheck.Core/ThirdParty.cs`: a fifth `ThirdPartyComponent("NLog", role, "BSD-3-Clause",
  <the copyright line of the package's LICENSE.txt, copied, not retyped from memory>,
  "https://nlog-project.org/", new[] { "NLog" })`. The role is a `T(...)` pair like the others.
  `ThirdPartyTests.EveryComponentNamesItsLicenceAndUrl` asserts exactly four components: it
  becomes five, which is the intended change of that test, not a test edited to pass.
- `THIRD-PARTY-NOTICES.md`: a `## NLog` section (Role, Licence, Compatibility: BSD-3-Clause is
  GPLv3-compatible, the FSF lists it; copyright line; Project and Licence links), before the
  "Test components" table. The README's "Third-party components" table gets a row. The About
  window lists `ThirdParty.Components`, so NLog appears there by itself; check `AboutTests`
  for a count.
- `docs/design/dependencies.md`: a pointer paragraph (NLog, ADR 0070, why no second package).

### `FlatCheck.Core/Logging/LogSetup.cs` (new)

The one place that knows NLog's configuration. Internal static class:

```csharp
internal const long FileSizeLimit = 1_048_576;          // 1 MiB per file
internal const int FilesPerExecutable = 5;               // active file + archives
internal const string LevelVariable = "FLATCHECK_LOG";
internal const string FolderName = "log";

internal static string ResolveFolder(string baseDirectory, string fallbackRoot);
internal static LogLevel LevelFrom(string variableValue);         // null/empty/unknown -> Debug, "off" -> LogLevel.Off
internal static LoggingConfiguration Build(string executableName, string folder, LogLevel minimum);
internal static string StartupLine(string executableName, string[] args, string folder, bool fellBack);
internal static void Apply(string executableName, string[] args);  // the apps' one call
internal static void Close(int exitCode);                          // the apps' last call
```

- `ResolveFolder`: `Path.Combine(baseDirectory, "log")`; `Directory.CreateDirectory` then open
  and close a file `<exe>.log` for append; any exception → `Path.Combine(fallbackRoot,
  "FlatCheck", "log")`, created the same way. Returns the folder; `Apply` passes
  `AppContext.BaseDirectory` (for the single-file console that is the exe's folder) and
  `Environment.GetFolderPath(LocalApplicationData)`.
- `Build`: a `FileTarget` named `file` with `FileName = <folder>/<exe>.log`, `ArchiveAboveSize =
  FileSizeLimit`, `MaxArchiveFiles` = the value the rolling test settles (start at
  `FilesPerExecutable - 1`; NLog's docs do not say whether the active file counts, so measure:
  write 8 MiB through the configuration, count the files, adjust, and record the measured
  behaviour in the design note), `KeepFileOpen = false`, `AutoFlush = true`, `Encoding = UTF-8`
  without BOM, `CreateDirs = true`. Layout:
  `${date:format=yyyy-MM-dd HH\:mm\:ss.fff} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger:shortName=true} ${message}${onexception:${newline}${exception:format=ToString}}`.
  One rule `*` → `file` at `minimum`. `DefaultCultureInfo = CultureInfo.InvariantCulture`.
  No console target, ever: stdout and stderr are the golden-tested contract (R-001, R-008).
- `Apply`: resolves the folder, reads the level from the environment, sets
  `LogManager.Configuration`, `LogManager.ThrowExceptions = false`, hooks
  `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` to log
  Fatal/Error with the exception and `LogManager.Flush()`, then writes the startup line at Info.
  Idempotent (a second call is ignored) so a test can never double-configure through it.
- `StartupLine`: `<exe> <VersionInfo.Value> starting; exe=<Environment.ProcessPath>;
  args=["..", ".."]; cwd=..; log=<folder> (fallback: yes/no); level=<minimum>;
  os=<RuntimeInformation.OSDescription> <OSArchitecture>; runtime=<FrameworkDescription>;
  culture=<CultureInfo.CurrentCulture.Name or "invariant">; tz=<TimeZoneInfo.Local.Id>;
  pid=<id>`. The arguments are logged verbatim (paths are the diagnosis).
- `Close`: `Info("exit <code>, elapsed <s> s")` then `LogManager.Shutdown()`.
- Loggers elsewhere: `private static readonly Logger Log = LogManager.GetCurrentClassLogger();`
  per class that logs. With no configuration (the tests, the library used elsewhere) NLog
  discards everything and creates no file.

### The applications

- `FlatCheck/Program.cs`: `Main` becomes
  `LogSetup.Apply("flatcheck", args); int code = 1; try { code = FlatCheckCli.Run(...); return code; } finally { LogSetup.Close(code); }`.
  The executable name is `Path.GetFileNameWithoutExtension(Environment.ProcessPath)` with the
  entry assembly's name as the fallback (both give `flatcheck`; the window's gives
  `FlatCheck.Wpf`). `FlatCheckCli.Run` itself stays free of setup so the golden tests, which call
  it in-process, never configure NLog; it gains: Info `mode=<name> input=<path>` after dispatch
  is decided, Warn for each refusal that is not an exception (bad `--lang`, folder without
  `--light`, parse error with the message, the `--smooth`/`--synthetic` refusals), and in the
  catch at line 128 `Log.Error(e, "run failed")` before the stderr line (the stack lives in the
  log, stderr keeps its one line).
- `FlatCheck.Wpf/App.xaml.cs`: in `OnStartup` after `ForceInvariantNumbers()`:
  `LogSetup.Apply("FlatCheck.Wpf", e.Args)`; `DispatcherUnhandledException += (s, a) => Log.Fatal(a.Exception, "unhandled on the dispatcher")` (not handled, D7); `OnExit` override calls
  `LogSetup.Close(e.ApplicationExitCode)`. Info for the folder chosen from the arguments or
  the dialog, and for a bad `--lang`.

### What is logged, by seam

Every line is English, invariant numbers (`G6` for ratios, `F1` for ms and MB), paths verbatim.

| Seam | Level | Line (one per bullet) |
| --- | --- | --- |
| `ImageLoader.Load` / `LoadInfo` / `LoadImage` | Info | `loaded <path> (<size> MB) reader=<fits/raw> <W>x<H> planes=<n> bitpix=<n> cfa=<pattern or none> scale=<white>/<black> (<source>) camera=<..> focal=<..> fnumber=<..> lens=<id>/<mount>/<name> range=<..> in <ms> ms` |
| `FitsReader` | Debug | the cards that decide: `NAXIS*`, `BITPIX`, `BZERO`, `BSCALE`, `BAYERPAT`, `ROWORDER`, `XBAYROFF`, `YBAYROFF`, `INSTRUME`, `FOCALLEN`, `EXPTIME`, and the HDU chosen |
| `FitsReader` | Trace | every header card |
| `RawReader` | Debug | libraw's margins, `Filters`, `Cdesc`, `color.maximum`, the four `CBlack`, the preview's kind and size or "no preview" |
| `RawReader.FillShootingInfo` catch (line 304), `TryCapturePreview` catch (481) | Warn | the exception, one line each: "lens block unreadable", "preview capture failed" |
| `RawReader.Missing` | Error | the libraw load failure with the inner exception |
| `PreviewReader.Decode` catches (92-97) | Warn | "preview decode failed: <type>: <message>" |
| `ProfileLocator.Locate` | Info | `profiles: adobe dirs=<n> files=<n> skipped=<n> duplicate=<n>; lensfun <present/absent> <version> at <dir>; lenses adobe=<n> lensfun=<n> in <ms> ms` |
| `ProfileLocator` catches (131, 172, 180, 191) | Warn / Debug | the folder that could not be listed (Warn), the `.lcp` that did not parse (Debug, with the file) |
| `LensIdentification.Identify` | Debug | `shot: focal=<v> (<source>) fstop=<v> (<source>) crop=<v> (<source>) camera=<..> rawLens=<id>/<name>/<range> candidates=<n> [<names with mount and source>] byRangeOnly=<bool> notes=[..]` |
| `SeriesFinder.Find` | Info | `series: <n> files match <pattern> in <dir>` (and the exception when none, logged by the run) |
| every `Runs/*.Execute` and `Advise` | Info | `<run> start <path(s)> <options that matter: fov, sat, black, sky, scale, pattern, lens text, focal, fstop, crop>`; `<run> done verdict=<key> in <ms> ms (load <ms>, analyze <ms>, report <ms>, map <ms>)` |
| `StructureRun` | Debug | the `StructureResult` figures: `W x H, rawMedian, rawMax, fit centre (FitCx, FitCy) vs sensor centre, centroidOffsetPct, tiltPct, cornerMean, centreMedian, vignetteStops, cornerSpreadPct, azimuthalMaxPct, residualPtpPct, maxTilePct, localSpotPct at (x,y) flagged=<bool>`, and the five verdicts |
| `StructureRun.Compare` | Debug | per `ProfileDepth`: profile name, source, cornerStops, diffStops, errorPct, verdict, cameraCrop, extrapolation; then nearest, spreadStops, gap, skippedForCrop |
| `StructureRun` catch (157) | Warn | "raw beside the master unreadable: <path>" with the exception |
| `LightRun`, `LightSeriesRun` | Debug | suitability (fov, limit, refused), per frame `frame <i>/<n> <file> rad=<RadPct> plane=<PlanePct> kept=<k>/<t> verdict=<key> in <ms> ms`, the combined result |
| `ExposureRun` | Debug | `white, black, range, scaleSource, channel medians, cfaSpread, isCfa, brightest, centreMedianPct, globalMedianPct, centreClipPct, levelTooLow, looksDebayered, isSky, verdict, preview=<present/absent>` |
| `HistEqRun` | Debug | the histogram's input range and the LUT's end points (what `HistEqResult` exposes) |
| `SmoothRun`, `Advise` | Debug | `scalePx, tilePx, sigmaTiles, positions (tiles, rejected, rms, fitFailed each), removed p1/p99/ptp/maxTile/spot, flagged, inputTexture, outputTexture`; the advisor's proposal; the refusal reason |
| `SyntheticRun`, `AgainstRun`, `SplitRun` | Debug | the request (lens text, source, focal, fstop, crop and their sources), the lens chosen or the refusal kind, the profile nearest; Against's depth diff/error/verdict; Split's per-part verdicts and the map range |
| `WriteFits` (smooth, synthetic) and every PNG written by the console (`SaveMap`) | Info | `wrote <path> (<size> KB)` |
| Window `MainViewModel.OpenAsync` | Info | `session <folder>: <ok / problem title>`; Debug the `SirilSession` facts (master path, how it was chosen, lights found, raw beside the master, result text path) |
| Window `TabViewModel.RunGuardedAsync` | Info / Error | `tab <name> run start`, `tab <name> run done in <ms> ms`; `Log.Error(e, "tab <name> failed")` in the catch at line 136, **also** when `gen != _generation` (today that exception vanishes), as Debug "superseded run failed" |
| Window user actions | Info | folder chosen, file chosen on the Exposure tab, Run/Compare/Advise/Start/Stop commands, saves (path), the About window |
| Window save and silent catches (`StructureTabViewModel:424`, `SplitTabViewModel:362,405,531,650`, `SyntheticTabViewModel:235,245,352,460,634`, `SmoothTabViewModel:365`) | Warn | one line with the exception each; behaviour unchanged |
| `SirilRunViewModel` / `SirilScriptRun` | Info / Debug | siril-cli path and version, the script path, the private settings copy, start, exit code and elapsed at Info; every output line at Debug (the preprocessing run is the likeliest thing to fail on a user's machine) |
| `SirilInstall` catches (103, 149, 161, 199-212, 346) | Debug | what was looked for and why it was skipped |

Never logged: pixel data, header values other than those named, anything from outside the
session folder, nothing through `T()`.

### Requirements

New section `## Diagnostics` in `docs/requirements.md`, before `## Dropped`, same table header.

- **R-109** `implemented`. Each application writes a text log in the folder `log` beside its
  executable (`%LOCALAPPDATA%\FlatCheck\log` when that folder cannot be written, said in the
  first line). The file name begins with the executable's name; when the active file passes
  1 MiB a new one is started; at most 5 files per executable exist; the oldest is deleted.
  Example: after 8 MiB of lines through `flatcheck.exe`, `log/` holds 5 files named
  `flatcheck*.log`, none above 1 MiB plus one line.
- **R-110** `implemented`. The log carries what a diagnosis needs: a startup line (executable,
  version, arguments verbatim, working folder, OS, runtime, culture, time zone, process id),
  one line per file loaded (path, size, reader, dimensions, BITPIX, CFA, scale, camera, lens),
  one line per run with the mode, the input and the verdict and the time of each stage, the
  figures behind each verdict, the lens profiles found and the candidates, every file written,
  every exception with its type, message and stack trace, including those the program recovers
  from, and the exit code. Example: a structure run on a 6016x4016 FITS writes the startup
  line, `loaded ... 6016x4016 bitpix=16 ...`, `structure done verdict=OK in 1840 ms (...)` and
  `exit 0`. Nothing is written to stdout or stderr (R-001, R-008 unchanged).
- **R-111** `implemented`. The level is Debug unless the environment variable `FLATCHECK_LOG`
  holds `off`, `error`, `warn`, `info`, `debug` or `trace`; an unknown value means Debug. The
  log is English with invariant numbers whatever `--lang` says. Example: `FLATCHECK_LOG=off`
  and a run leave `log/` unchanged.

### ADR and design note

- `docs/decisions/0070-log-with-nlog-configured-in-code-at-the-seams.md`: Context (nothing to
  diagnose from), Decision (D1 to D8 as one decision with its values), Alternatives rejected
  (`Microsoft.Extensions.Logging` + a provider: two packages for one sink; Serilog: same
  capability, NLog asked for; an `NLog.config` file: ships and drifts; logging inside
  `Analysis/`: mixes maths and I/O, noise in the mutation score; a `--log` option: touches the
  console contract and the goldens; `NLog.Targets.AtomicFile`: a second package for a case
  `KeepFileOpen=false` already covers; a per-process file name: breaks "5 files per
  executable"), Consequences (a fifth third-party component, the log is the first thing to ask
  a reporter for, the other tools of the suite can take the same `LogSetup`).
- `docs/design/logging.md`: `Covers: FlatCheck.Core/Logging/LogSetup.cs, FlatCheck/Program.cs,
  FlatCheck.Wpf/App.xaml.cs` plus the seams. Sections: the file layout measured (which file is
  current, how NLog numbers them, the measured `MaxArchiveFiles`), the levels, the line format,
  the seam table above (kept in step with the code), reading a log (how to find a run: the
  startup line, the pid, the `exit` line), what a test may rely on. Add it to the index table of
  `docs/design/README.md`.
- `CHANGELOG.md` under `### Added`: the log (R-109 to R-111, ADR 0070), where it is and what
  to send with a bug report; under `### Internal`: NLog 6.2.2 as the fifth component.
- `README.md`: a `## Logs` section after "The window" (before "Development"): where the log
  is, that it holds paths and figures and nothing else, "attach the whole `log` folder to a bug
  report", the `FLATCHECK_LOG` variable; a row in the third-party table.

## Steps, in order

1. **Worktree and branch.** From `E:\Photografie\Code\FlatCheck` on `main`:
   `git worktree add .claude/worktrees/Add-a-diagnostic-log-with-NLog -b claude/Add-a-diagnostic-log-with-NLog main`.
   Work and build only there. Copy this plan to `docs/plans/2026-10-27-add-a-diagnostic-log-with-nlog.md`.
2. **Requirements.** R-109, R-110, R-111 in the new `## Diagnostics` section, `implemented`
   (RepoInvariantTests requires `implemented` once the `R###_` tests exist).
3. **The package.** `Directory.Packages.props`, `FlatCheck.Core.csproj` (`PackageReference
   Include="NLog"`), `dotnet restore --force-evaluate`, `ThirdParty.cs`, `THIRD-PARTY-NOTICES.md`,
   the `ThirdPartyTests` count 4 → 5, `dependencies.md`. Build green before any logging code.
4. **The failing tests** (next section), then `LogSetup.cs` until they pass.
5. **The applications.** `Program.cs`, `App.xaml.cs`, `FlatCheckCli.cs` as designed. Run the
   console from the worktree (`dotnet run --project FlatCheck -- <a fixture .fit>`) and read
   `FlatCheck/bin/.../log/flatcheck.log`; run the window on a session folder and read
   `FlatCheck.Wpf.log`.
6. **The seams**, in this order, each with its test where the table below names one: loaders,
   `ProfileLocator`, `LensIdentification`, the eight runs, the window's view-models and the
   Siril run. Keep every `catch` as it is; add the log line only.
7. **Docs.** ADR 0070, `docs/design/logging.md` and its index row, `dependencies.md`,
   `CHANGELOG.md`, `README.md` ("Logs" section and the third-party row).
8. **Gate and wrap-up.** `scripts/verify.ps1` green; `scripts/check-design.ps1 -Since main`;
   a last `dotnet build --no-incremental -warnaserror --no-restore` before the final verify;
   read the whole diff; commit on the branch in small commits (package; LogSetup and tests;
   apps; seams; docs); propose the merge. No formula moved, so `/validate` is not needed; say so.

## Tests

All in one xUnit collection `Logging` with `DisableParallelization = true` (pattern:
`LanguageTests.cs:141`), because they set the global `LogManager.Configuration`; each test
restores it to `null` in `finally`. A helper `LogCapture` (in `FlatCheck.Tests`, a copy or a
shared reference in `FlatCheck.Wpf.Tests`) installs a `MemoryTarget` at Trace and exposes the
lines. Tests never call `LogSetup.Apply`.

`FlatCheck.Tests/LoggingTests.cs`:
- `R109_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName`: `ResolveFolder(ws.Dir,
  fallback)` returns `ws.Dir/log`, creates it; `Build("flatcheck", folder, Debug)` applied to a
  private `LogFactory`, one line, the file `flatcheck.log` exists and holds the line.
- `R109_WhenTheFolderCannotBeWrittenTheLogFallsBackToLocalAppData`: `baseDirectory` is a path
  that is an existing *file*, so `CreateDirectory` fails; the result is under the fallback root
  the test passes (a temp folder, never the real LocalAppData).
- `R109_TheLogRollsAt1MiBAndKeepsAtMost5Files`: write about 8 MiB of 200-byte lines through a
  private `LogFactory`; assert at most 5 files matching `flatcheck*.log`, each at most
  `FileSizeLimit + 4096` bytes, and that the newest holds the last line. This test settles
  `MaxArchiveFiles`.
- `R110_TheStartupLineCarriesTheFacts`: `StartupLine(...)` contains the version
  (`VersionInfo.Value`), every argument verbatim, `pid=`, `os=`, `runtime=`.
- `R110_AStructureRunLogsTheLoadTheVerdictAndTheTimings`: with `LogCapture`, run
  `FlatCheckCli.Run` on a generated flat (`FitsTestData`); assert lines `loaded <path>`,
  `structure done verdict=`, a `(load` timing, and a Debug line with `residualPtpPct=`.
- `R110_AFailingRunLogsTheExceptionWithItsStack`: `FlatCheckCli.Run` on a file that is not a
  FITS; assert an Error line with `InvalidDataException` and `   at ` (the stack) in the
  captured text; assert stdout and stderr are what the golden expects (one `ERROR:` line).
- `R110_TheConsoleWritesNothingExtraToStdoutOrStderr`: covered by every golden; add one explicit
  assertion that `Build(...)` has exactly one target and it is a `FileTarget`.
- `R111_TheLevelComesFromTheEnvironmentVariable`: `LevelFrom(null)` is Debug, `"trace"` Trace,
  `"OFF"` Off, `"nonsense"` Debug.
- `R111_TheLogIsEnglishWhateverTheLanguage`: run under `Ui.Use(Romanian)`; the captured lines
  contain no `EROARE`, no `esuat`, and the verdict key, not the Romanian word.
- `OnlyTheTwoEntryPointsApplyTheConfiguration` (repo invariant, not an R test): grep the code
  folders of `kit.json`: `LogSetup.Apply(` appears in exactly `FlatCheck/Program.cs` and
  `FlatCheck.Wpf/App.xaml.cs`; `LogSetup.Close(` in the same two. Keeps the tests and the
  library free of a global configuration.
- `NoLogLineGoesThroughT`: grep `FlatCheck.Core` and `FlatCheck.Wpf` for `Log\.(Trace|Debug|Info|Warn|Error|Fatal)\(` lines containing `T(`; none.

`FlatCheck.Wpf.Tests/LoggingTests.cs`:
- `R110_ATabFailureIsLoggedWithTheException`: a `TabViewModel` whose work throws; the captured
  text has `Error` with the exception type; `ErrorText` unchanged.
- `R110_ASupersededRunFailureIsLoggedNotLost`: two generations; the first's exception appears
  at Debug.

## Verification

- `scripts/verify.ps1` green in the worktree (format, build with `-warnaserror`, every test,
  `check-design`, `check-decisions`, `sync -Check`).
- By hand, pasted at wrap-up: the console on a generated FITS and on a non-FITS, both logs
  quoted (startup line, loaded line, done or Error line, exit line); the window opened on a
  Siril session folder (`FLATCHECK_TESTDATA_LARGE` masters, or any folder with a master):
  `FlatCheck.Wpf.log` quoted for the session line, one tab run and the exit line; the window
  started twice at once, both pids present in one file; `FLATCHECK_LOG=off` leaves `log/`
  unchanged; `FLATCHECK_LOG=trace` shows the header cards.
- The rolling test's measured file list pasted into `docs/design/logging.md`.
- `dotnet publish` of the console as `kit.json` publishes it (`-p:PublishSingleFile=true`), run
  from a scratch folder: `log/flatcheck.log` appears beside the exe (single-file sets
  `AppContext.BaseDirectory` to the exe's folder; verify, do not assume).
- Golden files: no diff. `ThirdPartyTests`: the count line is the one intended change.

## Out of scope

- The other tools of the suite and the launcher: `LogSetup` is written so it can be lifted
  into each (same names, same env variable prefix per tool); a task of its own after this one.
- Sending or zipping the log from the window: the README asks the user to attach the folder.
- Any change to what the console prints or to the crash dialogs (D7).

## Outcome and deviations (2026-10-27)

Implemented as planned, on `claude/Add-a-diagnostic-log-with-NLog`; ADR 0070 and
`docs/design/logging.md` hold the result. What differs from the plan, and why:

- **`MaxArchiveFiles` is 5, not 4.** Measured: NLog 6.2.2 counts the file being written among its
  archives, and 4 left 4 files.
- **D3 held, measured, with one question left.** `KeepFileOpen = false` as approved. Measured
  against the file kept open and against `NLog.Targets.AtomicFile`: with two real windows on one
  session the closed file kept 28 and 28 lines where the open one kept 0 and 28; two processes
  writing 2000 lines each at full speed lose 33 to 48 of 4000 with the closed file and none with
  AtomicFile; a line costs 4.8 ms closed and 0.09 ms with AtomicFile. "No line lost under full
  load" became R-112, a draft, and taking the second package is put to the owner. A real
  structure run took 3.8 to 4.1 s with the log off and 4.0 to 4.4 s at Debug or Trace.
- **The siril-cli lines.** The plan said Trace in D2 and Debug in the seam table; the progress
  lines are at Trace and every other line at Debug, since a failed Siril run is the likeliest
  report.
- **The tests' capture.** Instead of a non-parallel collection that swaps the configuration per
  test, `LogCapture` configures the test assembly once, at Trace, and a test reads its own lines
  through an `AsyncLocal` scope; so every test runs every log line on its path. `LogSetup.Plan`
  (and `LogPlace`) was split out of `Apply` so the `off` and fallback cases are tested without
  configuring the process.
- **Two more silent catches logged** than the table listed: `LensCatalog`'s parse of an Adobe file
  and `LensfunReader`'s of an XML file, both Warn. `PreviewReader`'s six catches became one, with
  the types in a comment.
- **The analyzer's Romanian corner keys** (`stanga-sus` ...) are written in English in the log.
