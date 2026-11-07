# The dark series: the stack, the masters, checks 2 to 8

**Covers:** `src/DarksCheck.Core/Analysis/Planes.cs`, `src/DarksCheck.Core/Analysis/FrameStack.cs`, `src/DarksCheck.Core/Analysis/Normalisation.cs`, `src/DarksCheck.Core/Analysis/DarkSeriesAnalyzer.cs`, `src/DarksCheck.Core/Analysis/BiasAnalyzer.cs`, `src/DarksCheck.Core/Analysis/DarkVerdicts.cs`, `src/DarksCheck.Core/Analysis/LeakAnalyzer.cs`, `src/DarksCheck.Core/Runs/DarkSeriesRun.cs`, `src/DarksCheck.Core/Report/DarkReport.cs`, `src/DarksCheck.Core/Report/ScaleReport.cs`, `src/DarksCheck.Core/Analysis/CameraFacts.cs`, `src/DarksCheck.Core/Analysis/StackNoise.cs`, `src/DarksCheck.Core/Report/ReportRows.cs`, `src/DarksCheck.Core/Report/DarkOutputs.cs`, `src/DarksCheck.Cli/runtimeconfig.template.json`.

The behaviour is R-020 to R-041; the decisions are ADR 0007 (the stack in memory, the FITS
black, the verdict thresholds) and ADR 0004 (the normalisation). This note says how the
pieces fit and what was measured against the prototype (`prototype/analyze_darks.py`,
`supplementary.py`, the figures in `docs/validation/2026-08-12-smida/`).

## The records of the run

Every record the notes name has one home. `Runs/DarkSeriesRun.cs` declares `DarkOptions`
(what a run takes from the user: `Threads`, `MaxFrames`, `Against`, `IsoFactorPerStop`,
`DoublingCelsius`; the run builds its `Normalisation` from the last two, and `EffectiveThreads`
is half the cores when `Threads` is 0), `RunProgress(Phase, Done, Total)` (the phase word is
in the run's language and both applications print it as it is) and `DarkSeriesResult`, the whole result.
`Analysis/DarkSeriesAnalyzer.cs` declares `DarkSeriesAnalysis` and its parts (`PlaneFigures`,
`ResidualFigures`, `DarkFrameFigures`, `MasterFigures`, `HotStability`, `NoisePoint`,
`NoiseVsN`, `Regions`, `BinnedMap`, `BandingFigures`, `Warming`); `Analysis/BiasAnalyzer.cs`
`BiasAnalysis` and `DarkCurrent`; `Analysis/DarkVerdicts.cs` the six verdict records and
`DarkVerdicts`; `Analysis/FrameStack.cs` `StackFrame`; `Analysis/Normalisation.cs` the
settings record, whose defaults are the record's parameter defaults (`IsoFactorPerStop = 2`,
`DoublingCelsius = Th.DoublingCelsius`). The console builds `DarkOptions` in `Program.Run` from
its options and leaves the two settings at their defaults; the window builds it in
`MainViewModel.Options` (`window.md`).

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

The two decoding loops, this one and the lights' (`lights-and-index.md`), are the only code of
`Analysis/` that writes the diagnostic log (ADR 0025): one line per frame decoded, with its time
and the process's working set after its collection, a frame left out or failed with its
exception, and the stack's counts and scale in the log's own words. `DarkSeriesRun` writes the
run's stages and every figure of the records it gets back, and `DarkOutputs.WriteAll` tells its
caller each file as soon as it is written, so the console and the window log the files; the
lines are in `logging.md`.

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
(above the threshold and below `Th.AmplitudeMaxFraction` of white), and the eight regions
(four edges, four corners) of its binned reference plane against the centre, in raw ADU. The stability of the hot pixels counts, per hot
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

## The warming and the plateau

