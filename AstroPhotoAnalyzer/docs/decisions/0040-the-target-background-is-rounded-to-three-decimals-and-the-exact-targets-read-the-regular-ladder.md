# 0040. The target background is rounded to three decimals and must lie strictly inside (0, 1); the exact targets are read on the regular ladder only

Date: 2026-10-23
Status: accepted 2026-10-23

## Context

The code review of area 2 (2026-10-23) found three defects around the given target background
(`--target-bg`, the window's "Fundal-tinta"): a target that rounds to 0 or 1 at six decimals,
or NaN, or exactly 1 from the window (whose field accepted (0, 1]) ended the run with an error
and no report (item 6); a target with a seventh decimal lost its `(dat)` mark and its Preview
role, since the row's target was rounded to six decimals and the mark compared the two within
1e-9 (item 8); a target over `ExactTargetHi` (0.60) extended the ladder, so the exact targets
were interpolated across the gap (0.615 printed where the 0.01 ladder crosses at 0.638) where
R-043 wants `> 0.60` (item 7). The console checked `0 < v < 1` on the parsed number, the window
`(0, 1]` on its own, and the core nothing.

The owner answered on 2026-10-23: "astea vrem sa le fixam pe toate, conform recomandarilor
tale": the target in one place both applications use, rounded to 3 decimals (the three the
report prints, `Fmt.Level`), refusing NaN, infinity and anything not strictly inside (0, 1)
after rounding, with one Romanian message; the exact targets read on the regular ladder only, a
given target outside it getting its table row but feeding no series.

## Decision

1. `Parameters.TargetBackground` holds the given value rounded to three decimals;
   `Parameters.TargetBackgroundError(double)` returns the one message for a value that is NaN,
   infinite or not strictly inside (0, 1) once rounded, and `Parameters.Validate()` applies it,
   so the console (`--target-bg`, prefixed with the flag's name), the window (the field) and a
   `Parameters` built in code refuse the same values with the same words before any reading
   (R-023, R-025). The simulation compares its row's target with the rounded given one, so every
   given target that passes keeps its `(dat)` mark and the Preview role.
2. The series the exact targets are read on take only the steps within `[ExactTargetLo,
   ExactTargetHi]`: the regular steps, the grid rows and a given target inside it. A given target
   outside it is lifted for its table row alone, and a crossing the ladder does not reach prints
   `> 0.60` (R-043).

## Alternatives rejected

- **Six decimals, as the ladder's steps.** The report prints three; a target the user cannot
  read back from the report serves nothing, and 1e-9 comparisons on six decimals are what lost
  the mark.
- **Accept 1 in the window and 0 in the console as "no target".** Both mean a gamma the
  simulation cannot compute (`RasterOps.GammaFor`); an empty field already means no target.
- **Let a given target extend the ladder.** It changes the exact targets of the dust and of
  every cloud by the one step the user typed, and R-043's `> 0.60` becomes a number nobody
  else would get.

## Consequences

- `Golden/help.txt` and the README name the interval and the rounding; R-023 says both.
- Guards: `R023_TheTargetBackgroundIsRoundedToThreeDecimalsAndRefusedOutsideTheOpenInterval`
  (the core, the console and the window), `R043_AGivenTargetOutsideTheLadderGetsItsRowButDoesNotFeedTheExactTargets`.
