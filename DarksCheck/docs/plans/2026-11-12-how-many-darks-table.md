# Check 9 says how good the master is with 5, 10, 15, ... darks

Saved at approval as `docs/plans/2026-11-12-how-many-darks-table.md`.
Branch `claude/Say-how-good-the-master-is-with-fewer-darks`, worktree
`.claude/worktrees/Say-how-good-the-master-is-with-fewer-darks`, from `main`.

## Context

The owner has 35 darks and wants to know whether 30 would do, so the frames at a poor
temperature (check 4's `to discard` row) can be dropped. Check 9, "How many darks are enough",
does not answer:

- the verdict says only `OK: 35 frames are enough`; its rule (the master's noise under the
  file's value step, 2 ADU on the D3300) is met from 10 frames on every series of the owner's,
  and the report never says so;
- the `noise when averaged` table (1, 2, 4, 5, 10 frames; measured, expected) only checks that
  the noise falls as it should when frames are averaged. It carries no verdict and stops at 10.

Decided by the owner on 2026-11-12 (to record in ADR 0050):

1. The new table has, for each n: the master's noise, how much higher it is than with all the
   frames, and how much noisier the lights stack comes out. The stack column only with lights
   (a session's, or `--lights`).
2. With lights, the darks are enough when the master makes the stack at most **10 %** noisier.
   Without lights the rule of today stays (noise under the value step, at least 10 frames).

Estimate that led to the choice (from recorded figures, not a run; no dithering assumed): the
35 darks at -2 °C with the 83 Veil lights give about 32 / 17 / 12 / 9 / 7 / 6 / 5 % at
5 / 10 / 15 / 20 / 25 / 30 / 35 darks, so about 18 darks are enough at 10 %.

## The model (all of it already in the core)

- Master of n frames: `Th.MedianNoiseFactor x temporal sigma / sqrt(n)`, the formula of
  `NoiseVsN.MasterNoise` (`DarkSeriesAnalyzer.NoiseCurve`), read at n instead of N.
- Against all the frames: `sqrt(N / n) - 1`.
- The stack: `StackNoise` (`Analysis/StackNoise.cs`, ADR 0016): the lights' share
  `sky sigma / sqrt(lights)` and the master's noise in quadrature, the master not averaging
  out. Noisier by `sqrt(1 + (master(n) / LightsShareAdu)^2) - 1`. This is the case without
  dithering, the worst one; the row's note says that with dithering it is less.
- Frames needed with lights: the smallest n with that figure at or under the limit, at least
  `Th.MinDarks`.

## Steps

1. **Measure first** (the kit's rule "Measured numbers"). In the worktree, run the console on
   the -2 °C darks with `--lights Veil/2026-11-08`, on Smida and on the library with
   `--lights Veil/2026-10-06-3h`; read the temporal sigma, the sky sigma and the lights count
   from the report and the log; compute the table by hand. Paste the three tables to the owner.
   If the real figures contradict the estimate above (18 darks at 10 %), that goes back to the
   owner as a new question before any code.
2. **Requirement.** Rewrite R-024 in `docs/requirements.md`: the table's rows, the three
   figures, the 10 % rule with lights, the old rule without, the words, examples from step 1.
3. **Texts shown to the owner first** (rule "Plain words"): the old and the proposed wording
   side by side, English and Romanian, edited only on a yes. Drafts:
   - row `master with fewer or more frames` / `master cu mai putine sau mai multe cadre`,
     console value `5 frames: 2.07 ADU (2.6 times the noise of now; the stack 32 % noisier);
     10 frames: ...; 35 frames (now): 0.78 ADU (the stack 5 % noisier)`;
   - window table: `frames | master's noise | against now | the stack is noisier by | how it is`
     (`too few` under 10, `few` over 10 %, `enough`); note under it: the stack figure is for
     lights without dithering, with dithering it is less;
   - verdict with lights: `OK: 35 frames are enough; from 18 on the master makes the stack of
     the 83 lights at most 10 % noisier, so up to 17 can be left out` /
     `WARNING: 12 frames make the stack 15 % noisier; 18 are needed for at most 10 %`;
     without lights: today's words plus `from 10 frames on`;
   - `noise when averaged` becomes one sentence without a table: the noise falls as it should
     when frames are added (the last measured point and what the rule gave).
4. **Tests first** (`tests/DarksCheck.Tests/DarkSeriesTests.cs`, `LightsAndIndexTests.cs`):
   `R024_...` for the rows (multiples of 5, the row of N itself, two steps past N, on to the
   first row that is enough, capped), the three figures against hand values, the frames needed
   with and without lights, the verdict flipping at the limit, one frame and under 10 frames.
   The table tests of today (`R024_TheNoiseCurveRowCarriesItsPointsAsATable`,
   `MainViewModelTests` line 317) follow the row that now carries the table: an intended change
   of R-024, named at wrap-up.
5. **Code, in the core only.**
   - `Thresholds.cs`: `MasterStackPenalty = 0.10`, `FramesTableStep = 5`,
     `FramesTablePastCount = 2`, `FramesTableMax = 100`, each with its why.
   - `Analysis/DarkVerdicts.cs`: `FramesPoint(N, MasterNoise, AgainstNow, StackPenalty, Rating)`
     and `EnoughVerdict` gains the points, the frames needed by the rule in force, which rule
     it was and the lights count. `DarkVerdictAnalyzer.Judge` runs before the lights are read
     (`Runs/DarkSeriesRun.cs` line 269 against 289), so the enough verdict moves to a method
     `EnoughOf(DarkSeriesAnalysis, StackNoise?)`, called by `Judge` without lights and again by
     `DarkSeriesRun` once the lights are measured (`verdicts with { Enough = ... }`).
     `StackNoise.Of` is reused as it is.
   - `Report/DarkReport.cs`: `CurveRow`, `Enough`, `EnoughVerdictText` and the section at line
     246; the new row carries the `ReportTable`; the master's noise of N stays bold.
   - `Logging/`: the new figures in the run's record if `LogText` lists the enough verdict.
   - `NoiseVsN.Curve` and the window's noise chart stay as they are.
6. **Window.** `Theme/Rows.xaml`'s `ReportTable` template has three fixed columns; make it
   take the header's count (five here, three for check 2's table). `MainViewModel` only passes
   the table on. Tests in `tests/DarksCheck.Wpf.Tests/` (rendering of the five columns).
7. **Goldens.** Regenerate with `DARKSCHECK_UPDATE_GOLDEN=1`; every changed line must be in
   check 9 (three rows) of the ten files; read and reported.
8. **Validation.** `/validate` on all eleven cases, drift table pasted. No recorded figure is
   expected to move (the darks cases have no lights; Smida needs 10). New figures to record at
   the owner's yes: frames needed and the stack figure at N for V-005, V-012, V-013.
9. **Docs.** ADR 0050 (the two decisions, the no-dithering model, alternatives);
   `docs/design/dark-analysis.md` (the verdicts section) and `window.md` (the table template);
   `README.md` line 316; CHANGELOG `Changed`; XML comments on everything added or edited.
10. **Wrap-up.** `dotnet build --no-incremental -warnaserror`, `scripts/verify.ps1`, code
    review by a subagent, commit on the branch, merge proposed. The wiki page and a version
    bump stay the owner's call, named at wrap-up.

## Verification

- `dotnet test DarksCheck.sln -c Release --filter "FullyQualifiedName~R024"` green.
- The console on the three pairs of step 1: the printed table equals the hand table.
- The window rendered off-screen on the -2 °C darks with the Veil lights: the five columns,
  the row of 35 marked as now, the verdict naming 18 (or what step 1 measures).
- `scripts/verify.ps1` prints `== OK`; `/validate` shows no drift.

## Amendment of 2026-11-13, after step 1's measurement

The estimate above was wrong: it took 22 ADU for the noise of a Veil light, and the stack figure
the program has (`StackNoise`) reads 40.7 ADU there. The program holds two noises of a light:
the sky sigma (`LightFigures.SkySigma`: the whole plane, so vignetting, nebula and the Milky Way
count as noise) and the noise against the neighbours (`LightFigures.LocalSigma`, the `noise`
column of the lights table, what R-067's traces already call the noise of a light).

Measured 2026-11-13 (temporal sigma, darks, lights; sky sigma / neighbour noise, raw ADU):

| pair | figures | stack noisier, sky sigma, at 5 .. 35 darks | stack noisier, neighbour noise |
| --- | --- | --- | --- |
| the -2 °C darks + Veil 2026-11-08 | 3.40, 35, 83; 40.7 / 26.27 | 8.7 / 4.5 / 3.0 / 2.2 / 1.8 / 1.5 / 1.3 % | 19.9 / 10.4 / 7.1 / 5.3 / 4.3 / 3.6 / 3.1 % |
| Smida | 1.426, 20, 32; 18.9 / 6.77 | 2.8 / 1.4 / 0.9 / 0.7 % (5 .. 20) | 20.2 / 10.6 / 7.2 / 5.4 % |
| the 15.8 °C library + Veil 2026-10-06 | 4.83, 30, 87; 33.2 / 19.79 | 25.6 / 13.5 / 9.2 / 7.0 / 5.6 / 4.7 % (5 .. 30) | 62.1 / 34.7 / 24.2 / 18.6 / 15.1 / 12.8 % |

Frames needed with the neighbour noise: 11 / 11 / 39 at 10 %, 22 / 22 / 80 at 5 %; with the sky
sigma 10 / 10 / 14 and 10 / 10 / 29.

The owner's answers of 2026-11-13, which replace decision 2 and the model's stack line:

1. Check 9 counts the stack with the **noise against the neighbours**, in check 9 only; the ADU
   scale section keeps `StackNoise` as it is (a task of its own, proposed at wrap-up).
2. The limit is chosen in the window from a **dropdown in check 9's card: 5 % and 10 %, 10 % the
   default**. Assumptions stated to the owner: the choice re-judges without reading the frames
   again and is not remembered between starts; the console judges at 10 % and a row
   `enough from` gives the frames for both limits, so the console gets no new option.

Changes to the steps: `StackNoise` is not used by check 9; the lights' noise per plane comes
from the loop `LightsAnalyzer.TracesOf` already has, made a method; `Thresholds.cs` gets
`StackNoiseLimits` (0.05, 0.10) and `StackNoiseLimit` (0.10) in place of `MasterStackPenalty`,
and no `FramesTableMax` (past N the table has two more steps and then only the row of the
frames needed); `ReportRow` gains a `Choice` (label, options, selected) the window renders as
the dropdown above the table, and `DarkSeriesRun.WithStackLimit` gives the result judged at
another limit; the texts shown to the owner on 2026-11-13 replace the drafts of step 3.

The ADR of this task is 0051: main gained an ADR 0050 on 2026-11-11, before the branch was cut.
