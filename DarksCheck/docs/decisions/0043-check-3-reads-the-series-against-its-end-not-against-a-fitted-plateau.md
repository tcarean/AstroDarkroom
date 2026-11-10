# 0043. Check 3 reads the series against its end, not against a fitted plateau

Date: 2026-11-10
Status: accepted 2026-11-10
Supersedes: 0017 in part (the fitted plateau as the reference of check 3 and the settle test); 0038 in part (the plateau as the reference of the REFA rule and of the rows)

## Context

Check 3 fitted every frame's hot-pixel ratio as an approach to a plateau and judged the series
against that plateau (ADR 0017): settled when the fitted curve was within 1 °C of it at the last
frame, the frames more than 2 °C from it to discard, `REFA` when more than half were (ADR 0038).
On 2026-11-10 the owner's darks taken right after the Veil lights of 2026-11-08 (35 x 120 s, the
folder says -2 °C) read `ATENTIE: the camera was still cooling at the last frame, 1.4 degrees
above the plateau`. Measured the same day: the series moved 0.5 °C in all (1.00 of the master
over its first twelve frames, 0.95 over its last twelve), the plateau at 0.82 was extrapolated
from that slope with the time constant at the top of the grid, and the camera never gets there:
the air warmed while the sensor cooled. On the 2.7 .. 1.7 °C library the same extrapolation
named all 40 frames as off a plateau at 0.58. The owner's reading: what counts is where the
series ended, not a plateau it never reaches. Measured on the eleven real series, the end as the
median ratio of the last five frames against the fitted curve's last value: within 0.01 (0.1 °C)
on ten, and 0.05 under it on the 4.4 .. 2.7 °C series, which the exponential does not fit.

## Decision

Check 3 reads the series against its end: the median ratio of the last `Th.SeriesEndFrames` = 5
frames that have one, measured, not fitted. The frames more than `Th.OffEndCelsius` = 2 °C from
the end are the ones to discard; `REFA` when they are more than `Th.RedoOffEndFraction` = 0.5 of
the frames with a ratio (the owner's limit of 2026-11-07, kept); `ATENTIE` when the master is
more than `Th.MasterNearEndCelsius` = 1 °C from the end, or a frame leaves the `-opt` band of the
master; `OK` otherwise. The plateau leaves every row, column, chart line and verdict; the
exponential fit stays for one figure, the minutes of the `REFA` advice of a cooling series, and
only when its curve moves the way the series did and its time constant is not the top of the grid. The owner chose the
rule, the `REFA` limit against the end and the fit's one remaining use in chat on 2026-11-10,
from this table (the tool's own per-frame ratios, measured 2026-11-10):

| Series | Plateau rule | Master against the end | Off the end | End rule |
|---|---|---|---|---|
| -2 °C (idx13) | ATENTIE, 1 off | 0.4 °C above | 0 of 35 | OK |
| 2.7 .. 1.7 °C (idx18) | ATENTIE, 40 off | 0.6 °C above | 0 of 40 | OK |
| 4.4 .. 2.7 °C (idx20) | OK, 0 off | 0.4 °C above | 0 of 30 | OK |
| 7 °C (idx33) | OK, 1 off | 0.4 °C below | 1 of 30 | OK |
| 9.5 °C (idx33) | OK, 3 off | 0.7 °C below | 3 of 30 | OK |
| 9.2 °C (idx34) | ATENTIE, 12 off | 1.2 °C below | 8 of 30 | ATENTIE |
| 13.3 °C (idx47) | OK, 4 off | 0.7 °C below | 3 of 30 | OK |
| 15.8 °C (idx67) | OK, 5 off | 0.7 °C below | 4 of 30 | OK |
| Smida | OK, 2 off | 0.6 °C below | 2 of 20 | OK |
| the leak set | OK, 8 off | 0.99 °C below | 6 of 30 | OK, at the limit |
| 2 °C of 2026-11-07 | REFA, 21 off | 1.4 °C above | 10 of 30 | ATENTIE |

## Alternatives rejected

- **Keep the plateau and refuse an extrapolated one.** The -2 °C set's `ATENTIE` would go, but a
  fitted plateau a degree and a half from the last frame is still a place the series was not at;
  the owner's question is about the frames he has.
- **The fitted curve's value at the last frame as the end.** Within 0.01 of the median of the
  last five on ten series, but on the 4.4 .. 2.7 °C series the curve sits 0.05 above the frames
  (0.4 °C); a figure read from the frames needs no model to be explained.
- **Flag a series still moving at its end.** The slope over the last twenty minutes is 0.1 ..
  0.5 °C on every real series but the 2 °C set (0.8); a steady climb whose master is within a
  degree of its end has a usable master at its own index, and the owner cannot stop the air from
  moving. A series that moved far has its frames in `de aruncat` and its master away from the end.
- **`REFA` from a third of the frames.** It would keep the 2 °C set (10 of 30) on the limit
  without passing it; the owner kept half: by the end, that set's last twenty frames are within
  two degrees of where it ended.

## Consequences

R-023 rewritten; R-025, R-071 and R-083 name the `sfarsit` column and its words; `Warming`
carries `EndRatio`, `FirstCelsius`, `MasterCelsius` and the `OffEnd` figures, `DriftVerdict`
`MasterNearEnd`. A series still moving at its end is no longer flagged for that alone. The direction the rows
name is the end against the first frame; a series that leaves and comes back (warm in the
middle only) reads as one that stayed put, with its frames in `de aruncat`. The
owner's darks of 2026-11-07 read `ATENTIE` with frames 1 .. 10 to discard, no longer `REFA`
(the master 0.7 °C above the end without them, 22 % more noise); no real series is `REFA` now.
The synthetic `REFA` series of the tests and of the `redo` goldens is steeper (`0.35 + 1.45 x
exp(-i / 6)`), since the old one has 4 of 12 frames off its end. `docs/validation.json` loses
`plateau`, `settleCelsius` and `offPlateauFrames` and gains `endRatio`, `masterCelsius` and
`offEndFrames` on the darks cases. The owner's practice: the frames to discard are still read off
the `de aruncat` row; a master more than a degree from the end of its series is the sign to drop
them or take the series longer.
