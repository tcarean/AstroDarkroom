# The radial model on partial rings: the even 2θ term

Covers `Metrics.RadialModel` and `Metrics.RingFit` in `FlatCheck.Core/Metrics.cs`, their
constants (`RingFitMinDet`, `RingSectors`, `RingSmooth`, `RingTripleMinDet`, `RingFitTie`),
their mirror `radial_model` / `_ring_fit` in `flatcheck.py`, and the tests
`RadialModel_OnASaddle_RecoversTheRadialPartOutToTheCorner`,
`RadialModel_OnAGradient_TheProfileJumpsUnlessTheRasterIsSymmetrised`,
`OnAGradient_TheResidualHasNoRingsOutToTheCorner`,
`ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`. Measured 2026-09-17 on the six real
masters under `E:\Photografie\Astro\Flats and Darks\` (the Tokina sky master
`Tokina\ISO400\f2.8-sky-no-difuser`, the two Tokina monitor masters, the two Nikkor 35 DX sky
masters, the Samyang LCD master) and on synthetics at the analysis size. If the ring model
changes, re-run section 6 and update the tables.

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
the median of `1 + a·cos(2θ − φ)` over a full ring is 1: no bias. Beyond it a ring is only
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

## 4. The final algorithm (specification)

Inputs: the symmetrised raster `sym` (H×W), centre `(cx, cy) = (W/2, H/2)`, `nbins = 80`.
For every pixel: `r = hypot(x − cx, y − cy)`, `θ = atan2(y − cy, x − cx)`,
`bin = min(int(r / rmax · nbins), nbins − 1)`, `sector = min(int((θ + π)/(2π) · RingSectors), RingSectors − 1)`
with `RingSectors = 24`. A bin with fewer than 8 pixels is skipped (unchanged rule).
`rIns = min(cx, cy)`; a ring is **full** when its centre `(i + 0.5)/nbins · rmax < rIns`.

1. **Full rings**: profile = median of the ring's pixels. Unchanged from before.
2. **Partial, identifiable rings** - identifiability is pure geometry: with `c = cos 2θ`,
   `s = sin 2θ` over all pixels of the ring, `det = (n(Σc²Σs² − (Σcs)²) − Σc(ΣcΣs² − ΣcsΣs) + Σs(ΣcΣcs − Σc²Σs)) / (nΣc²Σs²)`
   (the determinant of the correlation matrix of the basis `[1, c, s]`); identifiable when
   `det ≥ RingFitMinDet = 0.10`, which on a 3:2 frame is r up to ~0.85·rmax (bins 44-69 of 80,
   the corners excluded). On each such ring `RingFit` estimates `(a0, a2, b2)` of
   `a0 + a2·c + b2·s`:
   - per sector with ≥ 8 pixels: the median of the values, the mean of `c` and of `s`, the
     pixel count as weight; fewer than 4 sectors → not identifiable;
   - **LMedS** over all sector triples `(i, j, k)`: the exact solution by Cramer's rule
     (`d3 = (mc_j·ms_k − ms_j·mc_k) − (mc_i·ms_k − ms_i·mc_k) + (mc_i·ms_j − ms_i·mc_j)`,
     skipped when `|d3| < RingTripleMinDet = 1e-9`), scored by the median of the squared
     residuals over all sectors; the first strict minimum wins;
   - scale `1.4826·√score·(1 + 5/(m − 3))` (Rousseeuw's small-sample factor), inliers
     `|res| ≤ 2.5·scale + RingFitTie·|a0|` (`RingFitTie = 1e-9`, for noise-free scenes where
     the scale is zero); fewer than 4 inliers → the LMedS candidate is returned;
   - weighted least squares on the inliers (weights = pixel counts) gives `(a0, a2, b2)`.
   The ring's relative amplitudes per r² are `q_c = a2 / a0 / r²`, `q_s = b2 / a0 / r²`.
3. **Smoothing across rings**: for each partial identifiable ring, `q̃` = the median of `q`
   over the identifiable rings within ±3 positions in the list of identifiable rings
   (`RingSmooth = 7`, truncated at the ends); for a partial *non-identifiable* ring (the
   corners), `q̃` = the median over the last 7 identifiable rings.
4. **Partial rings, profile**: median over the ring's pixels of `v / (1 + q̃_c·r²·c + q̃_s·r²·s)`,
   with `r` the ring's centre. Relative (a ratio, like every figure in the tool: a sky term
   multiplies), and a pixel median, so robustness and noise are those of the old profile.
5. The model is the linear interpolation of the profile over `r`, as before; the residual
   is `img / model` (the un-symmetrised raster), as before.

Cost: ~0.3 s extra on a 1504×1004 raster (220 triples × ~26 rings). Both implementations
use the same arithmetic order where it matters (Cramer's rule, sequential sums); C# and
Python agree on all 43 `crosscheck.py` scenarios and print identical figures on the real
sky master.

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
python flatcheck.py "<same file>" -o sky_py.png     # same figures to the printed digit
FLATCHECK_DUMP_FIXTURES=/tmp/xc dotnet test FlatCheck.sln --filter DumpsFixtures && python crosscheck.py /tmp/xc
```

Expected on the sky master: corners 10.28 %, azimuthal 7.25 %, residual 9.31 %, tilt 0.49 %,
worst tile 7.87 %, and a map with two smooth lobes and no arcs. The synthetic saddle fixture
(`FitsTestData.WithSaddle`, 11 % at 1200×800) must stay under 1 % of model error against the
same scene without the saddle; the corner-blotch fixture must keep its residual under 2 %.
