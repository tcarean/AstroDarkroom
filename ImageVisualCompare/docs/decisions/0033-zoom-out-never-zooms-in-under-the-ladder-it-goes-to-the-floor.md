# 0033. "−" never zooms in: when no stop of the ladder is below the scale, it goes to the floor

Date: 2026-10-22
Status: accepted 2026-10-22
Supersedes: 0020 in part (− staying at 0.02 at the low end of the ladder)

## Context

ADR 0020 has `+` and `−` step through `ZoomLadder.Steps`, 0.02 to 16, and stay at 16 or 0.02
at the ends, and puts the zoom floor at the fit scale (`Viewport.MinScale`). The floor has no
bound at 0.02: a window small enough makes the fit smaller than the lowest stop. The code review
of the documentation backfill (2026-10-21) measured it on the 5938 × 3888 pair in a comparison
area about 40 physical pixels tall: the fit is 1.54 %, and `−` called
`ZoomTo(ZoomLadder.Previous(0.0154))`, that is 0.02, so the image grew to 2 %; a second `−` did
nothing. R-024's "past either end the step stays at the end" describes the code, and the code
zooms in on the key that means out. On 2026-10-22 the owner chose, of two answers, the floor.

## Decision

- When no stop of the ladder is below the current scale, `Viewport.ZoomOut` zooms to
  `MinScale`, the floor, instead of the ladder's lowest stop; at the floor it changes nothing.
  `−` never zooms in. Everywhere else it steps as before.
- `ZoomLadder` keeps its contract: `Previous` still answers 0.02 for a scale under the ladder,
  and the viewport does not take that step.

## Alternatives rejected

- **`−` does nothing under the lowest stop.** A scale between the fit and 0.02, which the wheel
  can leave, would then have no step out at all, though the fit is still below it.

## Consequences

- The change shows only in a window so small that the image fits under 2 %; R-024 gives the
  example of 91 × 60 physical pixels, where `−` from 1.8 % goes to the fit, 1.53 %.
- `+` is unchanged: the ceiling, 16, is the top stop, so no scale is ever above the ladder.
- Guards: `ViewportTests.R024_ZoomOut_UnderTheLowestStop_GoesToTheFloor_NeverIn` and
  `R024_ZoomOut_StepsDownTheLadder`; `ZoomLadderTests` unchanged.
