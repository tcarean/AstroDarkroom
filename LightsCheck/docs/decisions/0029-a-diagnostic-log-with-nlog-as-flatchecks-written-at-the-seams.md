# 0029. A diagnostic log with NLog, as FlatCheck's, written at the seams

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

A bug report carried only what was on the screen, and the screen is thin on purpose: the console
prints `ERROR: <message>` without the exception's type, stack or inner exceptions, the window puts
the same message on its problem card or under a frame's cut-outs, and a frame that could not be
measured keeps only its message in its row. Nothing recorded which files were read and what their
headers said, which figures led to a verdict, how long each stage took, or what the window did
before it failed; several places swallowed an exception for good reasons and left no trace (the
read noise, a plate-solve candidate, a raw's lens block, the stored arrows, the dark title bar,
the About window's link). The owner asked on 2026-10-30 for a log with NLog, as text, in `log/`
beside the executable, the file names starting with the executable's name, at most 5 rolling files
of at most 1 MB each, done as FlatCheck does it, with one criterion: a bug report with the log
attached can be diagnosed from it. FlatCheck built that log on 2026-10-27 (its ADR 0070) and fixed
it after a review of 42 points (its ADR 0072), measuring on the way what decided the design. The
plan (`docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`) took FlatCheck's decisions as they
are and put the nine choices LightsCheck makes on its own to the owner, each with a
recommendation; the owner approved it as written on 2026-10-30.

## Decision

- **FlatCheck's log, its decisions taken as they are.** NLog 6.2.2 and `NLog.Targets.AtomicFile`
  6.2.2, referenced by the core alone. The two applications configure it in code, through
  `LogSetup.Apply` first and `LogSetup.Close` last; there is no `NLog.config`, and without a
  configuration (a library use) every call writes nothing. The files: `<executable>.log` in `log`
  beside the executable, rolled forward by NLog past 1 MiB to `<executable>_01.log`, `_02`, ...,
  5 files kept (NLog 6.2.2's `MaxArchiveFiles` counts the file being written, so it is 5), the
  next process going on in the newest file, UTF-8 without BOM; the atomic target kept open, so two
  windows on one file keep every line. The line: the time, the level, the process and thread
  ids, the logger, the message, and the exception's `ToString()` below it. The level: Debug, or
  what an environment variable names. The words: English, invariant numbers, an enum by its name;
  an exception's or a refusal's message as the user saw it. Crashes: logged at Fatal, the same
  exception once, the behaviour unchanged. The log never stops the tool: `Apply` and `Close` are
  guarded, and the loggers of the types that run before the configuration exists are fetched on
  use.
- **`Logging/` is LightsCheck's own code**: FlatCheck's `LogSetup`, `LogText` and `RunClock`
  adapted in LightsCheck's style (nullable enabled, braces, file-scoped namespaces), with their
  names and their behaviour, not a copy under the rule "Copied files stay copies", which keeps to
  `Imaging/`. `LogText` writes LightsCheck's results.
- **The names.** `LIGHTSCHECK_LOG` for the level; `%LOCALAPPDATA%\LightsCheck\log` for the
  fallback, beside the window's `settings.json`; the core's loggers by class, `Process` for the
  first and last lines, `Cli` for the console, and two for the window, `App` and `Window`: it has
  one view-model, whose tabs are views, so a logger per tab would name nothing.
- **The FITS reader stays a copy.** Of `Imaging/`, the two adapted files log, `ImageLoader.cs`
  (one line per file read, the FITS keywords that decide the reading at Debug, every card at
  Trace, from what the reader returns) and `RawReader.cs` (libraw's figures at Debug, the lens
  block's failure at Warn); `FitsReader.cs` does not. FlatCheck's reader logs from inside since its
  ADR 0070; that hunk is not taken.
- **Where the lines go.** At the seams: the readers, `SessionFolder`, `FrameInfo`, the three runs,
  the console, the window. Never in `Analysis/`, `Report/`, `Thresholds.cs` or the copied readers:
  a run logs what its analyzers returned. The series writes one Info line per frame measured, from
  the frame's worker thread, with its time; each frame's measurement, registration, class and
  flags at Debug; the cadence, the dithering, the sensor's pattern, the pixel size and its source,
  the read noise, the WCS and the series analysis at Debug; no line per progress tick, the frame
  lines being the progress. Each run's start and done lines carry the time of its stages. A value
  the report words in the run's language (an assumed scale's source, the RA axis's source, an
  unreadable frame's error in its flag) is left out of the log's own words.
- **One mode.** `Options.Mode`, decided once by the dispatch's precedence (help, then an error,
  then `--frame`, then `--inventory`, else the series), is what the console dispatches on and what
  its `mode=` line writes, as FlatCheck decided in its ADR 0072.
- **What the window adds.** A session run's start and outcome: done, stopped (at Debug when a
  newer run replaced it), refused with the exception while the card keeps the message. An
  exception the window does not handle is written at Error, with its stack, by an exception filter
  that returns false, before it leaves `OpenAsync` in a task nobody awaits; the behaviour does not
  change.
- **Measured here: the tests and the cost.** FlatCheck's measurements of the file targets are not
  repeated; the tests that pin them are (the rolling and its file names, the two writers across
  the roll, the read-only newest file, the file another process holds open). The cost, measured
  on 2026-10-30 on the Veil session (87 NEF lights, 30 biases, 4 threads, on a hard disk), the
  Release console, three series runs each, alternated, while the owner used the PC:

  | run | log | wall time | log written |
  | --- | --- | --- | --- |
  | 1 | off | 154.1 s | nothing, no folder |
  | 2 | Debug | 173.5 s | 172720 bytes, 540 lines |
  | 3 | off | 133.0 s | nothing |
  | 4 | Debug | 163.7 s | 173309 bytes |
  | 5 | off | 111.4 s | nothing |
  | 6 | Debug | 96.7 s | 173257 bytes |

  The spread of one setting (111 to 154 s off, 97 to 174 s at Debug; the runs grew faster as the
  disk cache filled) is wider than any difference between the two, so the wall time cannot show
  the log's cost. A line through the same configuration costs 0.03 to 0.08 ms, on the session's
  hard disk as on the SSD, one writer or four (600 and 2400 lines, the same day): a run's 540 lines
  cost under 0.1 s, under 0.1 % of the fastest run. The log costs nothing measurable, as FlatCheck
  found, and its 5 MiB hold the Debug log of about thirty such runs.

## Alternatives rejected

- **`Microsoft.Extensions.Logging` with a provider, an `NLog.config` beside the executable,
  logging inside `Analysis/` and `Report/`, a `--log` console option.** Rejected by FlatCheck's
  ADR 0070 for reasons that hold here: two packages for one file sink when the owner asked for
  NLog by name; a file to ship and keep in step that a user can break; input and output mixed into
  the pure functions, and noisier mutation reports; the console's contract and its goldens moved
  for what an environment variable does.
- **NLog's plain file target.** FlatCheck measured it losing lines between two processes, closed
  between lines (33 to 48 of 4000, at 4.8 ms a line) or kept open (the second window's lines over
  the first's); not measured again.
- **Taking FlatCheck's logging hunk of `FitsReader.cs`.** It would change the copy's stated
  contents (FlatCheck 2.3.0 with the edits ADRs 0003, 0018, 0021, 0025 and 0027 list) and need
  `LogText` inside the copy, for no line the loader cannot write from what the reader returns.
- **A logger per tab.** The tabs are views over one view-model; the names would tell nothing
  apart.
- **A line per progress tick.** The frame lines are the progress, with the figures.
- **A line where libraw fails to load (`RawReader.Missing`).** Every caller logs the exception it
  throws, the libraw failure inside it as the inner exception; the stack would be written twice.

## Consequences

- A third third-party component with two packages of the same project, as LibRaw has
  (`ThirdParty.cs`, `THIRD-PARTY-NOTICES.md` with the BSD-3-Clause text, the About window by
  itself); `ThirdPartyTests` counts three. The two versions move together, as in FlatCheck.
- The log is the first thing to ask a reporter for: the README's "Logs" section says where it is
  and what it holds.
- Every test runs every log line on its path: the test assemblies configure NLog once, at Trace,
  into a target that renders each event (`LogCapture`, linked into the window's tests), so a log
  line that throws fails a test, not a user's run. Guarded by the `R070_` to `R073_` tests of
  `LoggingTests` and `WindowLoggingTests`, and by `OnlyTheTwoEntryPointsConfigureTheLog`,
  `NoLogLineGoesThroughTheTranslation` and `NothingThatComputesOrWordsTheReportLogs`.
- The mechanics are in `docs/design/logging.md`. DarksCheck, CopyWcs and the launcher can take
  `Logging/` the same way, under their own names.
