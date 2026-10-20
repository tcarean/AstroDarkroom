# 2026-10-20 Explain the ADU scale on the open set: a tab and a report section

## Context

The owner asked how 0.20 ADU-eq of constant light can matter and what the ADU scale is. The
answer is a short table of magnitudes computed on the open set (the file's range, the
normalisation, the quantisation step, the read noise, the dark current, the thresholds in raw
ADU, what was found, the sky and the stack noise when there are lights, the saturation) and
four short paragraphs (ADU and ADU-eq; why an offset in the master is not noise and does not
average out; how 1 ADU compares to the sky and to the stack's noise; what each threshold means,
"measured" against "visible"). He wants it inside the application as a small help, with the
table computed on the given set, in a tab named "Explicatii scara ADU", and the electrons per
ADU from a good source.

The source found: photonstophotos.net (Bill Claff), "DxOMark Derived Sensor Characteristics"
and "DxOMark Derived Gain versus Measured ISO", read on 2026-10-20 from the pages' own data:
Nikon D3300, 12-bit: full well 34,446 e-, read noise 2.0 e-, QE 56 %, unity gain at measured
ISO 841; gain per nominal ISO 100 .. 25600 = 2^y with y = 3.136, 2.096, 1.048, 0.074, -0.928,
-1.921, -2.942, -3.893, -4.922, that is 8.8, 4.3, 2.1, 1.05, 0.53, 0.26, 0.13, 0.067, 0.033
e-/ADU. The site's own caveat: the method derives the gain from DxOMark's SNR curves and
overestimates it on small, noisy sensors; it is a published, derived figure, not one the tool
measures. It agrees with the owner's biases: 3.0 ADU of read noise at ISO 1600 x 0.53 = 1.6 e-.

Outcome: the console prints a section `EXPLICATII SCARA ADU` at the end of the report (so the
saved `darkscheck-raport.txt` has it) and the window gets the tab `Explicatii scara ADU`
between Index and Raport; both show the same rows and paragraphs, computed in the core.

## Decisions (ADR 0015, `proposed`; the approval of this plan is the owner's yes)

1. **One source of words, in the core.** `Report/ScaleReport.cs` builds the rows
   (`ReportRow` label / value, Romanian) and the paragraphs from `DarkSeriesResult`; the
   console writes them, the window binds them. Nothing is computed in the window (the rule
   "Computed only in the core").
2. **The camera's published figures live in `Analysis/CameraFacts.cs`**: for `Nikon D3300`
   the gain per nominal ISO, the full well, the read noise, the QE, the unity ISO, with the
   source, the date read and the caveat in the summary; a camera not in the table reads
   `necunoscut` on the electrons rows. The gain at the set's ISO is interpolated on the log2
   scale between the listed stops. The figure is always printed with its provenance
   (`photonstophotos.net din curbele DxOMark`), never as a measurement.
