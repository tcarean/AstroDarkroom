# 0094. A FITS is read and written with its first row at the bottom, as Siril shows it

Date: 2026-11-09
Status: accepted 2026-11-09
Supersedes: 0023 in part (the `ROWORDER` rule of the CFA order, which now applies to the raster read with the file's first row last)

## Context

`FitsReader` read the rows of a FITS in the file's order and every raster had the file's first
row as row 0; `ROWORDER` was used only to move the Bayer phase (ADR 0023). Siril, measured on
2026-10-26 through `siril-cli` (`load`, `savepng`; ImageVisualCompare ADR 0039, AstroPhotoAnalyzer
ADR 0049), displays every FITS with the file's first row at the bottom whatever `ROWORDER` says,
and uses the keyword only when it debayers. A raw read through libraw comes in the camera's
orientation, which is the one Siril shows after its conversion. So FlatCheck analysed the same
flat the right way up as a NEF and upside down as the FITS Siril made of it, and everything that
says up or down inherited the flip: the residual map of the console and of the window, the histeq
picture, the printed coordinates (illumination peak, sensor centre, local spot) and the four corner
names, whose `top-left` was Siril's bottom-left.

On 2026-11-09 the owner read a horizontal band on the map of the Veil 2026-11-08 master at 82 %
down the frame while Siril and the raw show it 18 % from the top (the band is at raw rows 690-750
of 4016, FITS rows 3262-3328 of the bottom-up file). Asked where it really was, the tool had to be
read against Siril's convention; the owner asked for the fix.

Measured on the branch before deciding:

- Flipping at load is not numerically neutral. The analysis geometry is anchored on pixel
  indices: the radial model's centre is the index `(w/2, h/2)`, half a pixel below the geometric
  centre `((w-1)/2, (h-1)/2)`, and the tile grid starts at row 0 and leaves its remainder at the
  far edge. The same frame read the other way up sits one analysis pixel (four sensor pixels)
  differently against those anchors. On the 36 validation cases, 17 drift past the 0.05
  tolerance, by 0.05 to 0.58 points, none of them a verdict and none a depth: the largest are
  V-010's local spot, which becomes another spot of the same size (-0.287 to +0.290), its worst
  tile (8.809 to 8.472) and V-012's tilt (1.643 to 1.930). On the synthetic fixtures the effect is
  larger because they are centred on the same index as the model: the clean fixture's residual
  goes from 0.28 to 0.64 %. On the Veil 2026-11-08 master, GOOD stays GOOD with the residual 3.89
  to 3.82 %.
- Flipping only at drawing and in the printed text would leave every figure as it was.

The owner, shown both, chose the flip at load. A first version of the branch then recaptured
the parity goldens with their moved figures; the code review pointed out that the test fixtures
are synthetic files the tests write, so written as Siril writes a file they are read as drawn and
keep every figure. The owner chose that.

## Decision

1. **`FitsReader.LoadFull` always puts the file's first row last**, whatever `ROWORDER` says and
   whether the card is there: row 0 of every raster, FITS or raw, is the top of the frame as Siril
   shows it and as the camera shows a raw (R-130). Nothing downstream converts anything: the
   maps, the pictures, the coordinates and the corner names follow the raster.
2. **`CfaPattern.ForRowOrder` answers for that raster** (R-044): a `BOTTOM-UP` file is the sensor
   seen top-down and keeps `BAYERPAT` as written at any height; a `TOP-DOWN` or silent file of
   even height shifts the phase by one row. An `RGGB` Siril master is named `RGGB`, as its NEF is.
3. **`FitsWriter.Write` writes the raster from its last row to its first**, so the synthetic
   flat keeps its reference's row order and Siril shows it the way up it was analysed; a file with
   no `ROWORDER` card to copy gets `ROWORDER= 'BOTTOM-UP'`, a copied card is kept as it is.
4. **The test fixtures are written as Siril writes a file**, the first row of the file at the
   bottom (`FitsTestData.BuildFits`, `BuildFitsCube`, `BuildFitsWithEmptyPrimary`; the first
   two with a `bottomUp: false` escape for the tests of the flip itself), so every synthetic scene is read
   as it was drawn and the 39 parity goldens keep the figures the prototype confirmed. Only the
   17 validation cases, measured on real files, carry new figures: the 2026-11-09 ones, the old
   in their row's last column. A figure that changes by 0.3 points with the way a file is read
   was never precise to the third decimal; one orientation everywhere is worth more than
   keeping those decimals on the real masters.

## Alternatives rejected

- **Flip only at drawing and in the text.** Every figure stays, and the parity goldens change
  only in coordinates and corner names. But the raster stays upside down inside, every future
  coordinate printed must remember the conversion, the CFA names stay in file order and the
  synthetic flat's order is left alone; the class of error being fixed would come back with the
  next print. ImageVisualCompare flips at display because it computes nothing on the raster;
  FlatCheck does.
- **Obey `ROWORDER`.** The FITS convention, but not Siril's: a `TOP-DOWN` file, or one with the
  card missing, would be shown the other way up from the one program the tool is compared with,
  and Siril itself writes `TOP-DOWN` over bottom-up data after a SyQon step (ImageVisualCompare
  ADR 0039).
- **Make the geometry symmetric first** (the geometric centre, a centred tile grid), so that the
  orientation cannot move a figure. It would move the figures once more, by a small amount, and
  it is a formula change of its own, with its own validation; left as a follow-up, not mixed in.
- **Recapture the parity goldens with the moved figures**, as the first version of this branch
  did: 21 scenarios lost the prototype's confirmation for figures that had only moved because the
  fixtures were still written top-down, against Siril's convention; `parity_structure_clean` read
  0.64 % where 0.28 % was confirmed, `parity_light_nebulosity` +36.66 % where +45.09 % was. Writing
  the fixtures as Siril does keeps all of it.

## Consequences

- A genuine top-down camera file (SharpCap writes `TOP-DOWN`) is analysed upside down: exactly as
  Siril shows it, so the map and the report still match the screen. None has been met.
- The validation figures of 17 cases changed (`docs/validation.md`, History). Of the 87 goldens,
  four change, each in its four position-name lines only, every figure the same: the two
  `expo_cfa_bayerpat` (a BAYERPAT without `ROWORDER`, now named `GBRG` at `[0,0]`, R-044) and
  `parity_expo_cfa_bayerpat` the same way, and `parity_expo_cfa_bottom_up`, whose `RGGB` is now
  read as `RGGB`. Fixtures that stand for Siril files carry Siril's `ROWORDER= 'BOTTOM-UP'`; the
  two "BAYERPAT without `ROWORDER`" scenarios stay silent on purpose, as R-044's contrast.
- `RawReader` is unchanged: libraw's rows are the sensor's, top-down (checked with rawpy on the
  2026-11-08 NEFs against their Siril conversions).
- Guards: `R130_*` in `FitsReaderTests` (the three states of the card, through the path-based
  load too), `FitsWriterTests` (the bytes' order, the fresh and the copied `ROWORDER`) and
  `StructureAnalyzerTests` (a shadow in the file's first rows is reported in the lower half, the
  file's last rows are the top corners); `R044_*` in `FitsReaderTests`, `CfaPatternTests` and
  `GoldenOutputTests`.
