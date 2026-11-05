# 0042. The gamut figure takes the exact round trip near the threshold, once per 8-bit colour, in parallel

Date: 2026-10-23
Status: accepted 2026-10-23; the chroma threshold `CurveGamutDeltaAb` (3) of the decision superseded by 0081 (the hybrid stays)
Supersedes: 0032 in part (the consequence that the gamut figure is built on a round-trip grid alone)

## Context

The gamut figure of the Curbe tab (R-144) counted a pixel as outside the paper's gamut when the
displacement of its a\*, b\* after the round trip through the profile, interpolated on a grid of
33 nodes per channel, passed `CurveGamutDeltaAb` (3). The code review of area 2 (2026-10-23)
found the interpolation rounds the gamut's edge inward and undercounts, by up to a few points
per class. The owner's rule: measure the exact per-pixel round trip against a finer grid and a
hybrid, implement the cheapest option within about 0.2 points per class if it adds no more than
about 1 s per "Calculeaza", else present. Measured on 2026-10-23 on the plate-solved FITS with
both lab profiles and on `de_printat.png`, as is and after `Puternica`
(`docs/plans/2026-10-23-fix-the-defects-the-analyzer-review-found.md`, "Record", item 9):

- the exact round trip costs 24.7 microseconds per colour single-threaded, 35 s per curve on
  the 1.4 MP subsample;
- the grid of 33 reads the FITS on baryta at 1.02 % against 1.35 % exact (the dust class 2.7
  against 4.2), its canvas at 35.1 % against 39.7 %, the PNG's dust class at 0.0 % against
  2.4 %; grids of 49 and 65 are still 0.45 and 2.2 points low on the two dust classes, at 1 and
  2.2 s to build;
- the hybrid, the exact trip for the pixels whose interpolated displacement lies within a margin
  of the threshold: at a margin of 0.5 it is 0.5 points low on the canvas sky and 1.0 on the
  PNG's dust; at 1.0 it equals the exact figure to three decimals on every class, but sends 4 %
  of the FITS's pixels on baryta, 32 to 35 % on canvas and 1 % of the PNG's to the trip: 1.3 s,
  11 s and 0.4 s per curve single-threaded;
- the same hybrid with the trip taken once per distinct 8-bit colour of those pixels: within
  0.17 points of exact on every class (the canvas sky 38.24 against 38.08; 0.10 % of the pixels
  decided otherwise on baryta, 1.6 % on canvas, 0.02 % on the PNG), 9 100 trips per curve on the
  FITS on baryta, 29 000 to 40 500 on canvas, 1 700 on the PNG: 0.13 to 0.23 s, 0.40 s and
  0.21 s per curve single-threaded, that is 0.7, 2.0 and 1.0 s per "Calculeaza" of five;
- the package's configuration served eight threads in the probe with the single-threaded
  results, to the pixel: the full exact pass took 3.8 to 4.8 s in parallel where it takes 35 s
  on one thread.

No option met both limits on canvas single-threaded; the owner, told so, chose the recommended
one on 2026-10-23: the cached hybrid with its round trips run in parallel over the rows.

## Decision

A pixel whose interpolated displacement lies within `CurveGamutExactMargin` (1.0) of
`CurveGamutDeltaAb` takes the exact round trip of its 8-bit colour, made once per colour and
kept in the table's cache for the run; every other pixel keeps the grid's answer; `Measure`
runs the rows of the subsample in parallel. The grid stays at 33 nodes. A table without a
profile behind it (the hand-built tables of the tests) is judged by the interpolation alone.

## Alternatives rejected

- **A finer grid.** 65 nodes cost 2.2 s to build and still miss the dust classes by 0.45 and
  2.2 points: the corner-rounding shrinks slowly with the node spacing.
- **The exact trip for every near pixel, uncached.** The exact figure to three decimals, at
  11 s per curve on canvas: a third of its pixels sit near the threshold.
- **The cached hybrid on one thread.** Within the accuracy everywhere, 2 s per "Calculeaza" on
  canvas, over the owner's budget there.
- **Leave the grid and call the figure a lower bound.** The figure then stays silent about the
  dust, the class the tab is for.

## Consequences

- The figure is defined, for the pixels near the threshold, on their 8-bit colour; the saved
  FITS and every curve are untouched. R-144 says so; the per-class example of V-008 is
  re-measured.
- `Thresholds.CurveGamutExactMargin` with its measurements; `GamutTable.Exact` and `Cache`;
  `measurements.md` F2 and `known-limitations.md` no longer call the figure an estimate to
  tenths of a percent.
- Guards: `R144_NearTheThresholdTheExactRoundTripDecidesOncePerEightBitColour`, the existing
  R-144 tests, V-008's gamut figures.
