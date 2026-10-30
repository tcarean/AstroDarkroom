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

Given `W`, `H` and `TargetAspect`: no crop when null, or when the rounded size removes no
pixel (ADR 0043; a ratio tolerance of 1e-6 used to decide it, and cut 0 px). If the target is
narrower, the new width is `round(H x target)`, cut from both sides, the right one a pixel more
when the cut is odd; otherwise the height, the bottom side the pixel more. Result: the centred
rectangle (`X0 Y0 W H`), `Axis` ("latime" / "inaltime"), `CutStartPx` and `CutEndPx` (the two
sides), `CutPct`; `ReportWriter.CropCutText` writes the two sides as one text, named apart when
they differ. The reference image at 50:35: 5938 x 3888 -> 5554 x 3888, 192 px per side, 6.5 %.

### B1/B2 - `TonalAnalyzer` - where the tones sit, and how many fall under paper black

B1 in **levels** (0..255), on all pixels: `MeanLevels` per plane; `PctBelowDarkA` /
`PctBelowDarkB` = share of samples (all planes) whose 8-bit level (`RasterOps.Level8`) is under
32 and under 64, compared as `v x 255 < N - 0.5`, which is `Level8(v) < N` for an even N
(ADR 0043; the float compare with N / 255 counted the level N itself on 8-bit sources);
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

`LstarStats(raster, dmax, stride)` computes them through `LstarStats(L*, dmax)` on the plane;
`LstarShares(L*)` gives only the two pixel shares (under 15, over 50) a simulation row carries,
without the median's sort and the histogram, which the row does not show (ADR 0035).

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
  patches (`FindClouds`: every patch centre tested against every cloud by the dot product of
  unit vectors against the cosine of the radius, after one pre-filter on the frame's centre and
  angular radius; `Wcs.SkyToPixel` and `ScaleDegPerPx` give the centre and the radius in pixels
  for the map alone; about 50 ms on the plate-solved FITS, ADR 0044), and the five classes (`Classify`): the band is |b| <
  `BandGalacticLatitudeDeg`; `SkyModel(L, band, cols, rows, percentile)` is the given
  percentile (the median here) of the outside-band patches per cell of `SkyModelCellPatches`,
  empty cells from the nearest (all patches together when no cell has enough), bilinear to
  patch resolution, then `Smooth`; `BandLevel` is `MedianFilter` over `BandLocalWindowPatches`
  then `Smooth`; a patch is `Dust` when `DustDepthLstar` under the local level in the band or
  under the sky model anywhere, `BrightCloud` when in the band and `BrightCloudLstar` over the
  local level, `Peak` when over the `PeakFraction` percentile, `Band` otherwise in the band (or
  outside it when `BrightCloudLstar` over the sky model), `Sky` otherwise. `Method` is `Band`.
  Without a WCS, with the band under `BandMinFraction` of the patches (`SkyReason.NoBand`)
  or the outside sky under it (`AllBand`), or with the patch centres spanning under
  `BandMinLatitudeSpanDeg` of galactic latitude (`LatitudeSpanDeg`, the maximum minus the
  minimum of `GalacticLatitude`, set in `Build` before the classes; `NarrowField`, tested
  after the two fractions so that a frame far from the plane still says `NoBand`),
  `ClassifyBySky` makes the classes instead (ADR 0033, ADR 0037, `Method` `Sky`): the sky
  model from all patches at `SkyModelLowPercentile` per cell;
  the residual patch minus model, with `Stats.RobustSigma` (1.4826 x MAD about the median),
  one clipping pass at `SkyNoiseClipK` sigmas and sigma taken again on what is kept; `Dust`
  under the model by `SkyNoiseK` sigmas (`NoiseThreshold`), `Band` (nebula) over it by as
  much, `BrightCloud` (bright nebula) over `Stats.OtsuThreshold` of the nebula residuals taken
  without the peak patches and with at least `BrightClassMinTailFraction` of them left above
  the split (a few star patches far over a faint nebula would otherwise be the upper class and
  leave the bright class empty; infinity when the tail is too small), `Peak` as above, `Sky`
  the rest. With sigma 0 (a
  posterised sky) the threshold is 0 and the class is the sign of the residual. The map keeps
  `SkySigma`, `NoiseThreshold` and `BrightThreshold` for the legend and Anexa B; `BandLevel` is
  null. The five `PatchClass` values are one enum on both paths and `ClassNames(Method)` gives
  their names in the interface language (`band` / `bright clouds` or `nebula` / `bright nebula`;
  in Romanian `banda` / `nori luminosi` or `nebuloasa` / `nebuloasa stralucitoare`).
