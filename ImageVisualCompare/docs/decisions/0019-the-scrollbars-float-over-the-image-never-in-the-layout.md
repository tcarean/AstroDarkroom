# 0019. The scrollbars float over the image, never in the layout, so they cannot change the viewport or the fit scale

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "Scrollbars float over the
image") and the scrollbar comment in `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml`;
the decision itself dates from a6e5f10 (the base version, 2026-09-08). The fit scale comes from
the viewport size (`Viewport.FitScale`, the smaller of the two ratios of viewport to content)
and is the zoom floor for an image larger than the viewport (ADR 0020), while a bar is needed
only when the image overflows the viewport on its axis (`Viewport.CanScrollHorizontally`,
`CanScrollVertically`). A bar placed in the layout would shrink the viewport when it appears,
which changes the fit scale, which can change whether the bar is needed.

## Decision

`VerticalBar` and `HorizontalBar` share the `Grid` cell of the image layers and are drawn over
them, against the right and the bottom edge, collapsed unless `ScrollBarStates` reports
`IsVisible`. The viewport is always the whole control (`ActualWidth` and `ActualHeight` times
the device scale), whether the bars show or not. The bars are thin, `ThinVerticalScrollBar`
9 DIPs wide and `ThinHorizontalScrollBar` 9 high, and are a thumb with no repeat buttons.

## Alternatives rejected

- **Bars in the layout, beside and below the image.** Their appearing and disappearing would
  change the viewport size and so feed back into the fit scale, which decides whether they are
  needed (`CLAUDE.md`; the `CompareView.xaml` comment).

## Consequences

- A visible bar covers a 9-DIP strip of the image near the right or the bottom edge, which is
  why it is a thumb only: "every pixel of chrome hides part of what is being compared"
  (`Theme.xaml`).
- The fit scale, and with it the zoom floor, depends only on the control's size and the canvas
  size.
- Guards: none by test; the application has to be launched and looked at (ADR 0026).
  `ScrollBarStateTests` guards the numbers the bars show (range, thumb proportion, value,
  visibility), not where they sit.

Source: CLAUDE.md ("Invariants"); src/ImageVisualCompare.Wpf/Controls/CompareView.xaml;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs; src/ImageVisualCompare.Wpf/Theme.xaml;
src/ImageVisualCompare.Core/Viewport.cs; src/ImageVisualCompare.Core/ScrollBarState.cs.
