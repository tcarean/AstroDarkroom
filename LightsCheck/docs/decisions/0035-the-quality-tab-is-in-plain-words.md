# 0035. The Quality tab is in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0032 made plain words the rule for every text the user reads; the Dithering (ADR 0032),
Summary (ADR 0033) and Frames (ADR 0034) tabs were rewritten first. The Calitate (Quality) tab
came next: four charts of the night, frame by frame, each under a heading and a note in the
method's words (`FWHM MEDIAN`, `ALUNGIREA MEDIANA`, `STELE DETECTATE`, `ADU peste black`), the
notes listing causes without saying what the figure is. Nothing said that a point is a frame, that
the numbers along the bottom are the frames', nor that the colour of the first two charts' points
is the frame's class. These texts are the window's only: the console has no such section. The
owner approved the eight proposals and one new line in chat on 2026-11-02 ("Merg pe
recomandarile tale").

## Decision

The Quality tab takes the approved words (R-080). Each heading names its figure as the glossary
does (`latimea stelelor (FWHM)`, `cat de alungite sunt stelele`, `stele gasite` as in the DEW row,
`fondul cerului ... peste nivelul de negru`), and each note says first what the figure is in each
frame, then what moves it. A new line above the four charts says that each point is a frame, in
the order they were shot, and what the colours of the first two charts mean. The elongation note
names the yellow and red dotted lines by their class and gives no figure: the lines take theirs
from `Th`, and a figure in the XAML would be a literal that a change there leaves behind. The
guide labels (the class words) and the axes' numbers stay.

## Alternatives rejected

- **Naming the thresholds in the elongation note** (`peste 1.35`, `peste 1.60`). The note would
  hold two literals beside lines drawn from `Th`; the class words on the lines already say which
  is which.
- **No line above the charts.** The axes carry only numbers; without it the reader has to guess
  that they are the frames and that green, yellow and red are the classes.

## Consequences

- `R080_TheQualityTabUsesNoneOfTheMethodsWords` renders the tab in both languages, holds its texts
  and the guides' labels against `PlainWords`, and checks the new line.
- No golden changes: the console prints none of these texts.
- The Rejected tab follows; its explanation paragraphs keep their words until its own task.