- `Measure(patchLstars, map, paperBlack)` gives the `DustStats` of a (lifted) subsample on one
  path for both: `LstarSky` is the median of the sky patches (of all patches when the class is
  empty); `LstarDust` is the darkest extended area, the lower of the `DustClassFraction` (25 %)
  percentile of the dust patches, counted only when the class holds at least
  `DustClassMinFraction` (5 %) of the patches, and the `SkyDarkestFraction` (1 %) percentile of
  the sky patches. Which of the two is decided once, on the as-is L\*s (`DecideDustSource`,
  `DustMap.DustSource`), and every lift measures that statistic, so the ladder's rows and the
  definition sentence name the same thing; `DustPatchCount` is the dust class's size, and
  `DustMap.SkyClassEmpty` tells the texts when the sky is the median of all patches.
  Always `PctPatchesBelowPaperBlack` ("negru pur") and `CloudLstars`, the `DustCloudFraction`
  (10 %) percentile of each cloud's own patches, for the rows. The map window paints
  `map.Classes` as they are.

The first form (ADR 0025: every cloud-disc patch as dust, every outside patch as sky; the
owner rejected it on the map on 2026-10-02) and the second (the patches at or under the two
levels) are gone; what the Great Rift is on the plate-solved FITS, and what the plain edges
are, is now what the classes say.

### B4 - `PrintStretchSimulator` - the lowest level that puts the dust on paper black and keeps the sky visible

Builds the `DustMap`, measures the as-is row (B5 on the subsample; the pixel shares of B2),
then, when the background is strictly inside (0, 1), lifts the subsample once per step of a
**ladder**: every `ExactTargetStep` (0.01) from `ExactTargetLo` (0.05) to `ExactTargetHi`
(0.60), plus the grid `SimulationTargets` (0.05..0.20) and the explicit `TargetBackground`;
gamma = `ln(target) / ln(BackgroundMedian)` on every channel. A step builds no lifted raster:
one `LiftTable` for its gamma lifts and linearises every sample from a 16-bit table
interpolated on the linear value (`input.md`), `DustAnalyzer.PatchLstars(sub, table)` sums the
patch luminances straight from it, and a grid row's pixel shares come from
`TonalAnalyzer.LstarShares(RasterOps.LstarPlane(sub, table))`; the as-is row stays on the
exact path (ADR 0035; before it, `RasterOps.Gamma` plus a `Math.Pow` per lifted sample made
the lifted passes 15 of the simulation's 17 s on the FITS; the whole simulation takes 2.6 s
now). The steps are lifted from the lowest up and the ladder **stops early**: each series the
exact targets are read on (the dust against the paper black of the Dmax in use and of Dmax -/+
`DmaxSensitivityStep`, the sky against `LowSeparationLstar`, each row cloud against paper
black) is marked crossed at its first step at or above its level, and once every series has
crossed and every table row is built the remaining steps are not lifted; `Crossing` reads a
series no further than that first step, so the targets are those of the whole ladder
(`SimulationResult.StepsLifted` counts the lifts; the review of 2026-10-23, item 15). Only the
steps within `ExactTargetLo`..`ExactTargetHi` feed the series: a given target outside the ladder
is lifted for its row alone (ADR 0040). The grid steps become
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
the clouds with at least `DustCloudMinPatches` (9) patches, darkest first.
`DmaxDustOnPaperBlackAsIs` is the Dmax whose paper black equals the as-is dust's L\* (NaN at
L\* 0), and `DustOnPaperBlackAsIs` whether the as-is dust is on it: the dust's figures alone,
named so since the review of 2026-10-23 (ADR 0043); `ReportWriter.DustDmaxText` writes the
sentence, with the sky's caveat when the as-is sky is under `LowSeparationLstar`. `Primary` = the
explicit row, else the recommended one, else the as-is row. `DustDescription`,
`SkyDescription` and `MethodNote` are the words the report and the window use for the method.

