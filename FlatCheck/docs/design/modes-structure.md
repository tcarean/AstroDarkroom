# Mode 1: structure, the six tests on a master flat

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Analysis/StructureAnalyzer.cs`, `Analysis/VignettingComparison.cs`, `Report/StructureReport.cs`, `Runs/StructureRun.cs`, `LensProfiles/LensIdentification.cs`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 1. Structure (default) - `flatcheck flat.fit`

Is the master flat radially symmetric? Runs on the final master.

1. `FitsReader.Load` → `Prepare` (2×2 block-mean, then downsample toward ~1200 px).
2. `FitCenter` → one paraboloid fit on `log(I)`, two outputs. The **tilt** (the
   peak-to-peak amplitude of the fitted linear term, `2·sinh(|b|+|c|)`) is a test, on
   the shared residual scale. The **vertex offset** from the sensor centre, as a
   percentage of the half-diagonal, is printed informatively and carries no verdict -
   see "The residual scale" for why that changed in 1.2.0.
3. `CornerStats` → median of four 5% corner boxes and a central box. Reports
   vignetting in stops (informative only, *not* a test) and the spread between
   the four corners (a test - the corners should all be equal on a radial field).
4. `RadialModel` around the sensor centre → residual `img / model`, then
   `LargeScalePtp` of that residual (tile-averaged, `p95 - p5`, **whole frame** - see
   "Shared pipeline" for why the inscribed-circle restriction went) and
   `MaxTileDeviation` (the single worst tile, whole frame - what the 5% trims of the
   residual throw away).
5. `AzimuthalAsymmetry` → per-ring peak-to-peak across 12 sectors; the max is the
   metric.
6. Verdict = the worst of the five tests - corner spread, azimuthal asymmetry,
   residual, tilt, worst tile - on the shared residual scale (see
   "The residual scale" below). Prints that scale, and marks
   `reziduu dupa model rad.` as the metric that matters most. Writes the residual
   heat map PNG.
7. `LocalSpots` → the deepest *compact* spot after the large-scale structure is
   subtracted (fine tiles minus their 7x7 neighbourhood median), with its position.
   **Informative, with a warning above `Th.LocalSpotWarn`** (2%), never a verdict: a
   dust shadow is correct for the session whose lights carry the same shadow and wrong
   for a library flat, and the tool cannot know which use is intended. The wording is
   `StructureReport.LocalSpotWarning`, shown by the console under the informative rows
   and by the window under the verdict. Measured on the real Nikkor 35 f/2.8 master the
   dust shadow visible in the map is -0.31%, well under the warning - which is the
   measurement behind "dust at f/2 is below threshold" above.

**Why a fifth test.** `LargeScalePtp` trims 5% of the tiles at each end so that it
measures structure rather than noise - and that same trim drops any defect covering under
5% of the frame, however deep. Measured on a 24 MP synthetic: a +10% blotch the size of a
fingertip in a corner read 0.04% residual and `PERFECT`, with the blotch plainly visible
in the map. The worst tile reads 9.3% on it. On a gradient the worst tile reads half the
tilt (it is a per-pixel deviation, the tilt is peak-to-peak), so it never carries the
verdict for a gradient; it exists for the compact case. Pinned by
`ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`.

What the mode gives is an **upper bound** on the flat error, not the error: everything it
measures is the sum of a camera-fixed part (harmless - the flat divides it out) and a
world-fixed part (harmful). One master does not contain the information to separate them;
mode 2 does. The report says so, and the bound is still the right thing to run first,
because it is the only test that costs no extra shooting.

**The sixth test: the master's vignetting against the lens profiles** (`--raw <flat.NEF>`
or `--lens <text>`; the window does it by itself when `flats/` holds a raw). The five tests
measure departure from radial symmetry and are blind by construction to a *radial* excess:
the tablet masters of 2026-09-18 (Tokina 20 mm f/2.8, four orientations at 90°) read
PERFECT on all five with 1.26 stops of vignetting, against 0.66-0.92 from the two profile
databases - the panel's angular falloff, made radial by the rotation, and `--ratio` is
just as blind. The only external reference the tool has is the lens's published profile,
so `VignettingComparer.Analyze` puts the master beside every candidate lens's nearest
profile (focal, then f-stop in stops, then farthest distance - `LensCatalog.Nearest`).
Decisions, all measured:

- **The profile's depth is measured with the master's own operator.** Not the polynomial
  at the corner point: the model is built as a raster at the analysis size
  (`SyntheticFlatBuilder.Build`) and `Metrics.CornerStats` runs on it, so the box median
  and the half-pixel artefact (known issue 5) are the same on both sides. On a master that
  *is* the model the difference is 0.00 (`AMasterThatIsTheModelReadsZeroStopsAndPerfect`);
  what remains is the master's 2x2 block mean, under a thousandth of a stop.
- **No new threshold.** The difference in stops goes through `Stats.DepthErrorPct`
  (`2^|D| - 1`, the corner error of a light calibrated with the wrong flat) onto the
  residual scale, exactly as `--against`'s depth does: 0.03 / 0.14 / 0.26 stops. The
  arithmetic moved from `AgainstResult` into `Stats` when the second consumer appeared.
- **Two scales for one number, and the report shows both (2026-09-23).** That corner
  error is exactly what `--light` measures, directly in the light, against
  `Th.LightRadOk` / `LightRadBad` = 2 / 5 %; here it is judged on the residual scale,
  where 10 % is still BUN. The user asked which one to believe. The answer is the
  reference: the depth verdict's reference is a published profile, and the two databases
  differ by `Th.ProfileDatabaseSpreadStops` = 0.2 stops = 15 % in the corner on the same
  lens, three times `--light`'s PROBLEMA line, so a finer verdict here would be invented
  precision, while `--light` measures the light itself and can afford 5 %. Aligning the
  bands (option B) was measured and turned down: it put most of the library - the paper
  masters at 6-8 %, the Nikkor sky masters at 7-8 % - on REFA below the reference's own
  resolution. Option A is what is built: `Stats.DepthOnLightScale` reads the same error
  through the same `Verdict` and the same `Th.LightRad*` that `LightAnalyzer` uses, and
  `StructureReport.DepthLightScaleNote` prints the corner error, the word `--light` would
  give it and the reference's spread (from `Th`, never written out) - under the depth
  scale in the console, under `--against`'s depth line, glued to the scale note on the
  Structure tab and to the verdict detail on the Sintetic tab. Verdicts and exit codes
  did not move. Pinned by `TheDepthNoteSaysWhatTheLightScaleWouldReadAndWhyItDoesNotDecide`.
- **The verdict is the nearest profile's, not the worst.** The two databases differ by
  ~0.2 stops on the same lens (`docs/lens-profiles.md` §8); a master that coincides with
  one of them is not wrong, and "worst" would have called the good monitor masters REFA
  (0.15 off Adobe, 0.03 off lensfun). The block prints every profile, marks the nearest,
  and states the spread between them.
- **The run's verdict is the worse of shape and depth**, as in `AgainstResult`, and the
  exit code follows it. When the depth carried it the advice is `DepthAdvice`, which reads
  the sign: plus = the master falls faster than the model, the source falls off with angle
  (a panel too close or without a real diffuser; frosted sheets do not diffuse); minus =
  the source fills the corners (the sky dome, ambient light, a reflection). Neither
  `--ratio` nor more rotations help.
- **When it cannot compare it says why and votes nothing** (`ComparisonGap`: no profiles
  on the machine, no raw and no `--lens`, an ID nothing matches, no focal, no f-stop -
  Siril writes none - or lensfun-only profiles without a crop). Unknown is not a refusal,
  the `--light` rule.
- **Without `--raw`/`--lens` nothing changes**: same text, same golden files, the 15
  `crosscheck.py` structure scenarios untouched. `--help` moved, and was re-approved.

Measured on the library (2026-09-18, `--raw` on the first raw of each `flats/`; the
figures are in the README's table): tablet 7 and 15 sheets +0.34 / +0.33 against the
nearest (lensfun D7100 entry, 0.91) - REFA, exit 3; the sky master -0.39 against Adobe
0.66 - REFA; monitor close 0.00 against lensfun 7D2 - PERFECT; on-monitor -0.07 - BUN;
Nikkor 35 DX +0.11 / +0.10 - BUN; Samyang 135 f/2 by range (see `LensIdentification`)
+0.11 / +0.10 against the Nikkor 135 f/2 DC's Adobe file, +0.15 against its own lensfun
entry - BUN; Samyang at f/2.8 +0.01 - PERFECT. Pinned by `VignettingComparisonTests`,
the profile half of `StructureReportTests`, the goldens `structure_with_profiles`
(k = 0.6 fixture: BUN on shape, REFA on depth, exit 3) and `structure_lens_no_fstop`, and
in the window `StructureTabTests` with a fabricated raw plus two smoke renders.

