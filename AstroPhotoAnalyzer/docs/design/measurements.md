# Measurements: the analyzers

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §5 and the notes of
> phases 2, 3 and 4 of `docs/implementation-plan.md` (the note on the golden scenes went to
> `console.md`). The behaviour is stated with IDs in `../requirements.md` (R-030 to R-089);
> the decisions are ADR 0014 to ADR 0019. What the reference image taught the analyzers is in
> `../validation.md`, "What the real image taught the tool". **Covers:**
> `AstroPhotoAnalyzer.Core/Analysis/`. A change to the code this describes means this note is
> updated in the same task, or deleted with the decision it explains.

## Measurements

Each analyzer is a pure function of a `Raster` (and sometimes `Parameters` or an
earlier result) returning a record. Nothing formats, nothing decides beyond the boolean
flags listed. All thresholds are in `Thresholds.cs` (`thresholds.md`) with a comment saying where
they came from.

### A1 - `FileFacts`

Copies `SourceInfo` and the dimensions into `FileFactsResult`: `W`, `H`, `Megapixels`,
`NativeAspect`, `IsColor`, `Format`, `BitDepth`, `IsFloat`, `Compression`, `HasIcc`,
`IccDescription`, `FileBytes`, `ReaderNote`, and `IccLooksSrgb` (null without a profile;
otherwise whether the description contains "srgb", case-insensitive).

### A2 - `BitDepthAnalyzer` - genuine 16 bits or an 8-bit upscale

Applicable only when the source is 16-bit integer. Over **all** pixels of every plane:
distinct 16-bit levels per plane (`DistinctLevels`), and the fraction of samples with
`v == (v >> 8) * 257` (`UpscaledFraction`) - an 8-bit file saved as 16 has every sample
of that form; genuine data has ~0.4 % by coincidence. `IsUpscaled` when the fraction
exceeds `UpscaledFraction` (0.5).

### A3 - `CropAnalyzer` - what a target aspect cuts

Given `W`, `H` and `TargetAspect`: no crop when null or equal to native (1e-6). If the
target is narrower, the new width is `round(H x target)`, cut equally from both sides;
otherwise the height. Result: the centred rectangle (`X0 Y0 W H`), `Axis` ("latime" /
"inaltime"), `CutPerSidePx`, `CutPct`. The reference image at 50:35: 5938 x 3888 ->
5554 x 3888, 192 px per side, 6.5 %.

### B1/B2 - `TonalAnalyzer` - where the tones sit, and how many fall under paper black

B1 in **levels** (0..255), on all pixels: `MeanLevels` per plane; `PctBelowDarkA` /
`PctBelowDarkB` = share of samples (all planes) below 32/255 and 64/255;
`ReferencePercentiles` at ranks 0.1, 1, 50, 90, 99, 99.9, 100 of the reference plane at
stride `PercentileStride` (2); `BackgroundMedian` = median of the reference plane at
stride `TonalStride` (4) - the "background" the simulation lifts (correct whenever the
sky dominates the frame, i.e. any astrophoto).

B2 in **L\***, on the stride-4 subsample, through `RasterOps.LstarPlane` (Rec. 709
luminance of the linearised channels): `LstarMedian`, `PaperBlackLstar` (from
`Parameters.Dmax`), `PctBelowPaperBlack`, `PctBelowLowSeparation` (< 15),
`PctAboveHighlight` (> 50), `LstarHistogramPct` (101 bins, 0..100, percent).

