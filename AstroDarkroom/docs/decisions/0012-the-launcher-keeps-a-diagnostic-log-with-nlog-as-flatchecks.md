# 0012. The launcher keeps a diagnostic log with NLog, as FlatCheck's

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The launcher recorded nothing: a report that it did not open a tool, or that it crashed, came
with nothing to read. On 2026-10-30 the owner asked for a log with NLog: text files in `log/`
beside the executable, their names starting with the executable's name, at most 5 rolling files
of at most 1 MB each, done as FlatCheck does it, so that a bug can be diagnosed from the log; the
plan (`docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`) was approved the same day.
FlatCheck built that log on 2026-10-27 (its ADRs 0070 and 0072), measuring what decided its
design, and LightsCheck took it on 2026-10-30 (its ADR 0029). Three facts are the launcher's
own: it runs from the flat suite folder, where FlatCheck's and LightsCheck's windows already write
their logs into the same `log` folder and already ship `NLog.dll` and
`NLog.Targets.AtomicFile.dll` 6.2.2, a second copy of which `scripts/merge-publish.ps1` accepts
only with equal bytes; the atomic target's package has a `net8.0` build and a `net8.0-windows`
one, two different binaries, and the launcher's window ships the second; and the window has no
test project (ADR 0001).

## Decision

- **FlatCheck's log, its decisions taken as they are** (its ADRs 0070 and 0072, as LightsCheck's
  ADR 0029 restates them): NLog 6.2.2 and `NLog.Targets.AtomicFile` 6.2.2, referenced by the core
  alone; configured in code, by `LogSetup.Apply` in `App`'s constructor and `LogSetup.Close` in
  `App.OnExit`, both guarded so the log never stops the launcher, and no `NLog.config`; the files
  `<executable>.log` in `log` beside the executable, rolled forward by NLog past 1 MiB to
  `_01`, `_02`..., 5 files kept, the next start going on in the newest, UTF-8 without BOM,
  through the atomic target kept open; one line per event with the time, the level, the process
  and thread ids, the logger, the message and the exception's `ToString()` below it; English
  words and invariant numbers, never through `T`; the unhandled exceptions at Fatal, the same one
  once, the behaviour unchanged; the loggers of `LogSetup` and `App` fetched on use. FlatCheck's
  `RunClock` is not taken: the launcher runs nothing, a start is one line and its outcome another.
- **The launcher's values.** `AstroDarkroom.log`, after the window's `AssemblyName`;
  `ASTRODARKROOM_LOG` for the level, Debug by default; `%LOCALAPPDATA%\AstroDarkroom\log` for the
  fallback, beside the language setting; the loggers `Process`, `App` and `Window`, and in the
  core `ReleaseManifest` and `LanguageSetting`.
- **Where the lines go.** At the seams: the reading of `release.json`, every version or why there
  is none, and of the language setting, and the setting's write, whose failure was silent; the
  start's language and folders; in the window, at every build of the rows the suite folder's apps
  present and missing, the window's size, place, screens and DPI once shown, each language
  switch, each start with its file, arguments and folder and its outcome (the process's id, or the
  exception at Error beside the message box), About opened and closed, the window closing; the
  title bar's failed DWM call, as LightsCheck's copy logs it. The words of the window's lines
  about the suite are composed in the core (`LogText.Presence`, `LogText.Open`), so the tests pin
  them though the window has none. The other core types do not log: they are pure, and the
  window writes what they answered.
- **The versions are the components'.** A package the launcher shares with a component resolves
  to the component's version, so the suite folder keeps one copy of each DLL: `SuiteTests` reads
  the launcher's lock files beside the components'.
- **The tests load the build the window ships.** The test project targets `net8.0-windows`, the
  window's framework, so the two-writer test of R-014 runs on the `net8.0-windows` build of the
  atomic target, and `R014_TheTestsLoadTheWindowsBuildOfTheAtomicTarget` checks that it is that
  build; the core stays `net8.0`.

## Alternatives rejected

- **`Microsoft.Extensions.Logging` with a provider, an `NLog.config` beside the executable,
  NLog's plain file target.** Rejected by FlatCheck's ADR 0070 for reasons that hold here: two
  packages for one file sink when the owner asked for NLog by name; a file to ship that a user can
  break; the plain target lost 33 to 48 lines of 4000 between two processes where the atomic one
  lost none. Not measured again; the tests that pin the measured behaviour are taken.
- **A log under `%LOCALAPPDATA%` only.** The owner asked for `log/` beside the executable, and in
  the suite folder one `log` folder then holds the launcher's log and the tools', which a bug
  report attaches whole.
- **A logging library shared by the suite's projects.** Each project builds and releases from its
  own folder, and the kit shares scripts, not code; `LogSetup` is taken as LightsCheck took
  FlatCheck's, under the launcher's names, so a fix found in one carries to the others by hand.
- **The tests on the `net8.0` build of the atomic target.** It is not the binary the launcher
  ships; LightsCheck tests its window's build in its window's test project, which the launcher
  does not have.
- **The window composing its lines about the suite by itself.** No test would reach their words.

## Consequences

- The launcher's first two packages; their version moves with FlatCheck's and LightsCheck's, and
  `SuiteTests` fails when it does not.
- The log is the first thing to ask a reporter for: the README's "Logs" section says where it is,
  what it holds and how to change the level.
- Every test runs every log line on its path: the test assembly configures NLog once, at Trace,
  into a target that renders each event (`LogCapture`). Guarded by the `R011_` to `R014_` tests,
  `OnlyTheWindowsEntryPointConfiguresTheLog` and `NoLogLineGoesThroughTheTranslation`.
- The window's own calls (`App`'s constructor, `OnStartup` and `OnExit`, the handlers, the
  hooks' wiring) run in no test; they are checked by starting the launcher from a published suite
  folder and reading its log.
- The mechanics are in `docs/design/logging.md`.
