# 0066. A figure that rounds to zero has no minus, and reads +0 where the sign is always written

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

`RunReport.F`, the one formatter of the figures the user reads, is .NET's fixed format, which
keeps the minus of a negative number even when every digit it writes is 0. The reports so read
`halo : -0.000`, `(-0 %): the focus held the whole session`, `Y -0.00 px/min = +1.8 / -0.0
"/min`, `the image turned by -0.0' in all`, a table cell `-0.0`; the review of 2026-11-13
listed them as CR-021, evidence `owner`, because two requirements then quoted `(-0 %)` in
their examples and two tests asserted it (by 2026-11-20 only the golden files held it). The
trail's angle, written in `SeriesAnalyzer` with a format of its own, and the labels along the
value axis of the window's charts had the same minus. FlatCheck met the question on
2026-11-06 and its owner, the same, chose there that a difference that rounds to zero reads
`+0.00` (FlatCheck's ADR 0092).

## Decision

A figure that rounds to zero at the decimals shown is written without a minus, whatever sign
the value had: `0.000`, `0.0`, `0`. Where the sign is always written (the change of a trend,
the drift between frames, a background jump, the declination) it reads `+0 %`, `+0.00`,
`+0.0`. The owner chose it on 2026-11-20 among three options (this one, a plain `0` without a
sign everywhere, and leaving the texts as they were), with FlatCheck's rule named as the
precedent. R-092 states it.

The sign is decided on the digits as written: `RunReport.F` formats first and drops the minus
when no digit other than 0 follows it; `RunReport.Signed` puts a `+` before whatever `F` wrote
without a minus. The trail's angle and the labels of the charts' value axis go through `F`.
The CSV keeps its numbers as they are: programs read it, and it writes every digit a number
needs.

## Alternatives rejected

- **A plain `0` where the sign is always written.** `(0 %)` beside `(+14 %)` and `(-3 %)` in
  the same place reads as another kind of figure; FlatCheck's reports already read `+0.00`.
- **Leaving `-0`.** It shows the sign of a value the reader cannot see, and nothing in the
  report is decided by it.
- **Rounding the value before formatting it.** `Math.Round` and the formatter do not round
  every binary value the same way at a midpoint, so a figure that does not round to zero could
  move; deciding on the written digits leaves every other figure byte for byte as it was,
  which the golden files confirmed (only the zeros changed).
