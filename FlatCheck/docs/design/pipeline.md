# The core idea, the input contract, the mode dispatch and the shared algorithms

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the premise and the centre convention are
> ADR 0013, the metric decisions ADR 0014 to 0018 and ADR 0041, and this note keeps the input
> contract, the mechanics of the shared algorithms and the measurements no ADR records.
> **Covers:** in full, `Img.cs`, `Stats.cs` and `Metrics.cs` (`Prepare`, `Percentile`, `Solve`,
> `FitCenter`, `RadialModel`, `AzimuthalAsymmetry`, the verdict ladder); by pointer,
> `LargeScalePtp` (ADR 0016), `MaxTileDeviation` (ADR 0015), `LocalSpots` (ADR 0041) and
> `RingFit` (ADR 0017, 0018; the long form is `radial-model-even-term.md`).
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## The core idea

ADR 0013 holds it: for a photographic lens with no optical train a correct flat is a purely
radial function centred on the optical axis, so any departure from radial symmetry is error and
a number that can be measured; and the convention that follows, that the radial model and the
azimuthal asymmetry are computed around the sensor centre `(w/2, h/2)`, never around the fitted
illumination peak, which would absorb exactly the gradient the test exists to detect. `FitCenter`
never supplies that centre; it supplies the tilt and the vertex offset, and why the offset is
informative while the tilt is the test is ADR 0014.

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
failure modes (see "Raw-file sanity gate" in `modes-expo.md`).

Accepted formats: FITS (`.fit`/`.fits`/`.fts` - though the extension is never
actually checked, see `known-issues.md` item 2), BITPIX 8/16/32/-32/-64, mono or colour; and
camera raw (`.nef`/`.cr2`/`.cr3`/`.arw`/`.dng`/`.raf`), where the extension **is**
what dispatches. A raw carries no FITS header, so `--light` sees an empty
dictionary and lands in the guard's `Unknown` layer - runs, with a note (ADR 0020).


## The six modes

Dispatch order in `FlatCheckCli.Run` is `--lenses` → `--synthetic` → `--against` →
`--smooth` → `--histeq` → `--expo` → `--light` → structure (default; `--ratio` sat between
`--expo` and `--light` until its removal, ADR 0065). The
modes are mutually exclusive; the first match wins (R-003; the mechanics of the chain are
"Dispatch in FlatCheckCli.Run" in `process.md`). The WPF app has no dispatch: each tab is one
mode.


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
and returns `(cx, cy, tiltPct, vertexValid)`. In `tiltPct = 2·sinh(|b| + |c|)·100`, `|b| + |c|`
is the linear term's value at the corner (where `xn = yn = 1`), so the figure is that term's
peak-to-peak amplitude over the frame. The vertex comes from `M = [[2d, f], [f, 2e]]`. Why
`sinh` and not the linear approximation, why the cross term `f` is left out, the fallback to
the geometric centre with `vertexValid = false` and the `NaN` tilt on a singular system are
ADR 0014; that the tilt is a ratio like every other metric is ADR 0012.

**`RadialModel(img, cx, cy, nbins = 80, pct = 50)`** - bins pixels by radius into
80 equal-width bins out to `rmax`, takes the median per bin (bins with < 8 pixels
are dropped), then rebuilds a 2-D model by linear interpolation of that profile.
`Interp` clamps at both ends, matching `np.interp`.

**It is fed `Img.Symmetrise(img)`, not `img`** - the mean of the raster and its 180°
rotation; the residual is still `img / model`, so the gradient the map exists to show is
untouched. The mechanism it cures: beyond the inscribed circle a constant-radius ring is only
partial arcs, a gradient makes their pixel distribution bimodal, and the median falls into the
sparse gap between the two modes, jumping from bin to bin by an amount set by the gradient;
the interpolated model inherited the jumps and the map drew them as concentric rings plus a
hard circular edge on the inscribed radius, on a frame that contained neither. The verdict
numbers barely moved at the time (`LargeScalePtp` was then restricted to the inscribed radius,
where the median was already exact), but the map is the only place a *shape* is read, and it
was lying. The measurement, why symmetrising rather than the ring mean, and the two tests that
also assert the unfixed behaviour are ADR 0017.

