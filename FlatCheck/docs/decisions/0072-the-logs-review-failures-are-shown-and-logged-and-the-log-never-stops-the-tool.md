# 0072. After the log's review: failures are shown and logged, refusals say why, and the log never stops the tool

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

A code review of the diagnostic log (ADR 0070) on 2026-10-27 listed 42 findings; every one
was verified on the code before anything was planned, and none was wrong
(`docs/plans/2026-10-27-fix-the-diagnostic-log-review-findings.md`). Thirteen of them had more
than one reasonable fix and went to the owner with the facts, two options each and a
recommendation; the owner chose every recommendation on 2026-10-27. ADR 0071 records one of
them apart, the lens without a vignetting profile. This one records the others that someone
could later ask "why?" about.

## Decision

- **An opening that fails past the check shows a card and logs the exception.** The window
  fires `OpenAsync` without awaiting it, so an exception from listing `flats/` or `process/`
  after `SirilSession.Check()` reached no handler: the tabs stayed empty with no word. It is
  caught in `OpenAsync`, logged at Error with its stack and shown as a card naming the folder
  and the message (R-080, R-110).
- **A refusal a tab decides is logged with its reason, and its run says `refused`.** Each tab
  logs under its own logger, `Window.<Tab>`, so the logger column says which tab and no message
  carries a hand-written prefix; every logger is declared one way, a `Logger` field taken from
  `LogManager.GetLogger`, so the invariant test that keeps `T(...)` out of the log sees every
  call.
- **A run that throws leaves its stages.** `RunClock` is disposable and held in a `using`: a run
  that leaves without its done line writes `<run> ended without an outcome after N ms (<stages>)`
  at Info; the one Error line of a failure stays its caller's. Every run starts its clock before
  its load, so a refusal decided after the load and a file that does not load both leave the
  run's start line.
- **The log never stops the tool.** `LogSetup.Apply` and `Close` run in a `try` in both
  applications, which then run without a log. `LogSetup`'s own logger, and `App`'s, whose
  constructor calls `Apply`, are fetched on use, so the configuration is set before any logger
  exists and NLog never loads an `NLog.config` it finds beside the executable. Measured on 2026-10-27 with a scratch console beside an `NLog.config`
  naming an unknown target with `throwConfigExceptions="true"`: with the logger in a static
  field the process died in `TypeInitializationException` before `Main`'s body; fetched on use,
  it ran. The folder's probe also opens, for append, the file NLog will write next, so a
  read-only newest file sends the log to the fallback instead of losing every line.
- **One mode, decided in `Options`.** `Options.Mode` is set once by R-003's precedence; the
  dispatch and the log's `mode=` line both read it, so a new mode cannot be in one and not the
  other. The log no longer repeats the options: the process's first line has the arguments.
- **What "every exception" excepts** (R-110): a cancellation the user asked for, a race at the
  window's close and the log's own probes, whose outcome is their own line. The other quiet
  catches write a Debug line.
- **Both crash hooks stay**, the window's dispatcher hook and the process's, with the same
  exception written once, now pinned by a test.

## Alternatives rejected

- **Await `OpenAsync` in the window.** The crash hooks would see the exception, and the window
  would close over a folder permission.
- **One `Window` logger with a prefix helper.** Fewer loggers, but the prefix stays a convention
  every new line has to remember, and a grep cannot tell the tabs apart by logger.
- **Leave `RunClock` as it was.** The caller logs the exception, but without the run's stages or
  its time; a slow load followed by a failure would read as an instant failure.
- **Only the `try` around `Apply`.** It keeps the tool alive but leaves NLog loading a stray
  `NLog.config` on every start, and the comment saying `off` prevents it false.
- **Two copies of the dispatch with a test that they agree.** It catches the drift after it
  happens, where one value cannot drift at all.
- **Log every quiet catch, the shutdown races included.** Lines nobody can act on, written while
  the window closes and the log is being shut.
- **Keep only the process's hook.** It would change a bullet of ADR 0070's decision for one field
  and four lines, with no behaviour gained.

## Consequences

- `R080_...`, `R110_ARefusalATabShowsIsLoggedWithItsReason_AndTheRunSaysRefusedNotDone`,
  `R110_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached`,
  `R109_AReadOnlyNewestLogFileSendsTheLogToTheFallback`,
  `R003_TheModeIsDecidedOnceByThePrecedenceOfTheFlags`, `R110_TheSameCrashSeenByTwoHooksIsWrittenOnce`
  and the stricter `NoLogLineGoesThroughTheTranslation` pin these.
- The mechanics are in `docs/design/logging.md` and `two-applications.md`; ADR 0070 keeps its
  decision, with dated notes where its words were wider than the code.
