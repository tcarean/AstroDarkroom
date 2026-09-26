# The core idea, the input contract, the mode dispatch and the shared algorithms

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `Img.cs`, `Stats.cs`, `Metrics.cs` (`Prepare`, `Percentile`, `Solve`, `FitCenter`, `RadialModel`, `AzimuthalAsymmetry`, `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots`, the verdict ladder); the long form of `RadialModel` is `radial-model-even-term.md`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## The core idea

For a photographic lens with no optical train (the author's setup is a Samyang
135 f/2 on a Nikon D3300), a *correct* flat is by construction a **purely radial**
function centred on the optical axis. Vignetting is rotationally symmetric; dust
at f/2 is below threshold.

Therefore any departure from radial symmetry is **not vignetting - it is error**
(non-uniform light source, light leak, tilted camera, ambient light). "Good flat"
does not mean "looks flat"; it means "the departure from radial symmetry is below
noise". That is a number, and it can be measured.

A crucial consequence, and an easy thing to break: the radial model and the
azimuthal asymmetry are computed around the **sensor centre** (`w/2, h/2`), *not*
around the fitted illumination peak. Centring on the fitted peak would let the fit
absorb exactly the linear gradient you are trying to detect (a gradient laid over
quasi-quadratic vignetting merely shifts the vertex), and the test would silently
cancel itself out. `FitCenter` never supplies the centre the other metrics use - it
supplies the *tilt*, which is a test, and the vertex offset, which since 1.2.0 is only
informative (see "The residual scale").

## Input contract

Modes 1-3 want a **calibrated** FITS - flat-dark/offset already subtracted. In the
Siril workflow that is `pp_flat_stacked.fit` in `process/` or `masters/` (the `pp_` prefix means
calibrated). An uncalibrated stack still has a ~150-600 ADU pedestal which
flattens the profile and under-reports vignetting by ~1%, and **all tests will
report OK anyway**, because the profile stays radially symmetric - only the
amplitude is wrong. Not detectable, only avoidable.

Mode 4 (`--expo`) is the exception: it wants a **raw, uncalibrated, single**
frame with the CFA mosaic intact and the original ADU scale, because stacking
normalises away the absolute level. The simplest way to satisfy that is to hand it the
NEF itself. Failing that, in Siril it means the **Conversion** tab (add NEFs → Convert),
*not* Open + Save, which debayers and normalises. The mode detects and rejects both
failure modes (see "Raw-file sanity gate").

Accepted formats: FITS (`.fit`/`.fits`/`.fts` - though the extension is never
actually checked, see Known issues), BITPIX 8/16/32/-32/-64, mono or colour; and
camera raw (`.nef`/`.cr2`/`.cr3`/`.arw`/`.dng`/`.raf`), where the extension **is**
what dispatches. All four modes accept both, exactly like `flatcheck.py`'s
`load_image_with_header`. A raw carries no FITS header, so `--light` sees an empty
dictionary and lands in the guard's `Unknown` layer - runs, with a note.


## The seven modes

Dispatch order in `FlatCheckCli.Run` is `--lenses` → `--synthetic` → `--against` → `--smooth` → `--histeq` → `--expo` → `--ratio` →
`--light` → structure (default). The modes are mutually exclusive; the first match wins.
The WPF app has no dispatch: each tab is one mode.

Each mode's chain lives in `Runs/`, in the core, and both applications call it - see
"The two applications".


## Shared pipeline and algorithms

**`Prepare(data, targetLong = 1200)`** - `BlockMean(data, 2)` then
`BlockMean(img, k)` with `k = max(1, (int)(max(H,W) / 1200.0))`.

The 2×2 block-mean is what makes the tool work directly on an undebayered CFA
master: each 2×2 RGGB block contains exactly 1 R, 2 G, 1 B, so it is a *fixed*
linear combination and the radial profile survives intact. This is why you can
feed it `pp_flat_stacked.fit` straight out of Siril.

Note the integer truncation of `k`: the ~1200 px target is loose. A 6000×4000
D3300 frame becomes 3000×2000 after the 2×2, then `k = (int)(3000/1200) = 2` →
**1500×1000**, not 1200. Anything landing between 1200 and 2400 is left alone.
This matches Python exactly, so it is parity-correct - just don't be surprised.
`BlockMean` truncates partial blocks (`a.H / k`), dropping the last rows/columns.

