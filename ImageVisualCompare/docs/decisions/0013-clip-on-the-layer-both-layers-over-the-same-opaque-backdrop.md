# 0013. The clip goes on the layer, never on the image; both layers draw over the same opaque backdrop: same brush, size and transform

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "The clip goes on the layer,
never on the image" and "Both layers need the same opaque backdrop"), the two backdrop comments
in `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml` and the comment in
`CompareView.UpdateOverlay`; the decision itself dates from a6e5f10 (the base version,
2026-09-08) for the clip, e0ab0b3 ("bug fix", 2026-09-08) for the backdrop under image A, and
861f5c1 (image padding, 2026-09-09) for the backdrop under B and the shared
`ImageBackdropBrush`. Image A is drawn over image B and clipped to the left of the divider, and
the clip has to stay put on screen while the images pan under it. Without a backdrop, a file
with an alpha channel blended into B on the left while B blended into the background on the
right: two halves composited over different grounds, "exactly the artefact this tool exists to
rule out". e0ab0b3 gave A a rectangle of `CanvasBrush`, the colour B was shown over, and
`CLAUDE.md` as 591f433 wrote it recorded the check: a 50 %-alpha red over an opaque blue read
137,10,13 on the left (A over the canvas colour), not 128,0,127 (A over B). Padding then made
the canvas larger than an image, and without a rectangle of its own the gap beside B would have
shown the control background instead of black, so 861f5c1 gave B the same rectangle and both the
black brush; `CLAUDE.md` has given the reading as 128,0,0 since.

## Decision

- The clip of image A is set on `BeforeLayer`, the `Canvas` that fills the viewport, never on
  `BeforeImage`: `UpdateOverlay` assigns it a `RectangleGeometry` of `DividerLayout.ClipRect`,
  everything left of the line, in DIPs (ADR 0011).
- Each layer draws an opaque `Rectangle` under its image, `AfterBackdrop` and `BeforeBackdrop`,
  alike in everything: the same brush, `ImageBackdropBrush` (`#000000` in `Theme.xaml`); the
  same size, the shared canvas (`pair.PixelWidth` x `pair.PixelHeight`, set in `SetPair`); the
  same place, the canvas origin; and the same transform instance as the images (ADR 0010).
  `SetPair` shows both and `Clear` collapses both.

## Alternatives rejected

- **The clip on `BeforeImage`.** A clip on the `Image` lives in image coordinates and moves with
  every pan; a before/after slider needs it fixed on screen.
- **No backdrop under A**, as in the base version. A pixel with alpha is then composited over B
  on the left and over the background on the right; the 50 %-alpha check reads 128,0,127 on the
  left, A over B.
- **A backdrop under A only, in the canvas colour**, as e0ab0b3 had it. Padding needs black,
  which reads as absence of image where the canvas colour reads as part of the chrome (the
  `ImageBackdropBrush` comment in `Theme.xaml`), and a black rectangle under A alone would leave
  B over the control background: the gap beside a padded B in the wrong colour, and the two
  halves over different grounds again.

## Consequences

- Moving the divider changes only the clip, and zoom and pan change only the transform; neither
  touches the other.
- Transparency in either file is shown over black on both sides of the line, never over the
  other image; the same black fills the padding of a mismatched pair (ADR 0014).
- Guards: none by test (`DividerLayoutTests` pins the clip's rectangle, not where it is
  applied); the 50 %-alpha check (left half 128,0,0, not 128,0,127) was made by hand, and the
  application has to be launched and looked at (ADR 0026).

Source: CLAUDE.md ("Invariants", "The clip goes on the layer, never on the image" and "Both
layers need the same opaque backdrop"), and as 591f433 wrote it;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml (the two backdrop comments);
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs (`UpdateOverlay`, `SetPair`, `Clear`);
src/ImageVisualCompare.Wpf/Theme.xaml (`ImageBackdropBrush`); the diffs of e0ab0b3 and 861f5c1.
