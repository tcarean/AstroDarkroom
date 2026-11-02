# 0032. Every text for the user is in plain words, the Dithering tab first

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The Dithering tab explains itself in place: a caption under the map and under each chart, the
DITHERING card's rows and tables, and five paragraphs under them (R-045, R-047). Those texts
were written in the method's own words: median, sigma, Mpx, registered frames, coverage, radius,
distinct positions, isolated frames, span, consecutive frames, a correlated noise, the threshold.
Reading the tab on 2026-11-02, the owner found them too hard for someone who does not know the
method. They asked for texts anyone could follow, on every tab, one tab at a time, Dithering
first, with the old and the new text side by side for approval; and that every text written for
the user, and every explanation given to the owner in chat, keep that register from now on. The
same request came for AstroPhotoAnalyzer (its ADR 0071) and FlatCheck (its ADR 0082). The owner
approved the thirty proposals the same day, "in the console too": the DITHERING card's rows are
the console's DITHERING section, and four Summary rows share pieces with them (the axis line,
the verdict, the run behind the walking-noise streak).

Reading the texts again found one sentence wrong. Paragraph 2 said that at 20 mm a drift of
3"/min "stays under one pixel over the whole session". With the D3300's 3.92 um pixel, 3"/min
over a 2-minute exposure is 6": 1.0 px at 5.99 "/px (135 mm) and 0.15 px at 40.4 "/px (20 mm).
That is under a pixel per exposure, not per session: an hour without a dither adds up to several
pixels. The Smida session at 20 mm measured 0.17 px per exposure (V-013).

## Decision

Every text the user reads is in plain words: it says what a figure means for the frames and the
stack, in short sentences and everyday words, and names no part of the method. A term stays where
the astrophotographer knows the figure by it: dithering, walking noise, drift, dark, flat, stack,
fixed pattern, FWHM, ADU, RA, DEC, plate solve; so do the verdict words on the chips. The
numbers stay read from `Th`. A rewrite is shown to the owner first, the original and the
proposal side by side, and made only on their yes. The rule "Plain words" in `CLAUDE.md` carries
this for every tab, for the console and for the chat with the owner.

The Dithering tab is the first. R-077 carries the clause, the words it bans and the terms it
renames: the frames the registration placed are `aliniate` (`aligned`), the coverage is
`apropierea cadrelor` (`how close the frames are`), the distinct positions `locuri diferite`
(`different spots`), the isolated frames `cadre singure` (`frames alone`), the span `zona
pozitiilor` (`area of the positions`), and `pixeli fierbinti` becomes the `pixeli calzi` of
every other row. Paragraph 2 gives the computed figures above instead of the wrong sentence.
The texts change in the console too, so the two applications keep one voice, and the Summary
rows that share a piece take its new words with it.

## Alternatives rejected

- **Plain words in the window only, the console as it was.** The rows are composed once and
  printed by both applications on purpose (ADR 0012); two wordings for one figure would have to
  be kept in step by hand, and the console's reader is the same person.
- **Keeping the method's words with a gloss.** A gloss doubles the text and still asks the reader
  to learn the word; the figure is understood without it.
- **Leaving paragraph 2 for a fix of its own.** It was being rewritten anyway, and a plain
  sentence that is wrong misleads more than a hard one.

## Consequences

- `R077_TheDitheringTextsAvoidTheMethodsVocabulary` (the core's rows, tables and paragraphs, in
  both languages, over four sessions) and `R077_TheDitheringTabUsesNoneOfTheMethodsWords` (the
  rendered tab, in both languages) check the banned words, from one list.
- The goldens `series`, `en_series`, `series-classes` and `en_series-classes` carry the new words
  on the DITHERING rows and on the Summary, TRAILED FRAMES and DRIFT rows that share them; every
  number in them is unchanged.
- The glossary of `docs/design/language.md` and the README follow.
- The other tabs (Summary, Frames, Quality, Rejected) follow, one task each, with the same
  approval step; until then they keep their words, `neinregistrat` on the Rejected tab among
  them.
