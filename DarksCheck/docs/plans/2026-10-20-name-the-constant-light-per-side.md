# 2026-10-20 Name the light that is in every frame, per side, and explain every column

## Context

The owner's leak set (eyepiece open, a lamp behind the camera for frames 1-25, then in front
of the capped lens for 26-30) read, after the merge of 40618d4: `ATENTIE: 5 cadre au lumina
peste ce da incalzirea seriei ... scoate-le din darks\`. The owner objected: every frame has a
leak, not five. He is right about the words. The per-frame test sees only what changes between
frames, so light present in all of them lands in the constant part of the split by
construction; the tool measured it and printed it (`constant +0.17, din care in bias -0.01,
ramane +0.18`) but never called it a leak, because the map's threshold is 1 ADU-eq and nothing
judges the part that remains after the bias. Measured on 2026-10-20 over all eight regions
(frames 1-25, dark-minus-bias constant, ADU-eq), grouped by side (an edge and its two corners):

| side | leak set | Smida (clean) |
| --- | --- | --- |
| top | +0.19 (top +0.22, TL +0.18, TR +0.18) | +0.05 |
| right | +0.11 | +0.08 |
| left | +0.02 | -0.02 |
| bottom | -0.05 | -0.01 |

The top side is the eyepiece band (0.75 ADU raw over the top third, the raised mirror's
shadow) in all 25 frames; Smida's largest side is 0.08. The last five frames' +1.9 ADU raw
everywhere are the front position (the cap or the mount passes more than the eyepiece), so
the lit-frame finding stands; only its sentence implied the rest were clean.

Outcome: with biases, check 2 judges the constant light per side and names it; the lit-frame
sentence says "in plus fata de restul seriei" and both findings are said when both hold; the
per-frame table shows the worst side's offset frame by frame; every column of the Cadre and
Lights tables in the window has a hover hint.

## Decisions (ADR 0014, `proposed`; the approval of this plan is the owner's yes)

1. **The constant light is read per side**, the mean of the remainders (constant minus the
   bias's offset) of an edge and its two corners, dark-minus-bias, in ADU-eq. A band along an
   edge, which is what a leak through the eyepiece or the mount gives, raises a whole side,
   and three regions average the extrapolation noise down (the single-region remainders on
   Smida reach 0.10, the sides 0.08). Rejected: the single verdict region (misses a band whose
   largest region is a corner the glow also lifts); all eight regions separately (noisier,
   Smida's TR at 0.10 against the leak set's 0.18).
2. **`Th.LeakConstantAdu` = 0.15 ADU-eq**, with biases only. Measured: Smida's largest side
   0.08, the leak set's top 0.19, so a margin of about 2x each way; thin, said in the ADR, to
   be re-read when a third session with biases exists. Without biases the sides are printed as
   constants and the row says they cannot be told from readout.
3. **Words.** The verdict goes `ATENTIE` on a side over the threshold:
   `lumina constanta in toate cadrele: +0.19 ADU-eq pe latura de sus fata de centru, nu e in
   bias: scurgere de lumina (ocular, montura)`; the lit-frame sentence becomes `N cadre au
   lumina in plus fata de restul seriei (...)` and keeps `scoate-le din darks\ sau refa seria`
   only when no side is over the threshold, else `refa seria` alone; when both hold the two
   are joined with `; in plus `. The limit is said in the row when nothing is found: light
   uniform over the frame and present in every frame has no reference without a calibrated
   dark current (on the leak set the dark-minus-bias median, 0.54 ADU-eq, is under the 0.66
   the index predicts from Smida, so there is none).
4. **The per-frame column `latura`**: the worst side's mean offset to the centre in that
   frame, minus the bias's offset of that side when there are biases, in ADU-eq; the note
   names the side (`latura = sus - centru dupa bias, ADU-eq`). `-` when unmeasured.
5. **Hover hints on every column** of the two tables (nr, fisier, ora, q25, q75 had none), as
   static Romanian text in the XAML headers; the `latura` hint names what it is.

## Requirement drafts

- **R-021 (amended):** `ATENTIE` also on R-029's constant light over `Th.LeakConstantAdu`;
  the words above; the lit-frame sentence `in plus fata de restul seriei`.
- **R-028 (amended):** `TABEL PE CADRE` gets `latura` after `lumina`; the header, the note.
- **R-029 (amended):** the four sides (mean of the edge and its two corners) of the constant
  part and, with biases, of the remainder; the row `lumina constanta`: with biases `dupa bias,
  fata de centru: sus +0.19, dreapta +0.11, stanga +0.02, jos -0.05 ADU-eq; peste 0.15 la sus`
  (or `; niciuna peste 0.15`), without biases `fara bias nu se desparte de offsetul de citire:
  constant sus +0.21, dreapta +0.13, stanga +0.09, jos +0.11 ADU-eq`; `NEMASURAT` with the
  split. Example: eight darks warming 0.8 → 1.2 whose top 64 rows carry +2 ADU with flat
  biases read a top side of 1.5 .. 2.5, `ATENTIE: lumina constanta ...`; the same band in the
  biases too reads a top side within ±0.5 and `OK`; the leak set reads top +0.19, Smida +0.05.
- **R-071 (amended):** every column header of the Cadre and Lights tables carries a hint.

## Steps

0. Worktree `git worktree add .claude/worktrees/Name-the-constant-light-per-side -b claude/Name-the-constant-light-per-side main`; the plan saved as `docs/plans/2026-10-20-name-the-constant-light-per-side.md`.
1. `docs/requirements.md`: R-021, R-028, R-029, R-071 amended.
2. Failing tests: `tests/DarksCheck.Tests/DarkSeriesTests.cs` (`R029_LightInEveryFrameIsReadPerSideAndTheBiasClearsAReadoutBand`: the band fixtures above, the verdict words, the `lumina constanta` row, the `latura` column and note; R028's sentence updated to `in plus fata de restul seriei`); `tests/DarksCheck.Wpf.Tests/WindowSmokeTests.cs` or `MainViewModelTests.cs`: every header `TextBlock` of `FramesView` and `LightsView` has a non-empty `ToolTip` (walk the rendered view's visual tree). Run, paste the red output.
3. `docs/decisions/0014-the-light-in-every-frame-is-read-per-side-after-the-bias.md`.
4. Core: `Analysis/LeakAnalyzer.cs`: `MapSide` enum, `SideLight(Side, ConstantAdu, RemainderAdu?)`, `SidesOf(splits)`, `WorstSide`, and the per-frame side offset helper; `Thresholds.cs`: `LeakConstantAdu`; `Analysis/DarkVerdicts.cs`: `LeakVerdict` gains `Sides` and `WorstSide`, `Judge` goes `Warning` on a side over the threshold (biases only); `Report/DarkReport.cs`: the row, the words, the `latura` column (`DarkTableRow` in `ReportRows.cs`, header, note).
5. Window: `FrameRowVm`, `FramesView.xaml` (column + hints), `LightsView.xaml` (hints).
6. `tests/DarksCheck.Tests/ValidationTests.cs`: optional keys `constantSide` (the worst side's name) and `constantSideAdu`; `docs/validation.json`: V-003 and V-008 (measured in this task), `docs/validation.md` rows and History.
7. Gate; goldens regenerated (`darks.txt`, `lights.txt`, `lights-only.txt`: the new row, the column, the sentence tail), each line tied; `/validate` with the drift table.
8. README (check 2's words, the table column, the hints), `docs/design/dark-analysis.md`, CHANGELOG, statuses; `/wrap-up`; merge proposal.

## Verification

- The R028/R029 tests red at step 2, green at step 7; `scripts/verify.ps1` green.
- The console on the leak set: `ATENTIE: 5 cadre au lumina in plus fata de restul seriei (...); in plus lumina constanta in toate cadrele: +0.19 ADU-eq pe latura de sus ...`, the `lumina constanta` row with the four sides, the `latura` column at about +0.4 in frames 1-25; Smida `OK` with sides under 0.15; the library and V-009/V-010 unchanged (no biases: the row shows constants only).
- `/validate`: V-001 to V-010 pass; V-003 and V-008 carry the new keys.
- The window on the leak set: hover on every header of Cadre and Lights shows a hint.

## What deviated

- The tests and the core were written in one pass (the plan's steps 2 and 4), so the red state
  was the compile error on the new types, not a failing assertion; the three failures that
  followed were the synthetic fixtures' noise, below.
- A second condition was added to the constant-light rule: the side's light must also exceed
  `Th.LeakConstantSigmas` (3) times the standard error of its intercept. The synthetic frames
  (256 x 192 px, regions of 256 pixels) put 0.1 .. 0.7 ADU of error on a side's constant where
  the D3300's regions put 0.01 .. 0.05, and the readout-band case flickered over 0.15 on noise
  alone; the band fixtures use noise 1 ADU instead of 3 for the same reason. The sides are
  fitted on their own per-frame offsets (the mean of the three regions) rather than averaged
  from the region splits, so the error is measured, not guessed.
- The leak set's top side reads +0.20 ± 0.05 ADU-eq by the direct fit (the plan's table, from
  the mean of the three region intercepts, said +0.19).
