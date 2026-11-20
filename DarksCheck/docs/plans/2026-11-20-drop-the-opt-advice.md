# Drop the -opt advice: a dark off by temperature is subtracted as it is

## Context

DarksCheck tells the owner to "subtract the dark with -opt" when the lights' hot pixels are
0.7 .. 1.4 of the dark's, and to use the dark "for the cosmetic correction only" at 0.4 .. 0.7,
because "subtracted it would leave black dots". Measured on 2026-11-19/20 on copies (Siril 1.4.4,
Nikon D3300, ISO 1600, 120 s, L-eNhance; results kept in the old session's scratchpad `opt2\`):

- Siril's `-opt` chose 0.002 (the dark in effect not subtracted) on every light of 15 of 18
  light / dark pairs, matched pairs included; 0.002 .. 0.42 on the other three, always under the
  right factor. Cause: Siril looks at all colours of the raw frame together, and the colour
  differences of the sky decide the factor, not the hot pixels.
- Two stacks of 28 lights on one shared registration, the cosmetic correction on in each:

  | Stack | Dark | Plain subtraction against the right factor | Dots above 5 sigma |
  |---|---|---|---|
  | Veil 11-06 with idx33 | about 7 °C warmer, ratio about 0.45 | differ by 12-17 % of the stack's noise | black 358 against 360 |
  | Veil 10-06 with idx18 | about 4 °C colder, ratio about 1.6 | differ by 15-20 % of the noise (-opt: 23-32 %) | bright 13526 against 13542 (-opt: 13661) |

- The band 0.7 .. 1.4 is -3.1 / +2.9 °C by the program's 6-degree doubling; `Thresholds.cs` calls
  it "±5 °C".

The owner decided in chat on 2026-11-20: both yellow bands say "subtract the dark as it is" and
stay a WARNING; no `-opt` anywhere; for every camera, not only the D3300. No limit moves.

The note said "check 9"; since ADR 0046 this is check 2. The same words are also in the Index
tab's partner verdict, in check 4 and in the text for darks made at other settings (ADR 0055), so
all four change together.

## What changes in behaviour

1. `0.7 .. 1.4` outside the green band: WARNING, subtract as it is (text 1).
2. `0.4 .. 0.7`: WARNING, subtract as it is with the cosmetic correction on (text 2).
3. Darks at other settings than the lights (ADR 0055): the advice is new darks first, then the
   cosmetic correction only (texts 3, 4). **New:** a ratio in `0.4 .. 0.7` at other settings now
   takes this text too; today it took the cosmetic-only text, and text 2 would tell the owner to
   subtract a dark of another ISO as it is, which ADR 0055 forbids.
4. Check 4 keeps its band 0.7 .. 1.4 for the frames of a series and stops naming `-opt` (text 5).
5. Under 0.4 and over 1.4: unchanged ("they do not match"). One stack each side is not enough to
   move a limit.

No formula and no constant's value changes, so no figure of `docs/validation.json` can move
(none records a verdict word). `/validate` is therefore not required by the rule "Measured on the
sessions"; the by-hand run in Verification reads the new words on a real session.

## Texts for the owner (the rule "Plain words")

Figures in the examples are those of the golden files or of the test above. `the dark` reads
`the partner` in the partner's verdict. Edit any cell; I change the code only to what you approve.

| # | Where | Today | Proposal (English / Romanian) |
|---|---|---|---|
| 1 | Verdict, 0.7 .. 1.4 (check 2, partner) | `WARNING: subtract the dark with -opt (-opt: Siril multiplies it so that it matches the lights); in the lights the hot pixels are 1.30 times what they are in the dark, between 0.7 and 1.4, and what the subtraction leaves is on average 5.57 times the noise of a light: the dark is colder by 2.3 degrees` | `WARNING: subtract the dark as it is, though it is not at the same temperature; in the lights the hot pixels are 1.30 times what they are in the dark, between 0.7 and 1.4, and what the subtraction leaves is on average 5.57 times the noise of a light: the dark is colder by 2.3 degrees` / `ATENTIE: scade dark-ul asa cum e, desi nu e la aceeasi temperatura; in lights pixelii fierbinti sunt de 1.30 ori cat in dark, intre 0.7 si 1.4, iar ce ramane dupa scadere e in medie de 5.57 ori zgomotul unui light: dark-ul e mai rece cu 2.3 grade` |
| 2 | Verdict, 0.4 .. 0.7 (check 2, partner) | `WARNING: use the dark for the cosmetic correction only (-cc=dark: Siril finds the hot pixels in it and repairs them in the lights); in the lights the hot pixels are 0.45 times what they are in the dark: the dark is warmer by 6.9 degrees, it sees them all, but subtracted it would take out too much and leave black dots` | `WARNING: subtract the dark as it is, with the cosmetic correction on (-cc=dark: Siril finds the hot pixels in it and repairs them in the lights); in the lights the hot pixels are 0.45 times what they are in the dark: the dark is warmer by 6.9 degrees. In one light the subtraction takes too much at the hot pixels; with dithering this does not show in the stack` / `ATENTIE: scade dark-ul asa cum e, cu corectia cosmetica pornita (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); in lights pixelii fierbinti sunt de 0.45 ori cat in dark: dark-ul e mai cald cu 6.9 grade. Intr-un light scaderea ia prea mult la pixelii fierbinti; cu dithering asta nu se vede in stack` |
| 3 | Verdict, darks at other settings (the middle of the sentence; the rest stays) | `... Best make darks at the lights' settings. With these darks, subtract with -opt (-opt: Siril multiplies the dark so that it matches the lights; it needs the biases too) or use the dark for the cosmetic correction only (-cc=dark); worked out for the same settings, ...` | `... Best make darks at the lights' settings. With these darks, use the dark for the cosmetic correction only (-cc=dark: Siril finds the hot pixels in it and repairs them in the lights); worked out for the same settings, ...` / `... Cel mai bine fa dark-uri la setarile lights-urilor. Cu aceste dark-uri, foloseste dark-ul doar pentru corectia cosmetica (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); socotiti pentru aceleasi setari, ...` |
| 4 | Verdict, partner at other settings (same place) | `... With this partner, subtract with -opt (-opt: Siril multiplies the partner so that it matches the lights; it needs the biases too) or use it for the cosmetic correction only (-cc=dark); ...` | `... With this partner, use it for the cosmetic correction only (-cc=dark: Siril finds the hot pixels in it and repairs them in the lights); ...` / `... Cu acest partener, foloseste-l doar pentru corectia cosmetica (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); ...` |
| 5 | Check 4, frames far from the master | `5 frames (1 .. 3, 11, 12) are too far from the master for Siril to match them with -opt (outside 0.7 .. 1.4; the farthest 1.93): the series started warm; discard them` | `5 frames (1 .. 3, 11, 12) are too far from the master (their hot pixels are outside 0.7 .. 1.4 of the master's; the farthest 1.93): the series started warm; discard them` / `5 cadre (1 .. 3, 11, 12) sunt prea departe de master (pixelii lor fierbinti sunt in afara 0.7 .. 1.4 din ai masterului; cel mai departe 1.93): seria a pornit calda; arunca-le` |
| 6 | Lights chart, the sentence above it (the middle; first and last sentence stay; the widened variant keeps `Yellow from there to 0.7 and 1.4`) | `Yellow between 0.7 and 1.4: subtract the dark with -opt (Siril multiplies it so that it matches the lights); yellow too between 0.4 and 0.7: use the dark for the cosmetic correction only.` | `Yellow between 0.7 and 1.4: the dark is a few degrees off, subtract it as it is all the same; yellow too between 0.4 and 0.7: the dark is much warmer, subtract it as it is, with the cosmetic correction on.` / `Galben intre 0.7 si 1.4: dark-ul e la cateva grade de lights, scade-l tot asa cum e; tot galben intre 0.4 si 0.7: dark-ul e mult mai cald, scade-l asa cum e, cu corectia cosmetica pornita.` |
| 7 | Lights chart, yellow lines | `0.7 with -opt`, `1.4 with -opt` / `0.7 cu -opt`, `1.4 cu -opt` | `0.7 a few degrees off`, `1.4 a few degrees off` / `0.7 la cateva grade`, `1.4 la cateva grade` |
| 8 | Lights chart, red line | `0.4 cosmetic only` / `0.4 doar cosmetic` | `0.4 much warmer` / `0.4 mult mai cald` |

The green lines (`0.9 as it is`) and the row `what is left` keep their words.

## Where (worktree `.claude/worktrees/Drop-the-opt-advice-from-the-match-verdicts`, branch `claude/Drop-the-opt-advice-from-the-match-verdicts`)

Order: requirements, tests, code, docs (the kit's rule "Requirements first").

- `docs/requirements.md`: R-062 (the ladder's names and words), R-052, R-063 (other settings,
  the 0.4 .. 0.7 case added), R-023 (check 4's sentence), R-067, R-086 (chart lines), and the
  quoted examples of R-054, R-055, R-122.
- Tests, changed with their requirement: `LightsAndIndexTests` (the R062 theories and the quoted
  verdicts; a new `R052_...` test for a ratio in 0.4 .. 0.7 at other settings; the language sweep
  also asserts that no text of a report names `-opt`), `DarkSeriesTests` (check 4's sentence),
  `MainViewModelTests`, `LanguageWindowTests`, `PresentationTests`.
- `src/DarksCheck.Core/Analysis/IndexAnalyzer.cs`: `MatchVerdict.WithOpt` becomes `Near`,
  `CosmeticOnly` becomes `MuchWarmer`; `Judge` unchanged in its bounds.
- `src/DarksCheck.Core/Thresholds.cs`: `MatchOptLow/High` become `MatchNearLow/High`,
  `MatchCosmeticLow` becomes `MatchWarmLow`, values unchanged; the comments say what the bands
  mean now and "about ±3 °C" instead of "±5 °C".
- `src/DarksCheck.Core/Report/DarkReport.cs`: `MatchText` (texts 1 to 4; the other-settings arm
  also takes `MuchWarmer`), `Drift` (text 5), the comments that name `-opt`.
- `src/DarksCheck.Core/Analysis/LightsAnalyzer.cs`, `DarkVerdicts.cs`, `Runs/DarkSeriesRun.cs`:
  the renamed names and their comments (`NeverDirect` keeps its rule).
- `src/DarksCheck.Wpf/ViewModels/WindowTexts.cs` (text 6), `MainViewModel.cs` (texts 7, 8),
  `Presentation/VerdictPalette.cs` (names; both stay yellow).
- Goldens `lights`, `lights-from`, `redo` (and `en_`): regenerated, each changed line tied to
  text 1 or 5 at wrap-up.
- `docs/decisions/0070-...md`: the measurement, the decision, the alternatives rejected (keep
  `-opt` with a caution; green from 0.7 to 1.4; only the D3300; keep cosmetic only), what was not
  measured (other cameras, other settings, no dithering). Dated notes on ADRs 0004, 0044 and
  0055 in the way the kit's ADR 0024 allows.
- `docs/design/dark-analysis.md`, `docs/design/window.md`, `README.md` (the verdict table, the
  other-settings paragraph, check 4, the partner example, the Lights tab line), `CHANGELOG.md`
  (Changed), this plan saved as `docs/plans/2026-11-20-drop-the-opt-advice.md`.

## Verification

- `scripts/verify.ps1` green in the worktree (never in the main checkout).
- Golden diff read line by line.
- The window's Lights tab rendered off-screen in both languages, to see that the longer line
  labels (text 7) fit the chart.
- One by-hand console run on a real session with a yellow verdict (Veil 2026-11-06 against the
  idx20 library, read-only, files as the memory note on test data says), to read texts 1 and the
  row `what is left` together.
- A code review by a subagent before the merge is proposed.

## After the merge (not in this task)

- The wiki's DarksCheck manual names `-opt`; it follows the approved texts, pushed on your word.
- The change seems to warrant a version bump; the version is yours.
- `SirilScripts/OSC_PreprocessingWithDarkScaling.ssf` is left as it is; the stock script already
  does what the new verdicts say.

## What deviated

- The goldens `lights-only` and `en_lights-only` changed too (text 2), and `lights` / `en_lights`
  carry a partner verdict that changed with it: ten lines in eight files, not three pairs.
- R-064's example and `R064_TheLightsAreComparedWithThePartnerOnTheSettingsThoseTwoGive` follow
  item 3: a partner much warmer than lights at another ISO now reads the words of other settings.
- The singular of text 5 reads `its hot pixels` / `pixelii lui fierbinti`.
- The result files of the measurement were copied from the old session's scratchpad into
  `docs/validation/2026-11-20-siril-opt/`, so ADR 0070 does not rest on a temporary folder.
- Status lines: ADRs 0004, 0030, 0034 and 0055 name 0070; ADR 0044's decision stands and was not
  touched; ADR 0017's "±5 °C" got a dated correction.
- `R067_TheTracesOnlyRelaxTheOptBandAndAMatchedDarkLeavesNothing` was renamed `...TheNearBand...`.
- The plan was wrong that no recorded figure holds a verdict word: `docs/validation.json` holds
  `CosmeticOnly` in V-005 and V-012, and `ValidationTests` compares it with the verdict's name
  (found by the review). At the owner's word the two read `MuchWarmer`, and `/validate` was run
  after it (`docs/validation.md`, History).
- The review also found that, for lights at other settings, the chart's sentence said "subtract
  it as it is all the same" beside a verdict that says not to. The owner approved a sentence of
  its own for that case (text 9: `The darks are made at other settings than the lights, so no
  point is green: the dark is not subtracted as it is. Yellow between 0.4 and 1.4: use the dark
  for the cosmetic correction only.` / `Dark-urile sunt facute la alte setari decat
  lights-urile, asa ca niciun punct nu e verde: dark-ul nu se scade asa cum e. Galben intre 0.4
  si 1.4: foloseste dark-ul doar pentru corectia cosmetica.`), R-086.
- ADR 0017's body keeps the old constant names; only its "±5 °C" got a dated correction.
- Asked what a much warmer dark is good for, the owner was told what Siril's manual says:
  `-cc=dark` needs `-dark=`, and `calibrate` subtracts the dark it is given, so "the cosmetic
  correction only" is no switch. At the owner's word texts 3, 4 and 9 advise one thing: `Make
  darks at the lights' settings` / `Fa dark-uri la setarile lights-urilor` (`Use darks made at
  the lights' settings` / `Foloseste dark-uri facute la setarile lights-urilor` for a partner),
  and text 9's yellow reads `the temperatures are close` / `temperaturile sunt apropiate`.
- The owner then asked whether such darks should not simply be made again, and approved, texts
  side by side: at other settings the verdict's word is `REFA` / `REDO` at every measured
  ratio (the far ones took `nu se potrivesc` before), every point of the chart is red, the
  chart has no lines, and text 9 reads `... asa ca toate punctele sunt rosii: dark-ul nu se
  scade asa cum e. Fa dark-uri la setarile lights-urilor.` / `... so every point is red: the
  dark is not subtracted as it is. Make darks at the lights' settings.` A new verdict,
  `MatchVerdict.OtherSettings`; check 1's row on the settings stays `ATENTIE`.
- I told the owner that no real session has darks at other settings; the second review showed
  that V-005's partner is one (Smida at ISO 400 against the library at ISO 1600), so its
  recorded verdict no longer held. The owner called the pair no real case and had it taken out
  of the validation; V-005 keeps the Smida lights against their own darks. The README's
  `--against` example, which showed that pair, was replaced by a real run at the same settings.
- At the owner's two notes of the same evening the rest of what the investigation found was
  recorded, docs only: `docs/validation/2026-11-20-siril-opt/conclusions.md` with the files it
  cites, a dated note in ADR 0070, and the review list's CR-083. A third review, of the whole
  branch, found no defect in the code and six in that record and the ADR, corrected.
