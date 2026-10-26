# 0064. The End to end tab explains itself in place, with the thresholds read from Th

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The End to end tab shows a verdict, four series figures, a table with one row per frame and a
map, and said nothing about what any of them was: the owner, reading it, asked what the series
summary meant, what the picture at the bottom right was and what the table's columns held.
The window's other tabs explain themselves through four folded guides of one shape (a
`Disclosure` toggle, an intro and rows of heading + text, the text in `SessionMessages` as
`T(en, ro)` pairs, mirrored in the README by hand); ADR 0036 gave End-to-end the legend only,
because its map is the fitted radial term and the shape guide of the residual map does not
apply to it. The first proposal was a fifth folded guide. The owner asked for something else:
each explanation beside the thing it explains.

## Decision

The tab explains itself **in place**, in three places (R-105):

- under each of the four series figures, a line in the `Faint` style saying what the figure is
  and how it is judged, placed in the label column only (one column wide), so that it wraps
  there and never runs under the value;
- a tooltip on each of the four column headers of the frames table (frame, radial, sky, tiles),
  with a transparent background on the header so the whole header is hit-testable and a 30 s
  `ToolTipService.ShowDuration`, because a paragraph does not read in WPF's five seconds;
- under the map and its legend, a paragraph saying what the picture is (the series' mean of the
  fitted radial term, no star and no nebula in it) and what its colours mean, with the sign
  convention of the figures: blue corners are a background that falls toward the edges, the
  positive sign, under-correction.

The texts live in `SessionMessages`, like the guides', and are exposed by `LightTabViewModel`
as plain properties that depend on nothing the run produced: the same before a run, on a
refusal (where only the tooltips are on screen, the figures being hidden) and after a reset.
Every threshold they quote is formatted from `Th` (`LightRadOk`, `LightRadBad`,
`LightSeriesMinFrames`, `LightSeriesSemMax`, `LightSeriesSkyCorr`, `LightSeriesSkySpanMin`,
`LightPlaneWarn`), and the series pattern from `SeriesFinder.DefaultPattern`: the rule
"Ratios" says thresholds are never literals in text, and a recalibration must not leave a
stale figure on screen. The three conditions of the series verdict (ADR 0021) are told where
they bite: the frame count under the mean, the error of the mean under its figure, the
correlation and its span gate under theirs.

ADR 0036's line "End-to-end gets the legend only" stays true for what it was about, the shape
guide of the residual map, which still does not apply to this map; this ADR adds the
explanations of the figures, which are documentation, not a classifier, like the guides.

## Alternatives rejected

- **A fifth folded guide, like the other tabs.** It would have put the explanation of a figure
  a screen away from the figure and behind a click; the owner asked for the explanations beside
  what they explain, and a tooltip on a header costs no space at all.
- **Two panels, one under the summary and one under the map.** The right column is already
  shared by the table and a map capped at 220 px; a panel there would have squeezed the table.
- **Typing the thresholds into the texts.** The README does, by hand; the window's texts are
  code and can read `Th`, so they do, and `R105_TheThresholdsInTheTextsAreReadFromTh` fails if a
  figure is retyped.
- **A note that runs the full card width, under the value too.** The owner asked that it stop at
  the label column: the value keeps its own column, and the eye finds it on the right as
  before.
- **Putting the texts in the core's `Report/`.** They are documentation of the window's layout
  ("the figures on the left", "the series summary"), not a diagnosis; the console prints its
  own explanations in `LightSeriesReport` and does not have these elements.

## Consequences

- `SessionMessages.Light*Note` / `Light*Tip` (nine texts), `LightTabViewModel`'s nine
  properties, `LightView.xaml`'s summary grids with a second row, the header tooltips and the
  map note; the README's window section mirrors them by hand.
- Guards: `LightExplanationsTests` (both languages, no diacritics, thresholds from `Th`, blue
  corners read as under-correction, the same texts on a refusal and after a reset, the four
  tooltips in column order, the notes one column wide in the label column, the map note in a
  row below the legend); `WindowLanguageTests` collects tooltips too, so the English screen
  check covers them.
- Source note: docs/design/two-applications.md.
