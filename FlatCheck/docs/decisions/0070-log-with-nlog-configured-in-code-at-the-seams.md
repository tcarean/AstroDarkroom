# 0070. A diagnostic log with NLog, configured in code by the two applications, written at the seams

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

A bug report carried only what was on the screen: the console printed `ERROR: <message>` without
the exception's type, its stack or its inner exceptions, the window showed the messages and
dropped the stack on purpose, and nothing recorded what was loaded, which lens profiles were
found, which figures led to a verdict or how long each stage took. Several places swallowed an
exception for good reasons (the preview decoder, the profile folders, the raw beside the master,
the Siril installation lookup) and left no trace. The owner asked on 2026-10-27 for a log with
NLog, as text, in a `log` folder beside the executable, the file names starting with the
executable's name, at most 5 files per executable of at most 1 MB each, rolling, and left what to
log to the implementer, with one criterion: a bug report with the log attached can be diagnosed
from it. The plan (`docs/plans/2026-10-27-add-a-diagnostic-log-with-nlog.md`) put eight choices to
the owner, each with a recommendation; the owner approved it as written.

## Decision

- **Where NLog lives.** The core references NLog 6.2.2 and logs at its seams: the readers, the
  profile locator and the lens identification, every run in `Runs/`. `Analysis/`, `Report/`,
  `Metrics.cs`, `Stats.cs`, `Img.cs` and `Thresholds.cs` never log; a run logs the figures its
  analyzer returned. The two applications configure NLog in code, through
  `LogSetup.Apply` first and `LogSetup.Close` last; there is no `NLog.config`. Without a
  configuration, which is the case of the tests and of any library use, every call writes nothing.
- **The files.** `<executable>.log` in `log` beside the executable, `%LOCALAPPDATA%\FlatCheck\log`
  when that folder cannot be written. NLog 6's own rolling: past 1 MiB the next file is
  `<executable>_01.log`, then `_02` and so on, and the oldest is deleted when there are more than
  5. NLog 6.2.2's `MaxArchiveFiles` counts the file being written, so it is 5 (set to 4, it left 4
  files). The next process goes on in the newest file. UTF-8 without BOM.
- **Two processes.** The file is closed between lines (`KeepFileOpen = false`, the plan's
  choice), measured against the two alternatives:

  | target | two windows on one session | two processes, 2000 lines each at full speed | one process, per line |
  | --- | --- | --- | --- |
  | file closed between lines | 28 and 28 lines kept | 33, 33 and 42 lines lost of 4000 | 4.8 ms |
  | file kept open (NLog's default) | 0 and 28 lines kept: one window's whole log lost | 67, 70 and 72 lost | 0.07 ms |
  | `NLog.Targets.AtomicFile` (a second package) | not run | 0 lost, three times | 0.09 ms |

  At the Debug level a structure run writes about 10 lines and the opening of a session in the
  window about 30, so the closed file costs well under a second: a structure run on the Samyang
  master took 3.8 to 4.1 s with the log off and 4.0 to 4.4 s at Debug or Trace, three runs each.
  Whether to take
  the second package, so that no line is lost when two processes write at full speed, is the
  owner's call, asked at the end of this task and written down as R-112, a draft.
- **The level.** Debug by default; the environment variable `FLATCHECK_LOG` sets `off`, `fatal`,
  `error`, `warn`, `info`, `debug` or `trace`. No console option, so the console's contract and
  its golden files do not move. Trace adds every FITS header card and the progress lines siril-cli
  prints; every other siril-cli line is at Debug, because the Siril run is the likeliest thing to
  fail on a user's machine (the plan said Trace for both in one place and Debug in another; the
  seam table's Debug won, for that reason).
- **The words.** English, invariant numbers, a verdict by its key (`PERFECT`, `REFA`), never
  through `T(english, romanian)`; an exception's or a refusal's message stays as the user was
  shown it, so it can be in Romanian. The analyzer's Romanian corner keys are written in English.
- **The lines.** One line per event: time, level, process and thread ids, logger, message, and an
  exception as `Exception.ToString()` on the lines below. The process's first line (version,
  arguments, folders, machine, runtime, culture, time zone, process id) and last line (exit code,
  elapsed time); one line per file read; a start and a done line per run with the time of each
  stage; the figures behind each verdict at Debug; the files written; every exception, those
  recovered from included; the window's session, tab runs and user actions.
- **Crashes.** The process's unhandled exceptions and the window's dispatcher exceptions are
  logged at Fatal and flushed, the same exception written once; the behaviour on a crash does not
  change.

## Alternatives rejected

- **`Microsoft.Extensions.Logging` with a provider.** Two packages for one file sink, and the
  owner asked for NLog by name.
- **An `NLog.config` beside the executable.** One more file to ship and keep in step with the
  code, and one the user can break; the configuration is five properties.
- **Logging inside `Analysis/` and `Report/`.** It would mix input and output into the pure
  functions, make the mutation reports noisier, and the runs already hold every figure the
  analyzers return.
- **A `--log` console option.** It changes the console's contract, its usage text and the golden
  files, for what an environment variable does without touching them.
- **Keeping the file open.** 70 times faster, but the second of two windows wrote over the first's
  whole log (the table above).
- **One file per process.** No line lost, but the five files per executable would be filled by
  five runs, and a report would come in pieces.

## Consequences

- A fifth third-party component (`ThirdParty.cs`, `THIRD-PARTY-NOTICES.md` with the BSD-3-Clause
  text, the README's table, the About window by itself); `ThirdPartyTests` counts five.
- The log is the first thing to ask a reporter for: the README's "Logs" section says where it is.
- Every test runs every log line on its path: the test assemblies configure NLog once, at Trace,
  into a target that renders each event (`LogCapture`), so a log line that throws fails a test,
  not a user's run. Guarded by `R109_...` to `R111_...` in `LoggingTests` and `WindowLoggingTests`,
  and by `OnlyTheTwoEntryPointsConfigureTheLog`, `NoLogLineGoesThroughTheTranslation` and
  `NothingThatComputesOrWordsTheReportLogs`.
- The other tools of the suite can take `LogSetup` as it is, under their own names.
- Two processes writing at full speed can lose about 1 % of their lines until R-112 is decided.
