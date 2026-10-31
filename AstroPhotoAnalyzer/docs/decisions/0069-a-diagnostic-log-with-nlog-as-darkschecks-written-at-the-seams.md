# 0069. A diagnostic log with NLog, as DarksCheck's, written at the seams

Date: 2026-10-31
Status: accepted 2026-10-31

## Context

A bug report carried only what was on the screen. The console printed `ERROR: <message>` without
the exception's type, its stack or its inner exceptions; the window showed `Describe(e)`, the
messages alone, and dropped the stack on purpose. Nothing recorded which file was read and by
which reader, what its header said, which parameters and profile a run had, which figures led to
each recommendation, how long each stage took, or what the window did before it failed. Several
places swallowed an exception for good reasons and left no trace: a profile that cannot be read
became a `Problem` text (`IccPaperBlack`, `GamutAnalyzer.Build`, `ProofProfile.Open`), the
settings file that cannot be read or written gave the defaults, a link that does not open, the
title bar that stays light; and two places rewrapped a reader's exception without its stack
(`ImageSharpReader.Load`, `AnalysisRun.BorrowWcs`).

The owner asked on 2026-10-31 for a log with NLog, as text, in a folder `log` beside the
executable, the file names starting with the executable's name, at most 5 files per executable
of at most 1 MB each, rolling, made the way FlatCheck made it, with one criterion: a bug report
with the log attached can be diagnosed from it. FlatCheck built that log (its ADRs 0070 and
0072), and LightsCheck, DarksCheck, CopyWcs, ImageVisualCompare and the AstroDarkroom launcher
took it in turn, each reviewed twice. The plan (`docs/plans/2026-10-31-add-a-diagnostic-log-with-nlog.md`)
put nine choices to the owner, each with a recommendation; the owner approved it as written.

## Decision

- **DarksCheck's log, as FlatCheck built it.** The core references `NLog` 6.2.2 and
  `NLog.Targets.AtomicFile` 6.2.2, the versions every tool of the suite pins: AstroDarkroom
  publishes the windows into one folder, and the published DLLs hash equal to its bundle's and
  to DarksCheck's, FlatCheck's and LightsCheck's builds. `Logging/LogSetup.cs`, `LogText.cs` and
  `RunClock.cs` are DarksCheck's, member for member, in this core's style. The two applications
  configure NLog in code, `LogSetup.Apply` first and `LogSetup.Close` last, each guarded so the
  log never stops the tool; there is no `NLog.config`; a library use logs into nothing.
