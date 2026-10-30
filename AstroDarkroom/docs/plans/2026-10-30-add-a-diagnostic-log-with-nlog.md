# 2026-10-30 Add a diagnostic log with NLog

## Goal

The launcher (`AstroDarkroom.exe`) records nothing: no log, no crash hook. When someone who
received the suite zip reports "it does not open FlatCheck" or "it crashed", there is nothing to
read. The owner asked on 2026-10-30 for a log with NLog: text files in a `log/` folder beside the
executable, names starting with the executable's name, at most 5 rolling files of at most 1 MB
each, done the way FlatCheck does it, so that a bug can be diagnosed from the log alone. The plan
was approved in plan mode the same day.

FlatCheck already has this log (its ADRs 0070 and 0072, R-109 to R-112, its
`docs/design/logging.md`), and its ADR 0070 says the other tools of the suite can take `LogSetup`
as it is, under their own names. This task does that for the launcher: FlatCheck's mechanism and
measured values, the launcher's own seams.

Two facts about the suite folder shape it:

- The suite folder is flat, and `FlatCheck.Wpf.exe` already writes `log\FlatCheck.Wpf.log` there
  (seen in `artifacts/1.0.0/log/`). The launcher's `log\AstroDarkroom.log` goes in the same
  folder, so one `log` folder attached to a bug report holds both; the test "one executable's
  rolling leaves the other's files alone" covers it.
- `FlatCheck.Wpf` already publishes `NLog.dll` and `NLog.Targets.AtomicFile.dll` 6.2.2 into the
  suite folder, and `scripts/merge-publish.ps1` accepts a second copy only with equal bytes, so
  the launcher takes FlatCheck's versions. `SuiteTests` compares only the six components' lock
  files today; it is extended to the launcher's own.

The two packages are the ones FlatCheck took; approving the plan is the owner's yes to both (the
kit's rule "Packages"). The atomic target is kept over NLog's plain one because FlatCheck measured
the plain one losing lines between two processes (its ADR 0070), and two launchers started at
once (a double click) is real. No version bump: 1.0.0 is set and not yet cut. Out of scope: the
other components' own logs, which are their own tasks.

The values:

