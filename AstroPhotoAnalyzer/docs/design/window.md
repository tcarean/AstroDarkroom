# The window

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §10 and
> `docs/implementation-plan.md`, "Notes from phases 5-7". The behaviour is stated with IDs in
> `../requirements.md` (R-110 to R-119); the rule that the window explains and never decides
> is ADR 0020, the two "fundal" ADR 0013, the culture ADR 0021. The parameter fields and their
> help texts are in `console.md`, "Parameters". **Covers:** `AstroPhotoAnalyzer.Wpf/`. A
> change to the code this describes means this note is updated in the same task, or deleted
> with the decision it explains.

## The window (`AstroPhotoAnalyzer.Wpf/`)

**Layout** (`MainWindow.xaml`): top bar (banner, image path, "Deschide imagine...",
"Salveaza raportul...", "Despre" = F1); left column `ParameterFields` card with
"Analizeaza" (the WCS field, R-027, under the target background); right: a placeholder when nothing is loaded, an error card when the run
failed, otherwise a `TabControl` with Raport, Tonuri, Export, Rezolutie, Culoare,
Preview; `BusyOverlay` over the right side while running. "Fundal" names one thing, the
sky; Tonuri measures its *level* (`BackgroundMedian`, "Nivelul fundalului") and Culoare
its *colour* (`BackgroundResult.Levels`, "Culoarea fundalului"), and every label, row
and appendix says which of the two it shows. Starts maximised. A file from
the command line (`App.FileFromArgs`) is opened at once; otherwise the open dialog is
shown.

**`MainViewModel`**: `Path`, `HasPath`, `IsBusy`/`IsIdle`, `CanRun`, `BusyMessage`
(the progress stage), `Error`/`HasError`, `HasResult`, `CanSave`, `DefaultReportPath`
(`Raport.txt` beside the image), `Banner`, `Copyright`; `OpenAsync(path)` sets the path
and runs; `RunAsync()` validates the fields (`ParameterFields.TryBuild`), clears the
tabs, runs `AnalysisRun.Execute` via `Task.Run`, populates the tabs, or sets `Error`
(`Describe` joins inner messages, no stack); `SaveReport(file)` writes `WriteFull`. It
opens no dialogs (the window does), so it is fully testable.

**Tabs** (`ResultTabs.cs`, each a `ResultTabViewModel` with `Populate(AnalysisResult)`,
`Clear()`, `HasResult`):

