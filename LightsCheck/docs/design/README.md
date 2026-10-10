# Design notes

One note per area whose code does not explain itself: a numeric model, a file format parser, a
protocol. How that code works and how its parts fit together are written here and nowhere else
(the kit's rule "Design notes", the kit's ADR 0031). Any other file may name the code, as an
address, and say what a test or a check enforces; the mechanics live in the note of their area.
A note may name classes and methods. It holds the mechanics of the code it covers and the reasons
that have no other home; it repeats neither CLAUDE.md nor an ADR, and points at them instead.

CLAUDE.md points here under "Read before touching". Nothing in this folder is loaded into a
session unless the task needs it.

## The Covers sentence

Every note says, above its first section, which files it describes, in a sentence that starts
with **Covers:**, naming its files as paths in backticks; the rules of the sentence and of the
index are the kit's (`docs/design/README.md` in `ClaudeCodeStarterKit`), checked by
`scripts/check-design.ps1` as a step of `scripts/verify.ps1`. The note is updated or deleted in
the same task as the code it covers.

## The index

| Area | Note | Read before editing |
| --- | --- | --- |
| The session folder, the metadata of a frame, the cadence, the inventory, the banner and the shared formatters, the console's options, dispatch and exit codes | `session-and-console.md` | anything in `src/LightsCheck.Core/Session/` or `src/LightsCheck.Cli/`, `CadenceAnalyzer`, `InventoryRun`, `InventoryReport`, `RunReport`, any console option or exit code |
| The readers copied from FlatCheck: what each file does here, what was cut, the two adapted files, libraw's lazy loading and the shot metadata | `imaging.md` | anything in `src/LightsCheck.Core/Imaging/` |
| Measuring one light frame: the pipeline from mosaic to figures, each step's window and kernel, what was measured against the prototype | `frame-analysis.md` | `FrameAnalyzer`, `Raster`, `Components`, `StarShapes`, `Thresholds.cs`, `FrameRun`, any figure of R-012 to R-017 |
| The series: registration, the dither tables and verdicts, the frame classes and trends, drift and rotation, the drift-aware walking verdict, the sensor's pattern, the RA axis, the read noise, the report and the files | `series-analysis.md` | `Registration`, `DitherAnalyzer`, `SeriesAnalyzer`, `PatternAnalyzer`, `NoiseAnalyzer`, `SeriesRun`, `SeriesReport`, `SeriesOutputs`, any verdict wording |
| The window: the top bar and the tabs, how the rows, charts and cut-outs are fed from the core, the run with progress and cancel, the culture, the icon, the smoke tests | `window.md` | anything in `src/LightsCheck.Wpf/` or `tests/LightsCheck.Wpf.Tests/`, `StarCrops`, `Icon/make_icon.py` |
| The hand analysis the tool reproduces: the Veil session of 2026-10-06, the Python scripts, their figures and findings | `../validation/2026-10-06-veil/raport.md` (Romanian, the report as delivered), `prototype/` | any threshold, any verdict wording, the dithering tables |
