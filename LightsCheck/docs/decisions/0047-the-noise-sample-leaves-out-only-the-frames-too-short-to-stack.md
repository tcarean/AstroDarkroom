# 0047. The noise sample leaves out only the frames too short to stack

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The shot/read ratio of R-024 is measured on three frames, the first, the middle and the last
measured, and the summary's exposure verdict is the weakest channel of the three. The review of
2026-11-13 (CR-001, `docs/review/2026-11-13-code-review.md`) found that the three were taken
whatever their class: a 0.2 s last frame, cut when the intervalometer was stopped, has almost no
sky, reads a ratio near zero and turns the session's verdict to `MARGINAL`. The entry asked for
the sample "among the frames that stay in the stack". Two readings of that lead to different
work, and the owner chose on 2026-11-13. On the Veil session of 2026-10-06 frame 1 is trailed
and `Bad`, and the recorded figure of V-008 is "swamping of R on frame 1 9.4".

## Decision

The sample is the first, the middle and the last of the measured frames that are not flagged
`ShortExposure` (under half the usual exposure, R-085, ADR 0040); when every measured frame
carries that flag, the sample is taken among them, since an empty sample would be read as a
session without a read noise. A trailed frame stays in the sample: its sky and its noise are those of any frame exposed as long, so it says the same about
the exposure.

## Alternatives rejected

- **Only the frames that stay in the stack (good and marginal).** It also drops the trailed
  frames, which changes no verdict (their ratio is the others') and moves the frame the Veil
  figure of V-008 is read on, from frame 1 to frame 2, for nothing gained.
- **The sample as it was, and a `-` in the text when a ratio is not a number.** The verdict
  would still be the short frame's.

## Consequences

`SeriesAnalyzer.Analyze` drops the short frames from the list the three are picked from;
`R024_AFrameTooShortToStackIsNotInTheNoiseSample` and
`R024_WhenEveryMeasuredFrameIsTooShortTheSampleIsThoseFrames` guard it. No recorded figure moves: no
session of `docs/validation.md` holds a short frame. The `NaN %` the summary could print beside
such a ratio is CR-029 of the same list, still open: a ratio at or under zero can also come from
float lights with raw biases. Two cases keep a sentence that does not fit, both for CR-038 of
that list, which holds the summary's sentences about the biases. When every measured frame is
too short (the frames of the usual exposure could not be measured, their headers could), the
sample is the short frames and the verdict is theirs, `MARGINAL` with a ratio near zero: the
figures are those frames' own and the rows name them, where an empty sample printed `the
biases are missing` beside a measured read noise. And when no light was measured at all the
sample is empty and that sentence is still printed.
