# 0019. The --ratio verdict is the structure verdict of the combined master (A+B)/2; A/B is informative

Date: 2026-09-25
Status: superseded by 0067

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-ratio.md; the decision itself dates
from the rewrite of the mode (the note gives no date). The mode used to answer "is the flat
setup clean?" with OK/SUSPECT/PROBLEMA on `A/B`. Measured on three real master pairs of known
geometry it gave two false greens out of three: two correctly rotated stacks with a real ~1.2 %
sky gradient read `SUSPECT` although the rotation had already cancelled that gradient from the
master; two correctly rotated stacks whose master carries 11 % of non-radial structure read
`OK`, because the defect is camera-fixed and the test is blind to camera-fixed things by
construction; two stacks from the *same* orientation read `OK` although nothing had been
measured. Two stacks from the same orientation and two rotated stacks from a genuinely clean
setup produce statistically identical data - `A/B` is only noise in both - so no threshold on
`A/B` can separate them.

## Decision

The verdict is `StructureAnalyzer` run on `S = (A + B) / 2`, the camera-fixed part, which is
the master one gets by stacking both orientations (verified: 4.02 % computed this way against
4.00 % measured on the real `pp_flat_stacked.fit` built from all 30 frames). `W = A / B`, the
world-fixed part at double amplitude, is reported as information only; `Th.RatioOk/Bad` survive
solely as a label on it. Each stack is normalised to a median of 1 before averaging (Siril's
multiplicative normalisation; otherwise a twilight sky fading 12 % between the stacks would
weight A over B and the gradient would not fully cancel). The error bar on `W` is derived, not
calibrated: `log(A/B)` is exactly antisymmetric under a 180-degree rotation, so the even part is
an unbiased noise estimate for the odd part, computed on the tile grid of a centred crop
(`Img.CropCentred`) and halved before reporting. `S` is built at analysis resolution through
`StructureAnalyzer.AnalyzePrepared`. Two maps are written: the residual of `S` at `-o` and
`A/B` beside it with a `-lume` suffix (`RunOutcome.SecondaryPng`).

## Alternatives rejected

- **A threshold on `A/B` (the old OK/SUSPECT/PROBLEMA).** Two false greens out of three on
  real pairs; the separating information is not in the pixels.
- **The odd/even split on raw pixels.** Both halves are dominated by shot noise, which splits
  equally, so the ratio would read 1 whatever the signal; it runs on the tile grid instead.
- **The odd/even split on `Img.BlockMean`'s top-left-anchored grid.** A 180-degree rotation
  lands up to one tile off, which moved the even/odd ratio by ~20 % (measured); hence the
  centred crop. `LargeScalePtp` keeps the top-left anchoring because that is what
  `flatcheck.py` does and parity is tested.
- **Averaging at full resolution.** `Prepare` is linear, so the mean of the prepared rasters
  equals the prepared mean at a thirtieth of the memory. (Corrected 2026-10-24: a sixteenth.
  `Img.Prepare` shrinks by (2k)^2 with k = floor(long side / 2 / 1200); every master in
  `docs/validation.md` is 6016 x 4016, k = 2, and the console prints `dimensiune analiza :
  1504 x 1004` for them. No measurement of the old figure is recorded, and `Prepare` has not
  changed since this decision.)

## Consequences

- No thresholds of its own: residual scale and exit code (ISO800 pair: `OK`/0 -> `ACCEPTABIL`/2).
- The report must keep stating what the mode cannot do: it cannot verify from pixels that the
  camera was rotated, so `RatioRun` prints `STACKCNT` and `DATE-OBS` from both headers
  ("necunoscut" when missing, as on a raw); "camera-fixed" is not "harmless" (a strap, dew or
  a crooked hood rotate with the camera); anything that rotates with the camera is a blind
  spot, and the counter-test for a panel is a distance test (30 cm against 60 cm).
- Guards: the odd/even validation (1.17x with no rotation, 2.59x rotated) and the four
  `--ratio` scenarios in `crosscheck.py`, 32 numbers each; `flatcheck.py` mirrors the rewrite
  (`split_by_rotation`, `world_map_path`, `_provenance`), so a formula change in
  `RatioAnalyzer` goes there too.
- Source note: docs/design/modes-ratio.md.
