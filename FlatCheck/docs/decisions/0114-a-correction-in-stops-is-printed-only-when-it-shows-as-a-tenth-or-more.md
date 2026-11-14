# 0114. A correction in stops is printed only when it shows as a tenth or more

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

`--expo` ends with a sentence that says how far to move the exposure: `log2(target / centre)`
stops, the target being 57 % for a panel flat (ADR 0022, R-043). For a flat that is too high
the sentence is "Lower the exposure by ~N stops", with N the negative of that figure.

A flat is too high in two ways: its centre is above 80 %, or more than 0.05 % of the centre
window is burnt, at any level ("clipping forces `PREA SUS` even inside the target band",
R-043). In the second way the centre can sit at the target or below it, and the figure then
has the wrong sign or says nothing. The review of 2026-11-13 (CR-020) read the lines; the
failing tests of 2026-11-14 showed the sentences, on a panel flat with five burnt pixels in
the centre window:

| Centre | The sentence printed |
|-------:|----------------------|
| 40 % | `Lower the exposure by ~-0.5 stops.` |
| 55 % | `Lower the exposure by ~-0.1 stops.` |
| 58 % | `Lower the exposure by ~0.0 stops.` |
| 62 % | `Lower the exposure by ~0.1 stops.` |

The review asked for no figure at or below the target. The third row is outside that: the
centre is above the target and the figure is right, but it reads as "by nothing".

## Decision

The owner's choice of 2026-11-14, between "no figure at or below the target" and "no figure
whenever it would show as less than a tenth":

1. For a panel flat that is too high, the figure is printed only when it is positive and
   shows as `0.1` or more at the one decimal the report prints. The test is on the figure as
   printed (`Numbers.RoundsToZero`), so the word and the number cannot disagree.
2. Otherwise the sentence is `Lower the exposure.` (`Scade expunerea.`), the one a sky flat
   that is too high already gets. The line "The centre is burnt: ..." follows as before.
3. The verdict does not change: a burnt centre is too high at any level.

## Alternatives rejected

- **No figure only at or below the target.** What the review asked, and it leaves `~0.0 stops`
  for a centre between 57 % and 59 %.
- **Aim the figure at something else when the centre is burnt** (the level at which the burnt
  pixels would fall under white). The tool does not know how bright the burnt spot was: a
  saturated pixel has lost that.
- **Print the absolute value.** "Lower by ~0.5 stops" for a centre at 40 % would send the flat
  to 28 %, under the limit where the master comes out noisy.

## Consequences

- R-043 holds the rule; `R043_TheCorrectionFigureIsPrintedOnlyWhenItShowsAsATenthOfAStopOrMore`
  in `ExposureReportTests.cs` reads the sentence at the four levels above and at 81 % without
  clipping, in both languages and in the console's block.
- No golden changes: the three goldens that print the sentence are too high by their level
  (0.5 and 0.6 stops).
- The other sentences with a figure cannot reach a small one: "raise" is printed below 50 %
  (0.2 stops or more), "you could lower it" above 65 % (0.2 or more).
- With a burnt centre below the target the advice is to lower an exposure that is not high by
  its level. That is the verdict's own position (R-043), not this decision's: the burnt
  pixels are what has to go, and the tool cannot say by how much.
