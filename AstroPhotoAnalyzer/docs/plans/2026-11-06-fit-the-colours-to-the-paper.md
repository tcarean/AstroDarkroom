# Fit the colours to the paper

Date: 2026-11-06. Branch `claude/Fit-the-colours-to-the-paper`, worktree
`.claude/worktrees/Fit-the-colours-to-the-paper` (from `main` at c3141954, the kit's rule "Worktrees").
Already in the worktree: the draft rows R-175 to R-178 in `docs/requirements.md`, ADR 0083
(accepted 2026-11-06 in chat), the failing tests `AstroPhotoAnalyzer.Tests/GamutFitTests.cs` and
`AstroPhotoAnalyzer.Wpf.Tests/GamutFitTabTests.cs` (the solution does not compile: the types do not
exist), V-016 unmeasured in `docs/validation.json` and `.md`.

## Context

Since ADR 0081 the warning marks what Lightroom marks: dark saturated dust the paper cannot hold.
The owner asked (2026-11-06) for the correction to be automated generically: the recipe as a
recommendation, and a new corrected FITS or 16-bit TIFF written by the tool, the originals never
touched, offered as variants in a tab like Editare Curbe. Measured (ADR 0083): every such pixel
fits once its chroma alone is reduced along its hue at its L\*; a table over L\* and hue builds in
1.3 s and matches the exact search on baryta, needs the exact search near the edge on canvas;
ImageSharp writes TIFF at 8 bits only, so the TIFF writer is ours.

## The change, in order

### 1. Core: colour maths and thresholds

- `Input/ColorMath.cs`: `LabD50OfSrgb(r, g, b)` and `SrgbOfLabD50(L, a, b)`: encoded sRGB <->
  Lab under D50 through the Bradford-adapted matrices `SoftProof` already holds (lift the three
  rows and their inverse into `ColorMath` as the one place; `SoftProof.Display` uses them from
  there). These are the Lab the package's round trip uses (D50), so the fit, the table and the
  gamut rule speak one Lab. `LabOfSrgb` (D65) stays for the Culoare tab.
- `Thresholds.cs`: `GamutFitLstarNodes` (101), `GamutFitHueNodes` (121), `GamutFitEdgeMargin`
  (the band of chroma around the table's edge where the exact rule decides; measured in step 2,
  starting at 0.6 units, the canvas miss being 0.28 in factor on chroma ~2), `GamutFitKneeExact`
  (1.0), `GamutFitKneeSoft` (0.9), `GamutFitKneeSofter` (0.8), `GamutFitBandShare` (0.90),
  `GamutFitRecipeShare` (0.99), `GamutFitRecipeStep` (0.05), `GamutFitMinChroma` (0.05, under
  which a pixel is grey and never touched). Each with its provenance comment (the probe figures of
  ADR 0083).

### 2. Core: the fit (`Analysis/GamutFit.cs`)

- `GamutFitTable`: `Build(Func<double,double,double,double> deltaE, int lNodes, int hNodes)`:
  at every node (L\*, hue) a binary search (10 steps, 0..150) for the largest chroma with
  `deltaE <= CurveGamutDeltaE`; `Cmax(L, hue)` bilinear; keeps `deltaE` as `Exact` for the edge;
  `Problem` when the rule gives NaN. Rows in parallel. `GamutAnalyzer` gains
  `DeltaEOfLab(config, black, L, a, b)` (the compensated trip from a Lab colour: Lab -> XYZ D50
  -> `Compensate` -> `.Icc` -> back -> `Uncompensate` -> Lab, the same trip as `RoundTrip`) and
  `GamutTable.ExactLab` set in `Build`, so the fit's table is built from the gamut table of the
  run (`GamutFitTable.Build(gamut.ExactLab, ...)`).
- `GamutFit.Apply(Raster r, GamutFitTable t, double knee, bool keepFacts)` -> `GamutFitOutcome`
  { `Image` (a new raster), and with `keepFacts`: `OutsideBefore[]`, `OutsideAfter[]`,
  `PctOutsideBefore/After`, `PctTouched`, `PctUnreachable`, `ChromaLostMedian`, `ChromaLostP99`,
  `PctOutsideUnderLowSeparation` }. Per pixel (rows in parallel): Lab D50; chroma under
  `GamutFitMinChroma` -> copy; `cmax = t.Cmax(L, hue)`; if `|C - cmax| < GamutFitEdgeMargin`
  the exact rule refines cmax by a short bisection on the pixel's own hue (cached per 8-bit colour
  in a `ConcurrentDictionary`, as ADR 0042's hybrid); the knee: `C' = C` for `C <= k cmax`, else
  `k cmax + (cmax - k cmax)(1 - exp(-(C - k cmax)/(cmax - k cmax)))` (hard clip at `cmax` when
  `k == 1`); a pixel outside whose `cmax == 0` and whose rule at chroma 0 still passes is "out of
  reach": copied, counted. `OutsideAfter` is the rule on the fitted colour (exact, once per 8-bit
  colour; with `keepFacts` only). Back to sRGB, clamped 0..1. Mono rasters: a copy, nothing
  outside.
- `GamutFit.Recipe(Raster r, GamutFitTable t)` -> `GamutFitRecipe { LstarCeiling, Factor, PctLeft }`:
  the L\* under which `GamutFitBandShare` of the pixels outside lie (rounded up to a whole),
  then the largest factor in steps of `GamutFitRecipeStep` whose uniform chroma scaling of the
  pixels under that L\* brings `GamutFitRecipeShare` of the outside pixels in (the rule per
  pixel, exact once per 8-bit colour), and the share of them left.
- Measure in this step (a scratchpad harness over the worktree's core, or a `[Trait("Category",
  "Validation")]` test run by hand on the three pairs): the share left outside against the exact
  per-pixel search for `GamutFitEdgeMargin` 0.3, 0.6, 1.0, 1.5 on the plate-solved FITS through
  baryta and canvas and on the Smida TIFF through WhiteWall; pick the smallest margin within 0.05
  points everywhere; its comment carries the table. Time the full-image fit (22.6 MP) for the save.

### 3. Core: the run, the texts, the writers

- `Runs/GamutFitRun.cs`: `GamutFitSet { Problem, Profile (ProofProfile, for the description),
  Table, OriginalPreview (Raster), OriginalOutside (bool[]), Variants, Recipe }` and
  `GamutFitVariant { Name, Knee, Preview (Raster), Outside (bool[] after), Evidence
  (GamutFitEvidence: the outcome's shares and figures) }`. `Execute(a, maxWidth, maxHeight,
  progress)`: no profile -> `ProofTexts.NoProfile`; `GamutAnalyzer.Build(path)` (the run's own
  table; a `Problem` is the set's); `GamutFitTable.Build` from it; the preview at
  `CurveRun.PreviewStride`; the three variants on the preview with facts; the recipe on the tonal
  subsample (`Thresholds.TonalStride`, where the evidence shares are measured too, so V-016
  matches the figures the owner reads). `RunClock` named `gamutfit` (`profile`, `table`, each
  variant), the set's figures on its done line (`logging.md`).
- `GamutFitExport.Save(AnalysisResult a, GamutFitSet set, GamutFitVariant v, string target)`:
  the guards of `CurveExport.Save` (input gone, same path, the hold without sharing) lifted into
  one helper both exports call (`CurveExport.Guarded(a, target, Action<string sourceFits> write)`);
  the full image fitted (`GamutFit.Apply` with the set's table, no facts); by the target's
  extension: `.tif`/`.tiff` -> `TiffWriter.Save(image, a.Raster.Source.Icc ?? IccProfiles.Srgb,
  GamutFitTexts.Description(v), target)`; otherwise `FitsWriter.Save(image, identity, sourceFits,
  GamutFitTexts.History(v), target)` with the identity `ToneCurve` of two points. Log one line per
  file written.
- `Input/TiffWriter.cs`: classic little-endian TIFF, one strip per `RowsPerStrip` of 64 rows,
  uncompressed, 16-bit RGB chunky (`BitsPerSample` 16,16,16, `SampleFormat` 1), tags 256, 257,
  258, 259, 262, 270 (the description), 273, 277, 278, 279, 282/283 (72 dpi, as Siril writes),
  284, 296, 305 (`VersionInfo.Banner`), 339, 34675 (the ICC bytes); through `AtomicFile.Write`.
  Mono rasters as 16-bit grey (`Photometric` 1, one sample) so the writer has no colour-only
  branch. Samples rounded to 0..65535.
- `Input/IccProfiles.cs` (or a constant in `TiffWriter`): the standard sRGB profile for an input
  without one. The smallest honest choice: embed the compact sRGB v2 the tests already build
  (`IccTestData.V2` lives in the tests): instead, build it in the core from the sRGB primaries,
  D65 white adapted to D50, the sRGB curve as a 1024-point `curv`, `desc` "sRGB", `cprt` ours
  (no licensed bytes); about 60 lines; `IccProfileInfo.Describe` reads it back as "sRGB".
- `Report/GamutFitTexts.cs`: `Original` (reuse `CurveTexts.Original`), `Computing`, the three
  names (`Tr.Pick("Exact", "Exacta")`, `("Soft", "Lina")`, `("Softer", "Foarte lina")`), each
  variant's one-line help, `EvidenceRows(variant)` in plain words with every number interpolated
  (shares before/after, touched, chroma given up at the middle and at the 99 % mark worded as
  "the colour moves by ... at most for 99 of 100 of them", the dark share with
  `LowSeparationLstar`, the recipe sentence, the out-of-reach sentence when > 0),
  `ProposedFileName(input, name)` (the input's extension, `.fit` for anything not TIFF),
  `History(variant)` (the banner, the rule in words, the variant and its knee),
  `Description(variant)` for the TIFF tag, `Saved`, the tab's note under the gallery (what the
  fit does and does not do, the reduced copy, sRGB assumed). The words median, percentile, chroma,
  knee, gamut as a bare noun, LUT never appear in a user text (the test checks three of them).
- `CLAUDE.md` first paragraph and the "Read before touching" list: the tool now writes two kinds
  of file; `docs/design/extending.md` "A new output" paragraph updated.

### 4. Window: the tab

- `ViewModels/GamutFitTab.cs`: `GamutFitTabViewModel : ResultTabViewModel` on the
  `CurvesTabViewModel` pattern (`Populate` keeps the result; `ComputeAsync`/`Compute(w, h)`;
  `_run`; `Reset`; `SetGallerySize`; `Items` of `GamutFitGalleryItem { Name, Image, Overlay
  (BitmapSource or null: made once per item from the set's mask through `RasterBitmaps.Overlay`,
  null when the mask is empty), Width, Height, EvidenceRows, Variant }`; `ShowOutside` (the box;
  reset by `Reset`); `Current.Overlay` exposed through `CurrentOverlay` = `ShowOutside ?
  Current?.Overlay : null`... the test reads `Current.Overlay` directly after setting
  `ShowOutside`, so the item's `Overlay` property returns null while the box is off: the item
  holds the bitmap privately and the tab sets a flag on its items when the box changes);
  `Next`/`Previous`/`SaveAs`/`Calculate`; `SaveRequested(proposedName)`; `Save(path)` calls
  `GamutFitExport.Save`; `IsProblem`/`Message` as the curves tab. Logger `Loggers.GamutFit` =
  "Window.GamutFit".
- `Views/GamutFitView.xaml` (+ the partial in `ResultViews.xaml.cs` with `GallerySizeChanged`):
  the `CurvesView` grid without the purpose radios: the box `arata ce nu intra`, `Calculeaza`,
  the message; the image with the overlay `Image` on top inside the same `Viewbox` (a `Grid`
  of two images, as the soft proof's layers); Back/Name/Position/Next, `Salveaza ca...`; the
  stacked evidence; the note; the save message's fixed place.
- `MainWindow.xaml`: the `TabItem` `Culori pentru hartie` / `Colours for paper` between the
  curves and the soft proof, `x:Name="GamutFitTabItem"`. `MainWindow.xaml.cs`: `GalleryKeys` serves
  whichever of the two gallery tabs is selected; `SaveGamutFit(proposedName)` with the dialog
  filter `FITS|*.fit|TIFF|*.tif` and the default extension from the proposed name.
- `MainViewModel`: `GamutFit` tab, `GamutFitComputation` started beside the other two after
  `Populate`; `Populate`/`ClearTabs` include it; `Loggers.cs` the new name.
- `WindowSmokeTests.R110_EveryTabRenders...`: a `Render` line for the new view;
  `window.md`'s tab table a row; `testing.md` if it lists the tabs' tests.

### 5. Docs, figures, wrap-up

- `docs/design/paper-fit.md`, new: Covers `GamutFit.cs`, `GamutFitRun.cs`, `GamutFitTexts.cs`,
  `TiffWriter.cs`, `IccProfiles.cs`, `GamutFitTab.cs`, `GamutFitView.xaml`: the table, the knee,
  the edge rule, the recipe, the run, the writers, the tab; `docs/design/README.md` index row;
  `pipeline.md` (the fourth run), `input.md` (the TIFF writer, the sRGB profile), `logging.md`
  (the run and the writes), `thresholds.md` (the new rows), `window.md` (the tab row, the keys),
  `measurements.md` if it lists the gamut figure's uses; `known-limitations.md` (the fit never
  moves tone: what is out of reach stays, Tonuri's lift is the remedy).
- `ValidationTests`: kind `gamutfit` -> `GamutFitFigures`: the set's problem, the three variants'
  shares before/after/touched, chroma lost median/p99, the dark share, the recipe's three figures,
  the time of the full-image fit is not a figure. Copy the reference files into the worktree's
  `testdata/large/` (the FITS, `de_printat.png` is not needed, the two profiles) and run
  `/validate`: fill V-016, paste the MEASURED line, record the date; V-008 and V-015 must not
  move (no formula of theirs changes; `ColorMath` gains functions, `SoftProof.Display` keeps its
  numbers: the drift table shows it).
- `README.md`: "The window" gets the tab; the files the tool writes (FITS and 16-bit TIFF) where
  the curves' FITS is described. `CHANGELOG.md` Added. Requirements rows to `implemented`.
  `docs/plans/2026-11-06-fit-the-colours-to-the-paper.md` = this plan. Then `/wrap-up`: gate,
  the diff read back, commits on the branch (small: core maths + thresholds; the fit + tests;
  the run and the writers; the window; the docs and figures), the merge proposed, a subagent
  review offered before it (the memory "Code review before merge").

## Verification

- `dotnet build` the worktree's solution green, then `GamutFitTests` and `GamutFitTabTests`
  green; `RepoInvariantTests` (R-175..R-178 implemented with tests named R175_..R178_);
  `LicenseHeaderTests`; `WindowEnglishTests` (every new text a pair); the goldens unchanged (no
  console change: `APA_UPDATE_GOLDEN` not needed).
- `scripts/verify.ps1` green in the worktree; `APA_DUMP_SCREENS` render of the new tab looked at
  (`tab-culori-hartie.png`): the box, the red layer over the patch, the evidence, the buttons.
- `/validate`: V-016 measured and recorded; V-008, V-015 unchanged; the edge-margin table of
  step 2 pasted in the ADR's consequences or the design note.
- The owner's image: run the window from the worktree build on the Smida TIFF with the WhiteWall
  profile, save `Exacta` as TIFF, open it in the window again: the warning's share near 0 and the
  file 135 MB, 16-bit, sRGB profile present (checked with the console's file facts line).
