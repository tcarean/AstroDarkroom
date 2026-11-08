# 0084. The Exposure, Histogram, Synthetic and Split explanations are in plain words, the same in the console

Date: 2026-11-01
Status: accepted 2026-11-01; the words the typical level superseded by 0093

## Context

The third step of the series (ADR 0082 for End to end, ADR 0083 for Structure): the owner asked
for the same on the four remaining tabs. Their explanations were the method's: the median of the
central window, a distribution and its distribution function, CFA positions and Bayer filters,
gamma encoding, clipping, SNR, a radial model, gradients, sectors, tiles, ranks, a polynomial and
its extrapolation, an achromatic profile. Many of those texts are the console's too
(`ExposureReport`, `HistEqReport`, `SyntheticReport`, `AgainstRun`, `SplitReport`); the rest are
the window's guides and notes (`SessionMessages`, `ExposureView.xaml`). The owner approved the
Romanian texts one by one and said yes to changing the console's too.

## Decision

Every explanation of the four tabs is in plain words: the typical level, the middle of the frame,
the colour channels, burnt pixels and saturation, a usual vignetting symmetric about the centre.
R-123 carries that as a requirement, and `R123_` tests check the words of the method are absent
from the guides and from the shared sentences that need no analysed frame, in both languages, as
whole words. Three words stay on purpose, because the reader must know them: the file formats
(JPEG, raw, FITS), the view named Mosaic, and "debayered" in brackets after "processed", once, in
the refusal of a processed file. The labels stay: "mediana globala", "CENTRU 20 %", the test
names, the four depth points of the synthetic flat. The texts that were already plain stay as
they were: the refusals and errors, the lens list, the split's intro and phase messages, the
zoom row of the equalised picture's guide.

The texts shared with the console change in the console too, one voice for one verdict (ADR
0082's reason); the parity goldens that print them were recaptured with the owner's yes for
their text lines alone, every figure unchanged (`python-parity.md` lists them).

## Alternatives rejected

- **Keeping "median" and "clipping" as the standard terms.** They are standard for someone who
  knows the method; the tab's reader asks what the number means, and "the typical level at the
  middle of the frame" and "burnt" answer that where "median" and "clipping" do not.
- **Keeping the technical explanation of the camera's screen (gamma, compressive curve).** The
  reader needs the consequence, that the screen is a one-way alarm, not the encoding.

## Consequences

- The `expo_*`, `histeq_*`, `synthetic_*`, `against_*`, `split_*` goldens, their English
  counterparts and the parity goldens that print those sentences carry the new texts. A C#-only
  call (ADR 0047).
- The README's tables of the histogram guide and of the equalised picture's guide quote the new
  English texts.