Symmetrising removes the *odd* part only. An *even* non-radial term - a saddle (`xy`) or a
falloff steeper along one axis than the other (`x² - y²`), which is what a twilight sky leaves
in a 70° field and what a 180° rotation cannot cancel - goes through it untouched, and the
median on the left/right arcs beyond the inscribed circle absorbed it into the profile; on a
master built from four orientations the same arcs read a 4θ term as 2θ. The correction,
`Metrics.RingFit` on the partial rings (the 2θ + 4θ basis, the sector medians, the three
MAD-clipped rounds, the identifiability gate `RingFitMinDet`, one `q` per `r²` anchored on the
full rings from `RingQFrom` = 0.75 of the inscribed radius), the measurements on the Tokina sky
and tablet masters and on the synthetics, and the designs rejected on the way are ADR 0017.
The full specification with every measurement is `radial-model-even-term.md` (§7 for the 4θ
face), and the header comment of `RadialModel` lists the rejected designs beside the code; read
both before changing the method. Two measurements the ADR does not record:

- The saddle face was proved to be the model's by reproducing the arcs, arc for arc, on a
  smooth synthetic with the fitted coefficients. On the sky master the sharpest step between
  8-px tiles of the residual went 3.58 → 0.23 % on that synthetic and 2.71 → 0.44 % on the
  master.
- On the 4θ synthetics beyond the two the ADR quotes (saddle + 4θ, gradient, compact blotch,
  blotch + saddle, blotch + 4θ) the 2θ + 4θ model's rms error against the truth is under
  0.13 %. The user's words on the red annulus the 2θ-only fit had drawn on a master of smooth
  blotches: "in nature there are no straight lines".

**The ring-step metric stays out of the report, and the "vignetting edge" metric was
prototyped and rejected; don't add either.** ADR 0018 holds both: what each measured, why the
ring step is a property of the model and not of the flat, why the edge metric's 30x separation
was the ring artefact and not the flat, the figures, and the guards (`WorstRingStep` in
`StructureAnalyzerTests` on the gradient and four-fold scenes; the whole-library loop of
`radial-model-even-term.md` §6.1 whenever the model is touched).

A radial profile cannot be odd, so nothing the model is entitled to keep is lost to the
symmetrisation. What is lost is a half-pixel's worth of accuracy, the same cause as known
issue 5 and negligible at analysis resolution; ADR 0013 has the figures and the test.

**`AzimuthalAsymmetry(img, cx, cy, nsect = 12, nrings = 10)`** - 10 rings spanning
`0.15·r_ins` to `r_ins`, 12 angular sectors. Per ring: median of each sector, then
`(max - min)/median × 100`. Restricted to the **inscribed** radius because only
there is every azimuth sampled - out in the corners the comparison would be
invalid. Rings needing at least `nsect - 1` populated sectors.

**`LargeScalePtp(resid, ntiles = 40)`** - block-means the residual into ~40-wide tiles
over the **whole frame** and returns `p95 - p5`. Why the tiles, why the whole frame and no
longer the inscribed circle, the figures, the golden moves and the tests are ADR 0016.

**`MaxTileDeviation(resid, ntiles = 40)`** - the same tile grid, `max |tile - 1|`, whole
frame. Why it is the fifth test, and its floor, are ADR 0015 (and "Why a fifth test" under
mode 1 in `modes-structure.md`).

**`LocalSpots(resid, ntiles = 80, win = 3)`** - finer tiles (80 across, ~18 px at analysis
resolution, so a 60 px dust shadow is not diluted), each minus the median of its 7x7
neighbourhood (`win` tiles on each side), and the largest |difference| wins, with sign and
position. The rest is ADR 0041: what a gradient and a shadow read, why the 3-tile border is
skipped, the tie rule, the sign named in words and the threshold read from `Th.LocalSpotWarn`,
what it can misread on two real masters, and why the round-or-elongated check stays manual.

**Verdict ladder** - `Verdict(val, ok, bad)` → `OK` / `SUSPECT` / `PROBLEMA`;
`Rank` maps those to 0/1/2 and the mode verdict is the worst of its tests. Note
that `NaN` compares false against both bounds and therefore degrades to
**PROBLEMA** - which is how degenerate inputs (too few tiles, no valid rings)
surface. Matches Python.
