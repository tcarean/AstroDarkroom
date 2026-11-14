# The crop for a frame

> Written 2026-11-14 with the feature (R-180 to R-183, ADR 0093). **Covers:**
> `AstroPhotoAnalyzer.Core/Analysis/FrameCrop.cs`, `AstroPhotoAnalyzer.Core/Runs/CropRun.cs`,
> `AstroPhotoAnalyzer.Core/Report/CropTexts.cs`, `AstroPhotoAnalyzer.Core/Input/PngWriter.cs`,
> `AstroPhotoAnalyzer.Wpf/ViewModels/CropTab.cs`, `AstroPhotoAnalyzer.Wpf/Views/CropView.xaml`,
> the `CropView` class in `AstroPhotoAnalyzer.Wpf/Views/ResultViews.xaml.cs` and
> `AstroPhotoAnalyzer.Wpf/Presentation/CropViewer.cs`. The FITS crop itself, `FitsWriter.SaveCrop`,
> is in `input.md` with the rest of that writer, and the TIFF writer in `paper-fit.md`. A change
> to the code this describes means this note is updated in the same task, or deleted with the
> decision it explains.

## What it does

The Crop with aspect ratio tab takes a frame's width and height, shows over the analysed image
the rectangle of those proportions, lets the mouse move it and resize it, and saves what it
keeps as a new file of the input's own kind and depth, no sample changed. It is the tool's
third export and the only one that changes no pixel. The owner's request, the measurements and
the alternatives are in ADR 0093.

## The rectangle (`Analysis/FrameCrop.cs`)

`CropRect` is four whole numbers in pixels of the full image, the row 0 at the top as in a
`Raster`. Every rectangle the window shows comes from one of three functions, so the window
never holds one the core did not make:

- `Largest` is `CropAnalyzer.Analyze` with the frame's width over its height: the centred
  rectangle of R-032, the odd pixel of a cut on the right or bottom. The sizes are taken as
  typed, so 54.62 x 70 stands taller than wide.
- `Move` shifts by a distance and clamps to the image.
- `Resize` keeps the corner opposite the dragged one. The point the corner is pulled to asks for
  a width by its distance from that corner along x, and for another by its distance along y
  times the ratio; the larger is taken, so the corner follows the pointer along the axis it was
  pulled further on. The width is clamped between the smallest allowed (`Thresholds.CropMinSide`
  on the short side) and the largest the room from the fixed corner to the image's edges holds
  (`Fit`: the full width with its rounded height when that fits, otherwise the full height with
  its rounded width); the height is the width over the ratio, rounded, and at the largest size
  the one `Fit` gave. Hence the frame's proportions hold within half a pixel either from the
  width or from the height, which is what `R180_AResizeHoldsTheFramesProportions...` checks for
  three ratios, one pixel of pull at a time from the smallest to past the largest, from two
  opposite corners. A point past the fixed corner asks for a negative size and gets the
  smallest. The ratio is held between a millionth and a million (`Ratio`) and every size is
  clamped as a real number before it becomes a whole one: the first version cast first, and a
  frame of 40000000 x 1 gave a rectangle of a negative width, which the viewer cannot draw
  (the review of 2026-11-14; `R180_AFrameOfAbsurdProportions...`).

`ParseSize` is the rule of a typed size (a finite number above 0, with a point). `Facts` gives
the cut on each side, the share of the image's pixels kept, and the pixels per inch: the kept
width over the frame's width in inches. Only the width enters: the height's figure differs from
it by the half pixel of the rounding.

## The save (`Runs/CropRun.cs`)

`CropExport.OutputOf(SourceInfo)` chooses the file from what the reader said of the input:

| input | written as | from |
| --- | --- | --- |
| FITS, any BITPIX | a FITS of the same BITPIX | the input file's bytes (`FitsWriter.SaveCrop`) |
| TIFF, 8 or 16 bits | a TIFF of that depth, uncompressed | the raster (`TiffWriter`, `bits`) |
| TIFF, another depth | refused, with `CropTexts.TiffDepthRefused` | - |
| PNG, 16 bits | a 16-bit PNG | the raster (`PngWriter`) |
| PNG, fewer | an 8-bit PNG | the raster |
| JPEG | an 8-bit TIFF, the proposed name ending in `.tif` | the raster |
| another format (a mis-named file) | refused | - |
| a raster from memory (the tests) | a fresh 32-bit float FITS | the raster (`FitsWriter.Save`) |

