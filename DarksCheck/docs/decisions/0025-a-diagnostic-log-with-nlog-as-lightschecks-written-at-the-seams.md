# 0025. A diagnostic log with NLog, as LightsCheck's, written at the seams

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

A bug report carried only what was on the screen, and the screen is thin on purpose: the console
prints `ERROR: <message>` without the exception's type, stack or inner exceptions, the window puts
the same message on its problem card, and a frame that could not be read or decoded keeps only its
message in its row. Nothing recorded which files were read and what their headers said, which
frame failed and why, how much memory the decoding held (ADR 0007's 14 GB trap was found by hand),
which figures led to a verdict, which options and partner a run had, or how long each stage took;
several places swallowed an exception for good reasons and left no trace (the settings file, the
clipboard, the dark title bar, the About window's link, a raw's lens block). The owner asked on
2026-10-30 for a log with NLog, as text, in `log/` beside the executable, the file names starting
with the executable's name, at most 5 rolling files of at most 1 MB each, done as FlatCheck does
it, with one criterion: a bug report with the log attached can be diagnosed from it. FlatCheck
built that log on 2026-10-27 (its ADR 0070) and fixed it after a review of 42 points (its ADR
0072); LightsCheck took it the same day as this task (its ADR 0029), in the style DarksCheck
shares. The plan (`docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`) put eight decisions
to the owner, each with a recommendation, and the owner approved it as written on 2026-10-30.

## Decision

- **FlatCheck's log, as LightsCheck took it.** NLog 6.2.2 and `NLog.Targets.AtomicFile` 6.2.2,
  referenced by the core alone (D1, D2). The two applications configure it in code, through
  `LogSetup.Apply` first and `LogSetup.Close` last, both guarded so a log that cannot be set up
  never stops the tool (NLog's own assembly is no such case: like the core's, it must be there);
  there is no `NLog.config`, and without a configuration (a library use) every call writes
  nothing. The files: `<executable>.log` in `log` beside the executable, rolled forward by NLog
  past 1 MiB to `<executable>_01.log`, `_02`, ..., 5 files kept (NLog 6.2.2's `MaxArchiveFiles`
  counts the file being written), the next process going on in the newest file, UTF-8 without
  BOM, through the atomic target kept open, so two windows on one file keep every line. The
  level is Debug, or what `DARKSCHECK_LOG` names (D5); the fallback is
  `%LOCALAPPDATA%\DarksCheck\log`, beside the window's `settings.json` (D6). The line, the hooks,
  the words (English, invariant numbers, an enum by its name, a message the user saw as shown)
  and the loggers fetched on use where the configuration is set are FlatCheck's, unchanged.
- **`Logging/` is DarksCheck's own code**, taken from LightsCheck's (`LogSetup`, `LogText`,
  `RunClock`, and in the tests `LogCapture` and `LogFiles`) under DarksCheck's names, not a copy
  under the rule "Copied files stay copies", which keeps to `Imaging/`.
- **The readers: LightsCheck's log hunk, as a hunk (D3).** LightsCheck's log task merged first,
  as recommended, and its two adapted readers write the readers' lines: `ImageLoader.cs` one line
  per file read, the FITS keywords that decide the reading at Debug and every card a full load
  keeps at Trace (D7), `RawReader.cs` libraw's figures at Debug and the lens block's failure at
  Warn. DarksCheck takes exactly that hunk (LightsCheck's commits 5fa43622 and 80ac6867), the
  code byte for byte and the comments DarksCheck's, as ADR 0023 took FlatCheck's lens hunk: two
  other hunks LightsCheck made to the same files are not taken, its `InvalidDataException` for a
  raw libraw refuses (for its `--frame` mode, 321ab4aa) and the fields of its pixel size from the
  35 mm-equivalent focal length (its R-066, 7fa82a12), since they serve features DarksCheck does
  not have. `FitsReader.cs` and the other copies take no line. `LogText.Frame` is LightsCheck's
  line without those fields.
- **Of `Analysis/`, the two decoding loops log (D4).** `FrameStack.Load` (the darks, the biases,
  the partner) writes one Debug line per frame decoded, with its size, scale, normalisation, time
  and the process's working set after the forced collection that follows it, the figure ADR 0007's
  trap needed; a frame left out for its size or settings at Warn, in the log's own words; a frame
  that fails at Warn with its exception, where it is caught, the only place its stack exists; the
  stack's counts and scale at Info. The lights' loop in `LightsAnalyzer.Analyze` writes one Debug
  line per light measured, with its time, the working set and its figures, and a failure the same
  way. A failed frame is one line with the decode's exception that says whether its metadata
  could then be read; the metadata read's exception follows only when it is another error, so
  each exception is written once (LightsCheck's rule). Nothing else in `Analysis/` writes, and
  nothing in `Report/`: `DarkOutputs.WriteAll` tells its caller each file as soon as it is
  written, and the console and the window write the line, as LightsCheck's `SeriesOutputs` does.
- **The figures behind every verdict, by one rule (D8).** `DarkSeriesRun` writes at Debug each
  dark's and each bias's figures, the series', the dark current's, the six verdicts', the index's
  with the label, the lights' and the partner's; the lights' loop each light's.
  `LogText.Figures` writes a record by one rule, every public property by its name, a list whole
  up to twelve items, else its first and last and its count, so a figure the analyzers gain is
  written without a change to the log. A text the report words in the run's language (the scale's
  source, a frame's error, the partner's first error) is left out of these lines; the stack's line
  says the scale's origin in the log's own words.
- **The runs.** `DarkSeriesRun` and `InventoryRun` each hold a `RunClock`: the start line with
  the folder and the options, the done line with the outcome and the time of each stage, or, when
  the run throws, the stages it reached. The darks run's done line carries the six verdicts, the
  index, the label, each group's frames decoded out of its files and whether it met an error.
- **The applications' lines are no figure of the analysis.** The console and the window compose
  their own lines and time their own runs; the rule "Computed only in the core" keeps to what the
  reports and the window show. LightsCheck's owner settled that reading on 2026-10-30 (its ADR
  0029), and `CLAUDE.md` now says it here too.
- **The console** writes `mode=help`, or the mode and every option, a refusal as the user saw
  it, each file `--out` writes, and a run that failed with its exception before its one `ERROR:`
  line; its `Options` are unchanged, the mode named where it is decided.
- **The window** numbers its runs, from 1 for each view-model, so two runs of one folder are told
  apart: a run's start with its folder and options, and its outcome, done, stopped (at Debug when
  a newer run replaced it) or failed with the exception while the card keeps the message; an
  exception the window does not handle is written at Error by an exception filter that returns
  false, before it leaves `OpenAsync` in a task nobody awaits. Its dialogs, its buttons, the
  partner, the export and the label copied are Info lines; the settings file, the clipboard, the
  title bar and a link that fail are Debug lines with their exception.
