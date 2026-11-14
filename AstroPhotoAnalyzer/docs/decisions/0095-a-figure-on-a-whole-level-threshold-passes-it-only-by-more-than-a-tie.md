# 0095. A figure that sits on a whole-level threshold passes it only by more than a tie of 0.0001 levels

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The code review of 2026-11-13 (CR-003) found one family of defects: a threshold that sits on a
whole 8-bit level was compared in floating point against figures that, on an 8-bit source or a
16-bit file made from one, are whole levels too. A sample read from such a file is not exactly
n / 255 in single precision, so a figure exactly on the threshold fell on either side of it by
the float's last bit. ADR 0043 had met the same in the dark shares of `TonalAnalyzer`. Measured
on 2026-11-14 on rasters built as the readers build an 8-bit level:

- the plateau's tile (R-080, "range under 6 levels"): a tile spanning exactly 6 levels was
  accepted at 39 of the 61 darkest base levels and rejected at 22;
- the edge region (R-061, "exceeds 3 levels"): a corner exactly 3 levels over the sky was
  flagged at the sky levels 14, 15, 16, 30, 31, 32 (the delta read 3.0000017) and not at the
  other 45 between 10 and 60 (2.9999998);
- the JPEG error (R-083, "over 2 levels", "at most 10"): an error of exactly 2 counted as over 2,
  and one of exactly 10 made the quality not acceptable, at the reference levels 1, 2, 4, 8, 16,
  32, 64 and 128.

So the plateau's tiles, a region's flag and a JPEG row's share changed with the sky's level on
the files most people send.

## Decision

A figure in 8-bit levels passes a threshold on a whole level only by more than
`Thresholds.LevelTie`, 0.0001 levels (`RasterOps.LevelsOver`, `LevelsUnder`). The float's error
on such a figure is at most 3e-5 levels (255 x 2^-23; the largest measured was 3.8e-6), and the
finest real step, one 16-bit level, is 0.0039 levels: the tie sits between the two, so it
decides the 8-bit ties and nothing else. The rule is used where the review found the ties: the
plateau's tile (range and mean, `QuantizationAnalyzer.Accept`), the edge regions
(`EdgeRegion.Flagged`, `Coloured`, `Recommendations.ColourOf`) and the JPEG error
(`ErrorAccumulator.Add`, `JpegRow.Acceptable`). The figures themselves are not rounded.

## Alternatives rejected

- **Round both sides to 16-bit steps (1/257 of a level) before comparing.** Tried first, the
  same day: exact on integer sources, but on a float source every figure within half a step
  (0.002 levels) of the threshold counted as on it. On the float scene of
  `Golden/dark-sky-wcs.txt` 39 of 786 432 samples left "over 2" at q95 and the printed share
  went from 0.6 % to 0.5 %, a line the task did not mean to change. With the tie 4 samples
  move and no line does.
- **Round the figures themselves where they are measured.** Every printed median of a float
  source would move by up to 0.002 levels, and the figures of `docs/validation.json` with it.
- **Compare the 8-bit levels, as ADR 0043 did for the dark shares.** Right for a count of
  samples under a level; a range, a median's departure or an error on a 16-bit source is not a
  whole level, and rounding it to one would move the rule (a range of 5.6 levels would be 6).

## Consequences

- On an 8-bit source a tile spanning exactly 6 levels is never clean sky, a region exactly 3
  levels from the sky is never flagged, an error of exactly 2 is never "over 2" and one of
  exactly 10 is acceptable, whatever the sky's level.
- `Golden/mono-png8.txt` and its English twin: the share of samples over 2 levels reads 0.6 %
  for 0.8 % at q95 and 1.4 % for 1.5 % at q90. The scene's sky is at level 64, and 164 and 145
  of its 90 000 samples have an error of exactly 2 there (read 2.0000038).
- Guards: `R080_ATileSpanningExactlySixLevelsIsNeverCleanSkyWhateverItsLevel`,
  `R080_AFlatTileAtExactlySeventyLevelsIsNotCleanSky`,
  `R061_ACornerExactlyThreeLevelsFromTheSkyIsNeverFlaggedWhateverTheSkysLevel`,
  `R061_ASpreadOfExactlyThreeLevelsIsNotAColourWhateverTheSkysLevel`,
  `R083_AnErrorExactlyOnAThresholdNeverPassesItWhateverTheLevel`, each on both floats an 8-bit
  level becomes (`SyntheticRasters.EightBitLevels`).
- A new comparison of a level figure with a whole-level threshold uses the same two helpers.
