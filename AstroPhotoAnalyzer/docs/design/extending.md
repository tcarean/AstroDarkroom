# How to extend

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §15; the section
> numbers it named are now the notes that hold those sections. Every kind of change below
> starts with its row in `../requirements.md` (the kit's rule "Requirements first").
> **Covers:** the places the checklist sends a change to,
> `AstroPhotoAnalyzer.Core/Runs/AnalysisRun.cs`,
> `AstroPhotoAnalyzer.Core/Input/ImageLoader.cs`,
> `AstroPhotoAnalyzer.Core/Report/Recommendations.cs`,
> `AstroPhotoAnalyzer.Core/Parameters.cs`, `AstroPhotoAnalyzer/Options.cs`,
> `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs`,
> `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs`, `AstroPhotoAnalyzer.Wpf/MainWindow.xaml`
> and `AstroPhotoAnalyzer.Wpf.Tests/WindowSmokeTests.cs`. A change to the code this describes
> means this note is updated in the same task, or deleted with the decision it explains.

## How to extend

**A new measurement**: an analyzer class in `Core/Analysis/` - pure, `Raster` (+ `Parameters`)
in, a record out, flags as booleans on the record, thresholds in `Thresholds.cs` with a comment;
call it from `AnalysisRun.Execute` in dependency order and add a progress stage; add its numbers
to an appendix in `ReportWriter`; write synthetic tests with a known answer; re-approve the
goldens and read the diff; show it in a tab through rows in `Rows.cs`/`ResultTabs.cs` (public
view-models, no core types), each row with a `Description`; **update its rows in
`../requirements.md` and the notes `measurements.md`, `thresholds.md`,
`recommendations-and-report.md` and `window.md`**.

**An explanatory text in the window** (a row's `Description`, a table's `GuideParagraph`s, a
preview tile's `Check`, a parameter field's `*Help` in `ParameterFields`): written in the
view-model, explanation rather than diagnosis, every number interpolated from `Thresholds`,
`Parameters` or the result - a literal the text needs goes into `Thresholds` first, with its
comment (this is how `EdgeRegion*Fraction`, `JpegOverThresholdWarnPct`, `ScreenPixelMm` and the
help-only `ScreenBackground*` / `PrintBackground*` got there). Window only, so no goldens;
`ViewModelTests` asserts the tab texts exist and `ParameterFieldsTests` the help texts (key
phrases, interpolated numbers, paragraphs); **`window.md`** (`console.md`, "Parameters", for a
help text), and `thresholds.md` for the lifted literal.

**A new recommendation**: a rule in `Recommendations.cs` under its topic, with a `Because` that
names the metric and the threshold; a test in `RecommendationsTests` on a scene that triggers it
(extend `Scenes` if none does); goldens; **its row in `../requirements.md` and
`recommendations-and-report.md`**.

**A new parameter**: `Parameters.cs` (default + comment), `Options.cs` + `--help` +
`OptionsTests`, `ParametersFrom` in `AnalyzerCli`, `ParameterFields` + `MainWindow.xaml` +
`ParameterFieldsTests`, appendix F; goldens (help.txt changes); **its row in
`../requirements.md` and `console.md`, "Parameters"**.

**A new print medium**: the source of its Dmax first, in a new ADR beside ADR 0028 (a value
without a source is recorded as unsourced). Then:

- the core: `PrintMedium` (with the source in its comment), `DmaxOf`, `MediumFromName` and
  `NameOf` in `Parameters.cs`. `MediumNames`, `Options.Media`, the `--medium` line of `--help`,
  the unknown-medium error, the window's combo and the level-L\* ladder follow by themselves;
- the one text that names the media by hand: the "Negrul hartiei" paragraph of the Tonuri guide
  in `ResultTabs.cs` (`window.md`);
- the requirements: R-003 (the usage line), R-016 (the paper-black list), R-020 (the names, the
  Dmax values and the error text), R-042 and R-113 ("the three media");
- the tests: `ParametersTests` (`R020_EachMediumHasItsDmax`,
  `R020_TheMediumNamesAreTheCoresAndRoundTrip`), `ColorMathTests.PaperBlackLstarPerMedium`,
  `TonalAnalyzerTests` (the ladder's row count), `OptionsTests`, and in the window's project
  `ParameterFieldsTests` (`R020_ChoosingAMediumFillsTheDmaxFieldWithItsDmax`) and
  `ViewModelTests` (the ladder's rows, the guide's sentence);
- the goldens: `Golden/help.txt` changes; a golden scenario on the new medium if its Dmax opens
  a case the others do not;
- the docs: `console.md`, "Parameters" (the Dmax row), `input.md` (the paper-black list), the
  `Run` line of `CLAUDE.md`, and `README.md` (the window paragraph and "Consola", the options
  line);
- `/validate` if the default medium or its Dmax changed: the cases that run on the default move,
  V-005 on every figure and V-002 if its verdict does; V-001 and V-004 name their paper (`dmax`
  2.0) and stay (the rule "Formula changes re-validate").

The scenes of `Scenes.cs` were built for a paper black of L\* 9.0 (`Scenes.BuiltForDmax`, ADR
0028): a test of "the background under paper black" gives that paper itself and does not
depend on the default medium.

**A new input format**: an `IFrameReader` in `Core/Input/` claiming its extensions,
registered in `ImageLoader.Readers`; fill `SourceInfo`; a hand-built fixture if the
encoder at hand cannot produce the real thing; **its row in `../requirements.md` and
`input.md`**.

**A new tab**: a `ResultTabViewModel` in `ResultTabs.cs`, a `UserControl` in `Views/`
(partial class in `ResultViews.xaml.cs`), a `TabItem` in `MainWindow.xaml`, a `Render`
line in `WindowSmokeTests.R110_EveryTabRenders...`; **its row in `../requirements.md` and `window.md`**.

**A new dependency**: the kit's rule "Packages" and the rule "Package notices" in `CLAUDE.md` -
discuss first; four places, two enforced by tests; **`dependencies.md`, and ADR 0009 if the
reasoning changes**.

**An embedded data set** (a catalogue, a table written by others): the same four places as a
package, with `Resources` instead of `Packages` on its `ThirdPartyComponent` and an
`EmbeddedResource` with a `LogicalName` in the core's csproj (`ThirdPartyTests` ties the two);
its origin and licence argued in `THIRD-PARTY-NOTICES.md`; the file's first lines say where it
came from and when; **`input.md` and `dependencies.md`**.
