# 0017. RadialModel: symmetrise, then a 2θ+4θ ring fit with one q per r² anchored on the full rings

Date: 2026-09-25
Status: accepted 2026-09-25; the plain median on the whole rings superseded by 0091

## Context

Retroactive: recorded on 2026-09-25 from docs/design/pipeline.md and docs/design/radial-model-even-term.md;
three dated faces: symmetrisation (before 2026-09-17), the 2θ ring fit (2026-09-17), the 2θ+4θ
fit with one anchored `q` (2026-09-23). Beyond the inscribed circle a ring is only partial arcs;
three things bent the arc median into rings the frame did not contain: a gradient (worst step
2.47 % on a synthetic); an even term that survives symmetrising (the −6 % sky saddle on the
Tokina 20 sky master: slope kinked 0.21 → 0.71 % per bin on the circle, map error 8.9 %); and a
four-orientation master's 4θ term read as 2θ on x-axis arcs, where `cos 2θ` and `cos 4θ` are
nearly collinear (profile stepped −1.67 %, a red annulus on a master of smooth blotches).

## Decision

The model is built on `Img.Symmetrise(img)`; the residual stays `img / model`. Full rings keep
the pixel median. From `RingQFrom = 0.75` of the inscribed radius outward `Metrics.RingFit` fits
`a0 + a2 cos 2θ + b2 sin 2θ + a4 cos 4θ + b4 sin 4θ` on 24 sector medians (≥ 6; identifiable when
the Gram determinant normalised to a full ring is ≥ `RingFitMinDet` = 0.02; 3 rounds of weighted
least squares, sectors beyond 2.5 MAD dropped). One median of the relative amplitudes per `r²`
over those rings is the single `q` (smooth in `r`, so it cannot draw rings); partial rings take the pixel median of `v / (1 + r² Σ q·harmonic)`.

## Alternatives rejected

- **Ring mean instead of median.** Cures the gradient face but loses the median's robustness to stars, hot pixels and dust on single frames.
- **A/A′/A″: `a0` from a per-ring LS, L1 or sector fit.** A +10 % corner blotch becomes an
  antipodal pair, a `cos 2θ` signal the fit absorbs: blotch fixture 0.04 → 2.62 / 2.43 / 2.95 %.
- **B, C, D, absolute amplitudes held.** From the last full ring scaled r²: a 2.03 % crease. 4θ
  extrapolated into the corners: on-monitor worst tile 9.8 → 28 %. A global quadratic even
  correction: −0.30 % false astigmatism on a radial fixture. Held from the last identifiable
  ring: 1.36 % mis-scale, a blotch propagated into all four corners.
- **The 2θ-only fit with LMedS and a 7-ring running median (2.2.0).** On a 3 % 4θ synthetic
  1.87 % rms error with 2.6 % ring steps, against 0.17 for the plain median and 0.03 for 2θ+4θ
  (6 % saddle: median 0.76, both fits 0.03). MAD clipping held the blotch out as well as LMedS
  (0.08 %); the running median left 0.7-1 % steps on the blotchy real masters.
- **`q` from the partial rings only, or another anchoring.** Partial-only: +1.3 points on the
  Nikkor ISO400 worst tile (2.75 → 4.22) for nothing in the data. From 0.5 rIns: sky 10.30 %.
  15-ring running median: a 1.3 % step. Linear trend in `r²`: worst tile 9.86. 4θ from full
  rings with 2θ alone on the arcs: 4.31 on the rotated master.
- **A correlation determinant as the identifiability gate.** Reads 1.0 on a square frame's
  corner arcs however narrow; the 256-px fixtures got REFA on a radial field.

## Consequences

- Ring steps on the real masters: rotated tablet 0.73 → 0.18 %, monitor 1.01 → 0.25, sky
  0.31 → 0.36; a single-orientation stack reads 7.82 % instead of 17.85; the sky master's
  true-residual error 8.9 → 0.5 %. Accepted cost: a constant `q` misfits smoothly where the true
  `q` varies with `r` (sky residual 9.31 → 10.06). No library verdict changed; golden files moved
  0.01-0.2 points. `flatcheck.py` mirrors it, all 43 `crosscheck.py` scenarios agree; touching the model means re-running §6 of the note (ADR 0018).
- Corrected 2026-09-27: this record said 0.17 for `Tokina ISO400
  f2.8-tableta-lipita-hartie-3-foi-rotiri-perfecte` and 0.29 for `Tokina ISO400
  f2.8-monitor-difuzor-aproape`; the whole-library table of `radial-model-even-term.md` (§7.3)
  says 0.18 and 0.25. The metric re-run that day on the eight Tokina ISO400 masters, with the
  definition of ADR 0018 (80 profile bins, bins under 5000 px excluded), reproduced every row of
  that table to the rounding (note / measured, in %): monitor-difuzor-aproape 0.25 / 0.247,
  monitor-difuzor-pe-monitor 0.79 / 0.791, sky-no-difuser 0.36 / 0.363, tableta-lipita-coperti-
  15-foi 0.03 / 0.030, tableta-lipita-coperti-7-foi 0.03 / 0.026, tableta-lipita-hartie-3-foi
  0.20 / 0.197, tableta-lipita-hartie-3-foi-rotiri-perfecte 0.18 / 0.182, tableta-lipita-hartie-
  4-foi 0.24 / 0.241. The 0.29 was `structure_clean`'s residual, quoted twice in the same note.
- Pinned by `RadialModel_OnAGradient_TheProfileJumpsUnlessTheRasterIsSymmetrised` and
  `OnAGradient_TheResidualHasNoRingsOutToTheCorner` (both also assert the unfixed behaviour),
  `RadialModel_OnASaddle_RecoversTheRadialPartOutToTheCorner`,
  `RadialModel_OnAFourfoldTerm_RecoversTheRadialPartOutToTheCorner`,
  `OnAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure` and
  `ACompactBlotchInACorner_IsNoLongerReportedAsPerfect`, the test the rejected designs failed.

Source notes: docs/design/pipeline.md (`RadialModel`), docs/design/radial-model-even-term.md (§3, §4, §7).
