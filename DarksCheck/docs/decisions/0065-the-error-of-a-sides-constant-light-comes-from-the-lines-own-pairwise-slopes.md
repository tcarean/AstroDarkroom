# 0065. The error of a side's constant light comes from the line's own pairwise slopes

Date: 2026-11-16
Status: accepted 2026-11-16
Supersedes: 0014 in part (the error of a side's constant light by least squares' formula)

## Context

A side's constant light is the value at ratio 0 of the line fitted to the side's per-frame
offsets on the warming (R-029, ADR 0014). The line is Theil-Sen's, the median of the pairwise
slopes; its error was least squares' formula, the residual scale x sqrt(1 / n + mean(ratio)^2
/ Sxx). The two do not go together. One frame far from the others makes Sxx large and the
formula's error small, since least squares would read the slope across that long lever; the
median of the pairwise slopes gains nothing from one frame, its slope still sits among the
many pairs of frames close to one another. The gate that the ratios span 0.1
(`Th.LeakFitMinSpan`) is highest minus lowest, so that one frame opens it. The code review of
2026-11-13 (CR-015, `to measure`) gave the case: 30 frames, the first at ratio 0.5, the others
between 1.00 and 1.03. R-023's own examples have a first frame at 0.5 of the master.

Measured on 2026-11-16 with a copy of the fit in Python, kept outside the repository: a side
with no constant light, a per-frame scatter of 0.02, 2000 series each; how much the intercept
really scatters, the error printed, and how often the side is read as light (over 0.15 and
over 3 times the error):

| Series | Really | Least squares' formula | From the line's own slopes |
|---|---:|---:|---:|
| one cold frame at 0.5, 29 at 1.00 .. 1.03 | 0.278 | 0.038, read as light in 17.9 % | 0.304, in 0.1 % |
| two cold frames, 28 at 1.00 .. 1.03 | 0.178 | 0.030, in 8.5 % | 0.174, in 0.0 % |
| even warming 0.8 .. 1.2, 30 frames | 0.033 | 0.029, in 0.0 % | 0.032, in 0.0 % |
| even warming 0.95 .. 1.05, 30 frames | 0.126 | 0.115, in 0.5 % | 0.130, in 0.1 % |
| even warming 0.8 .. 1.2, 8 frames | 0.060 | 0.039, in 0.4 % | 0.074, in 0.2 % |

The owner chose on 2026-11-16.

## Decision

The constant part of a side keeps its value. Its error is `sqrt(scale^2 / n + (mean(ratio) x
slope error)^2)`: the level of the line at the mean ratio, known to the residual scale over
sqrt(n), carried to ratio 0 by a slope that is itself uncertain. The slope error is half the
range the pairwise slopes give at one sigma, Sen's interval: with the N pairwise slopes sorted
and `C = sqrt(n (n - 1) (2n + 5) / 18)`, the standard deviation of Kendall's statistic for n
points, from slope `floor((N - C) / 2)` to slope `ceil((N + C) / 2)`, counted from 0
(`LeakAnalyzer.SlopeError`). The rule of ADR 0014 stands: a side is constant light when what
remains after the bias passes `Th.LeakConstantAdu` and `Th.LeakConstantSigmas` times this
error.

## Alternatives rejected

- **The row unmeasured when the warming rests on one frame** (the span without the lowest and
  the highest ratio under 0.1). It answers the case of the entry and no other: two cold frames
  pass that gate and the error is still six times too small, and on short series the old error
  stays too small (0.039 printed for 0.060 at eight frames).
- **The line by least squares, whose formula the error already was.** With a cold frame it is
  the better line (the intercept then really holds to 0.040), but it moves the recorded values
  of the sides and one odd frame pulls it: on the Smida darks' top edge it reads -0.01 +/- 0.23
  where Theil-Sen reads +0.04 +/- 0.05 (the same Python copy, on the prototype's per-frame
  figures).

## Consequences

A series whose warming rests on one or two frames gets an honest, large error, and no side of
it is called light; its row prints figures such as `+0.05 (+/-0.30)`, which say that the
series cannot tell. On an ordinary series the error grows a little, most on a short one (about
twice at eight frames), so a weak constant light on a short series may no longer pass three
times its error. The values, the glow / constant split and the frames named are untouched. The
error measured on the real sets, before and after, is in the plan
`docs/plans/2026-11-16-fix-cr-011-to-cr-015.md`. Guarded by
`R029_AWarmingThatRestsOnOneColdFrameGivesASideAnErrorThatFitsItsLine` and
`R029_TheSlopeErrorIsHalfTheRangeOfThePairwiseSlopesAtOneSigma`;
`R029_LightInEveryFrameIsReadPerSideAndTheBiasClearsAReadoutBand` bounds the error of its
eight-frame series at 0.67, where it bounded the old error at 0.4.
