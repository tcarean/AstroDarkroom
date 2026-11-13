# 0050. The sensor's pattern is refused when the frames stayed within three star widths

Date: 2026-11-13
Status: accepted 2026-11-13
Supersedes: 0015 in part (the 3 px distance at which the frames' largest cluster is judged before the pattern is read)

## Context

ADR 0015 reads the sensor's pattern from the per-pixel median of the unaligned lights, and only
"while the largest cluster of frames at 3 px holds under half the registered frames, so no
pixel sees a star in half the frames". The review of 2026-11-13 (CR-005, `to measure`) said
that holds for a star no wider than 3 px. Measured that day.

On synthetic crops (256 px, noise 10 ADU, 25 Gaussian stars of 30 to 300 times the noise, 40
frames that drift along X without dithering), with the rule of ADR 0015:

| Stars | Drift a frame | Largest group at 3 px | False hot pixels of 61,504 |
| --- | ---: | ---: | ---: |
| none | 0.17 px | 18 of 40 | 0 |
| 4.2 px wide | 0.17 px | 18 of 40 | 1127 |
| 3.0 px wide | 0.17 px | 18 of 40 | 583 |
| 6.0 px wide | 0.17 px | 18 of 40 | 1940 |
| 4.2 px wide | 0.30 px | 11 of 40 | 903 |
| 4.2 px wide | 0.50 px | 7 of 40 | 52 |
| 4.2 px wide | 1, 2 and 4 px | 4, 2 and 1 of 40 | 0 |

On the recorded sessions, the largest group of the greedy clustering at several distances:

| Session | Stars' width | at 3 px | at 10 px | at 12 px | at 15 px | Hot pixels in the median |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `Milkyway-Core/2026-08-12-Smida` | 4.17 px | 12 of 32 | 15 | 20 | 20 | 14, of which 13 in two tight groups, at (433-435, 169-173) and (205-208, 307-310) of the crop; none still hot in the lower quartile of the frames |
| `Veil/2026-10-06-3h` | 3.12 px | 4 of 87 | 12 | 14 | 16 | 1, alone, still hot in the lowest tenth of the frames |
| `Veil/2026-11-06` | 4.70 px | 4 of 82 | 8 | 7 | 10 | 0 |

A hot pixel of the sensor is hot in every frame; a star is on a pixel only in the frames that
put it there. Smida's 13 are two stars: the session has three spots (12, 8 and 12 frames, the
first two between 10 and 12 px apart, where the largest group grows from 15 to 20 frames), and a bright star's core, wider than its width at half height,
covers the pixels between two spots in 20 frames of 32. V-013 recorded them as 55 hot pixels
per megapixel. The owner chose on 2026-11-13.

## Decision

The pattern is read only while the largest group of frames within three times the stars' width
(`Th.PatternClusterWidths` times the median over the frames used of their median FWHM), 3 px at
least and when no frame has a width, holds under half the registered frames. The group is the
greedy clustering of the dithering tables. At that distance Smida is refused (20 of 32 within
12.5 px), both Veil sessions are measured as before (12 of 87 within 9.8 px and 9 of 82 within
14.1 px, the width being the median over every frame used, the trailed ones included), the
synthetic drifts of 0.17, 0.3 and 0.5 px a frame are refused and those of
1 px a frame and more, which read no false hot pixel, are measured. The report's sentence names
the distance and what it is: `the largest group within 13 px, three times the stars' width, has
20 frames of 32`.

## Alternatives rejected

- **A pixel is hot only when it is hot in nearly every frame** (a low percentile across the
  frames beside the median). It would keep a figure on Smida, but it is a new method with a
  threshold of its own to measure, and frames that did not move at all would still need the
  refusal. Worth a task of its own if a session like Smida should give a figure.
- **Two star widths.** At 10 px (2.4 widths) Smida's largest greedy group is 15 of 32, under
  half, and its two stars would still be counted.
- **The most frames within the distance of any one frame, instead of the greedy group.** On
  the synthetic drift of 1 px a frame it finds 26 of 40 within 12.6 px and refuses a session
  that reads no false hot pixel; the greedy group, measured from its first frame, finds 13.
- **Leaving the rule.** The figure printed for Smida was not the sensor's.

## Consequences

`docs/validation.json`: V-013's pattern goes from `Measured` (sigma 1.1 / 1.2 / 1.2 / 0.9 ADU,
55 hot pixels per megapixel) to `FramesDidNotMove` with no figures, by the owner's approval of
the plan `docs/plans/2026-11-13-fix-cr-001-to-cr-005.md`; `docs/validation.md` has the dated
line. A session that was not dithered and drifted by less than about six star widths in all
gets no pattern figure; its dithering verdict already says what that costs. Two limits of the
rule, known and not measured on a session. A session dithered by small steps, every frame
within three star widths of the first, is refused with the same sentence, `the frames stayed
in place too much`, though it moved. And the margin on Smida is thin: its two near spots are
within the 12.5 px by a pixel or two, and a little farther apart they would be three groups
under half again, with a bright star's core perhaps still on both; the alternative of a pixel
hot in nearly every frame, above, is the answer to that case. The walking-noise
verdict does not read the measured pattern (it reads the pattern that survives, from the
positions), so no verdict moves. `PatternAnalyzer.Analyze` takes the positions and the frames'
widths; `SensorPattern` carries the distance and the group. Guarded by
`R046_StarsThatDriftSlowlyWithoutDitheringGiveNoFigure`,
`R046_StarsThatMoveWithTheDitherLeaveNoHotPixel`,
`R046_TheGroupIsJudgedAtThreeTimesTheStarsWidth` and
`R046_FramesWithinThreeStarWidthsSayTheGroupAndItsDistance`.