Known limits: the median is not the sky when the sky does not dominate the frame; the gamma
curve keeps the shadows higher than a real stretch to the same background (the owner's VeraLux
stretch to 0.17 measured the dust at L\* 7.6, under paper black, where the curve from 0.103
predicted it above from 0.14); a disc is not the cloud's contour.

### B6 - `PrintPreview` - a simulation row as an image (window only)

`Render(img, gamma, dmax)`: the image subsampled to at most `PreviewMaxSide` (1600), lifted with
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

**Background** (E1) = the sky's colour: on the subsample the dust map was built on (stride
4), the pixels of the patches whose class is `Sky` (R-044, `DustMap.Classes`) that lie above
the bottom 25 % of rows (the horizon); `Levels` = their per-plane medians x 255, and `Source`
says which tier gave them: `SkyPatches`; `AllPatches` when no sky patch lies above the band
(every patch there); `WholeFrame` as a guard the grid never reaches (it keeps at least one
patch). `AnalysisRun` passes the simulation's map; the one-argument overload builds a map
without the catalogue for the tests. Before ADR 0055 the reference was the darkest quarter of
the pixels by luminance, which on a frame the Great Rift crosses is the dust, brown, so that
every sky edge read "bluer than the background". On colour: `Lab`, the colour in CIE L*a*b*
(`ColorMath.LabOfSrgb`, D65 with the sRGB matrix's row sums as the white, so that a grey is
exactly neutral), `Chroma` = sqrt(a*^2 + b*^2), and `HasCast` when the chroma exceeds
`CastWarnChroma` (3.0; ADR 0057); `GreenExcess` = G - (R+B)/2 and `RedMinusBlue` are figures
that name the cast's direction, the larger of the two giving the name (`Recommendations`).

**Regions** (E2), on the same subsample as the map: four corners (15 % of height x 20 % of
width) and four edges (the same bands between the corners). Each `EdgeRegion` has per-plane
medians (`Levels`), taken on the region's sky pixels (`ClassAt` reads the map's class of each
sample) when they are at least `RegionSkyMinFraction` (0.25) of it (`OnSkyPixels`,
`SkyFraction`) and on every pixel otherwise, with `PixelsNote` for the row (ADR 0056); `Deltas`
= region - background per plane, `MaxAbsDelta`, `Flagged` (> `CornerGradientLevels` = 3), and
`Coloured`: spread (max delta - min delta) > 3 **and** > `CornerColourSpreadRatio` (0.5) x mean
|delta| - the channels move differently. The distinction came from the reference image against
the darkest quarter: the horizon corner +9.4 / +1.7 / -9.9 (coloured, light pollution), the
corner the Milky Way enters +11 / +10 / +16 (neutral, the object). Against the sky patches, on
their own sky pixels, they read +4.1 / -4.0 / -17.1 (coloured) and +4.2 / +2.9 / +5.6 (neutral;
on every pixel the band made it +6.5 / +5.1 / +8.7, coloured). On mono there is one plane and
`Coloured` is always false.

### D1/D2 - `StarAnalyzer` - the real resolution

