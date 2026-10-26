# 0023. The WCS flip against the lights is the raws' alone: FITS lights are read as the solve is

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0013 in part (the flip applied to every session)

## Context

ADR 0013 maps a dither's star shift `(dx, dy)` to the sky through the solved FITS's WCS
applied to `(-dx, +dy)`: the y reversed because camera raws are read top-down while Siril
stores its FITS bottom-up, a fact measured on two plate-solved raws of the Veil session. Its
consequences named one case that would read reversed, a solve not written by Siril, and
missed another: `SessionFolder.FramesIn` takes a folder of FITS lights when it holds no raws,
and those are read in the file's order, the same order the solve is stored in. For such a
session the reversal was wrong, and so was `Wcs.Handedness`, which enters the polar bound of
R-050. On the owner's frames RA runs along Y, so the sense that came out reversed was east
against west.

Found on 2026-10-26 while surveying every FITS reader of the repository after Siril's
treatment of `ROWORDER` was measured (ImageVisualCompare ADR 0039, AstroPhotoAnalyzer ADR
0049); the readers of this project were right to read a FITS in file order, the flip against
the solve was the thing that assumed raws. Measured the same day: the Veil session
(`Veil/2026-10-06-3h`) run from its 87 raws, and from a folder holding hard links to Siril's
calibrated `process/pp_light_*.fit` of the same frames with the solved stack at the root; the
figures are in the Consequences.

## Decision

`Wcs` carries `FramesFlipped`: true when the lights are camera raws, false when they are FITS;
`SeriesRun` sets it from `SessionFolder.LightsAreRaws` (the two kinds are never mixed in one
folder). `Wcs.PointingOffsetDeg(dx, dy)` takes a displacement of the pointing in the lights'
own pixels and reverses y only when the flag is set; the moves of R-038, the star drift of
R-050 and `Handedness` all go through it, with `(-StepX, -StepY)`. The default of the flag is
true, the raws' case, which every hand-made `Wcs` in the tests is.

## Alternatives rejected

- **Reading FITS lights flipped, as Siril shows them**, so that every session is in one
  orientation. It would move the Bayer-phase rule of `CfaPattern.ForRowOrder` and every
  per-frame figure of a FITS session for the sake of one mapping; the flip belongs to the one
  place that compares the lights with the solve.
- **Refusing FITS lights when a solve is at the root.** The owner's `pp_light_*.fit` folders
  are a natural input, and the mapping is well defined for them.
- **Reading `ROWORDER` of the lights and of the solve and comparing.** Siril writes
  `BOTTOM-UP` on both whatever the data's origin (a SyQon output says `TOP-DOWN` over
  bottom-up data, ImageVisualCompare ADR 0039), so the keyword tells nothing the file kind
  does not; the raws have no keyword at all.

## Consequences

- The raw sessions read exactly as before: the flag is true for them and the formula is the
  same; `/validate` on the sessions shows no figure moved.
- A FITS session now gives the senses its raws would. Measured on Veil, the 87 raws against
  the 87 `pp_light_*.fit` with `result-bge-cc.fit` at the root, the `dithering` cell of every
  frame of the console's report compared (the registration of the calibrated frames agrees
  with the raws' to the pixel, `dY` mirrored as the files are):

  | run | cells identical to the raws' | differing |
  |---|---|---|
  | FITS lights, before | 52 of 87 | 35: every move with an RA component read the other way (frame 61 `RA 17 px → E` against `← W`, 6 `← W` against `→ E`, 80 `← W, ↓ S` against `→ E, ↓ S`); the DEC senses agreed |
  | FITS lights, after | 86 of 87 | 1: frame 25, `DEC 30 px` against `DEC 29 px`, a rounding of the calibrated frames' step |

  The polar bound reads 4.5' to 6.8' on the FITS copy against 4.5' to 6.7' on the raws, and
  the direction counts (`sign Y: +24 / -11` against `+11 / -24`) mirror as the frames do.
- `R038_FitsLightsAreNotFlippedAgainstTheSolve` pins the unit rule; the session tests whose
  fixture lights are FITS (`R038_TheFrameTableAndTheCsvCarryTheMoveOnTheSky`, R-040's console
  test, R-041's window test, R-050's and `LanguageTests`') carry the Veil solve with its y
  column mirrored, the solve a FITS session of those frames would have, so their expected
  words did not change; R-025's, which reads the axis alone, needed nothing.
- Seen on the way, not fixed here: in the CSV of a FITS session the channel names are
  `CFA[0,0]` and the like, written bare, so every row has four cells more than the header
  (R-028); the raws' `R`, `G1`, `G2`, `B` have no comma.
- A solve not written by Siril (rows top-down) under raw lights would still read reversed,
  as ADR 0013 said; `ROWORDER` cannot tell (above), so it stays the place to revisit if the
  workflow changes.
