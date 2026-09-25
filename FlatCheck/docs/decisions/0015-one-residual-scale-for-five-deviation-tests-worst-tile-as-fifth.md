# 0015. One residual scale for five deviation tests, with the worst tile as the fifth

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/thresholds.md ("The residual scale") and
docs/design/modes-structure.md ("Why a fifth test"); the decision itself dates from the
residual-scale rework (1.2.0) and the later addition of the worst tile. The structure tests -
corner spread, azimuthal asymmetry, residual after the radial model, tilt - all measure the same
thing from different angles: how much of the frame a purely radial function fails to explain.
All are fractional amplitudes in percent. `LargeScalePtp` trims 5 % of the tiles at each end so
it measures structure rather than noise, and that trim drops any defect covering under 5 % of
the frame, however deep: on a 24 MP synthetic a +10 % blotch the size of a fingertip in a corner
read 0.04 % residual and PERFECT, with the blotch plainly visible in the map.

## Decision

The five tests share one four-band scale instead of pass/fail thresholds: < 2 % PERFECT,
2-10 % BUN, 10-20 % ACCEPTABIL, > 20 % REFA (`Th.ResidPerfect/Usable/Check`), boundaries strict
`<` so each falls into the worse band, NaN degrading to REFA. The verdict is the worst of the
five. `MaxTileDeviation` - `max |tile - 1|` on the same 40-tile grid, whole frame - is the fifth
test, reading 9.3 % on the blotch above. `reziduu dupa model rad.` is marked as the one that
matters most, and the report states that the scale is an upper bound on the flat error, not the
error.

## Alternatives rejected

- **A pass/fail threshold per test.** They measure one quantity; one scale keeps the verdicts
  comparable and lets the console print the scale itself under
  `SCARA celor cinci teste de abatere de mai sus`.
- **Four tests without the worst tile.** The 0.04 % / PERFECT reading on a visible +10 % blotch.
- **Calling the scale "exact eroarea de flat".** The report said so once; what it measures is
  the sum of a camera-fixed part (harmless) and a world-fixed part (harmful), inseparable on one
  master - a Nikkor 35 DX master carries 4.00 % corner spread of which only ~1.3 % was the sky.
  `WhyResidualMatters` now says so and points at `--ratio`.
- **Verdict thresholds of `--ratio`'s own.** Dropped at the rewrite: its verdict is the
  structure verdict of `(A+B)/2` on this scale; the 1 % / 3 % pair survives only as an
  informative label on the world-fixed component.

## Consequences

- The five do not read the same defect with the same number, by shape: on a 3:2 frame with a
  12 % gradient, tilt 12.0, corners 11.5, residual 10.4, azimuthal 7.3 (its rings stop at the
  inscribed circle), worst tile 6.0 (a per-pixel deviation, half the peak-to-peak). A gradient
  is carried by tilt and corners, a compact blotch by the worst tile, a broad blotch inside the
  circle by the residual and the azimuthal test. The worst tile never carries a gradient.
- `MaxTileDeviation`'s floor is 0.13-0.37 % on clean fields, 0.4 % at 10 % pixel noise.
- `Stats.Severity` spans both ladders (0-3) and the exit code follows it in every mode; the
  `--ratio` rewrite moved the ISO800 library pair from OK/0 to ACCEPTABIL/2.
- Headings that count the tests are literals: the Structure tab said "SCARA CELOR PATRU TESTE"
  for a whole version after the fifth test; `TheScaleHasFourBandsAndMarksTheCurrentOne` now
  asserts the test count.
- Pinned by `ACompactBlotchInACorner_IsNoLongerReportedAsPerfect` and the golden files, which
  carry the printed scale.

Source notes: docs/design/thresholds.md ("The residual scale"), docs/design/modes-structure.md
("Why a fifth test", the upper-bound paragraph), docs/design/pipeline.md (`MaxTileDeviation`).
