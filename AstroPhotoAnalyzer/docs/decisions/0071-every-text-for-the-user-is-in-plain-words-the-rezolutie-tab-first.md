# 0071. Every text for the user is in plain words, the Rezolutie tab first

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

The window's tabs explain themselves in place (ADR 0020): a description under each row, notes
under the tables, a guide at the foot of a tab. Those texts were written in the method's own
words: median, Q1 / Q3, interpolation, amplitude against the background, the pixel grid, the
ratio of two distances that grow alike. Reading the Rezolutie tab on 2026-11-01, the owner found
them too hard for someone who does not know the method and asked for texts anyone could follow,
on every tab, one tab at a time, with the old and the new text side by side for approval. The
same request had come for FlatCheck's End-to-end tab the same day (FlatCheck ADR 0082). The
owner also asked that every text written for the user, and every explanation given to the
owner in chat, keep that register from now on.

The Rezolutie tab's texts are of two kinds. Eleven live only in the window: the five row
descriptions, the row names that said "FWHM median", the note under the star count, the three
notes of `ResolutionView.xaml`, the dpi scale's header and three of its meanings. Three are the
console's too: the size table's legend when there are no stars and the two lines of the legend
at the natural distance (`ReportWriter.SizeTableFooter`). Changing those changes six golden
files on their legend lines, every figure unchanged.

## Decision

Every text the user reads is in plain words: it says what a figure means for the print, in
short sentences and everyday words, and names no part of the method. A technical term stays
only where the figure is known by it, and then the plain name comes first and the term follows
in brackets: the owner asked that the stars' width be written "latimea stelelor (FWHM)" wherever
it is named, since FWHM is the word Siril and the report use. The numbers stay interpolated
from `Thresholds` (ADR 0012). The rule "Plain words" in `CLAUDE.md` carries this for every tab
and for the console.

The Rezolutie tab is the first: R-115 carries the clause and
`R115_TheResolutionTextsAreInPlainWords` checks, in both languages, on a scene with stars and
one without, that the rows' names, notes and descriptions, the size table's legend and the dpi
scale use none of the words median, Q1, Q3, interpolation, amplitude, unsaturated, grid and
percentile, and that the stars' width is named with "(FWHM)". The three legend texts change in
the console too, so the two applications keep one voice for one table; the six goldens
`clean-sky-canvas`, `dark-sky-wcs` and `mono-png8`, in both languages, were recaptured for
those lines alone.

The other six tabs follow, one task each, with the same approval step.

## Alternatives rejected

- **Plain words in the window only, the console's legend as it was.** The legend is composed
  once and printed by both applications on purpose (R-115: "the size table as the report
  writes it"); two wordings for one table would have to be kept in step by hand, and the
  console's reader is the same person.
- **Dropping FWHM altogether.** The owner keeps it: it is the name the figure has in Siril and
  in the report's appendix, and a reader who knows it should find it; the plain name in front
  serves the reader who does not.
- **Keeping the method's words with a gloss.** A gloss doubles the text and still asks the
  reader to learn the word; the figure is understood without it.

## Consequences

- The six goldens carry the new legend lines; every figure in them is unchanged.
- The row names of the five zones are "Latimea stelelor (FWHM) in centru" and
  "... in coltul <x>"; the test that named "FWHM median in centru" names the new label.
- `R115_...` is the first test in this project that checks a text's register; the other tabs'
  tasks add theirs under their own requirement rows.
- The parameter card's help texts (`ParameterFields`) and the report's recommendation texts
  (`Recommendations`, R-094) still use the method's words; they belong to the Raport tab's task
  and the card's, not to this one.
