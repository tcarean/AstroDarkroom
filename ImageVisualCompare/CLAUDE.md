# ImageVisualCompare

WPF before/after slider for two identically sized images: image A left of a draggable
vertical line, image B right of it, both at the same zoom and pan. Built for comparing two
variants of the same astro processing.

.NET 9, Windows, x64, **zero NuGet dependencies** outside the test project. See
"No imaging dependency" below before adding one.

User documentation lives in `README.md`. Do not restate it here or in comments.

## Structure

- `src/ImageVisualCompare.Core/` — `net9.0`, no UI framework reference at all. Everything
  here is pure and unit tested.
  - `Viewport.cs` — the single source of truth for zoom and pan. Screen space is **physical
    pixels**, content space is image pixels. Holds `Scale`, `OffsetX/Y`, the clamping rules,
    and anchored zoom.
  - `DividerModel.cs` — the split line as a fraction of viewport width, plus hit testing.
  - `ZoomLadder.cs` — the discrete stops the `+`/`−` buttons step through. The wheel zooms
    continuously, so an off-ladder scale can be current at any time; `Next`/`Previous`
    therefore search rather than index.
  - `ImageInfo.cs` — what a file turned out to be. Reported to the user, never branched on.
  - `ImagePairValidator.cs` — equal pixel dimensions or refusal, with both resolutions in
    the message.
  - `PointD.cs` — a local point type so Core needs no `WindowsBase` reference.
- `src/ImageVisualCompare.Wpf/` — `net9.0-windows`, the application.
  - `Imaging/IImageLoader.cs` — the decoder seam. Typed on `BitmapSource` deliberately: any
    replacement decoder still has to produce one for the `Image` control, so a generic
    parameter would buy nothing. That is also why it lives here and not in Core.
  - `Imaging/WicImageLoader.cs` — decodes via WIC. `Probe` reads the header only;
    `Load` realises the pixels and converts to Bgra32. `Classify` maps a WIC pixel format to
    words for the status bar, and falls through to raw bits-per-pixel rather than rejecting
    anything it does not recognise.
  - `Imaging/ImagePairLoader.cs` — probes **both** headers and validates before decoding
    either. A mismatched pair is then rejected having allocated nothing. Decodes
    sequentially, never in parallel, to keep the peak down. Turns every expected file
    failure into a message rather than a crash.
  - `Imaging/MemoryHousekeeping.cs` — the release path when a pair is replaced, and the
    process memory readout.
  - `Services/FileDialogService.cs` — the two open dialogs and the file filter.
  - `Controls/CompareView.xaml(.cs)` — the comparison surface. All input handling, the
    shared transform, the clip, the overlay, the scrollbars.
  - `Controls/MinimapView.xaml(.cs)` — thumbnail plus viewport rectangle, and click-to-jump.
  - `MainWindow.xaml(.cs)` — shell: toolbar, status bar, open/error/retry flow, pixel readout.
  - `App.xaml.cs` — command line pair, handed over once via `TakeStartupSelection`.
  - `Theme.xaml` — the dark palette and every control style.
- `tests/ImageVisualCompare.Tests/` — `net9.0-windows` with `UseWPF`, because the imaging
  tests drive the real WIC codecs. References both other projects.

## Invariants

These are the things that will bite a future change. Each one has already cost time.

**One transform object, not two.** `CompareView` assigns the *same* `TransformGroup`
instance to both images and to the backdrop. The synchronisation the comparison depends on is
structural: there is no state to keep in step and no way to update one image and forget the
other. Do not replace this with two transforms configured alike.

**Physical pixels below, device-independent pixels above.** `Viewport` works entirely in
physical screen pixels. WPF lays out in device-independent pixels. The conversion happens in
exactly two places — `CompareView`'s input handlers (multiply by `_deviceScale`) and `Sync`
(divide the render scale by it). This is what makes 100% mean one image pixel per physical
screen pixel on a scaled display. The divider is the exception and stays in DIPs throughout,
because it is a fraction of the window and never touches image coordinates.

