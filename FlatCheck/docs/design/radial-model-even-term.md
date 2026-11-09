# The radial model on partial rings: the even 2θ and 4θ terms

> Cut on 2026-09-27 to what neither CLAUDE.md nor a decision record says (ADR 0051): the
> decision, the headline figures of each rejected design and the accepted cost are ADR 0017; the
> ring-step metric's definition, floors and why it is not printed are ADR 0018. This note keeps
> the full specification of `RingFit`, the measurements behind every design, the tables and the
> re-check procedure. **Covers:** `Metrics.RadialModel` and `Metrics.RingFit` in
> `FlatCheck.Core/Metrics.cs`, their constants (`RingFitMinDet`, `RingSectors`,
> `RingMinSectors`, `RingFitRounds`, `RingQFrom`, `RingFitTie`), their mirror `radial_model` /
> `_ring_fit` in the frozen `flatcheck.py`, the fixture `FitsTestData.WithEvenHarmonics`, and the
> tests
> `RadialModel_OnASaddle_RecoversTheRadialPartOutToTheCorner`,
> `RadialModel_OnAFourfoldTerm_RecoversTheRadialPartOutToTheCorner`,
> `R014_RadialModel_OnASaddleWithAFourfoldTerm_RecoversTheRadialPartOnTheFullRingsToo`,
> `RadialModel_OnAGradient_TheProfileJumpsUnlessTheRasterIsSymmetrised`,
> `OnAGradient_TheResidualHasNoRingsOutToTheCorner`,
> `OnAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure`,
> `OnASaddleWithAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure`,
> `ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`.

