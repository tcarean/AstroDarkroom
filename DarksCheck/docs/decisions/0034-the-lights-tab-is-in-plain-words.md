# 0034. The Lights tab is in plain words

Date: 2026-11-06
Status: accepted 2026-11-06

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time; the ADU scale tab, the
Rezumat (ADR 0030), Cadre (ADR 0031), Harti (ADR 0032) and Pixeli fierbinti (ADR 0033) are done.
The owner picked the Lights tab next. It shows check 9's rows, which ADR 0030 already put in plain
words, a chart of every light against the dark and the lights table, which the console prints
too. Its own texts named the method: the chart's `Amplitudinea ... normalizata`, the column names
`sigma`, `candidati`, `izolati`, `amplit.`, `q25` and `q75`, hints of `metadate`, `mediana`,
`sigma excesului local, scara detectorului` and `quartila`, and the five guide lines read `direct`,
`-opt` and `cosmetic` in both languages. The Cadre task's review had also found that the console's
lights header ended one column short of its figures from `cer` on. The owner approved the list,
original and proposal side by side, with one change: "peste tot in loc de obisnuita pune medie".

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words.
2. **The columns are `zgomot`, `luminosi`, `singuri`, `fierb.`, `jos` and `sus`** (`noise`,
   `bright`, `single`, `hot`, `low`, `high`), in the window and in the console alike. `zgomot` and
   `fierb.` are the Cadre tab's names for the same kinds of figure (ADR 0031); `singuri` is check
   9's own word (`pixeli singuri, nu parte dintr-o stea`); `jos` and `sus` frame the middle half of
   the master's hot pixels, which their shared hint says.
3. **Each hint says what the column measures**, with the thresholds in words: `cu peste 6 ori
   zgomotul`, `peste 3 ori zgomotul`. The `ora` hint is the Cadre tab's. The sky's hint says
   `valoarea medie a pixelilor`: the owner's word for a typical value, asked for in place of
   `obisnuita`. The figure is the median of the pixels (the middle value, which the stars do not
   pull up), so in Siril's statistics it is the median, not the mean; the owner was told.
4. **The chart is titled `FIECARE LIGHT FATA DE DARK`** and its sentence says what each colour
   means to do with the dark (`se poate scadea asa cum e`, `cu -opt`, `doar pentru corectia
   cosmetica`), in check 9's verdict words; the guide lines say the same in short (`0.9 asa cum
   e`, `0.7 cu -opt`, `0.4 doar cosmetic`), now in both languages.
5. **In the console each column name ends where its figures end**, and the file and the time start
   where theirs start; the header and the rows keep their widths.
6. **R-086 guards it**: one test reads the console's table in both languages for the alignment,
   one its words, one renders the tab, with the word list of R-083 to R-085, which now also holds
   `exces`, `detector`, `quartila`, `izolat` and `q25` / `q75`.

## Alternatives rejected

- **`de la` and `pana la` for the quartiles.** They read as one phrase across two columns; `jos`
  and `sus` are shorter and their hint says what lies between them.
- **Keeping `candidati`.** R-082 lists it among the method's words; `luminosi` says what the
  pixels are.

## Consequences

- R-086 is new; R-050 to R-053 describe the same figures and hold.
- The four goldens with lights take the new header line of `TABEL PE LIGHTS`; nothing else in them
  changes.
- The README's console and window sections name the new columns.
