# 0016. LargeScalePtp runs over the whole frame, not the inscribed circle

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/pipeline.md (`LargeScalePtp`); the decision
itself dates from the change that followed the symmetrisation of the radial model (ADR 0017).
`LargeScalePtp` block-means the residual `img / model` into ~40-wide tiles and returns
`p95 - p5`. It used to keep only the tiles inside the inscribed circle, because beyond it the
radial profile came from partial arcs and was not trusted. Symmetrising fixed exactly that, so
the restriction became a leftover with a real cost: on a 3:2 frame the circle holds 53 % of the
tiles; the corners - where a gradient is largest and where light leaks live - were invisible to
the metric marked "most important"; and the same gradient read 6.5 % along the long axis and
9.7 % along the short one, because the circle's diameter is the short side.

## Decision

`LargeScalePtp(resid, ntiles = 40)` takes every tile of the frame. Whole-frame, the gradient
reads 10.4 / 10.6 % along either axis, close to the tilt's 12, and the floor on a clean radial
field stays under 0.1 % at analysis resolution. `MaxTileDeviation` uses the same whole-frame
grid.

## Alternatives rejected

- **Keeping the inscribed-circle restriction.** 53 % of the tiles, blind corners, and an
  axis-dependent reading (6.5 vs 9.7 %) of one gradient.
- **Dropping the tile averaging.** Noise falls as `√(px/tile)` while structure survives; without
  tiles the metric measures noise, not error.
- **A rotation-symmetric (centred) tile grid.** `Img.BlockMean` drops partial blocks only at the
  end, so its grid is anchored top-left; `Img.CropCentred` exists for the attribution mode,
  which compares a raster with its 180° rotation, and is deliberately not used here: the
  top-left anchoring is what `flatcheck.py` does, and the numeric parity is tested.

## Consequences

- The golden files moved: 0.24 -> 0.29 on the clean fixture, 9.43 -> 10.36 on the gradient one,
  which crossed into ACCEPTABIL - the verdict there was already ACCEPTABIL through the corners.
- `flatcheck.py` mirrors the change; the `crosscheck.py` scenarios stay in agreement.
- The tile side comes from one `Metrics.TileSize`, shared with `MaxTileDeviation`, `LocalSpots`
  and the attribution mode, so an adjustment of `ntiles` cannot diverge between them.
- The metric now depends on the model being smooth out to the corner; a regression of the ring
  model shows up here first (ADR 0017, 0018).
- Pinned by `LargeScalePtp_ReadsAGradientTheSameAlongEitherAxis`,
  `OnAThreeByTwoFrame_TheResidualReadsAGradientTheSameAlongEitherAxis` and, for the model's
  smoothness that made it safe, `OnAGradient_TheResidualHasNoRingsOutToTheCorner`.

Source note: docs/design/pipeline.md (`LargeScalePtp`, `Img.CropCentred` under `Img.cs` in
docs/design/code-map.md).
