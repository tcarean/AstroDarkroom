# 0051. Check 9 says what fewer or more darks would give, and judges against the lights' stack

Date: 2026-11-13
Status: accepted 2026-11-13; the rule without lights superseded by 0056
Supersedes: 0041 in part (the table of the row `zgomot dupa medie`)

## Context

The owner had 35 darks and asked whether 30 would do, so that the frames check 4 lists as
`de aruncat` could be dropped. Check 9, "how many darks are enough", could not say. Its verdict
read `OK: 35 de cadre ajung` by one rule, the median master's noise under the file's value step
(`Th.MasterNoiseVsStep`, 2 ADU on the owner's camera), which every series of the owner's meets
from 10 frames on, and the report never named that number. Its table (ADR 0041), the noise of the
mean of 1, 2, 4, 5 and 10 frames beside sigma / sqrt(n), only shows that the noise falls as it
should; it carries no verdict and stops at 10. The owner asked for what 5, 10, 15, 20, 25, 30,
35, ... frames give, and how good each is.

Measured 2026-11-13 with the console of the branch, on three pairs of darks and lights (the
temporal sigma of the darks; the darks; the lights; the lights' sky sigma and their noise against
the neighbours, raw ADU on the reference plane):

| Pair | Figures | Stack noisier, on the sky sigma | Stack noisier, on the noise against the neighbours |
| --- | --- | --- | --- |
| the -2 °C darks of 2026-11-09 with the Veil lights of 2026-11-08 | 3.40; 35; 83; 40.7 / 26.27 | 8.7 / 4.5 / 3.0 / 2.2 / 1.8 / 1.5 / 1.3 % at 5 .. 35 darks | 19.9 / 10.4 / 7.1 / 5.3 / 4.3 / 3.6 / 3.1 % |
| the Smida session | 1.426; 20; 32; 18.9 / 6.77 | 2.8 / 1.4 / 0.9 / 0.7 % at 5 .. 20 | 20.2 / 10.6 / 7.2 / 5.4 % |
| the 15.8 °C library with the Veil lights of 2026-10-06 | 4.83; 30; 87; 33.2 / 19.79 | 25.6 / 13.5 / 9.2 / 7.0 / 5.6 / 4.7 % at 5 .. 30 | 62.1 / 34.7 / 24.2 / 18.6 / 15.1 / 12.8 % |

The last column is what the console of the branch prints; the one on the sky sigma is computed
by hand from the same figures. The frames that keep the stack within 10 % are 10 / 10 / 14 on the sky sigma and 11 / 11 / 39 on
the noise against the neighbours; within 5 %, 10 / 10 / 29 and 22 / 22 / 80. The two noises of a
light differ by up to 2.8 times: the sky sigma (`LightFigures.SkySigma`) is the spread of the
whole plane, so vignetting, nebulosity and the Milky Way count as noise; the noise against the
neighbours (`LightFigures.LocalSigma`) is the grain, the figure the lights table prints as
`zgomot` and R-067's traces already call the noise of a light.

## Decision

The owner's answers of 2026-11-12 and 2026-11-13:

1. **A table of frame counts.** The row `cu n cadre` gives, for every 5 frames under the frames
   stacked, for the frames stacked, for two more steps of 5 and for the frames needed when they
   lie beyond: the master's noise, that noise against the master of now, with lights how much
   noisier their stack comes out than with an ideal dark master, and a rating (`prea putine` under `Th.MinDarks`, `putine` under the
   frames needed, `ajung`). A row `ajung de la` names the frames needed.
2. **With lights, enough is judged against their stack.** The darks are enough when the stack
   comes out at most `Th.StackNoiseLimit` (10 %) noisier than an ideal dark master, one with no
   noise of its own, would leave it, and at least `Th.MinDarks`. The stack is counted without dithering, the worst case:
   the master is subtracted from every light alike, so its noise stays whole while the lights'
   own falls with their number; the row's note says that with dithering it is less. Without
   lights the rule of before stands: the master's noise under the value step.
3. **The noise of a light is the noise against the neighbours**, in check 9 only. The ADU scale
   section and the leak rows keep `StackNoise`, on the sky sigma (ADR 0016), as they are: a task
   of its own.
4. **The limit is a choice of the window: 5 % or 10 %, 10 % the default** (`Th.StackNoiseLimits`,
   R-123), a dropdown above the table. The choice judges check 9 again from the figures the run
   has (`DarkSeriesRun.WithStackLimit`) and is not kept between analyses or starts. The console
   judges at 10 %; its `ajung de la` row gives the frames for both limits, so it has no new
   option.
5. **The measured rule becomes one sentence.** `zgomot dupa medie` prints the last measured point
   beside what a single frame's noise gave to expect, and loses its table; the window's noise
   chart keeps every point.
6. **The choice travels as data**, like the table and the bold parts (ADR 0016, ADR 0041):
   `ReportRow.Choice` holds the core's label, options and the one in force; the window shows them
   and hands the pick back.

7. **The stack's figure is said against an ideal dark master.** The owner read the first words in
   the window on 2026-11-13 (`stack-ul poate fi mai zgomotos cu cel mult 5 %`, `masterul face
   stack-ul cu 3.1 % mai zgomotos`) and asked "against what? against now?"; a second wording,
   `masterul adauga zgomot`, he refused: the master does not add noise, it helps. The words are
   his: the dropdown, the `ajung de la` row and the verdict say `fata de un master dark ideal` /
   `decat cu un master dark ideal`, the table's note says once what ideal means (of very many
   frames, with no noise of its own), and the column about the master's own noise reads `fata de
   masterul de acum`, so the two comparisons cannot be taken for one another.

## Alternatives rejected

- **The sky sigma for the stack**, the figure `StackNoise` has: it reads 10 darks as enough for
  every cold set (4.5 % at 10 darks on the Veil pair) because it counts the sky's own structure
  as noise the master cannot spoil.
- **A table against the master of now alone**: it needs no lights, and does not say what the
  frames cost in the image. Both columns are shown.
- **5 % as the default**: 22 darks on two of the three pairs and 80 on the third; the owner kept
  10 % and asked for the choice.
- **A console option for the limit**: one more option to document and test for a figure the
  `ajung de la` row already prints.
- **Reading the frames again on a choice** (the way of ADR 0048): the limit changes no measured
  figure, and a run takes minutes.
- **Keeping the measured-against-expected table** beside the new one: the owner could not tell
  from it whether the darks were good, which is what the check is asked.

## Consequences

- `Thresholds.cs`: `StackNoiseLimit`, `StackNoiseLimits`, `FramesTableStep`, `FramesTablePast`.
  `DarkVerdicts.cs`: `FramesPoint`, `FramesRating`, `LimitFrames`, `StackLimit`, the points and
  the stack on `EnoughVerdict`, `DarkVerdictAnalyzer.EnoughOf`, which the run calls again once
  the lights are measured. `LightsAnalyzer.NoiseOnPlane` is the loop R-067's traces had.
  `ReportRows.cs`: `ReportChoice`, `ReportRow.Choice`. The window: `ChoiceVm`, the `ReportTable`
  template at five columns, the dropdown in the `ReportRow` template.
- With lights a verdict can change both ways: a noisy master under few lights reads `OK` where
  the value step read `ATENTIE`, and a quiet one under many lights reads `ATENTIE` where it read
  `OK`. On the pairs measured, the 30 darks of the 15.8 °C library with the 87 Veil lights of
  2026-10-06 read `ATENTIE` (12.8 %, 39 needed) where they read `OK`.
- The report holds two noises of a lights stack until the scale section is looked at: check 9's,
  on the noise against the neighbours, and `zgomot in master`'s, on the sky sigma, 1.6 to 2.8
  times larger on the three pairs.
- Check 9 counts the frames read (`DarkSeriesAnalysis.Used`), as it did. Above the frame cap
  those are every k-th of the folder's, so the row marked `(acum)` and the frames needed are of
  the frames read, and the verdict then leaves out how many could be dropped (the review of
  2026-11-13). A folder of more darks than the cap is judged as the cap's worth of them.
- R-024 is rewritten and R-123 is new; the ten goldens change in check 9's rows alone.