Sections 1-5 were measured 2026-09-17 on the six real masters under
`E:\Photografie\Astro\Flats and Darks\` (the Tokina sky master
`Tokina\ISO400\f2.8-sky-no-difuser`, the two Tokina monitor masters, the two Nikkor 35 DX sky
masters, the Samyang LCD master) and on synthetics at the analysis size; section 7, which
replaced the 2θ-only ring fit of sections 3-5 with the 2θ+4θ fit now in the code, was measured
2026-09-23 on all fifteen masters of the library; section 8, which extended the correction
of section 4 to the whole rings, was measured 2026-11-06 on the Rosette master and on
synthetics. Section 4 is the current specification; sections 3 and 5 are kept as the record of
the 2θ-only design and its figures. If the ring model changes, re-run section 6 and update the
tables.

## 1. The problem, as it was found

The Tokina sky master (20 mm f/2.8, 15 frames + 15 rotated 180°, twilight sky near the
zenith) printed a residual map with two lens-shaped blue lobes bounded by sharp arcs and
red corners in diagonal pairs, "concentric with clear delimitations". The numbers: corner
spread 10.28 %, azimuthal 7.25 %, residual 8.81 %, tilt 0.49 %, worst tile 8.41 %.

What the frames contained (quadratic fit of `log I` per frame over `[1, x, y, x², y², xy]`,
coefficients ×100 at the frame edge, x and y each normalised to their own half-side):

| group | bx | by | cxx | cyy | cxy |
|---|---|---|---|---|---|
| frames 1-15 | −4.23 | +4.55 | −16.1 | −2.8 | −6.0 |
| frames 16-30 (rotated) | +4.26 | −4.90 | −15.7 | −2.5 | −6.0 |
| mean of 30 | +0.02 | −0.18 | −15.7 | −2.5 | −6.3 |

The rotation cancelled the linear terms. What survived is **even** under 180°: the saddle
`xy` (−6.3 % at the corners) and the anisotropic falloff (`cxx` ≠ `cyy` beyond what the 3:2
aspect explains: a radial field would give `cyy = cxx·(H/W)² = −7.0`, the master has −2.5).
Both are the sky's: the saddle tracks the dithering within each group (corr with `bx`
+0.97 / −0.94), is chromatic (R −7.0, G −6.4, B −5.9), and is absent on the two monitor
masters of the same lens (−0.24, +0.04).

## 2. The mechanism

`RadialModel` takes per-ring medians on `Img.Symmetrise(img)`. Symmetrising removes the odd
part exactly, which is what cured the first face of the artefact (a gradient). An even
non-radial term passes through it. Inside the inscribed circle every ring is complete and
the median of `1 + a·cos(2θ − φ)` over a full ring is 1: no bias from one harmonic (a mix of
2θ and 4θ does bias it, which is section 8, found on 2026-11-06). Beyond it a ring is only
the arcs near θ = 0 and π (left and right), where `cos 2θ ≈ +1`: the median is pulled to the
value of those arcs. On the sky master the model's slope kinked from 0.21 to 0.71 % per bin
exactly on the inscribed circle (r = 502 px at 1504×1004), and the residual - smooth to
−2.05 % at the circle along the midline - reversed there. Outside the circle the residual's
gradient was 20× the field's own (0.62 vs 0.03 % per px).

Proof that the arcs were the model's, not the flat's: a synthetic
`exp((bx·xn + by·yn + cxx·xn² + cyy·yn² + cxy·xn·yn)/100)` with the fitted coefficients,
no noise, reproduced the map arc for arc (tests 11.63 / 5.53 / 8.25 / 0.38 / 9.12). The
`--histeq` view of the master showed a smooth ellipse and nothing else.

## 3. Designs measured and rejected

All at 1504×1004, residual p95−p5 / worst tile / sharpest step between 8-px tiles of the
residual, in %. "truth error" is the map's error against the true residual of the
synthetic (field / its exact radial part).

| design | synthetic saddle | truth error max | Tokina sky | blotch fixture (residual) |
|---|---|---|---|---|
| ring median (before) | 8.26 / 9.31 / 3.58 | 8.88 | 8.87 / 8.55 / 2.71 | 0.04 (the reference) |
| A: per-ring LS fit `a0 + a2 cos2θ + b2 sin2θ`, a0 from the fit, 3-MAD clipping | 8.96 / 8.63 / 0.22 | 0.46 | 8.97 / 7.45 / 0.42 | **2.62** |
| A′: same with L1 (IRLS) instead of LS | – | – | 8.94 / 7.96 / 0.70 | **2.43** |
| A″: same on 15° sector medians with sector clipping | – | – | 8.95 / 7.98 / 0.72 | **2.95** |
| B: full rings only, amplitudes held from the last full ring, scaled r² | 9.09 / 8.88 / 0.51 | 1.34 | 10.52 / 9.85 / **2.03** | – |
| C: 4θ term added to A | 9.02 / 8.85 / 0.35 | – | 9.63 / 8.72 / 0.69; worst tile on pe-monitor **9.8 → 28** | – |
| D: global quadratic even correction (astig + saddle from a 6-term fit of log I) divided out before the median | 8.92 / 8.45 / 0.22 | 0.26 | 9.30 / 7.84 / 0.59 | 1.34 |
| **final** (section 4) | 9.04 / 8.65 / 0.23 | 0.48 | 9.47 / 7.98 / 0.44 | 1.26 |

Why each failed:

- **A, A′, A″**: a compact blotch of +10 % in a corner (the existing fixture: a Gaussian of
  σ = 5 % of the width at (0.1w, 0.1h)) becomes, after symmetrising, two half-blotches at
  antipodal positions - and that pair *is* a `cos 2θ` signal on the rings it crosses.
  The fit absorbs it (a0 shifted by 2.3 % on those rings, a spurious ring in the map) and
  no clipping catches it: the residuals of the two contaminated sectors (+0.7 %) sit inside
  the MAD of the others (±0.4 %), because the fit is good. L1 changes nothing for the same
  reason.
- **B**: the sky's 2θ amplitude does not grow as r² from the inscribed circle outward
  (relative amplitude per r², ×1e6: −0.115 at r = 503, −0.104 at 548, −0.087 at 639,
  −0.072 at 774), so one ring's value scaled outward leaves a 2 % crease.
- **C**: the 4θ amplitude is unidentifiable in the corners and its r⁴ extrapolation blows up.
- **D**: aliases higher-order *radial* terms into a false astigmatism on a 3:2 frame
  (−0.30 % on a perfectly radial fixture, which is `1 − k·rn²` in linear space, not
  quadratic in log space) and moved the Nikkor 800 worst tile by 1.3 points without cause.
- Absolute amplitudes held from the last identifiable ring (an early form of the final
  design) mis-scaled on a multiplicative saddle by the vignetting factor (1.36 % at the
  corner), and a blotch in that last ring propagated into all four corners.

## 4. The final algorithm (specification, as of 2026-09-23)

Inputs: the symmetrised raster `sym` (H×W), centre `(cx, cy) = ((W−1)/2, (H−1)/2)` (the
geometric centre since ADR 0095; the index `(W/2, H/2)` before), `nbins = 80`.
For every pixel: `r = hypot(x − cx, y − cy)`, `θ = atan2(y − cy, x − cx)`,
`bin = min(int(r / rmax · nbins), nbins − 1)`, `sector = min(int((θ + π)/(2π) · RingSectors), RingSectors − 1)`;
since ADR 0095 `Metrics.Azimuth` computes the sector, `cos 2θ` and `sin 2θ` in the first quadrant
from `|x − cx|`, `|y − cy|` and reflects them by the signs, so a pixel and its mirror land in
mirror sectors with the same cosine and the negated sine, bit for bit (on the diagonals, which
are sector boundaries, `atan2` on the full circle put a pixel in one sector one way up and in
the neighbour the other way up)
with `RingSectors = 24`. A bin with fewer than 8 pixels is skipped (unchanged rule).
`rIns = min(cx, cy)`; a ring is **full** when its centre `(i + 0.5)/nbins · rmax < rIns`.

1. **Full rings**: until 2026-11-06, profile = median of the ring's pixels; since then the
   corrected median of step 4, like the partial rings (section 8).
2. **Ring estimates** (`RingFit`), on every ring with centre `≥ RingQFrom · rIns`
   (`RingQFrom = 0.75`: full rings from three quarters of the inscribed radius outward, and
   every partial ring), of `(a0, a2, b2, a4, b4)` in
   `a0 + a2·cos 2θ + b2·sin 2θ + a4·cos 4θ + b4·sin 4θ`:
   - per sector with ≥ 8 pixels: the median of the values, the means of `cos 2θ`, `sin 2θ`,
     `cos 4θ = 2cos²2θ − 1`, `sin 4θ = 2 sin 2θ cos 2θ`, the pixel count as weight; fewer than
     `RingMinSectors = 6` sectors → not identifiable;
   - **identifiability** is pure geometry: `D` = the four non-constant columns of the sector
     rows, centred; `det(Dᵀ·D / m / 0.5) ≥ RingFitMinDet = 0.02` - the Gram matrix normalised
     to the variance of a harmonic on a full ring, so it reads 1 on a full ring and falls
     toward 0 as the arcs narrow (0.11 at ±60°, 0.04 at ±55°, 0.012 at ±50°: the gate falls
     at ±52°, r ≈ 1.3·rIns, bins 44-56 of 80 on a 3:2 frame). **Not** a correlation
     determinant (ADR 0017 records why that gate failed on the square fixtures);
   - weighted least squares (weights = pixel counts) in `RingFitRounds = 3` rounds; between
     rounds the residuals of all sectors are taken, `MAD · 1.4826` about their median, and a
     sector stays in when `|res| ≤ 2.5·MAD + RingFitTie·|a0|` (`RingFitTie = 1e-9`, for
     noise-free scenes where the MAD is zero); fewer than 6 inliers → all sectors back in.
   The ring's relative amplitudes per r² are `q = (a2, b2, a4, b4) / a0 / r²`.
3. **One `q`**: the component-wise median of `q` over all rings that gave an estimate. A
   single value per r², so the correction is a smooth function of `r` and cannot draw
   rings. The full rings anchor it: on them the harmonics are exactly identifiable, whereas
   from arcs alone the split between 2θ and 4θ is ill-conditioned and extrapolates into the
   unobserved azimuths (section 7). In the corners, where nothing is identifiable, the same
   `q` applies.
4. **Every ring, profile** (the partial rings until 2026-11-06, every ring since): median
   over the ring's pixels of
   `v / (1 + r² (q2c·cos 2θ + q2s·sin 2θ + q4c·cos 4θ + q4s·sin 4θ))`, with `r` the ring's
   centre. Relative (a ratio, like every figure in the tool: a sky term multiplies), and a
   pixel median, so robustness and noise are those of the old profile. Without any estimate
   (a tiny frame) every ring falls back to the plain median.
5. The model is the linear interpolation of the profile over `r`, as before; the residual
   is `img / model` (the un-symmetrised raster), as before.

Cost: under 0.2 s extra on a 1504×1004 raster (~30 rings × 3 solves of 5×5). Both
implementations used the same arithmetic (normal equations, sequential sums), which is why
the parity goldens of ADR 0047 pinned the numbers to the last printed digit until 2026-11-09;
since then `RingFit` sums in `decimal`, exact at this scale and so independent of the order the
pixels come in, because a sum that differed in its last bit between a frame and its mirror
tipped the inlier cut for one sector and moved the model by 1e-4 in the corners (R-131, ADR
0095). The parity goldens were recaptured then.

The 2θ-only design this replaced (2026-09-17 to 2026-09-23) estimated `(a0, a2, b2)` per
partial ring by LMedS over sector triples, smoothed `q` with a running median over 7 rings
(`RingSmooth`) and carried the last window into the corners; sections 3 and 5 are its
record. Section 7 says why it went.

## 5. What it changed, measured

Rings left in the map (profile roughness against a 9-point Savitzky-Golay smoothing, and
per-ring mean of the residual minus a 5-ring running mean, on the sky master):

| | before (median) | after (fit) |
|---|---|---|
| profile, outside the circle, max | 0.24 % | 0.10 % |
| profile, inside, max | 0.13 % | 0.05 % |
| residual rings, outside, max | 0.24 % | 0.14 % |
| residual rings, inside, max | 0.05 % | 0.07 % |

The 0.1 % that remains is the profile's own floor (80 bins, linear interpolation, a noisy
master) and is visible only on the ±2 % map scale; it was there before at twice the size.

Verdict figures, before → after (residual / worst tile):

| master | before | after |
|---|---|---|
| Tokina sky | 8.87 / 8.55 | 9.31 / 7.87 |
| Tokina monitor, close | 6.52 / 6.67 | 6.62 / 6.74 |
| Tokina on monitor | 12.18 / 9.79 | 12.75 / 11.99 |
| Nikkor 35 DX ISO400 | 3.93 / 3.04 | 3.90 / 2.78 |
| Nikkor 35 DX ISO800 | 7.30 / 5.57 | 7.29 / 5.49 |
| Samyang LCD | 3.03 / 2.07 | 2.99 / 2.25 |

The on-monitor master moves the most: both estimators (LMedS and a plain pixel LS) see a
real `cos 2θ` term of −2 to −5 % on its partial rings (the monitor's blotches), which the
median had partly hidden in the profile. No verdict changed. Golden files moved by
0.01-0.05 points; `structure_clean` reads 0.30 % instead of 0.29.

## 6. How to re-check

```bash
flatcheck "E:\Photografie\Astro\Flats and Darks\Tokina\ISO400\f2.8-sky-no-difuser\process\pp_flat_stacked.fit" -o sky.png
dotnet test FlatCheck.sln --filter ParityGoldenTests   # the 39 parity goldens, in the gate
```

Expected on the sky master: corners 10.28 %, azimuthal 7.25 %, residual 9.99 % (10.06 until
2026-11-06, section 8), tilt 0.49 %, worst tile 8.81 %, and a map with two smooth lobes and no
arcs (9.31 / 7.87 with the 2θ-only design). On the Rosette master of section 8
(`E:\Photografie\Astro\online\RosetteNebula\masters\pp_flat_stacked.fit`): residual 3.65 %
(3.76 before) and a map with no circle on the inscribed radius. The synthetic saddle fixture
(`FitsTestData.WithSaddle`, 11 % at 1200×800) and the four-fold fixture
(`FitsTestData.WithFourfold`, 3 % at the corner) must stay under 1 % of model error against
the same scene without the term, and the 2θ + 4θ mix (`FitsTestData.WithEvenHarmonics`,
+4.6 / −1.9 % at the corner) under 0.2 %; the corner-blotch fixture must keep its residual
under 2 %. The whole-library check of section 7 is the loop over every `pp_flat_stacked.fit`
under `E:\Photografie\Astro\Flats and Darks\`, with `--raw` on the first NEF of each
`flats/`.

### 6.1 The ring-step metric, and why it is not in the report

ADR 0018 holds the metric (the largest change of the residual's ring mean between two
consecutive profile bins, bins under 5000 px excluded), its floors on the library, why it is
not printed and where its guard lives (`WorstRingStep` in `StructureAnalyzerTests`, 0.5 % on
the gradient and four-fold scenes). Two things the ADR does not say:

- Why a step is the model's by construction: a real structure in a flat - a blotch, a saddle,
  a gradient - is smooth over hundreds of pixels and cannot move the ring mean by half a
  percent between two bins 11 px apart; a step is the profile jumping. The artefact of
  section 7 was caught at a glance on a master reading 3.76 % and BUN everywhere; the ring
  step alone would not have said what it was without the analysis behind it.
- What it cannot see: a *smooth* bias of the model (the constant `q` misfitting the sky's
  falling saddle by 1-2 % in the corners), which is why the synthetics with known truth sit
  beside it in every table here.

When the ring model is touched: run the section 7 loop over the library, compute the ring
step on each residual (`WorstRingStep` is the implementation), and look at every map on a
fixed scale. A step above ~0.4 % on a master that is not textured is the model's.

## 7. The third face: a 4θ field read as 2θ (2026-09-23)

### 7.1 As it was found

The Tokina tablet master `Tokina\ISO400\f2.8-tableta-lipita-hartie-3-foi-rotiri-perfecte`
(3 sheets of paper on the tablet, 4 × 10 frames rotated 90° in place) printed BUN on every
test and a residual map with a red annulus beyond the inscribed circle, a hard circular
edge on it and fainter rings inside - on a master whose source map, rebuilt in tablet
coordinates from the four orientations (0° against 180° rotated: corr 0.97, rms 0.7 %), is
nothing but smooth blotches of ±4-5 %. The user's words: in nature there are no straight
lines. The single-orientation stacks read 17-18 % residual for 7.6 % of true non-radial
content (tiles, against a smooth polynomial radial fit).

### 7.2 The mechanism

A master built from four orientations rotated 90° keeps, by construction, **no** 2θ part of
the world (it cancels in the four-way mean) and **only** the 4θ family - the world's blotches
smeared four-fold. On the partial rings the arcs sit about the x axis, at ±30..60°, where
`cos 2θ` and `cos 4θ` are nearly collinear, so the 2θ-only ring fit read the 4θ term as 2θ.
Per ring, on that master (relative amplitudes at the ring, % of `a0`):

| | 2θ-only LMedS `q_c·r²` / `q_s·r²` | 5-term LS `a2` / `b2` / `a4` / `b4` |
|---|---|---|
| bin 47, r = 1.07·rIns | +1.78 / −1.53 | +0.52 / −0.05 / −0.27 / −0.97 |
| bin 48 | **−1.32** / −1.54 | +0.56 / −0.04 / −0.23 / −1.09 |
| bin 52 | +3.47 / −2.88 | +0.21 / −0.06 / +0.22 / −1.40 |
| bin 61, r = 1.38·rIns | +3.68 / −1.88 | +0.51 / −0.38 / +0.65 / −0.96 |

The real content of the arcs is `b4 ≈ −1 %` and almost no 2θ; the 2θ-only fit reported
`q_c` of +2..+4 % with the sign flipping ring to ring (LMedS picking different sector
triplets), the 7-ring running median kept the bias and smoothed the flips, and the profile
got a step of −1.67 % on bin 44 (the first partial ring) and bands of ±1 % beyond: the red
annulus. The sky master, the case the 2θ fit was built for, has a real 2θ of −3..−5 % and
`a4, b4 ≈ 0`; there both estimators agree and the correction was right.

### 7.3 What was measured

Synthetics at 1504×1004, the Adobe radial model of the Tokina at 20 mm f/2.8 times a known
field, 0.3 % pixel noise; "error" is model / truth, rms over the frame; "steps" is the
largest change of the residual's ring mean between consecutive rings (rings under 5000 px
excluded, i.e. the corner tips):

| scene | ring median | 2θ-only (2.2.0) | 2θ+4θ, `q` from partial rings | **2θ+4θ, `q` anchored (final)** |
|---|---|---|---|---|
| clean | 0.03 | 0.03 | 0.03 | 0.03 |
| saddle 2θ, 6 % at the corner | 0.76 | 0.03 | 0.03 | 0.03 |
| 4θ, 3 % at the corner | 0.17 | **1.87, steps 2.6** | 0.03 | 0.03 |
| saddle + 4θ | 0.53 | 1.79 | 0.13 | 0.12 |
| saddle whose `q` falls with r (the real sky) | 0.70 | – | 0.12 | 0.16 |
| gradient 11 % (odd) | 0.03 | 0.03 | 0.04 | 0.03 |
| compact blotch +10 % in a corner | 0.08 | 0.14 | 0.09 | 0.08 |
| blotch + saddle | 0.69 | 0.15 | 0.07 | 0.07 |
| blotch + 4θ | 0.15 | 1.45 | 0.08 | 0.08 |

The measurement behind anchoring `q` on the full rings (`RingQFrom = 0.75`) rather than
taking it from the partial rings alone: on the Nikkor 35 DX ISO400 sky master the harmonic
content is not constant in `r` - `a2` per r² runs +1.16 → +0.44 on the full rings and then
climbs +0.65 → +1.30 on the partial ones, with `a4` moving the opposite way, which is the
degenerate direction of the fit on x-axis arcs - and the partial-only median put +2.3 % on
the arcs and 1.3 points on the worst tile (2.75 → 4.22) for no reason visible in the data.
With the full rings from 0.75·rIns in the median the split is anchored where it is exactly
identifiable: 3.24. The other anchorings measured and rejected, and why LMedS went, are in
ADR 0017.

The whole library, old (2.2.0) → new, ring steps as above and the two verdict figures:

| master | ring step before | after | residual before -> after | worst tile before -> after |
|---|---|---|---|---|
| Nikkor 35mm DX ISO400 f2.8-sky | 0.11 % | 0.14 % | 3.87 -> 4.04 | 2.75 -> 3.24 |
| Nikkor 35mm DX ISO800 f2.8-sky | 0.28 % | 0.12 % | 7.54 -> 7.17 | 5.88 -> 5.79 |
| Samyang ISO1600 f2.0-sky | 0.07 % | 0.11 % | 8.32 -> 8.38 | 5.67 -> 5.44 |
| Samyang ISO1600 sursa_neuniforma f2.0-lcd | 0.08 % | 0.11 % | 2.95 -> 2.98 | 2.16 -> 2.36 |
| Samyang ISO1600 sursa_neuniforma f2.8 | 0.16 % | 0.05 % | 6.58 -> 6.61 | 4.21 -> 4.15 |
| Samyang ISO400 f2.0-sky | 0.06 % | 0.10 % | 6.32 -> 6.33 | 4.18 -> 4.17 |
| Samyang ISO400 f2.8-sky | 0.08 % | 0.05 % | 9.92 -> 9.99 | 6.34 -> 6.51 |
| Tokina ISO400 f2.8-monitor-difuzor-aproape | 1.01 % | 0.25 % | 6.97 -> 7.10 | 6.58 -> 6.65 |
| Tokina ISO400 f2.8-monitor-difuzor-pe-monitor | 1.26 % | 0.79 % | 12.32 -> 12.27 | 11.33 -> 12.60 |
| Tokina ISO400 f2.8-sky-no-difuser | 0.31 % | 0.36 % | 9.31 -> 10.06 | 7.87 -> 8.81 |
| Tokina ISO400 f2.8-tableta-lipita-coperti-15-foi | 0.04 % | 0.03 % | 2.37 -> 2.28 | 1.69 -> 1.54 |
| Tokina ISO400 f2.8-tableta-lipita-coperti-7-foi | 0.04 % | 0.03 % | 1.45 -> 1.42 | 1.13 -> 0.95 |
| Tokina ISO400 f2.8-tableta-lipita-hartie-3-foi-rotiri-perfecte | 0.73 % | 0.18 % | 3.76 -> 2.86 | 4.62 -> 2.65 |
| Tokina ISO400 f2.8-tableta-lipita-hartie-3-foi | 0.82 % | 0.20 % | 3.76 -> 3.50 | 2.77 -> 2.77 |
| Tokina ISO400 f2.8-tableta-lipita-hartie-4-foi | 0.54 % | 0.24 % | 5.11 -> 4.98 | 3.93 -> 3.93 |

The maps of all fifteen were looked at side by side (old `flatcheck.png` in each folder
against the new one, both on the automatic scale): the annulus and the hard circle are gone
from the three paper masters and the monitor master, and nothing else changed shape. The
0.79 % step left on the on-monitor master is its paper texture (0.69 % fine texture,
measured by the `--smooth` mode of the time), not the model. The verdicts, the single-orientation stack, the sky
master's accepted cost and the golden drift are in ADR 0017; on the fixtures,
`structure_clean` reads 0.29 % again and its worst tile 0.57 % (0.37 before, the correction's
cost on a 256-px square fixture where only the corner arcs are partial), and the k = 0.6
profile fixture's worst tile crossed from 2.03 BUN to 1.90 PERFECT.

## 8. The fourth face: the median of a 2θ + 4θ mix on a whole ring (2026-11-06)

### 8.1 As it was found

The Rosette master (`E:\Photografie\Astro\online\RosetteNebula\masters\pp_flat_stacked.fit`,
Canon 40D, 17 flats, 3908×2602, analysed at 1954×1301) read GOOD on every test (1.26 / 3.00 /
3.76 / 0.90 / 3.72) and printed a map with a hard circle on the inscribed radius: bright left
and right of it, dark above and below, the inside almost flat. The owner asked why the fix of
section 7 had cured the circles of the Tokina masters and not this one, and whether the model
was too particular to be generic.

### 8.2 The mechanism

Measured with a Python replica of the algorithm on the 2×2-binned, symmetrised raster
(the scratch script of that day; the arithmetic of section 4, not the C#):

- The ring fit is right. Per ring, `a2 / a0 / r²` reads 0.034-0.042e-6 from bin 6 to bin 60
  (0.15 to 1.36 of the inscribed radius), and `a4 / a0 / r²` −0.002 to −0.021e-6 growing
  outward; the median `q` over the 24 rings the code uses is (0.035, 0.002, −0.014, −0.001)e-6.
  The 2θ term is a real saddle of the illumination, ∝ r², the model's own form.
- The azimuthal profile is a mix. On the last whole ring (bin 43, 0.98·rIns) the sector
  medians against the fit's `a0` run +0.85, +1.10, +0.83, +0.23, −0.70, −1.72, −1.86, −0.83,
  −0.01, +0.68, +0.92, +0.89 % over half a turn: fourteen sectors above the level by 0.2 to
  1.1 %, ten below, four of them by 1.7 to 1.9 %. A broad hump on the sides and two narrow
  notches at the top and bottom, which is `a2 cos 2θ + a4 cos 4θ` with a4 of the opposite sign
  (+1.39 / −0.54 % on that ring, +1.43 / −0.60 at the inscribed radius).
- The median of such a profile is not its mean. Numerically, over 200 000 samples of θ:

  | a2 | a4 | mean | median |
  |---|---|---|---|
  | +1.43 | 0 | 0.000 | 0.000 |
  | +1.43 | −0.60 | 0.000 | **+0.600** |
  | +1.00 | −0.50 | 0.000 | +0.500 |
  | +3.00 | 0 | 0.000 | 0.000 |
  | +2.00 | −1.00 | 0.000 | +1.000 |

  A sinusoid's median is its mean, which is the reading section 2 took and section 4 step 1
  relied on. With the 4θ term of the opposite sign the median sits above the mean by about
  |a4|.
- The two halves of the profile did not meet. On the whole rings the plain median sat above
  the level by that bias, 0.23 % at bin 40, 0.36 at 42, 0.44 at 43 (median 0.13775 against
  the fit's a0 0.13714); on the first partial ring (bin 44) the corrected median, 0.13684, is
  the level (a0 0.13688). The profile stepped −0.661 % there against −0.169 before and −0.219
  after; the residual's ring mean stepped 0.361 % on the same bin, and the map drew the
  circle: inside it the residual is the saddle plus the growing bias (+0.7 / −1.3 % on the
  axes at 0.9·rIns), outside it the saddle alone (+1.1 / +1.4 %).

Why the library never showed it: the Tokina sky master is a pure 2θ (a4 ≈ 0, section 1); the
rotated paper masters are a pure 4θ (section 7); on one harmonic the median is the mean. The
mix needs a 4θ of the opposite sign to the 2θ, which this illumination has and none of the
fifteen has to the same degree.

### 8.3 What was measured, and the change

Dividing the harmonics out on every ring with the same `q` (section 4, steps 1 and 4), in the
replica, on the Rosette master and five library masters (step at the first partial ring in
the profile, %; the worst ring step of the residual's mean; p95−p5 of the residual; the worst
32-px tile):

| master | step before -> after (neighbours) | ring step | p95−p5 | worst tile |
|---|---|---|---|---|
| Rosette | −0.661 -> −0.237 (−0.17 / −0.22) | 0.361 -> 0.254 | 3.90 -> 3.78 | 3.74 -> 3.51 |
| Tokina sky | 0.000 -> −0.200 (−0.23 / −0.18) | 0.363 -> 0.338 | 10.32 -> 10.22 | 8.10 -> 8.08 |
| Nikkor 35 DX ISO400 | −0.680 -> −0.609 (−0.59 / −0.67) | 0.137 -> 0.137 | 4.14 -> 4.11 | 3.10 -> 3.08 |
| Samyang LCD | −0.632 -> −0.589 (−0.59 / −0.59) | 0.106 -> 0.106 | 3.16 -> 3.17 | 2.56 -> 2.58 |
| Tokina paper, rotated | −0.980 -> −0.856 (−0.82 / −0.91) | 0.182 -> 0.182 | 2.90 -> 2.89 | 2.66 -> 2.66 |
| Tokina on the monitor | −0.753 -> −0.706 (−0.81 / −1.01) | 0.791 -> 0.791 | 12.81 -> 12.81 | 12.66 -> 12.66 |

Every first-partial-ring step becomes its neighbours' value (the Tokina sky's 0.000 was a
kink of the other sign, under the map's scale); the figures move under 0.1 points. The maps,
side by side on the ±2 % scale: the Rosette circle is gone and the saddle stays, smooth.

In C#, on synthetics at the analysis size (`FitsTestData.Radial` times the even mix, 2400×1600
prepared to 1200×800), the model against the truth (the same scene without the terms), worst
pixel:

| scene | plain median on the whole rings | corrected on every ring |
|---|---|---|
| the Rosette mix, +4.6 / −1.9 % at the corner (`WithEvenHarmonics`) | **0.513 %** | 0.039 % |
| the 11 % saddle (`WithSaddle`) | 0.070 % (replica) | unchanged |
| the 3 % four-fold term (`WithFourfold`) | 0.019 % (replica) | unchanged |

End to end on the mix scene, `StructureAnalyzer`: ring step 0.463 -> 0.232 %, residual
3.60 -> 3.31 %, worst tile 2.90 % both ways. On the Rosette master, the console of the branch:
residual 3.76 -> 3.65 %, the four other tests and the verdict unchanged. The golden drift (six
console snapshots and four parity goldens, 0.01 on one line each: `structure_clean` reads
0.28 % since, and `R012_Structure_PrintsTheScaleAndNamesTheMostImportantMetric`, which quotes
that line, says 0.28 too) and the validation drift are in ADR 0091 and `docs/validation.md`.

The change is one condition in `RadialModel`: the corrected median no longer skips the rings
inside the inscribed circle. `q` is estimated as before, so the rings inside 0.75·rIns, which
never feed it, take a `q` measured further out: constant for 2θ (section 8.2), growing
outward by ten for 4θ, so the single q4 over-divides the inner rings' 4θ by about 0.15 % at
0.5·rIns, a smooth misfit of the kind ADR 0017 accepted, inside the ring-step figure above.
The regression test is
`R014_RadialModel_OnASaddleWithAFourfoldTerm_RecoversTheRadialPartOnTheFullRingsToo` (bound
0.2 %: five times the measured 0.039 %, two and a half times under the old 0.513 %); the
end-to-end guard is
`OnASaddleWithAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure`, which passed
the 0.5 % ring-step bound before the change too (0.463), which is why the model-against-truth
test is the one that decides. Section 6's re-check gains the Rosette master: its map must show
no circle on the inscribed radius.
