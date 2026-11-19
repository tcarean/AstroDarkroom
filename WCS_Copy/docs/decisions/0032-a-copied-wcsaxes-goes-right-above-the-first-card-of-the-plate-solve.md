# 0032. A copied `WCSAXES` goes right above the first card of the plate solve

Date: 2026-11-19
Status: accepted 2026-11-19

## Context

A copy sets each selected key with `FitsHeader.SetValueCards`: in the place of the destination's
card of that keyword, or appended after the destination's last card. The FITS standard (4.0,
section 8.2) says `WCSAXES`, if present, "must precede all WCS keywords except NAXIS in the HDU",
and its Table 22, of the reserved WCS keywords, lists beside the plate solve's own the date cards
(`DATE`, `DATE-OBS`, `MJD-OBS` and the like), `PVi_m`, `WCSNAME` and the alternate descriptions. CR-019 of the
review of 2026-11-16 showed the order the two rules give: a source holding `WCSAXES`
(astrometry.net writes it; Siril does not) copied into a destination whose own `CTYPE1` and the
rest the copy replaces where they stand puts `CTYPE1` at index 6 and `WCSAXES` at index 14 of 23
cards (the review's run). The prototype wrote the same order. The entry asked whether a reader
minds.

Measured on 2026-11-19. astropy 8.0.1 (WCSLIB) reads `WCSAXES` wherever it stands: `NAXIS = 2`
with `WCSAXES = 3` gives a WCS of 3 axes with the card before the other WCS cards and after them,
`NAXIS = 3` with `WCSAXES = 4` gives 4 in both places, the coordinates are the same, and
`verify('exception')` passes both. Siril was not measured. A read-only scan of the primary
headers of the owner's 5,206 FITS files under `E:\Photografie` found none holding `WCSAXES`.

## Decision

After the cards are set, when the copy set `WCSAXES`, it is moved right above the first card of
the target whose keyword is a plate-solve key, a WCS key of R-007 or a SIP coefficient
(`WcsMetadataKeys.IsPlateSolveKey`), when such a card stands above it; a destination's own
`WCSAXES` the copy replaced where it stood goes there too. Otherwise no card moves: a destination
with no plate solve gets the copied keys appended in R-007's order, `WCSAXES` first among them
(R-061). The mechanics are `FitsHeader.MoveAbove`, which moves a keyword's value cards with their
`CONTINUE` cards. No log line is added: the Trace of the cards written shows the order.

The move keeps `WCSAXES` above the plate solve the copy writes, not above every WCS keyword of
Table 22: it looks at the WCS keys of R-007 and the SIP coefficients alone. Of the standard's
other WCS keywords, `DATE` and `MJD-OBS` are no keys of the lists, and `DATE-OBS` is an optical
key a copy sets where the destination's card stands; none of them moves. A Siril header holds
`DATE` and `DATE-OBS` at cards 15 and 16 and `CTYPE1` at 36, so the copied `WCSAXES` stands at
card 36, under the two dates; the SyQon CLI writes its header in alphabetical order and gets it
at card 11, above `AP_0_0`, its first SIP coefficient. The review of the branch found this on
2026-11-19, the plan having said the move followed the standard; the owner chose that day to
keep the code and say what it does, the recommended option, since no file of the owner's holds
`WCSAXES` and astropy reads either order the same.

## Alternatives rejected

- **Leave the order and reject the entry.** No reader measured minds, but the standard says
  "must", the tool promises a standard header, and the fix is a few lines.
- **Put `WCSAXES` right after the image size (`NAXISn`, and `EXTEND`, which follows them in
  these headers, though the standard no longer asks for it).** It meets the
  standard's letter in every header, the date cards included, but takes the card far from the
  plate solve it belongs to, above cards that are no WCS (`BZERO`, `COMMENT`), and changes every
  copy's header for a case no file of the owner's holds; offered to the owner again at the
  review of the branch, and kept out.
- **Rebuild the plate solve as one block in R-007's order.** It would reorder the destination's
  cards beyond what the copy needs, and the WCS standard asks no order of the other keys.

## Consequences

The move looks at no card outside the plate solve of the lists: not at the keys outside both
lists (`PVi_m`, an alternate description such as `CTYPE1A`, `DATE`, `MJD-OBS`), which R-016
leaves alone, nor at the optical keys, `DATE-OBS` among them; a valid header holds its mandatory
cards first, so `WCSAXES` never goes above `NAXISn`. Guarded by the `R061_` tests: in
`WcsCopyTests`, `R061_Run_ACopiedWcsaxes_GoesRightAboveTheDestinationsPlateSolve`,
`R061_Run_ADestinationsOwnWcsaxesBelowItsSolve_IsMovedAboveIt` and
`R061_Run_IntoADestinationWithoutAPlateSolve_TheKeysFollowItsCardsInTheListsOrder`, with the two
`R061_MoveAbove_` tests of `FitsHeaderTests`; added by the review of the branch,
`WcsCopyTests.R061_Run_ACopyThatSetsNoWcsaxes_LeavesTheDestinationsOwnWhereItStands` and
`ValidationTests.R061_ACopiedWcsaxesGoesAboveTheFirstSipCoefficientOfTheRealSyQonHeader`, which
fail under a move above `CTYPE1` alone, one above the WCS keys without the SIP coefficients, and
a move whatever the source held; added by its second review,
`ValidationTests.R061_ACopiedWcsaxesGoesRightAboveCtype1OfTheRealSirilHeader_UnderItsDateCards`,
`WcsCopyTests.R061_Run_ADestinationWhoseSolveBeginsWithCdelt1_GetsWcsaxesRightAboveIt` and
`R061_Run_ASourceWithWcsaxesButNoCtype1_StillPutsItAboveTheDestinationsSolve`, which fail under a
move only when keys were dropped, one above the date cards too, one above `CTYPE1` or a SIP
coefficient alone, and one asked by the source's `CTYPE1`.
