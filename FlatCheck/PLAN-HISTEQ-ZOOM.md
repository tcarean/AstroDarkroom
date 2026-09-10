# Plan: Mozaic as the default view, and a smaller picture with zoom and pan (Histogram tab)

Read `CLAUDE.md` first. Builds on `PLAN-HISTEQ.md`, which is implemented. Same rules: no
version bump, no commit, GPL header on every new file, user-facing text in Romanian, code
documentation in English. Nothing here computes a number; it is all view.

## Why

Compared against a Siril 1.4.4 snapshot of `Nikkor 35mm DX/f2.8-sky/ISO400/flats/DSC_0031.NEF`
(B&W tab, raw opened undebayered), the view that matches what Siril shows is `Mozaic` -
the whole mosaic equalised as one layer. `RGB` is the richer picture, but it is not the
one the user has been reading patterns from, so it should not be what appears first.

The picture is also shown too large: it fills the right-hand column, ~1000 px wide, and
there is no way to look closer. Siril's own viewer zooms and pans; a pattern a few tiles
wide needs that.

## Decisions (taken here; say so if you want a different one)

1. **`Mozaic` becomes the first chip and the default selection on a CFA frame.** Chip
   order: `Mozaic`, `RGB`, `R`, `G1`, `G2`, `B`. On mono there is still one view, unchanged.
   The "keep the previous selection if its label still exists" rule in
   `HistEqViewViewModel.Rebuild` stays, so switching files keeps whatever chip was on.
2. **The console keeps writing the RGB composite**, shrunk, as today. Its `-o` file is an
   artefact for someone who cannot switch chips, and the composite is the more informative
   one. The window's default is a *viewing* choice, like the map scale chips, and with
   decision 3 the window no longer uses the run's PNG at all, so nothing has to match.
3. **Zoom reaches the sensor pixels, and the window stops going through PNG.** A first
   draft of this plan zoomed over the 1504 x 1004 display raster, and the user rightly
   asked what the point was: above 100 % that only magnifies pixels that are already
   2 x 2 (channel) or 4 x 4 (mosaic) block means. The reason for the small raster was the
   PNG round trip the window uses today - a 6016 x 4016 grey PNG of equalised noise is
   15-20 MB and seconds to encode per view. The window does not need PNG at all: a byte
   raster goes straight into a `BitmapSource` (`PixelFormats.Gray8` / `Rgb24`) with no
   encoding and memory equal to the raster. So:
   - the analyzer **keeps the full-resolution equalised rasters** it already computes
     (per channel 3008 x 2008, the mosaic 6016 x 4016, the composite 3008 x 2008 x 3;
     ~66 MB of bytes per file on a D3300, well under what structure mode holds);
   - the shrunk rasters stay, for the console's PNG and the tests - nothing on the console
     side moves, no golden file moves;
   - the window displays the full-res rasters directly. "100 %" is one sensor pixel per
     screen pixel; on `Mozaic` that shows the R/G/B chequerboard exactly as Siril does at
     100 %, which is more faithful than today. Above 100 % pixels are drawn as crisp
     squares (`NearestNeighbor`), below it smoothed (`HighQuality`). "Fit" lands around
     13 % on the usual column.
4. **Pan is view code, not view-model code.** Dragging a picture is interaction, not a
   decision, so it lives in a reusable control. What the view-model holds is the zoom
   *state* - fit or a factor - so a test can drive it and the tab can reset it.
5. **"Fit" never enlarges past 100 %** (found while implementing: on a picture smaller
   than the viewport the fit factor went above 1 and blew it up). A small picture is
   shown at its size; zooming in from there is a deliberate act.
6. **The viewport is a fixed 520 px tall**, full column width. "Fit" scales the whole
   picture into it, which on the usual column is ~780 x 520 - the "a bit smaller" asked
   for. A star-sized row that fills the rest of the tab was considered and rejected: the
   caption, caveat, guide and report would then need their own scrolling region, and the
   tab is a single vertical flow like the other four.

## Part 0 - Core: keep the full-resolution rasters

### 0.1 `FlatCheck.Core/Analysis/HistEqAnalyzer.cs`

- `HistEqChannel` gains `FullH`, `FullW`, `byte[] Full` - the equalised raster before
  `Shrink`. `Equalised`/`H`/`W` stay the shrunk one the console encodes.
