# The banding verdict is the share of the frame with a visible plateau

Branch `claude/Report-the-plateau-as-a-share-of-flat-tiles`, worktree
`.claude/worktrees/Plateau-share-of-flat-tiles` (made from the monorepo root
`E:\Photografie\Code`, base `main` at 9821430). The approved plan is saved to
`docs/plans/2026-10-27-plateau-share-of-flat-tiles.md`.

## Context

ADR 0059 left the plateau's statistic open: the mean over whichever tiles pass the strict
criterion neither compares across denoise strengths nor shows the worst tiles. Measured on
2026-10-27 over the whole grid of 64 px tiles (the strict criterion: range under 6 levels, mean
under 70), per tile the plateau and the noise sigma:

| Image | clean tiles (% of frame) | mean px | p90 px | max px | frame with plateau > 1 mm at 185 dpi | verdict before |
|---|---|---|---|---|---|---|
| `de_printat.png` | 25 (0.5 %) | 2.6 | 2.9 | 2.9 | 0.00 % (none over 1 mm at 202 dpi either) | no banding |
| plate-solved FITS, Rosetta, Orion, Veil | 0 | - | - | - | - | n/a, the noise rule |
| Syqon 0.5 | 10 (0.2 %) | 42.8 | 48.8 | 54.6 | 0.18 % | risk |
| Syqon 0.75 | 15 (0.3 %) | 16.3 | 27.3 | 45.0 | 0.24 % | risk |
| Syqon 1 | 1378 (25 %) | 4.7 | 5.8 | 64.0 | 0.44 % | no banding |

The mean and the 90th percentile both fall as the denoise grows, because the many ordinary
tiles of the heavily denoised frame drown its few dozen fully flat ones; the share of the frame
in tiles with a visible plateau grows with it, and is the only candidate that orders the three
strengths as the eye and the denoise section do.

## Decision

The owner's, 2026-10-27: the verdict on the share of the frame in clean tiles whose plateau
passes the visible length at the reference row's dpi, threshold 0.1 % (over 1 %), the tiles
looked for at every one of 5000 random positions (no stop at 120), the mean plateau kept as an
informative figure. ADR 0060 records it and supersedes ADR 0015 and ADR 0059 in part.

## Steps

1. R-080 and R-091 restated; tests: the share on a ramp (100 %), on a noisy flat sky (0 %), and
   on a noisy sky with one flat patch, where the mean stays small and the share says the truth;
   the dark scene's line.
2. `QuantizationAnalyzer`: 5000 positions, `PlateauTilePx`, `PlateauPositions`,
   `PlateauVisibleFramePctAt`, `PlateauVisibleAt` on the share; `Thresholds.PlateauPositions`,
   `PlateauFlatFrameWarnPct`; `PlateauTileCount` and `PlateauMaxAttempts` removed.
3. Texts: the banding line's "because", Appendix C, the Export tab's row and description.
4. Goldens, `/validate` (two new figures recorded; V-001 unmoved), ADR 0060, the Status lines
   of ADR 0015 and ADR 0059, `measurements.md`, `thresholds.md`, `known-limitations.md`,
   `validation.md`, CHANGELOG.

## Deviations

Noted at the wrap-up of 2026-10-27.

- The first R-080 test bounded the share of its square scene at 7 to 12 %; it measured 12.9 %,
  because a tile mostly inside the flat square also averages over the visible length. The bound
  became 9 to 16 %, with the reason in the test.
- The branch went past this plan at the owner's word: the share and the threshold in bold on the
  Raport tab (the `**` marks, recorded at the wrap-up as ADR 0063), "do not go below q100" and
  "keep the 16-bit master" moved to the Export tab (ADR 0061), and a density floor for the stars
  (ADR 0062).
- Two subagent code reviews followed, of the branch and then of the commit that applied the
  first: nine findings and six of a second pass, then seven, all applied, among them the
  starless validation test's count guard, which ADR 0062 had weakened.
