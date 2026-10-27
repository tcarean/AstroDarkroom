# 0015. Two tile criteria on purpose: wide for the noise, strict for the plateau

Date: 2026-10-02
Status: accepted 2026-10-02; the plateau's sampling and statistic superseded by 0060

## Context

Retroactive: recorded on 2026-10-02 from `docs/implementation-plan.md` ("Notes from phase 2",
"Two different tile criteria, on purpose") and `docs/specification.md` §5 (B3, C1); the decision
dates from phases 2 and 3, 2026-09-20. The noise sigma and the 8-bit plateau were measured in the
originating conversation by two scripts, each with its own way of choosing "flat sky" tiles, and
the acceptance numbers came from those two scripts.

## Decision

The noise uses 400 random 64 x 64 candidates with seed 0, smooth when their range is at most
6000/65535 (23.3 levels). The plateau uses up to 120 tiles with seed 1, drawn within 5000
attempts, accepted when the range is under 6 levels and the mean under 70. `Thresholds` keeps the
two sets of constants apart (`NoiseTile*`, `PlateauTile*`).

## Alternatives rejected

- **One criterion for both.** One of the two acceptance figures would no longer be reproduced,
  and a difference from the conversation would then mean either a change of criterion or a change
  of measurement, with no way to tell which.

## Consequences

- On a frame full of structure the strict criterion finds few tiles: 20 in 5000 attempts on the
  reference image, which still gives the conversation's plateau (2.7 px against 2.6), and
  possibly none on another image (`known-limitations.md`).
- A change to either criterion moves a validation figure and is re-measured (`/validate`).
- Guards: `NoiseAnalyzerTests`, `QuantizationAnalyzerTests`, validation case V-001.

Source: docs/implementation-plan.md; docs/specification.md §5.
