# 0049. A FITS is read and written with its first row at the bottom, whatever `ROWORDER` says, as Siril shows it

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

Since the reader was written, the rows of a FITS were flipped only when the header said
`ROWORDER = 'BOTTOM-UP'`, which is what Siril writes: a file without the keyword, or with
`TOP-DOWN`, kept the file's order, so its first row was the raster's top row. The writer did
the reverse: a FITS made from a PNG or a TIFF was written top-down and said so, and a FITS
made from a FITS kept the source's order. ADR 0025's dust catalogue and the lent WCS of R-027
took the flip from the keyword in the same way.

On 2026-10-26, in ImageVisualCompare, the owner found a Siril 1.4.4 file written after a SyQon
step with `ROWORDER = 'TOP-DOWN'` over data stored bottom-up like its siblings', and the
measurement that followed (ImageVisualCompare ADR 0039) settled what Siril does with the
keyword: it reads and displays every FITS with the file's first row at the bottom, and uses
`ROWORDER` only to correct the Bayer pattern at demosaicing. Exported through `siril-cli`
(`load`, `savepng`), PNG row r is FITS row H - 1 - r for the `TOP-DOWN` file as for a
`BOTTOM-UP` one, correlation 1.000 on four rows of each; Siril's documentation of the keyword
says the same, and its `readfits` has no flip on it.

For this tool that meant three defects. A FITS without the keyword, or the lying `TOP-DOWN`
file above, was analysed upside down against Siril, so the report named the wrong corner and
the catalogued dark nebulae were looked for in the wrong place. A FITS saved from a PNG was
one Siril shows upside down, since Siril does not read the `TOP-DOWN` it carried. And a WCS
lent to a PNG from such a FITS was applied with the wrong flip. The owner asked for the same
fix as in ImageVisualCompare, in the same branch.

## Decision

`FitsReader.ReadData` always flips: the file's first row becomes the raster's last, so row 0 of
the raster is the top of the image as Siril shows it. `ROWORDER` is read only to remark, in
the reader's note, that a `TOP-DOWN` value was ignored. `Wcs.TryFrom` is always given a flipped
raster, by the reader and by `AnalysisRun.BorrowWcs`; its `rowsFlipped` parameter stays, for
the geometry tests. `FitsWriter.WriteData` always writes the raster from its last row to its
first: from a FITS source that keeps the file's own order and its keyword true; from any other
source the file says `ROWORDER = 'BOTTOM-UP'`, and Siril shows it the way up it was analysed.

## Alternatives rejected

- **Keep obeying the keyword**, as the FITS convention suggests. The tool's reference is what
  the owner sees in Siril; a file Siril shows one way up and the tool the other way is a wrong
  report, whatever the standard says, and such a file exists.
- **Write the fresh FITS top-down and say so**, as before. Correct by the keyword, upside down
  in Siril, the one program the file is made for.
- **Keep the old default and flip only the lying file by a heuristic** (comparing it with a
  sibling). There is no sibling at analysis time, and no heuristic beats Siril's own rule.

## Consequences

- A genuine top-down file, as camera drivers write them, is analysed upside down: exactly as
  Siril shows it, so the report's corners still match the screen. None has been met.
- The note "the rows were flipped after ROWORDER = BOTTOM-UP" is gone from the report of the
  usual Siril file; a `TOP-DOWN` file gets "ROWORDER = TOP-DOWN was ignored" instead.
- `FitsReader.ReadImageHeader` no longer returns the flip; `ReadData` and `WriteData` lost
  their `bottomUp` parameter.
- The plate-solved reference FITS (`ROWORDER = 'BOTTOM-UP'`) and `de_printat.png` are read as
  before, so no validation figure moves; `/validate` was run on all fourteen cases to show it.
- The synthetic FITS of the golden `dark-sky-wcs` (R-044) had no keyword and was read in file
  order; read flipped, its warm corner moved to the top and the catalogued clouds fell on
  mirrored pixels, 45 lines of each golden. The owner chose to write that fixture as Siril
  writes a file, bottom row first under `BOTTOM-UP`, so the scene is read as it was drawn and
  the goldens did not change.
- Guards: `R011_*` in `FitsReaderTests` (the three values of the keyword),
  `R145_AFreshFitsIsWrittenBottomUpSoThatSirilShowsItTheRightWayUp` and the `ROWORDER` check
  of `R145_TheCurvedFitsIsReadBackAsTheCurveApplied` in `CurveTests`,
  `R027_TheLentWcsIsFlippedWhateverTheFitsRowOrderSays` in `WcsBorrowTests`.
- ImageVisualCompare follows the same rule (its ADR 0039); FlatCheck, LightsCheck and
  DarksCheck read a FITS in file order and use the keyword for the Bayer phase only, which is
  Siril's own use of it, and were not changed.
