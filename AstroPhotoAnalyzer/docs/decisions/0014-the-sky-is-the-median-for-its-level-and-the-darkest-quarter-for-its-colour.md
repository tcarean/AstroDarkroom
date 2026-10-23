# 0014. The sky's level is the frame's median; its colour is the darkest quarter above the horizon band, per channel

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §5 (B1, E1, E2) and §14, and
`docs/implementation-plan.md` ("Notes from phase 2"); the decision dates from phase 2,
2026-09-20. Two measurements need "the sky": the level the print simulation lifts toward a
target, and the colour against which the cast and the edge regions are judged. A finished
astrophoto is mostly sky; the reference image has a Milky Way core across it and horizon glow in
one corner.
(Corrected 2026-10-23: "real data has no ties" in the Decision below is not so. On the 16-bit
reference image 68 pixels above the bottom band sit exactly at the cut, without moving a printed
figure; on its 8-bit version 32442 do, and `<=` gives a green cast of +2.0 levels where `<`
would give +1.0, either side of `CastWarnLevels`. Found at the code review of 2026-10-23.)

## Decision

- **The level** is the median of the reference plane at stride 4. It is right whenever the sky
  dominates the frame, which is every astrophoto, and it is what the conversation measured.
- **The colour** is taken from the pixels at or below the 25th percentile of luminance (the mean
  of the planes) on the stride-3 subsample, without the bottom 25 % of rows, where the horizon is;
  its levels are the per-plane medians. The test is `<=`, not `<`: on a flat synthetic sky every
  pixel sits exactly at the percentile and a strict test selects nothing; real data has no ties,
  so the acceptance numbers are unaffected.
- **The edge regions** are compared with that colour per channel, not by luminance.

## Alternatives rejected

- **Segment the sky.** More machinery for frames that the sky dominates anyway; the median is the
  conversation's number.
- **Compare the regions by luminance.** The reference image's horizon corner has the sky's
  luminance (R +9, B -10): a luminance test misses it.
- **A strict `<` on the percentile.** It selects nothing on the synthetic scenes the tests use.

## Consequences

- On a frame the sky does not dominate, the median is not the sky; `known-limitations.md` says so
  and `WhenTheSkyDoesNotDominateTheMedianIsNotTheSky` keeps it visible.
- R-040 and R-060 state the two measurements; ADR 0013 names them apart; ADR 0018 says what a
  flagged region means.
- Guards: `TonalAnalyzerTests`, `PrintStretchSimulatorTests`, `BackgroundAnalyzerTests`,
  validation case V-001.

Source: docs/specification.md §5, §14; docs/implementation-plan.md.
