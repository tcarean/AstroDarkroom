# 0012. The image layers are Canvas, not Grid, and each Image is sized to its own pixel count, never to its DPI-derived natural size

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "The image layers are
`Canvas`, not `Grid`" and "Images are sized explicitly in pixels"; "Testing"), the comment on
`AfterLayer` in `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml` and the comment in
`SetPair` in `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`; the decision itself dates
from a6e5f10 (the base version, 2026-09-08), and each image has had its own size on a shared
canvas since 861f5c1 (image padding, 2026-09-09). In a `Grid` a child's desired size is clamped
to its cell: a 5794 px wide image is laid out as wide as the window, `Stretch` squashes the
bitmap to fit, and what shows is a tiny image in a corner at a fraction of the reported zoom.
`CLAUDE.md` ("Testing") counts WPF layout behaviour as one of the project's two real defects,
found by launching the application and looking at a screenshot; no reasonable unit test would
have caught it. The second trap is an `Image`'s natural size, which WPF derives from the file's
DPI metadata: a 72 dpi export would lay out larger than its own pixel count.

## Decision

- The two image layers, `AfterLayer` and `BeforeLayer`, are `Canvas` elements; the control's
  root `Grid` only stacks them. A `Canvas` measures its children with infinite space, so each
  `Image` keeps the size it is given, and the shared transform (ADR 0010), not the layout,
  decides what is seen.
- `SetPair` sets each image's `Width` and `Height` to its own pixel count
  (`pair.Before.Info.PixelWidth` and `PixelHeight` for A, the same for B): never the natural
  size, and never the shared canvas, which only the backdrops (ADR 0013) and the viewport's
  content size take. Both images sit at the canvas origin.

## Alternatives rejected

- **A `Grid` for the layers.** It clamps the image's desired size to the cell: a 5794 px wide
  image is laid out at the window's width and squashed, and the zoom shown is not the zoom seen.
- **The natural size.** WPF derives it from the file's DPI metadata, so a 72 dpi export would
  lay out larger than its own pixel count and quietly break the 1:1 zoom level.
- **Both images at the size of the shared canvas**, as before 861f5c1, when a pair had one size.
  The padding of a mismatched pair is the smaller image not reaching the far edge (ADR 0014),
  which needs each image at its own size.

## Consequences

- Before the transform, one image pixel is one layout unit in both images, whatever DPI the file
  declares; with the top-left anchoring of ADR 0014, a content coordinate is a pixel coordinate
  of each file, and 100 % (ADR 0011) is one image pixel per screen pixel for any file.
- Both traps are WPF layout behaviour that no unit test reaches: a change to the layers or to
  `SetPair` is checked by opening a pair wider than the window and a 72 dpi export, at fit and
  at 100 %.
- Guards: none by test; the application has to be launched and looked at (ADR 0026).

Source: CLAUDE.md ("Invariants", "The image layers are `Canvas`, not `Grid`" and "Images are
sized explicitly in pixels"; "Testing"); src/ImageVisualCompare.Wpf/Controls/CompareView.xaml
(the comment on `AfterLayer`); src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs
(`SetPair`); the diff of 861f5c1.
