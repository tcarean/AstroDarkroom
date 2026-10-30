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

- **The branch** is `claude/Log-AstroDarkroom-with-NLog`, after the siblings' branches of the
  same day (`claude/Log-LightsCheck-with-NLog`, ...), not the approved plan's
  `claude/Add-a-diagnostic-log-with-nlog`: branch names are the repository's, and DarksCheck's
  plan of the same day names that one.
- **The model** is LightsCheck's `Logging/LogSetup.cs`, FlatCheck's in a style close to the
  launcher's, merged into `main` the same day (its ADR 0029), without LightsCheck's `Wrote` and
  `Unobserved` (the launcher awaits no task: FlatCheck's plain hook stays) and without
  `RunClock` (the launcher runs nothing).
- **The test project targets `net8.0-windows`** (ADR 0012), a new fact found before the code:
  the atomic target's package has a `net8.0` build and a `net8.0-windows` one, two binaries,
  and the window ships the second; `R014_TheTestsLoadTheWindowsBuildOfTheAtomicTarget` checks
  that the tests load it.
- **"Despre" lists NLog** (R-015, ADR 0013, proposed, for the owner's yes at the merge), with
  `THIRD-PARTY-NOTICES.md` holding the BSD-3-Clause text word for word as FlatCheck's has it; the
  plan named a README row only, but every tool of the suite that ships NLog lists it in its
  "Despre" (FlatCheck, LightsCheck, and ImageVisualCompare and CopyWcs on their branches). The
  window grows from about 785 DIP to about 931 (Romanian) and 946 (English), measured offscreen,
  after the card was cut down from a first version that made it about 974 and 989.
- **The window's lines about the suite** take their words from `LogText.Presence` and
  `LogText.Open` in the core, so the tests pin them; the plan had them composed in the window.
- **The levels.** A missing `release.json` is an Info line, not Debug: the window then shows the
  versions as unknown. `DarkTitleBar` logs a failed DWM call only, as LightsCheck's copy does,
  not which attribute took nor the HRESULTs the plan listed.
- **The front page**, the repository's root `README.md`, has one sentence on the log folder.
- **Verification.** The gate is green (76 tests). The teeth, each restored after: with
  `MaxArchiveFiles` at 4 the roll test and the configuration test fail (4 files left); with
  NLog's plain `FileTarget` kept open (without `ConcurrentWrites`, which it lacks in NLog 6) the
  two-writer test fails three runs out of three, one writer's whole log lost; with both of the
  launcher's lock files pinned to NLog 6.2.1, `SuiteTests` fails with `NLog: AstroDarkroom 6.2.1,
  FlatCheck 6.2.2, LightsCheck 6.2.2`; the windows build of the atomic target carries the
  `Windows7.0` attribute and the `net8.0` build does not. `scripts/release.ps1 -Version 1.0.0
  -PublishOnly` from the worktree published 367 files (162.0 MB) with no collision, FlatCheck's
  and LightsCheck's NLog DLLs "already there with the same bytes" as the launcher's.
- **The live check was cut short.** With the owner away for about 20 minutes, a script started
  `AstroDarkroom.exe` from the published suite folder and drove it through UI Automation only
  (no mouse or keyboard input); after the switch to Romanian it found input 1 s old and stopped
  itself before opening FlatCheck and "Despre", and the launcher was closed through UI
  Automation; the owner's `settings.json` was put back. Its log, beside the executable:

  ```
  INFO  [32564:1] Process AstroDarkroom 1.0.0 starting; exe=...\artifacts\1.0.0\AstroDarkroom.exe; args=[]; cwd=...\artifacts\1.0.0; log=...\artifacts\1.0.0\log; level=Debug; os=Microsoft Windows 10.0.19045 X64; cpus=8; memory=31.9 GiB; runtime=.NET 8.0.33 X64; culture=en-GB/en-GB; tz=GTB Standard Time (UTC+02:00); pid=32564
  DEBUG [32564:1] LanguageSetting setting read from C:\Users\User\AppData\Local\AstroDarkroom\settings.json: language=en
  INFO  [32564:1] App language=en (the setting); settings=C:\Users\User\AppData\Local\AstroDarkroom\settings.json; suite folder=...\artifacts\1.0.0\
  INFO  [32564:1] ReleaseManifest read ...\artifacts\1.0.0\release.json: name=AstroDarkroom version=1.0.0 date=2026-10-30 commit=8a3089d...; 7 components: AstroDarkroom version=1.0.0 commit=8a3089d... unreleased=yes clean=no; DarksCheck version=1.0.0 ...; FlatCheck version=2.3.0 ...; ...
  INFO  [32564:1] Window suite folder ...\artifacts\1.0.0\: 6 of 6 apps present
  DEBUG [32564:1] Window window shown 680x743 at (-1300,141); work area 1920x1040 at (0,0); desktop 5760x1080 at (-1920,0); dpi 96x96
  INFO  [32564:1] Window language switched to ro
  DEBUG [32564:1] LanguageSetting setting written to C:\Users\User\AppData\Local\AstroDarkroom\settings.json: language=ro
  INFO  [32564:1] Window suite folder ...\artifacts\1.0.0\: 6 of 6 apps present
  INFO  [32564:1] Window window closing
  INFO  [32564:1] Process exit 0 after 30.1 s
  ```

  Not checked live: the `open` and `started` lines of a tool opened from a row, "Despre"
  opened and closed, `ASTRODARKROOM_LOG=off`, the build folder's run (every app missing) and the
  fallback; their words and their logic are tested in the core, the calls in the window are
  not.
