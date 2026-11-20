# 0056. Without lights check 9 judges only the minimum of ten darks

Date: 2026-11-14
Status: accepted 2026-11-14
Supersedes: 0051 in part (the rule without lights); 0007 in part (enough frames by the quantisation step)

## Context

Check 9 answers "how many darks are enough". With measured lights it holds the master's noise
against the lights' stack (ADR 0051). Without lights, which is every library folder, it kept
the rule of ADR 0007: the median master's noise under one step of the file's values, at least
ten frames. The code review of 2026-11-13 (CR-007) showed that this rule turns on the camera
and not on the darks: it passes 30 darks only while the frame-to-frame noise is under 4.37
steps. On the owner's D3300, whose values go 2 by 2, it asks for 10 frames on the Smida darks
and on the three libraries and 16 on the leak set (from the recorded noise of one frame
against the next, 1.43, 4.83, 4.42, 3.99 and 6.26 ADU, as `max(10, ceil((1.2533 x sigma /
2)^2))`); on a camera whose values go 1 by 1 the same noise of 6.26 ADU would ask for 62, and
9 ADU for 128. The same darks opened with their lights are judged on the stack, which gave 11
frames for one of the owner's pairs and 39 for another (R-024's two example verdicts), so an
`OK` by the step said little either way.

The owner chose on 2026-11-14, in chat, between keeping the rule and judging only the minimum.

## Decision

Without measured lights the frames needed are `Th.MinDarks` (10) and nothing else is judged:
under ten the verdict is the existing WARNING, from ten on it is OK and says that whether so
many are enough shows only against the lights, with the way to ask (`--lights`). The table of
what fewer or more darks would give stays, with the master's noise per row; its rows of ten
frames or more carry no rating (`FramesRating.Unjudged`), those under ten keep `too few`.
`Th.MasterNoiseVsStep` is removed. The value step itself stays a row of the report's header
and of the scale section, where it is a fact about the file.

## Alternatives rejected

- **Keep the rule of the step.** The owner's call. It works on the D3300 by the accident of a
  step of 2 and asks for a hundred darks elsewhere.
- **UNMEASURED without lights.** Every library would carry an unmeasured check, and the one
  thing that is known, that ten frames make a sound median, would be lost.
- **A floor on the step, or the read noise in its place.** Another constant chosen to give the
  answers of today on one camera; a library has no biases to give a read noise.
- **Rating the rows from ten up as `enough`.** The word would say what the verdict now
  declines to say.

## Consequences

On the owner's sets nothing changes in the verdict: the five recorded dark sets have 20 to 35
frames and read `OK` before and after; only the leak set's frames needed go from 16 to 10,
which no case records. `/validate` was run with the change (the plan
`docs/plans/2026-11-14-fix-cr-006-to-cr-010.md` holds the result). Four golden texts changed,
each on check 9's rows of a set without lights. A dark set that is noisy for its lights is no
longer flagged when opened alone: the report says to open it with the lights. Guarded by
`R024_WithoutLightsOnlyTheMinimumOfTenDarksIsJudged`.

## Note of 2026-11-19

The figures above that follow from the noise from one frame to the next are those of the first
pair of frames, as it was read until 2026-11-19. Since ADR 0068 that noise is the median over
every pair of consecutive frames: 1.48, 5.41, 4.70, 4.19 and 6.79 ADU on the five sets, where
this record says 1.43, 4.83, 4.42, 3.99 and 6.26. The rule decided here does not depend on
them.