**`Percentile(list, p)`** - sorts a copy and interpolates linearly between the two
neighbouring order statistics, matching `numpy.percentile`'s default. Returns 0 on
an empty list (numpy would raise - a deliberate divergence, but it means a
degenerate input yields a silent 0 rather than an error).

**`Solve(A, b, n)`** - Gauss-Jordan with partial pivoting; returns `null` if
singular (pivot < 1e-15). Used for both least-squares fits, which are set up as
**normal equations** (`AᵀA x = Aᵀb`) accumulated in-place rather than via a QR/SVD
`lstsq`. This is less numerically stable than numpy's `lstsq` but adequate for
6- and 5-parameter fits on well-conditioned normalised coordinates.

**`FitCenter(img)`** - fits a paraboloid to `log(I)` over
`[1, xn, yn, xn², yn², xn·yn]` on a strided subsample (`step = max(1, min(h,w)/200)`),
and returns `(cx, cy, tiltPct, vertexValid)`.

`tiltPct = 2·sinh(|b| + |c|)·100` is the peak-to-peak amplitude of the **linear** term
over the frame: `|b| + |c|` is its value at the corner (where `xn = yn = 1`), and `sinh`
converts exactly from log space to intensity instead of the linear approximation
`2(|b| + |c|)`. It is a fraction, so it is scale-invariant like every other metric, and
it recovers an injected gradient to within ~1%. The cross term `f` (astigmatism/saddle)
is deliberately left out: it is not a tilt, and the residual test catches it anyway.

The vertex comes from `M = [[2d, f], [f, 2e]]`. It falls back to the geometric centre -
with `vertexValid = false`, so the report can say `nedeterminat` instead of printing a
fake `0.00` - if the system is singular or the vertex lands outside the frame. If the
normal equations themselves are singular the tilt is `NaN`, which degrades to `REFA`
like every other NaN in the tool.

**`RadialModel(img, cx, cy, nbins = 80, pct = 50)`** - bins pixels by radius into
80 equal-width bins out to `rmax`, takes the median per bin (bins with < 8 pixels
are dropped), then rebuilds a 2-D model by linear interpolation of that profile.
`Interp` clamps at both ends, matching `np.interp`.

**It is fed `Img.Symmetrise(img)`, not `img`** - the mean of the raster and its 180°
rotation. The residual is still `img / model`, so the gradient the map exists to show is
untouched. This is a fix, not a refinement: beyond the inscribed circle a constant-radius
ring is only partial arcs, a gradient makes their pixel distribution bimodal, and the
median falls into the sparse gap between the two modes - so it jumps from bin to bin by
an amount set by the gradient. The interpolated model inherited the jumps and the map drew
them as **concentric rings plus a hard circular edge on the inscribed radius, on a frame
that contained neither**. Reproduced on a synthetic that is nothing but smooth vignetting
times an 11 % ramp; measured on it, the worst ring-to-ring step went from 2.47 % to 0.11 %.
The verdict numbers barely moved at the time (`LargeScalePtp` was then restricted to the
inscribed radius, where the median was already exact - the golden files shifted by
0.01-0.03 points and no band changed), but the map is the only place a *shape* is read,
and it was lying. Symmetrise
rather than switch the ring statistic to the mean: the mean cures it equally well but gives
up the median's robustness to stars, hot pixels and dust, and the analyzer accepts single
frames. Pinned by `RadialModel_OnAGradient_TheProfileJumpsUnlessTheRasterIsSymmetrised`
and `OnAGradient_TheResidualHasNoRingsOutToTheCorner`, both of which assert the *un*fixed
behaviour too, so removing the symmetrisation as redundant fails loudly.

**The same artefact has a second face, and symmetrising does not touch it.** Symmetrise
removes the *odd* part only. An *even* non-radial term - a saddle (`xy`) or a falloff
steeper along one axis than the other (`x² - y²`), which is what a twilight sky leaves in a
70° field and what a 180° rotation cannot cancel - goes through it untouched, and the median
on the left/right arcs beyond the inscribed circle absorbed it into the profile: the model's
slope kinked from 0.21 to 0.71 % per bin exactly on that circle and the map drew a crease
there, with two lens-shaped lobes bounded by sharp arcs. Found on the Tokina 20 sky master
(15 + 15 frames rotated 180°, a -6 % sky saddle at the corners), reproduced arc for arc on a
smooth synthetic with the fitted coefficients, which is what proved the arcs were the
model's: the error of the map against the true residual was 8.9 % outside the circle.