- **The files.** `<executable>.log` in `log` beside the executable, `AstroPhotoAnalyzer.log` and
  `AstroPhotoAnalyzer.Wpf.log`, `%LOCALAPPDATA%\AstroPhotoAnalyzer\log` when that folder cannot
  be written; past 1 MiB the next file, at most 5 per executable (`MaxArchiveFiles` counts the
  file being written, FlatCheck's measurement); UTF-8 without BOM; two processes into one file
  through the atomic target, which lost no line where NLog's plain target lost them either way
  (FlatCheck's measurement). The console's name begins the window's, and neither's rolling
  takes or deletes the other's files.
- **The level is `APA_LOG`** (D3): Debug by default, `off` to `trace` in any case; Trace adds
  every FITS header card. The project's own variables are `APA_*`; no console option, so the
  console's contract and its golden files do not move.
- **Where the core logs** (D4): `Runs/` (the three runs, each with a `RunClock`, and
  `CurveExport`) and the four files of `Input/` that read a file: `ImageLoader`, `FitsReader`,
  `ImageSharpReader`, `IccPaperBlack`. `Analysis/`, `Report/`, `Thresholds.cs`, `Parameters.cs`,
  `Language.cs` and the rest of `Input/` hold no logger. `GamutTable` and `ProofProfile` keep the
  exception behind their `Problem` as `Cause`, which `CurveRun` and `ProofRun` log with its
  stack; `ImageSharpReader` and `BorrowWcs` pass the reader's exception on as the inner one, the
  user's message unchanged.
- **The figures by one rule** (D5): `LogText.Figures` writes a result record by reflection, as
  DarksCheck's does, adapted to this core: its internal members, a hand-written getter that gives
  a text left out (on this core every such getter is a sentence through `Tr.Pick`), another
  assembly's object by its type's name, a list whole up to 24 items, the names given left out at
  every depth (the texts a record stores in the user's language). The eleven results of an
  analysis, the parameters, the anchors and every curve's evidence are written this way.
- **The words.** English, invariant numbers, an enum by its name, never through `Tr.Pick`; what
  the user was shown (a refusal, an exception's message, a reader's note, a recommendation's text
  and because, a profile's problem) as shown, so it can be in Romanian.
- **The applications' lines** are no figure of the analysis: the console and the window compose
  their own (the mode, the refusals, the runs numbered with their outcome, the dialogs and the
  buttons) from `LogText`, outside the core. The window's two tabs that run the core after the
  analysis log under loggers of their own, `Window.Curves` and `Window.SoftProof`, only what the
  core cannot see: a computation that failed or was superseded, a save that failed, a control
  changed.
- **Measured on 2026-10-31**, Release build, on this machine:

  | run | log on (Debug) | log off | lines and size at Debug |
  | --- | --- | --- | --- |
  | console, `de_printat.png` | 10.9, 10.2, 9.9 s | 10.0, 10.5, 10.0 s | 27 lines, 21.5 KB; the longest, the simulation's ladder, 6267 characters |
  | console, the plate-solved FITS with `--icc` | 14.2, 14.7 s | 14.2, 13.8 s | 30 lines, 24.6 KB; at Trace 129 lines, 35.1 KB, in 14.4 s |
  | window, `de_printat.png` with the remembered profile, `--lang ro` | 25.5 s from start to close | | 41 lines, 27.4 KB: the analysis, the curves and the proof |

  The log costs nothing measurable; a 1 MiB file holds about 40 console runs, the five files
  about 200. Two windows started at once on two images wrote their 42 lines each into one
  `AstroPhotoAnalyzer.Wpf.log`, every line whole, both closed with exit code 0. The two-writer
  test keeps all 8000 lines on both builds of the atomic target. The published single-file
  console writes `log\AstroPhotoAnalyzer.log` beside itself, nothing into the working folder.
- **The validation re-run** (D8): `/validate` on the branch, every reference file present: of
  the fifteen cases' 1350 figures 1348 identical to the recorded ones and the same two as since
  2026-10-23 one unit off in their fourth decimal, inside the 0.01 tolerance; the fifteen cases
  pass.

## Alternatives rejected

- **FlatCheck's rejected alternatives**: `Microsoft.Extensions.Logging` with a provider, an
  `NLog.config` beside the executable, a `--log` console option, NLog's plain file target closed
  between lines or kept open, one file per process; for the reasons its ADR 0070 gives.
- **`ASTROPHOTOANALYZER_LOG`**, the siblings' pattern (`FLATCHECK_LOG`, `DARKSCHECK_LOG`): 22
  characters to type for the same thing, where the project's own variables are `APA_*`.
- **One hand-written line per result record**, FlatCheck's way: eleven records, the parameters
  and the curves' evidence, several hundred figures to keep in step by hand.
- **Logging inside `Analysis/`**, where a profile's exception is caught: the analyzers would hold
  a logger; the `Cause` property keeps them free of input and output, and the run that reads the
  `Problem` logs it.
- **The tabs' runs as lines of their own around the core's**: `CurveRun` and `ProofRun` write
  their start and done lines with the stages already; a second pair from the tab would say the
  same twice.

## Consequences

- A fourth third-party component, two packages of one project (`ThirdParty.cs`, the BSD-3-Clause
  text in `THIRD-PARTY-NOTICES.md`, the README's table); `ThirdPartyTests` counts four. The About
  window lists it and grows by its card, 73 DIP in English and 88 in Romanian, to 804 and 819
  DIP of content; it has no scroll bar (`known-limitations.md`).
- The log is the first thing to ask a reporter for: the README's "Logs" section says where it is
  and how to raise the level. It holds the paths of the user's files and the figures measured on
  them, not their pixels. It is written beside the executable, wherever that is.
- Every test runs every log line on its path (`LogCapture`, at Trace, `ThrowExceptions` on), and
  three invariants pin where the log is configured, that no log call goes through `Tr.Pick` and
  that nothing that computes or words the report holds a logger (`docs/design/logging.md`).
- `Input/` holds log lines now, so the rule "Formula changes re-validate" asks a `/validate` run
  for a change there even when no figure moves; this task ran it.
- NLog's version moves with the other tools' on the same day, or the suite's release stops on
  the collision.
- A minor version (1.1.0) seems warranted at the next release; the version is the owner's.
