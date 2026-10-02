# 2026-10-02 Detect the dust and the sky from a plate-solved FITS and a dark-nebula catalogue

Status: **approved** by the owner on 2026-10-02 ("Merg pe recomandarile tale. Da-i drumul"): the four
open questions below are answered with the proposed option each (15 cloud rows in the report, the
darkest 15 labelled on the map with every cloud in the check-box list, `--wcs` kept, the
plate-solved FITS copied into `testdata/large/` as V-004). ADR 0025 holds the measurements and
the decisions; the owner agreed on 2026-10-02 to its points 1 to 4 and asked for the map window,
the per-row preview window and the definitions under the table (points 3, 4 and 6).

## Goal

Replace the print-background verdict (R-041, R-043, R-092) with the one the owner asked for:
the lowest lift, or the lowest usable level downwards, at which the dust lands on paper black
without going under it, where the dust is found by the tool itself from the image's WCS and an
embedded catalogue of dark nebulae, and the sky is what lies outside those clouds. Name the
clouds in the report and the window; show them on the image; show every simulation row on the
image. Keep the 1 % rule only as the named fallback for a file without a WCS.

## Read first

1. ADR 0025 (proposed), whole: the figures below come from it.
2. `docs/design/measurements.md` (the tonal analyzer and the simulator), `input.md` (the FITS
   reader, the raster, `SourceInfo`), `recommendations-and-report.md` (Anexa B, the background
   rule), `window.md` (the Tonuri tab, `Rows.cs`, the public view-model rule), `extending.md`,
   `dependencies.md` and `testing.md`.
3. The parked branch's `DustAnalyzer.cs`, `PrintStretchSimulator.cs`, `Recommendations.cs`
   (Background), `ReportWriter.cs` (Anexa B) and `DustSimulationTests.cs`, in the worktree
   `.claude/worktrees/Recommend-the-lift-that-puts-the-dust-on-paper-black/`: read for the patch
   code, the grid, the bisection and the `Dmax de la care trece` figure; rewrite on `main`, no
   merge, no cherry-pick (the owner's instruction).
4. `ThirdParty.cs`, `THIRD-PARTY-NOTICES.md`, `ThirdPartyTests.cs`: the notice contract to
   extend to a data component.

## Measured before the design (2026-10-02)

On `starless_rbf0.5_stretch0.17.fit` (5837 x 3867, background 0.1716, baryta): 406 LDN clouds
in the frame, 116 with 9 or more patches; the dust (P10 of all catalogued-cloud patches) at
L\* 7.63, the sky (median outside the discs) at 18.08, 3.9 % of the patches pure black; exact
target 0.187, grid row 0.19 (dust 9.3, sky 20.3); the darkest clouds LDN 42 (B78) 0.9, 1769 3.1,
1793 4.7, 572 4.7, 559 4.8, 34 5.1, 621 5.7. SIP moves a patch centre by at most 6 px: ignored.
Dmax 1.9 / 2.0 / 2.1 gives 0.208 / 0.187 / 0.166. The disc radius x0.7 / x1.4 gives 0.197 /
0.177. The full tables are in ADR 0025.

## Decisions (ADR 0025)

1. Catalogue LDN `VII/7A` embedded as a text resource with the original B1950 data, precessed in
   the core; Barnard numbers from its cross-identification column; notices in three places.
2. A cloud is a disc of its catalogue area; a patch belongs when its centre is inside.
3. Dust = P10 of all catalogued-cloud patches together; each cloud with 9+ patches a row with its
   L\* and its own exact target; sky = median of the patches outside the discs; the lowest grid
   row (0.10..0.20, as-is included) with the dust on or above paper black is recommended, up or
   down; exact target by bisection; the Dmax from which the as-is image passes.
4. Without a WCS or without a cloud in the frame: the 1 % rule and the frame median, named as
   such in the report and the window; a red sentence in the window when there is no WCS;
   `--wcs <fits>` lends a FITS's WCS to a TIFF/PNG of the same size.
5. The `Because` names the Dmax; Anexa B gives the exact target at Dmax -0.1 and +0.1 too.
6. Two visualisation windows, maximised, computing nothing: the map (dust, sky, clouds with
   labels, check boxes) and the per-row preview (the lifted image with the pixels under paper
   black and under L\* 15 coloured, check boxes).

## Requirements (step 1 writes these into `docs/requirements.md`, status `draft`)

Rewritten rows:

- **R-041** Tones in L\*, on the stride-4 subsample, through the Rec. 709 luminance of the
  linearised channels: the median L\*, paper black from the Dmax in use, the shares below paper
  black, below L\* `LowSeparationLstar` (15) and above L\* `HighlightLstar` (50), and the 101-bin
  histogram. Facts of the image; the verdict is R-044's. (The 2 % / 20 % sentence goes.)
- **R-043** The simulation: one row per target of the grid `Parameters.SimulationTargets` (0.10
  to 0.20 in steps of 0.01), plus the explicit target of R-023 when it is off the grid, plus the
  as-is row at the measured level (gamma 1), sorted by target. Each row: target, gamma, the dust
  (R-044), the sky (R-044), the share of patches under paper black (`negru pur`), the shares
  under L\* 15 and over L\* 50, `print-safe` when the dust is at or above paper black. The
  recommended row is the lowest print-safe row, the as-is row included, below or above the
  measured level; none when no row is. With it: the exact target by bisection between
  `ExactTargetLo` (0.05) and `ExactTargetHi` (0.60), the exact targets at Dmax minus and plus
  `DmaxSensitivityStep` (0.1), and the lowest Dmax at which the as-is image passes. Example: the
  plate-solved FITS at 0.1716: as-is dust 7.6, sky 18.1, 3.9 % pure black, not safe; 0.19: dust
  9.3, sky 20.3, 2.2 %, safe and recommended; exact 0.187; at Dmax 1.9 / 2.1: 0.208 / 0.166.
- **R-092** Background: Problem when the as-is sky is under paper black (`Cerul e sub negrul
  hartiei ...`), Attention when only the dust is (`Praful e sub negrul hartiei ...`), both with
  `Re-stretch din liniar la fundal <row> (simulat: praful la L* a, cerul la L* b; exact pe negru
  la <exact>)` or the sentence that no target up to 0.20 reaches it; Ok when the as-is dust is on
  or above paper black, with `poti cobori pana la <row>` when the recommended row is lower. The
  `Because` names the dust's definition, the darkest clouds by name with their L\* (up to
  `DustCloudReportRows`), the sky, the level, the Dmax, and the Dmax from which it passes as is.