| tab | shows |
|---|---|
| `ReportTabViewModel` | `VerdictLabel`; `Sections` (number, title, verdict label, `RecommendationRow`s with mark, text, because, verdict label); `Text` = the full report; `ShowText` toggle |
| `TonesTabViewModel` | `Rows` (metric rows, each with a `Description` - what it measures, thresholds interpolated from `Thresholds`), `Histogram` (101 values), `PaperBlack` (marker), `SimulationRows` (`SimulationRowItem`s: the report's mono line, the target, and a `Preview` command on every target row - none on the header and the footer - that raises `PreviewRequested` with a `PreviewWindowViewModel`), `Definition` (the dust and the sky in use, `ReportWriter.DustDefinition`), `ExactLine`, `NoWcsWarning` (the red sentence, `NoWcsSentence`, or null), `OpenMap` (raises `MapRequested` with a `MapViewModel`), `CloudRows` (every cloud row as mono text), `LadderRows` (mono strings from `TonalAnalyzer.LstarLadder`, "<- mediul ales" on the chosen medium; a static note under it says the level is a grey's), `Guide` (`GuideParagraph`s of the last card, "CUM SE CITESTE TABUL": what the tab asks, the two scales and the conversion, paper black per medium, the dust and the sky, the verdict, the simulation - every number from `Thresholds`, `Parameters.DmaxOf` and `ColorMath`, none typed in). It keeps the `AnalysisResult` privately to build the two windows on demand (`BuildMap`, `BuildPreview`); `MainWindow` subscribes to the two events and shows the windows, non-modal and maximised |
| `MapViewModel` (the map window, R-130) | `Image` (the whole frame under `PreviewMaxSide`), `Layers` (one `ClassLayer` per class of `DustAnalyzer.Classes`, five with the classes and two without: name, legend with the share and the definition, the `RasterBitmaps.Overlay` bitmap from a patch index per reduced pixel, a swatch, `IsVisible`), `Clouds` (`CloudLayer`s: name, caption with L\* and target, centre and radius in reduced pixels, `Labelled` for the darkest `DustCloudReportRows`, `IsVisible`), `ShowClouds` (circles and labels together; off, the cloud list is disabled), `SelectAllClouds` / `DeselectAllClouds`, `HasWcs` (the clouds box is disabled and off without one), `Definition`, `NoWcsWarning`, the two legends. `MapWindow.xaml` stacks the image, the layer bitmaps (an `ItemsControl` on a `Grid`) and a `Canvas` of ellipses and labels in one `Viewbox`, so that everything scales together |
| `PreviewWindowViewModel` (the preview window, R-131) | `Image` (`PrintPreview.Render` at the row's gamma), `BlackLayer` / `LowLayer` (overlays from the `PreviewClass`es), `ShowBlack` / `ShowLow`, `Title`, `Caption` (target, gamma, dust, sky, paper black, print-safe), the two legends with the shares |
| `ExportTabViewModel` | `JpegRows` (mono strings), `JpegGuide` (`GuideParagraph`s under the table: how it is measured, each column group, what is recommended - thresholds from `Thresholds`), `Rows` (TIFF size, rounding, stress, plateau at the reference row's dpi; each with a `Description` - what it measures, thresholds interpolated from `Thresholds`) |
| `ResolutionTabViewModel` | `Rows` (count, FWHM, effective detail, the five regions as "FWHM median in <region>", corner/centre; each with a `Description` - what it measures, thresholds interpolated from `Thresholds` - except the four corner rows, which the centre row's description covers), `SizeRows` (mono strings from `ReportWriter.SizeTableRow`/`SizeTableFooter`: "*" on the recommended row, or the note on why there is none); a static note under the table states the star rule and the pixel-grid rule (180 dpi in bold); `DpiRows` (mono strings, the dpi - distance reference scale from `DpiLadder`, window only) |
| `BackgroundTabViewModel` (the Culoare tab) | `Rows` (the background colour per channel, cast metrics; each with a `Description` - what it measures, thresholds interpolated from `Thresholds`), `RegionRows` (mono strings as in the report, except that a flagged region ends in "! colorat" or "! neutru" - "!" alone on mono), `Guide` (`GuideParagraph`s under the table: the regions, delta, the "!" mark, coloured vs neutral with the reference image's two corners, mono) |
| `PreviewTabViewModel` | `Guide` (`GuideParagraph`s of the card above the tiles, "CUM SE FOLOSESTE PREVIEW-UL": the screen is harsher than paper, with `ScreenEquivalentWidthCm`; the step back, with `ScreenStepBackFactor` against the reference row, or the inverse when the print is larger than the 100 % view; what to look for; what the tab is not), `Tiles`: centre 100 % (480 x 320), centre 200 % (nearest-neighbour zoom), four corners 100 % (320 x 220), whole frame (max side 520), the print simulation at the primary target (gamma on a downsample), the crop when needed - each with a `Check`, what to verify in it, wrapped to the image's width |

Tables are pre-aligned mono strings, the same the report writes, rendered with
`MonoRowTemplate`; the alternative was a template per table for the same text.

**Presentation**: `Theme/Dark.xaml` (palette, styles, converters, templates
`VerdictChip`, `RecommendationRowTemplate`, `SectionTemplate`, `MetricRowTemplate`,
`MonoRowTemplate`, `PreviewTileTemplate`, `ComponentRowTemplate`,
`DescribedMetricRowTemplate`, `GuideParagraphTemplate`); `MetricRowTemplate` puts the
optional `Description` under the label in the label's column only, wrapped, and the `Note`
below it across both columns. `DescribedMetricRowTemplate` (the described panels: Tonuri,
Export's Cuantizare, Rezolutie's stars, Culoare's colour) is the same row on two equal
columns over the card's full width: label, description and note in the left half, the
value right-aligned in the right half, so every description stops at the middle whatever
the value's length; its rows sit 14 px apart, each being a paragraph; the Tonuri
percentiles are one per line. `PreviewTileTemplate` wraps the tile's `Note` and `Check`
to the image's width (bitmaps are 96 dpi, so `PixelWidth` is the width in DIU), else a
sentence would widen the card. `VerdictPalette`
(colour from the core's label: OK green, ATENTIE amber, PROBLEMA red, anything else
neutral); `RasterBitmaps` (a raster region as `BitmapSource`, stride and integer zoom,
frozen); `BarsCanvas` (a `FrameworkElement` drawing a histogram with a marker line in
`OnRender`); `DarkTitleBar` (DWM attribute 20, falling back to 19); `AppIcon` (largest
frame of the .ico for "Despre").

**Culture**: `App.ForceInvariantNumbers` pins `DefaultThreadCurrentCulture` and
`CurrentCulture` to invariant at startup; `InvariantGlobalization` must **not** be set on
the WPF project (XAML fails to load without culture data).

## Notes from phases 5-7

- **The window adds no number and no sentence.** `MainViewModel.RunAsync` calls
  `AnalysisRun.Execute` on a pool thread; the six tab view-models turn the result records
  into rows. Tables are rendered as pre-aligned mono strings - the same strings the
  report writes - instead of one XAML template per table.
- **View-models are public and therefore cannot expose core types**, which are internal.
  The compiler enforces that everything shown passes through the simple rows in
  `Rows.cs`.
- **Parameters are text fields, parsed once at "Analizeaza"** (`ParameterFields`): a
  TextBox bound to a double refuses to be emptied while typing. Parsing is invariant;
  a comma decimal gets a message that says to use the point. The Dmax field alone is not
  empty at the start: it shows the chosen medium's Dmax and is rewritten when the medium
  changes (`console.md`, "Parameters"; ADR 0028).
- **The saved `Raport.txt` is `ReportWriter.WriteFull`** - banner plus body, the same
  bytes the console writes with `-o`. `CultureTests` proves the body is identical under
  `ro-RO` even without forcing the culture, because every number goes through `Fmt`.
- **`WindowSmokeTests` renders the window's *content*, not the window**: an unshown
  `Window` renders transparent. `APA_DUMP_SCREENS=<dir>` saves the renders.
- **Edge-region verdicts changed after the real image** - see
  `../validation.md`: neutral brightening is informative, coloured departure is
  Attention.
- **The explanation pass (after phase 7).** Every metric row got a `Description`, every
  mono table its `GuideParagraph`s, every preview tile a `Check`, proposed and reviewed
  tab by tab. The texts live in the view-models - the one sentence a view-model writes -
  but every number in them is interpolated, and three literals had to be lifted into
  `Thresholds` to make that possible: the edge-region fractions (analyzer), the 5 % of
  samples over 2 levels at q100 (recommendations), the monitor pixel pitch (new). Two
  things the pass changed beyond text: the Fundal table marks a flagged region
  "! colorat" / "! neutru" in the window (the report keeps "!"; the distinction was only
  in the recommendations), and the Preview tab computes the screen-versus-paper rule on
  the image (`ScreenEquivalentWidthCm`, `ScreenStepBackFactor`, window only) with two
  branches, since a small image prints larger than its 100 % view - the synthetic scenes
  are 512 px and land on that branch. Layout lessons: described rows need two equal
  columns, not label/auto, or the longest value narrows every description; a preview
  tile's text must wrap to the image's width or it widens the card.
- **The naming pass (after the explanation pass).** Writing the tooltip for the
  target-background field exposed that "fundal" named two numbers: the Tonuri tab's
  `BackgroundMedian` (median of the green channel over the frame, 0..1, what the
  simulation lifts) and the tab called Fundal's `BackgroundResult.Levels` (per-channel
  medians of the darkest quarter, 0..255, for cast and gradients). The only place that
  said so was a row description. Rather than a third term ("cer curat") or one
  measurement for both (which would move the acceptance number 0.103), the labels now
  name the *property*: the tab is Culoare, the rows are "Nivelul fundalului" and
  "Culoarea fundalului", and Anexa B / Anexa E and the background `Because` say which
  one they carry. The tooltip itself needed two ranges nobody had written down - where a
  screen image's sky sits (0.05-0.10) and where a print's should (0.15-0.25) - which
  went into `Thresholds` as help-only constants, the first literals lifted for a text
  that no rule reads.
