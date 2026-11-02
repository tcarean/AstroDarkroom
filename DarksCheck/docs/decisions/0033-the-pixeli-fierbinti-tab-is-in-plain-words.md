# 0033. The Pixeli fierbinti tab is in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time; the ADU scale tab, the
Rezumat (ADR 0030), Cadre (ADR 0031) and Harti (ADR 0032) are done. The owner picked the Pixeli
fierbinti tab next. It shows check 5's rows, which ADR 0030 already put in plain words, and the
noise curve of check 8. Its own texts are four: the text under the rows, which named the method
(`masterului median`, `pe planuri`, `amplitudinile`, `candidatii RTS`); the chart's title, `8.
ZGOMOTUL MEDIEI A n CADRE (ADU)`; the chart's sentence, which named `sigma / sqrt(n)`, `sigma
temporala` and `modelul fix`; and the hint of a point, `n=1: 1.426 masurat, 1.510 asteptat`. The
owner approved the list of the four, original and proposal side by side, with "Da, toate".

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words.
2. **The text under the rows says what the rows count, in the rows' own words**: the five
   thresholds from 10 to 500 ADU-eq, the colours above 20, `cat de fierbinti`, whether the pixels
   show in every frame, and those that `sar intre doua niveluri`, with `RTS` beside them. It drops
   `median`: how the master is made does not change what the counts mean. It says that check 5
   only informs and never warns, which is what its verdict row does (`OK (informativ)`, always).
3. **The chart is titled by what it shows**: `8. CAT SCADE ZGOMOTUL CU MAI MULTE CADRE (ADU)`.
4. **The chart's sentence explains the dotted line without the square root**: the noise of one
   frame, falling as a random noise falls, `de 4 ori mai multe cadre, zgomot de 2 ori mai mic`.
   The 4 and the 2 are no figure of the run; they say how the line falls, which its own points
   show. The sentence also says why the points can sit under the line: they are measured between
   two groups of frames, so without what is the same in every frame, while the line starts from
   the whole noise of one frame (on Smida 1.514 ADU, against 1.426 from one frame to the next).
5. **A point's hint names its frames as check 8's row does**: `1 cadru: masurat 1.426, se astepta
   1.514`, the count written by `RunReport.Frames`.
6. **R-085 guards it**: `R085_...` renders the tab in both languages with the word list of R-083
   and R-084, which now also holds `temporal`, `candidat...`, `planuri` / `planes` and `n=`.

## Alternatives rejected

- **Saying how the master is made** (`masterul facut cu mediana`). Check 8's `master` row keeps
  it, where it tells two masters apart (ADR 0030); here it is one more thing to explain and
  changes nothing the reader does.
- **The square root in words** (`zgomotul scade cu radacina numarului de cadre`). It names the
  method; 4 times the frames for half the noise says the same with numbers anyone can check.

## Consequences

- R-085 is new; R-024's and R-071's descriptions of the curve hold.
- The README's window section describes the chart in the same words.
