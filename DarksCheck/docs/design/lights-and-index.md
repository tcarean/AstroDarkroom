# The lights against the dark, the index and the matching

**Covers:** `src/DarksCheck.Core/Analysis/LightsAnalyzer.cs`, `src/DarksCheck.Core/Analysis/IndexAnalyzer.cs`.

The behaviour is R-050 to R-067; the decisions are ADR 0004 (the index as a rank statistic,
the matching as a ratio on shared pixels, no reference file), ADR 0008 (the lights read one
by one, the isolated detector, the pixels two masters match on) and ADR 0044 (what a plain
subtraction leaves, against the noise of a light). The run that calls these two
(`DarkSeriesRun`), the report sections `2. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE`, `INDEX` and `TABEL PE LIGHTS`
and the file `darkscheck-lights.csv` are described with the rest of the run in
`dark-analysis.md`. The oracle is the lights loop of `prototype/analyze_darks.py`
(`local_excess`, `neighbour_max`) and `docs/validation/2026-08-12-smida/report.md`, section 9.

`Analysis/LightsAnalyzer.cs` declares `LightFigures`, `OverlapAtLevel`, `TraceFigures` and `LightsAnalysis`;
`Analysis/IndexAnalyzer.cs` the enum `MatchVerdict`, `IndexFigures`, `MatchFigures` and
`PartnerAnalysis` (the partner itself is built by `MatchPartner` in `Runs/DarkSeriesRun.cs`,
`dark-analysis.md`, which also describes `darkscheck-lights.csv`).

## The lights, one at a time

`LightsAnalyzer.Analyze` takes the paths of the lights and the darks' analysis (its median
master, black, white, size, normalisation and plane names). The lights are never stacked:
`FrameStack.DecodeOne` decodes one file (`dark-analysis.md`, "The stack"), `Measure` reduces
it to a `LightFigures` and the mosaic is dropped, with the same forced collection per frame as
the stack. `threads` lights are in flight at once, and every light is read: the frame cap of
the darks does not apply, since no light is kept (ADR 0041). A light of another
size than the darks, or one that fails to decode, is listed with its error. A FITS light has no
black level of its own and takes the darks' black (the pedestal is the camera's); a raw brings
libraw's. A light that does not say its ISO or its exposure is taken at the darks'
normalisation and marked `AssumedSettings`. The loop writes the diagnostic log as it goes, one
line per light measured with its time, the working set and its figures, and a light that failed
with its exception (ADR 0025, `logging.md`); `Measure` and the rest of the file write nothing.

`Measure` works per CFA plane. The sky is the clipped sigma and median of the plane, read on
every fourth row and column (`Subsample`), minus black. The **local excess** `LocalExcess` is
the pixel minus the median of its eight same-colour neighbours (the eight pixels of the plane
around it, the border repeated, the median of eight being the mean of the two middle values,
as numpy's); its clipped sigma on the same subsample is the detector's scale, `LocalSigma`.
The excess and the excess over sigma (`Z`) are written back into full-resolution rasters so the
isolation test can look across colours.

A **candidate** is a pixel with `Z` above `Th.LightHotSigmas` (6) and a value under
`Th.LightSaturationFraction` of white (4000 of 4095). It is **isolated** when the largest `Z`
among its eight immediate neighbours, of whatever colour and 0 outside the frame, stays under
`Th.LightIsolationSigmas` (3): a star lights its neighbours, a hot pixel does not. In a Bayer
mosaic the immediate neighbours are other colours, so each is judged against its own colour's
local median; this is the prototype's `neighbour_max(Z) < 3`. Noise makes one of eight
neighbours cross 3 sigma about once per hundred hot pixels per light, which the persistence
threshold absorbs.

The isolated pixels of every light are counted per position under a lock; a position flagged
in at least `ceil(Th.PersistentFraction x lights used)` lights is **persistent** (26 of the 32
Smida lights). `HotCountHistogram[k]` is how many positions were flagged in k lights. The
overlap is read both ways at the first four hot levels (10, 20, 50, 100 ADU-eq): of the
persistent pixels, how many the master has above the level, and of the master's pixels above
the level, how many are persistent. The persistent pixels under `Th.HotAdu` in the master are
the **missed** ones, with the median of their master excess: a star that did not move between
lights, or a pixel the dark does not show.

The **amplitude** of a light is read at the master's hot pixels (`ampIdx`: above `Th.HotAdu`
in ADU-eq, under `Th.AmplitudeMaxFraction` of white): the median and quartiles over those
pixels of the light's local excess over the master's excess, each divided by its own
normalisation (`FrameStack.NormOf`), so lights at ISO 800 against darks at ISO 400 read 1 when
the sensor was equally warm. The night's figure is the median over the lights of their medians
(`IndexAnalyzer.Match`), with the first and the last light's ratios beside it, because the
first lights of a night run cold (0.72 on Smida, 1.0 from the fifteenth minute).