**The image layers are `Canvas`, not `Grid`.** A `Grid` clamps a child's desired size to the
cell, so a 5794 px wide image gets laid out at the width of the window and `Stretch` squashes
the bitmap into it. The symptom is a tiny image in the corner at a fraction of the reported
zoom. A `Canvas` measures with infinite space and the transform alone decides what is seen.

**The clip goes on the layer, never on the image.** A clip set on the `Image` is measured in
image coordinates and travels with the pan; on the layer it stays put on screen, which is
what a before/after slider needs.

**Image A needs an opaque backdrop.** A is drawn over B, so a file with an alpha channel
would blend into B on the left while B blends into the canvas colour on the right — two
halves composited over different backgrounds, which is exactly the artefact this tool exists
to rule out. `BeforeBackdrop` gives A the same backdrop B has. Verified with a 50%-alpha red
over opaque blue: the left half reads 137,10,13 (A over the canvas), not 128,0,127 (A over B).

**Never set `InvariantGlobalization`.** WPF's font cache constructs `CultureInfo("en")` while
computing typography availability; in invariant mode that throws `CultureNotFoundException`
and takes the process down during text layout. It was set in `Directory.Build.props` for two
phases and only crashed once a Consolas run appeared in the status bar. No unit test catches
this — the app has to be launched.

**Images are sized explicitly in pixels.** `image.Width = pair.PixelWidth`, not the natural
size. WPF derives natural size from the file's DPI metadata, so a 72 dpi export would lay out
larger than its own pixel count and quietly break the 1:1 zoom level.

**Scrollbars float over the image.** Putting them in the layout would shrink the viewport when
they appear, which changes the fit scale, which can change whether they are needed.

## No imaging dependency

WIC was chosen over SkiaSharp, ImageSharp and Magick.NET after comparing them, and the
reasoning still holds: it is the most complete PNG implementation on Windows, it costs
nothing at build time, and it is the only one that reaches the `Image` control without an
intermediate copy — which matters at 84 MB per image. It also gives access to the true source
format, which the status bar reports.

If a decoder ever has to change, `IImageLoader` is the seam. Replacing it is one class.

## Memory, measured

Measured on a 5794×3637 16-bit pair, ~108 MB each on disk:

| | private | working set |
|---|---|---|
| headless load, both images | 173 MB | 199 MB |
| after release | 12 MB | 38 MB |
| loaded and rendered, at fit | 283 MB | 290 MB |
| plateau after visiting several zoom levels | ~570 MB | ~300 MB |

The plateau is bounded, not a leak: five zoom cycles left it flat and it drifts down when
idle. The extra over the headless figure is WPF's render thread keeping scaled intermediates
for the Fant resampling path.

Two things follow. WIC **streams** the Rgb48 to Bgra32 conversion rather than materialising a
126 MB intermediate, so there is no transient spike above the steady state. And the managed
heap stays near zero because WIC pixels are unmanaged — which is why `ReleaseImageBuffers`
forces a finaliser pass and does *not* bother with large object heap compaction.

## Testing

79 cases. `dotnet test ImageVisualCompare.sln`.

Covered: everything in Core, and the imaging layer. `WicImageLoaderTests` generates fixtures
at run time in six pixel formats plus TIFF via `TempImages`, so the real codecs are exercised
without binary files in the repository. `ImagePairLoaderTests` covers valid, mismatched,
missing and corrupt inputs, progress reporting, and — with a recording fake — that a
mismatched pair never reaches a decode.

**Not covered at all: the whole WPF layer.** `CompareView`, `MinimapView`, `MainWindow`,
`FileDialogService`, `MemoryHousekeeping`.

Be aware of what that has meant in practice: both real defects found in this project so far
lived in the untested layer, and neither would have been caught by any reasonable unit test.
One was WPF layout behaviour, the other was build configuration killing text rendering. Both
were found by launching the app and looking at a screenshot. When changing the WPF layer,
run it — a green test suite says nothing about that code.

The planned next step is to move the pure calculations still buried in the controls — the
minimap rectangle and the divider clip geometry — into Core, where they can be tested. That
is intended as its own commit.