So on the partial rings the profile is now the median of
`v / (1 + r² (q2c cos 2θ + q2s sin 2θ + q4c cos 4θ + q4s sin 4θ))`, where `q` are the
*relative* amplitudes per `r²` - ratios, like every other figure in the tool, since a sky
term multiplies and grows with `r²`. `q` is estimated on each identifiable partial ring by
`Metrics.RingFit` - on the medians of 15° sectors, by weighted least squares in three rounds
with the sectors beyond 2.5 MAD dropped between rounds, so a compact blotch that symmetrising
turns into an antipodal pair (which *is* a 2θ signal on its rings) leaves the estimate at the
second round - and then taken as **one median over every ring from `RingQFrom` = 0.75 of
the inscribed radius outward, full rings included**: a single `q` per `r²`, hence a
correction that is smooth in `r` and cannot draw rings. The full rings are in the median
because on them the harmonics are exactly identifiable; from the arcs alone the split
between 2θ and 4θ is ill-conditioned and extrapolates into the unobserved azimuths
(measured: +1.3 points on the worst tile of the Nikkor ISO400 sky master for nothing in
the data, 0.4 with the anchor). In the corners, where the arcs can no longer separate the
terms from the constant (a purely geometric criterion, `RingFitMinDet` on the determinant
of the basis's Gram matrix normalised to a full ring's variance - **not** a correlation
determinant, which reads 1 on the corner arcs of a square frame however narrow they are),
the same `q` applies. Inside the circle nothing changed: the ring median is exact on a full
ring and immune to anything compact. `a0` stays a pixel median everywhere, so the profile's
noise and robustness are what they were.

**The third face (2026-09-23): the 4θ term is in the basis because without it a 4θ field
was read as 2θ.** On arcs of ±30-60° about the x axis `cos 2θ` and `cos 4θ` are nearly
collinear. A master built from four orientations rotated 90° has, by construction, *no* 2θ
part of the world and *only* the 4θ family (the world's blotches smeared four-fold), so the
2θ-only fit of version 2.2 had nothing to correct and injected `q2c` of +2..+4 % with the
sign flipping ring to ring (the LMedS it used picked different sector triplets), stepped
the profile by −1.7 % exactly on the inscribed circle, and drew a red annulus with a hard
circular edge on the Tokina tablet master whose blotches are smooth - the user's words: "in
nature there are no straight lines". Measured on synthetics with known truth (model/truth
error, rms): a 6 % saddle - ring median 0.76 %, 2θ 0.03, 2θ+4θ 0.03; a 3 % 4θ term - median
0.17, **2θ 1.87 (ring steps 2.6 %)**, 2θ+4θ 0.03; saddle + 4θ, gradient, compact blotch,
blotch + saddle, blotch + 4θ: 2θ+4θ under 0.13 %. On the real masters the largest step of
the residual's ring mean: rotated tablet master 0.73 → 0.17 %, monitor 1.01 → 0.29, sky
0.31 → 0.31; a single-orientation stack read 17.9 % residual for 7.6 % of true non-radial
content and reads 7.9. LMedS was dropped: the MAD clipping held the +10 % corner blotch out
of the estimate (0.08 % error). The running median over 7 rings was dropped too: on the
blotchy real masters it left ring-to-ring steps of 0.7-1 %; one median over all rings does
not, at the cost of a smooth misfit where the true `q` varies with `r` (the sky master's
residual 9.31 → 9.71 %).

