# 0067. The exposure verdict is taken on the ratio as it is shown

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

The `exposure` row of SUMMARY gives a verdict, `GOOD` or `MARGINAL`, and beside it the figure
the verdict comes from: the lowest shot/read ratio of the sampled frames, written with one
decimal, with the limit, `good from 10` (`Th.SwampingRatioOk`). The verdict was taken on the
ratio as measured, so a ratio of 9.96 read `MARGINAL - ... shot/read 10.0 (good from 10)`: the
word against its own figure. The review of 2026-11-13 found the case in the golden files of
that day (CR-017, `confirmed`). The same row wrote what the read noise adds without a decimal,
`the read noise adds 5 %` at a ratio of 10.0, where the `what it means` row of EXPOSURE AND
NOISE says that from 10 up it `adds under 5 %` (4.9 % at 10); and that `5` was typed, not
worked out from the threshold.

## Decision

The verdict is taken on the ratio as it is shown: the figure is written with its one decimal,
read back, and compared with `Th.SwampingRatioOk`, so a measured 9.96 reads `10.0` and `GOOD`,
and 9.94 reads `9.9` and `MARGINAL`. The owner chose it on 2026-11-20 over keeping the
measured ratio and writing more decimals when the rounding crosses the limit. A difference of
0.05 in this ratio is far below what the measurement tells apart: the three sampled frames of
one session differ among themselves by more.

The row writes what the read noise adds with one decimal, as the rows of EXPOSURE AND NOISE
do (`adds 4.9 %`), and the `5` of `adds under 5 %` is the first whole percent above what the
read noise adds at the threshold (`NoiseAnalyzer.PenaltyPct`), so it follows the constant.

## Alternatives rejected

- **The measured ratio, with one more decimal at the limit** (`shot/read 9.96 (good from
  10)`, `MARGINAL`). True to the measurement, but it gives a session two different verdicts
  for figures the user cannot tell apart, and one row two formats.
- **The verdict taken on the rounded value by `Math.Round`.** The formatter and `Math.Round`
  do not round every binary value alike at a midpoint; reading the written digits back cannot
  disagree with them.

## Consequences

The other figures written beside a limit (an elongation, a star count, a streak, the pattern
kept in the stack, the RA drift) are CR-040 of the same review and are not changed here; the
way taken here, the written figure read back (`RunReport.Shown`), is there for them.
