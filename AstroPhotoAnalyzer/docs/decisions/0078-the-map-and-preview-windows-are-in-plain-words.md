# 0078. The map and preview windows are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

After the seven tabs (ADRs 0071 to 0077), the two windows the Tonuri tab opens. The map window
explains each of the five kinds of patches in its legend, with the image's own thresholds, and
lists the catalogued dust clouds; the preview window shows the image lifted to a row's
background, with a caption, two boxes and a note. The legends were written in the method's
words: "percentila", "mediana", "cerul modelat", "sigma", "pragul Otsu", "coada de nebuloasa",
`|b|`; the clouds' box said "norii catalogati" and "etichete", and nothing said what a cloud's
"tinta" is. On 2026-11-02 the owner approved 18 rewrites, from an original / proposal list,
one of them a new sentence.

## Decision

Every text of the two windows is in plain words, in both languages, with every figure and
threshold it carried before. R-130 and R-131 carry the clause, checked by
`R130_TheMapAndPreviewTextsAreInPlainWords` in both languages on the sky path and on the band
path, the windows' own XAML texts included. In particular:

- the median is "valoarea din mijloc" and the dust's percentile "pragul sub care stau 25 % din
  ele", the words of the Tonuri and Editare Curbe tabs (ADRs 0072, 0076);
- the modelled sky is "cerul", a patch "mai luminoasa decat cerul" by so many L\*, and the sky's
  noise "zgomotul lui", as in the dust's definition the Tonuri tab already shows;
- the band is "banda Caii Lactee, la mai putin de 10 grade de ea" instead of `|b|`;
- the Otsu threshold is "pragul la care nebuloasa se desparte in zone slabe si stralucitoare";
- the clouds' list gets a new sentence under its heading: what a cloud's "tinta" is, in the
  words of the report's dust line, so the list's rows keep the report's columns;
- the preview's caption says the tones were lifted with a curve, the gamma in brackets.

## Alternatives rejected

- **Renaming "tinta" in the clouds' rows.** The rows repeat the report's clouds table, whose
  column is "tinta"; one sentence under the heading explains it for both, where a new word in
  the rows alone would make the window and the report disagree.
- **Translating "Preview" and "print-safe".** They are the Tonuri tab's own names, the button
  that opens the window and the mark of its table; they change with that tab or not at all.

## Consequences

- R-130 and R-131 say so; the README describes the two windows without quoting their texts and
  needs no change.
- The parameter card with Appendix F of the report, and the README's prose, remain.
