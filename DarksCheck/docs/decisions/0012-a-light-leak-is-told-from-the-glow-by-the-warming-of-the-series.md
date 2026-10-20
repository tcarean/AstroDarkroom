# 0012. A light leak is told from the glow by the warming of the series

Date: 2026-10-20
Status: accepted 2026-10-20

## Context

Check 2 read the master's nine regions and said `scurgere de lumina sau glow` when an edge or
corner sat more than `Th.LeakAdu` (1 ADU-eq) above the centre. It could not tell the two apart,
although they call for opposite actions (glow is dark current the dark must carry; a leak is
light the dark must not carry), and it missed a leak under that threshold. The owner's
deliberate leak set of 2026-10-20 (`Flats and Darks/Darks/Lightleaks/darks`: 30 darks of 120 s
at ISO 1600 at room temperature, the eyepiece uncovered, a lamp moved to three positions) read
`OK`, +0.48 ADU-eq top-left, while its last five frames carry +1.9 ADU raw (0.47 ADU-eq) of
light, 35 standard deviations over the series' own scatter.

The figures the core already computes per frame separate the three causes, measured on the
three real series (the hand analysis' CSV files for Smida and the library, the console's for
the leak set; the fits on the hot pixels' amplitude ratio of check 3, raw ADU):

| series | region | mean | slope on the ratio | intercept | residual sd |
| --- | --- | --- | --- | --- | --- |
| library (30 frames, 0.74 → 1.09) | left | 0.63 | 0.53 ± 0.04 | 0.11 | 0.02 |
| | top-left | 0.89 | 0.88 ± 0.15 | 0.02 | 0.08 |
| | bottom | 0.30 | -0.16 ± 0.04 | 0.46 | 0.02 |
| Smida (20 frames, 0.83 → 1.09) | left | 0.15 | 0.09 ± 0.03 | 0.06 | 0.01 |
| | bottom | 0.38 | -0.01 ± 0.02 | 0.38 | 0.01 |
| leak set, frames 1-25 | top | 1.53 | 0.55 | 1.00 | 0.09 |
| | top-left | 1.90 | 1.18 | 0.75 | 0.05 |
| | bottom | 0.17 | -0.27 | 0.43 | 0.02 |

- Glow is dark current, so an edge's excess grows in proportion to the ratio: the library's
  left and top-left edges have an intercept near zero.
- A readout offset is constant and sits in the biases: the bottom edge's intercept is 0.46 in
  the library and 0.38 at Smida, slope nil, and the Veil biases (the same ISO) hold 0.58 there.
- A leak is constant while the source stays, is absent from the biases and steps when the
  source moves: the leak set's top edge has a constant 1.00 ADU with -0.04 in the bias (the
  band of the raised mirror, a sharp edge across the top third); the frame mean, fitted on the
  warming over the first 19 frames (scale 0.055 ADU), jumps +0.25 ADU at frames 20 and 22
  (4.6 sd) and +1.9 ADU at 27-30 (35 sd) while the warming stays smooth (1.05 → 1.13).
- The per-frame scatter of the frame mean around its fit on the warming is 0.10 ADU-eq at
  Smida (20 frames, 2 ADU quantisation at sigma 1.5), 0.017 in the library and 0.023 on the
  leak set's clean frames; the largest clean residual is 2.2 scales. A least-squares fit on the
  leak set is tilted by the lit frames (slope 4.3 instead of 1.4, scale 0.57 instead of 0.08);
  a Theil-Sen fit is not (slope 2.2 on all 30 frames, 1.4 once the lit frames are clipped).

## Decision

1. **The warming is the reference, not the time.** Every per-frame figure is fitted on the
   frame's hot-pixel amplitude ratio (R-023), because glow is proportional to the dark
   current by construction, so its residual is zero whatever the shape of the warming curve.
2. **Theil-Sen with clipping passes** (R-028): slope the median of the pairwise slopes,
   intercept the median of y - slope x, scale 1.4826 x the median absolute residual; up to
   `Th.LeakFitPasses` (3) passes leave out the frames over the limit and refit; every frame's
   final residual against the last fit is its excess light. A frame is lit over
   `Th.LeakFrameSigmas` (5) scales and `Th.LeakFrameAdu` (0.1 ADU-eq): the floor keeps
   quantisation-level bumps on a quiet series out (the library's 5 x 0.017 = 0.085 would flag
   nothing-sized frames), takes the leak set's 0.17 and 0.45 ADU-eq in and leaves its 0.06
   positions out; at Smida 5 x 0.10 = 0.5 ADU-eq rules. The fit needs `Th.LeakFitMinFrames`
   (6) frames; under that the rows read `NEMASURAT`.
3. **The split of a region** (R-029): over the frames not lit, each region's offset to the
   centre is fitted on the ratio; the glow part is the slope (the value at ratio 1, the
   master's temperature), the constant part the intercept; with biases, the bias mean map's
   offset of the region is the readout part and what remains is the leak. The cause is the
   part largest in absolute value: `Glow`, `Readout` or `Leak` with biases, `Glow` or
   `Constant` without, since a constant leak and a readout offset look the same in the darks.
4. Check 2 goes `ATENTIE` on lit frames as well as on the master's offset, and its words for
   the offset follow the cause; check 6's `glow` row shows the glow part; `TABEL PE CADRE`
   shows every frame's excess light.

## Alternatives rejected

- **The master's map alone, as before.** The leak set stayed `OK` at +0.48 ADU-eq while five
  frames carried 0.47 ADU-eq of light each, and the words could not choose between glow and
  leak.
- **A trend in time instead of in warming.** The warming is monotonic in time but not linear
  (it saturates), so a fit on time leaves residuals of its own on a clean series; against the
  ratio the glow's residual is zero by construction.
- **Least squares.** Tilted by the lit frames (slope 4.3 against 1.4, scale 0.57 against 0.08
  on the leak set), which then show at 3 sd instead of 35 and pull clean frames to -2 sd.
- **A second exposure length as the discriminator.** Leak and glow both scale linearly with
  the exposure; only the temperature separates them.
- **A geometric test for the mirror's band.** The eyepiece leak has a sharp horizontal edge
  the glow never has, but a verdict on it needs a model of the shadow; the Harti tab shows it
  to the eye, and a moving source is caught by the per-frame test.

## Consequences

- `LeakAnalyzer` carries the two fits; `DarkVerdictAnalyzer.Judge` reads them into
  `LeakVerdict` and `PatternVerdict`; three constants in `Thresholds.cs` (`LeakFitMinFrames`,
  `LeakFrameSigmas`, `LeakFrameAdu`, with `LeakFitPasses`), measured as above and re-measured
  by `/validate` on V-003, V-004 and V-008 (the leak set).
- A leak that stays constant through a series without biases remains `Constant`: the report
  says so and asks for biases. The owner's remedy is in the words: drop the lit frames or
  reshoot.
- `R028_...` and `R029_...` in `DarkSeriesTests` guard the figures on synthetic series;
  the golden texts carry the new rows and the table column.
