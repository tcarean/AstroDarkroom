# Code map

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Structure". Where a paragraph became
> a decision record, its number is given beside it.
> **Covers:** `src/ImageVisualCompare.Core/` and `src/ImageVisualCompare.Wpf/`, what each
> file is for, and the test project,
> `tests/ImageVisualCompare.Tests/ImageVisualCompare.Tests.csproj`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## Structure

- `src/ImageVisualCompare.Core/` — `net8.0`, no UI framework reference at all. Everything
  here is pure and unit tested.
  - `Viewport.cs` — the single source of truth for zoom and pan. Screen space is **physical
    pixels**, content space is image pixels. Holds `Scale`, `OffsetX/Y`, the clamping rules,
    and anchored zoom.
  - `DividerModel.cs` — the split line as a fraction of viewport width, plus hit testing.
  - `ZoomLadder.cs` — the discrete stops the `+`/`−` buttons step through. The wheel zooms
    continuously, so an off-ladder scale can be current at any time; `Next`/`Previous`
    therefore search rather than index. See ADR 0020. Where `Previous` would step up, under
    the lowest stop, `Viewport.ZoomOut` goes to the floor instead (ADR 0033).
  - `ImageInfo.cs` — what a file turned out to be. Reported to the user; nothing after the
    load branches on it, and in the load only `ShownAsStored` does, which says the samples
    went through the project's own level rule and so can take a display mode. See ADR 0022,
    ADR 0035, ADR 0040.
  - `FitsHeader.cs`, `FitsScale.cs`, `FitsRowDecoder.cs` — the project's own FITS reader:
    where the image is in the file and how it is stored (`FitsImageLayout`), how a value
    becomes a 16-bit sample and one of 256 levels, and one row of 16-bit samples at a time out
    of the planes. How they fit is in `imaging.md`. See ADR 0029.
  - `FloatSamples.cs` — a row of float samples from a float TIFF turned into the same 16-bit
    samples through `FitsScale`, as a FITS's are. See ADR 0035.
  - `DisplayMode.cs`, `DisplayRenderer.cs`, `SampleHistogram.cs`, `LevelLookup.cs`,
    `RainbowTable.cs` — the display modes (ADR 0040): the renderer that turns 16-bit rows into
    Bgra32 rows, linear or through Siril's histogram equalisation (one 65 536-bin histogram per
    layer, the inclusive CDF as a lookup) and its rainbow colour map. See `imaging.md`.
  - `ImagePairValidator.cs` — `PadPlan`: the canvas the pair is compared on, which is the
    larger extent on each axis, plus whether that meant padding and the question to put to
    the user if it did.
  - `UiText.cs`, `UiLanguage.cs` — every text the application shows, English and Romanian side
    by side, and the table in use (`UiText.Current`). See `language.md`, ADR 0038.
  - `StartupArguments.cs` — the command line: `--lang` and the candidates for the pair.
  - `PointD.cs`, `Geometry.cs` — `PointD`, `SizeD`, `RectD` and `AspectFit`, declared here
    so Core needs no `WindowsBase` reference. See ADR 0009.
  - `RenderPlan.cs` — `RenderPlanner.For(viewport, deviceScale)` turns viewport state into
    the translation, scale and resampling mode the view puts on its transform. This is where
    the physical-pixel to device-independent-pixel conversion lives. See ADR 0011.
  - `DividerLayout.cs` — the record `DividerModel.Layout` returns: line position, the clip
    region for image A, and the handle rectangle.
  - `MinimapGeometry.cs` — the viewport rectangle on the thumbnail, and the content point a
    click maps to.
  - `ScrollBarState.cs` — range, thumb proportion, value and visibility per axis.

  The last four exist so that the controls do no arithmetic of their own. A control reads a
  layout record and assigns properties; if a number looks wrong on screen, the test for it
  is in Core. See ADR 0009.