| What | Value |
| --- | --- |
| Executable name | `AstroDarkroom` (the window's `AssemblyName`): `log\AstroDarkroom.log`, then `AstroDarkroom_01.log`, `_02`... (NLog 6's default `ArchiveSuffixFormat`) |
| Folder | `log` beside the executable (`AppContext.BaseDirectory`); `%LOCALAPPDATA%\AstroDarkroom\log` when it cannot be created or written, and the first line says so |
| Rolling | `ArchiveAboveSize` 1 MiB (a file ends one line past it), `MaxArchiveFiles` 5 (NLog 6.2.2 counts the file being written), the oldest deleted first, the next process appends to the newest |
| Target | `AtomicFileTarget`, `KeepFileOpen`, `ConcurrentWrites`, `AutoFlush`, `CreateDirs`, UTF-8 without BOM |
| Level | Debug by default; `ASTRODARKROOM_LOG` = `off`, `fatal`, `error`, `warn`, `info`, `debug` or `trace`, in any case, trimmed; anything else is Debug; `off` creates neither folder nor file |
| Line | FlatCheck's `LineLayout`: time, level, `[pid:thread]`, logger, message, and the exception's `ToString()` below; invariant culture |
| Words | English, invariant numbers, never through `T(en, ro)`; an exception's or a refusal's message as the user saw it |
| Loggers | `Process` (LogSetup), `App`, `Window`, `ReleaseManifest`, `LanguageSetting`; each a `Logger` named `Log` from `LogManager.GetLogger`, `static readonly` except in `LogSetup` and `App`, where it is a property fetched on use (a stray `NLog.config` beside the exe cannot kill the type initialiser, FlatCheck's ADR 0072) |
| Crash hooks | `AppDomain.UnhandledException` (Fatal, flushed) and `TaskScheduler.UnobservedTaskException` (Error) in `LogSetup.Apply`; `DispatcherUnhandledException` in `App`'s constructor through `LogSetup.Fatal`; the same exception seen twice is written once; the crash behaviour does not change |
| The log never stops the launcher | `Apply` and `Close` in a swallowing `try` in `App`; `LogManager.ThrowExceptions = false` |
| Where the code lives | `src/AstroDarkroom.Core/Logging/`; the core references the packages, the window gets them through it |
| Requirements | R-011 the files, R-012 the content, R-013 the level, R-014 two processes keep every line |
| ADR | 0012 (check `main`'s latest number again before the merge) |

## Steps

Branch `claude/Log-AstroDarkroom-with-NLog`, worktree
`.claude/worktrees/Log-AstroDarkroom-with-NLog`. Requirements first, then tests, then code.

1. **Requirements.** R-011 to R-014 in `docs/requirements.md`, modelled on FlatCheck's R-109 to
   R-112 with the launcher's values and one example each: R-011 the files (the folder, the
   fallback, the names, the roll, five kept, another executable's files left alone); R-012 the
   content (the first line with the executable, version, arguments, folders, level, OS, CPUs,
   memory, runtime, culture, time zone and process id; the setting file read; `release.json`
   read with the suite's and every component's version, or why not; at every build of the rows,
   how many apps are present and which are missing; the window's size, screen and DPI; every
   language switch and setting written; every start asked and its outcome, a process id or the
   exception; About opened; the window closing; every exception, those recovered from included;
   the last line with the exit code and the elapsed time); R-013 the level; R-014 two writers
   keep every line through the atomic target.
2. **Packages.** `NLog` and `NLog.Targets.AtomicFile` at FlatCheck's version (6.2.2) in
   `Directory.Packages.props`, each with its "why"; both referenced by the core; the three lock
   files restored. `SuiteTests`' package test also reads the launcher's lock files, so a package
   the launcher shares with a component resolves to one version (failed first by pinning NLog
   to another version).
3. **Core.** `Logging/LogSetup.cs`, FlatCheck's member for member: public `Apply`, `Close`,
   `Fatal`; internal `Plan`, `Build`, `ResolveFolder` (the probe file and the append probe of the
   newest file), `NewestLog`, `LevelFrom`, `ExecutableName`, `StartupLine` and the constants;
   `Logging/LogText.cs` with the few formatters the launcher uses. Log lines in
   `ReleaseManifest.TryRead` (Debug when absent, Info with the versions, Warn for a refused
   shape, Warn with the exception in each catch) and in `LanguageSetting` (read, written, and
   each failure with its exception, the write's failure no longer silent). The other core types
   do not log.
4. **The window.** `App`'s constructor calls `Apply` and hooks the dispatcher; `OnStartup` logs
   the language and the folders; `OnExit` calls `Close`. `MainWindow` logs the rows' summary,
   the window's size and DPI once shown, each language switch, each start asked with its
   outcome (process id, or Error with the exception, the message boxes unchanged), About opened
   and closed, and the window closing. `DarkTitleBar` logs which attribute took, or why none.
5. **Tests.** FlatCheck's `LogCapture` (a module initializer, a capture at Trace) and its file
   tests ported as `R011_` to `R014_` (the folder, the fallback, the roll to `_03`..`_07`, the
   other executable's files, the read-only newest file, a file another process holds open, the
   names, the first line, the configuration, the crash written once, the readers' lines, the
   same messages in both languages, the level, `off`, two writers), plus the invariants: only
   `App.xaml.cs` calls `Apply`/`Close`, only `LogSetup.cs` and `LogCapture.cs` set the
   configuration, no log call goes through `T(`, every logger declared one way.
6. **Docs.** ADR 0012; `docs/design/logging.md` with its Covers sentence and an index row;
   `launcher.md` updated; README "Logs" and the third-party list; CHANGELOG under
   `[Unreleased]`; `CLAUDE.md` (layout, the log's language, read before touching, the command
   for a trace run); requirements to `implemented`.
7. **Wrap-up.** `/wrap-up`; a clean `--no-incremental -warnaserror` build before the final
   verify; small one-line commits; `check-design.ps1 -Since main`; a subagent review of the
   branch offered before the merge proposal; `main`'s latest ADR number checked again.

## Verification

1. `scripts/verify.ps1` green in the worktree.
2. Teeth: the roll test fails with `MaxArchiveFiles` at 4, and the two-writer test is run once
   against NLog's plain `FileTarget`; the `SuiteTests` extension fails with NLog pinned to
   another version. Each restored after.
3. End to end: `scripts/release.ps1 -PublishOnly` from the worktree, which also proves
   `merge-publish.ps1` takes the launcher's `NLog.dll` beside FlatCheck's. Then, only when the
   owner is not using the PC, start `AstroDarkroom.exe` there, switch the language, open
   FlatCheck from it, open and close About, close the launcher, and read
   `log\AstroDarkroom.log`: the first line, the setting, `release.json` with its versions, the
   rows, the window's size, the switch, the start with FlatCheck's process id, About, the
   closing, `exit 0`; `FlatCheck.Wpf.log` beside it with the `--lang=` argument.
4. From the build folder, every app missing: the rows' line lists the six missing exes; with
   `ASTRODARKROOM_LOG=off` no `log` folder appears; with a file named `log` in its place the log
   lands in `%LOCALAPPDATA%\AstroDarkroom\log` and the first line says `(fallback`.

## What deviated

(filled at wrap-up)
