# 0038. A series mostly off the plateau is REFA, and check 3 says which way the camera moved

Date: 2026-11-07
Status: accepted 2026-11-07
Supersedes: 0017 in part (the verdict levels of check 3 and its rows)

## Context

The owner's darks of 2026-11-07 (30 x 120 s at ISO 1600, the folder labelled 2 °C) were shot
right after the camera came out of the house. The hot pixels fall from 1.34 of the master in
the first frame to 0.85 in the last, about 4 degrees in the hour; the fit puts the plateau at
0.72 of the master and names 21 of 30 frames as more than 2 degrees from it, the master 83 %
noisier without them; the master is only 1.5 degrees colder than the 7 °C library of the day
before. Check 3 (ADR 0017) said `ATENTIE` in yellow, and the owner could not tell from its rows
whether the camera warmed or cooled: the `platou` row printed the degrees still to go, signed
(`-1.3`), while the table's `platou` column printed the frame's degrees above the plateau
(`+1.4`); "cooling" appeared once, inside the verdict; the `pixeli fierbinti` row spoke only of
pixels that rise with the temperature; the chart's note explained a curve that climbs and
nothing about one that falls; and a yellow verdict that proposes discarding two thirds of the
frames does not say the series is compromised. Measured the same day on every real series, the
frames off the plateau over the frames and the noise the master would gain without them: the
15.8 °C library 5 / 30 and 10 %, 13.3 °C 4 / 30 and 7 %, 9.5 °C 3 / 30 and 5 %, 9.2 °C
12 / 30 and 29 %, 7 °C 1 / 30 and 2 %, the leak set 8 / 30 and 17 %, Smida 2 / 20 and 5 %, the
2 °C set 21 / 30 and 83 %.

## Decision

Check 3 gets a fourth verdict, `REFA` (`REDO` in English, `Verdict.Redo`, red in the window
beside `EROARE`), when more than `Th.RedoOffPlateauFraction` = 0.5 of the frames with a ratio
are off a fitted plateau (not one extrapolated from the top of the grid, which is a guess):
the median master is then made of frames that belong to no temperature. Its advice depends on
the direction: a cooling series is taken again after the camera has sat where it shoots for
three times the series' time constant (rounded up to five minutes; within 5 % of the plateau);
a warming one is taken longer, since every series warms itself (ADR 0017). The `pixeli
fierbinti` row goes, and the `platou` row opens with the direction and its figures (`aparatul
s-a racit pe toata seria: pixelii fierbinti scad de la 1.34 din master in primul cadru la 0.85
in ultimul`), says where the camera would settle against the master, and puts the last frame
above or below the plateau; the verdict rows do the same, with no signed degree anywhere, and
a cooling series names its cause (`a pornit mai cald decat locul in care a stat`); the chart's
note says a falling curve is the camera cooling. The owner chose the rule and the row's fate
in chat on 2026-11-07 (the kit's rule "Decisions").

## Alternatives rejected

- **Fewer than `Th.MinDarks` (10) frames left after the discard.** Names the same one series
  today, but a short library of 12 frames with the usual 3 of warm-up would read `REFA` for a
  normal start.
- **The master's noise would grow by more than 50 % without the frames.** The same threshold
  as a fraction (fewer than 44 % left), explained through a figure the owner has to work back
  from; the fraction of frames is what the `de aruncat` row already shows.
- **Keep `ATENTIE` and sharpen its words.** The owner's reading: a series whose two thirds are
  to be discarded is compromised, and yellow does not say so.
- **Keep the `pixeli fierbinti` row with new words.** The owner's reading: nothing in it could
  be followed; its two figures are the opening of the `platou` row now, where the direction
  gives them their meaning.

## Consequences

R-023 rewritten; R-025 and R-071 name `REFA` among the verdict words; `ReportRow` and
`VerdictPalette` read it; the three verdict words become four in the README and the design
notes. The goldens lose one line, the removed row. The owner's 2 °C set is validation case
V-011, the one real `REFA`; the other darks cases keep `Ok`. The owner's practice changes with
it: a camera brought out into cold air sits there for three time constants before the series
starts (about 105 min for the 35 min of the 2 °C set), and a library is labelled by its index,
not by the air's temperature, when the two disagree.
