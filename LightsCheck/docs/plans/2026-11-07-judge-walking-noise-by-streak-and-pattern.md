# Judge walking noise by the streak and the pattern

Branch `claude/Judge-walking-noise-by-streak-and-pattern`, worktree
`.claude/worktrees/Judge-walking-noise-by-streak-and-pattern`. Approved in chat on 2026-11-07
("mergem pe recomandarile tale. Fa schimbarile") after the requirement draft, the thresholds and
the texts were shown; the plan is written here without a plan-mode round, on that approval.

## Context

The Veil session of 2026-11-06 was dithered after 77 of 85 frames and still read `INSUFFICIENT
for walking noise`, because the rule of ADR 0007 counted different spots and frames alone at
10 px, which a random dither in a bounded area cannot satisfy. ADR 0039 records the decision:
the verdict reads the frames whose streak would show and the pattern that piles. R-084 states
the behaviour; R-021, R-043 and R-077 point at it.

## Steps

1. `docs/requirements.md`: R-084 draft, R-021, R-043 and R-077 amended (done before this plan).
2. Tests, failing first: `DitheringTests` (`WalkingStretches`, the old `WalkingNoiseOk` asserts
   replaced), `SeriesAnalyzerTests` (R-084: the fractions and the three words on synthetic
   sessions), `SeriesTests` (the Romanian verdict text of R-077), `ValidationTests` and
   `docs/validation.json` (`walkingSharedFrames` on V-008 and V-013, V-014 for the new session).
3. Core: `Thresholds.cs` (two constants in, three out), `Analysis/Dithering.cs`
   (`WalkingStretches`), `Analysis/SeriesAnalyzer.cs` (`WalkingSharedFrames`,
   `WalkingSharedFraction`, the verdict), `Report/SeriesReport.cs` (`DitherVerdict`'s text),
   `Logging/LogText.cs` (the dither line).
4. Goldens regenerated with `LIGHTSCHECK_UPDATE_GOLDEN=1`; every changed line read and tied to
   the verdict text at wrap-up.
5. `/validate` on the three sessions; the figures of V-014 filled from the run.
6. `docs/design/series-analysis.md`, `README.md` (the verdict list), `CHANGELOG.md`, ADR 0007
   and 0015 status lines, R-084 to `implemented`.
7. `scripts/verify.ps1`, commit, a review subagent, the merge proposal.

## What deviated

- No plan-mode round: the owner's "Fa schimbarile" came after the requirement, the thresholds
  and the texts were shown, so the plan was written and followed on that approval.
- V-014 was first recorded from the morning's run on a `lights\`-only folder (no biases, no
  darks); by the validation run the owner had added 30 biases and 40 darks, so the case failed
  on `darks` (0 against 40) and was re-recorded from a second console run with the read noise,
  the swamping and the darks; the dither and walking figures did not move.
- V-014's `session` is `Veil/2026-11-06`, the folder as the owner left it the morning after
  the session; the earlier session is `Veil/2026-10-06-3h`, so a rename would move the key.
- The R-084 example first said "its last 10 frames on one spot" for the `Static` case; a run of
  10 at one spot draws a dash, so the test and the example use ten pairs on the same pixels,
  30 px apart, instead.
