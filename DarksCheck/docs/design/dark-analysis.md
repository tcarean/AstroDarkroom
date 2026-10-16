# The dark series: the stack, the masters, checks 2 to 8

**Covers:** `src/DarksCheck.Core/Analysis/Planes.cs`, `src/DarksCheck.Core/Analysis/FrameStack.cs`, `src/DarksCheck.Core/Analysis/DarkSeriesAnalyzer.cs`, `src/DarksCheck.Core/Analysis/BiasAnalyzer.cs`, `src/DarksCheck.Core/Analysis/DarkVerdicts.cs`, `src/DarksCheck.Core/Runs/DarkSeriesRun.cs`, `src/DarksCheck.Core/Report/DarkReport.cs`, `src/DarksCheck.Core/Report/DarkOutputs.cs`, `src/DarksCheck.Cli/runtimeconfig.template.json`.

The behaviour is R-020 to R-041; the decisions are ADR 0007 (the stack in memory, the FITS
black, the verdict thresholds) and ADR 0004 (the normalisation). This note says how the
pieces fit and what was measured against the prototype (`prototype/analyze_darks.py`,
`supplementary.py`, the figures in `docs/validation/2026-08-12-smida/`).

## The stack

`FrameStack.Load` decodes a group of files into 16-bit mosaics, `threads` at a time. The
readers return a raster of doubles (194 MB at 24 MP); it is converted to `ushort` as soon as
it lands and dropped, with a forced collection per frame, since the collector otherwise lets
those rasters pile up (14 GB of working set measured before the collection and the
`System.GC.ConserveMemory=9` knob of the console's runtime config). The first file in order
is decoded alone and sets the geometry and the scale; the others decode in parallel and must
match its size. A FITS carries no black level, so the black of a FITS stack is the median of
the first decoded frame's reference plane, or the black the biases' stack established when
`DarkSeriesRun` read the biases first (a dark's median would overstate the pedestal by the
dark current). Above `Th.MaxFramesInMemory` only every k-th file is decoded. `Norm` is the
normalisation factor of ADR 0004, read from the first frame's ISO and exposure.

## The planes and the estimators

`Planes` works on one CFA position at a time, p = (y &amp; 1) * 2 + (x &amp; 1), named by
`CfaPattern.Name`. `ClippedSigma` is the prototype's `mad_sigma` with its subsampling (every
k-th value so at most 3 million are read): a median absolute deviation returns 0 on the
D3300's 2 ADU steps, so the noise is a population std after three passes that drop what lies
over 5 sigma (at least 5 ADU) from the running centre. `Median` and `Percentile` are numpy's
(the mean of the two middle values; linear interpolation at rank p / 100 x (n - 1)), by
quickselect on a copy. `BlockMean` bins a plane in `Th.BinPx` squares; `Regions.Of` reads
the nine areas of a map the way the prototype did (centre 40 .. 60 %, corners and edges a
tenth each).

## The masters and the frames

`DarkSeriesAnalyzer.Analyze` sorts the N values of every pixel for the median master (the
mean of the two middle values when N is even, as numpy), accumulates the mean master and the
temporal std in the same pass, row-parallel. Each plane of the median master gives
`MasterFigures` (the counts at `Th.HotLevelsAdu` in ADU-eq, so the Smida master counts above
19.9 ADU at 119.2 s: 443 where the prototype's raw 20 ADU gave 433, ten pixels sitting at
exactly 20.0). The mean master gives the binned maps; the median master's excess, clamped to
the hot threshold so one 600 ADU pixel does not lift its row, gives the structured banding
(profile minus a running median over `Th.BandingWindow`). Every frame then gets its
`PlaneFigures`, its residual against the median master with the row and column banding
against white noise, the median and quartiles of its amplitude at the master's hot pixels
(above the threshold and below `Th.AmplitudeMaxFraction` of white), and the edges of its
binned reference plane against the centre. The stability of the hot pixels counts, per hot
pixel, the frames where it stays above half the threshold; the random-telegraph candidates
are the reference plane's pixels whose temporal std exceeds `Th.RtsSigmaFactor` times the
median per-frame sigma (the prototype's `sig1`). The noise curve averages the first n frames
against the next n on the central crop, hot pixels masked out, and compares with
sigma / sqrt(n).

## The biases and the dark current

`BiasAnalyzer` reads a bias stack the same way, takes the read noise per plane from
consecutive pairs (the clipped sigma of the difference over sqrt(2), the median over the
pairs), the mean master and its maps, and the clipping check on the first frame's reference
plane. `DarkMinusBias` subtracts the two binned mean maps: the dark current per block, read
in regions; its median on the Smida session is 0.14 ADU, below one quantisation step, which
is why a dark's value lies in its hot pixels (the report of 2026-10-11).

## The verdicts

`DarkVerdictAnalyzer.Judge` turns the figures into six verdicts with the constants of
`Th`: the leak from the largest region departure of the dark-minus-bias map (or the dark
master without biases), its sign telling light from a readout offset; the drift from the
warming of the series; the noise from the residual sigma outliers, with the temporal noise
over the read noise beside it; the pattern from the worst structured banding and the glow;
the dead and saturated counts; and whether the frames are enough: at least `Th.MinDarks`, and
the median master's predicted noise (`Th.MedianNoiseFactor` x the temporal sigma / sqrt(N))
under the quantisation step. The hot levels have a floor of `Th.MinHotAdu` raw ADU, the dead
limit is raw, and the biases' plane figures are not normalised. `DarkReport` prints the
words; `DarkOutputs` writes the report, two CSV files and three grey PNG maps of the
reference plane, and refuses a folder inside the session. A frame the cap skipped is listed
as `sarit`, not as an error, and the exit code ignores it.

## Measured

On the owner's machine (8 cores, 32 GB; 4 decoding threads), 2026-10-11: the Smida session
(30 biases then 20 darks, 24 MP NEF) runs in 35 s with a peak working set of 4.5 to 4.7 GB
once the memory knob is set (14 GB without it), the 30-frame library in 24 to 26 s and 2.9 to
3.3 GB (runs of 2026-10-11 and 2026-10-16). The figures
agree with the prototype on every line of `docs/validation.md` (V-003, V-004) to the
tolerances recorded there, the definitional differences listed in its History.