- **R-113** Tonuri tab: as today, plus: the simulation table as rows with a `Preview` button on
  each, the as-is row included (R-131); under the table the sentence that defines the dust and
  the sky in use (R-044), the red sentence when the file carries no WCS, and the button `Harta
  praf / cer` (R-130).
- **R-122** Every package and every embedded data set the core ships is claimed by exactly one
  entry of `ThirdParty.Components` (a data entry names its resource instead of packages) and
  has a heading in `THIRD-PARTY-NOTICES.md`; the README's table is kept by hand.

New rows:

- **R-018** WCS from the FITS header: `CTYPE1`/`CTYPE2` starting `RA---TAN` / `DEC--TAN` (a
  `-SIP` suffix accepted, the SIP terms ignored), `CRPIX`, `CRVAL`, and the linear part as
  `CD1_1..CD2_2`, or `PC1_1..PC2_2` with `CDELT1/2`, or `CDELT1/2` with `CROTA2`; anything else
  means no WCS, never an error. Pixel to sky is the TAN deprojection in FITS convention (1-based,
  row 1 at the bottom), applied before the `ROWORDER` flip; the result travels on
  `SourceInfo.Wcs`. Example: the plate-solved FITS puts its centre at RA 271.59, Dec -10.83 and
  Antares inside the frame; a header without `CTYPE` gives null.
- **R-019** The catalogue: 1791 LDN rows embedded in the core (number, B1950 centre, area,
  opacity, Barnard numbers), precessed to J2000 on load; LDN 42 lands within 1' of RA 263.27,
  Dec -25.70; the Barnard column of LDN 42 reads `78`.
- **R-027** `--wcs <file.fit>` (the window: the field `WCS din FITS` under the image path): the
  header of that FITS lends its WCS to a TIFF/PNG of the same width and height; a different size
  or a file without a WCS is an error naming the flag, exit `1`; on a FITS input the flag
  overrides the file's own.
