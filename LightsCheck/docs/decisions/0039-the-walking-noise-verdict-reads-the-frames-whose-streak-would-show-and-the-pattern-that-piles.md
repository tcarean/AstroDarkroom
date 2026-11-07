# 0039. The walking-noise verdict reads the frames whose streak would show and the pattern that piles, not the map's spread

Date: 2026-11-07
Status: accepted 2026-11-07
Supersedes: 0007 in part (the walking-noise rule: half the frames distinct at 10 px, 30 % isolated, a quarter of the moves on each axis); 0015 in part (`Ok` by that dither rule before the dash is read)

## Context

The owner's Veil session of 2026-11-06 (85 frames at 135 mm, a move after 77 of them, typically
17 px = 105" on the sky) came out `INSUFFICIENT for walking noise`: 38 different spots at 10 px
(45 %, the rule wanted 50 %) and 15 frames alone (18 %, it wanted 30 %). Yet the `pattern in the
stack` row read 1.4 % of one frame's pattern against 1.2 % ideal, and the longest run of frames
at one spot was 4 (11-14), a dash of 5.9 px. The owner's objection: a random dither inside a
bounded area must repeat positions, else the target walks out of the frame; and a return to
within 10 px of an earlier frame is not a return to the same pixels. Measured on 2026-11-07
(a prototype over the frames CSV, then the code): the frames inside stretches of two or more
consecutive frames at one spot whose dash reaches 3 px are 53 of 87 (61 %) on the Veil session
of 2026-10-06 (worms in the stack), 7 of 85 (8 %) on the session of 2026-11-06, 0 of 32 at
Smida (its longest run of 12 draws 1.9 px at 20 mm); the pattern kept over the ideal `1 / n` is
1.37, 1.19 and 7.5 times.

## Decision

The verdict reads what reaches the stack. `Insufficient` when the frames inside the stretches
whose dash reaches `Th.WalkingStreakVisible` (3 px, ADR 0015) are `Th.WalkingSharedFraction`
(25 %) or more of the registered frames: the streak sits in too many frames for the stacking's
rejection to drop it. Else `Static` (`NO walking noise`) when the pattern piles in place,
`PatternSurviving` (R-048) above `Th.PatternPilingFactor` (1.5) times `1 / n`. Else `Ok`.
The distinct, isolated and two-axis counts stay in the text as information. Both thresholds
are rules of thumb placed between the measured sessions (8 % and 61 %; 1.37 and 7.5), to tune on
more, as the 3 px of ADR 0015 is.

## Alternatives rejected

- **Keep the rule and soften the word.** The rule measures the map's spread, not the streak; a
  session can fail it with a clean stack, as the 2026-11-06 one does, so a softer word would
  still be a wrong one.
- **A fourth word, `LA LIMITA`, between `OK` and `INSUFICIENT`.** It adds a chip colour and a
  place in the worst-first order for a figure the text already gives in percent.
- **The dash as the stretch's measured span instead of the drift times the frames.** It reopens
  ADR 0015, and small random moves inside a stretch spread the pattern without lining it up,
  which is not a streak.
- **The pattern left out of the verdict.** The owner asked for both; Smida, where nothing walks
  but 23.4 % of the pattern piles, needs a word apart from `OK`, and `Static` already was it.

## Consequences

`DitherAnalysis.WalkingNoiseOk` and `Th.WalkingNoiseDistinctFraction`,
`Th.WalkingNoiseIsolatedFraction` and `Th.TwoDAxisFraction` go; `DitherAnalysis.WalkingStretches`,
`SeriesAnalysis.WalkingSharedFrames` and `WalkingSharedFraction`, `Th.WalkingSharedFraction` and
`Th.PatternPilingFactor` come, with R-084 and its tests. The verdict text gains the share of
frames and the pattern clause, so the goldens' `dithering` and `verdict` rows change. V-008 and
V-013 trade `walkingNoiseOk` for `walkingSharedFrames`; V-014 adds the Veil session of
2026-11-06, which reads `Ok` where the old rule read `Insufficient`. The README's verdict list
and the design note follow.
