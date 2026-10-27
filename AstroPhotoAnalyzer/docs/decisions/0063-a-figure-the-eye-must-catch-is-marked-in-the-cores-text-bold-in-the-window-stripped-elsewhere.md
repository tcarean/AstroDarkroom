# 0063. A figure the eye must catch is marked `**` in the core's text: bold in the window, stripped everywhere else

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

On 2026-10-27 the owner asked for the share of the frame with a visible plateau and its
threshold (ADR 0060) to be in bold in the banding line of the window's Raport tab. That line is a
`Recommendation.Because`, a string the core writes (the rule "Computed only in the core") and
three places show: the text report, the Raport tab, and, for the paper checks, the evidence rows
of the Editare Curbe tab. The window cannot find the figures in the text by itself without
deciding what matters, which is diagnosis. Recorded at the wrap-up of the same day: the choice
was made while implementing the owner's request and had no ADR of its own.

## Decision

The core marks a part of a `Because` with `Fmt.Strong`, `**part**`, its one piece of markup. The
Raport tab renders the marked parts in bold through the attached properties `Emphasis.Label` and
`Emphasis.Text` of its "fiindca" block; the label is a property and not the block's content
because a XAML attribute is applied before the element's content, which put the label after the
text. The text report (`ReportWriter`) and the curves tab's evidence rows
(`CurveTexts.EvidenceRows`) strip the marks with `Fmt.Plain`, so the console's text and the golden
files read as before. An unclosed mark is shown as text. Today only the banding line uses it.

## Alternatives rejected

- **A structured `Because`**, a list of runs with an emphasis flag: every recommendation, the
  report writer and the window would change for one line's two figures.
- **The window finding the figures**, a pattern on percentages: a decision about what matters,
  taken outside the core.
- **Markdown or XAML in the text**: a richer syntax than one mark needs, and every plain-text
  reader would have to strip more of it.

## Consequences

- A new `Because` that marks a figure needs nothing else; a new place that shows a `Because` as
  plain text must pass it through `Fmt.Plain`.
- Guards: `EmphasisTests` (R-112, the bold runs after the label), `RecommendationsTests` (R-091,
  the marks in the banding line and none in the report body), `CurveTests` (R-112, the curves
  evidence strips them), the golden files.
