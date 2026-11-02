# 0034. The Frames tab and the reasons are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0032 made plain words the rule for every text the user reads; the Dithering tab (ADR 0032)
and the Summary tab (ADR 0033) were rewritten first. The Cadre (Frames) tab came next. Its column
tooltips said `alungirea mediana`, `latimea mediana`, `ADU peste black`, `cadrul anterior`,
`consecutiv`; its `motive` column gave each flag in the method's words: `(prag 1.60)`,
`1.3 x mediana sesiunii`, `din mediana`, `salt de fond`, `sigma clipping o scoate`,
`neinregistrat`; the cut-outs said `pe canalul verde, fiecare intinsa pe propria scara` and
`Se decodeaza cadrul`, and the line under a cut-out gave an angle without its unit. The reasons
are also the Rejected tab's list and the console's RESPINSE section and frame table. The owner
approved the 18 proposals in chat on 2026-11-02, "in the console too".

## Decision

The Frames tab takes the approved words (R-079). A reason names the class its figure gives the
frame (`de la 1.60 cadrul e slab`, `de la 1.35 cadrul e la limita`), compares the stars' width and
count with the session's usual ones, calls a background jump the sky jumping against its
neighbours, a satellite or plane trail one Siril takes out when stacking (its Romanian plural
`dare`, which read `dara` for any count), and an unregistered frame one that could not be
aligned. The reasons change wherever they are printed, the Rejected tab and the console
included. The column titles, the class words and the cells with figures stay. The words prag,
consecutiv, decodeaza and decoding join the one list of `PlainWords`.

## Alternatives rejected

- **The window only.** The reasons are composed once (ADR 0008, ADR 0012) and the console's
  reader is the same person; ADR 0032 rejected the same split.
- **Keeping `sigma clipping` by name.** It is the name of Siril's step, but the reader needs to
  know only that the stack takes the trail out; the word `sigma` is the method's.

## Consequences

- `R079_TheReasonsAvoidTheMethodsVocabulary` (the reasons and cut-out lines from the core, both
  languages) and `R079_TheFramesTabUsesNoneOfTheMethodsWords` (the rendered tab with its
  tooltips and cut-outs, both languages) check it.
- The goldens `series` and `series-classes` and their English twins change on their RESPINSE
  lines and the reasons of their frame tables; every number in them is unchanged.
- The Quality and Rejected tabs follow; the Rejected tab keeps its explanation paragraphs and
  heading until its own task.
