# Analysing the series

**Covers:** `src/LightsCheck.Core/Analysis/Registration.cs`, `src/LightsCheck.Core/Analysis/Dithering.cs`, `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs`, `src/LightsCheck.Core/Analysis/Wcs.cs`, `src/LightsCheck.Core/Analysis/NoiseAnalyzer.cs`, `src/LightsCheck.Core/Analysis/PatternAnalyzer.cs`, `src/LightsCheck.Core/Analysis/PixelPitch.cs`, `src/LightsCheck.Core/Runs/SeriesRun.cs`, `src/LightsCheck.Core/Report/SeriesReport.cs`, `src/LightsCheck.Core/Report/SeriesOutputs.cs`.

The behaviour is R-018 to R-028, R-042 to R-046, R-049, R-050 and R-084; the dither definitions and the verdicts are ADR 0007, the drift-aware walking verdict and the sensor's pattern ADR 0015, the drift pairs and the polar alignment ADR 0016, the verdict by the frames whose streak would show and the pattern that piles ADR 0039. The
prototype is `prototype/reg.py` and `prototype/summary.py`; `docs/validation/2026-10-06-veil/raport.md`
is the hand report the tool reproduces, and V-008 pins its session figures.

## The chain (`SeriesRun`)

The entry point both applications call is `SeriesRun.Run(folder, SeriesOptions(PixelSizeUm,
Threads), progress, cancel)`: the options record carries the pixel size the user typed (null
when none was typed) and the thread count (0 = `SeriesOptions.DefaultThreads`, half the cores), `progress` is an
`IProgress<(int Done, int Total)>` reported once per measured frame, `cancel` a
`CancellationToken`; the result is a `SeriesResult` with the session, the `AnalyzedFrame`
list (each with its metadata, measurement, registration and any error), the cadence, the
`DitherAnalysis`, the `SeriesAnalysis` (classes, trends, drift, axes, noise), the pixel size
with its source, the arcseconds per pixel and the 1-based reference index; `HasErrors` says
whether any frame could not be read.
`FrameRun.Run(folder, index)` and `InventoryRun.Run(folder)` are the other two entry
points (`frame-analysis.md`, `session-and-console.md`).

1. Every light is decoded and measured in parallel (`Parallel.For`, half the cores by
   default; each frame holds about 400 MB at its peak, so four in flight is 1.6 GB). A frame
   that throws becomes an `AnalyzedFrame` with its error and the metadata read without pixels.
   `FrameRun.Measure` also hands back the frame's `PatternCrop` (1 MB), kept in an array
   beside the frames until the pattern is read.
2. The reference is the first measured frame; every measured frame is registered to its
   600-star list, in parallel.
3. `CadenceAnalyzer` (pauses), `DitherAnalyzer` (positions and pauses), `PatternAnalyzer` on
   the crops (then the crops are dropped), the read noise from the first two biases, the RA
   axis from a plate-solved FITS at the root, then `SeriesAnalyzer`.
4. The pixel scale is `206.265 x pixel_um / focal_mm` from the first frame's focal length and
   the pixel size `PixelPitch.Choose` picks (R-066, ADR 0026): the first frame's `XPIXSZ`, else
   the typed size, else the first raw's own figure, else `SeriesOptions.D3300PixelUm`. The
   raw's figure (`PixelPitch.FromCrop`) is `43.2666 mm x focal / (focal35 x sqrt(W² + H²))`:
   the camera's 35 mm-equivalent focal length over the shot's is the crop, a ratio of sensor
   diagonals, so the full-frame diagonal over the crop is the sensor's diagonal, spread over the
   mosaic's diagonal in pixels. `Choose` returns a `PixelPitchSource`; `SeriesRun` turns it into
   the words of the `scale` line, so the choice is `Analysis/`'s and the words stay in the run.

The run times its stages for the diagnostic log (`open`, `measure`, `register`, `dither`,
`pattern`, `noise`, `analyze`), writes a line per frame measured from the frame's worker thread,
and at the end writes the figures of every step above at Debug, the source of the pixel size by
its `PixelPitchSource` key, not by the scale line's words (`logging.md`).

## The constants (`Th`)

