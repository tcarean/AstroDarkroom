# Check 3 reads the series against its end; check 9 weighs what the mismatch leaves

## Context

On 2026-11-10 the owner asked whether the darks `2026-11-06-ISO1600-120s-minus2celsius-idx13`
must be thrown away for the Veil 2026-11-08 lights. DarksCheck said two things that the
measurement did not support:

- **Check 3** said `ATENTIE`: "the camera was still cooling at the last frame, 1.4 degrees above
  the plateau". The plateau was extrapolated from a series that moved 0.5 degrees in all; the
  camera never gets there. The owner's reading: what counts is where the series ended, not a
  virtual plateau.
- **Check 9** said `ATENTIE: subtract the dark with -opt` from the ratio 0.85 alone. On this cold
  set (183 hot pixels) a plain subtraction leaves, at an average hot pixel, 0.73 of the noise of
  one light; a pair the tool calls `OK` today (Veil 10-06, ratio 0.94, 758 hot pixels) leaves
  more large traces (19 over three times the noise, against 7).

Both rules were measured on the 11 real dark series and 4 sets of lights and put to the owner
with options; the owner chose the three recommendations in chat on 2026-11-10. This plan
implements them. Nothing changes in any index.

## The decided rules

### Check 3

- **The end of the series** is the average (median) of the last 5 frames' hot-pixel ratios. It is
  measured, not fitted. It needs at least 6 frames with a ratio, as the plateau did.
- **To discard**: the frames more than 2.0 degrees from the end of the series.
- **REFA**: more than half the frames with a ratio are to discard (the limit the owner chose on
  2026-11-07, now against the end).
- **ATENTIE**: the master is more than 1.0 degree from the end of the series, or a frame is
  outside 0.7 .. 1.4 of the master (unchanged). **OK** otherwise.
- The plateau leaves every text, row, column, chart line and verdict. The table column `platou`
  becomes `sfarsit` (`end`): the frame's degrees from the end of the series.
- The curve fit stays in the code for one thing only: the minutes in the REFA advice of a
  cooling series ("after the camera has sat where it shoots for about N min"). Without a fit, or
  with one that cannot tell the speed, the advice has no minutes. (The chart draws no curve
  today, only points and lines, so there is no curve to keep there.)