- `src/ImageVisualCompare.Wpf/` — `net8.0-windows`, the application.
  - `Imaging/IImageLoader.cs` — the decoder seam. Typed on `BitmapSource` deliberately: any
    replacement decoder still has to produce one for the `Image` control, so a generic
    parameter would buy nothing. That is also why it lives here and not in Core. See ADR 0008.
  - `Imaging/WicImageLoader.cs` — decodes via WIC. `Probe` reads the header only;
    `Load` realises the pixels and converts to Bgra32. `Classify` maps a WIC pixel format to
    words for the status bar, and falls through to raw bits-per-pixel rather than rejecting
    anything it does not recognise. See ADR 0022. Float samples are found by
    `FloatSamplesOf` and shown as stored (ADR 0035); WIC's own failures become the message
    types (ADR 0031).
  - `Imaging/FitsImageLoader.cs` — decodes FITS with the reader in Core, row by row into a
    `WriteableBitmap`, and a second time when the floats turn out to count to 65 535; in a
    display mode other than linear it counts every row first and renders after (ADR 0040).
    See ADR 0029.
  - `Imaging/Bitmaps.cs` — the Bgra32 bitmap the two loaders above write rows into, and the
    too-large refusal both say, once. See `imaging.md`.
  - `Imaging/RoutingImageLoader.cs` — the loader the application constructs: FITS, by the
    file's first bytes, to `FitsImageLoader`, everything else to `WicImageLoader`.
  - `Imaging/ImagePairLoader.cs` — probes **both** headers and settles the canvas before
    decoding either, so a pair that needs a decision costs nothing until it is made. A
    mismatch is put to the caller through `confirmPadding`; with no delegate it is refused,
    which is what anything headless wants. Decodes sequentially, never in parallel, to keep
    the peak down (the 2026-09-08 measurement found no transient spike to keep down; the
    loader's comment says why the order stays, ADR 0015). Turns every expected file failure into
    a message rather than a crash. See ADR 0015.
  - `Imaging/MemoryHousekeeping.cs` — the release path when a pair is replaced, and the
    process memory readout.
  - `Services/FileDialogService.cs` — the two open dialogs and the file filter. These are
    the Windows common dialogs and follow the *system* theme, which is the one place the
    palette is not ours to choose. See ADR 0018.
  - `Interop/DarkTitleBar.cs` — the DWM opt-in that makes a window's caption dark. Every
    `Window` in the application calls `Apply` in its constructor. See ADR 0018.
  - `Controls/CompareView.xaml(.cs)` — the comparison surface. All input handling, the
    shared transform, the clip, the overlay, the scrollbars.
  - `Controls/MinimapView.xaml(.cs)` — thumbnail plus viewport rectangle, and click-to-jump.
  - `MainWindow.xaml(.cs)` — shell: toolbar, status bar, open/error/retry flow, pixel readout.
  - `MessageDialog.xaml(.cs)` — the application's own message box: `Show` to say something,
    `Confirm` to ask yes or no.
  - `AboutWindow.xaml(.cs)` and `AboutViewModel.cs` — the About window, FlatCheck's in this
    tool's colours: the icon, the name, the version and the copyright, the licence card, the card
    of the components written by others, `Close`. See `shell.md`, ADR 0045.
  - `App.xaml.cs` — the interface language, set from `--lang` before the first window; the
    command line pair, handed over once via `TakeStartupSelection`, and a `--lang` not
    understood, via `TakeUnknownLanguage`.
  - `Program.cs` — the entry point, the csproj's `StartupObject`: starts the diagnostic log,
    runs the `App` as WPF's generated `Main` would, ends the log with the exit code. See
    `logging.md`, ADR 0043.
  - `Logging/LogSetup.cs` — the log's configuration: the folder and its fallback, the file
    names, the rolling, the level, the line, the first and last lines, the crash hooks.
    `Logging/LogText.cs` — how a value is written in the log, invariant. `Logging/RunClock.cs`
    — a load's start and done lines with the time of each stage. `Logging/Loggers.cs` — the
    loggers' names. See `logging.md`.
  - `Version.cs` and `ThirdParty.cs` — what the status bar and About say: the version and the
    copyright, read from the assembly, the licence's page, and the components written by others,
    which the About window lists (R-075). See `versioning-and-licence.md`.
  - `Theme.xaml` — the dark palette and every control style.
  - `Assets/ImageVisualCompare.ico` and `Assets/make-icon.ps1` — the application's icon and the
    script that draws it; the csproj's `ApplicationIcon` embeds the file. See `shell.md`.
  - `app.manifest` — what Windows reads from the executable at start-up: per-monitor DPI
    awareness, so 100% is one to one on every monitor; the csproj's `ApplicationManifest`
    embeds it. See `rendering.md`, ADR 0034.
- `tests/ImageVisualCompare.Tests/` — `net8.0-windows` with `UseWPF`, because the imaging
  tests drive the real WIC codecs. References both other projects. `WpfThread` runs the tests
  of a window or a control on one STA thread (ADR 0032). `LogCapture` configures the log of the
  whole test run, at `Trace`, and lets a test read the lines of its own call tree (ADR 0043).
- `THIRD-PARTY-NOTICES.md` — the components written by others that the application ships,
  NLog's two packages, with their licence's whole text and why it is compatible with GPLv3; it
  stays beside the source, and About lists the components from `ThirdParty.cs` (R-075).