The series' constants are fields of `Th` in `Thresholds.cs` like the frame's
(`frame-analysis.md`), each with a doc comment, the value there only. Registration,
`VoteStars`, `VoteRange`, `VoteBin`, `MatchToleranceCoarse`, `MatchToleranceFine`,
`MatchIterations`, `MinMatches`; dithering, `DitherStep`, `DitherPause`, `DitherSizeBounds`,
`AxisStep`, `CoverageTight`, `CoverageMid`, `CoverageLoose`, `RunMinFrames`,
`HotPixelRunMax`; the drift-aware verdict and the pattern, `WalkingStreakVisible`,
`WalkingSharedFraction`, `PatternPilingFactor`,
`PatternCrop`, `PatternHotSigma`, `PatternMinFrames`, `PatternClusterFraction`,
`PatternSharePx`; the drift pairs and the polar alignment, `DriftMinPairs`,
`PolarDriftFwhmFraction`, `PolarRotationMinHours`; frame classes, `ElongationBad`, `ElongationMarginal`, `SoftFwhmFactor`,
`CloudStarFraction`, `BackgroundJumpPct`, `BackgroundNeighbours`; trends, `TrendFrames`,
`DewFwhmRise`, `DewHaloRise`, `FocusHeldFraction`, `SwampingRatioOk`; the RA axis from the
trails, `TrailAxisMinFrames`, `TrailAxisCoherence`, `TrailAxisTolerance`.

## Registration (`Registration`)

The prototype's `reg.py`: the offsets between the 300 brightest stars of each list vote in
a 2D histogram (8 px bins, +-2000 px); the peak is the coarse shift. Then four rounds: every
reference star is transformed by the current similarity, its nearest frame star within the
tolerance (8 px, then 3 px) found through a hash grid, and the pairs fit a similarity by the
normal equations of (a, b, tx, ty) solved with the copied `Stats.Solve`. The reported shift is
where the reference's centre lands in the frame, so a dither that moved the mount left shows
as a positive Dx. Rotation is `atan2(b, a)`. With fewer than 10 matches the frame has no
position; on the Veil frames 500 to 550 of 600 match with an RMS of 0.3 to 0.8 px.

## Dithering (`DitherAnalyzer`)

Steps are differences of consecutive positions; a step needs both frames registered. A move
is a step of `Th.DitherStep` (5 px) or more (ADR 0007). The tables are what ADR 0007 lists. Coverage is O(n^2) over the
registered frames (87 frames: nothing). The greedy clustering seeds a cluster with the first
frame no earlier seed covers; it is the prototype's, order-dependent by design (the first
frame of a spot names it). Runs at a radius are stretches of 3 or more consecutive frames
within the radius of the stretch's first frame. `SpotRun` is the per-frame view of the runs at
10 px, filled from `runsLoose` (the ordinal in the run, its length, its first frame; null
outside a run), for the table cell and the map ring of R-034 (ADR 0011). `Clusters` is the
greedy clustering behind the distinct counts; its sizes also give `LargestClusterTight`, the
biggest group of frames at 3 px, which the pattern analyzer reads to know whether a pixel can
have seen a star in half the frames. `SharingPairs` counts the pairs of registered frames
closer than `Th.PatternSharePx`, and `PatternSurviving` is `(n + 2 pairs) / n^2`, how much of
one frame's pattern the mean of the stack keeps (R-048): the n copies of 1/n each, plus the
pairs that put the same pixels on the same pixels.

## The drift over an exposure and the walking verdict (`SeriesAnalyzer`, R-043)

After the drift per minute, `SeriesAnalyzer` takes the median exposure of the frames that
carry one and turns the drift vector into `DriftPerExposurePx`; `DriftAxis` is the image axis
with the larger component (the report words it RA or DEC through `RaAxis`, else X or Y).
`MedianExposureSeconds` is kept for the report. `DitherAnalysis.WalkingStretches` is the
result of `Runs` with a minimum of two frames at `Th.CoverageLoose`, every stretch of
frames in a row at one spot, and `WalkingStretch` the longest of them; a pair of frames at
one spot with a large drift between them counts, which the runs of three did not. The dash,
`WalkingStreakPx`, is the drift per exposure times the frames minus one of the longest
stretch. The verdict reads the stack, not the map (R-084, ADR 0039): `WalkingSharedFrames`
sums the frames of the stretches whose own dash reaches `Th.WalkingStreakVisible`, the
frames whose streak would show, and `WalkingSharedFraction` divides by the registered
frames; `PatternPiles` is `PatternSurviving` above `Th.PatternPilingFactor` over the registered
count (the pattern piles in place instead of walking), decided here once and read by the
report; `WalkingNoise` is `Insufficient` from `Th.WalkingSharedFraction` up, else `Static`
when the pattern piles, else `Ok`; `Unknown` under two registered
frames, and without a drift no dash is known, so no frame is counted and the pattern alone
decides. The `Static` word is the one Smida gets: a drift of a seventh of a pixel per
exposure moves no pattern, while 23 % of it stays in the stack. The report's `DitherVerdict`
orders the walking and the hot-pixel words worst first, so the chip's colour is the worse of
the two; its text is `StreakClause` (the dash, the frames that share a visible one or the
size under which none shows, the drift and the longest stretch) then `PatternClause` (what
stays against the ideal and the piling limit), then the counts of R-021 as information.

