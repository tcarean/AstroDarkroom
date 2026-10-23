# 0039. Suggested curves are judged on the unclamped spline, and Maxima's factor is capped under the overshoot

Date: 2026-10-23
Status: accepted 2026-10-23
Supersedes: 0032 in part (decision 4, Maxima's factor; the judgement of a curve on its clamped table)

## Context

ADR 0032 decided that `Maxima` "scales the slopes up until the peaks reach 0.92", and the code
judged a curve on its 65 536-entry table after clamping to 0..1. The code review of area 2
(2026-10-23) found that every Maxima seen had a natural spline that rose over 1 between the
peaks' point and (1, 1) and came back, was held at 1 by the clamp and kept: R-140 and R-142 say
"a spline that is not monotone is never suggested", and these were not monotone before the
clamp. Measured on 2026-10-23 with a probe on the four curve images (the anchors of V-008,
V-009, V-011, V-014; `docs/plans/2026-10-23-fix-the-defects-the-analyzer-review-found.md`,
"Record"):

| image | room / reach | uncapped Maxima: spline max, input mapped to white | capped factor, peaks |
| --- | --- | --- | --- |
| plate-solved FITS (V-008) | 1.0014 | 1.00000, 0 % (no Maxima: gain 0.001 under 0.02) | 1.0014, 0.920 |
| de_printat.png (V-009) | 1.2633 | 1.00998, 24.2 % | 1.2198, 0.892 |
| Rosetta (V-011) | 1.1904 | 1.00657, 21.2 % | 1.1605, 0.899 |
| Veil (V-014) | 1.5594 | 1.05064, 41.0 % | 1.3833, 0.828 |

On all four the full curve (Puternica, factor 1) peaks at exactly 1.00000, at the (1, 1) end:
Blanda, Medie and Puternica are unchanged by any check. The owner answered on 2026-10-23: "astea
vrem sa le fixam pe toate, conform recomandarilor tale": cap Maxima's slope factor so that the
spline never rises over 1 on (0, 1), the largest factor up to room / reach whose spline stays at
or under 1; Maxima still exists only when its peaks then gain more than `CurveMaximaMinGain`
over Puternica; the same no-overshoot check (the unclamped spline within 0..1 and monotone)
applies to every suggestion, so the code honours R-140 and R-142 as written.

## Decision

1. A curve is suggestible when its unclamped spline, at the steps of the table it is applied
   through, decreases nowhere and stays within 0..1 (`ToneCurve.IsMonotone`, `IsWithinRange`,
   `IsSuggestible`); a curve that is not is never suggested (R-140).
2. The slope factor of the full curve and of Maxima is capped at the largest value, up to the
   recipe's own (min(room / reach, 1) and room / reach), whose full curve is suggestible, found
   by bisection from the identity. Maxima exists only when its capped peaks gain more than
   `CurveMaximaMinGain` over Puternica's (R-142). The cap on the full curve's factor changes
   nothing on the four images and stays as the one rule for both.

## Alternatives rejected

- **Reject the overshoot and offer no Maxima.** The owner wants the strongest curve that stays
  honest; on the three images the capped Maxima still gains 0.11 to 0.20 over Puternica.
- **Reword R-140 and R-142 to accept the clamp.** A clamped spline maps a fifth to two fifths of
  the input range to white: not a contrast curve, and not what the table of points says.
- **Judge the clamped table, as before.** It hid exactly this; the spline is what Siril computes
  from the same points.

## Consequences

- The Maxima figures of V-009, V-011 and V-014 move (their points, levels, gains and clipped
  shares); V-008 keeps its three curves. `docs/validation.json` is updated after the owner's
  approval of the drift table.
- Guards: `R140_ACurveWhoseSplineLeavesTheRangeIsNotSuggestible`,
  `R142_MaximaIsCappedSoThatItsSplineStaysWithinTheRange`, the existing R-142 tests.
