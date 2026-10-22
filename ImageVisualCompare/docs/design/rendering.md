# Rendering: one transform, two pixel spaces, the layers

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Invariants" (seven of its ten).
> Where a paragraph became a decision record, its number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml`,
> `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`,
> `src/ImageVisualCompare.Core/Viewport.cs`,
> `src/ImageVisualCompare.Core/RenderPlan.cs`,
> `src/ImageVisualCompare.Core/DividerModel.cs`,
> `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml`,
> `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs`,
> `src/ImageVisualCompare.Core/MinimapGeometry.cs`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

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

**The image layers are `Canvas`, not `Grid`.** A `Grid` clamps a child's desired size to the
cell, so a 5794 px wide image gets laid out at the width of the window and `Stretch` squashes
the bitmap into it. The symptom is a tiny image in the corner at a fraction of the reported
zoom. A `Canvas` measures with infinite space and the transform alone decides what is seen. See ADR 0012.

**The clip goes on the layer, never on the image.** A clip set on the `Image` is measured in
image coordinates and travels with the pan; on the layer it stays put on screen, which is
what a before/after slider needs. See ADR 0013.

**Both layers need the same opaque backdrop.** A is drawn over B, so a file with an alpha
channel would blend into B on the left while B blends into whatever is behind it on the right
— two halves composited over different backgrounds, which is exactly the artefact this tool
exists to rule out. `BeforeBackdrop` and `AfterBackdrop` are the same brush, the same size and
the same transform for that reason. Verified with a 50%-alpha red over opaque blue: the left
half reads 128,0,0 (A over the backdrop), not 128,0,127 (A over B). See ADR 0013.

**Images are sized explicitly in pixels.** `image.Width = pair.Before.Info.PixelWidth` — its
own size, not the shared canvas, and not the natural size. WPF derives natural size from the file's DPI metadata, so a 72 dpi export would lay out
larger than its own pixel count and quietly break the 1:1 zoom level. See ADR 0012.

**Scrollbars float over the image.** Putting them in the layout would shrink the viewport when
they appear, which changes the fit scale, which can change whether they are needed. See ADR 0019.

**The minimap measures on its thumbnail, inside its border.** `MinimapView` wraps the thumbnail
and the `Overlay` canvas the rectangle is drawn on in a 1-DIP border, so the minimap's own size
and position are not the thumbnail's. It lays the rectangle out in the overlay's size, and maps
a click through `TranslatePoint` onto the overlay before `MinimapGeometry.ContentPointAt`: on a
188-DIP thumbnail of a 5938-pixel image, the 1 DIP the border adds is 31 image pixels. Until
2026-10-22 both used the minimap's own size and position (R-041, R-042). `MinimapGeometry.Layout`
floors the rectangle at 2 DIPs after clamping it, then moves it in by what the floor added, so
by the right or bottom edge it stays on the thumbnail.
