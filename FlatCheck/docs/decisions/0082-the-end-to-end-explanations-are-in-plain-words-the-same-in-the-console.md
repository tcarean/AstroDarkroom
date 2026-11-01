# 0082. The End-to-end explanations are in plain words, the same in the console

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

The End to end tab explains itself in place (ADR 0064): a line under each series figure, a
tooltip on each column, a paragraph under the map, and the verdict's diagnosis and reasons shared
with the console report. Those texts were written in the method's own words: radial component,
fit, plane, Pearson's correlation, standard deviation, tiles, one-sided clipping, geometric
degeneracy, proxy. Reading the tab on 2026-11-01, the owner found them too hard for someone who
does not know the method, and asked for wording anyone could follow, with the old and the new text
side by side for approval. The texts are of two kinds: eleven live only in the window
(`SessionMessages.Light*`, `LightView.xaml`, `SessionMessages.NoLightSeries`) and ten are the
console's too (`LightReport`, `LightSeriesReport`: the three diagnoses, the two undetermined
reasons, the closing paragraph of an undetermined series, the wide-field refusal, the warning at
the limit, the unknown-field note). Changing the second kind changes the console output and
eleven of the 39 parity goldens on their text lines.

## Decision

Every text of the tab is in plain words: it says how much darker or brighter the corners are than
the centre after the flat was applied, and what is sky; it names no part of the method. R-105
carries that as a clause and `R105_TheExplanationsAvoidTheMethodsVocabulary` checks the words
fit, plane, Pearson, standard deviation, clipping, degeneracy, radial component and proxy are
absent, in both languages, as whole words. The thresholds stay read from `Th`.

The texts shared with the console change in the console too, so the two applications keep one
voice for one verdict; the eleven `parity_light_*.txt` were recaptured with the owner's yes, for
their text lines alone, every figure unchanged (`python-parity.md` lists them). The column header
`tiles` keeps its name, the same as the console's; its tooltip now says what a tile is.

## Alternatives rejected

- **Plain words in the window only, the console as it was.** The diagnosis and the reasons are
  composed once and printed by both applications on purpose (ADR 0064, R-105); two wordings for
  one verdict would have to be kept in step by hand, and the console's reader is the same
  person.
- **Keeping the method's words with a gloss.** A gloss doubles the text and still asks the reader
  to learn the word; the figure is understood without it.

## Consequences

- The goldens `light_*`, `en_light_*` and the eleven `parity_light_*` carry the new sentences.
  The parity recapture is a C#-only call (ADR 0047): the Python prototype never printed these
  sentences in English, and its Romanian ones are prior art.
- `python-parity.md`'s remark that the capitalised diagnosis ("Fondul CADE/CRESTE spre margini")
  accounted for the wording-only differences is history: the diagnosis is no longer capitalised.
- The README's table of what the tab says quotes the new English texts.
