# 0048. A stack beside FITS lights is told by its header

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

ADR 0004 keeps the stacked results out of the lights by reading `lights/` when it exists. A
folder without `lights/` is itself the lights folder, and the README allows it. The review of
2026-11-13 (CR-003) found that with FITS lights there, Siril's `result.fit` beside them is
counted as a light: 50 lights and a stack read 51 lights, and the stack's exposure, time, stars
and sky enter the total exposure, the pauses and the series. With raws the case does not arise,
since a folder's raws win over its FITS files. Measured on 2026-11-13 on
`Veil/2026-11-06/result_9873s.fit`, written by Siril 1.4.4: the header carries `STACKCNT = 82`
and `LIVETIME`, with `NAXIS3 = 3` and `BITPIX = -32`. The owner chose the header over the name
the same day.

## Decision

Without `lights/`, a FITS of the folder whose header carries `STACKCNT` or `NCOMBINE` above 1 is
not a light. `STACKCNT` is what Siril writes; `NCOMBINE` is the keyword other stacking programs
use. A FITS whose header cannot be read stays among the lights, so its row says the error as
before. Each file left out is named in the diagnostic log; the report does not mention it.

## Alternatives rejected

- **By the name (`result*.fit`, `*_stacked*.fit`).** No file is opened, but a renamed stack
  passes for a light, and a light the user named `result_...` would be dropped.
- **Both the header and the name.** The name adds only the second failure above.
- **Three colour planes or a float `BITPIX` as the sign.** A calibrated or debayered light has
  them too.

## Consequences

`SessionFolder.Open` reads one header per FITS, only when the folder itself holds the lights
and they are FITS files; a session with `lights/` or with raws opens as before. A stack written
by a program that sets neither keyword is still counted; it would be a new entry of the review
list. The calibrated and registered copies Siril writes beside the lights (`pp_...`, `r_pp_...`)
are frames, not stacks, and are still listed; ADR 0004's layout, with `lights/`, is the way
around them. `SeriesRun.ReadWcs` lists the root's FITS files on its own and still finds the
stack for the plate solve. Guarded by the three `R002_` tests of `SessionFolderTests` on a stack,
an unreadable file and a folder with a stack alone.
