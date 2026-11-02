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
| The session folder, the metadata of a frame, the cadence, the match of check 1, the inventory, the banner and the shared formatters, the console's options, dispatch and exit codes, the interface language and `--lang` | `session-and-console.md` | anything in `src/DarksCheck.Core/Session/` or `src/DarksCheck.Cli/`, `CadenceAnalyzer`, `MatchAnalyzer`, `Thresholds.cs`, `InventoryRun`, `InventoryReport`, `RunReport`, anything in `src/DarksCheck.Core/Language/`, any console option or exit code |
| The readers copied from LightsCheck (FlatCheck's copies): what each file does here, what was cut, the two adapted files, libraw's lazy loading and the shot metadata | `imaging.md` | anything in `src/DarksCheck.Core/Imaging/` |
| The dark series: the stack in memory and the FITS black, the plane estimators, the masters, every frame's figures, the hot pixels, the noise curve, the biases and the dark current, the light and the glow against the warming, the six verdicts, the report and the files | `dark-analysis.md` | `Planes`, `FrameStack`, `Normalisation`, `DarkSeriesAnalyzer`, `BiasAnalyzer`, `LeakAnalyzer`, `DarkVerdicts`, `DarkSeriesRun`, `DarkReport`, `ReportRows`, `DarkOutputs`, `Thresholds.cs`, any figure or verdict of R-020 to R-041, the report's rows |
| Check 9, the lights one by one against the dark master (the local detector, isolation, persistence, the overlap, the amplitude ratio), the rank index and the label, the matching of two masters and the partner of `--against` | `lights-and-index.md` | `LightsAnalyzer`, `IndexAnalyzer`, the step-3 constants of `Thresholds.cs`, any figure or verdict of R-050 to R-065 |
| The window: the top bar and the four states, About and its fit to the screen, the view-model fed by the report's rows, the tables, the maps, the charts, the settings file, the culture, the icon, the tests | `window.md` | anything in `src/DarksCheck.Wpf/` or `tests/DarksCheck.Wpf.Tests/`, `Icon/make_icon.py`, any behaviour of R-070 to R-079, R-120 or R-121, About and its fit |
| The diagnostic log: the configuration, the files and their rolling, the line, what each seam writes, the figures of a record, reading a log, the capture in the tests | `logging.md` | anything in `src/DarksCheck.Core/Logging/`, `src/DarksCheck.Wpf/Loggers.cs`, a log line anywhere, `Program.Main`, `App`'s constructor and `OnExit`, `LogCapture`, `LogFiles`, `LoggingTests`, `WindowLoggingTests` |
| The hand analysis the tool reproduces: the Smida session of 2026-08-12 and the library of 2026-10-10, the Python scripts, their figures and findings | `../validation/2026-08-12-smida/report.md` (the report as delivered), `prototype/` | any threshold, any verdict wording, the index |
