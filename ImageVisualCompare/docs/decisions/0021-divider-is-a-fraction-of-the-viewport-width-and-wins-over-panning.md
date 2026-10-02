# 0021. The divider is a fraction of the viewport width, in DIPs; the line is grabbed anywhere along it and wins over panning

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `src/ImageVisualCompare.Core/DividerModel.cs` (the
summary, the constants and the `HitTest` comment), `src/ImageVisualCompare.Core/DividerLayout.cs`,
`CompareView.OnMouseLeftButtonDown` (its comment) and `OnKeyDown` in `CompareView.xaml.cs`, the
`DividerHandle` comment in `CompareView.xaml`, and `README.md` ("Controale"); the decision
itself dates from a6e5f10 (the base version, 2026-09-08). A before/after slider has two
gestures on one surface, moving the line and panning the pair, and this one has no mode button
to choose between them. In the base version the handle's size was written twice, as
`HandleWidth` and `HandleHeight` in `DividerModel` for the hit test and as `Width="26"` and
`Height="56"` in the XAML for the drawing; 801bda5 (refactoring and new tests, 2026-09-08)
added the layout record `DividerLayout` and took the size out of the XAML.

## Decision

- `DividerModel.Position` is a fraction of the viewport width, 0 at the left edge and 1 at the
  right, clamped to that range; it starts at 0.5, and `SetPair` puts it back there for every
  new pair. It is screen space, not content space: the line stays where the user put it while
  the pair is zoomed and panned under it, since "the line belongs to the window, not to the
  image". Unlike the viewport (ADR 0011) it is measured in device-independent pixels
  throughout, because it is a fraction of the window and never touches image coordinates.
- `DividerModel.Layout` returns a `DividerLayout`: the line's x, the clip rectangle of image A
  (everything left of the line, at full height) and the handle, `HandleWidth` 26 by
  `HandleHeight` 56 DIPs, centred on the line and on the height. `CompareView` sizes and places
  the drawn handle from it.
- `HitTest` takes a press within `LineGrabTolerance` (8 DIPs) of the line, anywhere along its
  height, or inside the handle's rectangle. `OnMouseLeftButtonDown` asks it first: a hit drags
  the divider, anything else pans.
- The left and right arrow keys nudge the line by 1 DIP, with `Shift` by 10 (`NudgeBy`).

## Alternatives rejected

- **The position in content space.** The line would travel with every pan and zoom; a
  before/after slider keeps it where the user put it (the `DividerModel` summary).
- **A mode button to switch between moving the line and panning.** With the divider first, "the
  line can be adjusted without first having to aim at a mode button" (the
  `OnMouseLeftButtonDown` comment).
- **The handle's size in the XAML**, as in the base version. Set from `DividerModel.Layout`,
  "the hit test and the drawing cannot disagree about how big the handle is" (the
  `DividerHandle` comment).

## Consequences

- A press within 8 DIPs of the line, or on the handle, never starts a pan; anywhere else on the
  surface it pans.
- The line, the clip of image A and the hit test all derive from `DividerModel.ScreenX`, so what
  is shown left of the line and where the line can be grabbed cannot disagree; the clip goes on
  the layer (ADR 0013).
- The grab tolerance and the README's "1 px" and "10 px" for the arrow keys are DIPs: on a
  display scaled to 150 % a nudge moves the line 1.5 physical pixels.
- Guards: `DividerModelTests` (the fraction and its clamping, the start and the reset, the
  tolerance along the whole line, the wider handle target), `DividerLayoutTests` (the clip, the
  handle centred on the line and on the height, the handle drawn is the handle the hit test
  accepts). The precedence over panning and the keys live in `CompareView`, with no test: the
  application has to be launched and looked at (ADR 0026).

Source: src/ImageVisualCompare.Core/DividerModel.cs; src/ImageVisualCompare.Core/DividerLayout.cs;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs (`OnMouseLeftButtonDown`, `OnKeyDown`,
`SetPair`, `UpdateOverlay`); src/ImageVisualCompare.Wpf/Controls/CompareView.xaml
(`DividerHandle`); README.md ("Controale"); the diff of 801bda5.
