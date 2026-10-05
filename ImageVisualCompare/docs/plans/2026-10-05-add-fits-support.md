# Add FITS support (.fit, .fits, .fts)

## Context

ImageVisualCompare opens only what WIC decodes (PNG, TIFF). The owner's processing lives in
FITS files written by Siril and GraXpert (`E:\Photografie\Claude\Processed_WithFlats_RBF_light\*.fit`),
so comparing two variants today needs an export of each first. The task: open a FITS file as
image A or B, directly, next to or against a PNG/TIFF.

Decided by the owner on 2026-10-05 (in this session; they go into one ADR, accepted):

1. **Linear, as in the file.** No autostretch. A FITS shows exactly as its PNG/TIFF export
   would, so the RGB readout stays honest. A linear stack looks nearly black; that is accepted.
2. **Row order.** `ROWORDER` is obeyed when present; a file without it is bottom-up, as Siril
   shows it. (This differs from AstroPhotoAnalyzer's reader, which flips only on `BOTTOM-UP`.)
3. **Float scale is detected.** Float data in 0..1 is the default; when the data clearly
   exceeds 1 it is read as 0..65535, and the status bar says which scale was used.

No package: FITS is a 2880-byte-block format and the monorepo already has a reader to start
from (`AstroPhotoAnalyzer/AstroPhotoAnalyzer.Core/Input/FitsReader.cs`: header cards,
BZERO/BSCALE, HDU skipping, all BITPIX, streamed reads). It is adapted, not referenced: English,
this project's strictness, 8-bit output instead of float planes. The rule "No imaging
dependency" holds.

## Draft requirements (Formats block, next free IDs; status `draft` until tested)

- **R-001 (edit).** Filter entries become `Images (*.png;*.tif;*.tiff;*.fit;*.fits;*.fts)`
  (default), `PNG`, `TIFF`, `FITS (*.fit;*.fits;*.fts)`, `All files`.
- **R-014 FITS is accepted.** A file whose first bytes are `SIMPLE  =` is read as FITS,
  whatever its extension; anything else goes to WIC as before. The image is the first HDU with
  2 axes, or 3 axes with `NAXIS3` 1 or 3; `BITPIX` 8, 16, 32, -32, -64; `BZERO`/`BSCALE`
  applied. One plane is shown gray (R = G = B, no debayer). Result: Bgra32, frozen, file not
  locked, like R-010. Example: Siril's `result_3813s.fit`, 3 planes of 32-bit float.
- **R-015 Values map linearly.** Integers over their full range (16-bit with `BZERO` 32768 is
  0..65535), float over 0..1; reduced to 8 bits by going through the 16-bit value
  (`round(v × 65535)`) and then exactly as WIC reduces an Rgb48 PNG. Acceptance: a 16-bit FITS
  and an Rgb48 PNG holding the same samples load to byte-identical Bgra32 buffers.
- **R-016 Float scale.** While decoding, the largest finite sample is tracked; above
  `FitsScale.RescaleThreshold` the file is taken as 0..65535 and divided by 65535. NaN and
  values below 0 become 0; values above the scale become 255. The threshold's value is chosen
  after measuring the maxima of the five real files (expected: Siril overshoots stay near 1).
  Example: a float file with maximum 41 200 shows 41 200 as 160, not everything white.
- **R-017 Row order.** `ROWORDER = 'TOP-DOWN'`: first row in the file is the top row. `BOTTOM-UP`
  or no keyword: it is the bottom row. Example: a 2 × 2 FITS without the keyword whose first
  sample is 1.0 shows white at the bottom left.
- **R-018 What the status bar says.** `ImageInfo.FormatDescription` gains the word `float`:
  `32-bit float RGB`, `64-bit float gray`; integer FITS read like any file (`16-bit gray`).
  A rescaled file adds ` (0..65535)`. `Probe` reports the same size, depth and channels as the
  load (R-011); the scale is known only after the decode and only the loaded image carries it.
- **R-019 FITS failures are messages.** No image HDU, more than 3 axes, a plane count other
  than 1 or 3, an unsupported `BITPIX`, tile-compressed data (`ZIMAGE`), a truncated header or
  data block: each gives `Could not read <file>.` plus its own line (R-005), nothing crashes.
  Raised as `NotSupportedException` / `EndOfStreamException`, both already expected by
  `ImagePairLoader.IsExpectedLoadFailure`.

## Design

**Core (`src/ImageVisualCompare.Core/`, pure, unit tested on `MemoryStream`):**

- `FitsHeader.cs` - card parsing and HDU walk, from APA's `ReadHeader`/`LoadFrom`; returns
  `FitsImageLayout(Width, Height, Planes, BitPix, BZero, BScale, BottomUp, DataOffset)`.
  `FitsHeader.IsFits(Stream)` is the signature test.
- `FitsRowDecoder.cs` - given the layout and the stream(s), fills one Bgra32 row at a time, top
  row first: FITS is plane-sequential, so it reads the same row from each plane (one buffered
  position per plane), applies BZERO/BSCALE, the scale and the 8-bit reduction, and reports the
  maximum finite sample seen. No whole-image float buffer exists at any point.
- `FitsScale.cs` - the mapping and `RescaleThreshold`; the single place the numbers live.
- `ImageInfo.cs` - two optional members (`IsFloat`, `ValueScaleNote`), existing callers
  unchanged; `FormatDescription` words them (R-018).

**Wpf (`src/ImageVisualCompare.Wpf/Imaging/`):**

- `FitsImageLoader.cs : IImageLoader` - `Probe` reads the header only; `Load` writes the rows
  into the bitmap and freezes it. If the maximum passed the threshold, the rows are decoded a
  second time with the 65535 scale (only such files pay twice).
  How the rows reach a `BitmapSource` (`WriteableBitmap.WritePixels` per row, or one managed
  buffer into `BitmapSource.Create`) is chosen by measuring private memory and time of both on
  `result_3813s.fit` (the kit's rule "Measured numbers"); the figures go into the ADR.
- `RoutingImageLoader.cs : IImageLoader` - `IsFits` → `FitsImageLoader`, else `WicImageLoader`.
  `ImagePairLoader` does not change (ADR 0008's promise).
- `MainWindow.xaml.cs:33` constructs the routing loader; `Services/FileDialogService.cs` gets
  the new filter. No change in `Controls/`.

## Order of work (the `/new-feature` skill)

0. Worktree from the git root `E:\Photografie\Code`:
   `git worktree add .claude/worktrees/Add-FITS-support -b claude/Add-FITS-support main`.
   Save this plan as `docs/plans/2026-10-05-add-fits-support.md`.
1. `docs/requirements.md`: the entries above, `draft`.
2. Failing tests, all in `tests/ImageVisualCompare.Tests`, fixtures generated at run time:
   `TempImages.WriteFits(name, bitpix, width, height, planes, rowOrder, samples)` (never a
   binary file). `FitsHeaderTests`, `FitsRowDecoderTests`, `FitsScaleTests` (Core, R014-R017,
   R019), `FitsImageLoaderTests` and `RoutingImageLoaderTests` (R014, R015 parity with the
   Rgb48 PNG, R018 probe equals load), an `ImageInfoTests` case (R018), an
   `ImagePairLoaderTests` case (FITS against PNG as one pair). Run, paste the failures.
   `docs/validation.json`: case V-003, `result_3813s.fit` against
   `result_3813s_Graxpert_AI.fit`, `expected` null; note in `docs/validation.md`.
3. ADR `docs/decisions/0029-fits-is-read-by-the-projects-own-reader-linear-bottom-up-scale-detected.md`,
   accepted 2026-10-05 (the three answers above), with the alternatives rejected (autostretch,
   top-down default, clip only, a FITS package, a reference to APA's assembly).
4. Core, then Wpf, keeping `scripts/verify.ps1 -SkipTests` green. Measure the threshold and the
   bitmap path on the real files before fixing either.
5. Real data: copy the two FITS into `tests/testdata/large/` (git-ignored; add `*.fit` handling
   to the ignore check by hand, the root `.gitignore` does not cover it), add them to
   `tests/testdata/manifest.json` with `scripts/check-testdata.ps1 -Update` (an intended
   addition, no existing hash changes), run `/validate`, fill V-003, paste the output.
6. Docs: `docs/design/imaging.md` (Covers + a FITS section), `docs/design/code-map.md`,
   `CLAUDE.md` (opening lines, Layout, the rule "No imaging dependency": WIC decodes PNG/TIFF,
   the project's own reader FITS), `README.md` in Romanian ("Ce fișiere îi dai", the status
   bar section), `CHANGELOG.md` Added, requirements to `implemented`. GPL header on every new
   `.cs` file.
7. `/wrap-up`: full gate, launch the application on the FITS pair and on FITS against PNG and
   look at a screenshot (the rule "Run the application"), read the diff, commit on the branch,
   propose the merge.

## Verification

- `scripts/verify.ps1` green (build, format, tests, design-note index, licence headers,
  `RepoInvariantTests` on R-014..R-019).
- The parity test: same samples as 16-bit FITS and as Rgb48 PNG give identical Bgra32 bytes.
- `/validate`: V-001 and V-002 unchanged (WIC path untouched), V-003 measured.
- By hand: `dotnet run --project src/ImageVisualCompare.Wpf -- result_3813s.fit result_3813s_Graxpert_AI.fit`;
  orientation compared with Siril's display of the same file; status bar reads
  `32-bit float RGB` for both; a FITS against its own PNG export shows no difference across
  the divider.

## Not in this task

- Autostretch or any display stretch (a linear stack stays dark), debayering of CFA frames,
  tile-compressed FITS, multi-extension selection beyond the first image HDU.
- The version: a new input format reads like a minor bump (1.2.0); the owner decides.

## As built (2026-10-05)

- The requirement texts are those of `docs/requirements.md`, R-014 to R-019, which refine the
  drafts above: the 8-bit reduction is the high byte, as measured on WIC; the threshold is 2.0.
- One stream and a seek per plane, not one stream per plane; `ReadFileRow` takes the row of the
  file, and `FitsImageLayout.DisplayRow` places it.
- `ImageInfo.ValueRange`, not `ValueScaleNote`.
- A second case, V-004 (a FITS against `de_printat.png`), beside V-003; `ValidationTests` loads
  through `RoutingImageLoader`.
- After an independent review of the first commit (the owner: "fix them all"): the header is
  validated (negative or absurd sizes hung or crashed the reader), the float scale goes by the
  share of samples above 2.0, more than 1 in 100, not by the largest sample, the decode loop is
  one loop per sample type on an unbuffered stream (0.95 s to 0.60 s on `result_3813s.fit`),
  the load checks its cancellation token, and the routing loader opens the file off the
  calling thread. The header tests were written first and seen to fail.
- Step 2 was not kept in order: the tests and the code were written together, so no failing
  run was recorded before the code existed.
