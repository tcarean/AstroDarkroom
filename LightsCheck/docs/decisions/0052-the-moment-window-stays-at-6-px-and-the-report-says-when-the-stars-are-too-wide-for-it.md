# 0052. The moment window stays at 6 px, and the report says when the stars are too wide for it

Date: 2026-11-14
Status: accepted 2026-11-14; the moment window of 6 px, and the limit and the words of the wide stars row superseded by 0056; the wide stars row superseded by 0059

## Context

A star's width is taken from its second moments inside a circle of 6 px (R-014,
`Th.MomentRadius`), whatever the star. The review of 2026-11-13 (CR-007, `owner`) said the
circle cuts a wide star's wings, so the width is read too narrow, the frames of a session of
wide stars read closer to one another than they are, and a frame 1.4 times wider than the
session's, soft by `Th.SoftFwhmFactor` (1.3), is not flagged. Measured on 2026-11-14.

Clean Gaussians through `StarShapes.Measure`, and the same with a circle that follows the star
(its radius set to a multiple of the width read, 6 px at least, until it settles):

| True width | Radius 6 | Radius 2 x the width | Radius 1.5 x the width |
| ---: | ---: | ---: | ---: |
| 2.0 px | 2.00 | 2.00 | 2.00 |
| 3.0 px | 3.00 | 3.00 | 3.00 |
| 4.0 px | 3.98 | 4.00 | 3.98 |
| 5.0 px | 4.82 | 5.00 | 4.99 |
| 7.0 px | 5.89 | 7.00 | 6.98 |
| 10.0 px | 6.52 | 10.00 | 9.94 |
| 15.0 px | 6.75 | 15.00 | 14.93 |

On a clean star the following circle is exact. On the real frames it is not. The median width
of the about 400 measured stars of six raws of the recorded sessions, the same stars at each
radius:

