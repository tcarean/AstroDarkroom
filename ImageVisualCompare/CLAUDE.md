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
  - `ImagePairValidator.cs` — `PadPlan`: the canvas the pair is compared on, which is the
    larger extent on each axis, plus whether that meant padding and the question to put to
    the user if it did.
  - `PointD.cs`, `Geometry.cs` — `PointD`, `SizeD`, `RectD` and `AspectFit`, declared here
    so Core needs no `WindowsBase` reference.
  - `RenderPlan.cs` — `RenderPlanner.For(viewport, deviceScale)` turns viewport state into
    the translation, scale and resampling mode the view puts on its transform. This is where
    the physical-pixel to device-independent-pixel conversion lives.
  - `DividerLayout.cs` — the record `DividerModel.Layout` returns: line position, the clip
    region for image A, and the handle rectangle.
  - `MinimapGeometry.cs` — the viewport rectangle on the thumbnail, and the content point a
    click maps to.
  - `ScrollBarState.cs` — range, thumb proportion, value and visibility per axis.

  The last four exist so that the controls do no arithmetic of their own. A control reads a
  layout record and assigns properties; if a number looks wrong on screen, the test for it
  is in Core.
- `src/ImageVisualCompare.Wpf/` — `net9.0-windows`, the application.
  - `Imaging/IImageLoader.cs` — the decoder seam. Typed on `BitmapSource` deliberately: any
    replacement decoder still has to produce one for the `Image` control, so a generic
    parameter would buy nothing. That is also why it lives here and not in Core.
  - `Imaging/WicImageLoader.cs` — decodes via WIC. `Probe` reads the header only;
    `Load` realises the pixels and converts to Bgra32. `Classify` maps a WIC pixel format to
    words for the status bar, and falls through to raw bits-per-pixel rather than rejecting
    anything it does not recognise.
  - `Imaging/ImagePairLoader.cs` — probes **both** headers and settles the canvas before
    decoding either, so a pair that needs a decision costs nothing until it is made. A
    mismatch is put to the caller through `confirmPadding`; with no delegate it is refused,
    which is what anything headless wants. Decodes sequentially, never in parallel, to keep
    the peak down. Turns every expected file failure into a message rather than a crash.
  - `Imaging/MemoryHousekeeping.cs` — the release path when a pair is replaced, and the
    process memory readout.
  - `Services/FileDialogService.cs` — the two open dialogs and the file filter. These are
    the Windows common dialogs and follow the *system* theme, which is the one place the
    palette is not ours to choose.
  - `Interop/DarkTitleBar.cs` — the DWM opt-in that makes a window's caption dark. Every
    `Window` in the application calls `Apply` in its constructor.
  - `Controls/CompareView.xaml(.cs)` — the comparison surface. All input handling, the
    shared transform, the clip, the overlay, the scrollbars.
  - `Controls/MinimapView.xaml(.cs)` — thumbnail plus viewport rectangle, and click-to-jump.
  - `MainWindow.xaml(.cs)` — shell: toolbar, status bar, open/error/retry flow, pixel readout.
  - `MessageDialog.xaml(.cs)` — the application's own message box: `Show` to say something,
    `Confirm` to ask yes or no.
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

**Both layers need the same opaque backdrop.** A is drawn over B, so a file with an alpha
channel would blend into B on the left while B blends into whatever is behind it on the right
— two halves composited over different backgrounds, which is exactly the artefact this tool
exists to rule out. `BeforeBackdrop` and `AfterBackdrop` are the same brush, the same size and
the same transform for that reason. Verified with a 50%-alpha red over opaque blue: the left
half reads 128,0,0 (A over the backdrop), not 128,0,127 (A over B).

**Padding is layout, never pixels.** When the two images differ in size they are compared on
a canvas as large as the pair needs, and the smaller one simply does not reach the far edge —
the backdrop is what fills the gap. Nothing is copied into a larger buffer — at 84 MB an
image, that copy would be the most expensive thing here. Both are anchored top-left, and that is what
keeps a content coordinate equal to a pixel coordinate in *both* images: the pixel readout
needs no offsets and prints `—` past the end of an image on its own bounds check. Centring
would break that, and `PadPlan` is where the offsets would have to go.

**Never set `InvariantGlobalization`.** WPF's font cache constructs `CultureInfo("en")` while
computing typography availability; in invariant mode that throws `CultureNotFoundException`
and takes the process down during text layout. It was set in `Directory.Build.props` for two
phases and only crashed once a Consolas run appeared in the status bar. No unit test catches
this — the app has to be launched.

