# Add the Crop with aspect ratio tab

Date: 2026-11-14. Branch `claude/Add-crop-with-aspect-ratio-tab`, worktree
`AstroPhotoAnalyzer/.claude/worktrees/Add-crop-with-aspect-ratio-tab` (from `main` at 9677ecf7; the kit's rule "Worktrees"). Approved by the owner on 2026-11-14.

## Context

The owner has a frame of a fixed size (70 x 54.62 cm) and wants the image cut to exactly its
proportions: type the two sizes, see a rectangle of those proportions over the image, move and
resize it to choose what is left out, and save the result as a **new** file of the input's own
kind and depth, with no loss (32-bit FITS to 32-bit FITS, 16-bit TIFF to 16-bit TIFF), under the
proposed name `<input>_cropped_XXxYY`.

Already settled with the owner on 2026-11-14 (requirement rows R-180 to R-183, accepted as a
draft; ADR 0093, proposed, which this plan's approval accepts):

- A JPEG input is saved as an 8-bit TIFF (no second compression).
- A tab named `Crop with aspect ratio` (`Decupare cu raport de aspect`), the last one; the image
  fills the tab's area, no zoom or pan.
- The two sizes are read as typed: width, then height. No rotate button.
- Mine, shown with the draft and not objected to: a line with the pixels per inch at the frame's
  size; the two fields stay as typed when another image is opened; nothing in the console.

Measured on 2026-11-14 (ADR 0093 holds the table): ImageSharp 3.1.11's PNG encoder writes the
16-bit crop of `de_printat.png` sample for sample, 85.1 MiB in 16.8 s, **with** the `iCCP`
profile, the same bytes as the source's. So the PNG is written by the encoder, not by hand. This
contradicts the sentence "the PNG encoder does not write `iCCP`" in `dependencies.md`, `input.md`
and the fixtures' comments; the two notes are corrected, the hand-built fixture stays.

## What exists and is reused

- `Analysis/CropAnalyzer.Analyze(w, h, aspect)`: the largest centred rectangle (R-032). The new
  rectangle starts from it.
- `Runs/CurveRun.cs`: `CurveExport.Guarded` (the guards of R-145: never the input, the input held
  while writing, the input gone) and `CurveRun.PreviewStride` (the reduced copy for the screen).
- `Input/FitsWriter.cs`: its private `Copy`, `ReadExact`, `WriteCards`, `HistoryCard`;
  `FitsReader.ReadHeader`, `HInt`, `HDbl`. `Input/AtomicFile.Write` (whole or not at all).
- `Input/TiffWriter.Save` (16-bit; gains an 8-bit depth). `RasterOps.Subsample`, `Level8`,
  `Level16`. `Wpf/Presentation/RasterBitmaps.Region`.
- The tab pattern of `ViewModels/GamutFitTab.cs` + `Views/GamutFitView.xaml` +
  `MainWindow.SaveGamutFit` (the `SaveRequested` event, the fixed place of the save message).

## The change

### 1. Tests first (they fail; their output is pasted before any code)

`AstroPhotoAnalyzer.Tests/FrameCropTests.cs`:

- `R180_...`: the largest rectangle on 5938 x 3888 with 70 x 54.62 is 4983 x 3888 at x 477; the
  sizes as typed (54.62 x 70 is taller than wide); a move stops at each edge; a resize from each
  corner keeps the opposite corner and the proportions within half a pixel, never leaves the
  image, never goes under `CropMinSide`; the figures (cut on four sides, share kept, pixels per
  inch); a size that is not a number above 0 gives no rectangle and names the field.
- `R181_...`: a FITS crop for BITPIX 8, 16, 32, -32, -64, mono and colour: the data equal the
  input's bytes for the rectangle (values outside 0..1 included), the cards kept in order,
  `NAXIS1/2` new, `CRPIX1/2` moved (and untouched cards when there is no WCS), `HISTORY` added,
  the HDUs around copied, a `TOP-DOWN` file's rows right; a 16-bit and an 8-bit TIFF, a 16-bit
  and an 8-bit PNG, colour and grey, read back equal to the rectangle of the analysed raster,
  with the input's profile (none when the input has none); a JPEG gives an 8-bit TIFF; a TIFF of
  another depth is refused with the sentence; the refusals of R-145 (over the input, the input
  gone) and no file left after a failed write.
- `R182_...`: the proposed names (`M31_cropped_70x54.62.fit`, `poza_cropped_50x40.tif` from a
  JPEG with 40.0 typed).

`AstroPhotoAnalyzer.Wpf.Tests/CropTabTests.cs` (+ a `Render` line in
`WindowSmokeTests.R110_EveryTabRenders...`, and the English guard of `WindowEnglishTests`):

- `R183_...`: empty fields give the image alone, the save disabled and the asking sentence; two
  valid sizes give the rectangle and the two sentences; a bad field is named; `Largest`, a
  changed field and another image put the rectangle back; a drag inside moves it, a drag on a
  corner resizes it (driven through the viewer's internal drag methods, no mouse, as
  `BeforeAfterViewerTests` does); the save writes the file and shows `scris: ...`; the fields
  survive another image; the view renders (`APA_DUMP_SCREENS` for a look at the PNG).

### 2. The core (`AstroPhotoAnalyzer.Core`)

- `Thresholds.cs`: `CropMinSide = 64`, with its comment (a rule of thumb: under it a rectangle
  cannot be grabbed on a reduced copy and is no print).
- `Analysis/FrameCrop.cs` (new): `CropRect` (X, Y, W, H in image pixels), `CropCorner`,
  `FrameCropFacts`; `Largest` (through `CropAnalyzer.Analyze`), `Move`, `Resize` (the opposite
  corner fixed, the larger of the two pulls decides the size, clamped to the image and to
  `CropMinSide`), `Facts`.
- `Report/CropTexts.cs` (new): every sentence of the tab as `Tr.Pick` pairs (what is kept and
  cut, the pixels per inch line, the kind of file the save writes and why a JPEG becomes a TIFF,
  the asking sentence, the bad-field sentence, the refusal of a TIFF depth), `ProposedFileName`,
  `History`, the parse of a typed size.
- `Input/FitsWriter.cs`: `SaveCrop(sourceFits, rect, history, target)`: the header's cards
  copied, `NAXIS1/2` and `CRPIX1/2` rewritten in place (the comment kept), `HISTORY` before
  `END`; the data copied row by row from the source file (file row = height - 1 - raster row),
  plane after plane; the HDUs around through `Copy`.
- `Input/TiffWriter.cs`: a `bits` argument, 16 (default, today's callers unchanged) or 8.
- `Input/PngWriter.cs` (new): the raster as `Rgb48` / `L16` or `Rgb24` / `L8` through
  ImageSharp's PNG encoder, the profile set on the image's metadata, to `AtomicFile.Write`.
- `Input/RasterOps.cs`: `Crop(raster, rect)`.
- `Runs/CropRun.cs` (new): `CropRun.Preview(a, maxW, maxH)` (the reduced copy) and
  `CropExport`: `KindOf(a)` (FITS / TIFF 8 or 16 / PNG 8 or 16, or the refusal) and
  `Save(a, rect, frameW, frameH, target)` under `CurveExport.Guarded`, one Info log line per
  file written (`logging.md`).

### 3. The window (`AstroPhotoAnalyzer.Wpf`)

- `Presentation/CropViewer.cs` (new): one element that draws the image fitted to its area, the
  darkened part outside the rectangle, the border and the four corner handles, and turns the
  mouse into image pixels: a press on a handle resizes, a press inside moves, the cursor says
  which; it raises the drag as events and decides nothing about the rectangle. Internal
  `BeginDrag` / `DragTo` / `EndDrag` hold the whole rule, for the tests.
- `ViewModels/CropTab.cs` (new): `CropTabViewModel` (the two fields, the bitmap, the rectangle as
  four integers, the sentences from `CropTexts`, `Largest`, `SaveAs` + `SaveRequested`,
  `Save(path)` on a pool thread with the outcome in the fixed place). It calls `FrameCrop` for
  every move; it computes nothing.
- `Views/CropView.xaml` + its class in `ResultViews.xaml.cs`; a `TabItem` after Soft proof in
  `MainWindow.xaml`; `MainWindow.SaveCrop` (the dialog with the proposed name, one filter);
  `MainViewModel` (`Crop` property, `Populate`, `Clear`); `Loggers.Crop`.

### 4. Documents

- `docs/requirements.md`: R-180 to R-183 to `implemented`; R-110 (nine tabs, the list) and R-163
  ("the last tab") follow.
- `docs/decisions/0093-...`: `accepted 2026-11-14`.
- New design note `docs/design/crop.md` (with its `Covers:` and its row in `design/README.md`);
  `input.md` (the writers, the corrected `iCCP` sentence), `dependencies.md` (the second place
  ImageSharp encodes, the corrected sentence), `extending.md` ("three kinds of image"),
  `window.md` and `code-map.md` (the new files), `logging.md` (the new lines),
  `known-limitations.md` (what a TIFF or PNG crop does not carry; no zoom).
- `CLAUDE.md` (first paragraph: the third export; "nine tabs"; `Runs/CropRun.cs`), kept within
  its 200 lines. `README.md`: a "Crop for a frame" section under "The window". `CHANGELOG.md`:
  an `Added` line.
- The plan saved as `docs/plans/2026-11-14-add-the-crop-with-aspect-ratio-tab.md`.

## Order and how each step is verified

1. The tests of step 1, run and failing (output pasted).
2. The core; `scripts/verify.ps1 -SkipTests` green while editing; the core tests green.
3. The window; its tests green; the tab's render looked at as a PNG (`APA_DUMP_SCREENS`).
4. On the real files, output pasted: the plate-solved `starless_rbf0.5_stretch0.17.fit` cropped
   and re-read (data bytes equal, `CRPIX` moved, the tool's WCS naming the same sky for a pixel
   before and after); `de_printat.png` cropped and re-read (samples and profile equal).
5. `/validate` (code under `Input/`, `Analysis/`, `Report/` and `Thresholds.cs` changed; no
   existing figure should move), the drift table pasted.
6. `dotnet build --no-incremental -warnaserror`, the full gate, the whole diff read back.
7. A review by a subagent before the merge is proposed; then `/wrap-up`: commit on the branch,
   the merge proposed and asked.

## Not in this task

- No console flag for the crop; `--aspect` and the size table stay as they are.
- No zoom on the crop's image, no arrow-key nudging of the rectangle.
- No version bump (the owner's); the change adds a feature, so the next release is a minor one.