Candidate = pixel of the reference plane above `mean + StarCandidateAboveMean16/65535`
(3000/65535 = 0.046 above the plane mean), not above `StarMaxPeak16/65535` (saturated),
and the maximum (ties allowed) of its 25 x 25 window (`StarWindow`, `Half = StarWindow / 2` the
border left out); among tied maxima only the first in scan order passes, so a flat two-pixel
peak is one star (ADR 0041); a tied pixel in the border left out disqualifies nothing, so a flat
peak straddling the border is still measured, at its first scanned pixel. Base = median of the
window's 96 border pixels; sigma = 1.4826 x the median absolute deviation of those pixels about
a plane fitted to them by least squares, whose coefficients are three sums because the border
is symmetric about the centre (`BorderStats` returns the base and the sigma; near 0 on a
quantised flat border, exactly 0 on a uniform one); amplitude = peak - base >=
`StarMinAmplitude16/65535` and >= `StarMinSnr` (12) x sigma (ADR 0054). Width
at half maximum is measured on the horizontal and vertical lines through the peak:
descend from the centre until below `base + amplitude/2`, interpolate the crossing
linearly (the acceptance script's function, epsilon included), keep the star when both
widths are in (`StarFwhmMin` 0.8, `StarFwhmMax` 15) and record their mean.

Result: `Stars` (x, y, fwhm, peak), `FwhmMedian` / `Q1` / `Q3` (NaN below
`StarMinCount` = 20, or under `StarMinPerMegapixel` = 10 per megapixel, `DensityPerMp`; ADR 0062), `Regions` (centre = central 40 % x 40 %, four corners 20 % x 20 %;
median per region when >= `RegionMinStars` = 5), `CornerToCenterRatio` = widest known
corner / centre, `SoftCorners` when > `CornerSoftRatio` (1.4), `EffectiveDetailPx(w)` =
w / FwhmMedian.

Known: the estimator overestimates a sampled Gaussian by ~2.5 % (linear interpolation on
a convex curve); the candidate threshold is `mean + minimum amplitude`, not the script's
`3 x mean`, because at a 0.25 background the latter rejected every star (the print
version of a real image). On a nebulous core stars measure wider (bright, structured
base) - see the acceptance record. The floor alone found the grain: on a stretched frame
the noise's window maxima stand 3 to 4 local sigmas over the border median and the floor
(0.046) sits on the expected maximum of 625 samples (0.044 on the starless reference FITS),
so a starless frame gave 6297 "stars" at 2.38 px until the sigma clause (ADR 0054); the
clause costs the faint stars (under 12 sigmas), and the FWHM is the bright stars'. The sigma
is taken about a plane because the raw deviation about the border median read a nebula's
slope as noise: it lost 14 of the 64 stars of the DarkSky golden scene (0.4 over 60 px) and a
quarter of the reference image's 7163, against an eighth about the plane.

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

**C1 plateau**: at every one of `PlateauPositions` (5000) random positions from
`SeededRandom(PlateauSeed = 1)`, the tiles accepted when the reference plane's range < 6
levels and mean < 70 levels (`PlateauTile*` - the strict criterion of the other acceptance
script). On each, the 8-bit quantised values: changes of value per row, averaged; the tile's
plateau = 64 / (1 + changes), kept in `PlateauTilePx` (sorted). `PlateauPx` = mean over tiles
(NaN without tiles), the typical step, for information; `PlateauMmAt(dpi)` = PlateauPx x
25.4 / dpi. The verdict (ADR 0060): `PlateauVisibleFramePctAt(dpi)` = 100 x (tiles with px x
25.4 / dpi > `PlateauVisibleMm` (1.0)) / positions, and `PlateauVisibleAt(dpi)` when it passes
`PlateauFlatFrameWarnPct` (0.1 %). The mean cannot decide: on a heavily denoised frame the many
ordinary tiles drown the few fully flat ones (the owner's 2026-08-12 core at Syqon 1: mean
4.7 px, but 0.44 % of the frame flat over 1 mm). `PlateauFramePct` = 100 x tiles accepted /
positions (the draws are uniform, so it estimates the share of the frame that is clean sky by
the strict criterion); `PlateauSigmaLsb` = the median over the tiles of
`NoiseAnalyzer.SigmaLsb`. The two say where the plateau was measured: a tile with real noise
of about 1 LSB or more spans over 6 levels (the range of its 4096 samples is about 7 sigma;
the clean-sky scene at 1.0 LSB yields no tile) and fails the range criterion, so on an
AI-denoised frame the plateau's tiles are the patches the denoiser wiped (at Syqon 0.75: 11
tiles, 0.2 % of the frame, sigma 0.01, against a noise median of 0.82 on 313 smooth tiles).

