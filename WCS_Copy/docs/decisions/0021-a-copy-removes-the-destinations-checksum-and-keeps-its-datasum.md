# 0021. A copy removes the destination's CHECKSUM and keeps its DATASUM

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The SyQon CLI writes `CHECKSUM` and `DATASUM` at the end of its header; V-002's destination holds
`CHECKSUM= '6jF39j936jE36j93'` and `DATASUM = '2647693551'`, valid before a copy. A copy changes
the header and wrote every other card back verbatim, `CHECKSUM` included, which then no longer
matched: the review of 2026-11-16 showed the sum going from valid to `0x2452C8C8` (CR-004), so a
reader that verifies reports the file as corrupted. The Python prototype, through astropy 8.0.1,
kept the checksum valid. Measured on 2026-11-18 on a 100 x 80 file written by astropy with
`checksum=True` and a copy with one header byte changed: Siril 1.4.4 (`siril-cli` with a private
empty settings file, `load` and `stat`) read both without a word; astropy warns `Checksum
verification failed for HDU ('PRIMARY', 1).` only when opened with `checksum=True`. ASTAP and
PixInsight are not installed on the owner's machine and were not measured. The FITS checksum
convention asks a program that changes an HDU to update the card or remove it. Given the two,
recomputing the card with a test against the real fixture or removing it, the owner chose on
2026-11-18 to remove it.

## Decision

Every copy that writes, in place or into a new file and whatever the source holds, removes the
destination's `CHECKSUM` cards and leaves `DATASUM` as it was, the data area being the
destination's, unchanged (R-018). The screen says nothing of it; the log has `drop CHECKSUM:
<card>` at Debug, a dry run's too.

## Alternatives rejected

- **Recompute `CHECKSUM`.** It keeps the file verifiable, as the prototype did, but makes the
  copy compose a second card besides `HISTORY` and needs `DATASUM`, the card's fixed format and
  a new date in its comment; the owner chose the plain removal.
- **Leave it.** A reader that verifies takes the file for a damaged one, while nothing in it is.
- **Say it on screen.** The sum changes nothing in the image or in its plate solve, and Siril
  reads neither card; the log is where a diagnosis looks.

## Consequences

The one card the copy composes is still `HISTORY`; besides the old plate-solve keys of R-016, the
copy drops this one card of the destination's. The done line's `dropped` count stays R-016's
keys, so V-002's recorded figures do not move. A file copied into keeps a valid `DATASUM`, which a
verifying reader can still check. Guarded by `WcsCopyTests`' `R018_` tests,
`ValidationTests.R018_ACopyIntoTheRealSyQonHeader_DropsItsChecksumAndKeepsItsDatasum` on the real
fixture, and the log's drop line in `LoggingTests`.
