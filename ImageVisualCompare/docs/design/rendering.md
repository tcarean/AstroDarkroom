# Rendering: one transform, two pixel spaces, the layers

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Invariants" (seven of its ten).
> Where a paragraph became a decision record, its number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml`,
> `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`,
> `src/ImageVisualCompare.Core/Viewport.cs`,
> `src/ImageVisualCompare.Core/ImagePixel.cs`,
> `src/ImageVisualCompare.Core/RenderPlan.cs`,
> `src/ImageVisualCompare.Core/DividerModel.cs`,
> `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml`,
> `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs`,
> `src/ImageVisualCompare.Core/MinimapGeometry.cs`,
> `src/ImageVisualCompare.Wpf/app.manifest`,
> and, for its two bars' layout rounding, `src/ImageVisualCompare.Wpf/MainWindow.xaml`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

`CompareView.SetPair` shows a new pair: the sources, the pixel sizes, the backdrops, the
content size, zoom to fit, the divider reset, the thumbnail. `ReplacePair` (ADR 0040) shows
another rendering on the same canvas: the same files decoded again in another display mode
(R-113), or one file changed with the canvas size unchanged (R-065, ADR 0047). The two sources,
their pixel sizes and the thumbnail change, nothing else is touched, so `Viewport` and
`DividerModel` keep the zoom, the pan and the line where they were; the caller guarantees the
canvas size, since the backdrops and `Viewport`'s content size are not touched.
Either way the two `Image` elements keep the outgoing bitmaps in their render content until
the next render pass, whatever their `Source` says: measured in the application on the real
FITS pair (2026-10-26), a forced collection run before that pass freed nothing, and the
process carried the outgoing pair, about 270 MB with the render thread's copies, until the
next switch or open. That is why `MainWindow.ShowPair` lets one render pass through
(`Dispatcher.Invoke` at `Render` priority) after the incoming pair is on the surface and
releases the outgoing one only then (ADR 0040, which restates ADR 0016's order).

In the diagnostic log (ADR 0043) the surface writes, at `Debug`, what `SetPair` and `ReplacePair`
left on screen, read from `Viewport` (the canvas, the view in physical pixels, the display scale
and the zoom), the zoom each of `ZoomIn`, `ZoomOut`, `ZoomToFit` and `ZoomToActualSize` leaves
once a pair is shown, which covers their keys too, a held key's first step only and, when the key
is released (`OnKeyUp`), the zoom its repeats left, and a change of the display scale. The wheel, a
drag, the divider, the scrollbars and the minimap write nothing: they come many times a second. The
lines are listed in `logging.md`.

## Invariants

These are the things that will bite a future change. Each one has already cost time.

The other three invariants of the same list are in `imaging.md` ("Padding is layout, never
pixels") and `shell.md` ("Never set `InvariantGlobalization`" and "No `MessageBox`, and every
window opts into a dark caption").

**One transform object, not two.** `CompareView` assigns the *same* `TransformGroup`
instance to both images and to the backdrop (both backdrops since 861f5c1, ADR 0010). The synchronisation the comparison depends on is
structural: there is no state to keep in step and no way to update one image and forget the
other. Do not replace this with two transforms configured alike. See ADR 0010.

**Physical pixels below, device-independent pixels above.** `Viewport` works entirely in
physical screen pixels. WPF lays out in device-independent pixels. The conversion happens in
exactly two places — `CompareView`'s input handlers (multiply by `_deviceScale`; so do `UpdateViewportSize` and
`ZoomAtCentre`, ADR 0011) and `Sync`
(divide the render scale by it). This is what makes 100% mean one image pixel per physical
screen pixel on a scaled display. The divider is the exception and stays in DIPs throughout,
because it is a fraction of the window and never touches image coordinates. See ADR 0011.
`_deviceScale` is the scale of the monitor the window is on because the application is
per-monitor DPI aware: `app.manifest` declares it, and without it the process is system aware,
WPF reports the system's scale everywhere and raises no `OnDpiChanged` when the window moves,
and Windows stretches the window on a monitor scaled differently. `AppManifestTests` reads the
manifest out of the built files. See ADR 0034.

**The image layers are `Canvas`, not `Grid`.** A `Grid` clamps a child's desired size to the
cell, so a 5794 px wide image gets laid out at the width of the window and `Stretch` squashes
the bitmap into it. The symptom is a tiny image in the corner at a fraction of the reported
zoom. A `Canvas` measures with infinite space and the transform alone decides what is seen. See ADR 0012.

**The clip goes on the layer, never on the image.** A clip set on the `Image` is measured in
image coordinates and travels with the pan; on the layer it stays put on screen, which is
what a before/after slider needs. See ADR 0013.

**Both layers need the same opaque backdrop.** A is drawn over B, so wherever A does not cover
its layer, B would show through on the left of the line while the control's background shows on
the right: two halves over different grounds, which is exactly the artefact this tool exists to
rule out. `BeforeBackdrop` and `AfterBackdrop` are the same brush, the same size and the same
transform for that reason. Since ADR 0054 every image is loaded opaque, so the one place an
image does not cover its layer is the padding of a mismatched pair, past the smaller image's
edge (ADR 0014). See ADR 0013.

**Images are sized explicitly in pixels.** `image.Width = pair.Before.Info.PixelWidth` — its
own size, not the shared canvas, and not the natural size. WPF derives natural size from the file's DPI metadata, so a 72 dpi export would lay out
larger than its own pixel count and quietly break the 1:1 zoom level. See ADR 0012.

**At a whole scale the image is drawn on whole physical pixels.** `Viewport` holds two pairs of
offsets. `OffsetX` and `OffsetY` are the pan: what the anchors, the centring, the resize, the
scrollbars and the minimap work with, fractions and all. `DrawnOffsetX` and `DrawnOffsetY` are
what the image is drawn with: the pan at a scale that is not whole, and at a whole one the
nearest offset that lands on whole physical pixels. `RenderPlanner.For` takes the translation
from the drawn pair and `PixelAt` reads through it; nothing else does. The pan is not rounded
itself because a rounding dropped at each movement adds up: a window resized pixel by pixel
would hold the image's edge still, not its centre. For the image to land on the screen's pixels
the surface has to start on one too, which is what the two bars of `MainWindow.xaml` are laid
out rounded for (`UseLayoutRounding` on each bar's `Border`, off again inside it, and
measured again from `MainWindow.OnDpiChanged` when the display's scale changes); the surface
and its images stay unrounded, since rounding a 5938 pixel wide `Image` to device pixels is
what the explicit pixel sizes are there to avoid. See ADR 0053.

**The read-out names the pixel that is drawn.** `CompareView.OnPreviewMouseMove` keeps the
pointer's position (`_pointer`, forgotten in `OnMouseLeave`): the preview event, because a
scrollbar's thumb marks the moves of its own drag handled, and they never reach `OnMouseMove`.
From that position `PixelUnderPointer` converts to physical pixels and asks `Viewport.PixelAt`;
that returns an `ImagePixel`, whole coordinates, or null off the image. `CursorPixelChanged` is raised when the pixel changes, and
`MainWindow.UpdatePixelReadout` reads both bitmaps at it. The pointer need not move for that:
`Sync`, which every zoom, pan and divider move ends in, asks `PixelUnderPointer` again, so the
pixel a zoom brings under a pointer at rest is reported; and `SetPair` and `ReplacePair` have it
report even an unchanged pixel, since the bitmaps read at it are others (R-051). `MainWindow.ShowPair`
sets its own pair and mode before it hands the pair to the surface for that reason. A position
off the surface, which a drag that holds the mouse gives, is on no pixel. The pointer's position is the corner of the physical pixel it is on, and WPF paints that
screen pixel from the image point at its centre, so `PixelAt` converts the centre, through the
drawn offsets; the step from
a point to a pixel is taken there and nowhere after it. A centre exactly on the edge between two
image pixels, or `Viewport.EdgeReach` past it, goes to the pixel before the edge: the display
adapter paints it so, and a centre that rounding left an ulp to either side of an edge then
falls on one side. `CompareViewTests` renders the surface into a bitmap and compares the pixel
drawn with the pixel reported, away from the edges, since the software rasteriser it renders
with gives an edge to the pixel after it. See ADR 0052.

**Scrollbars float over the image.** Putting them in the layout would shrink the viewport when
they appear, which changes the fit scale, which can change whether they are needed. See ADR 0019.

**Whether the image overflows the view is asked in one place, with a slack.** At the fit the
scale is the view over the image and the visible extent the view over the scale, and in doubles
`v / (v / c)` is not always `c`: a 5938 x 3888 image on a view 1920 x 973 has a visible height
of 3887.9999999999995. `Viewport.Overflow` takes a difference of up to `WholeTolerance` (1e-9)
of a physical pixel as none, so `MaxOffsetX` and `MaxOffsetY` are 0 there, `CanScroll...` false,
and `ClampOffsets` puts such an axis at 0, not at half the residue. The bars
(`ScrollBarStates`) and the minimap's rectangle (`MinimapGeometry.Layout`) read only those, so
neither shows at the fit. Measured on 2026-11-20: at a width of 1920, 85 of the heights from 600
to 1499 leave that image 4.5e-13 over at the fit and 85 leave it as much under; over 1.9 million
random images (to 100,000 pixels) and views (to 8,000, whole and not) the largest residue was
1.3e-12 of a physical pixel. A real overflow, however small, still scrolls.

**`M` takes the minimap and the zoom badge off the image.** From 100 % up the pan stops at the
image's edges (`Viewport.ClampOffsets`), so the bottom-left corner of the image can only be
brought to the bottom-left corner of the surface, where the minimap lies over it, and the
top-right corner to where the zoom badge is (ADR 0059). `CompareView.ToggleOverlays` flips
`OverlaysHidden` and `ApplyOverlaysHidden` collapses or shows the two; nothing else is touched,
neither the scrollbars (ADR 0019) nor the divider. `MinimapView.Show` makes the minimap visible
for every pair it is given, so `SetPair` and `ReplacePair` apply the choice again right after
it, and it holds through a mode switch and a new pair. With no pair on the surface the minimap
stays collapsed whatever the choice: it has no thumbnail. A collapsed minimap takes no click,
and its rectangle is laid out again when it comes back (`MinimapView.Refresh`). The key itself
is the window's (`MainWindow.OnOverlaysKey`, in `shell.md`).

**The minimap measures on its thumbnail, inside its border.** `MinimapView` wraps the thumbnail
and the `Overlay` canvas the rectangle is drawn on in a 1-DIP border, so the minimap's own size
and position are not the thumbnail's. It lays the rectangle out in the overlay's size, and maps
a click through `TranslatePoint` onto the overlay before `MinimapGeometry.ContentPointAt`: on a
188-DIP thumbnail of a 5938-pixel image, the 1 DIP the border adds is 31 image pixels. Until
2026-10-22 both used the minimap's own size and position (R-041, R-042). `MinimapGeometry.Layout`
floors the rectangle at 2 DIPs after clamping it, then moves it in by what the floor added, so
by the right or bottom edge it stays on the thumbnail.