| Frame | Radius 6 (today) | 8 | 10 | 12 | 1.5 x the width | 2 x the width |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Veil/2026-10-06-3h`, `DSC_0001` | 3.85 | 4.27 | 5.01 | 6.14 | 3.85 | 5.00 |
| `Veil/2026-10-06-3h`, `DSC_0060` | 4.07 | 4.53 | 5.32 | 6.44 | 4.27 | 10.54 |
| `Veil/2026-11-06`, `DSC_0001` | 4.62 | 5.32 | 6.40 | 8.03 | 5.32 | 34.23 |
| `Veil/2026-11-06`, `DSC_0050` | 4.71 | 5.08 | 5.59 | 6.36 | 5.07 | 9.95 |
| `Milkyway-Core/2026-08-12-Smida`, `DSC_0002` | 4.46 | 4.79 | 5.17 | 5.73 | 4.64 | 5.37 |
| `Milkyway-Core/2026-08-12-Smida`, `DSC_0020` | 4.10 | 4.40 | 4.81 | 5.48 | 4.26 | 4.76 |

The reading grows with the circle, by 0.3 to 0.7 px from 6 to 8 and by 1.3 to 3.4 px from 6 to
12, on stars that did not change: the moments are taken on values clipped at zero, so the sky's
noise counts as flux all over the circle, and a larger circle holds more of it and more of the
neighbouring stars. A circle that follows the width it reads runs away (10 px, 34 px). The
width at radius 6 is therefore a steady index of the star rather than its true size, and a
wider circle is a worse one. The owner chose on 2026-11-14 to keep the window and say the
limit.

What the limit costs the soft-frame test, worked out on Gaussian stars from the reading at
radius 6 (the formula of a Gaussian cut at 6 px, which gives the first table's 4.82 as 4.81): a
frame is soft when it reads above 1.3 times the session's reading, and the readings sit closer
together than the stars.

| Session reads | Its true width | A frame is soft from a reading of | That frame's true width | Times wider than the session |
| ---: | ---: | ---: | ---: | ---: |
| 3.0 px | 3.00 | 3.90 | 3.92 | 1.31 |
| 3.5 px | 3.50 | 4.55 | 4.66 | 1.33 |
| 4.0 px | 4.03 | 5.20 | 5.60 | 1.39 |
| 4.2 px | 4.25 | 5.46 | 6.09 | 1.43 |
| 4.5 px | 4.60 | 5.85 | 7.05 | 1.53 |
| 4.7 px | 4.85 | 6.11 | 7.97 | 1.64 |
| 5.0 px | 5.28 | 6.50 | about 10 | about 2 |
| 5.2 px | 5.60 | 6.76 | about 14 or more | about 2.5 or more |

The formula follows the readings of the first table closely up to 5 px and less so above: for
7, 10 and 15 px it gives 5.83, 6.46 and 6.80 where the pixels gave 5.89, 6.52 and 6.75. There
the reading hardly grows with the star, 0.05 px of reading is a pixel or more of width, and the
last two rows are orders of size, not figures. At 4 and 5 px the formula and the pixels agree
to 0.01 px. A straight line between the first table's readings, in place of the formula, gives
1.5 and 1.7 times for the rows at 4.5 and 4.7 px, where the formula gives 1.53 and 1.64.

The row was first written for a reading above 5 px, the figure the review's entry names. The
review of the branch (2026-11-14) showed the table's point: at a reading of 5 px the test is
already one at twice the width, and the session of 2026-11-06, at 4.70 px, has it at 1.6 times
with nothing said. Put to the owner with 4, 4.5 and 5 px as the choices and 4.5 recommended,
the owner had no preference, so the recommended one stands.

## Decision

The window stays a circle of 6 px. The limit is written where the width is defined (R-014) and
in the README, and the series report says it when it matters: when the session's width (the
median FWHM of the good frames, as read) is above `Th.WideStarFwhm`, 4.5 px, FOCUS gets a row
`wide stars` before its verdict, `the stars are wider than 4.5 px; LightsCheck reads such stars
narrower than they are, so a frame slightly out of focus can pass as good` (R-088). At 4.5 px
the soft-frame test has become one at 1.5 times the session's width. No figure changes.

## Alternatives rejected

- **A circle that follows the star.** Exact on a clean Gaussian; on the sky it reads the noise
  and the neighbours (the last column above).
- **A fixed wider circle.** Every recorded width would move, by a figure that depends on the
  sky's noise more than on the stars (the middle columns above).
- **The reading corrected as for a Gaussian star.** It would move the recorded widths (4.71 to
  about 4.9 on `Veil/2026-11-06`), and above 7 px one tenth of a pixel in the reading is half a
  pixel or more in the answer (5.89 reads 7, 6.52 reads 10), while the sky's noise alone moves
  the reading by more than a tenth.
- **Only the documents, no row.** The one user who meets the limit, with a longer focal length
  or smaller pixels, would not be told where it matters.
- **The row from a reading of 5 px**, as first written: the test is at twice the width there.
- **The row from a reading of 4 px**, where the reading starts to part from the width: the test
  is at 1.4 times instead of 1.3, and the row would also stand on the 20 mm session, which
  reads 4.17 px, so on two of the three recorded sessions, for a test barely weaker than it
  says.

## Consequences

On stars wider than about 4 px the widths printed are too small and the soft-frame test is
weaker than its 1.3 says: a session at a true 5 px with a frame at 7 px reads 4.82 and 5.89, a
ratio of 1.22, and the frame is not flagged. That session reads above 4.5 px and gets the row;
nothing measures such sessions better until another estimator is written (a fit, or a width
at half height), a task of its own. Between a reading of 4 and of 4.5 px the test is at 1.4 to
1.5 times with no row. The second table is arithmetic on Gaussian stars, not a measurement on
a sky, where the reading also carries the sky's noise. The upper bound of the kept widths,
`Th.MaxFwhm` (15), is never met, since the reading cannot pass about 7 px on a clean star. Of
the recorded sessions, those that read 3.12 and 4.17 px get no row and `Veil/2026-11-06`, at
4.70 px, gets it; no recorded figure changes. Guarded by
`R014_TheWidthIsRightUpToFourPixelsAndTooNarrowAbove`,
`R088_ASessionOfWideStarsSaysTheyAreReadNarrowerThanTheyAre`,
`R088_ASessionThatReadsBetweenFourAndAHalfAndFivePixelsHasTheRow` and
`R088_ASessionOfOrdinaryStarsHasNoWideStarsRow`.