- **R-044** The dust and the sky, on 32 x 32 image-pixel patches of the stride-4 subsample (the
  L\* of each patch's mean linear luminance, partial edge patches dropped). With a WCS and at
  least one catalogued cloud: a patch is a cloud's when its centre is within the disc of the
  cloud's area; the dust is the `DustCloudFraction` (10 %) percentile of all cloud patches
  together; the sky is the median of the patches outside every disc; every cloud with at least
  `DustCloudMinPatches` (9) patches is a row (name `LDN n`, with `(B m)` when it has Barnard
  numbers; patches; L\* P10; its exact target). Otherwise the dust is the `DustAreaFraction`
  (1 %) percentile of all patches, the sky the median of all, and the method is named `fara WCS`
  or `fara nori catalogati in cadru`. Example: the plate-solved FITS: 406 clouds, 116 rows, dust
  7.6, sky 18.1; LDN 42 (B78) 12 patches at 0.9, exact 0.392.
- **R-045** The per-pixel classes of a lifted image, for the preview window (R-131): on a
  downsample of at most `PreviewMaxSide` pixels, lifted with the row's gamma, each pixel is
  `under paper black`, `under L* 15` or neither; the core returns the image and the classes,
  the window paints them.
- **R-130** The map window: opened from the Tonuri tab, maximised, the image fitted to the
  window, over it the dust patches in one colour and the sky patches in another, the clouds of
  R-044's rows as circles with their names; check boxes for the dust layer, the sky layer, the
  labels (disabled without a WCS) and each cloud, all on by default; nothing computed.
- **R-131** The preview window: opened from a simulation row, maximised, the image lifted to
  that row's target (the as-is row at gamma 1), the pixels under paper black and under L\* 15
  coloured apart, each layer behind a check box, on by default; the title names the target,
  the dust and the sky of the row.

## Steps

1. **Requirements** (`docs/requirements.md`): the rows above, status `draft`; R-023 loses the
   "0.15 and 0.20" sentence (the grid is `SimulationTargets`).
2. **Acceptance tests, failing first**: `WcsTests` (R-018: CD, PC+CDELT, CDELT+CROTA, SIP suffix,
   missing keywords; the plate-solved header's centre and corners, typed in as a fixture;
   `FitsTestData` grows a header with a WCS), `DarkNebulaCatalogueTests` (R-019),
   `OptionsTests`/`CliTests` (R-027), `DustAnalyzerTests` (R-044: a synthetic raster with a dark
   disc under a synthetic WCS centred on LDN 42; the fallback without WCS; no cloud in a frame
   pointed at the galactic pole), `PrintStretchSimulatorTests` (R-043), `PrintPreviewTests`
   (R-045), `RecommendationsTests` (R-092), `GoldenOutputTests` (a scene with a WCS: a FITS
   fixture written by `FitsTestData`; the existing scenes show the fallback sentence),
   `ThirdPartyTests` (R-122), `ViewModelTests` (R-113, R-130, R-131 view-models),
   `WindowSmokeTests` (the two windows' content rendered). `RepoInvariantTests` ties the names.
3. **Core, Input**: `Wcs.cs` (record + `TryFrom(header, w, h)` + `PixelToSky` + `SkyToPixel` in
   raster coordinates), `FitsReader` keeps the header's WCS on `SourceInfo`, `ReadHeader(path)`
   for `--wcs`; `DarkNebulaCatalogue.cs` + `Input/ldn.txt` (resource; a script in the ADR's
   scratch is not kept: the file is written once from VizieR's `VII/7A/ldn` and its origin stated
   in its first line); `Astrometry.cs` (B1950 to J2000, equatorial to galactic, angular
   distance).
4. **Core, Analysis**: `DustAnalyzer.cs` (patches, masks, rows, method), `PrintStretchSimulator`
   rewritten (grid, dust/sky per row, recommendation up or down, bisection, Dmax figures),
   `PrintPreview.cs` (R-045). `Thresholds`: the new constants with their comments;
   `PaperBlackOkPct`/`WarnPct` removed. `Parameters`: `SimulationTargets` = 0.10..0.20,
   `TargetBackground` nullable (only adds its row and drives the Preview tile), `WcsPath`.
5. **Core, Report**: Anexa B (table with `L* praf` before `L* cer`, the definition line, the
   exact/Dmax line, the cloud table up to `DustCloudReportRows`), the Background rule, `Fmt.Dmax`.
   `AnalysisRun`: the dust after the tones, the simulation after the dust; a progress stage.
6. **Console**: `--wcs`, `--help`, `AnalyzerCli.ParametersFrom`; goldens re-approved after the
   owner reads the diff.
7. **Window**: `ParameterFields.WcsPath`; `TonesTabViewModel`: `SimulationRows` become
   `SimulationRowViewModel`s with a command; `Definition`, `NoWcsWarning`, `OpenMap` command;
   `MapWindow` + `MapViewModel` (layers from `DustResult`: patch classes as an `int[]` grid,
   circles as `(x, y, r, name)`), `PreviewWindow` + `PreviewWindowViewModel` (from
   `PrintPreview`); `RasterBitmaps` gains an overlay painter; `WindowSmokeTests` renders both.
8. **Notices**: `THIRD-PARTY-NOTICES.md` section "Lynds' Catalogue of Dark Nebulae",
   `ThirdParty.cs` entry, README table row.
9. **Validation**: copy `starless_rbf0.5_stretch0.17.fit` into `testdata/large/`, add it to
   `manifest.json` (`scripts/check-testdata.ps1 -Update`), V-004 in `validation.json` (its dust,
   sky, clouds, recommendation, exact target), re-measure V-001 to V-003 (no WCS: the fallback;
   `simulation.recommendedTarget` and the verdict will move), `docs/validation.md` History.
10. **Docs**: README (Tonuri, the two windows, `--wcs`, the catalogue in the components table),
    CHANGELOG (Changed, Added), design notes `input.md`, `measurements.md`, `thresholds.md`,
    `recommendations-and-report.md`, `console.md`, `window.md`, `dependencies.md`,
    `code-map.md`, `known-limitations.md` (the gamma curve against VeraLux; the disc), ADR 0025
    to `accepted`; `/wrap-up`.

## Files touched

Core: `Input/Wcs.cs`, `Input/Astrometry.cs`, `Input/DarkNebulaCatalogue.cs`, `Input/ldn.txt`,
`Input/FitsReader.cs`, `Input/Raster.cs` (`SourceInfo.Wcs`), `Analysis/DustAnalyzer.cs`,
`Analysis/PrintStretchSimulator.cs`, `Analysis/PrintPreview.cs`, `Analysis/TonalAnalyzer.cs`,
`Report/ReportWriter.cs`, `Report/Recommendations.cs`, `Report/Fmt.cs`, `Runs/AnalysisRun.cs`,
`Parameters.cs`, `Thresholds.cs`, `ThirdParty.cs`, the csproj (the resource). Console:
`Options.cs`, `AnalyzerCli.cs`. Window: `ViewModels/ParameterFields.cs`, `ResultTabs.cs`,
`Rows.cs`, `MapViewModel.cs`, `PreviewWindowViewModel.cs`, `Views/TonesView.xaml`,
`Views/MapWindow.xaml(.cs)`, `Views/PreviewWindow.xaml(.cs)`, `MainWindow.xaml`,
`Presentation/RasterBitmaps.cs`. Tests: the ones in step 2 and the goldens. Docs: step 10.

## Open questions put to the owner on 2026-10-02 (answered: the proposed option each time)

1. The report's cloud table: all rows with 9+ patches (116 on the measured frame) or the
   darkest `DustCloudReportRows` (proposed 15)? The window lists all.
2. The map's labels: all 116 clouds labelled by default, or the darkest 15 labelled and the
   rest circled only, with every cloud in the check-box list? Proposed: the latter.
3. `--wcs` as proposed under decision 4: yes or no.
4. The plate-solved FITS (270 MB) copied into `testdata/large/` as the second validation image:
   yes or no. (The reference PNG stays, measured by the fallback rule; no FITS of its stretch
   exists.)

## What deviated

- The first measurement pass mapped the catalogue onto `de_printat.png` through the WCS of a
  linear stack of the same resolution; the owner said it was a different crop, and every figure
  was re-measured on the plate-solved FITS alone (ADR 0025 records only those).
- The exact target is interpolated on a ladder of lifts 0.01 apart (`ExactTargetStep`) instead
  of a bisection per statistic: the per-cloud targets (114 clouds on the reference FITS) would
  have cost thousands of lifts; R-043 says so. `RasterOps.Gamma` went through a 16-bit lookup
  table with interpolation for the same reason.
- The sky is the median outside the discs without a galactic-latitude cut (ADR 0025, point 3):
  on the plate-solved FITS the cut moved the sky by half an L\* and added a threshold with no
  natural value; the plan's first draft had the cut.
- The core finds 397 clouds in the frame of the plate-solved FITS where astropy found 406, and
  the dust at L\* 7.89 against 7.63: astropy honoured the SIP terms, the core ignores them (ADR
  0025), and a few edge patches change cloud; recorded in `validation.md`.
- Barnard designations may carry a letter (`119A`), and VizieR's column is fixed-width (`8A4`),
  so the resource was regenerated from 4-character fields and the ids are strings.
- The map and preview windows take their layers from `DustAnalyzer.Classes` and `PrintPreview`
  (R-045), two small additions to the core, so that the windows paint and never decide.
- After testing the window on the plate-solved FITS, the owner rejected the catalogue's discs
  as the dust and the outside-disc median as the sky on the map (the Rift uncovered, the edges
  unpainted), and chose the relative classes of ADR 0026, with the dust's L\* at the 25 %
  percentile of the dust class; decision 3 of ADR 0025 and the map's layers are superseded.
- At the end of the task the freshly built `AstroPhotoAnalyzer.Wpf.Tests.dll` could not be read
  by any process on the owner's machine (no handle held, ACL intact; the machine runs three
  third-party antivirus products beside Defender), so the window tests and the gate were left
  for the owner to run; the core and console tests (275, goldens included) and the four
  validation cases ran green.