What it says on the real series (measured 2026-11-10 from the tool's own per-frame ratios):

| Series | Today | Master against the end | To discard | New verdict |
|---|---|---|---|---|
| -2 °C (idx13) | ATENTIE, 1 frame | 0.4 above | 0 | OK |
| 2.7-1.7 °C (idx18) | ATENTIE, all 40 | 0.6 above | 0 | OK |
| 4.4-2.7 °C (idx20) | OK, 0 | 0.4 above | 0 | OK |
| 7 °C (idx33) | OK, 1 | 0.4 below | 1 | OK |
| 9.5 °C (idx33) | OK, 3 | 0.7 below | 3 | OK |
| 9.2 °C (idx34) | ATENTIE, 12 | 1.2 below | 8 | ATENTIE |
| 13.3 °C (idx47) | OK, 4 | 0.7 below | 3 | OK |
| 15.8 °C (idx67) | OK, 5 | 0.7 below | 4 | OK |
| Smida | OK, 2 | 0.6 below | 2 | OK |
| Lightleaks | OK, 8 | 1.0 below (0.99) | 6 | OK, at the limit |
| `stricate`, 2 °C | REFA, 21 | 1.4 above | 10 | ATENTIE |

### Check 9

- For every hot pixel of the master the report already reads (above 20 ADU-eq, under 0.73 of
  white), **the trace** a plain subtraction leaves in a light is `|ratio - 1|` times the pixel's
  level in the master, divided by **the light's noise on that pixel's colour** (the `zgomot`
  figure of the lights table, per colour, the average over the lights).
- Three figures: the average trace over those pixels (as a part of the noise), how many pixels
  keep more than the noise, how many more than 3 times the noise.
- **Verdict**: the dark can be subtracted as it is when the ratio is within 0.9 .. 1.1 (as
  today), or when it is within 0.7 .. 1.4 and the average trace is under the noise of a light
  (new constant `1.0`). The traces only relax the verdict. The other verdicts (cosmetic only, do
  not match) and the partner's verdicts on the Index tab are unchanged.
- A new row, `ce ramane` (`what is left`), prints the three figures and says the reckoning holds
  for dithered lights.

Measured: Veil 11-08 against the -2 °C darks: ratio 0.85, average trace 0.73, 55 of 183 above the
noise, 7 above 3 times; today `-opt`, new `OK`. Veil 11-06 against the same darks: 1.06, 0.28,
7, 0. Veil 10-06 against the 7 °C darks: 0.94, 0.49, 97 of 758, 19. Smida: 1.00, 0.

## The texts, old and new

The Romanian is what the owner reads; the English pair is built the same way. Figures are those
of the -2 °C set with the Veil 11-08 lights unless another set is named.

### Check 3, row `platou` -> `sfarsitul seriei` (`end of series`)

A series that moved (the 15.8 °C library):

- Old: `aparatul s-a incalzit pe toata seria: pixelii fierbinti cresc de la 0.73 din master in primul cadru la 1.09 in ultimul. Daca ar continua, s-ar opri cu 0.8 grade peste master (platoul, la 1.10 din master; doua treimi din drum iau 18 min); ultimul cadru e cu 0.1 grade sub platou`
- New: `aparatul s-a incalzit in timpul seriei: pixelii fierbinti cresc de la 0.73 din master in primul cadru la 1.08 la sfarsitul seriei (media ultimelor 5 cadre), adica 3.4 grade in 58 min; masterul e cu 0.7 grade sub sfarsitul seriei`
- New, English: `the camera warmed during the series: the hot pixels rise from 0.73 of the master in the first frame to 1.08 at the end of the series (the average of the last 5 frames), that is 3.4 degrees in 58 min; the master is 0.7 degrees below the end of the series`

A series that stayed put (first frame under 1.0 degree from the end; the -2 °C set):

- Old (on the 4.4-2.7 °C set, the wording of today): `aparatul nu s-a incalzit: pixelii fierbinti au stat la 1.00 din master, de la primul cadru (0.93) la ultimul (0.96), iar primul cadru e la mai putin de 1.0 grade de platou`
- New: `aparatul a stat la aceeasi temperatura: pixelii fierbinti sunt la 0.97 din master in primul cadru si la 0.96 la sfarsitul seriei (media ultimelor 5 cadre), mai putin de 1.0 grade intre ele; masterul e cu 0.4 grade peste sfarsitul seriei`
- New, English: `the camera stayed at the same temperature: the hot pixels are at 0.97 of the master in the first frame and at 0.96 at the end of the series (the average of the last 5 frames), less than 1.0 degrees apart; the master is 0.4 degrees above the end of the series`

### Check 3, row `de aruncat`

- Old: `5 cadre (1 .. 5) sunt la mai mult de 2.0 grade de platou (pixelii fierbinti sub 0.87 sau peste 1.39 din master; platoul 1.10); fara ele masterul ar fi cu 0.4 grade sub platou, dar cu 10 % mai mult zgomot`
- New: `4 cadre (1 .. 4) sunt la mai mult de 2.0 grade de sfarsitul seriei (pixelii fierbinti sub 0.86 sau peste 1.36 din master; sfarsitul seriei 1.08); fara ele masterul ar fi cu 0.4 grade sub sfarsitul seriei, dar cu 7 % mai mult zgomot`
- Old, none: `niciun cadru nu e la mai mult de 2.0 grade de platou (pixelii fierbinti intre 0.80 si 1.27 din master)`
- New, none: `niciun cadru nu e la mai mult de 2.0 grade de sfarsitul seriei (pixelii fierbinti intre 0.76 si 1.21 din master)`
- English: the same sentences with `the end of the series` for `the plateau`.

### Check 3, row `verdict`

- Old OK: `OK: seria s-a stabilizat (ultimul cadru e cu 0.1 grade sub platou, mai putin de 1.0) si toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt (intre 0.7 si 1.4); incalzirea de 3.4 grade din 58 min e la inceput (gradele socotite cu regula: semnalul termic se dubleaza la fiecare 6 grade); cadrele de aruncat sunt in randul de mai sus`
- New OK: `OK: masterul e aproape de sfarsitul seriei (cu 0.7 grade sub el, mai putin de 1.0) si toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt (intre 0.7 si 1.4); gradele sunt socotite cu regula: semnalul termic se dubleaza la fiecare 6 grade; cadrele de aruncat sunt in randul de mai sus`
- New OK, English: `OK: the master is close to the end of the series (0.7 degrees below it, under 1.0) and every frame is close enough to the master for Siril to match it with -opt (between 0.7 and 1.4); the degrees are reckoned with the rule that the thermal signal doubles every 6 degrees; the frames to discard are in the row above`
- Old ATENTIE (the -2 °C set): `ATENTIE: aparatul inca se racea la ultimul cadru (cu 1.4 grade peste platou, mai mult de 1.0; platoul e ghicit din curba): a pornit mai cald decat locul in care a stat; masterul amesteca temperaturi diferite; continua seria sau arunca cadrele din randul de mai sus`
- New ATENTIE, warming (the 9.2 °C library): `ATENTIE: masterul e cu 1.2 grade sub sfarsitul seriei, mai mult de 1.0: cadrele de la inceput, mai reci, il trag in jos; arunca cadrele din randul de mai sus`
- New ATENTIE, cooling (the `stricate` set): `ATENTIE: masterul e cu 1.4 grade peste sfarsitul seriei, mai mult de 1.0: cadrele de la inceput, mai calde, il trag in sus (aparatul a pornit mai cald decat locul in care a stat); arunca cadrele din randul de mai sus`
- New ATENTIE with no frame to discard: the same, ending `; fa seria mai lunga` (`take a longer series`).
- English: `WARNING: the master is 1.2 degrees below the end of the series, more than 1.0: the frames from the start, colder, pull it down; discard the frames in the row above` / `... 1.4 degrees above ..., warmer, pull it up (the camera started warmer than the place it was in); ...`
- Old REFA: `REFA: aparatul inca se racea la ultimul cadru (cu 1.3 grade peste platou, mai mult de 1.0): a pornit mai cald decat locul in care a stat; 21 din 30 de cadre sunt la peste 2.0 grade de platou, deci masterul nu apartine niciunei temperaturi; refa seria dupa ce aparatul a stat vreo 105 min in locul unde se trag dark-urile (de trei ori cele 35 min ale seriei)`
- New REFA, cooling: `REFA: 8 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; aparatul a pornit mai cald decat locul in care a stat: refa seria dupa ce aparatul a stat vreo 105 min in locul unde se trag dark-urile (de trei ori cele 35 min ale seriei)`; without minutes: `...: refa seria dupa ce aparatul a stat mai mult in locul unde se trag dark-urile`
- New REFA, warming: `REFA: 8 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; refa seria mai lunga: primele cadre sunt incalzirea`
- The part about a frame outside 0.7 .. 1.4 is unchanged. Under 6 frames: `..., deci fara platou` becomes `..., deci sfarsitul seriei nu se masoara` (`so the end of the series is not measured`).

### Check 3, the table, the hint and the chart

- Column `platou` -> `sfarsit` (`plateau` -> `end`). Table note: `platou = cu cate grade e cadrul fata de temperatura stabila a seriei; de aruncat = la peste 2.0 grade de ea` -> `sfarsit = cu cate grade e cadrul fata de sfarsitul seriei; de aruncat = la peste 2.0 grade de el`
- Row note: `..., adica la peste 2.0 grade sub platou (1.10)` -> `..., adica la peste 2.0 grade sub sfarsitul seriei (1.08)`
- Column hint: `cu cate grade e cadrul fata de temperatura stabila a seriei (platoul), ...; un cadru la peste 2.0 grade de platou e de aruncat ...` -> `cu cate grade e cadrul fata de sfarsitul seriei (media ultimelor 5 cadre), dupa pixelii fierbinti; un cadru la peste 2.0 grade de el e de aruncat si are motivul pe randul de sub el (verificarea 3)`
- Chart note: `Temperatura stabila a seriei (platoul) e la 1.10 din master; cadrele sub 0.87 sau peste 1.39 (la peste 2.0 grade de platou) sunt de aruncat si apar in galben.` -> `Sfarsitul seriei (media ultimelor 5 cadre) e la 1.08 din master; cadrele sub 0.86 sau peste 1.36 (la peste 2.0 grade de el) sunt de aruncat si apar in galben.`; under 6 frames `nu se poate gasi platoul` -> `sfarsitul seriei nu se masoara`
- Chart line label: `platoul 1.10` -> `sfarsitul seriei 1.08` (`end of series 1.08`).

### Check 9

- New row `ce ramane` (`what is left`), after `lights fata de dark`. It says which way the
  subtraction errs: a ratio under 1 takes too much and leaves a hole, one over 1 too little and
  leaves a rest.
  - Under 1: `scaderea ia prea mult: la un pixel fierbinte ramane in medie o gaura de 0.73 din zgomotul unui light; la 55 din 183 gaura trece de zgomot, la 7 de 3 ori zgomotul (zgomotul light-ului pe culoarea pixelului; cu dithering urmele se imprastie, fara el raman in acelasi loc in stack)`
  - Over 1: `scaderea ia prea putin: la un pixel fierbinte ramane in medie un rest de 0.28 din zgomotul unui light; la 7 din 183 restul trece de zgomot, la 0 de 3 ori zgomotul (...)`
  - English: `the subtraction takes too much: a hot pixel keeps on average a hole of 0.73 of the noise of a light; at 55 of 183 the hole is over the noise, at 7 over 3 times the noise (the light's noise on the pixel's colour; with dithering the traces spread, without it they stay in the same place in the stack)` / `takes too little ... a rest of ...`
- Old verdict: `ATENTIE: scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); in lights pixelii fierbinti sunt de 0.85 ori cat in dark, intre 0.7 si 1.4: dark-ul e mai cald cu 1.4 grade`
- New verdict when the traces are small: `OK: dark-ul se poate scadea asa cum e; in lights pixelii fierbinti sunt de 0.85 ori cat in dark, in afara 0.9 .. 1.1, dar ce ramane dupa scadere e in medie sub zgomotul unui light (0.73 din el; pe aceste lights, asa e intre 0.80 si 1.20): dark-ul e mai cald cu 1.4 grade`
  English: `OK: the dark can be subtracted as it is; in the lights the hot pixels are 0.85 times what they are in the dark, outside 0.9 .. 1.1, but what the subtraction leaves is on average under the noise of a light (0.73 of it; on these lights that holds between 0.80 and 1.20): the dark is warmer by 1.4 degrees`
- New verdict when they are not: `ATENTIE: scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); in lights pixelii fierbinti sunt de 0.79 ori cat in dark, intre 0.7 si 1.4, iar ce ramane dupa scadere e in medie de 1.16 ori zgomotul unui light: dark-ul e mai cald cu 2.0 grade`
- The `OK` within 0.9 .. 1.1 and the other verdicts keep their words.

### Check 9, the chart of the Lights tab (a consequence of the rule, new here)

The average trace grows in step with `|ratio - 1|`, so the rule is the same as a wider green
band: 0.9 .. 1.1 stretched to where the average trace reaches the noise of a light, never past
0.7 .. 1.4 (0.80 .. 1.20 for the Veil 11-08 lights against the -2 °C darks). The chart follows
it, so a green verdict does not sit over yellow points:

- Each light's point is green inside that band (today: inside 0.9 .. 1.1).
- The two green lines move to the band's ends: `0.9 asa cum e` -> `0.80 asa cum e`.
- The text above the chart, old: `Verde intre 0.9 si 1.1: dark-ul se poate scadea asa cum e. Galben intre 0.7 si 1.4: scade dark-ul cu -opt (...)`
- New, when the band is wider: `Verde intre 0.80 si 1.20: dark-ul se poate scadea asa cum e (0.9 .. 1.1, largit pana unde ce ramane dupa scadere ajunge cat zgomotul unui light). Galben de acolo pana la 0.7 si 1.4: scade dark-ul cu -opt (...)`; when it is not wider the text stays as it is.

## Requirements (drafts, written to `docs/requirements.md` first)

- **R-023 rewritten**: the end of the series, the frames off it, the master's degrees from it,
  the three verdict rules above, the rows' words; the fit only for the REFA minutes. Example: the
  12-frame synthetic series `1.2 - 0.4 x exp(-i / 3)` and the real figures of the table above.
- **R-025 amended**: the table column `sfarsit`, its note and the `de aruncat` row notes.
- **R-071 amended** (the window's frames tab): the column, its hint, the chart's lines and note.
- **R-052 and R-062 amended**: check 9's verdict takes R-067's relaxation; the partner's
  verdicts stay on the ratio.
- **R-067 new** (`draft` -> `implemented`): the trace figures, the `ce ramane` row, the rule
  "within 0.7 .. 1.4 and the average trace under `Th.TraceNoiseLimit` (1.0) of the light's
  noise reads `OK`", with a synthetic example each way and the Veil 11-08 figures.
- No console option, exit code or file name changes.

## Decisions to record

- **ADR 0043**: check 3 reads the series against its end, not a fitted plateau. Supersedes 0017
  and 0038 in part (their status lines get the note; the REFA limit of 0038 stays). Holds the
  table above, and that the owner's 2 °C set in `stricate` reads `ATENTIE` with frames 1 .. 10 to
  discard, no longer `REFA`.
- **ADR 0044**: check 9 weighs what the mismatch leaves against the noise of a light. Amends the
  verdict of 0004. Holds the measured pairs, the rejected alternative (a count of pixels over 3
  times the noise, two arbitrary limits) and that the rule assumes dithering.

## What changes that the owner should know before saying yes

- **A series still moving at its end is no longer flagged for that alone.** A steady climb whose
  master is within 1 degree of its end reads `OK`, with the far frames in `de aruncat`. This is
  the rule as chosen; nothing else looks at the slope.
- **The owner's 2 °C set in `stricate` reads `ATENTIE`, not `REFA`** (answered in chat).
- **Lightleaks sits at 0.99 degrees against the limit of 1.0.** Whichever side the tool's own
  figure lands on is recorded as measured; if it reads `ATENTIE` it is reported, not tuned.
- **Three synthetic fixtures are replaced**, because under the new rule they are no longer what
  their tests need: the cooling series of the `REFA` golden (`redo.txt`, `en_redo.txt`) and of
  `R023_ASeriesMostlyOffThePlateauIsRefa` / `R071_ARefaVerdictOfCheck3IsARedChip` (4 of 12 frames
  off its end, not more than half) get a steeper series that is `REFA` by the end; the "still
  climbing" test becomes a test of the new reading. Each replaced fixture is named in the commit
  and in the wrap-up, with its figures.
- **`docs/validation.json` changes keys and figures** on the darks cases V-003, V-004, V-008,
  V-009, V-010: `plateau`, `settleCelsius`, `offPlateauFrames` go; `endRatio`, `masterCelsius`,
  `offEndFrames` come (expected 2, 4, 6, 3, 3 frames against today's 2, 5, 8, 4, 3). The lights
  cases V-005, V-007, V-012 gain the three trace figures; their verdicts are expected unchanged.
  One new case, V-013: the -2 °C darks with the Veil 2026-11-08 lights (ratio 0.85, average trace
  0.73, 55 and 7 pixels, `Direct`; check 3 `Ok`, no frame to discard).
- **Every golden changes**: the frames table (the column `sfarsit`, one character wider, and its
  degrees), the check-3 rows, the new `ce ramane` row. Each changed line is read and tied to
  these changes at wrap-up.
- The light's noise is the report's own `zgomot` of the lights table (how much a pixel differs
  from its neighbours), which runs about 9 % above the noise of a single pixel; R-067 says so.
- The version: this changes verdicts a user sees; it seems to warrant a minor bump, at the
  owner's word.

## Implementation steps

One task, one worktree: `.claude/worktrees/Judge-darks-by-series-end-and-traces`, branch
`claude/Judge-darks-by-series-end-and-traces`, from `main`. Check 3 first, then check 9, a
commit each step with the gate green. Builds and tests run only in the worktree.

1. **Requirements** (`docs/requirements.md`): R-023 rewritten; R-025, R-071, R-083, R-086 (the
   quoted words), R-052, R-062 amended; R-067 added as `draft`; the examples of R-054 and R-055
   checked. Verified by `RepoInvariantTests`.
2. **Failing tests** (`tests/DarksCheck.Tests/DarkSeriesTests.cs`, `LightsAndIndexTests.cs`,
   `tests/DarksCheck.Wpf.Tests/MainViewModelTests.cs`, `WindowSmokeTests.cs`): the `R023_...`
   tests rewritten to the end rule (the end and the frames off it, a flat series, under six
   frames, `REFA` by the end, a master over 1 degree from the end, the last frames without hot
   pixels give no end); new `R067_...` tests (lights at 0.85 with weak hot pixels read `OK` with
   the trace figures; with strong ones `-opt`; a ratio of 0.65 stays cosmetic whatever the
   traces; the existing 1.3 lattice lights keep `-opt`). The failing output is pasted.
3. **Core, check 3**: `Thresholds.cs` (`SeriesEndFrames` = 5; `OffPlateauCelsius`,
   `RedoOffPlateauFraction`, `SettledCelsius` renamed to the end's words, values unchanged);
   `DarkSeriesAnalyzer.cs` (`Warming`: `EndRatio`, `MasterCelsius`, `HasEnd`, `OffEnd*`, the
   master without the off frames against the end; `Plateau`, `PlateauCelsius`, `SettleCelsius`
   go; `WarmingOf` takes the last five frames that have a ratio, no end when their median is not
   positive; `PlateauFit` kept, its time constant used only when the fit falls as the series
   does and is not at the top of its grid); `DarkVerdicts.cs` (`DriftVerdict`: `MasterNearEnd`,
   `MostlyOff` by the end); `MatchAnalyzer.cs` (the doc of `Verdict.Redo`).
4. **Report, check 3**: `DarkReport.cs` (the rows `sfarsitul seriei`, `de aruncat`, `verdict`;
   the table column, widened to 7, its note, the row notes, the column hint, the chart note);
   `ReportRows.cs` (`DarkTableRow.End`).
5. **Core and report, check 9**: `LightsAnalyzer.cs` (a `TraceFigures` record on
   `LightsAnalysis`: the trace per unit of `|ratio - 1|`, the average, the two counts, the
   widened band; the pixel's colour by the existing `((y & 1) << 1) | (x & 1)`; the light's norm
   recomputed as the loop does, not stored; a zero noise guarded; the night's verdict relaxed in
   `LightsOverDark` and a flag for the words); `IndexAnalyzer.cs` (`Judge` with a band, the
   plain one unchanged for the partner); `Thresholds.cs` (`TraceNoiseLimit` = 1.0,
   `TraceStrongFactor` = 3); `DarkReport.cs` (`ce ramane`, the two new verdict wordings).
6. **Window**: `MainViewModel.cs` (the frames row and hint renamed, the chart's lines `masterul`,
   `sfarsitul seriei`, the two discard bounds; the lights' points and green lines by the widened
   band), `Views/FramesView.xaml` (the column), `WindowTexts.cs` (`LightsChartLead` takes the
   band). Nothing is computed in the window.
7. **Goldens**: regenerate with `DARKSCHECK_UPDATE_GOLDEN=1 ... --filter GoldenOutputTests`,
   read the whole diff.
8. **Validation**: `ValidationTests.cs` reads the new keys; `/validate` on the real sessions in
   the worktree, the drift table pasted; `docs/validation.json` and `docs/validation.md` take the
   measured figures and V-013.
9. **Docs**: ADR 0043 and 0044; dated status notes on 0017, 0038 and 0004; the design notes
   `dark-analysis.md`, `lights-and-index.md`, `window.md`; `README.md` (checks 3 and 9, the
   table, the chart); `CHANGELOG.md` (`Changed`); this plan saved as
   `docs/plans/2026-11-10-series-end-and-traces.md`; XML documentation on everything added or
   edited.
10. **Review and wrap-up**: a subagent review of the diff, its fixes, then `/wrap-up` (the gate,
    the commit, the merge proposal).

Reused as they are: `Planes.Median`, `Normalisation.Degrees` and `NormOf`,
`DarkSeriesAnalyzer.HotLevel`, `DarkReport.Ranges`, `RunReport.Frames`, `RoCount`,
`VerdictWord`, the fixtures `SessionFixture`, `SyntheticDark`, `SyntheticLight`.

## Verification

- `scripts/verify.ps1 -SkipTests` while editing; `dotnet build ... --no-incremental -warnaserror`
  and the full `scripts/verify.ps1` before each commit, in the worktree.
- `dotnet test --filter "FullyQualifiedName~R023"` and `~R067` red before the code, green after.
- The window: `WindowSmokeTests` render the Frames and Lights tabs; a wrong binding fails there.
- The real sets, with the built console from the worktree: the -2 °C darks with
  `--lights` on Veil 2026-11-08 must read check 3 `OK` (master 0.4 degrees above the end, no
  frame to discard) and check 9 `OK` with `0.73`, `55 din 183`, `7`; the eleven series of the
  table above must read the verdicts and counts of its last two columns (Lightleaks as
  measured).
- `/validate` with `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: every case green on the
  re-recorded figures, the drift table in the wrap-up.
- `scripts/check-design.ps1 -Since main` for the design notes.

## What deviated

- The frames table's column stays six characters wide: `sfarsit` fits where `platou` stood, one
  space closer to `fierb.`, as `plateau` did in English. The plan widened it to seven; the table
  rows of the goldens change in their figures only.
- The check-3 tests were written before the code but not run before it: they could not compile
  against the old `Warming`. The check-9 tests were built first and their compile errors pasted.
- The synthetic `REFA` series is `0.35 + 1.45 x exp(-i / 6)` (7 of 12 frames off its end); it
  also has frames outside the `-opt` band, which a monotone series that is `REFA` by the end
  cannot avoid, so its golden carries that clause too.
- V-013 is a match case that records its darks' check 3 in a `drift` block
  (`ValidationTests.CheckMatch` reads it when present), not a second darks case. The trace
  figures are read on the cases that record them, so V-007 was left as it was.
- V-007 failed, for a reason outside this task: since 2026-11-08 17:31 the Veil session of
  2026-10-06 has a `darks\` folder (the 7 °C library's frames), so it no longer runs as a
  session without darks against the library. Reported, and removed at the owner's word the same
  day (ADR 0045, a second commit); V-012 keeps its figures.
- From the review of the diff: check 3 is `NEMASURAT` only without a first ratio, as R-023
  already said (a last frame without hot pixels had made the whole check unmeasured, and would
  have hidden a `REFA`); a distance that one decimal would print as the limit is printed with
  two (the leak set's master is 0.99 degrees from its end); tests for the band's clamp at
  0.7 .. 1.4 and for the `REFA` advice without minutes.
- A wording added after the owner saw it: from 1.00 up the `ce ramane` row says `un rest de
  5.57 ori cat zgomotul unui light` where the first commit said `un rest de 5.57 din zgomotul
  unui light` (the second commit).
- The sentence above the lights' chart stays in the window's `WindowTexts`, taking the core's
  band; the figures and the band are the core's.
- One commit for the two checks, not one per check: their words share `DarkReport.cs`, the
  goldens and the validation file, and neither half passes the gate alone once the goldens are
  regenerated.
- From a second review, of the whole branch (a fourth commit): tests that pin the count above
  three times the noise, a series of exactly six frames and exactly half the frames off the
  end; the words for "no frame left" removed, since the end is one of the last five ratios and
  its frame is always left; ADR 0044 now supersedes 0004 in part, as step 9 had planned; R-071
  and the README's figures for the 9.2 °C library corrected. Left as they are, reported to the
  owner: a figure that rounds onto a limit in check 9's words, the end read from the last five
  decoded frames over the frame cap, the band's words when it reaches 0.7 .. 1.4.
- After the owner read the window: the two `OK` verdicts of check 3 no longer name `-opt`,
  which read as advice to use it; they say that no frame is too far from the master. The
  `ATENTIE` for a frame outside 0.7 .. 1.4 keeps `-opt`, at the owner's word (a third commit).