Why the raster is enough for a TIFF or a PNG and not for a FITS: it holds every 8- and 16-bit
level exactly (R-013), but it is 32-bit float clipped to 0..1, so a 32-bit integer, a 64-bit
float or a value outside 0..1 would not survive it. `Save` checks the rectangle and the output
first, then runs under `CurveExport.Guarded` like the two other exports: the input is never
opened for writing. The file's kind follows the input's content, not the target's extension;
the dialog offers that one kind, and `CropOutput.Extension` is the extension to propose when
the input's own is not one of that kind's (a JPEG, or a file named after another format). A cropped TIFF or PNG carries the input's ICC profile and adds none; the
TIFF's description tag and the FITS's `HISTORY` cards name the frame and the rectangle
(`CropTexts.Description`, `History`). One Info line per file written.

`CropRun.Preview` is the reduced copy the tab shows, by the galleries' step
(`CurveRun.PreviewStride`).

## The PNG writer (`Input/PngWriter.cs`)

The raster becomes an ImageSharp image of the fitting pixel type (`Rgb48`, `L16`, `Rgb24` or
`L8`), the profile goes on its metadata, and the PNG encoder writes it at the depth and colour
type asked, through `AtomicFile.Write`. Measured on 2026-11-14 (ADR 0093): the encoder writes
the samples exactly and the `iCCP` chunk with the profile's own bytes, at the size and speed of
a hand-written PNG, so there is no writer by hand here. It also writes a `pHYs` chunk of 96 dpi
whether the input had one or not.

## The tab (`ViewModels/CropTab.cs`, `Views/CropView.xaml`, `Presentation/CropViewer.cs`)

`CropTabViewModel` holds the two fields as typed, the rectangle as the core last gave it, the
reduced copy as a bitmap, and the sentences from `CropTexts`. `Populate` keeps the result, asks
`CropExport.OutputOf` what a save would write, makes the bitmap and calls `Reframe`; `Reframe`
(also a change of either field, and the `Largest` button) parses the fields and either sets the
largest rectangle or says why there is none: the sizes are asked for, or the field that is not
a size is named, in red. `Populate` then puts back the rectangle the user had placed when the
result is a new analysis of the image last shown with the same frame (`_placed`, noted by
`Remember` when the image goes: its file, its size, the frame, the rectangle): a changed
parameter or profile runs the analysis again by itself, and must not undo the placing. The fields are the one thing the tab keeps from an image to the next
(`Clear` leaves them): the frame does not change with the image. Unlike the galleries, nothing
here is computed after the analysis on a pool thread: the reduced copy is a subsample, made at
once, and made again when the area the view reports allows a finer step (`SetArea`, the rule of
ADR 0084); the rectangle is in pixels of the full image and does not move then.

`CropViewer` is one `FrameworkElement` that draws, in `OnRender`, the image fitted to its area
(both axes, centred, enlarged too, since the owner asked for the image on the whole area), a
half-transparent black over the image outside the rectangle, the rectangle's line (white on a
wider dark one, so that it shows on any image) and a square handle at each corner. Its
dependency properties are the image, the full image's size and the rectangle; it converts
between its device-independent pixels and the image's pixels (`ImageArea`, `PixelSize`,
`CropArea`) and decides only what a press grabs (`HitTestGrip`: the nearest corner within
`HandleGrab` on both axes, otherwise the rectangle when the point is inside it; inside the
rectangle a corner reaches no further than a third of each side, or a rectangle smaller than
its handles, 117 image pixels on a 23 MP image, could not be moved at all). A drag is told
through four internal events: started, moved by a distance from its start, a corner pulled to a
point, ended; a corner keeps the distance from the pointer it was grabbed at, so it does not
jump. `CropView`'s code-behind hands the events to the view-model (`BeginDrag`, `MoveBy`,
`ResizeTo`, `EndDrag`), which asks `FrameCrop` and raises the four numbers, which the viewer's
bindings draw. The three drag methods of the viewer are internal and hold the whole rule, so
`CropTabTests` drives a drag without a mouse. There is no zoom or pan (the owner's choice, ADR
0093): on a 23 MP image shown at a quarter of its size the rectangle is placed to about four
image pixels.

The save runs on a pool thread (`SaveAsync`; the 19 MP 16-bit crop of `de_printat.png` took
10.1 s through `CropExport.Save` on 2026-11-14, and 16.8 s in ADR 0093's probe, which also
cloned and cropped the image through ImageSharp), with
`CropTexts.Saving` in the fixed place of the save message and the button off meanwhile; its
outcome is shown even when another image was opened in between, since the file was written or
was not, whatever the tab shows now; while it lasts, another image leaves the message in place
and a second `SaveAsync` returns the save in progress. `Save` is the same on the calling
thread, for the tests, and `Writer` is the save itself as a field, so that a test can hold
one in progress. Being the one export written off the UI thread, it is also the one the end
of the process could stop in the middle, leaving `AtomicFile`'s temporary file and no crop:
`MainWindow.WaitForTheCropBeingWritten` cancels a close asked meanwhile and closes the window
when `Saving` completes (`R183_ClosingTheWindowWaits...`).
