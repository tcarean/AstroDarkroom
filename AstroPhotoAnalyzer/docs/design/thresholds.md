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
| `LowSeparationLstar` | 15 | below: paper separates, the eye barely |
| `HighlightLstar` | 50 | above: bright |
| `PaperBlackWarnPct` / `PaperBlackOkPct` | 20 / 2 | % below paper black: problem / safe |
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
