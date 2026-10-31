# The diagnostic log

**Covers:** `src/AstroDarkroom.Core/Logging/LogSetup.cs`,
`src/AstroDarkroom.Core/Logging/LogText.cs`, `src/AstroDarkroom.Wpf/Loggers.cs`,
`tests/AstroDarkroom.Tests/LogCapture.cs`, `tests/AstroDarkroom.Tests/LogFiles.cs`,
`tests/AstroDarkroom.Tests/LoggingTests.cs`, and the log lines of the seams the table below
lists.

The behaviour is R-011 to R-014 and the decisions are ADR 0012, which takes FlatCheck's (its
ADRs 0070 and 0072) as LightsCheck took them (its ADR 0029); this note is the mechanics.
FlatCheck's and LightsCheck's `docs/design/logging.md` describe the same parts under the same
names, and what FlatCheck measured about NLog (the rolling, the targets, the stray
`NLog.config`) holds here: the same packages, the same settings.

## How the parts fit

`LogSetup.Apply` is the first call of `App`'s constructor, in a `try` that swallows anything it
throws, because the log never stops the launcher; `LogSetup.Close` is the last call of
`App.OnExit`, guarded the same way. `Apply` runs once per process:

1. `Plan` decides the place without configuring anything: the level from `ASTRODARKROOM_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name,
   `AstroDarkroom`, or the entry name when the path is the `dotnet` host), and, unless the level
   is `Off`, the folder (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable, a
   probe file in it, `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, and opens for
   append the file NLog will write next when there is one (`NewestLog`: the highest-numbered
   `<name>_NN.log`, else `<name>.log`), with the share mode that lets another launcher hold it
   open; when any of these fails it does the same under `%LOCALAPPDATA%\AstroDarkroom\log`.
2. `Build` makes the configuration, one `AtomicFileTarget` and one rule from the level up, and
   `Apply` sets it before any logger exists: `LogSetup`'s own logger and `App`'s are properties
   fetched on use, not static fields, because those two types run before the configuration is
   set, and a logger created by their type initialiser could make NLog load an `NLog.config` it
   finds beside the executable. `Off` gives a configuration without targets, and `Apply` returns
   there.
3. The hooks: `AppDomain.UnhandledException` (Fatal, flushed) and
   `TaskScheduler.UnobservedTaskException` (Error); `App`'s constructor adds the dispatcher's
   `DispatcherUnhandledException`, which calls `LogSetup.Fatal` and leaves the exception
   unhandled, so the window closes on it as before. `Fatal` names the same exception seen a
   second time (the dispatcher's, then the process's) instead of writing it again.
4. The first line, `StartupLine`, at Info.

The other loggers are `static readonly` fields of the types that log (`ReleaseManifest`,
`LanguageSetting`, `MainWindow`, `AboutWindow`, `DarkTitleBar`, `ScreenArea`); none of those
types runs before `Apply`, the first being `LanguageSetting` in `OnStartup`.

## The files, measured

NLog 6.2.2 rolls forward without moving a file: `AstroDarkroom.log`, then past `FileSizeLimit`
`AstroDarkroom_01.log`, `_02`, and so on (`ArchiveSuffixFormat`'s default `_{0:00}`); a file
ends one line past the limit. `R011_TheLogRollsAt1MiBAndKeepsAtMost5Files` writes 8 MiB of 4 KB
lines through the launcher's configuration; on 2026-10-30 it left `AstroDarkroom_03.log` to
`AstroDarkroom_06.log` at 1049544 bytes each and `AstroDarkroom_07.log` at 980453 bytes, with the
last line, and the next writer appended to `_07`. The byte counts move by a few hundred from one
run to the next: every line carries the process and thread ids.

In the suite folder the `log` folder is shared: the tools' windows that keep a log (FlatCheck's
and LightsCheck's among them) write their own files there, under their own names, and NLog's
rolling matches its own name and number only
(`R011_OneExecutablesRollingLeavesTheOthersFilesAlone`). A program that falls back writes under
`%LOCALAPPDATA%`, in a folder of its own name.

Two factories on one file stand for two launchers, each on a thread of its own released by a
`Barrier` so they overlap: `R014_TwoProcessesWritingAtFullSpeedKeepEveryLine` writes 4000 lines
from each, crossing the 1 MiB roll, and asks for every line, whole, once, the writers' lines
interleaved both ways round; on 2026-10-30 it left `AstroDarkroom.log` (1048930 bytes) and
`AstroDarkroom_01.log` (646938 bytes) with 4000 whole lines of each writer and none broken. The
test project targets `net8.0-windows` so that it loads the atomic target's `net8.0-windows`
build, the binary the window ships; `R014_TheTestsLoadTheWindowsBuildOfTheAtomicTarget` reads the
assembly's `TargetPlatform` attribute, `Windows7.0`.

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the
level padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant
numbers, `none` for a missing value, `yes` or `no` for a flag, an elapsed time as `12.9 s`) and
logged as plain strings, so NLog never parses them as message templates and a `{` in a path is
harmless; the window's geometry line, the one that costs something to build, is guarded by
`IsDebugEnabled`. The window's lines about the suite take their words from `LogText.Presence`
and `LogText.Open`, which the tests call in both languages; a line about the window itself
(its geometry, a switch, About) is composed where it is written.

