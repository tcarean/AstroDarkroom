# 0087. A figure that is an average is called an average: the flat areas' width and the level of the JPEG's changed values

Date: 2026-11-08
Status: accepted 2026-11-08

## Context

Two figures the user reads are means. The width of the flat areas on the sky taken to 8 bits
(`QuantizationResult.PlateauPx`, ADR 0060) is the mean of the clean tiles' plateaus: their sum
over their count. The level of the values that JPEG q100 changes by more than 2 levels
(`JpegRow.MeanLevelOverThreshold`) is the mean of those values' original levels. Since the
plain-words passes of the recommendations (ADR 0073) and of the Export tab and Appendix C (ADR
0074), the user texts called the first what a flat area "usually" is ("de obicei", "latimea
obisnuita"), in the Format recommendation's `Because`, in Appendix C's line and in the Export
row's description, and said of the second that the changed values lie "mostly around" it ("mai
ales in jurul nivelului"), in the Format recommendation's `Because` and the Export tab's guide.

The owner asked that every user text where "usual" or "typical" stands for "on average" say
"average" ("medie"), since anyone understands an average. A search of the window, the report
and `--help` in both languages found that sense in the flat areas' three places; "usual" stays
where it means something else (ordinary room light, the viewing distance of 1.5 x the diagonal,
the perceptual intent, the dpi labs ask for, the built-in target backgrounds, the print shop
giving the profile). A review of the branch found the JPEG level, described as where most
values lie, and the owner asked that it be named an average too.

## Decision

Both figures are named as averages, in both languages:

- the flat areas: "a flat area is on average 2.2 px" / "o zona plata are in medie 2.2 px",
  "flat areas on the sky taken to 8 bits: on average 2.2 px" / "... in medie 2.2 px", "the
  average width of a flat area" / "latimea medie a unei zone plate"; the Export description also
  says why the average cannot decide: after a strong denoise the many ordinary areas pull the
  average down and hide the few fully flat ones;
- the JPEG level: "at an average level of 28" / "la un nivel mediu de 28", the name the JPEG
  table's column already has ("mean level" / "nivel mediu"), and in the guide "at what level
  (0..255) they lie on average" / "la ce nivel (0..255) stau in medie".

"Average" ("medie") is a plain word in the sense of ADR 0071, as the rounding and JPEG error
lines already use it ("in medie", "err medie"); "median" stays banned.

## Alternatives rejected

- **Keep "usually" and "mostly around".** Both read as where most values are, which a mean is
  not: a few wide areas among many narrow ones raise the mean above what most areas measure,
  and changed values split between the dark sky and the stars average to a level where few of
  them lie.
- **Replace "usual" everywhere.** In the other places it is no average of anything measured, and
  "average" would state a computation the program does not make.

## Consequences

- In the goldens, the two flat-area lines of `dark-sky` and `dark-sky-wcs` and the JPEG line of
  every golden that has a JPEG table (`clean-sky-canvas`, `dark-sky`, `dark-sky-wcs`,
  `mono-png8`), in both languages, changed by those words alone; R-091 states both wordings,
  `R091_TheBandingLineSaysWhereThePlateauWasMeasured` and
  `R091_TheJpegBecauseNamesTheAverageLevelOfTheChangedValues` check them.
- No figure moves.
- When no value changes by more than 2 levels the level is 0 and the line still states it ("0.0
  % of the values ..., at an average level of 0", `mono-png8`), as it stated "mostly around
  level 0" before; this decision leaves that as it was.
