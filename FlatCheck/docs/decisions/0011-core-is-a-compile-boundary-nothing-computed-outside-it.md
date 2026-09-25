# 0011. FlatCheck.Core is a compile boundary; nothing is computed outside it

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/code-map.md and
docs/design/two-applications.md; the decision itself dates from 2.0.0, when a second application
(the WPF window) appeared over the same numbers as the console. Before the split, "Analysis/ and
Report/ are pure" was a convention held by goodwill. Two front ends that each derived a figure
would have produced two figures for one master.

## Decision

Everything that computes and everything that formats lives in `FlatCheck.Core`, which has no
entry point and knows nothing about consoles or windows, so it cannot reach `Console` even by
accident. `Analysis/` decides every number (one pure analyzer per mode into an `XxxResult`),
`Thresholds.cs` holds every threshold, `Report/` holds all Romanian wording (pure
`XxxResult + TextWriter -> text`), and `Runs/` is the whole chain of a mode - load, analyze,
report, encode the map - that both applications call. Neither application reimplements a
number; a view-model formats, it does not decide. A run writes its report into a `TextWriter`
the caller supplies and returns the residual map as PNG bytes (`RunOutcome`); a run that
produces a file (`SmoothRun`, `SyntheticRun`) writes it to the caller's `Stream`, never to a
path of its own choosing.

## Alternatives rejected

- **Copying the console's code into the window.** The 2.0.0 split was a library extraction, not
  a copy, so a change reaches both or neither.
- **Computing in a view-model.** Any new number, threshold or sentence of diagnosis added there
  would exist in one application only; the rule sends it to `Analysis/`, `Thresholds.cs` or
  `Report/` so the console gets it too.
- **Runs returning the report as a string.** A 32-frame `--light` series would sit silent for
  two minutes; with a caller-supplied `TextWriter` the console streams the table row by row and
  anyone who wants the string passes a `StringWriter`.
- **Runs writing the map to disk themselves.** The window wants to show it, not save it; naming
  a second output file (`FlatCheckCli.WorldMapPath`) is a console concern and lives there.

## Consequences

- The window adds a workflow (one folder, `pp_flat_stacked.fit` found by convention, runs started
  on open) and never an analysis, a threshold or a wording. Its scale switch, legend, shape guide
  and histogram card are drawing and wording; the caps come from `Th.Resid*`, the bands from
  `ExposureBands`.
- Report pieces both applications show are exposed as values from `Report/` (`StructureReport.
  TestNote`, `LocalSpotNote`, `RatioReport.WorldNote`, `ExposureReport.HistogramReading`,
  `RunReport.WriteBanner`, `WarrantyLines`), so one definition serves both.
- A public view-model cannot expose an internal core type, so the compiler itself stops a
  `StructureResult` from leaking into XAML.
- `RunOutcome.SecondaryPng` exists only for `--ratio`, the one mode with two maps; `ExposureRun`'s
  `withPreview` is the one opt-in, off by default because only the window shows the preview.
  The golden files and `crosscheck.py` stay valid for both applications.
- Pinned by `EveryTestRowCarriesItsOwnDescription`, `TheRowsAreTheSameOnesTheStructureTabShows`,
  `EveryRowOnTheScreenCarriesADescription` and `WindowSmokeTests` (which renders every tab from
  core results and fails on any binding error).

Source notes: docs/design/code-map.md (`FlatCheck.Core`, `Analysis/`, `Report/`, `Runs/`),
docs/design/two-applications.md ("Neither reimplements a number", "What the window adds, and
what it does not").
