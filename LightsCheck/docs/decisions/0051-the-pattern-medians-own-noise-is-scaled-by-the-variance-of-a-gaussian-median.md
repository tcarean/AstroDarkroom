# 0051. The pattern median's own noise is scaled by the variance of a Gaussian median

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The sensor's pattern is read from the per-pixel median of the lights' crops, and the median's
own noise is taken out of its sigma (ADR 0015). That noise is measured on the same crops: the
median of the odd frames minus the median of the even ones holds no pattern and the noise of
two half-medians. To get from that difference to the noise of the median of all the frames the
code used `1 / (n (1 / n_odd + 1 / n_even))`, which assumes that a median's variance is one
constant over its count. It is not: `Raster.Median` averages the two middle values of an even
count, and such a median is tighter than an odd count's. The review of 2026-11-13 (CR-006, `to
measure`) said so. Measured on 2026-11-14 in the C# code, on crops of 512 px of Gaussian noise
alone, where the pattern is 0 % of one frame's noise (two runs, four CFA positions each):

| Frames | Halves | Share until now | Read until now | Share by the variances | Read with it |
| ---: | --- | ---: | ---: | ---: | ---: |
| 7 | 4 + 3 | 0.2449 | 16 to 18 % | 0.2818 | 0 to 7 % |
| 8 | 4 + 4 | 0.2500 | 13 to 14 % | 0.2820 | 0 to 3 % |
| 9 | 5 + 4 | 0.2469 | 14 to 16 % | 0.2839 | 0 to 5 % |
| 10 | 5 + 5 | 0.2500 | 0 %, cut at 0 from -6 to -9 % | 0.2411 | 0 to 5 % |
| 12 | 6 + 6 | 0.2500 | 10 to 11 % | 0.2736 | 0 to 4 % |
| 20 | 10 + 10 | 0.2500 | 6 to 7 % | 0.2654 | 0 to 3 % |
| 32 | 16 + 16 | 0.2500 | 4 to 5 % | 0.2602 | 0 to 3 % |
| 82 | 41 + 41 | 0.2500 | 0 to 2 % | 0.2484 | 0 to 2 % |
| 87 | 44 + 43 | 0.2500 | 1 to 3 % | 0.2539 | 0 to 2 % |

A sensor with no pattern at all printed `10 % of one frame's noise` on 12 frames, the figure of
the Veil session's real pattern. A first run of the measurement drew each crop from its own
seed, the seeds neighbours; .NET's seeded generator gives streams that are not independent
there, and the figures were wrong. One stream for all the crops gives the table.

## Decision

The share is `v(n) / (v(n_odd) + v(n_even))`, where `v(k)` is the variance of the median of `k`
independent Gaussian values of variance 1 as `Raster.Median` takes it, worked out by numeric
integration (`PatternAnalyzer.MedianVariance`): 1, 0.5, 0.4487, 0.2982 and 0.2868 for one to
five values, near `pi / (2 k)` for many. The difference of the half-medians still gives the
scale; only the ratio of the three variances is taken from the Gaussian.

## Alternatives rejected

- **A table of the variances.** Numbers typed where a short integral gives them for any count,
  and a session can have several hundred frames.
- **The pattern read on the mean of two half-medians of equal size**, whose noise is exactly a
  quarter of the difference's variance, with no Gaussian assumed. It changes the median the hot
  pixels are counted on, drops a frame on an odd count, and would move the hot-pixel counts of
  the recorded sessions for a figure that no verdict reads.
- **A simulation with a fixed seed at every run.** At 200,000 draws a variance is known to
  0.3 %, and the share to about 0.5 %, which is the size of the effect on 87 frames.

## Consequences

What is left on noise alone is the scatter of a difference of two near-equal variances: up to
about 7 % of one frame's noise on 7 frames, 4 % on 12, 2 % on 87, cut at 0 from below. A weak
pattern on a short session is therefore known no better than that. The share assumes Gaussian
noise in the frames; the sky's noise of a raw is close to it. The recorded sessions (87 and 82
frames measured, the third refused) move by under 2 % of the pattern's own sigma. No verdict
reads the figure. Guarded by `R046_TheVarianceOfAMedianOfAFewValuesIsTheKnownOne`,
`R046_TheVarianceOfAMedianOfManyValuesNearsHalfPiOverTheCount`,
`R046_TheMediansNoiseShareFollowsTheParityOfTheCounts` and
`R046_NoiseAloneOnAShortSessionReadsAsNoPattern`.
