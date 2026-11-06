# Design notes

One note per area whose code does not explain itself. Each says what the code does, how its
parts fit, and, where it was found out the hard way, why the obvious simpler version is wrong.
`CLAUDE.md` points here under "Read before touching"; nothing in this folder is loaded into a
session unless the task needs it.

Until 2026-10-02 these notes were `docs/specification.md` (§2 to §16) and the notes of
`docs/implementation-plan.md`; they were moved here unchanged (ADR 0001), with pointers in
place of the old section numbers. The specification's §1 is now the first paragraph of
`CLAUDE.md`, ADR 0007 and ADR 0008; its §12 is `CLAUDE.md` ("Commands"), ADR 0022 and ADR 0023.
The behaviour the notes describe is also stated, with IDs, in `../requirements.md`. Where a
row, a note and an ADR were written from the same paragraph, the three still say it in their own
words; cutting the notes down to what neither a row nor an ADR says is a task of its own.

**Every note is maintained** (the kit's rule "Design notes"): it says above its first section,
in a **Covers:** sentence, which files it describes, and a change to those files means the note
is updated in the same task, or deleted with the decision it explains. `scripts/check-design.ps1`
checks this index and the Covers paths at every commit; `scripts/check-design.ps1 -Since main`,
run in a task's worktree, lists the notes a branch touched.

| Area | Note | Read before editing |
| --- | --- | --- |
| The projects, the invariants of the structure, the chain of analyzers, the progress stages, the result | `pipeline.md` | `Runs/AnalysisRun.cs`, a new analyzer's place in the chain |
| The readers, the raster, colour maths, statistics, the refusal of linear data; what was measured about ImageSharp 3.1.11 | `input.md` | anything in `Input/`, the hand-built TIFF and PNG fixtures |
| Every analyzer: what it measures and how, its flags, what was found while building it | `measurements.md` | anything in `Analysis/`; ADR 0014 to ADR 0019 first |
| The thresholds, with their values and meaning | `thresholds.md` | `Thresholds.cs`; ADR 0012 first |
| The recommendation rules per topic, the layout of the report, the number formatting | `recommendations-and-report.md` | anything in `Report/`, the wording of a recommendation, the golden files |
| The parameters (defaults, flags, fields, the rule between them, the help texts), the console, the golden scenes | `console.md` | `Parameters.cs`, `Options.cs`, `AnalyzerCli.cs`, `ParameterFields.cs`, `GoldenOutputTests` |
| The window: layout, `MainViewModel`, the eight tabs, presentation, culture; what the explanation and naming passes taught | `window.md` | anything in `AstroPhotoAnalyzer.Wpf/`; ADR 0013 and ADR 0020 first |
| The soft proof: the chain through the profile's tables, the table per intent, the display step, the warning, the tab and its window | `soft-proof.md` | `SoftProof.cs`, `ProofRun.cs`, `ProofTexts.cs`, `SoftProofTab.cs`, the three soft-proof views, `SoftProofWindow` in `OverlayWindows.xaml.cs`; ADR 0066 first |
| The paper fit: the table of the largest chroma over L\* and hue, the edge rule, the knee, the recipe, the run, the two writers (FITS and 16-bit TIFF), the sRGB profile, the tab | `paper-fit.md` | `GamutFit.cs`, `GamutFitRun.cs`, `GamutFitTexts.cs`, `TiffWriter.cs`, `IccProfiles.cs`, `GamutFitTab.cs`, `GamutFitView.xaml`; ADR 0083 first |
| The interface language: `Tr.Pick`, the `T` markup extension, who sets the language, the aligned tables, the tests kept in Romanian and the English guard | `language.md` | any text the user sees, `Language.cs`, `TExtension.cs`; ADR 0048 first |
| The diagnostic log: the configuration, the files, the line, the figures' rule, what each seam writes, the capture in the tests | `logging.md` | anything in `Logging/`, a log line anywhere, `Program.cs`, `App.xaml.cs`; ADR 0069 first |
| The checklist per kind of change: a measurement, a window text, a recommendation, a parameter, a medium, an input format, a tab, a dependency | `extending.md` | before any of those changes |
| The package, where it is touched, the behaviours of 3.1.11 the code depends on, the rule as first written | `dependencies.md` | `Directory.Packages.props`, the core's csproj, `ThirdParty.cs`; ADR 0009 first |
| The kinds of test, the shared fixtures, the window's harness | `testing.md` | a new fixture or scene, `StaFixture`, `WindowSmokeTests` |
| The limits known and accepted | `known-limitations.md` | before "fixing" one |
| Every file, one line each | `code-map.md` | a file you have not opened before |

The decisions themselves, with the alternatives rejected, are in `../decisions/`. The behaviour,
with IDs, is in `../requirements.md`. The figures measured on the reference image are in
`../validation.md`.