- `HistEqResult` gains `FullH`, `FullW` (the channel's full size), `byte[] MosaicFull`
  (`SourceH x SourceW`, null on mono) and `byte[] CompositeFullRgb` (`FullH x FullW x 3`).
  `Composite` becomes a function of a size and a selector so it builds both the shrunk
  and the full one without a second copy of the logic.
- Memory note in the header updated with the measured peak after the change; the
  `sources[i] = null` release stays, the full rasters replace it as the thing held.

Tests (`HistEqAnalyzerTests`): `Full` is `H*2 x W*2`... precisely `sources[i].H x W`
before shrinking; `Shrink` of `Full` equals `Equalised` byte for byte (the shrunk one is
derived, not recomputed); `MosaicFull` is `SourceH x SourceW` and null on mono; the
composite's full and shrunk versions agree after shrinking the full one.

### 0.2 `FlatCheck.Core/Runs/HistEqRun.cs`

Unchanged in behaviour: `Png` is still the shrunk composite; `EncodeChannel`/`EncodeMosaic`
keep encoding the shrunk rasters (tests and any future console use). The window stops
calling them - see 2.0.

## Part 1 - View-model

### 1.0 `FlatCheck.Wpf/Presentation/RasterBitmaps.cs` (new file)

Two pure functions, WPF-side because they return a WPF type: `Grey(byte[] g, int w, int h)`
-> `BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, g, w)`, frozen; and
`Rgb(byte[] rgb, int w, int h)` with `Rgb24` and stride `w * 3`. No copy, no encode. A
`PresentationTests` case checks the pixel format, size and a sampled pixel of each.

### 1.1 `FlatCheck.Wpf/ViewModels/HistEqViewViewModel.cs`

`Rebuild`: build the CFA list as `Mozaic`, `RGB`, then one per channel. Nothing else
changes; `Selected` falls back to `list[0]`, which is now `Mozaic`.

### 1.2 `FlatCheck.Wpf/ViewModels/ImageZoomViewModel.cs` (new file)

The zoom state, on the `MapScaleViewModel` pattern - public, decides nothing:

```
public sealed class ImageZoomViewModel : ObservableObject
    public bool IsFit                 // true: the control computes the scale to fit
    public double Zoom                // the manual factor, meaningful when !IsFit; 1 = 100 %
    public double EffectiveZoom       // what is actually on screen; the control writes it (TwoWay)
    public string Label               // "potrivit (47 %)" / "100 %" / "250 %"
    public ICommand ZoomIn, ZoomOut, Fit, Actual
    internal const double Min = 0.1, Max = 8, Step = 1.25
    internal void Reset()             // back to fit; the tab calls it on a new file
```

- `ZoomIn`/`ZoomOut` multiply/divide by `Step`, clamped to `[Min, Max]`. Leaving fit by
  zooming starts from `EffectiveZoom`, so the first step in is a step, not a jump to 100 %.
- `Actual` sets `Zoom = 1`, `IsFit = false`. `Fit` sets `IsFit = true`.
- `Label` is derived and raised whenever `IsFit`, `Zoom` or `EffectiveZoom` changes.
- `RelayCommand` from `Mvvm/` is what the commands use; check its constructor shape.

### 1.3 `FlatCheck.Wpf/ViewModels/HistEqTabViewModel.cs`

- `public ImageZoomViewModel Zoom { get; } = new();`
- `Image` changes type from `byte[]` (PNG) to `ImageSource`, built by `RasterBitmaps`
  from the **full-resolution** raster of the selected view. The per-chip cache now holds
  frozen `BitmapSource`s instead of PNG bytes; `compositePng` leaves `Populate`'s
  signature (nothing consumes it any more), and the `PngToImage` converter is no longer
  used on this tab (it stays for the maps).
- `Populate`: `Zoom.Reset()` **only when the file changed** (a new `_last` from a run), not
  when a chip is switched - looking at the same spot on `G1` after `Mozaic` is exactly
  the use. So `RefreshImage` leaves zoom alone; the reset goes next to `_last = r`.
- `Reset()`: `Zoom.Reset()`.
- Tests that read `vm.Image` as bytes (`HistEqTabTests`: "different image", "same object
  from cache") compare `ImageSource` references and pixel sizes instead.

## Part 2 - View

### 2.1 `FlatCheck.Wpf/Views/ZoomableImage.xaml` + `.xaml.cs` (new files, GPL notices)

A `UserControl` that shows one `ImageSource` in a `ScrollViewer` and owns the pointer
interaction. Dependency properties: `Source` (ImageSource), `IsFit` (bool, TwoWay),
`Zoom` (double, TwoWay), `EffectiveZoom` (double, TwoWay, written by the control).

- Layout: `ScrollViewer` (both scrollbars `Auto`, `Background` = `PanelBg2`) containing a
  `Grid` containing the `Image` with `Stretch="None"` and a `LayoutTransform` of
  `ScaleTransform(EffectiveZoom, EffectiveZoom)`. `LayoutTransform`, not `RenderTransform`,
  so the scrollable extent follows the zoom.
- `EffectiveZoom` = `IsFit ? min(viewportW / pixelW, viewportH / pixelH) : Zoom`,
  recomputed on `SizeChanged`, on `Source` change and on `IsFit`/`Zoom` change. Written
  back through the DP so the view-model's label can show it.
- `RenderOptions.BitmapScalingMode` on the `Image`: `NearestNeighbor` when
  `EffectiveZoom >= 1`, `HighQuality` below (decision 3). Set in code when the zoom changes.
- **Wheel**: with `Ctrl`, zoom in/out by `Step` about the pointer - set `IsFit = false`,
  change `Zoom`, then adjust the two scroll offsets so the raster point under the pointer
  stays under it (`offset' = (offset + p) * z'/z - p`). Without `Ctrl`, the default
  scrolling. Mark the event handled only in the `Ctrl` case, so the outer page
  `ScrollViewer` still scrolls the tab with a plain wheel over the picture.
- **Drag**: left button down -> capture, remember pointer and offsets, cursor `SizeAll`;
  move -> `ScrollToHorizontalOffset`/`ScrollToVerticalOffset`; up -> release. A drag of
  under 3 px is a click and does nothing.
- **Double-click**: toggle between fit and 100 %.
- Keyboard: `+`/`-`/`0`/`1` on the focused control map to in/out/fit/actual; the control
  is `Focusable` and takes focus on mouse down, so the keys work after a click.
- No `Dark.xaml` changes are needed unless the `ScrollViewer` template there hides the
  horizontal bar; check `ScrollThumb`/the `ScrollViewer` style and, if it does, give this
  control a local style with both bars.

### 2.2 `FlatCheck.Wpf/Views/HistEqView.xaml`

Replace the `Border`+`Image` with:

```
<local:ZoomableImage Height="520"
    Source="{Binding Image}"
    IsFit="{Binding Zoom.IsFit, Mode=TwoWay}"
    Zoom="{Binding Zoom.Zoom, Mode=TwoWay}"
    EffectiveZoom="{Binding Zoom.EffectiveZoom, Mode=TwoWay}" />
```

Above it, on the same row as the chips (right-aligned): four small buttons `-`, `+`,
`potrivit`, `100 %`, bound to the commands, and the `Label` in `Faint`. Under the
picture, one `Faint` line: "Ctrl + rotita = zoom, trage cu mouse-ul = deplasare, dublu
click = potrivit / 100 %. La 100 % un pixel de pe ecran e un pixel de senzor; pe Mozaic
se vede tabla de sah a mozaicului, ca in Siril." `ViewCaption` reports the full size.

### 2.3 `Session/SessionMessages.cs` and `README.md`

- Guide row "vederea Mozaic" becomes the first row and says it is the default and why
  (it is the view that matches Siril on an undebayered raw); the "vederea RGB" row loses
  "(implicit)".
- README, Histogram tab paragraph: `Mozaic` first and default, `RGB` second; a sentence
  on zoom/pan and on what 100 % means. The mirrored table gets the same reorder.
- `CLAUDE.md`: the mode 5 section's "Channels" paragraph and the `HistEqViewViewModel`
  entry mention the default; a line under "What the window adds" on `ZoomableImage`
  and decision 3.

## Part 3 - Tests

### 3.1 `FlatCheck.Wpf.Tests/ImageZoomViewModelTests.cs` (new)

Starts in fit; `ZoomIn` from fit starts at the effective zoom and multiplies by the step;
`ZoomOut` clamps at `Min`, `ZoomIn` at `Max`; `Actual` gives 1 and leaves fit; `Fit`
returns; `Label` reads "potrivit (47 %)" when fit with `EffectiveZoom = 0.47` and "125 %"
when manual; `Reset` returns to fit.

### 3.2 `FlatCheck.Wpf.Tests/HistEqTabTests.cs`

- `PickingAnotherFileRegenerates...`: the option order is now `Mozaic, RGB, R, G1, G2, B`
  and `View.Selected.Label == "Mozaic"` after a CFA run.
- New: switching a chip does not reset zoom (`Zoom.IsFit = false; Zoom.Zoom = 2` then
  select `R` -> still `2`); picking another file does (`IsFit` back to true).

### 3.3 `FlatCheck.Wpf.Tests/WindowSmokeTests.cs`

`TheHistEqTabRendersAResult`: after the fit render, set `vm.Zoom.Actual` and render once
more (`tab-histeq-100`), then `vm.Zoom.Zoom = 3` and render (`tab-histeq-zoom`). The
binding-error assertion covers the new control; the dumps are how to look at it. Pan
and wheel are not unit-testable and are checked by hand.

### 3.4 `LicenseHeaderTests` picks up the two new files by itself.

## Part 4 - Order

| step | depends on |
|---|---|
| 0.1 full rasters in the analyzer (+tests) | - |
| 1.0 RasterBitmaps (+test) | - |
| 1.1 chip order (+3.2 order assert) | - |
| 1.2 zoom view-model (+3.1) | - |
| 2.1 control | - |
| 1.3 tab wiring | 0.1, 1.0, 1.2 |
| 2.2 XAML (+3.3) | 1.3, 2.1 |
| 2.3 docs | all |

`dotnet test FlatCheck.sln` after each; no golden file moves (the console is untouched).
Measure after 0.1: peak memory of `--histeq` on the 24 MP master (was 672 MB) and the
window's working set with one file shown, and write both into the analyzer header.

## To check by eye when done

`FLATCHECK_DUMP_SCREENS` renders, then the real window on
`Nikkor 35mm DX/f2.8-sky/ISO400`: the tab opens on `Mozaic` fit at ~780 x 520, Ctrl+wheel
zooms about the pointer without the page jumping, drag pans, a plain wheel over the
picture still scrolls the tab, and at 300 % the raster pixels are crisp squares.
