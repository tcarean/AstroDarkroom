# 0061. "Do not go below q100" and "keep the 16-bit master" are facts of the Export tab, not warnings of the report

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

Section 1 of the report carried an Attention, "Do not go below q100", whenever a JPEG quality
under 100 failed the maximum-error threshold, and section 6 another, "Keep the 16-bit master",
whenever the shadow-lift stress test lost more levels from 8 bits than from 16. Both fired on
nearly every real image (the owner's 2026-08-12 core denoised by Syqon at 0.75,
`starless_denoised_syqon_studio_in_max_0.75_stretch0.15.fit`, measured 2026-10-27: q100 at 4.5
levels, q95 at 25.3 and q90 at 32.4 against a threshold of 10, 37 levels missing from 8 bits),
and each raised its section to CAUTION over something the owner does not decide at print time: the lab gets the TIFF the first line recommends, and the master is kept anyway.
On 2026-10-27 the owner judged that neither is worth a warning and asked for the two to live
on the tab where their measurements already are, the Export tab, which holds the JPEG table
with its "what is recommended" paragraph and the shadow-lift rows.

## Decision

The two recommendations leave the report: section 1 keeps the TIFF line and the q100 line with
its figures, section 6 keeps the profile and reader's notes. The Export tab marks the two
facts: its "What is recommended" paragraph names the highest quality under 100 that fails and
its maximum error against the threshold, and the "... from 8 bits" row of the stress test keeps
its mark "keep the 16-bit master" when the levels missing pass `MissingLevelsWarn`. The text
report still prints the JPEG table and the stress test in Appendix C, so the figures are not
lost to the console. `JpegResult.HighestFailingBelow100` names the row for the tab, so that the
view-model selects nothing itself.

## Alternatives rejected

- **Keep them as Ok lines in the report.** An Ok line that tells the reader not to do something
  reads as a warning in all but its chip; and the report is for decisions, these are not ones.
- **Put them on the Rezolutie tab.** The owner's first wording; the tab is about stars and
  sizes, and the Export tab already holds both measurements.

## Consequences

- A frame whose only Attention in section 1 was the q100 floor, or in section 6 the master, has
  that section Ok; the dark scene's goldens lose the master line and the Veil's and the core's
  reports the q100 one.
- Guards: `RecommendationsTests` (R-091, R-096), `ExportTabTests` or the view-model tests
  (R-114), the golden files.