## The polar alignment (`SeriesAnalyzer`, R-050)

A pole off by a small angle `e` (components `e_alt` along the meridian and `e_az` east-west)
makes the mount track about the wrong axis; to first order, with `w` the sidereal rate
(902.46 "/min, the private constant), `H` the hour angle and `d` the declination, the stars
drift in DEC at `w (e_alt sin H - e_az cos H)`, in RA at `w sin d (e_alt cos H + e_az sin H)`,
and the field turns at `w cos d (e_alt cos H + e_az sin H)`. The squares of the two brackets
add to `|e|^2` at any hour angle, which the raws do not carry, so the analyzer reads the two
brackets and never `H`. `PolarErrorMinArcmin` is the DEC component of `StarDriftPerMin`, in
arcseconds through the scale, over `w`: a bound from below that needs only the RA axis and the
scale. `PolarErrorMaxArcmin` adds in quadrature the tracking's rotation rate over `w cos d`, the
second bracket read as the alignment's; it needs `d`, `Wcs.DecDeg` from the solve's `CRVAL2`,
and is skipped within 0.01 of `cos d = 0`. The tracking's rate, `PolarRotationPerHourDeg`, is
not the raw end-to-end rotation: every slew about the mount's RA axis turns the frame by its
RA step times `sin d` (the hour circles converge; `tan d` per radian of arc), and on Veil the
dithers' net slew made most of the 8.45' the registration saw (regressing the per-frame
rotation on time and on the pointing's RA arc gives -0.51 to -0.59 rad per radian in that
frame, `tan d` being 0.62, and drops the per-frame scatter from 33" to 6-12"). So the
analyzer sums the moves' RA components (R-038) minus the drift over each move's interval, the
slews proper, and subtracts their convergence, `-Wcs.Handedness x tan d x` the net arc, from
the total rotation before dividing by the span; the drift's own projection stays in, since the
bound reads the whole tracking rotation as the alignment's. `Wcs.Handedness` is the sign of
the determinant of the pixel-to-sky map through `PointingOffsetDeg`, the lights' flip
included, which a mirrored frame flips together with the registration's rotation sign. The
rate is read only over `Th.PolarRotationMinHours` of registered span: the per-frame rotation
still scatters by about 10" and a short run would
read that as arcminutes per hour. The same reading bounds the RA drift the alignment can
explain at the rotation rate times `tan d` (`PolarRaDriftMaxArcsecPerMin`); on Veil 1.3 of
the 5.8 "/min, the rest being the tracking, which these frames cannot separate (ADR 0016).
The cost: `PolarDriftMinPx` is the DEC drift over the median exposure, `PolarDriftMaxPx` the
DEC drift and the RA share in quadrature over the same exposure (the rotation itself moves no
star at the centre), `PolarElongation` what a smear of the upper one adds to a Gaussian star
of the session FWHM, `sqrt(s^2 + L^2 / 12) / s` with `s = FWHM / 2.355`. The verdict reads
the drift against `Th.PolarDriftFwhmFraction` of the session FWHM: `Attention` when the lower
drift reaches it, `Ok` when the upper stays under it, `Marginal` between, `Unknown` without
the figures or with a harmless lower bound and no upper one. Everything is null without a
clean drift of at least `Th.DriftMinPairs` pairs, an axis, a scale or a median exposure. Refraction, which pushes the apparent
field toward the zenith at a fraction of an arcsecond per minute low in the sky, is not
modelled: the lower bound is that much soft on a field that sets.

## The sensor's pattern (`PatternAnalyzer`, R-046)

`PatternCrop.Cut` takes the central `Th.PatternCrop` square of the float raster above black
(the shorter side rounded down to even on a small frame), starting at an even row and column
so the crop's CFA phase is the frame's. `PatternAnalyzer.Analyze` keeps the crops of the first
crop's size, refuses with a status under `Th.PatternMinFrames` crops, under two registered
frames, or when `DitherAnalysis.LargestClusterTight` reaches `Th.PatternClusterFraction` of
the registered frames (the frames stayed put: a star would sit on the same pixels in half the
frames and the median would read it as pattern), and otherwise builds the per-pixel median
across the crops with `Raster.Median` on a reused buffer. The frames are not registered on
purpose: the sky and the stars move with the dither and the drift, the sensor does not, and
the median of n frames carries about 1.25 / sqrt(n) of one frame's noise, so a warm pixel of 3
sigma stands clear of it from 7 frames on. Per CFA position the median goes through
`NoiseAnalyzer.HighPass`, the residuals behind `HighPassSigma` (pixel minus the 5 x 5 median of
its own position, in the central window of that position, which on a 512 px crop is the whole
position plane minus a 2 px border); the position's one-frame sigma is the median over the
frames of `ChannelLevel.HighPassSigma`. The pattern's sigma is the clipped standard deviation
of those residuals with the median's own noise taken out in quadrature. That noise is
measured, not assumed: the medians of the odd and the even frames differ by the noise of two
half-medians and by no pattern, and `Var(median of n) = Var(odd - even) / (n (1 / n_odd +
1 / n_even))`; the analytic `sqrt(pi / 2) / sqrt(n)` of a Gaussian median was tried first and
over-subtracted on eight frames, where the small-n median is tighter than the formula. On 87
Veil frames about 3 of the 21 ADU of green noise survive in the median and the measured
3.7 ADU become 2.1 of pattern. A residual above `Th.PatternHotSigma` sigmas of the frame's noise is hot.
`SensorPattern` holds the position names, the sigmas, the frame sigmas, the hot count and
the pixels read; `FractionOfFrame` and `HotPerMpx` derive the fraction and the rate. There
is no count at a low threshold on purpose (ADR 0015: it sat on the steep tail). The raws carry the pattern whether
or not the session has darks; the report's `fixed pattern` row adds what the `darks/` folder says
(`SessionFolder.Darks`, counted and never read, R-042).

## Classes, trends, drift, axes (`SeriesAnalyzer`)

Two passes: the classes that need no session figure (trailing, from the median elongation)
and the session medians (FWHM over the good frames, stars over all), then the flags against
the session (soft, clouds, background jump against the two neighbours on each side). Before
the first pass the usual exposure is read (R-085, ADR 0040): the exposures over 0, rounded to
a tenth of a second, grouped, the largest group's value, the longer on a tie,
`UsualExposureSeconds`. In the first pass a measured frame under `Th.ExposureShortFraction`
of it gets `ShortExposure` and, after its elongation class is set, `Bad`; one farther than
`Th.ExposureSamePct` from it gets `OtherExposure` and keeps its class. A short frame's stars
stay out of the stars median, its FWHM out of the good median, and the second pass skips it
both as a subject (no soft, clouds or jump flag) and as a neighbour (its sky is left out of
the others' jump medians). A jump on a frame with `OtherExposure` whose size is within
`Th.BackgroundJumpPct` of `100 * (exposure / usual - 1)` carries `FrameFlag.ExposureDetail`,
which `SeriesReport.Reason` turns into the `, which its exposure explains` tail
(`ExposureSuffix`); `ShortFractionWord` words the fraction as `half` while it is 0.5. Trends
compare the first and the last 10 good frames (fewer on a short session: half the good
frames). Drift is the mean step per minute over the drift pairs: the consecutive registered pairs with
no pause above `Th.DitherPause` and no move between them, where nothing was pressed and the
mount did not jump (R-049). A pair after a pause is never a drift pair, even when its step
stayed under the move threshold: on Veil
nine such presses of 3 to 5 px, counted as drift, halved the RA drift and inflated the DEC
drift (ADR 0016); over the 39 clean pairs the drift is -0.19 / -0.96 px/min. With fewer than
`Th.DriftMinPairs` clean pairs (Smida, dithered every frame) the mean falls back to the pairs
that were not a move, as before, and `DriftApproximate` says so when that added pairs; the
private `Drift` helper sums the steps and the minutes under either rule. `StarDriftPerMin` is
the clean drift of at least `Th.DriftMinPairs` pairs as the stars move on the sky, a
`StarDrift` through the moves' mapping with the sign flipped (the stars are the negative of
the pointing), signed with a solve, magnitudes with the axis from the trails, null for an
approximate or a thin drift. Rotation is the last registered frame's minus the first's; the corner
figure is that rate over one exposure at the half-diagonal. The RA axis comes from the WCS
when a solved FITS sits at the root (Siril's result: `PC1_2` dominates `PC1_1`, so RA runs
along Y on the D3300 frames), else from the vector mean of the trail angles of the bad
frames, stated as an assumption. Once the axis is known, the trailing flag (bad or marginal)
of every frame whose coherence clears the same `Th.TrailAxisCoherence` is rewritten with the
axis its trail runs along (`TrailAxisOf`: RA within `Th.TrailAxisTolerance` of the image axis
that is RA, DEC within it of the other, oblique otherwise), and the series' mean angle gets
its own word (`TrailOn`); the flag keeps the angle as its `Detail`, so a frame without an axis
still reads its angle (R-033, ADR 0011). The per-frame axis is derived, not measured again: a
change of the axis rule moves every frame's word with it.

The solved FITS is read whole by `SeriesRun.ReadWcs` into a `Wcs` (`CDELT` and the `PC`
matrix, or the `CD` matrix with unit scales, the matrix then holding the scale); `RaAxis` is
the old rule on the matrix, `RaScaleDeg` and `DecScaleDeg` are each scale times the length of
its matrix row, so both forms give the same pixels. Each move (R-019) then gets a `MountMove`
(R-038): the pointing moved by minus the stars' shift, `(-StepX, -StepY)` in the lights'
pixels, handed to `Wcs.PointingOffsetDeg`, which reverses y when `FramesFlipped` is set:
`SeriesRun` sets it from `SessionFolder.LightsAreRaws`, since camera raws are read top-down
while Siril writes its FITS rows bottom-up, and FITS lights are read in the order the solve
is (ADR 0013, measured on Veil with two plate-solved raws; ADR 0023, the same session's
`pp_light_*.fit`). The result is degrees of RA (scaled by cos DEC, as the matrix is) and DEC,
each divided by its own scale into pixels, positive east and north. Without a WCS but with the axis from the trails, the move
keeps the magnitudes along the two axes with `Signed` false; without an axis it is null, as
is every non-move.

## Noise (`NoiseAnalyzer`)

The prototype's `noise2.py`. The read noise per CFA position is the clipped standard
deviation of the central 600 x 600 difference of two biases over sqrt 2 (the fixed pattern
cancels); the lights' total noise per position is the clipped standard deviation of the
pixel minus its 5 x 5 median in the same window, which overestimates sigma by a few percent,
so it is corrected by the ratio of the pair sigma to the same estimator on the bias. The shot
variance is total minus read; the swamping ratio shot over read variance; the penalty
`sqrt(1 + 1/ratio) - 1`. On Veil: 9 to 11 in red, 40 to 60 in green and blue, as the hand
report found.

## The report and the files (`SeriesReport`, `SeriesOutputs`)

Each section is a list of `ReportRow(Label, Value, Verdict)` built by a producer
(`SummaryRows`, `FocusRows`, `NoiseRows`, `DewRows`, `TrailRows`, `DriftRows`, `DitherRows`,
`RejectedRows`); `Write` prints every row as the label in a 19-column field, a colon, then the
verdict word and the value, and the window binds the same rows (ADR 0008). A section's text
therefore has one label column; a label longer than the field pushes its colon right.
`FrameRows` formats the table's cells once for the console and the window (`SpotCell` the
`k/n from f` cell of a run, `-` outside one), `RejectedRows` and `RejectedHeading` the
REJECTED block (the heading counts the bad and the unread frames through `Count`, singular for
one, R-081), `CropCaption` the caption of a star cut-out (`StarCrops` cuts, this file
words). `Reason` words a flag in plain words (R-079), a trailing one as `elongation v (from t
the frame is bad)`, or `is marginal`, plus `TrailSuffix`: `, trail on RA (a degrees)` when the
flag has an axis (`AxisWord`), `, trail at a degrees` when it has only the angle; the satellite
trails take their noun through `Count`, singular for one. `RejectedExplanation` is the list of `ReportParagraph`s the window shows
under REJECTED, one with `Emphasis` for the bold line, built once from `Th` so the thresholds it
quotes follow a change there. The CSV adds `trail_on` (`RA`, `DEC`, `oblique` or empty), the
three `spot_run_*` columns and the three `move_*` columns (signed pixels east and north, and
the arrow symbols). `MoveCell` words a `MountMove` as `RA 15 px ← W, DEC 12 px ↓ S` under
an `ArrowKeys` (which SynScan arrow moves the mount east and which north, the other two
following; `SeriesOptions.Arrows`, carried by the result, `ArrowKeys.Default` = right east and
up north, ADR 0014): a component under `Th.AxisStep` is dropped, the arrow symbol and the sense
letter appear only when the move is signed, `MoveArrows` gives the symbols alone for the CSV
(quoted there, two being joined with a comma); both read the one `MoveComponents` rule;
`FrameRows` also fills `StepTip`, the bar tooltip `frame 5: step 15.3 px; <cell>`, null when
the frame has no step.

