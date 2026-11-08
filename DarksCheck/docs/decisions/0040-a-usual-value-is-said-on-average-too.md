# 0040. A usual value is said on average too

Date: 2026-11-08
Status: accepted 2026-11-08
Supersedes: 0030 in part (the word de obicei for the median of the frames); 0035 in part (the usual of check 4's verdict)

## Context

ADR 0035 made a typical value `medie` / `average` wherever a text called it `obisnuit` (`usual`
or `typical`), and left check 4's verdict as it was: its Romanian says `de obicei`, which the
owner had not asked to change. On 2026-11-08 the owner asked for every text the user reads to
be searched for `obisnuit`, `typically` and `usually` in the sense of "on average", and for them
to say `medie` / `average` instead, since anyone understands an average. No `obisnuit` and no
`typical` was left in those texts; `usually` / `de obicei` stood in nine. Shown the nine, original
and proposal side by side, with eight to change and one to keep, the owner answered "Da, cele 8".

## Decision

1. **A typical value is never `de obicei` / `usually` either**: it is `in medie` / `on average`,
   or `mediu` / `average` beside a noun, as ADR 0035 set. The eight texts:
   - check 4's `against the master` row: `(in medie 1.43)` / `(on average 1.43)`;
   - check 4's verdict, OK and WARNING: `zgomotul de peste 1.5 ori zgomotul mediu` / `noise more
     than 1.5 times the average noise`, the words of the Cadre tab's guide line;
   - check 5's `how hot` row: `in medie 120 ADU-eq` / `on average 120 ADU-eq`;
   - check 9's `not in the master` row: `acolo au in medie 40 ADU-eq` / `there they have 40
     ADU-eq on average`;
   - check 9's `lights against dark` row: `pixelii fierbinti sunt in medie de 1.00 ori cat in dark` /
     `the hot pixels are on average 1.00 times what they are in the dark`;
   - the ADU scale tab's `cam cat greseste in medie un flat` / `about as much as a flat is off on
     average` and `cat difera in medie un cadru de celelalte` / `what a frame differs from the
     others on average`;
   - the hint of the Lights tab's `noise` column: `cat difera in medie un pixel de vecinii lui` /
     `how much a pixel differs on average from its neighbours`.
2. **`De obicei` that means most times stays.** The Lights chart's `Primele lights ale noptii ies
   de obicei mai jos` / `The first lights of the night usually sit lower` says what happens on
   most nights, not an average of a figure.
3. **The figures do not change.** As in ADR 0035, most are middle values (the median of the
   frames or of the pixels), not means.
4. **The tests hold it**: the word lists of R-081 (the ADU scale tab) and R-082 (the summary,
   checks 9 and INDEX included) take `de obicei` and `usual`, so a text that says them again fails.
   The window tabs' lists (R-083 to R-088) do not, since the Lights chart keeps its `de obicei`.

## Alternatives rejected

- **Changing the Lights chart's sentence too** (`ies in medie mai jos`): it does not speak of an
  average, and would read oddly; the owner chose the eight.
- **`media` alone in check 4's verdict** (`mai mare decat media`): `media` stays the name of a way
  Siril makes a stack (ADR 0028), and the Cadre tab already says `zgomotul mediu`.

## Consequences

- R-081 and R-082 name `de obicei` and `usual`; R-052's example quotes `in medie`.
- The goldens with checks 4, 5 and 9 take the new words and change nothing else; the README's
  example of check 9 follows.