- **Measured here: the tests, the size and the cost.** FlatCheck's measurements of the file
  targets are not repeated; the tests that pin them are (the rolling and its file names, the two
  writers across the roll on both builds of the atomic target, the read-only newest file, the file
  another process holds open), with their figures in `docs/design/logging.md`. A darks run of the
  Smida session against the library (20 darks, 30 biases, 32 lights, the library's 30 darks, all
  NEF) writes 495 lines, about 198 KB, at Debug: a 1 MiB file holds five such runs and the five
  files about twenty-six. The plan estimated 30 to 45 KB a run; it counted the figures lines alone,
  and at 200 to 300 bytes where a dark's line, four planes with their residuals, is 1.3 KB, while
  the readers' three lines per file (the inventory's header read, the decode's `loaded` line,
  libraw's figures) are 55 % of the bytes. The cost, measured
  the same day on that run (the sessions on the hard disk E:), the Release console at its default
  threads (half of 8 cores), three runs each, alternated, while the owner used the PC:

  | run | log | wall time | exit | log written |
  | --- | --- | --- | --- | --- |
  | 1 | Debug | 157.7 s | 0 | 198210 bytes |
  | 2 | off | 170.5 s | 0 | nothing, no folder |
  | 3 | Debug | 151.1 s | 0 | 198189 bytes |
  | 4 | off | 153.5 s | 0 | nothing |
  | 5 | Debug | 152.8 s | 0 | 198184 bytes |
  | 6 | off | 127.4 s | 0 | nothing |

  The spread of one setting (127 to 171 s off, 151 to 158 s at Debug) is wider than any
  difference between the two, so the wall time cannot show the log's cost, as LightsCheck found
  on the Veil session; a line through this configuration costs 0.03 to 0.09 ms (FlatCheck's and
  LightsCheck's measurements, and here R-103's two writers put 8000 lines through in about
  0.3 s, reading them back included), so a run's 495 lines cost under 0.05 s. The report on
  stdout was byte for byte the same with the log on and off, in all three pairs. Two real console
  processes started together (`--inventory` on the session and on the library) wrote 130 lines
  into one `darkscheck.log`, none broken, each process's lines whole and interleaved both ways
  round. The published single-file console (`scripts/release.ps1 -Version 1.0.0 -PublishOnly`),
  run from another folder, wrote `log\darkscheck.log` beside its executable, not in the working
  folder: `AppContext.BaseDirectory` is the executable's folder for this publish. The window, the
  Release build started at the owner's word the same evening and closed with `WM_CLOSE`: one
  window in Romanian on the idx67 library wrote 168 lines, every message English, and exited 0
  after 76.6 s; two windows started together on the idx47 and idx33 libraries wrote 336 lines into
  one `DarksCheck.Wpf.log`, 168 each, none broken, interleaved both ways round (64 switches); one
  window on an empty folder showed the problem card and wrote `run 1 failed after 0.0 s; the
  problem card shows the message` at Error with the `InvalidOperationException` and its stack
  from `SessionFolder.Open` up to `MainViewModel.OpenAsync`, after the two clocks' `ended without
  an outcome` lines.

## Alternatives rejected

- **`Microsoft.Extensions.Logging` with a provider, an `NLog.config` beside the executable, a
  `--log` console option, NLog's plain file target.** Rejected by FlatCheck's ADR 0070 for
  reasons that hold here: two packages for one file sink when the owner asked for NLog by name; a
  file to ship and keep in step that a user can break; the console's contract and its goldens
  moved for what an environment variable does; a target that lost lines between two processes.
- **A whole refresh of the two readers from LightsCheck.** The rule "Copied files stay copies"
  allows it, and the plan's step 3 said it; it would have brought the two LightsCheck features
  above into DarksCheck, the first of them changing what a raw libraw refuses says in the report,
  in a task about a log. The hunk keeps the change to the log.
- **Taking FlatCheck's logging hunk of `FitsReader.cs`.** The copy would change for a line its
  loader writes itself from what the reader returns; LightsCheck rejected it the same way.
- **Records that carry each frame's exception, logged after the group.** The live lines and the
  per-frame memory would be lost, and the memory is the figure ADR 0007's trap needed (D4).
- **One hand-written line per record.** About twenty-five records and over 150 figures to keep in
  step by hand, a new figure missing from the log until someone noticed; `LogText.Figures` reads
  them.
- **`DarkOutputs` writing its own lines.** `Report/` decides words and holds no logger; the
  callback gives the console and the window the same line, as soon as each file is written.
## Consequences

- A third third-party component with two packages of the same project, as LibRaw has
  (`ThirdParty.cs`, `THIRD-PARTY-NOTICES.md` with the BSD-3-Clause text, the About window by
  itself); `ThirdPartyTests` counts three. The two versions move together.
- The log is the first thing to ask a reporter for: the README's "Logs" section says where it is
  and what it holds.
- The log is written beside the executable, wherever that is: a program placed inside a session
  or library folder writes its `log` folder there, against the rule "Read-only input". The
  README's "Logs" and the rule in `CLAUDE.md` say to keep the program outside those folders; no
  check is made, since `Apply` sets the log up before any argument is read, and a check after the
  parsing would find the folder made and the first line in it (the branch's second review, on
  2026-10-31). `--help` and R-026 keep saying that nothing is written into the folder: the
  analysis writes nothing there.
- Every test runs every log line on its path: the test assemblies configure NLog once, at Trace,
  into a target that renders each event (`LogCapture`, linked into the window's tests), so a log
  line that throws fails a test, not a user's run. Guarded by the `R100_` to `R103_` tests of
  `LoggingTests` and `WindowLoggingTests`, and by `OnlyTheTwoEntryPointsConfigureTheLog`,
  `NoLogLineGoesThroughTheTranslation` and `NothingThatComputesOrWordsTheReportLogs`, which also
  pins the two files of `Analysis/` and the two of `Imaging/` that log.
- The readers' log lines come with LightsCheck's: a later refresh of the two adapted files from
  LightsCheck keeps them, and `docs/design/imaging.md` lists the hunk and the two left out.
- `/mutate` on `FrameStack.cs` or `LightsAnalyzer.cs` will find string mutants in the log lines
  that no test kills; they are survivors to expect, not tests to write.
- The mechanics are in `docs/design/logging.md`.
