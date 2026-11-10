# 0044. Check 9 weighs what the mismatch leaves against the noise of a light

Date: 2026-11-10
Status: accepted 2026-11-10
Supersedes: 0004 in part (the lights' verdict by the ratio alone)

## Context

Check 9 judged a set of lights against its dark by the amplitude ratio alone (ADR 0004, R-062):
as it is within 0.9 .. 1.1, `-opt` within 0.7 .. 1.4. On 2026-11-10 the Veil lights of
2026-11-08 read 0.85 of the owner's -2 °C darks, so `scade dark-ul cu -opt`, which the owner
does not want: it changes his Siril scripts. Measured the same day, with a script apart from the
tool: the ratio is real (0.85 .. 0.87, the darks taken back to back run 1.3 °C warmer than the
dithered lights), but the master has 183 hot pixels and a plain subtraction leaves, at a typical
one, a hole of 0.73 of the noise of a light on that pixel's colour; 55 pixels keep more than the
noise and 7 more than three times it, in 24 Mpx. A pair the ratio calls `OK`, the Veil lights of
2026-10-06 against the 7 °C darks at 0.94, leaves more: 97 of 758 above the noise, 19 above
three times. The same ratio means a different thing on a cold master and on a warm one.

## Decision

Check 9 also measures what a plain subtraction leaves: for every amplitude pixel of the master,
`|ratio - 1|` x its level over the light's noise on that pixel's colour (the median over the
lights of the local sigma, in ADU-eq), with the median over the pixels and the counts above 1 x
and above `Th.TraceStrongFactor` = 3 x the noise, on a new row `ce ramane`. A ratio inside
0.7 .. 1.4 whose median trace is under `Th.TraceNoiseLimit` = 1.0 x the noise reads `OK`: the
`Direct` band is 0.9 .. 1.1 widened to where the median trace reaches the noise, never past
0.7 .. 1.4, and the window's chart colours each light and draws its green lines by that band.
The traces only relax; the partner's verdicts stay on the ratio. The owner chose the rule over
the count below in chat on 2026-11-10. Measured pairs (the last two reckoned from the indexes):

| Lights | Darks | Ratio | Median trace | Above the noise | Above 3 x | Ratio alone | With the traces |
|---|---|---|---|---|---|---|---|
| Veil 2026-11-08 | -2 °C | 0.85 | 0.73 | 55 of 183 | 7 | -opt | OK |
| Veil 2026-11-06 | -2 °C | 1.06 | 0.28 | 7 of 183 | 0 | OK | OK |
| Veil 2026-10-06 | 7 °C | 0.94 | 0.49 | 97 of 758 | 19 | OK | OK |
| Veil 2026-11-06 | 2.7 .. 1.7 °C | 0.79 | 1.16 | 157 of 251 | 18 | -opt | -opt |
| Smida | 7 °C | 0.77 | 1.06 | 413 of 758 | 70 | -opt | -opt |

## Alternatives rejected

- **A limit on the count of pixels above three times the noise** (one per megapixel, say).
  Two limits with nothing to set them by; and the pair the ratio already accepts at 0.94 has
  more such pixels than the pair it sends to `-opt`.
- **The traces decide both ways**, tightening a ratio inside 0.9 .. 1.1 on a warm master. On
  every measured pair inside that band the median trace is under half the noise; the band the
  owner has relied on since 2026-10-10 keeps its meaning.
- **The noise of the stack instead of one light's.** In a stack the traces add up or spread
  out by the dithering, which the darks and the lights do not tell; the row says so in words,
  and the rule is stated for one light.

## Consequences

R-067 new, R-052 and R-062 amended, R-086 names the chart's moving lines; `TraceFigures` on
`LightsAnalysis`, `IndexAnalyzer.Judge` with a band. The light's noise is the local sigma of
R-050, about 9 % above a single pixel's, so a trace reads that much smaller. The rule assumes
dithered lights: without dithering a trace stays on the same pixel of the stack, where the
noise falls with the number of lights and the trace does not; the row's words carry that, the
verdict does not. A lights-only session against a partner (R-054) and `--lights` (R-055) take
the same rule, their darks being the run's. V-013 records the Veil 2026-11-08 pair.
