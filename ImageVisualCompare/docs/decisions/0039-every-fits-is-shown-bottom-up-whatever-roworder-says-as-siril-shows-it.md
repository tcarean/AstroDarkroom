# 0039. Every FITS is shown with its first row at the bottom, whatever `ROWORDER` says, as Siril shows it

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0029 in part (the row order: bottom-up unless the file says `TOP-DOWN`)

## Context

ADR 0029 obeyed `ROWORDER`: a file saying `TOP-DOWN` had its first row at the top, on the
premise that this was what Siril shows. On 2026-10-26 the owner compared
`starless_gradremoved_syqon_studio_in_cc_stretch0.15.fit` (5837 × 3867, three planes of 32-bit
float, written by Siril 1.4.4 after a SyQon step and a VeraLux stretch, `ROWORDER =
'TOP-DOWN'`) against its seven siblings in the same folder (the same processing without the
SyQon step, all `ROWORDER = 'BOTTOM-UP'`) and saw it upside down against Siril.

Measured the same day:

- The file's rows, in file order, are the siblings' rows in file order: the red plane of file
  row 200, 1000, 2800 and 3600 correlates at 0.951, 0.962, 0.925 and 0.867 with the same row of
  `starless_result_3813s_bgeAI_cc_starlessStretch1.5.fit`, and at 0.030, -0.004, 0.379 and
  0.225 with the mirrored row. The data is bottom-up; the keyword is wrong about it.
- Siril 1.4.4 (`siril-cli`, `load` then `savepng`, on both files) writes PNG row r from FITS row
  H - 1 - r, correlation 1.000 at rows 200, 1000, 2800 and 3600, for the `TOP-DOWN` file as for
  the `BOTTOM-UP` one. Siril puts the first row of the file at the bottom whatever the keyword
  says.
- Siril's documentation of the keyword says the same: Siril always reads and displays images
  bottom-up, and a `TOP-DOWN` value only corrects the Bayer pattern at demosaicing. Its
  `readfits` has no flip on the keyword.

The new fact contradicted the owner's answer of 2026-10-05, so it went back to the owner with
three options; the owner chose this one.

## Decision

`FitsImageLayout.DisplayRow` is always `Height - 1 - fileRow`, and `FitsHeader.ReadLayout` does
not read `ROWORDER`. The record lost `BottomUp`. The orientation of a FITS is Siril's, which is
the display the tool is compared against; the keyword is left to the programs that debayer.

## Alternatives rejected

- **Keep obeying `ROWORDER`** and treat the file as the bug. Every output of that SyQon step
  would stay upside down against Siril and against its own sibling on the other side of the
  divider, which is the one comparison the tool exists for; and no file at hand is both
  `TOP-DOWN` and stored top-down.
- **A flip control.** A feature with its own requirements, for a case that following Siril's
  convention removes.

## Consequences

- A genuine top-down file, as camera drivers write them (SharpCap writes `TOP-DOWN`), shows
  upside down: exactly as Siril shows it, so the two sides of the divider still agree with
  the reference display. AstroPhotoAnalyzer's reader, top-down unless `BOTTOM-UP`, now differs
  from this one by design.
- Guards: `R017_TheFirstRowOfTheFile_IsTheBottomRowWhateverRowOrderSays` in `FitsHeaderTests`
  and in `FitsImageLoaderTests`, the three values of the keyword each; R015 in
  `FitsImageLoaderTests` writes the same samples as a 16-bit PNG and as a FITS with mirrored
  rows and gets identical bytes.
