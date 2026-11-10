# 0045. V-007 leaves the validation: its session has darks of its own

Date: 2026-11-10
Status: accepted 2026-11-10

## Context

V-007 recorded the Veil session of 2026-10-06 as a session with lights and no darks, run
against the 15.8 °C library (R-054): the library's darks as the run's own, the lights at 0.43
of them, cosmetic correction only. On 2026-11-08 the owner copied the 7 °C darks into that
session's `darks\` folder, to calibrate it. Since then the same command reads the lights
against the session's own darks (0.94, index 32.6, 758 pixels) and the library as a partner,
so V-007 fails on its first figure, with no change in the code: found on 2026-11-10 by the
validation run of the series-end task (`Passed: 11, Failed: 1` of twelve).

## Decision

V-007 is removed from `docs/validation.json` and from the table of `docs/validation.md`; its
figures stay in the History there. V-012 keeps them in the validation: the same lights against
the same library, asked darks first with `--lights`, whose figures were V-007's to the digit.
The owner said so in chat on 2026-11-10.

## Alternatives rejected

- **Re-record V-007 on what the session gives now** (its own darks, the library as a partner).
  A different case under an old number; the session's lights against its darks are not a
  figure anyone asked to pin, and the darks are a copy of a library.
- **Point V-007 at a copy of the session without its darks.** Eighty-seven raws kept twice to
  repeat what V-012 already measures.

## Consequences

Eleven cases are left. No real session without darks is validated any more: R-054's path is
held by its synthetic tests (`lights-only.txt` among the goldens) and, for the figures, by
V-012 through `--lights`. R-054 and R-055 name the change in their examples.
