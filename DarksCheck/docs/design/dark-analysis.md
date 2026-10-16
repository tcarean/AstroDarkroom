# The dark series: the stack, the masters, checks 2 to 8

**Covers:** `src/DarksCheck.Core/Analysis/Planes.cs`, `src/DarksCheck.Core/Analysis/FrameStack.cs`, `src/DarksCheck.Core/Analysis/Normalisation.cs`, `src/DarksCheck.Core/Analysis/DarkSeriesAnalyzer.cs`, `src/DarksCheck.Core/Analysis/BiasAnalyzer.cs`, `src/DarksCheck.Core/Analysis/DarkVerdicts.cs`, `src/DarksCheck.Core/Runs/DarkSeriesRun.cs`, `src/DarksCheck.Core/Report/DarkReport.cs`, `src/DarksCheck.Core/Report/ReportRows.cs`, `src/DarksCheck.Core/Report/DarkOutputs.cs`, `src/DarksCheck.Cli/runtimeconfig.template.json`.

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
is decoded alone and sets the geometry and the scale (the files are tried in order until one
decodes); the others decode in parallel and must match its size, its ISO and its exposure
(each on its own, within `Th.ExposureTolerance`). A float FITS on 0 .. 1 is brought to 0 ..
65535, one already in ADU is kept with 65535 as the presumed white. A FITS carries no black
level, so the black of a FITS stack is the median of
the first decoded frame's reference plane, or the black the biases' stack established when
`DarkSeriesRun` read the biases first (a dark's median would overstate the pedestal by the
dark current). Above `Th.MaxFramesInMemory` only every k-th file is decoded. `Norm` is the
normalisation factor of ADR 0004, read from the first frame's ISO and exposure through the
`Normalisation` the caller hands in (`Normalisation.Default` when none): the record of the
two settings of ADR 0009, the gain per ISO stop (`NormOf`: `gain ^ log2(ISO / 400)`, the
linear `ISO / 400` when the gain is 2, times `exposure / 120`) and the degrees per doubling
(`Degrees`: `D x log2(ratio)`, NaN for a ratio that is not positive). The stack keeps the
record, `DarkSeriesAnalysis` carries it on, and the warming, the lights' and the partner's
degrees all read it, so a setting changed in the window moves every degree in the report.
`FrameStack.DecodeOne` is the single-file form for a group that is never stacked (the
lights): the same decode and 16-bit conversion, the frame listed with `dimensiune diferita`
when its size is not the darks', and the file's own black and white returned (NaN black for a
FITS, which the caller replaces).

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
reference plane, refuses a folder inside the session (the console checks that before the
decoding starts) and skips the PNG of a map without blocks. A frame the cap skipped, dark or
bias, is listed as `sarit`, not as an error, and the exit code ignores it; a bias that failed
is counted in an `ATENTIE` line of the darks header, since the darks run prints no bias table.
With a single frame the residual, the stability, the warming and the noise curve have nothing
to measure and the report says `NEMASURAT` rather than printing zeros.

## The lights, the index and the partner in the run

After the verdicts, `DarkSeriesRun` computes the index and the label of the darks' master
(`IndexAnalyzer.RankIndex`, `Label`), then, when the session has lights, hands their paths and
the darks' analysis to `LightsAnalyzer.Analyze` (the lights are decoded one at a time and
dropped), and, with `--against`, opens the partner folder, loads its darks into a `FrameStack`,
reduces them to a median master (`DarkSeriesAnalyzer.MedianMaster`) and matches the session's
master against it (`MatchPartner`): the partner is opened with `SessionFolder.Open` like the
input, so a session or a bare folder both work; a partner with no decodable frame is reported
by its file errors, one of another size by the two sizes, and a frame that fails or has other
settings is counted (`Failed`, `FirstError`) and makes the result an error; its
`PartnerAnalysis` carries the
first decoded frame's settings and the stack's normalisation, the index and label of its
master, the session's darks over it (`IndexAnalyzer.SharedRatios`, the partner as reference),
the lights over it (`IndexAnalyzer.Chain`), and the overlap of the two hot-pixel sets at
`Th.PartnerOverlapAdu` (50 ADU-eq): the session's pixels above 50 that the partner has above
50, and the partner's above 50 that the session has above `Th.HotAdu`, each at its own
normalisation. Each group is released before the next is decoded. A
failure of the lights or of the partner is an error carried in the result (`LightsError`,
`PartnerError`), printed in its section, and does not stop the rest of the report. `DarkReport`
writes section 9, `INDEX` (the index, the label, the lights' equivalent index, the partner's
lines and the chained lights-over-partner ratio) and `TABEL PE LIGHTS`; `DarkOutputs` writes
`darkscheck-lights.csv`. How the two analyzers work is in `lights-and-index.md`.

## The report as rows

`DarkReport` builds the report as data first (ADR 0009): `HeaderLine` is the `=== DARK-URI`
line, `HeaderRows` the rows under it (`normalizare`, `cuantizare`, the biases' warnings),
`Sections` the nine `ReportSection(Title, Rows)` of checks 2 to 9 and INDEX, `FrameRows` and
`LightRows` the two tables as `DarkTableRow` / `LightTableRow` of formatted cells, with
`FrameTableHeader` and `LightTableHeader` as the header lines. `Write` prints exactly those:
a row as `    {label,-19}: {value}`, a table row with the console's column widths, a file name
cut to 20 characters at print time only. A `ReportRow` reads its own verdict word off the
value (`Verdict`: OK, ATENTIE or NEMASURAT when the value opens with it as a word, or ATENTIE
when that is the row's label, the warnings about frames that failed; `Rest` the value without
a leading word and the `: ` or space after it, the whole value otherwise), so the window shows
the chip and the rest without parsing prose; the word list and the split live here, in
`ReportRows.cs`. `InventoryReport.HeaderRows` gives the session lines the same way and
`WriteHeader` prints them. Every sentence stays in the reports' private helpers, unchanged by
the refactor; the goldens pin the text. The two figures the window charts and the tables both
need are properties of the records, not of the window: `ResidualFigures.RowBandingRatio` and
`NoiseVerdict.OutlierLimitAdu`. `DarkOutputs.MapRaster` is the grey raster of a binned map (each block a
`MapScale` square stretched from the 1st to the 99th percentile), what `WriteMap` encodes as
PNG and what the window shows as an image.

## Measured

On the owner's machine (8 cores, 32 GB; 4 decoding threads), 2026-10-11: the Smida session
(30 biases then 20 darks, 24 MP NEF) runs in 35 s with a peak working set of 4.5 to 4.7 GB
once the memory knob is set (14 GB without it), the 30-frame library in 24 to 26 s and 2.9 to
3.3 GB (runs of 2026-10-11 and 2026-10-16). The figures
agree with the prototype on every line of `docs/validation.md` (V-003, V-004) to the
tolerances recorded there, the definitional differences listed in its History.
