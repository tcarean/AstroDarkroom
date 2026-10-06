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
| `SkyModelLowPercentile` | 25 | without the band, the sky model takes this percentile of each cell over all patches, so an object covering half a cell does not lift it |
| `SkyNoiseClipK` / `SkyNoiseK` | 3 / 4 | the sky's patch noise: robust sigma of patch minus model after one clipping pass at the first; nebula is over the model by the second times that sigma, dust under it by as much (the bright-nebula threshold is the Otsu split of the nebula residuals, no constant) |
| `DustClassMinFraction` | 0.05 | the dust class decides the verdict only from this share of the patches; under it (cores, artefacts) the darkest sky stands (the owner's choice, ADR 0033) |
| `DustDepthLstar` | 5 | a dust patch is this many L\* under the band's local level or under the sky model |
| `BrightCloudLstar` | 10 | a bright-cloud patch is this many L\* over the band's local level |
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
| `TonalStride` / `PercentileStride` / `BackgroundStride` | 4 / 2 / 3 | subsampling steps of the acceptance scripts |
| `NoiseTileSize` / `NoiseTileCandidates` | 64 / 400 | |
| `NoiseTileMaxRangeLevels` | 6000/257 = 23.3 | smooth-tile range for noise |
| `NoiseMinTiles` | 10 | fewer: insufficient |
| `PlateauTileMaxRangeLevels` / `PlateauTileMaxMeanLevels` / `PlateauTileCount` / `PlateauSeed` / `PlateauMaxAttempts` | 6 / 70 / 120 / 1 / 5000 | strict tiles for the plateau |
| `NoiseBandingLsb` / `NoisePrintLoLsb` / `NoisePrintHiLsb` / `NoiseUnderDenoisedLsb` | 0.5 / 0.8 / 1.5 / 2.0 | sigma bands |
| `PlateauVisibleMm` | 1.0 | a plateau longer than this on paper is a contour |
| `JpegMaxErrorReject` / `JpegErrorCountLevels` / `JpegOverThresholdWarnPct` | 10 / 2 / 5 | q100 gets Attention above this percentage of samples over `JpegErrorCountLevels` even when its max error is acceptable |
| `ShadowLiftGamma` / `ShadowLiftLevelLo` / `ShadowLiftLevelHi` / `MissingLevelsWarn` | 1.9 / 8 / 120 / 20 | stress test |
| `StarWindow` / `StarMinAmplitude16` / `StarMaxPeak16` / `StarCandidateAboveMean16` | 25 / 3000 / 60000 / 3000 | in 16-bit units |
| `StarFwhmMin` / `StarFwhmMax` | 0.8 / 15 | accepted widths |
| `CornerSoftRatio` / `RegionFraction` / `CenterFraction` / `StarMinCount` / `RegionMinStars` | 1.4 / 0.2 / 0.4 / 20 / 5 | |
| `FallbackMinDpi` | 180 | pixel-grid floor without stars and without a distance: 0.14 mm pitch, invisible from ~0.5 m (lab convention 150-200); with a distance the floor is 25.4 / (0.29 x d) |
| `JpegQualities` | 100, 95, 90 | |
| `BackgroundDarkestFraction` / `BackgroundExcludeBottomFraction` | 0.25 / 0.25 | |
| `EdgeRegionHeightFraction` / `EdgeRegionWidthFraction` | 0.15 / 0.20 | corner = this much of the height x this much of the width; the edges are bands of the same thickness |
| `CastWarnLevels` / `CornerGradientLevels` / `CornerColourSpreadRatio` | 1.0 / 3.0 / 0.5 | |
| `ScreenPixelMm` | 0.25 | a common monitor's pixel pitch (96-110 ppi); a presumption about the screen, for the Preview tab's step-back rule |
| `AcuityMmAt1m` / `ViewingDistanceFactor` / `OversizeCm` / `ReferenceDpi` | 0.29 / 1.5 / 100 / 300 | 1.5 x the diagonal sits between the perspective-correct 1 x and the gallery 2 x (~37 degrees subtended); a presumption about the viewer, not a measurement |
| `NaturalDistanceTolerance` | 1.15 | allowance on the natural distance (the verdict is size-independent there) |
| `PassepartoutMinFrameRatio` | 2 | passepartout >= this x frame width, else the parameters are refused |
| `CurveBandClassPercentile` / `CurveCloudClassPercentile` | 80 / 75 | the upper part of the band and bright-cloud classes as curve anchors: 0.306 and 0.543 on the plate-solved FITS, the 0.32 and 0.55 of the owner's earlier curve (ADR 0032) |
| `CurveSlopeSkyBand` / `CurveSlopeBandCloud` / `CurveSlopeCloudPeak` | 1.70 / 1.48 / 0.94 | the segment slopes of that curve; one curve on one image |
| `CurvePeakTarget` / `CurveStrengths` / `CurveMaximaMinGain` | 0.92 / 0.5, 0.75, 1 / 0.02 | where the peaks go at most; the three strengths; `Maxima` only when `Puternica` stops further than this under the target |
| `CurveClipLevel` | 0.995 | a pixel counts as clipped with a channel at or over it |
| `CurveGamutDeltaAb` / `CurveGamutGrid` | 3 / 33 | the chroma displacement that counts as out of gamut; the round-trip grid per channel (0.01 % of decisions differ from the exact round trip) |
| `CurveShadowChartFactor` / `CurveChartSamples` | 1.25 / 257 | the shadow chart ends at this many times the band's input; samples per chart |
| `CurvePreviewDefaultWidth` / `CurvePreviewDefaultHeight` | 1600 / 900 | the preview size before the view has reported its gallery area: a presumption about the screen |
