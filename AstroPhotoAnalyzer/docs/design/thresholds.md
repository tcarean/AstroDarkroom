# Thresholds

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §7. Why the thresholds
> are empirical and where the literals of the window's texts go is ADR 0012; each requirement
> row quotes the ones it uses. **Covers:** `AstroPhotoAnalyzer.Core/Thresholds.cs`, whose
> comments say where each value came from. A change to the code this describes means this note
> is updated in the same task, or deleted with the decision it explains.

## Thresholds (`Thresholds.cs`)

All empirical, most from the reference image. Change one, change its comment.

| name | value | meaning |
|---|---|---|
| `LinearMedianMax` | 0.02 | below: image is probably linear, refused |
| `UpscaledFraction` | 0.5 | above: 16-bit file is an 8-bit upscale |
| `DarkLevelA` / `DarkLevelB` | 32 / 64 | "dark pixel" levels for context |
| `LowSeparationLstar` | 15 | the visibility threshold: below, the paper separates, the eye in room light barely; a sky under it is an Attention and the recommended row has the sky at or above it (ADR 0029); a grey of L\* 15 is the level 0.148, the same rule as `PrintBackgroundLo` |
| `HighlightLstar` | 50 | above: bright |
| `DustPatchPx` | 32 | the patch side (image px) the dust and the sky are measured on: 4 mm at 200 dpi; 16/32/64 barely move the verdict |
| `DustCloudFraction` | 0.10 | a catalogued cloud's L\* for its row: this percentile of its patches |
| `BandGalacticLatitudeDeg` | 10 | the galactic band: patches with \|b\| under it (ADR 0026) |
| `BandMinFraction` | 0.10 | the band classes need the band and the outside sky each at or above this share of the patches; else the classes are relative to the modelled sky (ADR 0033) |
| `BandMinLatitudeSpanDeg` | 20 | the band classes also need the patch centres to span at least this many degrees of galactic latitude, the band's full width; a narrower frame is deep-sky inside the band (the Veil at 10.9) and takes the sky path with the reason naming its span (ADR 0037) |
| `SkyModelPercentile` | 50 | without the band, the sky model takes this percentile of each cell over all patches: the median; the 25th percentile of ADR 0033 sank into the Great Rift on a Milky Way frame without a WCS (ADR 0085) |
| `SkyNoiseClipK` / `SkyNoiseK` | 3 / 3 | the sky's patch noise: robust sigma of patch minus model after one clipping pass at the first; nebula is over the model by the second times that sigma, dust under it by as much (the bright-nebula threshold is the Otsu split of the nebula residuals, no constant); the second was 4 until ADR 0085 |
| `BrightClassMinTailFraction` | 0.05 | the Otsu split of the nebula residuals leaves at least this share of them above: a few star patches far over a faint nebula would otherwise be the upper class and leave the bright class empty |
| `DustClassMinFraction` | 0.05 | the dust class decides the verdict only from this share of the patches; under it (cores, artefacts) the darkest sky stands (the owner's choice, ADR 0033) |
| `BandDustK` | 4 | on the band path, a dust patch is this many sigmas of the sky's noise (`SkySigma`, the sky path's figure) under the band's local level or under the sky model; a fixed 5 L\* until ADR 0086, which found half the Rift on a darker stretch |
| `BandBrightK` | 8 | a bright-cloud patch is this many sigmas of the sky's noise over the band's local level (outside the band, a band patch as much over the sky model): twice the dust's depth, as 10 L\* were twice 5 |
| `PeakFraction` | 0.001 | the peaks: the brightest patches by this fraction |
| `BandLocalWindowPatches` / `BandLocalSmoothPatches` | 15 / 2 | the band's local level: median window, then smoothing radius |
| `SkyModelCellPatches` / `SkyModelCellMinPatches` / `SkyModelSmoothPatches` | 20 / 20 / 3 | the sky model: cell size, patches a cell needs, smoothing radius |
| `DustClassFraction` | 0.25 | the dust's L\* for the verdict: this percentile of the dust patches (the owner's choice, ADR 0026) |
| `DustCloudMinPatches` | 9 | a cloud with this many patches gets its own row and circle |
| `SkyDarkestFraction` | 0.01 | the darkest sky: this percentile of the sky patches; the dust is the lower of it and the dust class's percentile |
| `DustCloudReportRows` | 15 | clouds named in Anexa B and in the Because; the window lists all |
| `ExactTargetLo` / `ExactTargetHi` / `ExactTargetStep` | 0.05 / 0.60 / 0.01 | the ladder of lifts the exact targets are interpolated on |
| `DmaxMax` | 4 | the largest Dmax accepted, typed or read from an ICC profile; a profile above it is damaged |
| `DmaxSensitivityStep` | 0.1 | the two neighbouring Dmax values Anexa B gives the exact target at (about 0.02 of target per 0.1) |
| `PreviewMaxSide` | 1600 | longest side of the map and preview windows' image |
| `ScreenBackgroundLo` / `ScreenBackgroundHi` / `PrintBackgroundLo` / `PrintBackgroundHi` | 0.05 / 0.10 / 0.15 / 0.25 | where a screen image's sky sits and where a print's should; rules of thumb for the window's help text only, no rule reads them |
| `TonalStride` / `PercentileStride` | 4 / 2 | subsampling steps of the acceptance scripts; the background colour and the edge regions use the map's, `TonalStride` (`BackgroundStride`, 3, went with ADR 0056) |
| `NoiseTileSize` / `NoiseTileCandidates` | 64 / 400 | |
| `NoiseTileMaxRangeLevels` | 6000/257 = 23.3 | smooth-tile range for noise |
| `NoiseMinTiles` | 10 | fewer: insufficient |
| `PlateauTileMaxRangeLevels` / `PlateauTileMaxMeanLevels` / `PlateauSeed` / `PlateauPositions` | 6 / 70 / 1 / 5000 | strict tiles for the plateau, at every one of the positions (`PlateauTileCount`, 120, and `PlateauMaxAttempts` went with ADR 0060) |
| `NoiseBandingLsb` / `NoisePrintLoLsb` / `NoisePrintHiLsb` / `NoiseUnderDenoisedLsb` | 0.5 / 0.8 / 1.5 / 2.0 | sigma bands |
| `PlateauVisibleMm` | 1.0 | a tile's plateau longer than this on paper is a contour |
| `PlateauFlatFrameWarnPct` | 0.1 | a banding risk when the tiles with a contour are more than this share of the frame, in percent: about five tiles on 23 MP, 4 cm2 on an 80 cm print; 0.00 % on the reference image, 0.18 / 0.24 / 0.44 % on the owner's core at Syqon 0.5 / 0.75 / 1 (ADR 0060) |
| `JpegMaxErrorReject` / `JpegErrorCountLevels` / `JpegOverThresholdWarnPct` | 10 / 2 / 5 | q100 gets Attention above this percentage of samples over `JpegErrorCountLevels` even when its max error is acceptable |
| `ShadowLiftGamma` / `ShadowLiftLevelLo` / `ShadowLiftLevelHi` / `MissingLevelsWarn` | 1.9 / 8 / 120 / 20 | stress test |
| `StarWindow` / `StarMinAmplitude16` / `StarMaxPeak16` / `StarCandidateAboveMean16` | 25 / 3000 / 60000 / 3000 | in 16-bit units |
| `StarMinSnr` | 12 | the amplitude over the border's robust sigma about a fitted plane; the grain peaks at 3 to 4 (ADR 0054) |
| `StarFwhmMin` / `StarFwhmMax` | 0.8 / 15 | accepted widths |
| `CornerSoftRatio` / `RegionFraction` / `CenterFraction` / `StarMinCount` / `RegionMinStars` | 1.4 / 0.2 / 0.4 / 20 / 5 | |
| `StarMinPerMegapixel` | 10 | stars per megapixel under which the FWHM is not trusted, beside the count: real fields at 76 to 332, the strongest Syqon denoise of a starless core at 2.1 (ADR 0062) |
| `FallbackMinDpi` | 180 | pixel-grid floor without stars and without a distance: 0.14 mm pitch, invisible from ~0.5 m (lab convention 150-200); with a distance the floor is 25.4 / (0.29 x d) |
| `JpegQualities` | 100, 95, 90 | |
| `BackgroundExcludeBottomFraction` | 0.25 | the sky's colour leaves out the bottom band, where the horizon sits; it is taken on the sky patches of R-044 (ADR 0055; `BackgroundDarkestFraction`, the darkest quarter, went with it) |
| `EdgeRegionHeightFraction` / `EdgeRegionWidthFraction` | 0.15 / 0.20 | corner = this much of the height x this much of the width; the edges are bands of the same thickness |
| `RegionSkyMinFraction` | 0.25 | a region is measured on its sky pixels alone from this share of sky; under it on every pixel, and the row says so; the lowest share on five real images was 41 % (ADR 0056) |
| `CastWarnChroma` | 3.0 | a cast is a sky colour whose L*a*b* chroma passes this; about 1 is the smallest difference seen beside a grey, 3 to 5 where a sky's cast reads on paper; the five real images sit at 1.0, 1.4, 1.8, 2.0 and 4.8 (ADR 0057; `CastWarnLevels`, 1.0 level, went with it) |
| `CornerGradientLevels` / `CornerColourSpreadRatio` | 3.0 / 0.5 | |
| `AcuityMmAt1m` / `ViewingDistanceFactor` / `OversizeCm` / `ReferenceDpi` | 0.29 / 1.5 / 100 / 300 | 1.5 x the diagonal sits between the perspective-correct 1 x and the gallery 2 x (~37 degrees subtended); a presumption about the viewer, not a measurement |
| `NaturalDistanceTolerance` | 1.15 | allowance on the natural distance (the verdict is size-independent there) |
| `PassepartoutMinFrameRatio` | 2 | passepartout >= this x frame width, else the parameters are refused |
| `CurveBandClassPercentile` / `CurveCloudClassPercentile` | 80 / 75 | the upper part of the band and bright-cloud classes as curve anchors: 0.306 and 0.543 on the plate-solved FITS, the 0.32 and 0.55 of the owner's earlier curve (ADR 0032) |
| `CurveSlopeSkyBand` / `CurveSlopeBandCloud` / `CurveSlopeCloudPeak` | 1.70 / 1.48 / 0.94 | the segment slopes of that curve; one curve on one image |
| `CurvePeakTarget` / `CurveStrengths` / `CurveMaximaMinGain` | 0.92 / 0.5, 0.75, 1 / 0.02 | where the peaks go at most; the three strengths; `Maxima` only when its capped peaks gain more than this over `Puternica`'s (ADR 0039) |
| `CurveClipLevel` | 0.995 | a pixel counts as clipped with a channel at or over it |
| `CurveGamutDeltaE` / `CurveGamutGrid` / `CurveGamutExactMargin` | 4 / 33 / 1.0 | the whole colour difference, lightness included, after the round trip with black point compensation, that counts as out of gamut (ADR 0081: the value that best matches Lightroom's destination gamut warning on the Smida TIFF through the WhiteWall baryta profile; the chroma alone over 3 missed the dark dust the paper prints lighter); the round-trip grid per channel; how close to the threshold the interpolated displacement must be for the exact round trip of the pixel's 8-bit colour to decide instead (ADR 0042: the grid alone undercounted by up to 2.4 points per class, the hybrid is within 0.2) |
| `CurveShadowChartFactor` / `CurveChartSamples` | 1.25 / 257 | the shadow chart ends at this many times the band's input; samples per chart |
| `CurvePreviewDefaultWidth` / `CurvePreviewDefaultHeight` | 1600 / 900 | the preview size before the view has reported its gallery area: a presumption about the screen |
| `CurvePreviewMaxPixels` | 8 000 000 | the most pixels a gallery preview may have (R-146, R-178): a 4K area on a 23 MP image would otherwise take the whole image, 0.9 GB held by the curves' set and 1.6 GB by the fit's, the process at 3.5 GB (measured 2026-11-06, ADR 0084) |
| `ProofGrid` | 65 | the nodes per channel of the soft proof's table (R-160): within Delta E76 1.0 of the exact per-pixel proof at the 99th percentile on both lab profiles, where 33 reach 5.2 on the canvas; 1.6 to 2.2 s to build in parallel (ADR 0066) |
| `ProofMaxSide` | 2400 | the long side of the reduced copy the proof is made on (R-160): the preview's 1600 fits a Full HD render, the proof is shown full screen on a 4K screen too; a presumption about the screen |
| `GamutFitLstarNodes` / `GamutFitHueNodes` | 101 / 121 | the nodes of the paper fit's table of the largest chroma over L\* and hue (R-175): builds in 1.3 s, within 0.025 of the exact search in the chroma factor on the baryta profiles (ADR 0083) |
| `GamutFitEdgeMargin` | 0.6 | the band of chroma around the table's edge where the judge itself decides a pixel's largest chroma, for every pixel the fit would land near the edge, once per 16-bit colour (R-175): the table alone leaves 0.19 to 0.59 % of the measured pairs outside |
| `GamutFitKneeExact` / `GamutFitKneeSoft` / `GamutFitKneeSofter` | 1.0 / 0.9 / 0.8 | the share of the edge the three variants compress the chroma from (R-176): the hard fit touches only the pixels outside, the knees 2 to 3 times as many for smaller losses each (ADR 0083) |
| `GamutFitMinChroma` | 0.05 | under this chroma a pixel is grey for the fit: never touched, never outside (R-175) |
| `GamutFitBandShare` / `GamutFitRecipeShare` / `GamutFitRecipeStep` | 0.90 / 0.99 / 0.05 | the recipe for a hand-made mask (R-176): the tone band holds 90 % of the pixels outside, the saturation factor brings 99 % of them in, in steps of 5 % |
