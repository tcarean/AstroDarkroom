# Explain the Exposure, Histogram, Synthetic and Split tabs in plain words

Approved in chat on 2026-11-01, the third of the series (ADR 0082 for End to end, ADR 0083 for
Structure): the owner approved the texts one by one in Romanian and said yes to changing the
sentences the console shares with the tabs, which recaptures the parity goldens for those text
lines. The branch starts from `claude/Explain-the-Structure-tab-plainly`, whose requirement R-122
and tests this one continues; it merges after it.

## What changes

- Exposure: the no-raw refusal, the sky-flat switch note and the note under the deciding figure
  (`SessionMessages.NoRawFlats`, `ExposureView.xaml`), the histogram guide
  (`SessionMessages.HistogramGuideIntro/Rows`); shared with the console (`ExposureReport`): the
  explanation under the histograms, the sensor-vs-screen paragraph, the channels note, the axis
  legend, the verdict lines, the "not a raw flat" block.
- Histogram: the guide (`SessionMessages.HistEqGuideIntro/Rows`); shared (`HistEqReport`): the
  informative caveat, the master note, the rainbow legend, the view descriptions.
- Synthetic: the tab's intro and the crop notes (`SessionMessages`); shared (`SyntheticReport`):
  the caveat, the four depth notes, the format line; the comparison with the master (`AgainstRun`,
  shared with `--against`): the meaning, the row notes, the verdict lines.
- Split: shared (`SplitReport`): the row notes and the meaning under the table. The intro and the
  phase messages stay, already plain.

The words dropped: median, distribution, distribution function, CFA, Bayer, gamma, clipping, SNR,
radial, gradient, sector, tile, rank, polynomial, extrapolation, achromatic, chromatic. In their
place: the typical level, the middle of the frame, the colour channels, burnt, saturation, a
usual vignetting symmetric about the centre. The labels stay ("mediana globala", "CENTRU 20 %",
"offset centru iluminare", the test names).

## Steps

1. R-123 for the four tabs, with vocabulary tests over the guides and the shared sentences; the
   tests that quote the old sentences change with it.
2. The texts, both languages.
3. Goldens: `GoldenOutputTests|EnglishOutputTests`, then `ParityGoldenTests` with the owner's yes
   (given); every changed line tied to a text above, no figure line changed.
4. README's tables of the histogram and the equalised-picture guides, CHANGELOG (Changed), an
   ADR, `python-parity.md`'s list of recaptures.