The earlier measurements still hold: on the sky master the sharpest step between 8-px
tiles of the residual went 3.58 → 0.23 % on the synthetic and 2.71 → 0.44 % on the master;
the true-residual error on the synthetic 8.9 → 0.5 %. The header comment of `RadialModel`
lists the designs that were measured and rejected on the way (a0 straight from a per-ring
fit, a global quadratic even correction, absolute amplitudes held from the last ring, a 4θ
amplitude *extrapolated* into the corners, the 7-ring running median) and why each failed;
read it before changing the method, and read `docs/radial-model-even-term.md` for the full
specification of `RingFit` (constants, sector rule, the identifiability gate, the rounds)
with every measurement behind it, section 7 for the 4θ face. Pinned by
`RadialModel_OnASaddle_RecoversTheRadialPartOutToTheCorner` and
`RadialModel_OnAFourfoldTerm_RecoversTheRadialPartOutToTheCorner` (the model against the
same scene without the term, under 1 % everywhere),
`OnAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure` (end to end, the
ring-step metric under 0.5 %) and by `ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`,
which is the test the rejected designs failed. The frozen `flatcheck.py` mirrored it in full
(`radial_model`, `_ring_fit`) and agreed on all 43 cross-check scenarios; those are now the
parity goldens (ADR 0047).

**The ring-step metric stays out of the report; don't add it.** The number that says
whether a map carries rings of the model's making is the largest change of the residual's
ring mean between two consecutive profile bins (`WorstRingStep` in
`StructureAnalyzerTests`; floor 0.03-0.36 % on the library, 0.5-1.3 % was a visible ring).
It was asked for under the map and turned down (2026-09-23): it is a property of the
model, not of the flat, so printed beside the five tests it reads as a sixth quality
figure that sits at the floor on every real master; and it cannot tell a model artefact
from real fine structure - the on-monitor master's paper texture reads 0.79 % with no ring
of the model's in it, so a warning threshold would fire exactly where `--smooth` is the
answer. The guard is the test suite (the gradient and four-fold scenes, 0.5 % threshold)
and, when the model is touched, the whole-library loop of `docs/radial-model-even-term.md`
§6.1 with the maps looked at on a fixed scale. Shapes are the eye's job, by the tool's own
rule.

**A "vignetting edge" metric was prototyped on the same occasion and rejected; don't
build it.** The rings on the Nikkor ISO800 map had been read as a hard-edged, decentred
falloff, and a metric for hard edges in the radial profile (max deviation from a
Savitzky-Golay smoothing of the ring medians) read 0.03 % on ISO400 and 0.98 % on ISO800,
a clean 30x separation - which was the artefact above, not the flat: on a synthetic with
no edge at all it read 3.05 %, and from the ring *mean* it read 0.05 % on both masters.
With the symmetrised profile both real masters read 0.06 %, so there is no real positive
in the library. Its dynamic range was also too narrow to threshold (synthetics at
1504x1004: smooth 0.02, a soft 10 % knee 0.24, a 1 % hard step 0.38, a 3 % hard step
1.16), and small rasters sit above that margin by discretisation alone (64x64 fixture
0.80 %, 256x256 0.15 %). If a real hard edge ever turns up, that is the starting point;
until then the metric has nothing to measure.

A radial profile cannot be odd, so nothing the model is entitled to keep is lost. What is
lost is a half-pixel's worth of accuracy: `Rotate180` mirrors about `((H-1)/2, (W-1)/2)`
while the rings are measured about `(H/2, W/2)`, so a field radial about the latter is
perturbed by ~0.3 % on a 512² raster and ~2.8 % on 64², fading as ~1/n. Same cause as
known issue 5, negligible at analysis resolution, and pinned by
`Symmetrise_OnTheFixtureFieldCostsOnlyTheHalfPixelArtefact`.

**`AzimuthalAsymmetry(img, cx, cy, nsect = 12, nrings = 10)`** - 10 rings spanning
`0.15·r_ins` to `r_ins`, 12 angular sectors. Per ring: median of each sector, then
`(max - min)/median × 100`. Restricted to the **inscribed** radius because only
there is every azimuth sampled - out in the corners the comparison would be
invalid. Rings needing at least `nsect - 1` populated sectors.

**`LargeScalePtp(resid, ntiles = 40)`** - block-means the residual into ~40-wide tiles
over the **whole frame** and returns `p95 - p5`. The tile averaging is essential: noise
falls as `√(px/tile)` while structure survives. Without it the metric would measure noise,
not error.

