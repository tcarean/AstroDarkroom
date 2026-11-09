# Show every FITS as Siril shows it (R-130, R-044 amended, ADR 0094)

## Context

On 2026-11-09 the owner read a horizontal band on FlatCheck's residual map of the Veil 2026-11-08
master at 82 % down the frame, while Siril and the raw NEF show the same band 18 % from the top.
FlatCheck reads a FITS in file order and draws file row 0 at the top; Siril (measured 2026-10-26,
ImageVisualCompare ADR 0039, AstroPhotoAnalyzer ADR 0049) draws every FITS with the file's first
row at the bottom whatever `ROWORDER` says, and uses the keyword only for the Bayer phase. A raw
read through libraw comes in the camera's orientation, so today the same flat as NEF and as its
Siril conversion is analysed upside down one against the other. Everything that says "up/down"
inherits the error: the PNG and window maps, the histeq picture, the printed coordinates
(illumination peak, local spot, sensor centre), the four corner names (`top-left` of the report is
Siril's bottom-left). The owner chose, 2026-11-09: flip at load, always, as AstroPhotoAnalyzer did
(not at drawing), recapture the parity goldens whose coordinate and corner lines mirror, and name
the CFA positions as the sensor has them (R-044). No figure in `docs/validation.json` is a
coordinate, so `/validate` is expected to show zero drift.

Branch `claude/Show-every-FITS-as-Siril-shows-it`, worktree of the same name.

## Steps

1. **Requirements first.** `docs/requirements.md`: new `R-130` in the Console contract block, after
   R-009: a FITS is analysed with the file's first row as the raster's LAST row, whatever
   `ROWORDER` says and whether it is there, so row 0 of every raster, FITS or raw, is the top of
   the frame as Siril shows it and as the camera shows a raw; every coordinate, corner name, map
   and picture follows; the synthetic flat is written from the raster's last row to its first, so
   the file keeps the reference's order and a fresh file says `ROWORDER = 'BOTTOM-UP'`. Amend
   `R-044`: `BAYERPAT` is applied to the flipped raster, so a `BOTTOM-UP` file keeps its pattern as
   written at any height and a `TOP-DOWN` (or silent) file of even height shifts by one row; the
   example becomes "an `RGGB` frame written bottom-up at 6016x4016 is named `RGGB`".
2. **Tests before code** (all failing first, pasted in the session):
   - `FlatCheck.Tests/FitsReaderTests.cs`: `R130_TheFirstRowOfTheFile_IsTheRasterLastRow_WhateverRowOrderSays`
     (theory over `BOTTOM-UP`, `TOP-DOWN`, no card; a 4x3 frame whose rows differ), on
     `FitsReader.Load(path)` and `FitsReader.Load(byte[])`; rewrite
     `ABottomUpFrameOfEvenHeightShiftsTheOrderByOneRow` and `...OddHeightKeepsTheOrder` into
     the new rule (a `BOTTOM-UP` frame keeps `RGGB` at 16 and 15 rows; a frame without the card
     at 16 rows reads `GBRG`).
   - `FlatCheck.Tests/CfaPatternTests.cs`: the `ForRowOrder` table inverted to the new semantics
     (the method now answers for a raster read with the file's first row at the bottom).
   - Writer round trip, in the file that already tests `FitsWriter` (`SyntheticRunTests` or a
     new `FitsWriterTests.cs`): `R130_TheSyntheticFlat_IsWrittenFromTheLastRowToTheFirst` (the
     bytes: file row 0 holds the raster's last row; reading the bytes back gives the raster),
     and a fresh file with no cards carries `ROWORDER= 'BOTTOM-UP'`, while a copied `ROWORDER`
     card is kept as it is.
   - Orientation end to end, `FlatCheck.Tests/StructureAnalyzerTests.cs`: a FITS fixture with a
     dust spot drawn in the file's first rows (the top of the file) is reported at a `LocalSpotY`
     in the lower half of the analysis raster, and `Corners["stanga-sus"]` holds the file's last
     rows (`R130_...`).
   - `GoldenOutputTests.R044_Expo_OnABottomUpFrame_NamesTheChannelsByFileRowOrder` becomes
     `..._NamesTheChannelsAsTheSensorHasThem` and asserts `R  (CFA[0,0]):`.
   - `FlatCheck.Wpf.Tests/StructureTabTests.cs:271` (`stanga-sus` in a note): check how its
     fixture is drawn; if the corner moves with the flip, draw the fixture's bright corner in
     the file's last rows so the note still names `stanga-sus`, and say so at wrap-up.
3. **Code**, `FlatCheck.Core` only:
   - `FitsReader.LoadFull` (`FitsReader.cs:369-380`): fill `img.D[(n2 - 1 - y) * n1 + x]` in both
     branches (one plane, planes averaged); no second raster, no extra memory. Doc comments of
     `Load`, `LoadFull`, `Load(byte[])`, `LoadWithHeader` say the row rule.
   - `CfaPattern.ForRowOrder` (`CfaPattern.cs:599`): new rule — `BOTTOM-UP`: return `this`
     (the raster is the sensor top-down, `BAYERPAT` applies as written); anything else, even
     height: `Shift(1, 0)`; odd: `this`. Doc comment rewritten; the two callers in `FitsReader`
     (`Load`, `LoadInfo`) and their comments updated.
   - `FitsWriter.Write` (`FitsWriter.cs:108-125`): iterate `y` from `img.H - 1` down to 0, `x`
     across, instead of `foreach (double v in img.D)`; add `ROWORDER= 'BOTTOM-UP'` after the
     structural cards when no copied card carries the keyword (`Text("ROWORDER", ...)` helper
     beside `Number`). `Encode` unchanged.
   - `RawReader` unchanged: libraw's rows are the sensor's top-down (measured on the 11-08 NEFs
     with rawpy: the band at raw rows 690-750 from the top is at FITS rows 3262-3328 of the
     4016-row bottom-up file). Said in the design note, not in code.
   - No report, analyzer, PNG or window change: coordinates and corner names follow the raster.
4. **Goldens.** Regenerate `GoldenOutputTests|EnglishOutputTests`, read the whole diff: expected
   classes of change are the `y` of the peak, the local spot and the sensor centre (`y` becomes
   `H - 1 - y` on the analysis raster), `top-*` ↔ `bottom-*` corner values swapped, and in the
   `expo` CFA goldens of even height the `[0,·]` and `[1,·]` position lines swapping their
   figures (the same set of numbers; the colour names follow their values). Then, with the
   owner's yes of 2026-11-09, `ParityGoldenTests` (15 goldens hold coordinates, corners or CFA
   lines); any line that does not fall in those classes, or any new figure, is a finding: stop and
   show the owner. Record the recapture in `docs/design/python-parity.md` (a C#-only call; the
   prototype read the file order).
5. **Docs in the same task.** ADR `0094-a-fits-is-read-and-written-with-its-first-row-at-the-bottom-as-siril-shows-it.md`
   (context with the 11-08 measurement, the decision, alternatives rejected: flip at drawing
   only, obey `ROWORDER`; consequences: goldens, R-044, raws unchanged, the synthetic file's
   order, a genuine top-down camera file is analysed upside down exactly as Siril shows it).
   Design notes: `code-map.md` (`FitsReader`, `FitsWriter`, `CfaPattern` entries), `modes-expo.md`
   ("The channel order" paragraph), `pipeline.md` (input contract: row 0 is the top as Siril
   shows it), `png-output.md` (the map is the frame as Siril shows it), `modes-synthetic.md`
   (the writer's row order). `README.md`: one sentence where the map and the coordinates are
   explained (the map, the `(x, y)` and the corner names are oriented as Siril shows the frame
   and as the camera shows a raw; `(0, 0)` top-left of that view). `CHANGELOG.md` under
   `[Unreleased]`, `Fixed`: maps, coordinates and corner names of a FITS were upside down
   against Siril; `Changed`: R-044's names. This plan saved as
   `docs/plans/2026-11-09-show-every-fits-as-siril-shows-it.md`. Memory
   `siril-ignores-roworder-on-display` updated (FlatCheck now follows Siril too).
6. **Validate.** `/validate` on the real masters (`FLATCHECK_TESTDATA_LARGE`), drift table pasted;
   zero drift expected (no figure is a coordinate; 6016x4016 divides exactly, so no dropped
   rows move). Then the Release console on
   `E:\Photografie\Astro\Veil\2026-11-08\masters\pp_flat_stacked.fit` into the scratchpad: the
   local spot at `(981, 184)` and the band in the upper fifth of the map, as in Siril.
7. **Gate and commits.** `scripts/verify.ps1` green; commits on the task branch: the requirement
   + tests + code, the goldens, the ADR and docs; a subagent code review, its fixes a commit
   apiece; then the merge proposed to the owner. The version is the owner's: 2.4.0 suggested at
   wrap-up, not bumped.

## Verification

- `dotnet test FlatCheck.sln -c Release --filter "FullyQualifiedName~R130|FullyQualifiedName~R044|FullyQualifiedName~CfaPattern|FullyQualifiedName~FitsReader"`
- The golden diffs read whole, every changed line tied to one of the three classes above.
- `/validate`: the drift table.
- The console on the 11-08 and 11-06 masters: the spot's `y` mirrored, the band at the top.
- `FLATCHECK_DUMP_SCREENS` smoke render of the window on the 11-08 session, the map compared by
  eye with Siril's display.