**C2 rounding** (when the source has more than 8 bits): |v x 255 - round(v x 255)| on all
planes at stride 2 -> `RoundingMeanAbs`, `RoundingMax`, `RoundingP999` (from a 1000-bin
histogram).

**C4 shadow-lift stress**: the reference plane at stride 2, lifted with x^(1/1.9)
(`ShadowLiftGamma`), quantised to 8 bits, once from the float data and once from data
first rounded to 8 bits. Levels used strictly inside (`ShadowLiftLevelLo` 8,
`ShadowLiftLevelHi` 120) and missing between the first and last used:
`LevelsUsedFrom16` / `MissingFrom16` / `LevelsUsedFrom8` / `MissingFrom8`.
`Keep16BitMaster` when MissingFrom8 - MissingFrom16 > `MissingLevelsWarn` (20), a mark on the Export tab's stress row since ADR 0061, not a warning: the levels 8 bits
lose that 16 bits keep, so an upscale, missing the same on both paths, gets no such advice
(ADR 0043).

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

Not a measurement of the image but a suggestion built on its measurements; computed by a second run after the analysis
(`pipeline.md`, "The second run").

**The curve** (`ToneCurve`). Control points from (0, 0) to (1, 1), strictly increasing in x,
and the natural cubic spline through them, solved with the same tridiagonal recurrence as
Siril's `curve_transform.c`, clamped to 0..1: the table of points typed into Siril's Curves
tool in `cubic spline` mode gives the same image. `Map` and `Slope` evaluate the spline;
`Apply` and the FITS writer go through a 65 536-entry table with linear interpolation between
entries (`MapSample`), the spline at every 16-bit level to float precision (within 1.5e-7 on
the curves measured on 2026-10-23); `IsMonotone` and `IsWithinRange` are judged on the
unclamped spline at the same steps, to a tolerance of 1e-9 for the arithmetic's noise, and
`IsSuggestible` is both: a spline the clamp would have to hold at 0 or 1 is not offered (ADR
0039). `WouldBeSuggestible(points)` makes the same judgement without building the table, for
the cap's bisection.

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
that do not increase strictly give a `Problem` and no curve, in that order. A dust anchor at 0 is the
origin itself: the dust point takes the place of the (0, 0) end, with its label, since the
curve never moves it anyway.

