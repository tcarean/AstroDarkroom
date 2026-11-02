# 0033. The Summary tab is in plain words, the same in the console

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0032 made plain words the rule for every text the user reads and rewrote the Dithering tab
first; the other tabs follow one at a time, each from an original / proposal list the owner
approves. The Rezumat (Summary) tab came next. Its seven cards are the console report's session
lines and sections, written in the method's words: `FWHM median` and `mediane`, `penalizare`,
`integrare`, `coerenta`, `arcmin`, `eroare de cel putin 4.6'` and `costa ... px` for the polar
alignment, `consecutive` and `neinregistrate`, `>=` and `..` in the labels. The owner approved
the 25 proposals in chat on 2026-11-02, "in the console too", and asked for the stars' width to
read `latimea stelelor (FWHM)`, as AstroPhotoAnalyzer writes it (its ADR 0071).

## Decision

The Summary tab and the console's session lines and sections REZUMAT, FOCUS, EXPUNERE SI ZGOMOT,
ROUA, CADRE MISCATE and DRIFT SI ROTATIE take the approved words (R-078): the stars' width
(FWHM), `de obicei` for the median, `expunere totala` for the integration, `zgomot in plus` for
the penalty, the light around the stars for the halo, the polar error as how far the mount's
axis is off the pole and its cost as how far it moves the stars, angles in `'`, frames `la rand`
and `aliniate`. The card titles, the chip words and every figure stay. The `--inventory` header
is the same session lines, so it changes with them, and the noise legend gets a label of its own,
`ce inseamna` (`what it means`), instead of `scara`, which the session's scale line keeps. The
test helper `PlainWords` holds one list of the method's words for every tab; the new words join
it, and the Dithering texts pass it as they did.

## Alternatives rejected

- **The window only, the console as it was.** The rows are composed once and printed by both
  applications (ADR 0008, ADR 0012); two wordings would drift apart, and ADR 0032 rejected the
  same split for Dithering.
- **One list of method words per tab.** A word that is the method's on one tab is the method's
  on every tab; one list keeps the check from forgetting a tab's words on the next.

## Consequences

- `R078_TheSummaryTextsAvoidTheMethodsVocabulary` (the core's session lines and rows, both
  languages, over sessions that reach the verdicts' branches) and
  `R078_TheSummaryTabUsesNoneOfTheMethodsWords` (the rendered tab, both languages) check it.
- The goldens `inventory`, `series` and `series-classes` and their English twins change on their
  session lines and their Summary sections' lines; every number in them is unchanged.
- The Frames, Quality and Rejected tabs follow, one task each.
