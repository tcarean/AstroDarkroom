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
pattern (`series-analysis.md`); the single-frame run drops it. `Measure` writes every figure
of the measurement to the diagnostic log at Debug, and times its `load`, `measure` and `crop`
stages on the clock `FrameRun.Run` hands it (`logging.md`).

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
   a quarter of the box plus 60: a line, not a blob. A trail is taken out of what may be a
   star, whatever its area (R-015).
7. **Stars**: the components of at least 5 px that are no trail, in flux order, are the
   candidates. A candidate *votes* when its peak is over 20 sigma and under 0.8 of the scale
   and it is not saturated (the peak is the one on `d`, above the sky, so a clipped star under
   a sky of 20 % of the scale or more would pass; a star whose raw peak is within 1 ADU of the
   top, the saturated count's test, is left out as well; the prototype has the first test
   alone, and on a sky of 3 % of the scale, the Veil's, the two agree).
   *The first width* (`FirstWidth`, ADR 0059): the 400 brightest voting candidates, of any
   area, get `StarShapes.Measure`, and the median of their core widths, with no upper bound,
   is the stars' width before the list of stars is known. It sets the two limits that follow
   the stars' width, neither of which falls under what it was in pixels: a candidate is a star
   up to `Th.MaxStarAreaWidths` (20) times the square of the first width, at least
   `Th.MaxStarAreaFloor` (400 px), and a shape is kept up to `Th.MaxFwhmWidths` (3) times the
   first width, at least `Th.MaxFwhmFloor` (15 px). A frame where no candidate was measured
   keeps the two floors. Being a median, the first width is the stars' while the bright
   patches that are no stars are fewer than the bright stars. `Shape` keeps what `Measure` read of a component, so the 400 brightest
   voting stars, which then get their shape, are not measured twice: the first width costs
   only the candidates that turn out too large to be stars, clumps and cloud for the most part.
   Bright = peak over 50 sigma.
   `StarShapes.Measure` takes two readings of each star, neither with a size in pixels of its
   own (ADR 0056).
   *The core*, `StarShapes.Core`, by adaptive moments. A Gaussian weight starts round at the
   component's centroid, its sigma the radius of a disc of the component's area over
   `Th.CoreStartDivisor`. Each round sums the star's zeroth, first and second moments under
   the weight (out to `Th.CoreWeightReach` of its sigmas), takes the star's inverse covariance
   as the moments' less the weight's, sets the weight to that star, and moves the centre by
   twice the weighted offset; for a Gaussian star the weight ends as the star itself. When
   the difference is no covariance (noise, a star that is no Gaussian) the weight is set to
   twice the moments, which has the same fixed point. A round may grow the weight by
   `Th.CoreGrowthLimit` at most; the rounds end when the weight changes by under
   `Th.CoreTolerance` and the centre by under `Th.CoreCentreTolerance`. The core gives the
   centre, the flux (twice the weighted sum), the FWHM from the mean of the two eigenvalues and
   the core's elongation from their ratio. A weight that grows past `Th.CoreDivergedFwhm`, a
   centre that leaves the component's centroid by more than the first width, or
   `Th.CoreIterations` rounds without agreement: the star is not measured. Because the weight
   falls off as the star does, the sky's noise far from the star and the star's neighbours
   count for nothing, which is what made a wide fixed circle fail (ADR 0052).
   *The whole star*, `StarShapes.Whole`: a ring outside a circle of `Th.WholeStarRadiusWidths`
   core widths gives the local sky (its median) and the local noise (1.4826 x its MAD); the
   pixels of the circle at least `Th.WholeStarSigmas` of that noise above that sky give the
   second moments, around a centre refined `Th.WholeStarCentreIterations` times: elongation
   from the ratio of the eigenvalues, angle from `0.5 atan2(2 mxy, mxx - myy)` in the image
   frame (x right, y down). A tail or a second copy of the star counts; the noise does not,
   since no pixel of it passes the cut but by chance, so a faint round star reads round.
   The two elongations agree on a star stretched as a whole and part on a sharp star with a
   tail or a second copy, which is what the classes read (R-090, `series-analysis.md`).
   The circle and the ring must lie inside the raster, so a star nearer the edge than about
   five of its widths is not measured.
   Kept when the FWHM is at least 1 px and within the limit of width above. The halo of the 40
   brightest: for each star the flux in the annulus from `Th.HaloInnerWidths` (2) to
   `Th.HaloOuterWidths` (4) of its own core width over the flux within `Th.HaloCoreWidths`
   (1.2) of it, on the unclipped `d` (`StarShapes.Halo` takes the three radii in pixels). A
   star nearer the border than four of its widths has no halo and is not replaced among the 40. The
   rings are each star's, not the frame's: a fast lens draws wider stars in the centre than at
   the edge, and rings of the frame's one width left the halo following the width there
   (ADR 0059).
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
`DetectionSigmas`, `MinStarArea`, `MaxStarAreaWidths`, `MaxStarAreaFloor`, `BrightPeakSigmas`, `ShapePeakSigmas`,
`ShapePeakMaxFraction`, `ShapeStars`, the core's `CoreStartDivisor`, `CoreStartMinFwhm`,
`CoreStartMaxFwhm`, `CoreIterations`, `CoreTolerance`, `CoreCentreTolerance`, `CoreWeightReach`,
`CoreGrowthLimit`, `CoreDivergedFwhm`, `CoreCentreDriftMin`, the whole star's
`WholeStarRadiusWidths`, `WholeStarMinRadius`, `WholeStarSigmas`, `WholeStarCentreIterations`,
`LocalRingGap`, `LocalRingWidthFraction`, `LocalRingMinWidth`, then `MinFwhm`,
`MaxFwhmWidths`, `MaxFwhmFloor`, `ElongatedRatio`, `AngleVoteRatio`, `AngleVoteMin`, `CentreZoneFraction`,
`MidZoneFraction`, `ZoneStarsMin`, `HaloStars`, `HaloCoreWidths`, `HaloInnerWidths`, `HaloOuterWidths`,
`SaturationMargin(top)`; trails, `TrailMinArea`, `TrailMinLength`, `TrailMaxFill`,
`TrailFillSlack`; the star list for registration, `RegistrationStars`. The series' own
constants are listed in `series-analysis.md`.

## What was measured

Since 2026-11-15 the shapes are no longer the prototype's (ADR 0056): the widths and the
elongations below are those of the first versions, kept as the record of that comparison; the
counts, the levels, the background and the noise still equal the prototype's. The same day the
limits of what is a star and the halo's rings began to follow the stars' width (ADR 0059, which
holds what was measured): the halo is no longer the prototype's, and the star counts are its
own only on a frame whose stars are under 4.5 px wide and where no trail was also a star.

On six Veil frames (1, 4, 17, 23, 72, 83) the C# figures equalled the prototype's: the detected
and bright counts to the unit, the channel medians and the background to the ADU, the noise
sigma to 0.1, the median FWHM and halo to 0.01, the elongation to 0.02 (the prototype measured
400 stars where the C# measures 397 to 399: a star whose moment window leaves the raster is
dropped a little earlier here). One frame runs in under five seconds end to end (4.7 s for
`--frame 4` on 2026-10-09, including the process start and the libraw decode), single-threaded.

The prototype counted saturated stars on the background-subtracted raster, so it always found
zero; the C# counts them on the raw raster (frame 1: 227 components touch the top of the
scale). That is a correction, not a drift, and is not a validation figure.
