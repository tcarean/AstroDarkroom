# 0068. The noise from one frame to the next is read on every pair of frames

Date: 2026-11-19
Status: accepted 2026-11-19

## Context

The master's noise of check 9 is `Th.MedianNoiseFactor` x the noise from one frame to the next
/ sqrt(N) (R-024, ADR 0007), and that noise was the first point of the measured curve: the
clipped sigma of the first decoded frame minus the second, over sqrt(2). The frames needed, the
table of check 9 (ADR 0051) and check 5's noise above the read noise all follow from it. The
code review of 2026-11-13 (CR-018, `owner`) gave the case of one odd frame: a first frame of
noise 9 among frames of noise 3 reads 6.7 instead of 3, so the frames needed come out about
five times too many; check 5 names the frame, check 9 does not leave it out. The biases' read
noise is already the median over the consecutive pairs.

Measured on 2026-11-19 on the six real dark sets, with a throw-away Python copy of the curve's
first point that gives the recorded figures to the printed decimals (in ADU):

| Set | First pair | Median of all pairs | Lowest .. highest pair | Master's noise |
|---|---:|---:|---|---|
| Smida (20 darks) | 1.426 | 1.475 | 1.426 .. 1.511 | 0.400 to 0.413 (+3.4 %) |
| Library idx67 (30) | 4.830 | 5.405 | 4.830 .. 5.610 | 1.105 to 1.237 (+11.9 %) |
| Leak set (30) | 6.261 | 6.785 | 6.261 .. 8.711 | 1.433 to 1.553 (+8.4 %) |
| Library idx47 (30) | 4.421 | 4.697 | 4.409 .. 4.835 | 1.012 to 1.075 (+6.2 %) |
| Library idx33 (30) | 3.991 | 4.192 | 3.984 .. 4.276 | 0.913 to 0.959 (+5.0 %) |
| Minus 2 idx13 (35) | 3.405 | 3.381 | 3.295 .. 3.497 | 0.721 to 0.716 (-0.7 %) |

In five of the six the first pair is the quietest of the series, or within 0.3 % of it: the
camera is still cold, and the noise grows as it warms over the series. So the figure was too
small on every series that warms, with no odd frame in it. The owner chose on 2026-11-19, with
this table and what follows from it for check 9, between the median over all pairs and leaving
the rule as it was.

## Decision

The noise from one frame to the next (`NoiseVsN.TemporalSigma`) is the median, over every pair
of consecutive decoded frames, of the clipped sigma of their difference over sqrt(2), at the
quiet pixels of the crop (ADR 0054). With two frames it is that one pair's figure, as before;
under two frames there is none. The master's noise, the table and the frames needed of check 9,
the verdict against the lights' stack and check 5's noise added during the exposure follow it.
The measured curve and its row keep their points, the mean of the first n frames against the
next n, the first point included: they show that the frames are independent, not how noisy the
master is.

## Alternatives rejected

- **The first two frames, as before.** One odd frame moves the figure whole, and on a series
  that warms it reads the coldest pair for a master made of every frame.
- **The first pair, without the frames check 5 names as outliers.** It mends the odd frame and
  leaves the cold first pair.
- **The root of the mean of the squared pair figures**, which is what a mean master's noise
  follows exactly. One odd frame moves it, and the gain over the median is small on an ordinary
  series; the median is also what the biases' read noise takes.

## Consequences

The master's noise of a series that warms grows by 3 to 12 % on the real sets and the frames
needed with it, by the square: the 15.8 °C library with the Veil lights of 2026-10-06 needs
about 49 darks at 10 % where it read 39. The recorded figures that follow from this noise
(`masterNoise` and check 9's figures in `docs/validation.json`, the examples of R-024) are
measured again with the program and replaced, at the owner's choice; `docs/validation.md` has
the table. The earlier figures in ADR 0007, ADR 0051 and ADR 0056 are those of the first pair
and carry a dated note. The golden files moved in the last decimals of these figures only.
Reading n - 1 differences of the crop instead of one adds about a second to a run of sixty
darks. Guarded by `R024_OneNoisyFirstFrameDoesNotSetTheMastersNoise`.
