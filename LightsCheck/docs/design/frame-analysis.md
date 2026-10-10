# Measuring one light frame

**Covers:** `src/LightsCheck.Core/Analysis/FrameAnalyzer.cs`, `src/LightsCheck.Core/Analysis/Raster.cs`, `src/LightsCheck.Core/Analysis/Components.cs`, `src/LightsCheck.Core/Analysis/StarShape.cs`, `src/LightsCheck.Core/Analysis/FrameMeasurement.cs`, `src/LightsCheck.Core/Thresholds.cs`, `src/LightsCheck.Core/Runs/FrameRun.cs`.

The behaviour is R-012 to R-017; the decision to reproduce the prototype step for step is
ADR 0006. This note says how the pieces fit, what each step does and why, and what was
measured. The prototype is `prototype/analyze.py`; its figures on the Veil session are
`docs/validation/2026-10-06-veil/frames.csv`, and V-002 to V-007 hold the C# figures on six
of those frames.

## The pipeline

`FrameRun` decodes the file (`ImageLoader.Load`), takes the scale the file carries or assumes
one from BITPIX, and hands `FrameAnalyzer` the double `Img`, which becomes a float `Raster`
above black at once (a 24 MP frame is 96 MB as floats; four rasters live at the peak: the
mosaic, the green, the residual and the smoothed residual, about 400 MB). Before the
mosaic goes, `FrameRun.Measure` cuts its central `PatternCrop` for the series' sensor
pattern (`series-analysis.md`); the single-frame run drops it.

1. **Channel levels** (`ChannelLevels`): the four CFA positions read as the prototype's
   `a[i::2, j::2][::4, ::4]`: the median of every 4th row and column of the position, the
   saturated count on every pixel of the position, the minimum. Names from `CfaPattern`.
2. **Green at full resolution** (`Raster.InterpolateGreen`): green pixels kept, the red and
   blue positions replaced by the mean of their 4-neighbours (3 or 2 at the edges), which is
   the prototype's `convolve(a*mask, k) / convolve(mask, k)` with the cross kernel. Without a
   colour order the raster is used as it is.
3. **Background** (`Raster.TileMedians` + `BilinearResize`): medians of 128 px tiles read at
   every 4th pixel, partial tiles at the bottom and right dropped (31 x 47 on 6016 x 4016),
   stretched back with scipy's `zoom(order=1)` mapping, the grid's corners on the raster's
   corners. The median and the extremes of the tile grid, the mean of the central 4 x 4 tiles
   and of the four 3 x 3 corners are the background figures.
4. **Residual and noise**: `d = g - bg`, in place in the green raster; sigma is 1.4826 x MAD of
   the central 2000 x 2000 window read at every 3rd pixel.
5. **Detection** (`Raster.Gaussian`, `Components.Find`): `d` smoothed with sigma 1 px (9-tap
   kernel, reflect border, the normalised centre weight 0.39894), its own MAD sigma on the same
   window, and the 4-connected components above 5 sigma, two-pass union-find. Each component
   carries area, box, the peak and flux on `d`, the peak on the raw raster above black (the
   saturation test: within 1 ADU of white - black) and the flux-weighted centroid.
6. **Trails**: components over 250 px whose box diagonal exceeds 80 px and whose area is under
   a quarter of the box plus 60: a line, not a blob.
7. **Stars**: components of 5 to 400 px. Bright = peak over 50 sigma. The 400 brightest by
   flux among those with a peak over 20 sigma and under 0.8 of the scale get `StarShapes.Measure`:
   three centroid refinements in a circle of radius 6 px on `d` clipped at zero, then the second
   moments; FWHM from the mean of the two eigenvalues, elongation from their ratio, angle from
   `0.5 atan2(2 mxy, mxx - myy)` in the image frame (x right, y down). Kept when the FWHM is in
   1..15 px. The halo of the 40 brightest: annulus (5, 10] over core 3 on the unclipped `d`.
8. **Summary**: percentiles with numpy's linear interpolation; the angle as the vector mean of
   the doubled angles of the stars over 1.2 (coherence = the vector's length, 1 = every star
   agrees), reported with 6 voters or more; three radial zones by fractions of the
   half-diagonal that equal the prototype's 1000 and 2200 px on the D3300 frame.
9. **Registration list**: the 600 brightest star components by flux, saturated ones included,
   with their centroids; step 3 registers frames with it.

## The constants (`Th`)

Every number above is a field of `Th` in `Thresholds.cs`, grouped by requirement, each with
a doc comment that says what it bounds; the value lives there only (ADR 0006). The series'
constants, the drift-aware walking verdict's and the pattern's included, are listed in
`series-analysis.md`. The names here, so a change is made by name: background and noise, `BackgroundTile`, `BackgroundSampleStep`,
`NoiseWindowHalf`, `NoiseSampleStep`, `DetectionSmoothSigma`; detection and shape,
`DetectionSigmas`, `MinStarArea`, `MaxStarArea`, `BrightPeakSigmas`, `ShapePeakSigmas`,
`ShapePeakMaxFraction`, `ShapeStars`, `MomentRadius`, `MomentIterations`, `MinFwhm`,
`MaxFwhm`, `ElongatedRatio`, `AngleVoteRatio`, `AngleVoteMin`, `CentreZoneFraction`,
`MidZoneFraction`, `ZoneStarsMin`, `HaloStars`, `HaloCore`, `HaloInner`, `HaloOuter`,
`SaturationMargin(top)`; trails, `TrailMinArea`, `TrailMinLength`, `TrailMaxFill`,
`TrailFillSlack`; the star list for registration, `RegistrationStars`. The series' own
constants are listed in `series-analysis.md`.

## What was measured

On six Veil frames (1, 4, 17, 23, 72, 83) the C# figures equal the prototype's: the detected
and bright counts to the unit, the channel medians and the background to the ADU, the noise
sigma to 0.1, the median FWHM and halo to 0.01, the elongation to 0.02 (the prototype measured
400 stars where the C# measures 397 to 399: a star whose moment window leaves the raster is
dropped a little earlier here). One frame runs in under five seconds end to end (4.7 s for
`--frame 4` on 2026-10-09, including the process start and the libraw decode), single-threaded.

The prototype counted saturated stars on the background-subtracted raster, so it always found
zero; the C# counts them on the raw raster (frame 1: 227 components touch the top of the
scale). That is a correction, not a drift, and is not a validation figure.
