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
window's texts and the progress lines, with "Da, toate"; part 2, the console's `--help` and the
message of a blank folder path (`folderul e gol`, which reads as a folder without files), with
"Da, toate" too.

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
4. **The help says what each option does in the same words**: without options the program
   `citeste si masoara` the frames and prints the checks by the Rezumat's names (`incalzirea
   camerei`, `ce e la fel in toate cadrele`, `cat de bine se potriveste dark-ul cu lights-urile`);
   `--against` gives the partner's index and `cu cate grade sunt dark-urile sesiunii mai calde sau
   mai reci decat el`; `--threads` and `--max-frames` say what the top bar's hints say;
   `--inventory` reads `doar ce scrie in fisiere, fara sa citeasca pixelii`, with `cat de des s-au
   facut cadrele` for the cadence. A blank folder path reads `calea folderului e goala`.
5. **R-088 guards it**: `R088_...` collects the phases of real runs, renders the empty window and
   reads `--help` and the blank path's message, in both languages, with the word list of R-083 to
   R-087, which now also holds `liniar` / `linear`, `deriv...` / `drift` and `cadent...` /
   `cadence`.

## Consequences

- R-088 is new; R-025 quotes the new progress line and R-075 the new setting name.
- The progress line is on stderr, which the goldens do not hold; the tests that read it take the
  new phases. The two help goldens take the new lines of the options they name, nothing else.
