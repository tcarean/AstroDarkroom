# 0054. The quiet pixels of the noise curve are chosen against the master's own level

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

Check 9's noise curve (R-024) is read on a central crop of the reference plane, at the pixels
that are not hot, so that a hot pixel's own noise does not count as the frame's. "Not hot" was
"under `Th.HotAdu` (20 ADU-eq) above the black". The code review of 2026-11-13 (CR-005) showed
what that does to a set whose dark level sits above the threshold in every pixel, a warm
sensor or biases with a lower pedestal: no pixel is left, the clipped sigma of an empty array
was 0, and the master's noise came out 0.00 ADU with check 9 saying `OK`. The tests of this
task reproduced it on synthetic darks: on a level of 230 against a black of 200 the
frame-to-frame noise read 0 where the same frames on a level of 200 read 2.99 ADU, and on a
level of 219, just under the threshold, it read 3.05, the mask having cut off the crop's upper
part. The owner chose on 2026-11-13, in chat, between a mask relative to the master's level and
an unmeasured noise when no pixel is left.

## Decision

A pixel of the crop is quiet when the median master has it less than the hot threshold above
the crop's median level in the master (`DarkSeriesAnalyzer.NoiseCurve`). The threshold keeps
its value, `Th.HotAdu` in the file's ADU; only what it is counted from changes. And
`Planes.ClippedSigma` answers NaN for an empty array, as its comment already said, so a figure
read on nothing can only be unmeasured.

## Alternatives rejected

- **Unmeasured when no pixel is quiet.** No figure of a set with a low dark level could move,
  but a set near the threshold would keep a noise read on part of its pixels, a warm sensor
  would get no check 9 at all, and the reason would need a text of its own.
- **No mask.** The hot pixels' frame-to-frame noise, several times the quiet pixels', would
  enter the figure; the clipping drops only what lies far out.
- **A threshold relative to the plane's median instead of the crop's.** The curve is read on
  the crop; a glow at an edge would move a level the crop does not have.

## Consequences

The mask can no longer empty: half the crop lies at or under its median. On a set whose dark
level is near the black the mask moves by that level, a fraction of an ADU, and takes in or
leaves out only pixels that sit within it of the threshold. `/validate` was run on 2026-11-13
with the change: the eleven cases of `docs/validation.json` pass, every session present, and
with them the noise curve, the master's noise and the half against half of the five dark sets
that record them, each within its tolerance; no recorded figure was changed. Guarded by
`R024_ASetWarmAboveTheHotThresholdStillMeasuresItsNoise` (levels of 230 and 219 read the plain
series' figures to six decimals) and `R024_TheClippedSigmaOfNoValueIsUnmeasuredNotZero`.
