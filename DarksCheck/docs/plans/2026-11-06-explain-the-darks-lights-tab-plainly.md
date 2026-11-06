# Explain the Lights tab in plain words

Plan for the task branch `claude/Explain-the-darks-Lights-tab-plainly`, worktree
`.claude/worktrees/Explain-the-darks-Lights-tab-plainly`, from `main` at c3141954. Written on
2026-11-06. The owner picked the Lights tab after Pixeli fierbinti (ADR 0033) and approved its list
with one change: "peste tot in loc de obisnuita pune medie. In rest ok". The version is not
changed.

## Context

The Lights tab shows check 9's rows, already in plain words (ADR 0030), the chart of every light
against the dark with five guide lines, and the lights table, whose rows the console prints too
(`TABEL PE LIGHTS`). The tab's own texts live in `WindowTexts` (the chart's sentence, two hints),
`LightsView.xaml` (the titles, the column names, the other hints), `MainViewModel` (the guide
lines) and `DarkReport` (the console's header line and the table's note, which the window shows
too). The Cadre task's review found that the console's lights header ended one column short of
its figures; it was left for this tab.

## The list, approved ("In rest ok", 2026-11-06)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | the chart's title | `LIGHTS / DARK, PE CADRU` | `FIECARE LIGHT FATA DE DARK` |
| 2 | the chart's sentence | `Amplitudinea pixelilor fierbinti ai masterului in fiecare light, fata de master, normalizata la ISO si expunere. Verde intre 0.9 si 1.1 (scadere directa), galben pana la 0.7 si 1.4 (-opt) si pana la 0.4 (doar corectie cosmetica), rosu dincolo; primele lights ale unei nopti sunt reci.` | `Fiecare punct e un light: cat de fierbinti sunt in el pixelii fierbinti ai masterului, fata de master (1 = la fel), dupa ce se tine cont de ISO si de expunere. Verde intre 0.9 si 1.1: dark-ul se poate scadea asa cum e. Galben intre 0.7 si 1.4: scade dark-ul cu -opt (Siril il inmulteste cat sa se potriveasca cu lights-urile); tot galben intre 0.4 si 0.7: foloseste dark-ul doar pentru corectia cosmetica. Rosu mai departe: nu se potrivesc. Primele lights ale noptii ies de obicei mai jos, pentru ca aparatul e inca rece.` |
| 3 | the five lines | `0.9 direct`, `1.1 direct`, `0.7 -opt`, `1.4 -opt`, `0.4 cosmetic`, the same in both languages | `0.9 asa cum e`, `1.1 asa cum e`, `0.7 cu -opt`, `1.4 cu -opt`, `0.4 doar cosmetic` |
| 4 | the column names | `sigma`, `candidati`, `izolati`, `amplit.`, `q25`, `q75` | `zgomot`, `luminosi`, `singuri`, `fierb.`, `jos`, `sus`, in the window and the console |
| 5a | `ora`'s hint | `ora de start a expunerii, din metadatele fisierului` | the Cadre tab's: `ora la care a inceput expunerea, cum e scrisa in fisier (la un raw, ora aparatului; la un FITS de la Siril, ora UTC)` |
| 5b | `cer`'s hint | `mediana cerului peste negru, ADU` | `cat de luminos e cerul in light: valoarea medie a pixelilor, peste negru, in ADU` (the list said `valoarea obisnuita`; the owner asked for `medie`) |
| 5c | `zgomot`'s hint | `sigma excesului local, scara detectorului, ADU` | `cat difera de obicei un pixel de vecinii lui de aceeasi culoare, in ADU; cu el se cauta pixelii fierbinti` |
| 5d | `luminosi`'s hint | `pixeli peste 6 sigma locale, sub saturatie` | `cati pixeli sunt mai luminosi decat vecinii lor cu peste 6 ori zgomotul, fara sa fie saturati: stele si pixeli fierbinti` |
| 5e | `singuri`'s hint | `candidati fara niciun vecin peste 3 sigma: pixeli fierbinti, nu stele` | `cati dintre ei nu au niciun vecin care sa iasa si el peste 3 ori zgomotul: pixeli fierbinti, nu stele` |
| 5f | `fierb.`'s hint | `lights / dark la pixelii fierbinti ai masterului, mediana` | `cat de fierbinti sunt in acest light pixelii fierbinti ai masterului, fata de master (1 = la fel), dupa ce se tine cont de ISO si de expunere; e punctul din grafic` |
| 5g | `jos` and `sus`'s hint | `quartila de jos (de sus) a raportului lights / dark pe pixelii fierbinti` | `jumatate din pixelii fierbinti ai masterului sunt, in acest light, intre jos si sus (fata de master, 1 = la fel)` |
| 6 | the table's note | `[G1; cer = ADU peste negru; sigma = a excesului local, scara detectorului; amplit. = lights / dark la pixelii fierbinti ai masterului]` | `[pixelii verzi G1; cer = cat de luminos e cerul, ADU; zgomot = cat difera un pixel de vecinii lui, ADU; luminosi = mai luminosi decat vecinii cu peste 6 ori zgomotul; singuri = fara vecini luminosi, adica pixeli fierbinti; fierb. = pixelii fierbinti ai masterului fata de master (1 = la fel); jos, sus = jumatate dintre ei sunt intre aceste doua valori]` |
| 7 | the console's header | from `cer` on, each name ends one column before its figures | each name ends where its figures end |

The English says the same, with the same figures: `EACH LIGHT AGAINST THE DARK`, `noise`,
`bright`, `single`, `hot`, `low`, `high`, `0.9 as it is`, `0.7 with -opt`, `0.4 cosmetic only`.

## Steps

1. Requirements: R-086, new.
2. Tests: `R086_TheLightsTableNamesStandAboveTheirFigures` first, failing on the old header in
   both languages; then `R086_...` on the console table's words and on the rendered tab, with the
   word list of R-083 to R-085, which takes `exces`, `detector`, `quartila`, `izolat` and
   `q25` / `q75`.
3. Code: `WindowTexts.cs`, `LightsView.xaml`, `MainViewModel.cs`, `DarkReport.cs`
   (`LightTableHeader`, `LightTableNote`); no figure changes, so no `/validate`.
4. The goldens with lights take the new header line, and nothing else.
5. Gate, ADR 0034, CHANGELOG, README, the design notes, commit, the review offer, the merge
   proposal.
6. The owner's "peste tot": the other texts that say `obisnuit` are shown to the owner, original
   and proposal, before any edit.

## What deviated

- `MainViewModelTests` read the old note's start, `[G1;`; it now reads `[pixelii verzi G1;`, the
  approved note's start, as it already did for the frames table's note.
- The sky's hint says `valoarea medie` at the owner's word while its figure is the median; ADR
  0034 records it, and the owner is told.
