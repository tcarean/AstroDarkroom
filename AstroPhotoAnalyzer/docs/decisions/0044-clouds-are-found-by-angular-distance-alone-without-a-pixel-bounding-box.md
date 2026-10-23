# 0044. Clouds are found by angular distance alone, without a pixel bounding box

Date: 2026-10-23
Status: accepted 2026-10-23

## Context

`DustAnalyzer.FindClouds` tested a cloud's patches only inside a box of the cloud's radius at the
central plate scale around its projected centre. Far from the tangent point the TAN projection
stretches a disc past that box, so a cloud could lose its outer patches or be skipped although
its disc held patch centres: the code review of area 2 (2026-10-23) measured, over 312
pointings of a frame like the plate-solved FITS, 48 clouds skipped, 9 of them with a row (item
10); at the FITS's own pointing no row appeared or disappeared (R-044, V-004). The owner
answered on 2026-10-23: drop the pixel bounding box and test every patch centre of every cloud
by angular distance, a cheap angular pre-filter on the frame's centre allowed; report the time;
V-004 should not move.

## Decision

A cloud is tested against every patch centre by the dot product of unit vectors against the
cosine of its radius, after one angular pre-filter: a cloud whose centre lies farther from the
frame's centre than the frame's angular radius plus the cloud's radius has no patch in its disc
and is skipped. The box is gone; `CentreX`, `CentreY` and `RadiusPx` still come from the
projection, for the map's circles.

## Alternatives rejected

- **A box widened by a factor.** Any factor is a guess that the next pointing exceeds; the
  angular test is the definition itself.
- **No pre-filter.** 1787 clouds x 21 840 patches is about 39 million dot products, tens of
  milliseconds; the pre-filter keeps most clouds out of that loop for nothing lost.

## Consequences

- Measured on the plate-solved FITS on 2026-10-23 (`fixprobe clouds`): `DustAnalyzer.Build` with
  the WCS 156 ms before and 174 ms after (8 and 10 ms without a WCS; the search itself is the
  brute force's 49 to 66 ms, within the run's noise), the same 397 clouds, 114 rows and 5110
  patches both ways at this pointing. The time is in the task's plan too
  (`docs/plans/2026-10-23-fix-the-defects-the-analyzer-review-found.md`, "Record").
- Guards: `R044_ACloudFarFromTheTangentPointKeepsEveryPatchWhoseCentreLiesInItsDisc`; V-004's
  cloud figures.