`DarkSeriesAnalyzer.WarmingOf` reads check 3 from every frame's amplitude ratio (the median,
over the master's hot pixels under `Th.AmplitudeMaxFraction` of white, of the frame's
amplitude over the master's). The `Warming` record keeps the first and last ratio and their
degrees, then the fit of ADR 0017 when at least `Th.WarmingFitMinFrames` frames have a ratio:
`TimeAxis` gives the minutes from the first frame (`TimeFromStamps`) or, when a timestamp is
missing, a stamp goes back in series order or the span is zero, the frame's position; `PlateauFit` tries every time constant on
a grid from a fiftieth of the span to three times it, in steps of a two-hundredth, solves the
two linear coefficients of `plateau - drop x exp(-t / tau)` by least squares for each and
keeps the smallest residual sum of squares (`FitScale` is the rms). From the fit: the master's
degrees to the plateau (`Degrees(1 / plateau)`, negative under it), the degrees left between
the fitted last frame and the plateau (`SettleCelsius`), the band `OffPlateauLow` ..
`OffPlateauHigh` of `Th.OffPlateauCelsius` around the plateau, the frames outside it
(`OffPlateauFrames`) and where the median ratio of the frames left would put the master
(`MasterWithoutOffCelsius`) and the noise factor sqrt(frames / frames left). A fit whose
plateau or fitted last frame is not positive counts as none; `TauAtGridTop` marks a time
constant at three times the span, where the plateau is extrapolated and the report says so.
`DarkVerdictAnalyzer` adds the frames whose ratio leaves `Th.MatchOptLow` .. `Th.MatchOptHigh`
of the master and the ratio farthest from 1 among them; the series is settled when there is
no fit or `|SettleCelsius|` is within `Th.SettledCelsius`.

## The light and the glow

