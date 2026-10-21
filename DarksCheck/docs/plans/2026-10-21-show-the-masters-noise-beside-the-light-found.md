# 2026-10-21 Show the masters' noise beside every light found, in bold

## Context

The owner's reference for "does 0.8 ADU matter" is the noise left in the masters: the dark
master's per-pixel noise (check 8 prints it, 1.43 ADU on the leak set) and the lights stack's
(only the scale section's `zgomotul stack-ului` estimates it, sigma / sqrt(N), 3.3 ADU at
Smida). They are printed far apart from the light check 2 finds, and never as a ratio. He asked
for the two noises beside each light found, the ratio stated, and both the masters' noise and
the leak figures shown in bold in the window.

Outcome: a row `zgomot in master` in the scale section; the ratio of every light found to the
dark master's noise (and to the stack's when there are lights) in check 2's rows and verdict
and in the scale's `gasit`; the window shows those figures in bold; the console, plain text,
shows the same words without emphasis.

## Decisions (ADR 0016, `proposed`; the approval of this plan is the owner's yes)

1. **The emphasis travels as data, not as markup.** `ReportRow` gains `Bold`, a list of the
   value's substrings the window renders bold (empty by default); `RowVm` carries it; an
   attached property in `Presentation/` (`Emphasis.Text` + `Emphasis.Bold`) rebuilds a
   `TextBlock`'s inlines from `Rest` with bold runs for each occurrence. The console prints
   `Value` as it is. Rejected: markers in the text (`**...**`), which the console would have to
   strip and the goldens would carry.
2. **The stack's noise is one figure, computed once, in `Analysis`**: `StackNoise.Of(lights,
   rp)` gives the lights' sky median, sky sigma, count, the mean stack's noise
   (sigma / sqrt(N)) and the median stack's (x `Th.MedianNoiseFactor`), used by `DarkReport`
   and `ScaleReport` (the scale's private `Figures` takes it). The dark master's noise is
   `Noise.MasterNoise`, already there.
3. **Where the ratios go.** Check 2's `lumina constanta` row ends with `; +0.79 ADU e 0.55 din
   zgomotul pe pixel al masterului dark (1.43 ADU)[ si 0.24 din al stack-ului de lights
   (3.3 ADU)]`; the lit-frames row the same for the largest excess; the verdict's constant-light
   sentence carries the master-dark ratio. The scale's `gasit` carries both ratios; the new
   row `zgomot in master` reads `dark: 1.43 ADU pe pixel (mediana a 30 cadre; 0.36 ADU-eq);
   stack lights: 3.3 ADU pe pixel cu media, 4.1 cu mediana (32 lights)` or `...; stack lights:
   fara lights`. Check 8's `master` row is unchanged in words; its noise figure is bold.
4. **Bold**: the master noise figures (check 8 `master`, scale `zgomot in master`) and the
   light figures (the ADU-eq and raw ADU of the constant light, of the lit frames' maximum, in
   the check 2 rows, the verdict and the scale's `gasit`). The second paragraph of the scale
   explanation gets the sentence on the 50 x 50 patch (the ratio grows with the square root
   of the pixels averaged).

## Requirement drafts

- **R-021 / R-029 (amended):** the ratios in the rows and the verdict; the bold figures.
- **R-080 (amended):** the row `zgomot in master`, the ratios in `gasit`, the patch sentence.
- **R-071 (amended):** a row's `Bold` substrings are rendered bold in every tab that lists rows;
  the example names check 8's `master` row.
- **R-024 (amended, one clause):** the `master` row's noise figure is marked bold.

## Steps

0. Worktree `.claude/worktrees/Show-the-masters-noise-beside-the-light-found`, branch
   `claude/Show-the-masters-noise-beside-the-light-found`; the plan saved as
   `docs/plans/2026-10-21-show-the-masters-noise-beside-the-light-found.md`.
1. Requirements amended.
2. Failing tests: `tests/DarksCheck.Tests` (`R029_...` / `R080_...` extended: the ratios'
   words, the `Bold` lists of the rows, `StackNoise.Of` on the synthetic lights; a unit test of
   the emphasis splitter `Emphasis.Segments(text, bold)` lives in the Wpf tests since the
   class is the window's); `tests/DarksCheck.Wpf.Tests` (`R071_...` extended: `RowVm.Bold`
   equals the core's, a rendered `ReportRow` has a bold `Run`).
3. ADR 0016.
4. Core: `Analysis/StackNoise.cs` (new), `ReportRows.cs` (`Bold`), `DarkReport.cs` (the
   rows, the verdict, check 8's bold), `ScaleReport.cs` (the row, `gasit`, paragraph 2).
5. Window: `Presentation/Emphasis.cs` (attached property + `Segments`), `Theme/Rows.xaml`
   (the Rest `TextBlock` uses it), `MainViewModel.RowVm` (`Bold`), `Row()`.
6. Gate; goldens (the new scale row and the ratio words in `lights.txt`; the others' rows
   are `NEMASURAT` or without lights), each line tied; the console on the leak set and Smida
   pasted; `/validate` not needed (no figure changes).
7. README, `docs/design/dark-analysis.md` (StackNoise, Bold), `docs/design/window.md`
   (Emphasis), CHANGELOG, statuses; `/wrap-up`; merge proposal.

## Verification

- The tests red at step 2, green at step 6; `scripts/verify.ps1` green.
- The leak set prints `lumina constanta ... ; +0.79 ADU e 0.55 din zgomotul pe pixel al
  masterului dark (1.43 ADU)` and `zgomot in master : dark: 1.43 ADU pe pixel ...; stack
  lights: fara lights`; Smida prints the stack figures (3.3 / 4.1 ADU, 32 lights).
- The window on the leak set: the figures named above in bold in Rezumat (checks 2 and 8) and
  in the scale tab.

## What deviated

- The scale section's `zgomotul stack-ului` row was not kept beside the new `zgomot in master`:
  the new row holds both masters (dark and stack, mean and median), so the old one would have
  repeated the stack's figure; R-080 and the goldens follow.
- The tests and the core were written in one pass again; the red state was the compile error
  on `ReportRow.Bold` and `StackNoise`; two analyzer rules (CA1861, CA1865) in the new tests
  were the only fixes after.
- The second paragraph of the scale explanation also names the dark master's own noise, not
  only the 50 x 50 patch, since that is the figure the owner compares with.

