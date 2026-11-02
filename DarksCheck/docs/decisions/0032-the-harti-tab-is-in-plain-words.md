# 0032. The Harti tab is in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time; the ADU scale tab, the
Rezumat (ADR 0030) and Cadre (ADR 0031) are done. The owner picked the Harti tab next. It shows the
three maps of check 2, each with its title and, under it, the Rezumat's row of the same map, which
ADR 0030 already put in plain words. Its own texts are three: the lead above the maps, which named
the method (`planul de referinta`, `percentilele 1 si 99`, `citire`); the third map's title, `DARK
- BIAS`; and the message of a missing map, `Harta nu are blocuri (...) sau grupul lipseste`. The
owner approved the plan's list of the three, shown with their real texts, with "Da, toate".

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words.
2. **The lead says what the maps show and how they are drawn**: the frame in squares of 32 px,
   the dark current with its meaning beside it (`ce aduna senzorul din caldura lui`), each map's
   own contrast as what it does to the squares (`cel mai intunecat 1 % din patrate e negru, cel mai
   luminos 1 % e alb`), and the readout as `citirea senzorului`. It names no pixels: the row under
   each map does (`pixelii verzi G1`).
3. **The third map is titled `DARK MINUS BIAS`**, in the Rezumat's words (`pe dark minus bias`).
   The row under it keeps its label `dark - bias`, by which the window finds it (ADR 0030).
4. **A missing map says why in the user's terms**, the dark master's `Nu e harta: cadrele sunt mai
   mici de 32 px.`, the two bias maps' `Nu e harta: sesiunea nu are bias-uri care sa se poata
   folosi, sau cadrele sunt mai mici de 32 px.` The list's one message for all three named the
   biases under the dark master and said "no biases" for biases that could not be used; the review
   found it and the owner approved the two.
5. **R-084 guards it**: `R084_...` renders the tab in both languages, with the word list R-083's
   tests use, which now also holds `percentila` / `percentile` and `planul... de referinta`.

## Alternatives rejected

- **Naming the pixels in the lead** (`Hartile pixelilor verzi G1 ...`). The lead is a fixed text of
  the window and the pixels belong to the series; the row under each map names them already.

## Consequences

- R-084 is new; R-071's description of the Harti tab holds.
- The README's window section names the third map `dark minus bias`.