It used to keep only the tiles inside the inscribed circle, because beyond it the radial
profile came from partial arcs and was not trusted. The symmetrisation above fixed exactly
that (`OnAGradient_TheResidualHasNoRingsOutToTheCorner` asserts the model is smooth out to
the corner), so the restriction had become a leftover with a real cost: on a 3:2 frame the
circle holds 53% of the tiles, the corners - where a gradient is largest and where light
leaks live - were invisible to the "most important" metric, and the same gradient read
6.5% along the long axis and 9.7% along the short one, because the circle's diameter is
the short side. Whole-frame it reads 10.4 / 10.6, close to the tilt's 12, and the floor on
a clean radial field stays under 0.1% at analysis resolution. Pinned by
`LargeScalePtp_ReadsAGradientTheSameAlongEitherAxis` and
`OnAThreeByTwoFrame_TheResidualReadsAGradientTheSameAlongEitherAxis`. `flatcheck.py`
mirrors it, and the golden files moved (0.24 → 0.29 on the clean fixture, 9.43 → 10.36 on
the gradient one, which crossed into `ACCEPTABIL` - the verdict there was already
`ACCEPTABIL` through the corners).

**`MaxTileDeviation(resid, ntiles = 40)`** - the same tile grid, `max |tile - 1|`, whole
frame. The fifth test; see "Why a fifth test" under mode 1. Floor 0.13-0.37% on clean
fields, 0.4% at 10% pixel noise.

**`LocalSpots(resid, ntiles = 80, win = 3)`** - finer tiles (80 across, ~18 px at analysis
resolution, so a 60 px dust shadow is not diluted), each minus the median of its 7x7
neighbourhood, and the largest |difference| wins, with sign and position. A gradient is
smooth so the neighbourhood median follows it (0.46% at a 12% ramp, 0.68% at 25%); a dust
shadow, ring or small leak stays whole (4.99% on a 5% shadow, 4.71% with a 12% ramp and 3%
noise on top). **Only tiles with a whole neighbourhood are evaluated** - a 3-tile border is
skipped - because at the edge the truncated neighbourhood's median no longer follows a
gradient and the difference comes from the gradient, not a spot: measured on the 256 px
golden fixture, -0.76% in the corner on a *clean* field and -1.61% under a 60% gradient,
one step from a false dust warning. A spot right on the edge is caught by the worst tile
anyway. Ties within 1e-12 keep the first tile in row-major order, in both
implementations: on a symmetric scene two tiles come out equal to the last bit and C# and
numpy sum in different orders, which is how the Python cross-check caught a position flip
on the diagonal fixture (now `parity_structure_diagonal.txt`). Not a test; the warning
threshold is `Th.LocalSpotWarn`.

**The sign is kept, and the report names it in words.** Negative means darker than the
surroundings (dust, dew, an obstruction), positive means brighter (a light leak, a
reflection). `StructureReport.LocalSpotValue` appends `(mai intunecat)` / `(mai luminos)`,
and says nothing at exactly zero, where a constant residual has no direction to state. The
row was printing a bare signed number, and the first question asked once the figure existed
was whether a minus meant a bug - which is the answer to whether the wording was needed.
`StructureReport.LocalSpotNote` carries the explanation and **states the threshold, read
from `Th.LocalSpotWarn` rather than written out**, so a recalibration cannot leave the text
quoting the old value; it is the only place the user learns where the figure starts to
matter. Both applications print that note, the window with the newlines flattened.

**What it can misread, measured on two real masters.** The metric finds the deepest
*compact* departure, but a large-scale feature with a ridge steeper than the 7x7
neighbourhood (~250 px on the sensor) also survives the high-pass. On
`Nikkor 35mm DX/f2.8-sky/ISO400` the extreme is -0.44 % and the five deepest tiles all sit
at `x=117` with different `y`: a vertical stripe down the left edge, i.e. the edge of the
frame's large dark region, not a spot. On the ISO800 master of the same lens the extreme is
`+0.36 %` in the top-right corner, the same kind of thing. Both are a fifth of the warning
threshold, so nothing fired and nothing should have - but on a master with a harder edge
this could warn about dust that is not there. The counter-measure if it ever does: check
whether the extreme is round or elongated before trusting it, since only the round case is
dust. Deliberately not automated - that needs a shape threshold nobody has calibrated, and
the figure is informative, not a verdict.

**Verdict ladder** - `Verdict(val, ok, bad)` → `OK` / `SUSPECT` / `PROBLEMA`;
`Rank` maps those to 0/1/2 and the mode verdict is the worst of its tests. Note
that `NaN` compares false against both bounds and therefore degrades to
**PROBLEMA** - which is how degenerate inputs (too few tiles, no valid rings)
surface. Matches Python.

