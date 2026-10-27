# 0060. The banding verdict is the share of the frame in tiles with a visible plateau, at every one of 5000 positions; the mean plateau informs

Date: 2026-10-27
Status: accepted 2026-10-27
Supersedes: 0015 in part (the plateau's sampling and statistic); 0059 in part (the plateau's statistic)

## Context

ADR 0015 judged banding at 8 bits by the mean plateau over up to 120 clean sky tiles, and ADR
0059 added where that mean was measured, leaving its statistic an open question. Measured on
2026-10-27 over the whole grid of 64 px tiles on eight images
(`docs/plans/2026-10-27-plateau-share-of-flat-tiles.md`): on the owner's 2026-08-12 core
denoised by Syqon at 0.5, 0.75 and 1, the share of the frame in tiles whose plateau exceeds
1 mm at 185 dpi grows with the denoise, 0.18, 0.24 and 0.44 %, while the mean plateau falls,
43, 16 and 4.7 px, and the 90th percentile too, 49, 27 and 5.8 px: at Syqon 1, 25 % of the
frame passes the strict criterion and the many ordinary tiles drown the few dozen fully flat
ones, so the mean cleared the strongest denoise and warned on the lighter two. On the reference
image 25 tiles pass, none over 1 mm at 202 dpi; on the four FITS frames of the validation cases
(the plate-solved core, whose sky keeps a noise of 2.0 LSB, Rosetta, Orion, the Veil) no tile
passes at all. The share is the one statistic that orders the three strengths the way the eye
and the denoise section do. The old sampling stopped at 120 accepted tiles, so on a frame where
most tiles pass it had seen about 460 positions, and a share estimated from it would have been
three times coarser than on a frame where few pass.

## Decision

The plateau's tiles are looked for at every one of `PlateauPositions` (5000) random positions
from the same seed; every accepted tile keeps its plateau (`PlateauTilePx`). The banding
verdict is `PlateauVisibleFramePctAt(dpi)`, the tiles whose plateau is longer than
`PlateauVisibleMm` (1.0 mm) at the reference row's dpi over the positions tried, in percent of
the frame: a risk when it passes `PlateauFlatFrameWarnPct`, 0.1 %, about five tiles on a 23 MP
frame, 4 cm2 on an 80 cm print, the owner's choice on 2026-10-27 over 1 %, because a contour is
a contour wherever the eye lands and the cure, 16 bits or dither at the export, is cheap. The
mean plateau stays in the line, in Appendix C and on the Export tab as the typical step, for
information, with the tiles' count, share of the frame and noise left (ADR 0059's evidence).
Under the threshold the line is Ok and gives the share all the same.

## Alternatives rejected

- **Keep the mean.** It clears the most denoised frame and warns on the lighter ones; it
  measures the typical tile, and banding is the worst tile.
- **The 90th percentile of the per-tile plateau.** Falls with the denoise just as the mean
  does on these frames; a few dozen flat tiles among 1378 are under the 90th percentile.
- **A threshold of 1 % of the frame.** Tolerates the wiped patches on all three Syqon
  strengths; the owner preferred to be told.
- **Keep the stop at 120 tiles.** The share's precision would depend on how much of the frame
  is clean.

## Consequences

- On the owner's core all three Syqon strengths warn, 0.18, 0.24 and 0.44 % of the frame,
  the strongest now rightly; the reference image stays clear (0.00 %); the four FITS frames
  still have no plateau and fall back to the noise's banding rule.
- The dark scene's goldens move: its plateau is measured on every clean tile among 5000
  positions, not the first 120, and the line carries the share and the threshold. V-001's
  figures do not move (20 tiles were all that 5000 positions found before, too);
  `quantization.plateauPositions` and `quantization.plateauVisibleFramePctAtReference` are
  recorded.
- `PlateauTileCount` and `PlateauMaxAttempts` are removed.
- Guards: `QuantizationAnalyzerTests` (R-080), `RecommendationsTests` (R-091), the golden
  files, the validation cases.