3. **Where.** Console: after `TABEL PE LIGHTS` (or `TABEL PE CADRE` without lights), the
   block `  EXPLICATII SCARA ADU`, the rows as `    label : value`, then the paragraphs
   wrapped at 96 columns with a four-space indent. Window: the eighth tab, between Index and
   Raport, a card with the rows (the Rezumat's row style) and the paragraphs under it.
4. **Rows** (each from figures the result already holds; `-` or `NEMASURAT` when absent):
   `fisier` (bits from the white level, black, white, usable signal), `1 ADU-eq` (the
   normalisation's raw ADU at the set's ISO and exposure), `cuantizare` (the step),
   `electroni` (gain at the set's ISO from CameraFacts, 1 ADU-eq in electrons, the full well,
   the source), `zgomot de citire` (per pixel on the reference plane, ADU and e-),
   `zgomot pe cadru` (the per-frame sigma and the dark current's median when there are
   biases, ADU and ADU-eq), `praguri pe acest set` (hot pixel 20 ADU-eq, map leak 1 ADU-eq,
   constant light 0.15 ADU-eq, each in raw ADU), `gasit` (the constant light and the lit
   frames of check 2, in ADU-eq, raw ADU and electrons, or `nimic peste praguri`), `cer` (the
   lights' sky median above black and sigma on the reference plane, ADU, or `fara lights`),
   `zgomotul stack-ului` (the lights' sky sigma / sqrt(N) on the reference plane, against an
   offset that stays whole), `saturatie` (the white level, the full well).
5. **Paragraphs**, four, with the set's numbers filled in (the sky and stack lines only with
   lights): ADU / ADU-eq; offset against noise; 1 ADU against the sky and the stack; the
   thresholds, measured against visible.

## Requirement drafts

- **R-080 (new, console contract block):** the section, its rows and paragraphs, the
  provenance words, the `necunoscut` case, the wrapping. Example: the leak set reads
  `electroni           : 1 ADU = 0.53 e- la ISO 1600 (Nikon D3300, photonstophotos.net din
  curbele DxOMark, 2026-10-20); 1 ADU-eq = 4.01 ADU = 2.1 e-` and `gasit : lumina constanta
  +0.20 ADU-eq pe latura de sus = 0.80 ADU = 0.42 e- pe pixel; ...`; a FITS session of an
  unknown camera reads `electroni : necunoscut pentru <camera>`.
- **R-078 (new, window block):** the tab `Explicatii scara ADU`, the rows and the
  paragraphs of R-080, nothing reworded; R-071's tab list becomes eight.

## Steps

0. Worktree `.claude/worktrees/Explain-the-adu-scale-on-the-open-set`, branch
   `claude/Explain-the-adu-scale-on-the-open-set`; the plan saved as
   `docs/plans/2026-10-20-explain-the-adu-scale-on-the-open-set.md`.
1. `docs/requirements.md`: R-080 (console block, heading says the block continues at R-080),
   R-078, R-071 amended (eight tabs).
2. Failing tests: `tests/DarksCheck.Tests` `R080_...` (rows on the synthetic session with and
   without biases and lights; the unknown camera; the D3300 gain by a FITS carrying the camera
   name, if `FitsTestData` can set `INSTRUME`, else by a unit call to `CameraFacts`);
   `tests/DarksCheck.Wpf.Tests` `R078_...` (the tab's rows equal the core's; the tab renders
   in `WindowSmokeTests`). Run, paste the red output.
3. `docs/decisions/0015-the-adu-scale-is-explained-on-the-open-set-with-the-cameras-published-gain.md`.
4. Core: `Analysis/CameraFacts.cs`, `Report/ScaleReport.cs`, `DarkReport.Write` writes the
   block; `Report/RunReport.Wrap` or a local wrapper for the paragraphs.
5. Window: `Views/ScaleView.xaml` (+ `.cs`), the tab in `MainWindow.xaml`, `ScaleRows` and
   `ScaleParagraphs` in `MainViewModel`, `WindowTexts` for the tab's title.
6. Gate; goldens regenerated (the appended block in `darks.txt`, `lights.txt`,
   `lights-only.txt`), each line tied; `/validate` is not needed (no figure or threshold
   changes) but the console is run on the leak set and Smida and the block pasted.
7. README (the tab and the section, Romanian), `docs/design/window.md` (the tab),
   `docs/design/dark-analysis.md` (ScaleReport, CameraFacts in Covers), CHANGELOG, statuses;
   `/wrap-up`; merge proposal.

## Verification

- `R080` and `R078` red at step 2, green at step 6; `scripts/verify.ps1` green.
- The console on the leak set prints the block with `1 ADU = 0.53 e- la ISO 1600`, the
  thresholds at 80 / 4.0 / 0.60 ADU, `gasit` with +0.20 ADU-eq = 0.80 ADU = 0.42 e-; Smida
  prints the sky (55 ADU) and the stack noise lines; a FITS session prints `necunoscut`.
- The window on the leak set: the tab shows the same rows and the four paragraphs.

## What deviated

- The tests and the core were written in one pass again (steps 2 and 4), so the red state was
  the compile error on the new types; the one assertion that then failed was the exposure's
  wording (`120.0 s`, as `InventoryReport.Exposure` prints it) and the console's `
` line
  endings, both test-side.
- The label `praguri pe acest set` became `praguri pe set` to fit the report's 19-character
  label column; the dark current's ADU-eq is shown only when the normalisation is not 1.
- No validation case was added: every row is arithmetic on figures the cases already pin, and
  the gain is a published figure recorded in ADR 0015, not a measurement.

