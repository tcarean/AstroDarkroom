# 0046. The Tonuri histogram draws the visibility threshold as a second line, amber and dashed, in the preview's colours

Date: 2026-10-24
Status: accepted 2026-10-24

## Context

The Tonuri tab's L\* histogram marked one tone, paper black, as a red line, with a heading and
a note that spoke of "the line". Since ADR 0029 the tab judges a second tone, the visibility
threshold `Thresholds.LowSeparationLstar` (L\* 15): the row "Sub L\* 15 (separare slaba)", the
simulation's `cer sub L* 15` mark, the preview window's amber overlay. The owner asked for that
threshold on the histogram too, as a line of another colour or dashed, with the texts rewritten
for two lines.

Two facts shaped the choice. The preview window already paints the same two classes, red for
"sub negrul hartiei" and amber for "sub L\* 15, peste negru" (`PreviewWindowViewModel`). And
the two lines can come in either order: on baryta (Dmax 2.4) paper black is L\* 3.6, left of
the threshold; on canvas (Dmax 1.5) it is L\* 20.7, right of it; at a Dmax near 1.9 the two
nearly coincide.

## Decision

1. The second line is amber (the theme's `WarnBr`) and dashed; the paper-black line stays red
   (`BadBr`) and solid. One colour means one thing across the window: red is under paper
   black, amber is under the visibility threshold. The dash tells the two apart where the
   colours alone would not, on a paper whose black sits near L\* 15.
2. `BarsCanvas` gets a generic `SecondMarker` and `SecondMarkerBrush`, drawn dashed; it keeps
   knowing nothing about the core. The position comes from the view-model's `LowSeparation`,
   set to `Thresholds.LowSeparationLstar` with the result and NaN when cleared, so an empty
   chart has no line.
3. The heading and the note under the chart move from XAML literals into the view-model
   (`HistogramHeading`, `HistogramNote`), because they now carry the number 15, which the rule
   "Explanation is not diagnosis" wants interpolated from `Thresholds`.
4. Each sentence of the note says what lies left of one line ("La stanga liniei rosii ...",
   "La stanga liniei galbene intrerupte ...") and never which line is left of the other, so the
   same text is right on baryta and on canvas; the threshold's sentence speaks only of the
   tones the paper still separates, because part of what lies under the threshold is under
   paper black too, on every paper. The line is called "intrerupta" (dashed), as it is
   drawn, not "punctata".

## Alternatives rejected

- **A second line in the same red, solid.** On canvas the reader could not tell which line is
  paper black.
- **A shaded band between the two lines** ("the paper separates, the eye barely"). The band is
  empty on a paper whose black is at the threshold and inverted on canvas, where the region
  under paper black lies inside the region under L\* 15; two "left of" lines carry the same
  information without that trap.
- **Texts that describe the order** ("intre linia rosie si cea galbena"). Wrong on canvas.

## Consequences

- R-113 names both lines and the order-independent wording;
  `R113_TheHistogramMarksPaperBlackAndTheVisibilityThresholdInEitherOrder` checks the two
  positions on baryta and on canvas and that the texts are the same strings on both.
- `docs/design/window.md` describes the new members; the README's Tonuri sentence names the
  second line.
- The code review of this task found both lines half a bin left of their value: the bins are
  centred on whole L\* values (`TonalAnalyzer` rounds), and `BarsCanvas` put a marker at the
  left edge of its bin. A marker now falls at the centre of its bin, the paper-black line
  moved with it, and `R113_TheCanvasDrawsBothLinesAtTheCentreOfTheirBin` reads the rendered
  columns back.
