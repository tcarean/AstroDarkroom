# Check 3 says which way the camera moved, and a series mostly off the plateau is REDO

Branch `claude/Redo-a-series-mostly-off-the-plateau`, worktree
`.claude/worktrees/Redo-a-series-mostly-off-the-plateau` (the whole `photo` repo; the project is
its `DarksCheck/` subfolder). Plan saved as `docs/plans/2026-11-07-redo-a-series-mostly-off-the-plateau.md`.

## Context

The owner's darks of 2026-11-07 (30 x 120 s ISO 1600, folder labelled 2 °C) cooled the whole
hour: the hot pixels fall from 1.34 of the master to 0.85, about 4 degrees; the fit puts the
plateau at 0.72 of the master and names 21 of 30 frames as more than 2 degrees from it (9 left,
83 % more noise). The master is only 1.5 degrees colder than the 7 °C library of the day before:
the camera came out warm and never reached the air. Check 3 said `WARNING` in yellow and the owner
could not tell from its rows whether the camera warmed or cooled:

- the `plateau` row says "the last frame is still -1.3 degrees from the plateau" while the table's
  `plateau` column says `+1.4` for the same frame (the row prints the degrees still to go, signed;
  the table prints the frame's degrees above the plateau);
- "cooling" appears once, inside the verdict; the `hot pixels` row speaks only of pixels that
  "rise with the temperature" and the chart's note only explains a curve that climbs;
- nothing names the cause in plain words (the camera started warmer than the place it sat in);
- a series whose master is made mostly of frames off the plateau is compromised, and a yellow
  `WARNING` with "discard 21 frames" does not say so.

Measured on every real set (frames off the plateau / frames, noise the master would gain without
them): 15.8 °C 5/30 +10 %, 13.3 °C 4/30 +7 %, 9.5 °C 3/30 +5 %, 9.2 °C 12/30 +29 %, 7 °C 1/30
+2 %, leak set 8/30 +17 %, Smida 2/20 +5 %, the 2 °C set 21/30 +83 %.

The owner decided (2026-11-07, in chat): the three reworded texts below; the verdict is a red
`REDO` (`REFA`) when more than half the frames are off the plateau (the median master then
belongs to no temperature; the next set, 9.2 °C, has 40 %); the `hot pixels` row of check 3
goes, its first/last figures move into the `plateau` row.

## Changes

### Core: a fourth verdict level (`src/DarksCheck.Core`)

- `Analysis/MatchAnalyzer.cs`: `Verdict.Redo` ("the series is compromised: take it again") added
  to the enum. `InventoryReport`'s switches already have `default` arms.
- `Thresholds.cs`: `RedoOffPlateauFraction = 0.5` beside `OffPlateauCelsius`, with the measured
  table in its comment (the 2 °C set 0.70, the next 0.40).
- `Analysis/DarkVerdicts.cs`, `Judge`, step 3: `Verdict.Redo` when
  `wm.OffPlateauFrames.Count > Th.RedoOffPlateauFraction * withRatio.Length` (a fit exists);
  else as now. `DriftVerdict` gains `MostlyOff` (bool) so the report need not recompute.
- `Report/ReportRows.cs`: `Words` gains `"REFA"` and `"REDO"` (checked first, like the others as a
  whole word followed by `:` or a space).
- `Report/DarkReport.cs`:
  - `VerdictWord`: `Verdict.Redo => T("REDO", "REFA")`.
  - Section 3 rows become `plateau`, `to discard`, `verdict` (the `hot pixels` row and
    `Amplitude(...)` go; the `UNMEASURED: too few frames or no hot pixel` case moves to the
    `plateau` row when `FirstRatio` is NaN).
  - `PlateauRow`: the new words (below); `FromPlateau`-style words with above/below, no sign.
  - `Drift`: the new words; `REDO` branch; the `SettleCelsius` figure printed unsigned with
    above/below.
  - `WarmingChartNote`: the falling-curve clause.
- `Runs/`: nothing (the verdicts flow through the rows).

### Window (`src/DarksCheck.Wpf`)

- `Presentation/VerdictPalette.cs`, `Severity`: `REDO` / `REFA` → 3 (red, `Bad`), the summary
  comment updated. The chip, the card, the Rezumat take it through `ReportRow.Verdict`.
- Nothing else: the rows are the report's.

### The texts (every one a `T(en, ro)` pair; RO without diacritics; `RoCount` for 20 and up)

`plateau` row, cooling (warming: "warmed", "rise", "below", "under"):

- EN: `the camera cooled the whole series: the hot pixels fall from 1.34 of the master in the
  first frame to 0.85 in the last. Left to run, it would settle 2.8 degrees below the master (the
  plateau, at 0.72 of the master; two thirds of the way take 35 min); the last frame is still 1.3
  degrees above it`
- RO: `aparatul s-a racit pe toata seria: pixelii fierbinti scad de la 1.34 din master in primul
  cadru la 0.85 in ultimul. Daca ar continua, s-ar opri cu 2.8 grade sub master (platoul, la 0.72
  din master; doua treimi din drum iau 35 min); ultimul cadru e inca cu 1.3 grade deasupra lui`
- the flat case keeps its words but opens with the figures: `the camera did not warm: the hot
  pixels stay at 1.00 of the master from the first frame (0.99) to the last (1.01), and the first
  frame is less than 1.0 degrees from the plateau`; the grid-top case keeps its clause in place of
  "two thirds of the way take N min"; `UNMEASURED` cases unchanged.

`verdict` row:

- OK (cooling): `OK: the series settled (the last frame is 0.2 degrees above the plateau, under
  1.0) and every frame is close enough to the master for Siril to match it with -opt (between 0.7
  and 1.4); the cooling of 3.4 degrees over 58 min is at the start (...rule...); no frame to
  discard` (RO: `ultimul cadru e cu 0.2 grade deasupra platoului`; warming: `below` / `sub`).
- WARNING, not settled: `WARNING: the camera was still cooling at the last frame (1.3 degrees
  above the plateau, more than 1.0): it started warmer than the place it was in; the master mixes
  different temperatures; continue the series or discard the frames in the row above`. Warming:
  `the camera was still warming at the last frame (1.5 degrees below the plateau, more than 1.0):
  the master mixes different temperatures; continue the series ...` (no cause clause: every series
  self-warms, ADR 0017). RO: `ATENTIE: aparatul inca se racea la ultimul cadru (cu 1.3 grade
  deasupra platoului, peste 1.0): a pornit mai cald decat locul in care a stat; masterul amesteca
  temperaturi diferite; continua seria sau arunca cadrele din randul de mai sus`.
- REDO (mostly off; the not-settled clause first when it holds, then the -opt clause as now):
  `REDO: the camera was still cooling at the last frame (1.3 degrees above the plateau, more than
  1.0): it started warmer than the place it was in; 21 of 30 frames are more than 2.0 degrees from
  the plateau, so the master belongs to no temperature; take the series again after the camera has
  sat where it shoots for about 105 min (three times the 35 min of this series)`. Warming:
  `...; take a longer series: the first frames are the warm-up`. RO: `REFA: aparatul inca se racea
  la ultimul cadru (...): a pornit mai cald decat locul in care a stat; 21 din 30 de cadre sunt la
  peste 2.0 grade de platou, deci masterul nu apartine niciunei temperaturi; refa seria dupa ce
  aparatul a stat vreo 105 min in locul unde se trag dark-urile (de trei ori cele 35 min ale
  seriei)` / `...; refa seria mai lunga: primele cadre sunt incalzirea`. The 105 min is
  3 x `TauMinutes` rounded to 5 (a time constant in frames, no stamps: `about 3 x N frames`).

Chart note: `... when the curve climbs, the camera warms during the series; when it falls, the
camera cools (check 3).` / `... cand curba urca, aparatul se incalzeste in timpul seriei; cand
coboara, se raceste (verificarea 3).`

### Tests (requirement first, then the test, then the code)

- `tests/DarksCheck.Tests/DarkSeriesTests.cs`: `R023_...` row list becomes
  `["platou", "de aruncat", "verdict"]`, the new `plateau` opening; a new
  `R023_ASeriesMostlyOffThePlateauIsRedo` (twelve synthetic darks cooling as
  `0.7 + 0.6 x exp(-i / 6)` over 12 frames: most frames off a plateau near 0.7 → `Verdict.Redo`,
  the REFA words, the 3 x tau advice, the warming variant with the "longer series" advice); the
  not-settled test asserts `aparatul inca se incalzeste ... sub platou`; the English words through
  the existing `--lang en` theories (line ~851).
- `tests/DarksCheck.Tests/EnglishGoldenOutputTests.cs` `RomanianWords` and
  `tests/DarksCheck.Wpf.Tests/LanguageWindowTests.cs` regex gain `REFA`.
- `tests/DarksCheck.Wpf.Tests/PresentationTests.cs`: `REDO` and `REFA` are `Bad`; the
  "four distinct colours" test takes the new word. `MainViewModelTests` R071: a REDO chip on the
  Rezumat card of a mostly-off series.
- Goldens `darks.txt` / `en_darks.txt`: the `pixeli fierbinti` / `hot pixels` line of section 3
  goes, the `platou` row carries the UNMEASURED words; regenerated, every changed line read and
  tied to this change at wrap-up.
- `ValidationTests`: V-011, the 2 °C set, kind `darks`, recorded from the first run in the
  worktree (figures pasted), `drift: "Redo"`, index 28; V-003/V-004/V-008/V-009/V-010 unchanged
  (all under half; the settle figures keep their sign in the JSON).

### Docs

- `docs/requirements.md`: R-023 rewritten (rows, words, the REDO rule and `Th.RedoOffPlateauFraction`,
  the signless above/below wording, the examples); R-025 and R-071 mention `REFA` among the
  verdict words; R-082's check 3 row list.
- `docs/decisions/0038-a-series-mostly-off-the-plateau-is-refa-and-check-3-says-which-way-the-camera-moved.md`:
  supersedes 0017 in part (the verdict levels of check 3 and the row set), with the measured table
  and the alternatives rejected (fewer than `Th.MinDarks` left; noise factor over 1.5).
- `docs/design/dark-analysis.md` "The verdicts" and "The report as rows" (the four words);
  `docs/design/window.md` (the chip's four severities).
- `docs/validation.md` + `.json`: V-011 and the history line.
- `README.md`: the verdict words (lines ~112, 122, 142) gain `REDO`; section 3 (lines 160-234):
  the sample rows and the verdict paragraph.
- `CHANGELOG.md` under Unreleased: Changed (check 3's words and REDO), Removed (the hot pixels
  row).

## Verification

- `scripts/verify.ps1` green in the worktree (gate); build once with
  `--no-incremental -warnaserror` before the final verify.
- `DARKSCHECK_UPDATE_GOLDEN=1 dotnet test ... --filter GoldenOutputTests`, then read the diff.
- `/validate` with `DARKSCHECK_TESTDATA_LARGE`: V-003..V-011 pass; the drift table pasted at
  wrap-up (a formula change: `Thresholds.cs` gains a constant).
- The console on the 2 °C set reads `REFA` / `REDO` with the 105 min advice; the 9.2 °C set stays
  `OK`; a window run on the 2 °C set shows the red chip (off-screen harness, no input).
