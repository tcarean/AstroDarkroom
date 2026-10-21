# 0017. Check 3 judges the plateau of the series, not the first frame against the last

Date: 2026-10-21
Status: accepted 2026-10-21
Supersedes: 0007 in part (the drift verdict at 1 °C first to last)

## Context

Check 3 read the warming of a dark series as the hot-pixel amplitude ratio of the last frame
over the first and flagged `ATENTIE` over 1 °C (ADR 0007). Measured on 2026-10-21 on every
real series, with the ten discarded frames of two libraries put back: the ratio follows an
exponential approach to a plateau, `plateau - drop x exp(-t / tau)`, with a time constant of
14.5 min outdoors (Smida) and 18 .. 28.5 min indoors, the sensor starting 10 °C under its
running plateau when cold; the last third of every series moved 0.15 .. 0.47 °C and every
series moved 2.2 .. 3.6 °C first to last. No series of this camera could pass the 1 °C test,
so the verdict said nothing. The lights of the Smida night themselves spanned 3.4 °C
(0.72 → 1.07 → 0.94 of the dark master, the drop in the last twenty minutes from pauses of up
to four minutes), and check 9 gave them `OK` at a ratio of 1.00; the `-opt` margin absorbs
±5 °C. What the master needs is a series that has settled, so that its median belongs to a
temperature, and a list of the frames still in the warm-up, for the owner to move to
`aruncate` by hand (ADR 0002).

## Decision

Check 3 fits every frame's ratio on the minutes from the first frame as an approach to a
plateau (least squares of the two linear coefficients for each time constant on a grid from a
fiftieth of the span to three times it; at least `Th.WarmingFitMinFrames` = 6 frames) and
judges three things: the series has settled when the fitted curve is within
`Th.SettledCelsius` = 1 °C of the plateau at the last frame; every frame's ratio stays in the
`-opt` band of the master, `Th.MatchOptLow` .. `Th.MatchOptHigh` (0.7 .. 1.4); and the frames
more than `Th.OffPlateauCelsius` = 1 °C from the plateau, measured against the plateau and not
against the master (the owner's choice of 2026-10-21: 10 .. 18 of 30 frames on the present
libraries, 6 of 20 at Smida, against 2 .. 6 the master would have named), are listed as the
ones to discard, in the `de aruncat` row, in the table's `platou` column with a note on the
row, and in the window's Cadre tab in the attention colour. `ATENTIE` only when the series has
not settled or a frame leaves the `-opt` band; the warm-up at the start is information, not a
fault. `Th.DriftCelsius` is gone.

## Alternatives rejected

- **Keep first-to-last with a wider threshold (3 or 4 °C).** It would still flag a cold-started
  series and pass a series cut mid-warm-up, the one case where the master's temperature is
  undefined; the shape of the curve, not its span, is what tells the two apart.
- **Discard against the master, not the plateau.** Fewer frames named (2 .. 6), but the master
  stays 0.8 .. 1.5 °C under the plateau, where the lights of the same night run; discarding
  against the plateau brings it within about half a degree (`fara ele masterul ar fi ...`).
- **The last third's rise as the settle test.** On seven frames the last third is one step and
  understates a slow climb; the degrees left between the fitted last frame and the plateau
  read the same thing from the whole curve.
- **Theil-Sen or a robust fit.** The ratio is already a median over hundreds of hot pixels per
  frame; the rms residuals of the seven real series are 0.005 .. 0.011 and a lit frame does
  not move the ratio (ADR 0012).

## Consequences

R-023 rewritten, R-025 and R-071 amended; the design note `docs/design/dark-analysis.md`
(the fit) and `docs/design/window.md` (the Cadre tab). The drift verdict of V-003, V-004,
V-008, V-009 and V-010 changes from `Warning` to `Ok`, and `docs/validation.json` gains the
plateau, the time constant, the degrees left at the end and the count of frames off the
plateau per darks case. The owner's warm-up changes with it: no resting the camera at ambient
before a library (it starts 10 °C under the plateau and climbs for an hour), the discarded
frames read off the `de aruncat` row rather than fixed at twenty minutes. A series of fewer
than six frames has no fit and is judged on the `-opt` band alone.
