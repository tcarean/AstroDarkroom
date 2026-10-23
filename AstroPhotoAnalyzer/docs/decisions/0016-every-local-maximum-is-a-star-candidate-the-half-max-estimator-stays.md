# 0016. Every local maximum above mean plus amplitude is a star candidate; the half-max estimator stays, with its bias

Date: 2026-10-02
Status: accepted 2026-10-02; a tied maximum counted once per tied pixel superseded by 0041

## Context

Retroactive: recorded on 2026-10-02 from `docs/implementation-plan.md` ("Notes from phase 3",
"Notes from phase 4"), `docs/specification.md` §5 (D1, "Known") and
`docs/acceptance-de-printat.md` (the star rows); the decision dates from phases 3 and 4,
2026-09-20. The acceptance script drew 20 000 random candidates above 3 x the plane mean and kept
193 stars, median FWHM 3.00 px. Ported as it was, that threshold failed on the golden scene with a
0.25 background: 3 x mean put it at 0.75, and no star of peak 0.55 qualified, which is exactly
what would happen on the print version of a real image, whose background is lifted to 0.19-0.25.

## Decision

- Every pixel of the reference plane is scanned; a candidate is the maximum, ties allowed, of its
  25 x 25 window, above the plane mean plus the minimum amplitude (3000/65535) and below
  saturation (60000/65535). The amplitude against the median of the window's border, the
  script's real filter, is unchanged.
- The width is the script's half-maximum estimator: the crossing of half the amplitude on the
  horizontal and vertical lines through the peak, interpolated linearly, the mean of the two. Its
  known bias is kept: about +2.5 % on a sampled Gaussian (a true FWHM of 3.0 measures about
  3.08), far smaller than the spread from star to star.

## Alternatives rejected

- **3 x the mean.** It rejects every star once the background is lifted.
- **A random sample of candidates.** It is less sampled and depends on the draw; scanning every
  pixel is deterministic.
- **A fitted profile instead of the half-maximum crossing.** A different estimator from the one
  the acceptance numbers were made with, to remove a bias that does not change a decision.

## Consequences

- On the reference image the tool keeps 7166 stars, median 3.13 px (Q1 2.87, Q3 3.49), against
  3.00 by hand; on a dark sky the threshold admits fainter stars than the script did.
- On bright nebulosity stars measure wider, because the border median overestimates the base: the
  reference image's centre reads 4.27 px, so its corner/centre ratio (0.77) is no statement about
  sharpness (`known-limitations.md`).
- Guards: `StarAnalyzerTests` (Gaussian stars of known FWHM within a stated tolerance), the golden
  files, validation cases V-001 and V-002.

Source: docs/implementation-plan.md; docs/specification.md §5; docs/acceptance-de-printat.md.