**Images are sized explicitly in pixels.** `image.Width = pair.Before.Info.PixelWidth` — its
own size, not the shared canvas, and not the natural size. WPF derives natural size from the file's DPI metadata, so a 72 dpi export would lay out
larger than its own pixel count and quietly break the 1:1 zoom level.

**No `MessageBox`, and every window opts into a dark caption.** `MessageBox.Show` opens a
Win32 window that cannot be themed at all — white background, black text — and on a tool for
judging dark images that is a bright rectangle over the picture being compared, not a
cosmetic complaint. `MessageDialog` replaces it. The caption is a separate problem: it belongs
to the desktop window manager, WPF cannot reach it and does not follow the system theme
either, so each window asks DWM for it through `DarkTitleBar.Apply` — in the constructor, so
the attribute is set before the first frame and the caption is never white even briefly. On
Windows 11 the caption is set to the exact chrome colour; Windows 10 has no such attribute and
gets the generic dark caption, which is close but not `#1B1F26`.

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

142 cases. `dotnet test ImageVisualCompare.sln`.

Covered: everything in Core, and the imaging layer. `WicImageLoaderTests` generates fixtures
at run time in six pixel formats plus TIFF via `TempImages`, so the real codecs are exercised
without binary files in the repository. `ImagePairLoaderTests` covers valid, mismatched,
missing and corrupt inputs, progress reporting, and — with a recording fake — that a
mismatched pair never reaches a decode, whether it was refused outright or declined at the
prompt.

What remains untested is now genuinely untestable without UI automation: event wiring, mouse
capture, property assignment, and the interop that darkens a caption. Every number those controls put on screen comes from Core
and has a test. `MainWindow`, `MessageDialog`, `DarkTitleBar`,
`FileDialogService` and `MemoryHousekeeping` have none.

Be aware of what that has meant in practice: both real defects found in this project lived in
the untested layer, and neither would have been caught by any reasonable unit test. One was
WPF layout behaviour, the other was build configuration killing text rendering. Both were
found by launching the app and looking at a screenshot. When changing the WPF layer, run it —
a green test suite says nothing about that code.

## Versioning

The version is shown in the window title, in the status bar next to the copyright, and in
the About dialog. There is no `--version` flag; a GUI that already displays it permanently
does not need one.

The single source of truth is `<Version>` in `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`.
`Version.cs` does **not** hardcode it: `VersionInfo.Value` reads the assembly's
`AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and trims any
`+sourcerevision` suffix. Don't reintroduce a literal there.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** — not as part of a change, not "while
you're in there". If a change seems to warrant a bump, say so and let them decide.

Bumping breaks nothing: `VersionTests` asserts the format and the csproj-to-assembly
plumbing, never a particular number. What does fail, and should: removing `<Version>` or
`<Copyright>` from the csproj, or breaking the way `VersionInfo` reads them back.

## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the project root (the
verbatim FSF `gpl-3.0.txt` — don't reword it, and don't reflow it).

Four things follow, and all four are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment: the
  SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy), with a `//` prefix. New files get it too
  — it is the first thing to write, not an afterthought. `LicenseHeaderTests` walks all
  three projects and fails on any file missing it, which is the only thing keeping the
  convention alive as files get added. The SPDX line is there so licence scanners can read
  the file without parsing English. `.xaml` files do not carry it, matching FlatCheck's
  `.cs`-only convention.
- **The copyright is on screen for the whole run**, in the status bar, docked right so it
  is never what gets clipped when the window narrows.
- **The About dialog carries the warranty disclaimer** and points at `LICENSE` — GPLv3
  §5(d), which wants a program that displays legal notices to display that one too. It
  lives behind a button rather than in the status bar on purpose: five lines of legal text
  across the bottom of a comparison window would crowd out the pixel readout. The text is
  `VersionInfo.About`, a property rather than a string built inside the click handler, so
  `VersionTests` can assert its content without opening a window.
- **One source of truth for the copyright string**: `<Copyright>` in the app csproj, read
  back through `AssemblyCopyrightAttribute` by `VersionInfo.Copyright`. The literal in
  `ReadCopyright` is only a fallback.

The year and the author are the user's call, exactly like the version. Don't roll the year
forward on your own initiative.

## Committing

**Do not commit.** Make the changes, run the tests, and hand over a commit message for the
user to use. They commit themselves.

This also means: don't `git add`, don't stage, don't amend. Leave the working tree as it is
so they can review the diff before deciding.

A good commit message here follows the existing log: a short lowercase subject line, then a
body that explains *why* rather than restating the diff — what the change is for, what was
deliberately left alone and why, and anything a future reader would otherwise have to
rediscover. No `Co-Authored-By` trailer.
