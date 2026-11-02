# 0030. The Rezumat tab is in plain words, approved part by part

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time, starting with the ADU
scale tab. The owner picked the Rezumat tab next. It shows the report's rows, the same the
console prints: the session card, the `=== DARK-URI` line and its rows, then checks 2 to 9 and
INDEX, about forty-five rows with their variants. One list of every text would be too long to
read, so the owner approves it in three parts, each shown with the texts of a real run on the
Smida session and on the light-leak set: the session card, the header and checks 2 and 3; checks
4 to 8; check 9 and INDEX. The plan `docs/plans/2026-11-02-explain-the-darks-summary-tab-plainly.md`
records each part's list.

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words; a term only with its meaning beside it; the figures as the result gives them.
2. **Part 1** (approved "Da, toate"): the words of the plan's first list.
   - The header line names what it shows: `cadre citite 20 din 20`, `valori de la 150 (negru) la
     4095 ADU (alb)`, `citite exact din raw` for a raw, the bits and where the white and the black
     came from for a FITS, `filtre de culoare RGGB`. The copied reader's `libraw (exact)` stays in
     the copy; `FrameStack` words it for the report.
   - The header's rows `normalizare` and `cuantizare` become `1 ADU-eq` and `pasul valorilor`, in
     the ADU tab's words; check 3's `amplitudine` becomes `pixeli fierbinti`.
   - The `±` of an error becomes `+/-`, which every console shows.
   - The labels `master dark`, `master bias` and `dark - bias` stay, since the window finds the
     maps' rows by them.
3. **Part 2** (approved "Aprobat"): the words of the plan's second list, checks 4 to 8.
   - A row says what its figure shows, not how it is reckoned: `de obicei` for the median of the
     frames, `80 % dintre ei intre 21 si 81` and `cei mai fierbinti 1 %` for the percentiles,
     `intre cel mai luminos si cel mai intunecat` for peak to peak, `pasul valorilor din fisier`
     for the quantisation step. The formulas (`sqrt(...)`, `1.25 x`) leave the rows; the factor
     stays in `Thresholds.cs` and in the requirements.
   - The median keeps its name in one place, check 8's `un master din 10 cadre facut cu mediana si
     unul din celelalte 10 facut cu media`, where it says how each of the two masters was made.
     `RTS` stays, with what it means beside it.
   - The titles become `4. ZGOMOTUL FIECARUI CADRU` and `6. MODEL FIX: CE E LA FEL IN TOATE
     CADRELE`; the rows `pixeli peste prag`, `pe culori, peste 20`, `cat de fierbinti`, `benzi
     fixe` and `zgomot dupa medie`. Check 4's first row is `fata de master`: the owner approved
     `zgomot fata de master`, which is two characters wider than the console's label column, and
     the title above it already says it is the noise; asked, the owner kept it.
4. **Part 3** (approved "Da, toate"): the words of the plan's third list, check 9 and INDEX.
   - A match says what its ratio means for the hot pixels: `in lights pixelii fierbinti sunt de
     1.30 ori cat in dark` (or `la partener`), with no `raport`, `q25` or `q75`; the quartiles are
     `jumatate dintre ei intre 0.38 si 0.44`.
   - The verdicts say what to do in Siril, with the option's meaning beside it: `se poate scadea
     asa cum e`, `scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu
     lights-urile)`, `foloseste dark-ul doar pentru corectia cosmetica (-cc=dark: Siril gaseste in
     el pixelii fierbinti si ii repara in lights)`, `nu se potrivesc`.
   - The degrees of the INDEX rows are words, `cu 7.5 grade mai reci decat partenerul` or `la
     aceeasi temperatura` (under `Th.SameTemperatureCelsius`, the verdicts' limit), not a signed
     figure.
   - The index says what it is: `valoarea obisnuita a pixelilor fierbinti de pe locurile 122 ..
     483, numarand de la cel mai fierbinte`.
   - Check 9's title becomes `9. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE`, the Lights
     tab's heading too; the rows `masterul in lights`, `lipsesc din master` and `lights fata de
     dark`.
5. **R-082 guards it**, growing with each part: `R082_...` reads every variant the fixtures reach,
   in both languages, and fails on a word of the method; an error message of the analysis that a
   row quotes is outside it.

## Alternatives rejected

- **The whole tab in one list.** About forty-five rows with their variants; the owner could not
  read it at one sitting.
- **Rewording the labels the window looks up** (`master dark`, `master bias`, `dark - bias`). The
  Harti tab finds its rows by them; renaming them breaks the window for no gain to the reader.

## Consequences

- The requirements that quote part 1's texts take the new words (R-003, R-006, R-007, R-020,
  R-021, R-023, R-025, R-028, R-029, R-041, R-071, R-072, R-076, R-095); R-082 is new. Part 2's
  take them in R-024, R-025 and R-040, and R-082 covers checks 4 to 8; part 3's in R-025, R-051 to
  R-054, R-060, R-062 to R-064 and R-093, and R-082 covers the whole tab.
- The `1 ADU-eq` row named a set at the reference settings whenever its factor was 1, which ISO
  800 at 60 s also gives; it now names the reference settings only at ISO 400 and 120 s, in the
  header and in the ADU tab alike (`DarkReport.AtReference`).
- The analysis's error messages that a row quotes still say `nu s-a putut decoda`; the console prints them
  too, so their words go with the console's texts.
