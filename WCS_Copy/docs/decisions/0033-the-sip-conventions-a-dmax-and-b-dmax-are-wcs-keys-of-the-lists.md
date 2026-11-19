# 0033. The SIP convention's `A_DMAX` and `B_DMAX` are WCS keys of the lists

Date: 2026-11-19
Status: accepted 2026-11-19
Supersedes: 0011 in part (the list of 30 WCS keys)

## Context

`WcsMetadataKeys.WcsKeys` held the SIP orders (`A_ORDER`, `B_ORDER`, `AP_ORDER`, `BP_ORDER`) and
the pattern `SipCoefficient` the coefficients, but not `A_DMAX` and `B_DMAX`, which the SIP
convention defines beside them: the largest correction the distortion makes, in pixels, an
optional value a solver may write. CR-020 of the review of 2026-11-16: a destination whose old
solve wrote them keeps them beside the source's SIP coefficients after a copy, and a source that
has them does not pass them on. R-016 and ADR 0011 leave a plate-solve card outside both lists
alone on purpose, naming `RADECSYS` and `PVi_m`, so the entry was the owner's call. A read-only
scan of the primary headers of the owner's 5,206 FITS files under `E:\Photografie` on 2026-11-19
found none holding either, among 257 with a SIP solve.

## Decision

`A_DMAX` and `B_DMAX` join `WcsKeys`, after `BP_ORDER`, so that the list holds 32 keys: a copy
takes them from the source when it has them, and drops the destination's when a plate-solved
source lacks them, as it does the orders (R-007, R-016). They count as WCS keys in the log's
`selected` line, as the orders do. The owner's answer of 2026-11-19, the recommended option.

## Alternatives rejected

- **Leave them out and reject the entry.** A destination's old `A_DMAX` would stay beside a new
  solve's coefficients and describe a distortion the file no longer holds; unlike `RADECSYS`, an
  old spelling, and `PVi_m`, another convention's terms, these two belong to the convention the
  lists already cover.
- **Widen the SIP pattern to take them.** The convention names these two alone; two names read
  better than a pattern for them.

## Consequences

R-007 and R-016 count 32 WCS keys, and ADR 0011's list of 30 is superseded in that number; the
rule of ADR 0011 stands. V-001's selection does not move: Siril writes neither key. Guarded by
`WcsMetadataKeysTests.R007_SelectKeysToCopy_TheSipLargestCorrections_AreCopied` and
`R016_SelectKeysToDrop_TheDestinationsOldSipLargestCorrections_AreDropped`.
