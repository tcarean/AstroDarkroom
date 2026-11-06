# 0037. The texts around the report are in plain words

Date: 2026-11-06
Status: accepted 2026-11-06

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time; every tab is done but
the Raport tab (ADRs 0028 to 0036). The Raport tab shows the console's text, whose sections and
tables are the other tabs' texts. What still named the method, or read oddly, are the texts around
the report: the window's start screen (`sunt decodate si masurate`), the progress lines of the
window and the console (`decodare dark-uri: 3 / 20`), the hints of the top bar's settings
(`se decodeaza in paralel`, `se ia unul din k`, `pentru normalizarea la ISO 400; 2 = liniar in
ISO`) with the setting named `CASTIG / TREAPTA ISO`, and the console's `--help`. The owner
approves them in two parts, each list shown original and proposal side by side; part 1, the
window's texts and the progress lines, with "Da, toate".

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words.
2. **The program reads the frames, it does not decode them**: the start screen says `programul
   citeste si masoara bias-urile, dark-urile si lights-urile, apoi socoteste indexul si eticheta`,
   and the phases are sentences like the wait screen's `Se deschide sesiunea...`: `Se citesc
   bias-urile`, `Se citesc dark-urile`, `Se citesc dark-urile partenerului` (a session that takes
   the partner's darks, R-054), `Se citeste partenerul`, `Se masoara dark-urile`, `Se masoara
   lights-urile`. They are the core's words, so the console's stderr line says the same.
3. **The top bar's hints say what each setting does to the frames**: frames read `in acelasi
   timp`; past the cap `se citesc doar unele, la distante egale (de exemplu unul din doua)`; the
   second setting is named `SEMNAL / TREAPTA ISO`, since `castig` is the method's word, and says
   that with it `toate cadrele se socotesc la ISO 400, ca sa poata fi comparate`, its 2 meaning
   `semnalul se dubleaza la fiecare treapta`; the doubling names the dark current with its meaning
   (`ce aduna senzorul din caldura lui`).
4. **R-088 guards it**: `R088_...` collects the phases of real runs and renders the empty window,
   in both languages, with the word list of R-083 to R-087, which now also holds `liniar` /
   `linear`.

## Consequences

- R-088 is new; R-025 quotes the new progress line and R-075 the new setting name.
- The progress line is on stderr, which the goldens do not hold; the tests that read it take the
  new phases.
