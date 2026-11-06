# 0011. A copy drops the destination's old plate-solve cards that the source lacks, so the target holds one plate solve

Date: 2026-11-06
Status: accepted 2026-11-06

## Context

`WcsCopy.Run` replaced or appended each selected card (`FitsHeader.SetCard`) and never removed
one, so a destination that held a plate solve of its own kept the cards of it that the source
lacks beside the copied ones. Writing the user manual for the GitHub wiki on 2026-11-06 raised
the question, and two measurements made it matter. **Siril 1.4.4 reads a `CD` matrix over `PC`
and `CDELT`**: a 200 x 100 test file holding PC = identity with CDELT 0.001 deg and CD = 0.002
deg turned 30 deg, same CRPIX and CRVAL, came out of `siril-cli` 1.4.4 `load` and `save` (with
`-i` on an empty private settings file) as CDELT 0.002 and a PC turned 30 deg, the CD cards gone;
astropy 8.0.1 (WCSLIB) gave the same file the PC's coordinates, with no warning. So a CD left in
a destination makes Siril, the program a copy is checked in, show the old solution while another
program shows the copied one. **In the owner's data**, a read-only scan of the 3,884 FITS files
under `E:\Photografie` outside `Code` found 231 plate-solved: 222 with `PC` and `CDELT` only, 5
with a `CD` matrix only (Siril 0.99.8.1), and 4 with both, the outputs of the SyQon CLI
(`*_syqon_studio_in.fit`), whose CD describes the PC's solution mirrored top to bottom; no
`CROTA` and no SIP term above `A_ORDER`. The owner was given three options with a
recommendation and chose this one on 2026-11-06.

## Decision

When the source has `CTYPE1`, a copy first removes from the destination every card whose keyword
is one of the 30 WCS keys or a SIP coefficient and that the source lacks (R-016), so that the
target holds the source's plate solve alone; it never drops an optical key, drops nothing when
the source has no `CTYPE1` or the run is refused, and names the keys in one line of the preview
and of the copy, and in the log.

## Alternatives rejected

- **Keep the cards and document them.** Siril reads the old `CD` lines, so a copy checked in
  Siril shows the old solution, right or wrong, for a reason the user cannot see.
- **Warn only.** The user has no tool at hand to remove a card: saving in Siril keeps the
  `CD`'s solution and drops the copied one.
- **Drop only the form that conflicts** (a `CD` matrix against `PC`, `CROTA` against either).
  It needs a rule per form and still leaves the old `RADESYS`, `LATPOLE` or SIP terms the source
  lacks; dropping every plate-solve key the source lacks gives that target with one rule.

## Consequences

- The target's plate solve is exactly the source's: a destination's own solve made by another
  program is replaced whole, which is what a copy promises. The optical keys are untouched
  beyond the copied ones, so a destination's own site or object line stays.
- A copy now changes the destination beyond the copied cards; the preview, the copy's lines and
  the log (one `drop` line per card, `dropped` in the done line, always) say which keys.
- Guarded by the `R016_` tests of `WcsMetadataKeysTests`, `FitsHeaderTests`, `WcsCopyTests`,
  `CopyWcsCliTests`, `MainViewModelTests` and `ValidationTests`, the two `drop` scenarios of
  `LanguageTests`, `R042_ADroppedCardIsLoggedWithTheDestinationsCard_InACopyAndInADryRun`, and
  V-002: V-001's solve copied into the header of the SyQon output of the same Veil stack (a new
  fixture) copies 72 keys and drops `CD1_1`, `CD1_2`, `CD2_1`, `CD2_2`.
- Measured end to end on 2026-11-06: the console copied `result-bge-cc.fit`'s solve into
  `denoised_syqon_studio_in.fit` of the same folder with `-o` into a scratch folder, both
  originals unchanged by SHA-256; it dropped the 4 CD keys, and Siril 1.4.4's `load` and `save`
  of the result wrote the copied `CDELT` and `PC` unchanged.
