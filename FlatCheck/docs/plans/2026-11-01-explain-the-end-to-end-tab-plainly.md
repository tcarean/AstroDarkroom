# Explain the End-to-end tab in plain words

Approved in chat on 2026-11-01, after the discussion of what `--light` measures and how sure it is
(the Veil series recorded on `claude/Record-the-Veil-light-series-figures`). The owner found the
tab's explanations too technical ("to be understood by anyone") and approved, text by text, a plain
rewording in Romanian, with the English rewritten in the same register, and said yes to changing
the texts the window shares with the console, which recaptures the parity goldens for those text
lines.

## What changes

Twenty-one texts, no number and no threshold: the thresholds stay read from `Th`.

- Window only (`SessionMessages.Light*Note/Tip`, `LightView.xaml`, `SessionMessages.NoLightSeries`):
  the four lines under the series figures, the paragraph under them, the four column tooltips,
  the paragraph under the map, the refusal when `process/` holds no calibrated light.
- Shared with the console (`LightReport`, `LightSeriesReport`): the three diagnoses (OK,
  under-correction, over-correction), the two undetermined reasons (error of the mean, sky
  correlation), the closing paragraph of an undetermined series, the refusal on a wide field
  (its four paragraphs), the warning on a field at the limit, the note on an unknown field.

The words dropped: radial component, fit, plane, Pearson, standard deviation, tile (kept only as
the column's name, explained in its tooltip), clipping, degeneracy, proxy. In their place: how much
darker or brighter the corners are than the centre, and the sky.

## Steps

1. R-105 gains the plain-words clause and its new example sentence; the tests that quote the old
   sentences change with it (`LightReportTests`, `LightSeriesReportTests`, `LightTabTests`,
   `LightExplanationsTests`, plus a test that the explanations avoid the method's vocabulary).
2. The texts, both languages.
3. Goldens: `GoldenOutputTests|EnglishOutputTests`, then `ParityGoldenTests` with the owner's yes
   (given above); every changed line read and tied to a text above, no figure line changed.
4. README's table of the tab's texts, CHANGELOG (Changed), an ADR recording the choice and the
   parity recapture, `python-parity.md`'s list of recaptures, `modes-light.md` if it quotes a
   sentence.
