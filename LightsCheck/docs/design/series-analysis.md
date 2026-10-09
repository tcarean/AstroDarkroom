# Analysing the series

**Covers:** `src/LightsCheck.Core/Analysis/Registration.cs`, `src/LightsCheck.Core/Analysis/Dithering.cs`, `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs`, `src/LightsCheck.Core/Analysis/Wcs.cs`, `src/LightsCheck.Core/Analysis/NoiseAnalyzer.cs`, `src/LightsCheck.Core/Runs/SeriesRun.cs`, `src/LightsCheck.Core/Report/SeriesReport.cs`, `src/LightsCheck.Core/Report/SeriesOutputs.cs`.

The behaviour is R-018 to R-028; the dither definitions and the verdicts are ADR 0007. The
prototype is `prototype/reg.py` and `prototype/summary.py`; `docs/validation/2026-10-06-veil/raport.md`
is the hand report the tool reproduces, and V-008 pins its session figures.

## The chain (`SeriesRun`)

The entry point both applications call is `SeriesRun.Run(folder, SeriesOptions(PixelSizeUm,
Threads), progress, cancel)`: the options record carries the pixel size (default
`D3300PixelUm`, 3.92 um) and the thread count (0 = half the cores), `progress` is an
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
2. The reference is the first measured frame; every measured frame is registered to its
   600-star list, in parallel.
3. `CadenceAnalyzer` (pauses), `DitherAnalyzer` (positions and pauses), the read noise from
   the first two biases, the RA axis from a plate-solved FITS at the root, then
   `SeriesAnalyzer`.
4. The pixel scale is `206.265 x pixel_um / focal_mm` from the first frame's focal length and
   the file's `XPIXSZ` or the option's pixel size.

## The constants (`Th`)

The series' constants are fields of `Th` in `Thresholds.cs` like the frame's
(`frame-analysis.md`), each with a doc comment, the value there only. Registration,
`VoteStars`, `VoteRange`, `VoteBin`, `MatchToleranceCoarse`, `MatchToleranceFine`,
`MatchIterations`, `MinMatches`; dithering, `DitherStep`, `DitherPause`, `DitherSizeBounds`,
`AxisStep`, `CoverageTight`, `CoverageMid`, `CoverageLoose`, `RunMinFrames`,
`HotPixelRunMax`, `WalkingNoiseDistinctFraction`, `WalkingNoiseIsolatedFraction`,
`TwoDAxisFraction`; frame classes, `ElongationBad`, `ElongationMarginal`, `SoftFwhmFactor`,
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
outside a run), for the table cell and the map ring of R-034 (ADR 0011).

## Classes, trends, drift, axes (`SeriesAnalyzer`)

Two passes: the classes that need no session figure (trailing, from the median elongation)
and the session medians (FWHM over the good frames, stars over all), then the flags against
the session (soft, clouds, background jump against the two neighbours on each side). Trends
compare the first and the last 10 good frames (fewer on a short session: half the good
frames). Drift is the mean step per minute over the consecutive registered pairs that were
not moves, so a dither does not count as drift; on Veil it is the 0.8 px/min along RA the
hand report found. Rotation is the last registered frame's minus the first's; the corner
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
(R-038): the pointing moved by minus the stars' shift, and because Siril writes its FITS rows
bottom-up while the raws are read top-down, the WCS is applied to `(-StepX, +StepY)`, giving
degrees of RA (scaled by cos DEC, as the matrix is) and DEC, each divided by its own scale
into pixels, positive east and north (ADR 0013, measured on Veil with two plate-solved raws). Without a WCS but with the axis from the trails, the move
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
`k/n de la f` cell of a run, `-` outside one), `RejectedRows` and `RejectedHeading` the
RESPINSE block, `CropCaption` the caption of a star cut-out (`StarCrops` cuts, this file
words). `Reason` words a trailing flag as `alungire v (prag t)` plus `TrailSuffix`: `, dara pe
RA (a grade)` when the flag has an axis (`AxisWord`), `, dara la a grade` when it has only the
angle. `RejectedExplanation` is the list of `ReportParagraph`s the window shows under
RESPINSE, one with `Emphasis` for the bold line, built once from `Th` so the thresholds it
quotes follow a change there. The CSV adds `trail_on` (`RA`, `DEC`, `oblic` or empty), the
three `spot_run_*` columns and the three `move_*` columns (signed pixels east and north, and
the arrow words). `MoveCell` words a `MountMove` as `RA 15 px V (dreapta), DEC 12 px S (jos)`:
a component under `Th.AxisStep` is dropped, the sense letter and the SynScan default arrow
appear only when the move is signed, `MoveArrows` gives the arrows alone for the CSV (quoted
there, two arrows being joined with a comma); both read the one `MoveComponents` rule;
`FrameRows` also fills `StepTip`, the bar tooltip `cadrul 5: pas 15.3 px; <cell>`, null when
the frame has no step.

Beside the rows, the report gives the window shapes it cannot derive from prose (ADR 0012):
`AxisLabels` and `PositionHeaders` word the RA/DEC axis for the map and the table headers;
`DitherTables` builds the five `ReportTable`s (how often, how far, direction, coverage, runs)
from the same `DitherAnalysis` figures the `DitherRows` print, through the helpers both use
(`SizeWord`, `FramesWord`, `TimesWord`, `PausesRow`, `LongestRow`, `HowOften` for the
no-move words) so a wording lives once; the runs table has its own `RunRows` and
`MinutesCell` beside the rows' `Runs` and `Minutes`, which differ in shape, not in words.
`DitherScalarRows` is the DITHERING rows that are not tables, plus the `axe` row that names
the RA axis and its source, which the console carries on its `directie` row. FOCUS closes with a
`verdict` row from the same `Focus` pair the summary uses. The console prints `DitherRows`
and never the tables.

The report prints the session header shared with the inventory, then the sections the
owner's hand report had, in the same order, with every verdict followed by its numbers. The
CSV holds every figure per frame with the invariant culture; the rejected list is the file
names of the bad and unreadable frames, one per line, for Siril's sequence selection by hand.
