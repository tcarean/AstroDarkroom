# 0087. The flat-area figure is called an average, not what a flat area "usually" is

Date: 2026-11-08
Status: accepted 2026-11-08

## Context

The width of the flat areas on the sky taken to 8 bits (`QuantizationResult.PlateauPx`, ADR
0060) is the mean of the clean tiles' plateaus: their sum over their count. Since the plain-words
passes of the recommendations (ADR 0073) and of the Export tab and Appendix C (ADR 0074) the
user texts called it what a flat area "usually" is ("de obicei", "latimea obisnuita"): in the Format recommendation's `Because`, in
Appendix C's line and in the Export row's description. The owner asked that every user text
where "usual" or "typical" stands for "on average" say "average" ("medie"), since anyone
understands an average. A search of the window, the report and `--help` in both languages found
that sense in those three places only; "usual" stays where it means something else (ordinary
room light, the viewing distance of 1.5 x the diagonal, the perceptual intent, the dpi labs ask
for, the built-in target backgrounds, the print shop giving the profile).

## Decision

The figure is named as an average in both languages: "a flat area is on average 2.2 px" /
"o zona plata are in medie 2.2 px", "flat areas on the sky taken to 8 bits: on average 2.2 px" /
"... in medie 2.2 px", "the average width of a flat area" / "latimea medie a unei zone plate".
The Export description also says why the average cannot decide: after a strong denoise the many
ordinary areas pull the average down and hide the few fully flat ones. "Average" ("medie")
is a plain word in the sense of ADR 0071, as the rounding and JPEG lines already use it ("in
medie", "err medie"); "median" stays banned.

## Alternatives rejected

- **Keep "usually".** It reads as the most common width, which a mean is not: a few wide areas
  among many narrow ones raise the mean above what most areas measure.
- **Replace "usual" everywhere.** In the other places it is no average of anything measured, and
  "average" would state a computation the program does not make.

## Consequences

- The eight lines of the goldens `dark-sky` and `dark-sky-wcs` that carry the figure, in both
  languages, changed by that word alone; R-091's example and
  `R091_TheBandingLineSaysWhereThePlateauWasMeasured` say "on average" / "in medie".
- No figure moves.
