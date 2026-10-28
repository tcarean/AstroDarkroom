# Mode 1: structure, the six tests on a master flat

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the fifth test and the upper bound are ADR
> 0015, the local spot is ADR 0041, the sixth test against the lens profiles is ADR 0031 (with
> ADR 0013 for the operator and ADR 0030 for the identification). This note keeps the order of
> the pipeline and the mechanics the decisions do not spell out. **Covers:** in full,
> `Analysis/StructureAnalyzer.cs`, `Analysis/VignettingComparison.cs`, `Report/StructureReport.cs`,
> `Runs/StructureRun.cs`; by pointer, `LensProfiles/LensIdentification.cs` (ADR 0030).
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 1. Structure (default) - `flatcheck flat.fit`

Is the master flat radially symmetric? Runs on the final master.

1. `FitsReader.Load` → `Prepare` (2×2 block-mean, then downsample toward ~1200 px).
2. `FitCenter` → one paraboloid fit on `log(I)`, two outputs. The **tilt** (the
   peak-to-peak amplitude of the fitted linear term, `2·sinh(|b|+|c|)`) is a test, on
   the shared residual scale. The **vertex offset** from the sensor centre, as a
   percentage of the half-diagonal, is printed informatively and carries no verdict
   (ADR 0014).
3. `CornerStats` → median of four 5% corner boxes and a central box. Reports
   vignetting in stops (informative only, *not* a test) and the spread between
   the four corners (a test - the corners should all be equal on a radial field).
4. `RadialModel` around the sensor centre → residual `img / model`, then
   `LargeScalePtp` of that residual (tile-averaged, `p95 - p5`, **whole frame**, ADR 0016) and
   `MaxTileDeviation` (the single worst tile, whole frame - what the 5% trims of the
   residual throw away).
5. `AzimuthalAsymmetry` → per-ring peak-to-peak across 12 sectors; the max is the
   metric.
6. Verdict = the worst of the five tests - corner spread, azimuthal asymmetry,
   residual, tilt, worst tile - on the shared residual scale (see
   "The residual scale" in `thresholds.md`). Prints that scale, and marks
   `reziduu dupa model rad.` as the metric that matters most. Writes the residual
   heat map PNG.
7. `LocalSpots` → the deepest *compact* spot after the large-scale structure is
   subtracted (fine tiles minus their 7x7 neighbourhood median), with its position.
   Informative, with a warning above `Th.LocalSpotWarn`, never a verdict: ADR 0041 holds
   why, the signed wording, the border rule and the manual shape check. The wording is
   `StructureReport.LocalSpotWarning`, shown by the console under the informative rows
   and by the window under the verdict.

**Why a fifth test.** ADR 0015 holds it: the 5 % trim of `LargeScalePtp` drops any defect
covering under 5 % of the frame (a +10 % corner blotch read 0.04 % and `PERFECT`), the worst
tile reads 9.3 % on it and never carries a gradient. Pinned by
`ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`.

What the mode gives is an **upper bound** on the flat error, not the error (ADR 0015: the sum
of a camera-fixed part and a world-fixed part, which one master cannot separate; nothing in
the tool does since the `--ratio` mode left, ADR 0067).
The bound is still the right thing to run first, because it is the only test that costs no
extra shooting.

**The sixth test: the master's vignetting against the lens profiles** (`--raw <flat.NEF>`
or `--lens <text>`; the window does it by itself when `flats/` holds a raw). ADR 0031 holds
the decision and every measured reason: why the five tests (and any rotation) are blind to a
radial excess (the tablet masters of 2026-09-18), the depth measured with the master's own
operator (ADR 0013), no new threshold (`Stats.DepthErrorPct`, `2^|D| - 1`, onto the residual
scale), the two scales for one number and option A over option B, the nearest profile's
verdict rather than the worst, the worse of shape and depth with `DepthAdvice` reading the
sign, `ComparisonGap` voting nothing, the byte-identical run without `--raw`/`--lens`, the
library figures of 2026-09-18 and the guards. ADR 0030 holds how the lens is identified
(`LensIdentification`). The mechanics the decisions do not spell out:

- `VignettingComparer.Analyze` puts the master beside every candidate lens's nearest profile
  (focal, then f-stop in stops, then farthest distance - `LensCatalog.Nearest`); the model is
  built as a raster at the analysis size by `SyntheticFlatBuilder.Build` and
  `Metrics.CornerStats` runs on it. What remains on a master that *is* the model is the
  master's 2x2 block mean, under a thousandth of a stop.
- The depth arithmetic moved from `AgainstResult` into `Stats` when the second consumer
  appeared; `--against`'s depth goes through the same `Stats.DepthErrorPct`.
- `Stats.DepthOnLightScale` reads the corner error through the same `Verdict` and the same
  `Th.LightRad*` that `LightAnalyzer` uses, and `StructureReport.DepthLightScaleNote` prints it
  (the reference's spread from `Th`, never written out) under the depth scale in the console,
  under `--against`'s depth line, glued to the scale note on the Structure tab and to the
  verdict detail on the Sintetic tab.
- `ComparisonGap` names the reason when nothing can be compared: no profiles on the machine,
  no raw and no `--lens`, a raw that was read but says nothing about the lens (`RawWithoutLens`,
  `ShotIdentity.RawSaysNoLens`: no lens ID and no focal range, a camera on a telescope; ADR
  0065, named with the file and the remedy, `StructureReport.RawWithoutLensRemedy` asking only
  for what is still unknown), an ID nothing matches, no focal, no f-stop (Siril writes none), or
  lensfun-only profiles without a crop. Unknown is not a refusal, the `--light` rule.
- What the search left out is said on the window's card only (R-113, ADR 0073):
  `StructureReport.AdobeMissing` and `AdobeSearched` take the sources the run searched and give
  text only when that search found no Adobe file (`ProfileSources.AdobeFiles == 0`), "" when it
  found one or when the run searched nothing (null, no raw). Without the `lensfun/` folder
  `AdobeMissing` keeps its first sentence only: the gap under it then says no profile is on the
  machine. `AdobeInstall` and `AdobeDngConverterUrl` are the instructions and the link under
  them. `Write` prints none of them: R-113 adds no line to the console's report and changes no
  golden.
- `--help` moved when the flags were added, and was re-approved.
