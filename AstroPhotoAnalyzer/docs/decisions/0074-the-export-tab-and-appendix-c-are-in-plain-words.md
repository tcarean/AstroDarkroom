# 0074. The Export tab and Appendix C are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The fourth tab under the rule "Plain words" (ADR 0071). The Export tab shows the JPEG table with
a five-paragraph guide under it, then the rows of the 8-bit TIFF's size, the 16 to 8 bit
rounding, the two shadow-lift rows and the plateau, each with a description; Appendix C of the
text report prints the same figures. They were written in the method's words: "encodata",
"subesantionarea culorii", "esantion", "YCbCr", "monoton", "predictor orizontal", "stres
shadow-lift", "auto-correct", "master-ul de 16 biti", "palier pe cerul cuantizat", "dale",
"pozitii", sigma and LSB. On 2026-11-02 the owner approved 16 rewrites, from an original /
proposal list.

## Decision

Every text of the tab and every line of Appendix C is in plain words, in both languages, with
every figure and threshold it carried before. R-114 carries the clause, and
`R114_TheExportTextsAreInPlainWords` checks it in both languages on the dark scene and the mono
sky, the appendix included. In particular:

- the headings are "JPEG: CAT SE SCHIMBA IMAGINEA LA FIECARE CALITATE" and "TRECEREA PE 8 BITI";
- q100 is said "calitatea 100" in the sentences, the noise is given in levels, and "the 16-bit
  master" is "originalul pe 16 biti";
- the plateau is "zone plate pe cerul trecut pe 8 biti", measured in "zone mici de cer neted"
  among "locuri incercate".

The JPEG table's column titles stay ("err medie", "p99", "> 2 niv"...), on the tab and in the
appendix, as the size table's did on the Rezolutie tab (ADR 0071): the columns have fixed widths,
and the guide under the table explains each one. The guide's leads, which name those columns,
stay with them.

The goldens `dark-sky`, `dark-sky-wcs`, `clean-sky-canvas` and `mono-png8`, in both languages,
were recaptured for Appendix C alone. Counting every number before and after, the measured
figures are all unchanged; the one digit added is the 8 of the label "trecut pe 8 biti".

## Alternatives rejected

- **Plain column titles in the JPEG table.** Words like "eroare medie" need wider columns, in two
  places kept in step by hand (the tab and the appendix), and the guide already says what each
  column is.
- **Dropping the table's p99 and p99.9.** They are figures the owner reads; the guide now says
  what they are, in a sentence.

## Consequences

- R-114 and R-112 say so; README's description of the tab says "the 16-bit original".
- The Export tab's tests name the new labels and sentences; the test that checks the 16-bit
  original is no warning in the report looks for the new words.
- Appendices E and F remain, with the Culoare tab and the parameter card.