`LeakAnalyzer` reads two per-frame figures against the frame's hot-pixel amplitude ratio
(the warming of check 3), on which dark current, glow included, is proportional and light
from outside is not (ADR 0012). `FrameLightOf` takes the frames that have a ratio (at least
`Th.LeakFitMinFrames`), fits the frame mean above black on the ratio by Theil-Sen (`TheilSen`:
the slope is the median of the pairwise slopes, 0 under two distinct ratios; the intercept the
median of y - slope x), reads the scale as 1.4826 x the median absolute residual, and in up to
`Th.LeakFitPasses` passes leaves out the frames whose residual exceeds the limit (the larger
of `Th.LeakFrameSigmas` scales and `Th.LeakFrameAdu` x the normalisation, in raw ADU) and
refits on the rest, stopping when the kept set no longer changes or would fall under three
frames. Every frame's final residual against the last fit, divided by the normalisation, is its
excess light (`FrameLight.ExcessAdu`, aligned with the series' frames, NaN without figures);
the frames over the limit on the positive side are lit (`LitFrames`, the file index, with their
excess beside them). `EdgeSplitsOf` then takes the frames that are not lit and, for each of the
eight regions, fits the frame's offset to the centre on the ratio without clipping: the slope
is the glow part (the value at ratio 1, the master's temperature), the intercept the constant
part; with biases, the bias mean map's offset of the same region (the bias's own regions, raw
ADU) is the readout part and the constant minus it the remainder, all divided by the
normalisation. The cause is the part that contributes most on the side the region departs to
(the sign of glow + remainder, what dark minus bias shows, when that passes `Th.LeakAdu`; else the sign of glow + constant, the dark's own departure), glow / readout / leak with biases and glow / constant without,
since a leak that stays constant and a readout offset look the same in the darks alone. The
split is an extrapolation to ratio 0, so `FrameLight.RatioSpan`, the span of the ratios of the
frames not lit, must reach `Th.LeakFitMinSpan`, else `EdgeSplitsOf` returns nothing and the row
says the series did not warm enough. `Regions.OffsetOf` and `EdgeFigures.Of` map a `MapRegion`
to its value for both the verdict and the split; `Planes.Median(IReadOnlyList<double>)` serves
the fits. `SidesOf` reads the four sides (`MapSide`, an edge with its two corners,
`SideLight.RegionsOf`) the same way, on the same frames (`KeptForSplit`): the side's per-frame
offset is the mean of its three regions (`SideOffset`), fitted on the ratio; the intercept is
the constant with the standard error `scale x sqrt(1 / n + mean(x)^2 / Sxx)` of a line read at
x = 0, and with biases the bias master's side offset is taken out. The sides need `Th.LeakSidesMinFrames` frames left after the lit ones, the split three;
`DarkReport.SplitUnmeasured` turns the three reasons (too few frames with a ratio, too little
warming, too few frames left) into the rows' `NEMASURAT` words. `LeakVerdict.ConstantLight`
is the strongest side whose remainder passes `Th.LeakConstantAdu` and `Th.LeakConstantSigmas`
times its error (ADR 0014); `WorstSide` is the side with the most light, which the frames
table shows per frame (`latura`: the frame's side offset minus the bias's, over the
normalisation). `DarkVerdictAnalyzer.Judge` keeps the verdict region's split
beside the eight and lets check 6's glow be the glow part of that region.

## The verdicts

`DarkVerdictAnalyzer.Judge` turns the figures into six verdicts with the constants of
`Th`: the leak from the largest region departure of the dark-minus-bias map (or the dark
master without biases), its sign telling light from a readout offset, `ATENTIE` also when a
frame is lit, and the words of the brighter case from the cause of the split; the drift as
`REFA` (`Verdict.Redo`, `DriftVerdict.MostlyOff`) when more than `Th.RedoOffPlateauFraction` of
the frames with a ratio are off a fitted plateau (not one extrapolated from the top of the
grid), since the median master is then made of them (ADR 0038), else `ATENTIE` only when the
series has not settled or a frame leaves the `-opt` band of the master (the frames off the
plateau are otherwise information, in the `de aruncat` row, the table's `platou` column and a
note on their rows); the noise from the residual sigma outliers, with the temporal noise
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

## The scale section

`ScaleReport` ends the report with `EXPLICATII SCARA ADU` (R-080, ADR 0015): eleven rows and
four paragraphs built from the result alone. The bits come from the white level
(`ceil(log2(white + 1))`), the settings from the first decoded dark's `FrameInfo`, the sky and
the stack noise from the median over the decoded lights of `SkyMedian` and `SkySigma` on the
reference plane (`sigma / sqrt(N)`), the thresholds from `Th` times the normalisation, what was
found from `LeakVerdict` (`ConstantLight`, the lit frames, the offset). `CameraFacts.For` maps
a camera name to its published figures (one entry, the D3300; a name containing `D3300` in any
case) and `GainAt` interpolates the gain on the log2 scale between the listed ISO stops,
returning null outside them, so a row never guesses; when `For` returns null the `electroni`
row says `necunoscut pentru <camera>`, the `saturatie` and `zgomot de citire` rows drop the
source's figures, and the first paragraph gains its last sentence, built in `Paragraphs` from
the first dark's `Camera` or the words for no camera (ADR 0023). The words are plain (R-081,
ADR 0028): `PlaneWords` names the reference position as the green pixels `G1` when its name
starts with `G` and by its name alone otherwise, and `CameraFacts.SourceWords` is the one
provenance phrase, after `Cifre pentru <camera> de pe` in the `electroni` row and in brackets
when the ISO is outside the table. The console writes the rows as
`label : value` and the paragraphs through `ScaleReport.Wrap` at 96 columns with a four-space
indent; the window binds `Rows` and `Paragraphs` as they are. `StackNoise.Of` holds the lights'
sky median, sky sigma and count on the reference plane with the mean and median stack's noise,
the lights' share sigma / sqrt(N) plus the dark master's noise in quadrature (the master is
subtracted from every light alike, so its noise does not average out);
`ScaleReport.AgainstMasters` turns a light in raw ADU into the words and the bold parts of its
ratio to the dark master's noise (`NoiseVsN.MasterNoise`) and to the stack's, used by the
`ce s-a gasit` row and by check 2's `lumina pe cadre`, `lumina constanta` and
verdict rows in `DarkReport`. A `ReportRow` carries `Bold`, the value's substrings the window
emphasises (ADR 0016); the console ignores it.

## The lights, the index and the partner in the run

Before anything is decoded, `DarkSeriesRun` looks at the session's darks: none (a lights-only
session, ADR 0011) and no `--against` is the refusal `RunReport.LightsOnlyNeedsPartner`; none
and a partner opens the partner (`SessionFolder.Open` without the lights-only flag), takes its
darks as `darkPaths`, recomputes check 1's match with the partner darks' metadata
(`InventoryRun.ReadAll`, kept in `Inventory.PartnerDarks` so the `aparat` row counts their
camera), records the partner's root in `Inventory.DarksFromPartner` and
`DarkSeriesResult.DarksFromPartner` (which `DarkOutputs.WriteAll` refuses as an export folder,
like the session), labels the master with the partner's folder and skips `MatchPartner` (the
darks are the partner's); a partner that does not open fails the run with its own message
prefixed `partenerul: `, so the window's problem card names the right folder. The progress phase then reads
`Se citesc dark-urile partenerului`. After the verdicts, `DarkSeriesRun` computes the index and the label of the darks' master
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
lines and the chained lights-over-partner ratio) and `TABEL PE LIGHTS`; one `MatchText` words
every match verdict, the lights' and the partner's, from where the set's hot pixels are (`in
lights`, `in dark-urile sesiunii`) against the master (`in dark`, `la partener`), and `Warmth`
turns a match's degrees into the clause of the INDEX rows (`adica cu 7.5 grade mai reci decat
partenerul`; `la aceeasi temperatura` under `Th.SameTemperatureCelsius`; none for a ratio that
gives no degrees); `DarkOutputs.LightsCsv`
writes `darkscheck-lights.csv` beside the darks' and biases' writers, with the columns `index,
file, time, exposure_s, iso, error, skipped`, then per plane `<P>_sky_median, <P>_sky_sigma,
<P>_local_sigma`, then `candidates, isolated_hot, amp_ratio_median, amp_ratio_q25,
amp_ratio_q75`. How the two analyzers work is in `lights-and-index.md`.

## The report as rows

`DarkReport` builds the report as data first (ADR 0009): `HeaderLine` is the `=== DARK-URI`
line, `HeaderRows` the rows under it (`1 ADU-eq`, `pasul valorilor`, the biases' warnings),
`Sections` the nine `ReportSection(Title, Rows)` of checks 2 to 9 and INDEX, `FrameRows` and
`LightRows` the two tables as `DarkTableRow` / `LightTableRow` of formatted cells, with
`FrameTableHeader` and `LightTableHeader` as the header lines, each column name ending where its
figures end (the file and the time, left-aligned, start where theirs start). `Write` prints exactly those:
a row as `    {label,-19}: {value}`, a table row with the console's column widths
(`FrameLine` for a dark: the cells, then the row's `Note` after the last column when the frame
is off the plateau, `Plateau` being its degrees from the plateau or `-` without a fit), a file
name cut to 20 characters at print time only. The header line takes the scale's source as
`FrameStack` words it: a raw's `libraw (exact)`, the copied reader's label, becomes
`citite exact din raw` (`RawSource`), a FITS says its bits and where its white and black came
from, and `HeaderLine` puts only a FITS's words in brackets. The `1 ADU-eq` rows, the header's
and the scale section's, call a set at
the reference settings themselves (`AtReference`: ISO `Th.NormIso` and `Th.NormExposureSeconds`)
by those words and any other set, a factor of 1 at ISO 800 and 60 s included, by its factor.
`WarmingChartNote` is the sentence the window
puts under its warming chart: the plateau and the band of the frames to discard, when there
is a fit. Check 3's rows take the camera's direction from the plateau against the first frame's
ratio (`PlateauRow`, `Drift`: the plateau under it is a series that cooled), print every
distance to the plateau unsigned with "above" or "below" (`FromMaster` for the plateau against
the master, `FromPlateau` for the master against the plateau), and give the `REFA` advice of a
cooling series as three times `TauMinutes` rounded up to a multiple of five (ADR 0038).
Every side text, check 2's, the note's and the ADU scale section's, names a side
with its preposition (`SideWithPreposition`: `latura de sus`, `latura din dreapta`). A frame
whose hot pixels do not show at all (a ratio at or under 0) is off the plateau in `WarmingOf`,
and `FrameRows` gives it its own reason, with no degrees; `NoiseColumnTip` and `HotColumnTip`
are the hints of two columns that name the series' pixels. A `ReportRow` reads its own verdict word off the
value (`Verdict`: OK, ATENTIE, REFA or NEMASURAT, or their English WARNING, REDO and UNMEASURED, when the value
opens with it as a word, or ATENTIE (WARNING) when that is the row's label, the warnings about frames that failed; `Rest` the value without
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
