# 0020. The zoom runs from the fit scale, or 100 %, to 1600 %; the wheel zooms at the cursor, the buttons step a ladder

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `README.md` ("Controale" and the two bold paragraphs
after it), `CLAUDE.md` ("Structure", `ZoomLadder.cs`) and the comments of `Viewport`,
`ZoomLadder`, `RenderPlanner` and `CompareView.OnMouseWheel`; the decision itself dates from
a6e5f10 (the base version, 2026-09-08). Until 8a01f47 ("changed zoom button", 2026-09-08) the
plain wheel scrolled vertically and only `Ctrl`+wheel zoomed. The zoom at which resampling
turns to nearest-neighbour was a private constant of `CompareView`, `NearestNeighbourThreshold`
(4.0), until 801bda5 (refactoring and new tests, 2026-09-08) moved it into Core as
`RenderPlanner.PixelatedThreshold`, where a test reads it. The tool is for looking at two
variants of one processing down to the pixel, on displays scaled to 125 % or 150 % as well,
where "100 %" has to mean one image pixel per physical screen pixel (ADR 0011).

## Decision

- **Range.** The floor is `Viewport.MinScale`, the smaller of `FitScale` and 1.0: zooming out
  stops where the whole image is visible, and an image smaller than the viewport stops at
  100 %, so it can always be seen at its own resolution. The ceiling is `Viewport.MaxScale`, 16
  (1600 %). `Viewport` clamps every zoom, whatever its source, to the two.
- **The wheel** zooms continuously, anchored at the cursor: each notch (each wheel event,
  whatever the size of its delta) multiplies the scale by `WheelZoomFactor` (1.15) or divides
  it by that, and the content point under the cursor stays where it is. `Ctrl`+wheel does the
  same, so the habit from other image viewers keeps working. `Shift`+wheel pans horizontally by
  `WheelPanPixels` (140 physical pixels) per notch.
- **The ladder.** The `+` and `−` buttons and keys step through `ZoomLadder.Steps`, 19 stops
  from 0.02 to 16.0, anchored at the centre of the view. Since the wheel can leave any scale
  current, `ZoomLadder.Next` and `Previous` search for the nearest stop strictly above or below
  it (with a tolerance of 1e-6) and stay at 16.0 or 0.02 at the ends. `Ctrl+0` or `Fit` fits
  the image; `Ctrl+1` or `100%` shows it at 100 %.
- **Resampling.** At 400 % and above (`RenderPlanner.PixelatedThreshold`, 4.0) both images are
  drawn nearest-neighbour, so the pixels show as the squares they are; below it they are
  interpolated (`BitmapScalingMode.HighQuality`).

## Alternatives rejected

- **The wheel scrolls and `Ctrl`+wheel zooms**, as until 8a01f47. On a comparison view the
  wheel is almost always wanted to "look closer at what is under the pointer", and panning is
  already covered by dragging, the scrollbars and the minimap (the `OnMouseWheel` comment).
- **Stepping the ladder by index.** After a wheel zoom the current scale is usually between two
  stops, where an index means nothing; the ladder "steps to the neighbouring stop rather than to
  a fixed index" (the `ZoomLadder` summary).
- **A wider range.** Below the fit scale a zoom "only shrinks an already fully visible image"
  (the `MinScale` comment; "o miniatură pierdută pe un fundal gol", says the README); beyond 16
  "a pixel is a large flat square and more zoom buys nothing" (the `MaxScale` comment).
- **Interpolation at every zoom.** Past 400 % it "is smoothing away exactly what the user
  zoomed in to see" (the `PixelatedThreshold` comment).

## Consequences

- A new pair opens at the fit scale (`SetPair` calls `ZoomToFit`), and a window that grows
  raises the floor and pulls a smaller zoom up with it.
- The ladder's low stops are reached only by an image much larger than the window: elsewhere
  `ZoomTo` clamps them to the floor, so `−` at the fit scale stays at the fit scale.
- Guards: `ViewportTests` (the floor at the fit scale and at 100 %, the ceiling, the anchored
  zoom, the floor following a resize), `ZoomLadderTests` (off-ladder scales, both ends),
  `RenderPlanTests` (smooth at 3.99, pixelated at 4.0). The wheel, its two constants and the
  keys live in `CompareView`, with no test: the application has to be launched and looked at
  (ADR 0026).

Source: README.md ("Controale" and the two paragraphs after it); CLAUDE.md ("Structure");
src/ImageVisualCompare.Core/Viewport.cs; src/ImageVisualCompare.Core/ZoomLadder.cs;
src/ImageVisualCompare.Core/RenderPlan.cs; src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs
(`OnMouseWheel`, `OnKeyDown`, `SetPair`); the diffs of 8a01f47 and 801bda5.
