# 0092. A difference that rounds to zero reads +0.00 in every report but --light's

Date: 2026-11-06
Status: accepted 2026-11-06

## Context

On 2026-11-02 a structure run whose master was the model of a lensfun profile itself printed
`-+0.00 stopuri` on the Samyang row and in the comparison summary. The cause is .NET's format
with sections, `{x:+0.00;-0.00}`: a negative that rounds to zero at the decimals shown is
formatted with the positive section, and the minus is still put in front. The project had met
it twice: `StructureReport.LocalSpotSize` rounds to zero before formatting and shows `+0.00 %`
with no word after it (ADR 0041), and `LightReport.Signed` writes the sign from the value's
own, `-0.00` for a negative zero, because the Python prototype wrote `%+.2f` that way and the
`parity_light_*` goldens hold it (ADR 0047, R-098). Eight other places used the section format:
the profile rows, the comparison summary and the depth verdict of the structure report, the
Split tab's difference cells, the ends of the map's scale in the legend and in the mosaic's
title, and the two screen-to-raw gaps of `--expo`. No golden held `-+0`: the case needs a
difference under half the last decimal, which the fixtures never produce.

## Decision

A signed difference that rounds to zero at the decimals shown reads `+0.00` (`+0.0`, `+0`),
whichever its sign was: the rule of `LocalSpotSize` becomes the rule of every report, through
one helper, `Numbers.Signed(value, decimals)`, which formats the digits first and decides the
sign on them, so the two cannot disagree; `Numbers.RoundsToZero` gives `LocalSpotSize` the same
zero for the word beside the digits. `LightReport.Signed` is the one exception and keeps
`-0.00`: its sign is the diagnosis of `--light` and its figures are the prototype's, so it
changes only with the parity goldens, at the owner's word. The owner chose it on 2026-11-06, in
the brief of the task: the local spot's rule is the rule wanted everywhere, and the light
mode's format is not to be changed. R-129 states it, `SignedZeroTests` reproduces the
`-+0.00` on the old code, one test per report, and `NumbersTests` pins the helper.

## Alternatives rejected

- **`-0.00` everywhere, as `--light` writes it.** It keeps the sign of a value the reader
  cannot see, and would move the local spot's `+0.00 %` of ADR 0041 and its word; the light
  mode's `-0.00` is a parity debt, not a wording the owner wants.
- **A fix in each place, keeping the section format with a guard.** Eight copies of the same
  guard, the next format with sections would miss it again, and the word of the local spot
  would be decided apart from its digits.
- **Rounding the value with `Math.Round` before the section format.** `Math.Round` and the
  formatter do not round the same binary value the same way at every midpoint; a golden line
  could move on a value that does not round to zero. Deciding the sign on the formatted digits
  keeps every other output byte-identical, which `/validate` and the goldens confirmed.