Every logger is a `Logger` named `Log`, taken from `LogManager.GetLogger` with `using NLog`
(`NoLogLineGoesThroughTheTranslation` checks it, so its scan of the calls sees them all). The
names: `Process` (`LogSetup`: the first and last lines, the crashes), `App` and `Window` (the
window's two, from `Loggers`), `ReleaseManifest` and `LanguageSetting` (the core's readers).

## What is logged where

| Seam | Logger | Level | What |
| --- | --- | --- | --- |
| `LogSetup` | `Process` | Info, Fatal, Error | the first line; `exit <code> after <s>`; an unhandled exception, the window thread's included, once; a task nobody awaited |
| `App.OnStartup` | `App` | Info | `language=<code> (the setting)` or `(the start-up default)`, then `settings=<path>; suite folder=<dir>` |
| `LanguageSetting` | `LanguageSetting` | Debug, Warn | the file missing, read (`language=<code>`) or written, at Debug; a file that is no JSON object or holds no known code, an unreadable file and a failed write, at Warn, the last two with the exception |
| `ReleaseManifest.TryRead` | `ReleaseManifest` | Info, Warn | `read <path>: name= version= date= commit=; N components: <name> version= commit= unreleased= clean=; ...` (`LogText.Release`), or `no file at <path>`, at Info; a file that is not a manifest, and an unreadable one with the exception, at Warn |
| `MainWindow.Relabel` | `Window` | Info | `suite folder <dir>: N of 6 apps present; missing <exes>` (`LogText.Presence`), at the start and at every language switch |
| `MainWindow` | `Window` | Info, Debug, Error | `window shown <w>x<h> at (<x>,<y>); primary work area ...; desktop ...; dpi ...` once, at Debug (the work area is the primary screen's whichever screen the window is on, in the primary screen's DIPs; with screens of different DPIs they are not the window's); `language switched to <code>`; `open <title>: <file> <arguments> in <dir>` (`LogText.Open`), then `started <exe>, process <id>`, or the start's exception at Error beside its message box; `About opened`, `About closed`; `window closing` |
| `AboutWindow` | `Window` | Debug | `About shown <w>x<h> at (<x>,<y>); work area ...; cards scroll: yes` or `no`, once laid out and moved inside its screen (R-016) |
| `DarkTitleBar` | `Window` | Debug | a window manager that cannot be called, with the exception, as LightsCheck's copy writes it |
| `ScreenArea` | `Window` | Debug | a screen's work area Windows did not give, with the exception when the call itself could not be made; the primary screen's is used |

Not logged: the catalogue, the plan of a start before the `open` line, each row's lookup (the
suite folder's line sums them up), the interface's texts, and the exceptions of the reads the log
makes for its own lines, whose outcome the line gives: the folder's probe (`CanWrite`, before the
log exists; the first line's `fallback` note names both of its causes), the working folder
(`cwd=?`) and a started process's id (`IdOf`, `process unknown`).

## Reading a log

A start begins at its `Process AstroDarkroom <version> starting` line and ends at `Process exit
<code> after <s>`. A crash writes a `FATAL` line with the exception first; the `exit` line
follows it only when `OnExit` still runs, so a crash is read from its `FATAL` line. Two launchers
writing at once interleave, and the `[pid:thread]` field separates them. A tool the launcher
started writes its own file in the same folder: the launcher's `started` line has its process id,
and the tool's first line has the same id and the `--lang=` argument the `open` line names.

## In the tests

`LogCapture` configures NLog once for the test assembly, through a module initializer, with one
target at Trace that renders every event: every test thus runs every log line on its path, and a
line that throws fails the test that reaches it. A test that reads the lines opens a capture with
`LogCapture.Start`, an `AsyncLocal` scope like `Ui.Use`, which sees only its own call tree. The
file tests never touch that configuration: they build the launcher's with `LogSetup.Build` into a
private `LogFactory` (`LogFiles.Factory`), which stands for one process. `Apply` and `Close` run
in no test, since they configure the process's own log, and neither do `App`, the window's
handlers and the hooks' wiring (the window has no tests, ADR 0001): they are checked by starting
the launcher from a published suite folder and reading its log. `Fatal`'s body is tested
directly.