**The recipe** (`CurveAdvisor.Curves`). The dust and the sky map to themselves. The three
segments above the sky get the increments slope x width with `CurveSlopeSkyBand`,
`CurveSlopeBandCloud`, `CurveSlopeCloudPeak` (1.70, 1.48, 0.94), all multiplied by one factor
`min(1, (CurvePeakTarget - sky) / sum)` so the peaks do not pass 0.92: the full curve. A point
never goes under its own input. `Blanda`, `Medie`, `Puternica` are `CurveStrengths` (0.5,
0.75, 1) of the way from the identity to it; `Maxima` uses the factor without the cap at 1, so
the peaks head for 0.92. Both factors are then capped at the largest value, up to their own,
whose full curve is suggestible (a bisection from the identity): on the four reference images
the full curve needs no cap and Maxima's spline overshoots 1 on three of them (1.010, 1.007,
1.051), so its peaks land at 0.892, 0.899 and 0.828 instead of 0.92 (ADR 0039). `Maxima` exists
only when its capped peaks gain more than `CurveMaximaMinGain` over `Puternica`'s. A curve that
is still not suggestible is dropped.

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
is the Tonuri tab's subject and on canvas is half the image. The package takes about 25
microseconds per colour (single-threaded, warm; 2026-10-23), so the displacement (da, db) is
computed once on a grid of `CurveGamutGrid` (33) values per channel and interpolated trilinearly
per pixel (`Interpolated`). The interpolation rounds the gamut's edge inward and misses colours
rather than adds them, most of them dark: against the exact round trip the grid alone read the
plate-solved FITS on the lab's baryta at 1.02 % against 1.35 % (the dust class 2.7 against 4.2),
its canvas at 35.1 % against 39.7 %, and `de_printat.png`'s dust class at 0.0 % against 2.4 %;
a grid of 65 was still 0.45 and 2.2 points low on the two dust classes. So `Outside` is a
hybrid (ADR 0042): a pixel whose interpolated displacement lies within `CurveGamutExactMargin`
(1.0) of the threshold takes the exact round trip of its 8-bit colour (`GamutTable.Exact`, the
package on the profile's tables), made once per colour and kept in `GamutTable.Cache` for the
run; the others keep the grid's answer. Measured 2026-10-23 against the exact figure: every
class within 0.17 points on the three image-profile pairs; 9 100 trips per curve on the FITS on
baryta, 29 000 to 40 500 on its canvas, 1 700 on the PNG. `Measure` gives the share of the
tonal subsample's pixels, in all and per class of the patch each pixel lies in, the rows run in
parallel (`Parallel.For`, each worker's counts over the rows it took merged under a lock; the package's configuration
served eight threads in the probe with the single-threaded results); a table without an `Exact`
trip, the hand-built ones of the tests, is judged by the interpolation alone.

On the plate-solved FITS through the lab's baryta profile (V-008): 1.36 % as is (dust 4.3 %,
sky 0.7 %, band 1.8 %, bright clouds 0.6 %, peaks 0.0 %; exact per pixel 1.35 %, dust 4.2 %),
1.20 % after `Puternica` (exact 1.19 %); a `Measure` takes 25 to 120 ms on the FITS on baryta,
110 to 250 ms on its canvas and 25 to 80 ms on the PNG (the first of a run fills the cache),
against 45 to 55 ms for the grid alone (measured 2026-10-23 with eight threads). The study of
ADR 0032 counted Delta E with black point compensation in 8 bits through LittleCMS and got
4.2 % in all, most of it in the darks, and 0.9 % / 0.3 % for the band and the bright clouds:
the same order above the darks, a different definition, so the two are not to be compared digit
by digit.

### F3 - `SoftProof` - what the paper shows of the image (R-160 to R-162)

Not a measurement of the image but of the paper's effect on it, viewing only: the chain through
the profile's tables, the table per intent, the display step and the two warnings are in
`soft-proof.md`; the decision is ADR 0066.

## Notes from phase 2

- **Two different tile criteria, on purpose.** The noise sigma (B3) uses tiles with a
  range up to 6000/65535 (~23 levels), seed 0, 400 candidates; the plateau (C1, phase 3)
  uses range < 6 levels, mean < 70, seed 1 (120 tiles then, every one of 5000 positions since ADR 0060). Those are the two scripts the
  acceptance numbers came from. `Thresholds` keeps them apart.
- **"Background" for the simulation is the median of the reference channel.** Right
  whenever the sky dominates the frame - every astrophoto - and wrong on a frame where
  it does not (`WhenTheSkyDoesNotDominateTheMedianIsNotTheSky` keeps that visible).
- **Background mask uses `<=` the percentile, not `<`.** On a flat synthetic sky every
  pixel sits exactly at the percentile and a strict `<` selects nothing. Real data ties
  too, but little: on the 16-bit reference image 68 pixels, and the acceptance numbers are
  unaffected; on 8-bit data the choice moves the cast by a level (the reference image's 8-bit
  version: +2.0 with `<=`, +1.0 with `<`), ADR 0014's correction of 2026-10-23. Moot since
  ADR 0055: the colour comes from the sky patches, and no percentile cut remains.
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
