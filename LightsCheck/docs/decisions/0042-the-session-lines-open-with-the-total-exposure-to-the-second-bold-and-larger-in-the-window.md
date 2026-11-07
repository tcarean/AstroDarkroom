# 0042. The session lines open with the total exposure to the second, bold and larger in the window

Date: 2026-11-07
Status: accepted 2026-11-07

## Context

On 2026-11-07 the owner asked for the total integration time to be the first value of the
Summary tab's SESSION card, in hours, minutes and seconds, in bold and a slightly larger font.
The figure existed: the interval line gives the session's integration (R-006) as `total exposure
2 h 54 min`, rounded to the minute, and SUMMARY's `leave out of stack` row the exposure of the
frames that stay. Measured that day with the console's `--inventory` on the three real sessions:
Veil 2026-10-06 10470.4 s (87 frames; the 65 that stay hold 7826.0 s), Veil 2026-11-06 9873.7 s
(82 frames, the three short ones taken out of `lights\` that day), Smida 2026-08-12 3813.7 s
(32 frames, all good). The owner's word, `integration`, is one R-078 keeps out of the session
lines since ADR 0033 named this figure `total exposure` (`expunere totala`). Put to the owner
with the alternatives below, they took the recommendations.

## Decision

The session lines open with `total exposure : 2 h 54 min 30 s` (`expunere totala`): in the
console, for `--inventory` and the series report alike and so on the Report tab, and as the first
row of the SESSION card, which sets its label and its value in bold, 2 px larger than the other
rows (14 and 15 px against 12 and 13). The figure is the interval line's own, the integration of
R-006 over the timed frames of `lights\`, rounded to the second as `RunReport.Duration` rounds;
the hours drop under an hour and the minutes under a minute. The core writes the line
(`InventoryReport.TotalLabel`, `RunReport.DurationToTheSecond`); the view-model marks the row
whose label is `TotalLabel` (`RowVm.Emphasis`) and `SummaryView` draws it, so the window still
computes nothing (ADR 0008).

## Alternatives rejected

- **Only the frames that stay in the stack**, the figure Siril puts in the name of its result
  (`result_9908s.fit`): it needs the frames' classes, so `--inventory` could not print it, and
  SUMMARY's `leave out of stack` row already gives it; the SESSION card describes what was shot.
- **Both figures in one row** (`2 h 54 min 30 s; in the stack 2 h 10 min 26 s`): one value was
  asked for, and the stack's figure has its own row.
- **`integration time` / `timp de integrare`**, the owner's words: the card would give one
  figure two names (the interval line says `total exposure`), and R-078 and its test would have
  to let the word back in; `total integration time`, 22 letters, does not fit the console's
  19-letter label column either.
- **`2:54:30`**: the report's other durations read `3 h 22 min`, and a clock form reads as a
  time of day.
- **The total in the window only**: the SESSION card is the console's session lines as rows,
  nothing reworded (R-030), and the Report tab is the console's text to the byte; the console has
  no bold, so it prints the line plain.

## Consequences

- R-086's tests: `R086_TheSessionLinesOpenWithTheTotalExposureToTheSecond` (the console, both
  languages), `R086_TheTotalExposureOpensTheSessionLines_InHoursMinutesAndSeconds` and
  `R086_WithoutAFrameWithATimeAndAnExposure_TheTotalIsUnknown` (the words, on the real nights'
  exposures, and the unknown case), `R086_TheSessionCardOpensWithTheTotalExposure` (the card's
  first row) and `R086_TheTotalExposureRowIsBoldAndLarger` (the look, rendered).
- The six golden texts with session lines gain one line each, and so does the README's
  `--inventory` sample.
- No figure moves: the line formats the integration V-001 records (10470.4 s), and no case of
  `docs/validation.json` changes for it. (V-014 was re-recorded the same day for another reason:
  the owner changed its session folder; `docs/validation.md`, History, 2026-11-07, evening.)