Beside the rows, the report gives the window shapes it cannot derive from prose (ADR 0012):
`AxisLabels` and `PositionHeaders` word the RA/DEC axis for the map and the table headers;
`DitherTables` builds the five `ReportTable`s (how often, how far, direction, how close the
frames are, runs) from the same `DitherAnalysis` figures the `DitherRows` print, through the
helpers both use (`SizeWord`, `FramesWord`, `TimesWord`, `PausesRow`, `LongestRow`, `HowOften`
for the no-move words, `TypicallyWord` before the median move, and `NearestWords`,
`AloneWords` and `AreaWords`, which `DitherScalarRows` shares too) so a wording lives once;
the runs table has its own `RunRows` and `MinutesCell` beside the rows' `Runs` and `Minutes`,
which differ in shape, not in words. Every one of these texts is in plain words (R-077):
`PlainWords` in the tests holds the method's words they must not use.
`DitherScalarRows` is the DITHERING rows that are not tables, plus the `axes` row that names
the RA axis and its source, which the console carries on its `direction` row; both end with
`ScaleRow` (what a move of `Th.DitherStep` and `Th.CoverageLoose` is in arcseconds at the
session's scale, R-044), `SurvivingRow` (the surviving fraction against `1 / n`, R-048, its
sharing pairs worded by `SharingPairsWords` for none, one or more),
`PatternRow` (the `SensorPattern` by its status, the strengths with their fractions, the hot
rate and the darks clause, R-046) and the verdict. `DriftRows` adds `DriftPerExposure`, the drift over the median
exposure on its axis (`DriftAxisWord`) and the dash of `Streak`, `no walking-noise streak` without a run;
its first row names the pairs through `PairsWord` (`without a pause between them`, or `with a pause;
approximate` for the fallback, a single pair in the singular), its rotation row is `RotationWords`, and it closes with `PolarRow` (the bracket of `PolarBounds` with what gave
each bound, the drift per exposure and the elongation, or the lower bound alone without a
solve) and `RaDriftRow` (the RA drift with the stars' sense when signed, the alignment's share
and whether the rest is the tracking). `PolarReason` is the one place that words why the
alignment is not measured (no registered pair, under three drift pairs, no axis, no scale),
`NoUpperBound` why the lower bound stands alone (no solve, or a span under an hour), and
`PolarSummary` the SUMMARY row with its verdict word, `OK`, `MARGINAL` or `WARNING`, none
when unknown.
`DitherExplanation` is the list of paragraphs the window's Dithering card shows under its
tables (R-045), built from `Th` like `RejectedExplanation`, the third one emphasised. FOCUS closes with a
`verdict` row from the same `Focus` pair the summary uses. The console prints `DitherRows`
and never the tables.

The report prints the session header shared with the inventory, then the sections the
owner's hand report had, in the same order, with every verdict followed by its numbers. The
CSV holds every figure per frame with the invariant culture, a bare text cell quoted only when
a comma or a quote inside it would break the row (`Cell`); the rejected list is the file
names of the bad and unreadable frames, one per line, for Siril's sequence selection by hand.