These are facts of the image, not the verdict (since ADR 0025 the two pixel-share flags
are gone; the verdict is B5's).

`LstarStats(raster, dmax, stride)` is public for the simulator.

`LstarLadder(parameters, simulation)` is a reference table, not a measurement (window only, like
`DpiLadder`): one `LstarLadderRow(Lstar, GrayLevel, Meaning, Chosen)` per reference of
the tab - the paper black of each of the three media (`Chosen` on the chosen medium's when the Dmax
in use is that medium's; an explicit Dmax, `Parameters.DmaxIsExplicit`, adds its own chosen
row instead, even when another medium has that Dmax, labelled `Dmax explicit` or, read from a
profile, `Dmax din profilul ICC`; R-042), the
levels `DarkLevelA`/`DarkLevelB`, the L\* thresholds `LowSeparationLstar`/`HighlightLstar`,
and the simulation's primary row (`fundalul-tinta dat` when the owner gave one, `fundalul
recomandat` otherwise, no row when the as-is row is primary) - sorted by L\*. The level is
that of a grey with the same L\*; on colour the same level gives a different L\* by hue,
which the window says under the table.

### B5 - `DustAnalyzer` - where the dust and the sky sit (ADR 0025, ADR 0026, ADR 0033)

The dust is the darkest *extended* structure, measured on patches of `DustPatchPx` (32) x 32
image pixels on the stride-4 subsample (8 x 8 samples, partial edge patches dropped), each
patch's value the L\* of its **mean linear luminance**, so that noise and isolated pixels
cannot move it (`PatchLstars(sub)`). Three parts:

- `DustMap` (`Build(w, h, wcs, asIsPatchLstars, catalogue)`) is the geometry and the classes,
  built once per image on its as-is patch L\*s: the patch grid (`Cols`, `Rows`, `Side`,
  `Stride`, `PatchCentre`); with a `Wcs`, the galactic latitude of every patch centre
  (`Astrometry.GalacticLatitude` on `Wcs.PixelToSky`), the catalogued clouds whose discs hold
  patches (`FindClouds`: `Wcs.SkyToPixel` for the centre, the radius in pixels from
  `ScaleDegPerPx`, the patches within the angular radius by dot product, tested inside the
  disc's bounding box), and the five classes (`Classify`): the band is |b| <
  `BandGalacticLatitudeDeg`; `SkyModel(L, band, cols, rows, percentile)` is the given
  percentile (the median here) of the outside-band patches per cell of `SkyModelCellPatches`,
  empty cells from the nearest (all patches together when no cell has enough), bilinear to
  patch resolution, then `Smooth`; `BandLevel` is `MedianFilter` over `BandLocalWindowPatches`
  then `Smooth`; a patch is `Dust` when `DustDepthLstar` under the local level in the band or
  under the sky model anywhere, `BrightCloud` when in the band and `BrightCloudLstar` over the
  local level, `Peak` when over the `PeakFraction` percentile, `Band` otherwise in the band (or
  outside it when `BrightCloudLstar` over the sky model), `Sky` otherwise. `Method` is `Band`.
  Without a WCS, or with the band under `BandMinFraction` of the patches (`SkyReason.NoBand`)
  or the outside sky under it (`AllBand`), `ClassifyBySky` makes the classes instead (ADR
  0033, `Method` `Sky`): the sky model from all patches at `SkyModelLowPercentile` per cell;
  the residual patch minus model, with `Stats.RobustSigma` (median and 1.4826 x MAD), one
  clipping pass at `SkyNoiseClipK` sigmas and the two taken again on what is kept; `Dust`
  under the median by `SkyNoiseK` sigmas, `Band` (nebula) over it by as much, `BrightCloud`
  (bright nebula) over `Stats.OtsuThreshold` of the nebula residuals (none under two), `Peak`
  as above, `Sky` the rest. The map keeps `SkySigma`, `NebulaThreshold`, `BrightThreshold` and
  `DustThreshold` (L\* over or under the model) for the legend and Anexa B; `BandLevel` is
  null. The five `PatchClass` values are one enum on both paths and `ClassNames(Method)` gives
  their Romanian names (`banda` / `nori luminosi` or `nebuloasa` / `nebuloasa stralucitoare`).
- `Measure(patchLstars, map, paperBlack)` gives the `DustStats` of a (lifted) subsample on one
  path for both: `LstarSky` is the median of the sky patches (of all patches when the class is
  empty); `LstarDust` is the darkest extended area, the lower of the `DustClassFraction` (25 %)
  percentile of the dust patches, counted only when the class holds at least
  `DustClassMinFraction` (5 %) of the patches, and the `SkyDarkestFraction` (1 %) percentile of
  the sky patches; `DustSource` says which won and `DustPatchCount` is the dust class's size.
  Always `PctPatchesBelowPaperBlack` ("negru pur") and `CloudLstars`, the `DustCloudFraction`
  (10 %) percentile of each cloud's own patches, for the rows. The map window paints
  `map.Classes` as they are.

The first form (ADR 0025: every cloud-disc patch as dust, every outside patch as sky; the
owner rejected it on the map on 2026-10-02) and the second (the patches at or under the two
levels) are gone; what the Great Rift is on the plate-solved FITS, and what the plain edges
are, is now what the classes say.

### B4 - `PrintStretchSimulator` - the lowest level that puts the dust on paper black

Builds the `DustMap`, measures the as-is row (B5 on the subsample; the pixel shares of B2),
then, when the background is strictly inside (0, 1), lifts the subsample once per step of a
**ladder**: every `ExactTargetStep` (0.01) from `ExactTargetLo` (0.05) to `ExactTargetHi`
(0.60), plus the grid `SimulationTargets` (0.10..0.20) and the explicit `TargetBackground`;
gamma = `ln(target) / ln(BackgroundMedian)` on every channel (`RasterOps.Gamma`, a 16-bit
lookup table with linear interpolation, exact on 16-bit sources). The grid steps become
`SimulationRow`s (`Target`, `AsIs`, `Explicit`, `Gamma`, `LstarDust`, `LstarSky`,
`PctPatchesBelowPaperBlack`, `PctBelowLowSeparation`, `PctAboveHighlight`, `DustOnPaperBlack`,
`SkyVisible` = the sky, rounded to the one decimal the table prints, at or above
`LowSeparationLstar`, 15, and `PrintSafe` = both), sorted by
target with the as-is row among them; every step feeds the ladder of dust values, of sky
values and of per-cloud values. `Recommended` is the first print-safe row (ADR 0029), the as-is row included and whether below or above the measured level;
`VisibilityTarget` is where the ladder of sky values crosses the threshold. `Crossing`
reads the ladder: the first step at or above a level, interpolated linearly with the step
before (the first step when it already passes, null when the last does not), which gives
`ExactTarget` at the Dmax in use, `ExactTargetDmaxLower`/`Higher` at Dmax -/+
`DmaxSensitivityStep` (0.1), and each `CloudRow`'s own `ExactTarget`. `CloudRow`s exist for
the clouds with at least `DustCloudMinPatches` (9) patches, darkest first. `DmaxToPassAsIs`
is the Dmax whose paper black equals the as-is dust's L\* (NaN at L\* 0). `Primary` = the
explicit row, else the recommended one, else the as-is row. `DustDescription`,
`SkyDescription` and `MethodNote` are the words the report and the window use for the method.

Known limits: the median is not the sky when the sky does not dominate the frame; the gamma
curve keeps the shadows higher than a real stretch to the same background (the owner's VeraLux
stretch to 0.17 measured the dust at L\* 7.6, under paper black, where the curve from 0.103
predicted it above from 0.14); a disc is not the cloud's contour.

### B6 - `PrintPreview` - a simulation row as an image (window only)

`Render(img, gamma, dmax)`: the image subsampled under `PreviewMaxSide` (1600), lifted with
the row's gamma (left alone at gamma 1), and one `PreviewClass` per pixel from
`RasterOps.LstarPlane`: `UnderPaperBlack`, `UnderLowSeparation` (under 15 but not under paper
black) or `None`, with the two shares. The preview window paints the classes; it decides
nothing.

### B3 - `NoiseAnalyzer` - residual noise in the sky, in 8-bit LSB

`NoiseTileCandidates` (400) random 64 x 64 positions from `SeededRandom(Parameters.Seed)`
on the reference plane; a tile is smooth when `max - min <= NoiseTileMaxRangeLevels`
(6000/257 = 23.3 levels - the acceptance script's wide criterion). Per smooth tile, sigma
= population std of horizontal neighbour differences / sqrt 2, x 255. Result: `Tiles`
(x, y, sigma), `SmoothTiles`, `SigmaMedianLsb`, `Sigma10thLsb`, `SigmaMinLsb`,
`PctTilesBelowBanding` (< 0.5), `PctTilesBelowOne`.

Flags: `Insufficient` (< `NoiseMinTiles` = 10 smooth tiles; sigmas NaN); `BandingRisk`
(median < 0.5); `GoodForPrint` (0.8..1.5); `UnderDenoised` (> 2.0). Rasters smaller than
a tile have zero candidates.

### E1/E2 - `BackgroundAnalyzer` - sky colour, and edge regions against it

On the stride-3 subsample. Luminance = mean of the planes. **Background** = pixels whose
luminance is `<=` the 25th percentile (computed over the whole subsample), excluding the
bottom 25 % of rows (the horizon). `Levels` = per-plane medians x 255. On colour:
`GreenExcess` = G - (R+B)/2, `RedMinusBlue`; `HasCast` when either exceeds
`CastWarnLevels` (1.0). `<=` and not `<` because a flat synthetic sky puts every pixel
exactly at the percentile.

**Regions**: four corners (15 % of height x 20 % of width) and four edges (the same bands
between the corners). Each `EdgeRegion` has per-plane medians (`Levels`), `Deltas` =
region - background per plane, `MaxAbsDelta`, `Flagged` (> `CornerGradientLevels` = 3),
and `Coloured`: spread (max delta - min delta) > 3 **and** > `CornerColourSpreadRatio`
(0.5) x mean |delta| - the channels move differently. The distinction came from the
reference image: the horizon corner is +9.4 / +1.7 / -9.9 (coloured, light pollution);
the corner the Milky Way enters is +11 / +10 / +16 (neutral, the object). On mono there
is one plane and `Coloured` is always false.

### D1/D2 - `StarAnalyzer` - the real resolution

Candidate = pixel of the reference plane above `mean + StarCandidateAboveMean16/65535`
(3000/65535 = 0.046 above the plane mean), not above `StarMaxPeak16/65535` (saturated),
and the maximum (ties allowed) of its 25 x 25 window (`StarWindow`). Base = median of the
window's 96 border pixels; amplitude = peak - base >= `StarMinAmplitude16/65535`. Width
at half maximum is measured on the horizontal and vertical lines through the peak:
descend from the centre until below `base + amplitude/2`, interpolate the crossing
linearly (the acceptance script's function, epsilon included), keep the star when both
widths are in (`StarFwhmMin` 0.8, `StarFwhmMax` 15) and record their mean.

Result: `Stars` (x, y, fwhm, peak), `FwhmMedian` / `Q1` / `Q3` (NaN below
`StarMinCount` = 20), `Regions` (centre = central 40 % x 40 %, four corners 20 % x 20 %;
median per region when >= `RegionMinStars` = 5), `CornerToCenterRatio` = widest known
corner / centre, `SoftCorners` when > `CornerSoftRatio` (1.4), `EffectiveDetailPx(w)` =
w / FwhmMedian.

Known: the estimator overestimates a sampled Gaussian by ~2.5 % (linear interpolation on
a convex curve); the candidate threshold is `mean + minimum amplitude`, not the script's
`3 x mean`, because at a 0.25 background the latter rejected every star (the print
version of a real image). On a nebulous core stars measure wider (bright, structured
base) - see the acceptance record.

### D3 - `PrintSizeAnalyzer` - the size table

For each candidate width W (cm), with pixel size (Wpx, Hpx) = the crop rectangle:
height = W x Hpx / Wpx; dpi = Wpx / (W / 2.54); pitch = 25.4 / dpi mm; `StarMm` = FWHM x
pitch; `MinDistanceM` = StarMm / `AcuityMmAt1m` (0.29) - the distance from which the
star is a point; `NaturalDistanceM` = `ViewingDistanceFactor` (1.5) x the print's
diagonal; `ViewingDistanceM` = the parameter or the natural distance;
`PointAtViewingDistance` = MinDistance <= ViewingDistance when the distance is explicit,
MinDistance <= NaturalDistance x `NaturalDistanceTolerance` (1.15) when it is not; without
FWHM, dpi >= `FallbackDpi`, which is 25.4 / (0.29 x d) with an explicit distance - the
pixel grid invisible from d - and `FallbackMinDpi` = 180 without one (the lab convention:
0.14 mm pitch, invisible from ~0.5 m); `PixelDistanceM` = pitch / 0.29 on every row;
outer size = image + 2 x (passepartout +
frame); `Oversize` = W > `OversizeCm` (100). **`ReferenceRow`** = the largest row that
passes and is not oversize; if none passes, the smallest - it always exists (given one
candidate). **`CanRecommend`** = the distance is explicit **and** FWHM is known - the
only case where the rows separate on a measurement; only then is the reference row
`Recommended` and `RecommendedRow` non-null. `NaturalRatio` = MinDistance / NaturalDistance, the same on
every row (NaN without FWHM). `EffectiveDetailPx` = Wpx / FWHM. `ScreenEquivalentWidthCm`
= Wpx x `ScreenPixelMm` / 10 - how wide the crop would print at a monitor's pixel pitch,
i.e. what a 100 % view on screen shows; `ScreenStepBackFactor` = that / the reference
row's width (NaN without one): view the 100 % crop from this many times the monitor
distance to see the reference print. Window only (the Preview tab's step-back rule).

**The natural distance cannot pick a size.** MinDistance and NaturalDistance both scale
with the width; their ratio is `fwhm x 1000 / (Wpx x 0.29 x 1.5 x sqrt(1 + (H/W)^2))`, a
constant of the image (1.018 on the reference image) - `NaturalRatio`. So without an
explicit distance the test answers "is the image sharp enough for its natural viewing
distance" - the same answer at every size - and **no row is recommended**: a star would
have landed on the largest candidate under the limit, i.e. on `--sizes`, not on the
image. The reference row is what the recommendation text quotes ("up to X cm") and where
the plateau (C1) is judged; the size table replaces the `*` legend with two lines saying
why there is no recommended row, with the ratio. The report asks for a distance if the
print will be looked at from closer. The tolerance absorbs the FWHM estimator's spread;
before it, the reference image failed every row by 2 % and the reference row fell to 50
cm. With an explicit distance the rows genuinely separate and the recommendation rests
on them.

**Without stars nothing about detail is measured.** The dpi floor is a fact about the
file - enough pixels for the grid to vanish from the viewing distance - not about the
image; a soft nebula may support far less. So no row is recommended there either: the
recommendation states the rule (the physical floor from the given distance, else the
180 dpi convention) and how far the file reaches, or that even the smallest width misses
the floor; the legend under the table says why there is no star; without a distance it
asks for one so the floor is physical rather than conventional. The Rezolutie tab
carries a static note with both rules.

`DpiLadder(viewingDistance)` is a reference scale, not a measurement: for 300 / 240 /
180 / 100 / 60 dpi (and the given distance's own floor when there is one) the pitch
25.4 / dpi and the distance from which the pixel grid vanishes, pitch / 0.29, each with
what it means (reading, printer native, the no-stars convention, poster, sofa). The window
shows it under the size table so the dpi column reads against physics rather than against
300; the report does not repeat it.

### C1/C2/C4 - `QuantizationAnalyzer`

**C1 plateau**: up to `PlateauTileCount` (120) tiles from `SeededRandom(PlateauSeed = 1)`
within `PlateauMaxAttempts` (5000), accepted when the reference plane's range < 6 levels
and mean < 70 levels (`PlateauTile*` - the strict criterion of the other acceptance
script). On each, the 8-bit quantised values: changes of value per row, averaged;
plateau = 64 / (1 + changes). `PlateauPx` = mean over tiles (NaN without tiles);
`PlateauMmAt(dpi)` = PlateauPx x 25.4 / dpi; `PlateauVisibleAt(dpi)` when >
`PlateauVisibleMm` (1.0).

**C2 rounding** (when the source has more than 8 bits): |v x 255 - round(v x 255)| on all
planes at stride 2 -> `RoundingMeanAbs`, `RoundingMax`, `RoundingP999` (from a 1000-bin
histogram).

**C4 shadow-lift stress**: the reference plane at stride 2, lifted with x^(1/1.9)
(`ShadowLiftGamma`), quantised to 8 bits, once from the float data and once from data
first rounded to 8 bits. Levels used strictly inside (`ShadowLiftLevelLo` 8,
`ShadowLiftLevelHi` 120) and missing between the first and last used:
`LevelsUsedFrom16` / `MissingFrom16` / `LevelsUsedFrom8` / `MissingFrom8`.
`Keep16BitMaster` when MissingFrom8 > `MissingLevelsWarn` (20).

### C3 - `JpegAnalyzer`

The raster quantised to 8 bits (`Rgb24` / `L8`), encoded in memory at each quality in
`JpegQualities` (100, 95, 90) with ImageSharp's encoder, 4:4:4 (`YCbCrRatio444`;
`Luminance` on mono), decoded back and compared **against the unrounded float reference
x 255** - so the error includes the 8-bit rounding, as the acceptance table did. Per
row: `Bytes`, `MeanAbsError`, `MaxError`, `P99`, `P999` (from a 0.01-level histogram, no
sort), `PctOverThreshold` (error > `JpegErrorCountLevels` = 2) and
`MeanLevelOverThreshold` (where those samples sit), `Acceptable` = MaxError <=
`JpegMaxErrorReject` (10). `Tiff8LzwBytes` = the same 8-bit image as TIFF LZW with the
horizontal predictor (without it ImageSharp gave 38.6 MB where ImageMagick gives 20).

Measured and relied upon: mean error is **not** monotone in quality this close to
lossless (integer YCbCr costs 1-2 levels at q100); max error and the count over 2 levels
are, and only those feed a verdict. The numbers are this encoder's, not libjpeg's.

### F1 - `ToneCurve`, `CurveAdvisor` - the suggested curves (ADR 0032)

Not a measurement of the image but a suggestion built on its measurements; computed on request
(`pipeline.md`, "The second run").

**The curve** (`ToneCurve`). Control points from (0, 0) to (1, 1), strictly increasing in x,
and the natural cubic spline through them, solved with the same tridiagonal recurrence as
Siril's `curve_transform.c`, clamped to 0..1: the table of points typed into Siril's Curves
tool in `cubic spline` mode gives the same image. `Map` and `Slope` evaluate the spline;
`Apply` and the FITS writer go through a 65 536-entry table with linear interpolation between
entries (`MapSample`), exact at every 16-bit level; `IsMonotone` is judged on that table,
after clamping, because it is the curve that is applied.

**The anchors** (`CurveAdvisor.ClassLevels`, `Anchors`). Five L\* levels turned into greys:
the dust and the sky exactly as `DustAnalyzer.Measure` gives them; the band at the
`CurveBandClassPercentile` (80) of the band class's patches, the bright clouds at
`CurveCloudClassPercentile` (75), the peaks at the median of the peak class. The upper part,
not the median: the band's median sits beside the sky, and the steep segment would start at
the sky (measured: noise gain 1.40 against 1.26). The classes are those of the map on either
path (ADR 0033): on the sky path the third and fourth are the nebula and the bright nebula,
and the point labels take the path's names (`map.ClassNames`, carried in `CurveSet.Labels`
and `CurveEvidence.Labels`). An empty upper class, a peaks anchor at or over `CurvePeakTarget`
(the recipe would leave every point on the diagonal: the image is at the ceiling) and anchors
that do not increase strictly between 0 and 1 give a `Problem` and no curve, in that order.

**The recipe** (`CurveAdvisor.Curves`). The dust and the sky map to themselves. The three
segments above the sky get the increments slope x width with `CurveSlopeSkyBand`,
`CurveSlopeBandCloud`, `CurveSlopeCloudPeak` (1.70, 1.48, 0.94), all multiplied by one factor
`min(1, (CurvePeakTarget - sky) / sum)` so the peaks do not pass 0.92: the full curve. A point
never goes under its own input. `Blanda`, `Medie`, `Puternica` are `CurveStrengths` (0.5,
0.75, 1) of the way from the identity to it; `Maxima` uses the factor without the cap at 1, so
the peaks land on 0.92, and exists only when `Puternica` leaves them more than
`CurveMaximaMinGain` under it. A curve whose table is not monotone is dropped.

**The evidence** (`CurveAdvisor.Measure`). The five class levels again, with the same
statistics, on the tonal subsample with the curve applied: measured, not predicted, since a
per-channel curve does not take a coloured patch where it takes its grey. The noise gain is
the spline's slope at the sky's input, and the noise after is `NoiseResult.SigmaMedianLsb`
times it. The clipped share counts pixels with a channel at or over `CurveClipLevel`.

On the plate-solved FITS (V-008): anchors 0.091, 0.169, 0.306, 0.543, 0.720; `Puternica` has
the points 0.401, 0.753, 0.919 and takes the five levels from 7.9, 17.6, 33.2, 57.7, 74.6 to
7.9, 17.8, 43.4, 77.0, 90.8 with a gain of 1.26. The study's Python port gave the same anchors
and points; its class levels for the band and the bright clouds were medians (28.2 and 64.4),
the build's are the upper parts of the classes, like the anchors.

### F2 - `GamutAnalyzer` - how much of the image leaves the paper's gamut (R-144)

A figure, never a correction. A colour is outside when the round trip sRGB to the printer's
device values and back, through the profile's relative colorimetric tables (Wacton.Unicolour,
both directions), moves its a\*, b\* by more than `CurveGamutDeltaAb` (3). The chroma only:
without black point compensation the round trip also lifts everything under paper black, which
is the Tonuri tab's subject and on canvas is half the image. The package takes about 28
microseconds per colour, so the displacement (da, db) is computed once on a grid of
`CurveGamutGrid` (33) values per channel and interpolated trilinearly per pixel; against the
exact round trip on 20 000 colours the grid of 33 changed 0.01 % of the decisions (17: 0.11 %).
`Measure` gives the share of the tonal subsample's pixels, in all and per class of the patch
each pixel lies in.

On the plate-solved FITS through the lab's baryta profile (V-008): 1.02 % as is (dust 2.7 %,
sky 0.4 %, band 1.8 %, bright clouds 0.6 %, peaks 0.0 %), 0.82 % after `Puternica`. The study
of ADR 0032 counted Delta E with black point compensation in 8 bits through LittleCMS and got
4.2 % in all, most of it in the darks, and 0.9 % / 0.3 % for the band and the bright clouds:
the same order above the darks, a different definition, so the two are not to be compared digit
by digit.

## Notes from phase 2

- **Two different tile criteria, on purpose.** The noise sigma (B3) uses tiles with a
  range up to 6000/65535 (~23 levels), seed 0, 400 candidates; the plateau (C1, phase 3)
  uses range < 6 levels, mean < 70, seed 1, 120 tiles. Those are the two scripts the
  acceptance numbers came from. `Thresholds` keeps them apart.
- **"Background" for the simulation is the median of the reference channel.** Right
  whenever the sky dominates the frame - every astrophoto - and wrong on a frame where
  it does not (`WhenTheSkyDoesNotDominateTheMedianIsNotTheSky` keeps that visible).
- **Background mask uses `<=` the percentile, not `<`.** On a flat synthetic sky every
  pixel sits exactly at the percentile and a strict `<` selects nothing. Real data has no
  ties, so the acceptance numbers are unaffected.
- **Corner regions are compared per channel, not by luminance.** The reference image's
  horizon corner has the sky's luminance (R +9, B -10); a luminance test would miss it.

## Notes from phase 3

- **All star candidates are scanned, not a random 20 000.** The acceptance script drew
  20 000 candidates at random and kept 193 stars; the analyzer keeps every pixel that
  is the maximum of its 25x25 window. Deterministic, better sampled, same estimator.
  Expect the same median on more stars.
- **The half-max estimator has a known bias of ~+2.5 % on a sampled Gaussian** (linear
  interpolation between 1 and 2 px on a convex curve). A true FWHM of 3.0 measures
  ~3.08. Kept as is: it is the acceptance script's estimator, and the bias is far
  smaller than the star-to-star spread.
- **The JPEG numbers are ImageSharp's encoder's numbers.** The conversation's table was
  produced with ImageMagick (libjpeg). Both are 4:4:4; the q100 mean error will differ
  by tenths of a level because the two encoders' RGB<->YCbCr integer paths differ. The
  acceptance comparison for C3 is therefore approximate. Mean error is not monotone in
  quality at these near-lossless levels (measured); max error and the count of pixels
  over 2 levels are, and those drive the recommendation.
- **Percentiles over 69 million error samples come from a histogram** (0.01-level bins),
  not a sort: exact to the bin, a hundred times cheaper.

## Notes from phase 4

- **The star candidate threshold is `mean + minimum amplitude`, not the script's
  `3 x mean`.** Found by the golden scene with a 0.25 background: `3 x mean` put the
  threshold at 0.75 and no star of peak 0.55 qualified - which is exactly what would
  happen on the print version of a real image (background 0.19-0.25). The amplitude
  test against the local border median is the real filter and is unchanged. On a dark
  sky the new threshold admits fainter stars than the script did; the median FWHM is
  expected to move little, and phase 7 will say by how much.

- **JPEG mean error is not used for any verdict** - only max error and the count of
  samples over 2 levels (see phase 3 notes).
