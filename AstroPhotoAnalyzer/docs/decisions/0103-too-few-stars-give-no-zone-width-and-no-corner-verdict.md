# 0103. Too few stars give no zone width and no corner verdict

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

Since ADR 0062 a frame with under 20 detections, or under 10 per megapixel, has no star width:
what a denoise leaves on a starless frame is a few dozen soft blobs, not stars. The code review
of 2026-11-13 (CR-006) found that `StarAnalyzer.Analyze` measured the five zones and the corner /
centre ratio before that test and kept them on the insufficient result. Section 4 could then say
"Without stars it cannot be measured how much detail the image holds" and, as an Attention, that
the stars in the corners are wider than those in the centre; Appendix D printed "too few to
measure the stars' width" and on the next line a zone's width (`Golden/mono-png8.txt`: 16
detections, `centru 3.09 px (6 stele)`). The entry asked for no ratio and no verdict; whether the
zones' widths stay was put to the owner on 2026-11-16, who chose that they go too.

## Decision

On a frame whose stars are too few nothing is measured on the detections: no width for the
frame, none for a zone, no corner / centre ratio, and so no soft-corner verdict. A zone keeps its
count, which is a fact, and reads `n/a`, in Appendix D and in the Rezolutie tab. The test moved
before the zones in `StarAnalyzer.Analyze`; the report and the tab follow from the result with
no change of their own.

## Alternatives rejected

- **Drop the ratio and the verdict, keep the zones' widths.** A width of "the stars in the
  centre" under a line that says the stars are too few to measure says two things at once, and
  the blobs' width is not the image's detail.
- **Test `Insufficient` where the verdict is written** (`SoftCorners`, `Recommendations.Size`).
  The ratio would still be printed by Appendix D and the tab.

## Consequences

- `Golden/mono-png8.txt` and `.en.txt`, Appendix D: `centru 3.09 px (6 stele)` reads
  `centru n/a (6 stele)`; nothing else in the two files moved.
- A sparse but real star field under the floors (thirty stars on 4 MP) loses its zone widths
  with its frame width, as ADR 0062 already accepted for the latter.
- Guard: `R071_TooFewStarsGiveNoZoneWidthNoRatioAndNoSoftCorners`.
