# Soft proofing tab (Lightroom-like, view only)

Branch `claude/Add-a-soft-proofing-tab`, worktree `.claude/worktrees/Add-a-soft-proofing-tab`.
Approved 2026-10-30. The requirement IDs became R-160 to R-163 (a new block of ten for a new
area, as `docs/requirements.md` asks), not R-158 to R-161 as written below.

## Context

The owner asked (2026-10-30) for a new window tab that soft-proofs the analysed image through the
ICC printer profile already chosen in the top bar (R-155), as close as possible to Lightroom
Classic's soft proofing: rendering intent, "Simulate Paper & Ink", gamut warnings, the image
full-screen with a margin whose colour follows the selections. Nothing is edited and no proof
copy is made: strictly viewing. Without a profile the tab shows only an error. The feature
proposal was accepted with its recommendations: a third intent beyond Lightroom (relative
without black point compensation, the Tonuri tab's own model), a screen-gamut warning with the
screen assumed sRGB, the proof live in the tab plus a maximised window, a margin list, no proof
histogram now.

Facts measured before this plan (scratchpad probe on the two lab profiles, the Epson P900
baryta and the CMYK `RSWOP.icm`, Wacton.Unicolour 8.0.0, 2026-10-30):

- Unicolour has the intents Perceptual, RelativeColorimetric and AbsoluteColorimetric and lets
  the two directions use different ones; it has no black point compensation (BPC), so BPC is
  the ICC XYZ scaling, written by us. The media white comes from the `wtpt` tag (Profile.Tags).
- Paper white through the absolute intent: baryta L\* 97.6 b\* +1.1, canvas L\* 95.0 (b\* -7
  on the proofed white), P900 L\* 95.5; paper black relative L\* 3.49 / 22.3 / 6.54.
- Relative without BPC on canvas crushes every grey under L\* 22 to paper black; with BPC
  nothing is crushed: the two differ, and the first is what the Tonuri tab counts.
- A grid of 65 nodes per channel, interpolated trilinearly, is within Delta E76 1.0 (p99) and
  1.4 (max) of the exact per-pixel proof on both lab profiles; 33 nodes reach 2.9 to 5.2 (max)
  on canvas. A 65-grid builds in 1.6 to 2.2 s with the rows in parallel on this machine.
- The CMYK profile proofs through every intent; nothing special is needed.

## Design (the decisions the plan carries; each gets its ADR, see "Records")

**The proof chain**, computed only in the core (`Analysis/SoftProof.cs`):

1. source sRGB (the image's pixels, assumed sRGB as everywhere in the tool) -> XYZ D50;
2. for the intent `Relative`: BPC towards the paper, `XYZ' = XYZ * (W - Kp) / W + Kp` per
   component, `Kp` the device black through A2B1 (what `IccPaperBlack` evaluates), `W` D50;
   for `Perceptual` and `Relative without BPC`: none;
3. the profile's B2A table of the intent (B2A0 perceptual, B2A1 relative) -> device values;
4. A2B1 (relative colorimetric) -> the colour the paper shows, XYZ relative to paper white.
   The table stores this (a `ProofTable`, 65^3 XYZ nodes, one per intent, built on demand);
5. the display step, per pixel after interpolation, decided by "Simulate paper & ink":
   off: BPC towards the screen (`(XYZ - Kp) * W / (W - Kp)`), paper white = screen white and
   paper black = screen black; on: the absolute scaling (`XYZ * wtpt / W`), paper white and
   black as they are; then XYZ -> linear sRGB (Bradford D50 -> D65 as Unicolour does) -> encoded.
6. warnings: *paper gamut* = `GamutAnalyzer.Outside` on the existing `GamutTable` (a*, b* moved
   over `CurveGamutDeltaAb` on the relative round trip; the share therefore equals the Curbe
   figure); *screen gamut* = the display colour of step 5 needs a channel outside 0..1 and the
   clipped colour differs from it by more than `ProofScreenGamutDeltaE` (new threshold, 2.0,
   to be confirmed on the reference image); both = the two overlays paint (magenta where they
   meet, as Lightroom).

**The margin** (view only, no number): `Auto` = screen white with the simulation off, the
paper's white (the display colour of sRGB white under step 5 "on") with it on; the list adds
White, Light grey, Grey, Dark grey, Black (Lightroom's right-click set). The margin colour is
a core figure (`SoftProofResult.PaperWhiteSrgb`); the five fixed greys are presentation
constants in the Wpf project (not thresholds: nothing is measured against them).

**Figures the tab shows** (all from the core, `SoftProofResult`): the profile's name, the
paper white L\* a\* b\*, the paper black L\* and Dmax, the share of pixels outside the paper's
gamut and outside the screen's, which tables the profile has (perceptual table present or
not; without B2A0 the `Perceptual` radio is disabled with the reason), and the one-line
caption the window shows.

**The state is viewing preference**: the intent, the two boxes, the two warnings, the margin
choice and "Original" live in the tab's view-model and survive another image in the session;
not remembered in `settings.json` (ADR 0064's reasoning: a view opens in a state the reader
chose this session, nothing hidden from before).

## Files

### Core (`AstroPhotoAnalyzer.Core/`)

- `Analysis/SoftProof.cs` (new): `ProofIntent` enum (`Perceptual`, `Relative`,
  `RelativeNoBpc`); `ProofTable` (the 65^3 XYZ nodes of one intent, `Problem` when the
  profile cannot be used, built with the rows in parallel; `Build(path, intent, n)` reusing
  the Unicolour calls of `GamutAnalyzer.Build`); `ProofProfileFacts` (name, media white XYZ
  from `wtpt`, paper black XYZ/L\*/Dmax, has B2A0/B2A1, the paper white's display colour);
  `SoftProof.Render(table, facts, raster, simulate, gamut)` -> `SoftProofResult` (the proofed
  `Raster`, a `ProofClass[]` per pixel: none / paper / screen / both, the two shares, the
  paper white sRGB, the caption). Trilinear interpolation shared with
  `GamutAnalyzer.Interpolated` (lift the weights loop into one helper used by both).
- `Thresholds.cs`: `ProofGrid = 65` (with the measured Delta E table in its comment),
  `ProofScreenGamutDeltaE = 2.0`, `ProofMaxSide = 2400` (the proof's long side: the window
  shows it on a 4K screen too; the preview's 1600 is for a Full HD render), each with its
  comment and its `thresholds.md` row.
- `Runs/ProofRun.cs` (new, the third run beside `AnalysisRun` and `CurveRun`):
  `ProofRun.Prepare(AnalysisResult)` -> `ProofSet` (the facts, the reduced raster at
  `ProofMaxSide`, the gamut table, or `Problem`: no profile -> the sentence naming the
  `From ICC profile...` button; unusable profile -> the core's reason), and
  `ProofRun.Render(set, intent, simulate)` that builds the intent's table once (cached on the
  set) and renders. The console never calls it.
- `Report/ProofTexts.cs` (new): the sentences (no profile, no perceptual table, the caption,
  the legends with the shares, the margin names, the intent names and their one-line help).

### Window (`AstroPhotoAnalyzer.Wpf/`)

- `ViewModels/SoftProofTab.cs` (new): `SoftProofTabViewModel : ResultTabViewModel` with
  `Intent` (three radios), `SimulatePaper`, `ShowPaperGamut`, `ShowScreenGamut`,
  `ShowOriginal`, `Margin` (an enum list), `Image` (proof or original bitmap), `PaperLayer`,
  `ScreenLayer`, `BothLayer` (overlays from `RasterBitmaps.Overlay`), `MarginBrush`, the
  figure rows, `Message`/`IsProblem` (the error when there is no profile), `IsBusy`, the
  `FullScreen` command raising `ProofRequested` with the same view-model. `Populate` keeps the
  result and starts `ProofRun.Prepare` + the first render on a pool thread (the `_run` number
  pattern of `CurvesTab.cs`: a late result for an old image touches nothing); a change of
  intent renders again on a pool thread (the table of a new intent builds then, ~2 s, "se
  calculeaza" shown meanwhile); a change of simulation, warnings, margin or original is a
  render of the cached table (~50 ms) or a visibility switch. `ProofComputation` task for the
  tests, like `CurvesComputation`.
- `Views/SoftProofView.xaml` (new) + partial in `ResultViews.xaml.cs`: a grid like
  `CurvesView`: controls row (intent radios, the four boxes, the margin combo, `Full screen`),
  the proof area: a `Border` with `MarginBrush` holding a `Viewbox` (`DownOnly`) with the
  image and the three overlay layers, a `Grid` of the figure rows under it, the faint sentence
  (reduced copy, sRGB assumed, the screen assumed sRGB), the red message when there is no
  profile and the area is empty.
- `Views/SoftProofWindow.xaml` (new) + class in `OverlayWindows.xaml.cs`: maximised, the
  same `DataContext`, a thin top bar with the caption and the same controls, the margin brush
  filling the window behind the image; keys P (paper & ink), G (paper gamut), M (screen
  gamut), I (next intent), O (original, toggle), Esc.
- `MainWindow.xaml`: the eighth `TabItem` `Soft proof` (same word in both languages);
  `MainWindow.xaml.cs`: `_vm.SoftProof.ProofRequested += p => new SoftProofWindow {...}.Show()`.
- `MainViewModel.cs`: the `SoftProof` tab in the constructor, `Populate`, `ClearTabs`; after
  `CurvesComputation` is started, `ProofComputation = SoftProof.ComputeAsync()` (the two
  second runs are independent; the proof's first table ~2 s).
- `Presentation/`: nothing new expected; `RasterBitmaps.Region` paints the proof raster.

### Tests

- `AstroPhotoAnalyzer.Tests/IccPrinterTestData.cs`: the v2 fixture gains `B2A0` and `B2A1`
  lut16 tables (Lab -> device, the inverse of the neutral linear model, a 2-point cube on L
  only), a `whiteXyz` for a tinted paper, and an optional distinct perceptual black, so a
  hand-built profile round-trips: greys come back to themselves under relative + BPC, paper
  white and black appear under the simulation, the intents differ.
- `AstroPhotoAnalyzer.Tests/SoftProofTests.cs` (new): `R158_...` the chain on the fixture (a
  grey ramp proofs to itself within 1/255 under Relative with simulation off; under
  simulation the white is the fixture's tinted white and the black the fixture's black L\*;
  `RelativeNoBpc` sends a grey under the black to the black; `Perceptual` uses B2A0);
  `R159_...` the warnings (a saturated colour the fixture desaturates is outside the paper
  gamut, the share counts it; a screen-gamut pixel from a hand-built table with an XYZ the
  screen cannot show); `R160_...` the problems (no profile, a profile without B2A, the
  perceptual radio disabled without B2A0); the trilinear helper equals the exact value at the
  nodes. A `ProofFigures` kind `proof` in `ValidationTests` and a case V-015 (the plate-solved
  FITS on `Hahnemuhle PRBaryta.icm`: the paper white L\*/b\*, the paper black L\*, the two
  shares per intent, the proof's width) with `expected` null until measured.
- `AstroPhotoAnalyzer.Wpf.Tests/SoftProofTabTests.cs` (new): `R161_...` the tab's state
  (starts on Perceptual with everything off and `Auto`; the error without a profile; the
  computation after the analysis without a press; a late result never reaches another image;
  the margin brush follows the selection; `Original` swaps the bitmap and keeps the margin;
  toggling a warning shows its layer alone), the window's keys, and a render of the tab and
  the window with no binding errors (`BindingErrors`); `WindowSmokeTests.R110_EveryTab...`
  gets its `Render` line (with a profile chosen through `vm.Parameters.UseProfile` on the
  fixture, and one render of the error state); `AutomationTreeTests` lists the new tab's
  view; `WindowEnglishTests` covers its texts; `ViewModelTests` asserts `ProofComputation`
  like `CurvesComputation`.

### Docs and records

- `docs/requirements.md`: R-158 (the chain and the intents), R-159 (the warnings), R-160 (the
  problems and the profile's tables), R-161 (the tab and the window), all `draft` until the
  tests pass, then `implemented`; R-110 says eight tabs and names `Soft proof`.
- `docs/decisions/0066-...md`: the soft-proof chain (the display step relative + BPC against
  absolute, the warnings' two definitions, the screen assumed sRGB, the grid of 65 with the
  measured table, the three intents and why the third exists, the margin rule, the state not
  remembered); alternatives rejected: lcms2 (native), a 33 grid, the exact trip per pixel
  (35 s), a monitor profile from Windows, a proof histogram, a remembered state.
  (Check `main`'s latest ADR number right before the merge; 0065 today.)
- `docs/design/soft-proof.md` (new; Covers: `Analysis/SoftProof.cs`, `Runs/ProofRun.cs`,
  `Report/ProofTexts.cs`, `ViewModels/SoftProofTab.cs`, the two views): the chain, the table,
  the display step, the warnings, the probe's figures; a row in `docs/design/README.md`;
  `window.md` (the eighth tab's row, the window), `pipeline.md` (the third run), `thresholds.md`
  (three rows), `measurements.md` (a pointer), `testing.md` (the new test file and the fixture's
  B2A), `code-map.md` (the new files), `extending.md` unchanged (the tab checklist already
  fits).
- `docs/validation.md`: V-015 as unmeasured, then measured with the date; `docs/validation.json`
  the case.
- `README.md` ("The window" table: the `Soft proof` row; a section "Soft proofing" after the
  curves one: the intents, the two boxes, the margin, what it is not; the Unicolour row of
  the dependency table names the proof), `THIRD-PARTY-NOTICES.md` and `ThirdParty.cs`
  (Unicolour's uses: the Dmax, the gamut figure, the soft proof).
- `CHANGELOG.md`: one `Added` line.
- `docs/plans/2026-10-30-soft-proofing-tab.md`: this plan.

## Order and verification

1. Worktree `git worktree add .claude/worktrees/Add-a-soft-proofing-tab -b
   claude/Add-a-soft-proofing-tab main`; copy the large test data (the plate-solved FITS and
   the two profiles) into its `AstroPhotoAnalyzer.Tests/testdata/large/` for V-015; save the
   plan under `docs/plans/`.
2. Requirements R-158 to R-161 as `draft`; the ADR as `proposed` (the owner's approval of this
   plan is the yes: it becomes `accepted` in the same task, per `/new-feature` step 3).
3. The fixture's B2A tables and the core tests, failing (paste the output).
4. `Analysis/SoftProof.cs`, `Thresholds.cs`, `Report/ProofTexts.cs`, `Runs/ProofRun.cs`; the
   core tests green; `scripts/verify.ps1 -SkipTests` green along the way.
5. The tab view-model and its tests, failing then green; the view, the window, `MainWindow`,
   `MainViewModel`; the smoke, automation-tree and English tests.
6. `APA_DUMP_SCREENS` renders of the tab (with a profile, without, with each warning) and the
   window, looked at; then the real window on the plate-solved FITS with the baryta profile,
   driven by hand only when the owner is not using the PC (the memory rule), screenshots via
   PrintWindow.
7. `/validate` (every case: `Analysis/` and `Thresholds.cs` changed, so the drift table is
   pasted; the other figures must not move since no existing formula is touched), V-015
   measured and recorded; the screen-gamut threshold confirmed or adjusted on the reference
   image with its comment.
8. Docs, README, CHANGELOG, notices; `scripts/check-design.ps1 -Since main`; the full gate;
   `/wrap-up`, the merge proposed (a subagent review of the branch first, as the owner
   usually asks).