## The index

`IndexAnalyzer.RankIndex` takes the median master, black, white, the normalisation and the
size. It collects the excess above black of every unsaturated pixel (under white - 1), then the
rank band: `kLo = max(1, round(Th.IndexRankLowPerMpx x Mpx))`,
`kHi = max(kLo + 1, round(Th.IndexRankHighPerMpx x Mpx))`, the 0-based descending ranks
`kLo .. kHi - 1` (the 122nd to the 483rd strongest of the D3300's 24.2 Mpx). Its median is the
element at the middle rank `(kLo + kHi - 1) / 2`, found by one quickselect (`Planes.Select`)
on the ascending order, position `n - 1 - midRank`, and divided by the normalisation. On a
frame under 0.1 Mpx the band collapses to the strongest pixel, which is what the synthetic
tests see. `IndexFigures.Tag` is `idx` plus the index rounded half up (`idx25`, `idx67`;
`idx-` when nothing is unsaturated).

`Label` is the folder's name plus `-` and the tag: the darks' parent when the darks sit in a
`darks` subfolder (a session, or a library kept as `<name>\darks\`), the darks folder itself
otherwise (R-061). The name goes through `WithoutLabelSuffix` first, one `Regex.Replace` with
the source-generated pattern `(?<=.)-idx(?:[0-9]+|-)$`, case-insensitive and culture-invariant:
a trailing `-idx` and digits, or the unmeasured `-idx-`, is cut when at least one character
stands before it (the lookbehind), and anything else, `night-idx67-b`, `night-idx`,
`night-idx-60` or a bare `-idx67`, is kept whole (ADR 0013). It is only displayed (ADR 0002).

`Label(session, tag)` does that folder-name work for any tag; `Label(session, index)` passes
`index.Tag`, and `IndexFigures.TagFor(double)` makes a tag from any index (`idx-` for NaN). A
session with lights and no darks of its own (R-054) is labelled with its lights' equivalent
index (R-066, ADR 0039): `DarkSeriesRun` first labels it on `TagFor(NaN)`, then, once the
lights are measured, on `TagFor(lightsIndex)`; its `DarksDir` is the absent `<root>\darks`, so
the parent, the session's own folder, names the label; the tag it ends with is carried as
`DarkSeriesResult.LabelTag`, so the window shows it without deciding it. The partner's own label,
`Label(partnerSession, index)`, is carried as `DarkSeriesResult.DarksFromPartnerLabel` and
printed at the end of the `dark-uri (partener)` row, so the partner's index and label stand
once, on the partner's row, and the `eticheta` row and the window's big label are the open
folder's.

## The matching

`SharedRatios(own, reference)` walks both masters, same size, and keeps the pixels both have
above `Th.MatchMinAdu` (20 ADU-eq, each at its own normalisation) and both under
`Th.AmplitudeMaxFraction` of white, returning the per-pixel ratio of normalised excesses.
`Match` reduces any list of ratios to a `MatchFigures`: the median, the quartiles, the degrees
`Th.DoublingCelsius x log2(median)` and the verdict `Judge`: `Direct` within
`Th.MatchDirectLow .. MatchDirectHigh` (0.9 .. 1.1), `WithOpt` within `Th.MatchOptLow ..
MatchOptHigh` (0.7 .. 1.4), `CosmeticOnly` from `Th.MatchCosmeticLow` (0.4) up to 0.7 (the
reference is much warmer: it sees every hot pixel, subtraction would over-correct), `Unmatched`
beyond either side, `Unmeasured` for no pixel or a ratio that is not positive. The same `Match`
serves the lights (their per-light medians) and the partner (the per-pixel ratios). Why the
pixels hot in both and not the reference's alone is measured in ADR 0008. `Judge(ratio, low,
high)` is the same rule with a `Direct` band of its own; the plain `Judge` passes
`Th.MatchDirectLow .. MatchDirectHigh` and is the partner's.

## What the mismatch leaves

A set of lights against its darks is judged with a wider `Direct` band when a plain subtraction
leaves little (R-067, ADR 0044). `LightsAnalyzer.TracesOf` runs at the end of `Analyze`, on
figures already there. The noise of a light per CFA position is the median over the decoded
lights of `LightFigures.LocalSigma[p]`, each divided by its own normalisation (the darks' for a
light with assumed settings), so in ADU-eq. For every amplitude pixel (`ampIdx`) on a position
whose noise is positive, the master's excess in ADU-eq over that noise is the trace it leaves
per unit of `|ratio - 1|`; the position of mosaic index `i` is `(((i / w) & 1) << 1) | ((i % w)
& 1)`, the order `LocalSigma` is filled in. `TraceFigures.PerUnit` is the median of those, `k`.
The trace of any ratio is then `|ratio - 1| x k`, so the band inside which it stays under
`Th.TraceNoiseLimit` is `1 -/+ Th.TraceNoiseLimit / k`: `DirectLow` and `DirectHigh` are
`Th.MatchDirectLow` and `MatchDirectHigh` widened to it and clamped to `Th.MatchOptLow ..
MatchOptHigh`, the plain band when `k` is large (`Widened` says which). For the night's ratio:
`Median` (`|ratio - 1| x k`), `AboveNoise` and `AboveStrong` (the pixels whose own trace passes
1 and `Th.TraceStrongFactor` times the noise), and `Relaxed`, true when the plain `Judge` says
`WithOpt` and the band says `Direct`; `Analyze` then sets `LightsOverDark.Verdict` to `Direct`,
so every reader of the verdict (the report, the validation, the window's chip) sees it, and the
report takes its wording from `Relaxed`. `LightsAnalysis.Judge(ratio)` judges any ratio with
the band: the window colours each light's point with it. `Chain` and `EquivalentIndex` read
only the ratio, so the partner's verdicts and the lights' index do not move. No pixel read
(`TraceFigures.None`) leaves the plain band.

For a partner (`--against`) the run calls `SharedRatios` with the session's master as `own`
and the partner's median master as `reference`, then `Match`; how the partner is opened,
decoded and reduced to its master, and how the overlap of the two hot-pixel sets is counted,
is `MatchPartner` in `dark-analysis.md`. `Chain` gives the lights against the partner: the
product of the two medians, lights over darks times darks over partner, judged by `Judge` as
one ratio; `EquivalentIndex` gives the lights' equivalent index, the darks' index times the
lights-over-dark ratio. Both are carried in the result (`DarkSeriesResult.LightsIndex`,
`PartnerAnalysis.LightsOverPartner`), so the report and the window only format them.

## Measured

The figures on the Smida session and the library, and how they compare with the prototype's,
are V-005 and V-006 in `docs/validation.md`; the pixel selections the matching was chosen on
are the table of ADR 0008.
