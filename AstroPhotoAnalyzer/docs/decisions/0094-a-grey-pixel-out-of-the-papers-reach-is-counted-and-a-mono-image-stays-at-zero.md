# 0094. A grey pixel out of the paper's reach is counted by the paper fit; a mono image stays at zero

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The paper fit copies a pixel whose chroma is under `GamutFitMinChroma` (0.05): it has no chroma
to reduce. The copy came before the judge was asked, so such a pixel was in none of "outside as
is", "left outside" and "tone the paper cannot make", whatever the judge says of its tone, while
the Soft proof tab paints it red and counts it (the code review of 2026-11-13, CR-002). R-175
says a pixel the chroma alone cannot bring in "stays as it is and is counted". Measured on
2026-11-14 on the tonal subsamples: `de_printat.png` through the canvas profile has 334 such
pixels of 1 443 420 (0.023 %), 350 on its 8-bit version; the other measured pairs have none.

The same review named the owner's part: a mono raster gives 0 in the three places that judge a
colour (`GamutAnalyzer.Measure`, `ProofRun.Reduce`, `GamutFit.Apply`), on purpose, "a mono image
has no chroma", a reason older than ADR 0081, whose rule counts the tone too. Measured the same
day: the grey copy of the plate-solved FITS, stored as RGB, has no pixel outside on the baryta
or on the canvas; the grey copy of `de_printat.png` has 431 of 1 443 420 (0.03 %) on the canvas,
all under L\* 0.13, and none on the baryta. The owner was asked on 2026-11-14 and chose to leave
the mono image at 0.

## Decision

A pixel of a colour image under `GamutFitMinChroma` is still copied, never moved; with the facts
asked for, the judge is asked about it as about every pixel (ADR 0093), and one it rejects is
counted outside before and after and as a tone out of reach. The recipe counts it among the
pixels a mask cannot bring in. A mono image stays at 0 in the three places.

## Alternatives rejected

- **Judge a mono image too, as R = G = B.** The same grey image would read the same as a mono
  file and as an RGB file, but it changes three places and their figures for at most 0.03 % of
  pixels that are next to black, which the Tonuri tab already reports as under paper black.
- **Lower `GamutFitMinChroma` to zero.** A grey pixel has no hue to reduce along; the search
  would divide by its chroma.

## Consequences

- On a colour image the fit's "outside as is" is the Soft proof tab's count on the same pixels,
  the grey ones included.
- A mono file and its RGB copy can differ by the dark greys the paper cannot reach: at most
  0.03 % on the measured images (`docs/design/known-limitations.md`).
- Guard: `R175_AGreyPixelWhoseToneIsOutOfReachIsCountedAndLeftAsItIs`.
