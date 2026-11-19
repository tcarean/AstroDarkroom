# 0032. A copied `WCSAXES` goes right above the first card of the plate solve

Date: 2026-11-19
Status: accepted 2026-11-19

## Context

A copy sets each selected key with `FitsHeader.SetValueCards`: in the place of the destination's
card of that keyword, or appended after the destination's last card. The FITS standard (4.0,
section 8.2) says `WCSAXES`, if present, must precede all the other WCS keywords. CR-019 of the
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

## Alternatives rejected

- **Leave the order and reject the entry.** No reader measured minds, but the standard says
  "must", the tool promises a standard header, and the fix is a few lines.
- **Put `WCSAXES` right after the mandatory cards (`NAXISn`).** Also valid, but it moves the card
  above cards that are no WCS, further than the rule needs.
- **Rebuild the plate solve as one block in R-007's order.** It would reorder the destination's
  cards beyond what the copy needs, and the WCS standard asks no order of the other keys.

## Consequences

The keys outside both lists (`PVi_m`, an alternate description such as `CTYPE1A`) are not looked
at, as R-016 leaves them alone; a valid header holds its mandatory cards first, so `WCSAXES`
never goes above `NAXISn`. Guarded by `WcsCopyTests.R061_Run_ACopiedWcsaxes_GoesRightAboveTheDestinationsPlateSolve`,
`R061_Run_ADestinationsOwnWcsaxesBelowItsSolve_IsMovedAboveIt`,
`R061_Run_IntoADestinationWithoutAPlateSolve_TheKeysFollowItsCardsInTheListsOrder` and the two
`R061_MoveAbove_` tests of `FitsHeaderTests`.
