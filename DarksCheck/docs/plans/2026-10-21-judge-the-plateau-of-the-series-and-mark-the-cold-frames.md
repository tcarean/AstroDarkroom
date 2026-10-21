# 2026-10-21 Judge check 3 by the plateau of the series and mark the cold frames

## Context

Check 3 (R-023) reads the warming of a dark series from its hot pixels, as the ratio of the
first frame to the last, and flags `ATENTIE` over `Th.DriftCelsius` = 1 °C. Measured today on
every real series (Smida darks, the three libraries, the leak set, and the two libraries with
their ten discarded frames put back): the ratio follows an exponential approach to a plateau,
time constant 14.5 min outdoors (Smida) and 18 .. 28.5 min indoors, the sensor starting
10 °C under its running plateau when cold; every series moves 2.2 .. 3.6 °C first to last, so
no series of this camera can pass the 1 °C test, and the verdict carries no information. What
matters for the master is different: whether the series has settled (the master then belongs
to a temperature), how far under the plateau the master sits (0.8 .. 1.5 °C on all series),
and which frames are still in the warm-up. The owner asked for check 3 to judge the plateau,
and for the Cadre tab to show plainly which frames should be discarded and why: the value,
the limit it is under, and an explanation. Discarding is measured against the plateau (the
owner's choice of 2026-10-21): 10 .. 18 of 30 frames on the present libraries, 6 of 20 at
Smida.

The frames of a session are never moved by the tool (ADR 0002): the report names them, the
owner moves them to `aruncate` by hand.

## Decisions (one ADR, `proposed`; the approval of this plan is the owner's yes)

1. **The model.** `ratio(t) = plateau - drop * exp(-t / tau)`, t in minutes from the first
   frame with a ratio (the frame index when a timestamp is missing); for each `tau` on a grid
   from `span / 50` to `3 * span` (step `span / 200`, span = the series' minutes) the linear
   least squares of `plateau` and `drop`; the best `tau` by residual sum of squares. Needs at
   least `Th.WarmingFitMinFrames` = 6 frames with a ratio (the same count as
   `Th.LeakFitMinFrames`, named apart), else the plateau figures are unmeasured. The fit lives in
   `DarkSeriesAnalyzer.WarmingOf` (`Analysis/`), the record `Warming` gains `Plateau`,
   `TauMinutes`, `FitScale` (rms residual), `PlateauCelsius` (= `D * log2(1 / plateau)`, the
   master's offset under the plateau, negative when under), `SettleCelsius` (the change over
   the last third of the frames, `D * log2(last / first of the third)`), `ColdFrames` (the
   indexes of the frames whose ratio is under `plateau * 2^(-Th.ColdFrameCelsius / D)`),
   `ColdLimitRatio` (that bound), `MasterWithoutColdCelsius` (the median ratio of the frames
   left, as degrees from the plateau).
2. **Three thresholds, all in `Thresholds.cs`.** `Th.ColdFrameCelsius` = 1.0: a frame more
   than this under the plateau is one to discard. `Th.SettledCelsius` = 1.0: a series whose
   last third moves by more than this has not settled. `Th.WarmingFitMinFrames` = 6.
   `Th.DriftCelsius` goes (its only use was the old verdict). The span check reuses
   `Th.MatchOptLow` / `Th.MatchOptHigh` (0.7 .. 1.4 of the master), the `-opt` margin, with
   no new number. Measured: the last third moved +0.15 .. +0.47 °C on the seven series
   (settled); a 20-frame series from cold would move about +1.5 °C over its last seven frames
   (unsettled); the cold frames per series as in Context.
3. **The verdict of check 3.** `NEMASURAT` as now (under two frames or no hot pixel);
   `ATENTIE` when the series has not settled (`|SettleCelsius| > Th.SettledCelsius`), or when a
   frame's ratio is outside `0.7 .. 1.4` of the master; `OK` otherwise, with the warm-up stated
   as information. Under `Th.WarmingFitMinFrames` frames there is no fit: the settle test is
   skipped and the verdict rests on the span alone (the goldens' four- and six-frame sessions
   read `OK`). The first-to-last degrees stay in the `amplitudine` row as a figure.
4. **The rows of check 3** (`DarkReport`, Romanian, wording final at implementation):
   - `amplitudine`: unchanged (`pixelii fierbinti la 0.73 din master in primul cadru, 1.09 in
     ultimul (3933 pixeli peste 20 ADU-eq)`).
   - `platou`: `seria se apropie de un platou la 1.10 din master (constanta de timp 18 min);
     masterul, mediana seriei, e cu 0.8 grade sub platou; ultima treime (10 cadre) mai urca
     0.15 grade` / `seria e plata: platoul la 1.00 din master, primul cadru la mai putin de
     1.0 grade de el` (when the first frame's offset to the plateau is under
     `Th.ColdFrameCelsius`) / `NEMASURAT: sub 6 cadre cu pixeli fierbinti`.
   - `de aruncat`: `11 cadre (1 .. 11) cu peste 1.0 grade sub platou (amplit. sub 0.98 =
     platoul 1.10 x 0.89); fara ele masterul ar fi cu 0.4 grade sub platou` / `niciun cadru cu
     peste 1.0 grade sub platou (amplit. sub 0.98)` / `NEMASURAT` with the fit. The frame list
     is printed as ranges (`1 .. 11`, `1 .. 3, 7`), by the existing range formatter if the
     report has one, else a small one in `RunReport`.
   - `verdict`: `OK: seria s-a asezat (ultima treime urca 0.15 grade, sub 1.0) si toate
     cadrele stau in 0.7 .. 1.4 din master; incalzirea de 3.4 grade in 58 min e in fata
     (regula dublarii la 6 grade), cadrele reci sunt in randul de mai sus` /
     `ATENTIE: seria inca se incalzeste la sfarsit (ultima treime urca 1.5 grade, peste 1.0):
     masterul nu apartine unei temperaturi; continua seria sau arunca cadrele reci` /
     `ATENTIE: 8 cadre ies din 0.7 .. 1.4 din master (cel mai rece 0.38): seria a pornit
     rece; arunca cadrele din randul de mai sus` (both reasons when both hold, joined by `; `)
     / with no fit: `OK: toate cadrele stau in 0.7 .. 1.4 din master (sub 6 cadre, fara platou)`.
5. **The frames table** (`DarkTableRow`, `DarkReport.FrameRows`, R-025): a column `platou`
   after `amplit.`: the frame's degrees from the plateau (`-3.5`, `-0.2`, `-` without a fit),
   and a `Note` on the row (null for most rows), which the console prints after the last column
   and the window shows in the last column in the attention colour: `de aruncat: amplit. 0.735
   sub 0.98 (1.0 grade sub platoul 1.10)`. The table's unit note gains `platou = grade fata de
   platoul seriei, sub -1.0 = de aruncat (verificarea 3)`.
6. **The window** (R-071, `FramesView`, `MainViewModel`): `FrameRowVm` gains `Plateau`,
   `Cold` and `Note`; a cold row has its `platou` cell and the note in the attention colour
   and a faint attention background on the row (a `DataTrigger` on `Cold`), the note's full
   text as the row's tooltip; the `platou` header's hint says what the column is. The warming
   chart colours the cold frames' points `VerdictPalette.Warn` and adds two guides: the plateau
   (`platoul 1.10`, neutral) and the cold limit (`0.98 de aruncat`, warn), beside the existing
   guide at 1; the card's sentence names the plateau and the limit. Nothing computed in the
   window: every figure and word is the core's.
7. **Validation.** `docs/validation.json` gains `plateau`, `tauMinutes`, `settleCelsius`,
   `coldFrames` (count) per darks case, compared at 0.01, 1 min, 0.1 °C, exact; the `drift`
   verdict of V-003, V-004, V-008, V-009, V-010 changes from `Warning` to `Ok` (the intended
   change, the owner's; a figure that moves only the owner's way). `/validate` runs at the end
   and the drift table goes in the wrap-up.

## Requirement drafts

- **R-023 (rewritten):** the fit, the plateau figures, the cold frames, the three thresholds,
  the verdict's three outcomes, the examples (synthetic: eight frames climbing 0.8 → 1.2 as
  `0.8 + 0.4 (1 - exp(-i / 2))`-like, which read a plateau near 1.2 and cold frames 1 .. k;
  the library 0.73 → 1.09, plateau 1.10, tau 18 min, 11 cold frames, `OK`).
- **R-025 (amended):** the `platou` column and the row note in `TABEL PE CADRE`.
- **R-071 (amended):** the Cadre tab's cold rows, the note column, the chart's two guides and
  the warn-coloured points.

## Steps

0. Worktree `.claude/worktrees/Judge-the-plateau-of-the-series-and-mark-the-cold-frames`,
   branch `claude/Judge-the-plateau-of-the-series-and-mark-the-cold-frames` from `main`; this
   plan saved as `docs/plans/2026-10-21-judge-the-plateau-of-the-series-and-mark-the-cold-frames.md`.
1. `docs/requirements.md`: R-023 rewritten, R-025 and R-071 amended.
2. Failing tests first:
   - `tests/DarksCheck.Tests/DarkSeriesTests.cs`: `R023_...` rewritten and split: the fit on a
     synthetic exponential series (plateau, tau, cold frames, settle), a flat series (no cold
     frame, `OK`), a series cut mid-warm-up (`ATENTIE` unsettled), a series from cold with
     frames under 0.7 (`ATENTIE` span), five frames (`NEMASURAT` plateau, verdict on the span);
     the table's `platou` cells and notes (`R025_...`); the report rows' words.
   - `tests/DarksCheck.Wpf.Tests/MainViewModelTests.cs`: `R071_...` the `FrameRowVm` cold
     flag, note and plateau cell equal the core's; the warming guides and point colours.
   - `tests/DarksCheck.Tests/ValidationTests.cs`: the four new fields read and compared.
3. Code: `Thresholds.cs`; `DarkSeriesAnalyzer.WarmingOf` + `Warming`; `DarkVerdicts`
   (`DriftVerdict` carries the new figures); `DarkReport` (rows, table column, note, unit
   note); `ReportRows.DarkTableRow`; `MainViewModel` (`FrameRowVm`, chart); `FramesView.xaml`
   (column, triggers, card text); the console's table line in `DarkReport` (the width of the
   header).
4. Goldens: `DARKSCHECK_UPDATE_GOLDEN=1 dotnet test --filter GoldenOutputTests`; read the whole
   diff; every changed line tied to the new rows, the new column and the verdict's words
   (`darks.txt`: 4 frames, no fit, `OK` on the span; `lights.txt` and `lights-only.txt`: flat
   series). Reported at wrap-up.
5. Docs: ADR 0017 `Check 3 judges the plateau of the series, not the first frame against the
   last` (+ a dated note on ADR 0007 where it states the 1 °C drift); `docs/design/dark-analysis.md`
   (the warming section, the verdict paragraph, the table) and `docs/design/window.md` (the Cadre
   tab); `README.md` (the check 3 sample and its sentence, Romanian); `CHANGELOG.md` under
   `[Unreleased]` (Changed); `docs/validation.md` cases V-003, V-004, V-008, V-009, V-010 and
   `docs/validation.json`.
6. `/validate` on the real sessions; the drift table pasted at wrap-up. `scripts/verify.ps1`
   green; commit on the task branch; the merge proposed.

## Verification

- `scripts/verify.ps1` green (build, format, tests, design-note index, kit drift).
- `dotnet test DarksCheck.sln -c Release --filter "Category=Validation"` with
  `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: the five darks cases pass on the new
  fields and the `Ok` drift verdicts.
- `darkscheck "E:\Photografie\Astro\Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius-idx67"`
  prints check 3 with `platou`, `de aruncat: 11 cadre (1 .. 11) ...` and `OK`, and the table
  marks frames 1 .. 11; the same folder in `DarksCheck.Wpf.exe` shows the eleven rows in the
  attention colour with the note, and the chart with the two guides (checked by eye, and by the
  smoke render test `tab-frames`).

## What deviated

- The settle figure is the degrees left between the fitted last frame and the plateau, not
  the change over the measured last third: on seven frames the last third is one step and
  understates a slow climb (ADR 0017 lists it among the alternatives).
- The frames to discard are measured on both sides of the plateau (`OffPlateauFrames`, the
  bounds `OffPlateauLow` / `OffPlateauHigh`), so a series that cools reads the same way; the
  names say "off the plateau" rather than "cold".
- The time constant is printed in frames when a timestamp is missing (`TimeFromStamps`), and
  as "at least" when it sits at the top of the grid (a series too short to measure it).
- Two existing assertions of R-028 and R-029 on the table header gained the `platou` column.
