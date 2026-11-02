# 0090. The Adobe profile gives the depth verdict when the lens was found by its ID or name; lensfun is shown for comparison

Date: 2026-11-02
Status: accepted 2026-11-02
Supersedes: 0031 in part (the nearest profile's verdict where an Adobe file is among lenses not found by range alone); 0054 in part (the reference profile as the whole session's nearest profile)

## Context

ADR 0031 gave the depth verdict to the nearest profile, on the reading that the two databases
differ by about 0.2 stops on the same lens and a master that matches one of them is not wrong.
On 2026-11-02 the owner asked why the Tokina tablet master (`coperti-7-foi`) was judged against
the lensfun `Tokina AF 11-20mm f/2.8 AT-X Pro DX` entry and not the Adobe file of their lens, the
atx-i CF. Measured that day: the four Adobe Tokina 11-20 files (Nikon and Canon, AT-X PRO DX and
atx-i CF) carry byte-identical vignetting data, so Adobe has one measurement of the lens; the
atx-i file's own `LensID` is the raw's 139, the AT-X file has 122 with 139 as an alternate;
lensfun's `ATX-i 11-20mm F2.8 CF` entry has no vignetting; focus distance moves each profile by
at most 0.03 stops at the corner. The two lensfun entries (D7100, 7D Mark II) agree with each
other (0.91 / 0.85 stops) and sit 0.2-0.25 stops deeper than Adobe (0.66).

The owner's own flats bound the lens on the D3300: the twilight sky can only fill the corners
(0.27 stops) and a panel only darken them (the shallowest, the paper rotated in place, 0.76), so
the lens lies between 0.27 and 0.76, Adobe inside, both lensfun entries above. The five panel
flats read +0.10 to +0.19 stops against Adobe but -0.08 to +0.00 against the 7D2 entry and -0.15
to -0.06 against the D7100 one, a sign no panel can give. On the Nikkor 35 DX the sky master
(0.63) is a lower bound and Adobe (0.52, a 1.51 m profile only) is the nearest; one lensfun entry
reads 0.16. The nearest rule thus took the deepest profile for the panel flats and let the
panel's own falloff pass as the lens's.

## Decision

When the lenses are the candidates the raw's lens ID or name found and at least one Adobe file
is among them, the depth verdict is an Adobe row's (R-128): the files whose own `LensID` is the raw's
first, the nearest of them, and the nearest Adobe row when no file carries the raw's ID as its
own. The lensfun rows stay in the list for comparison. By range alone (a manual lens, ADR 0030),
with a lens the person chose (ADR 0081), with a `--lens` text, or with no Adobe row, the nearest
row decides as before. The Split tab's
reference profile is the row that gives the whole's verdict (R-093), no longer always its nearest
(ADR 0054). The owner chose the rule on 2026-11-02 after the measurement below ("Da, Adobe
decide").

## Alternatives rejected

- **Keep the nearest rule.** On the Tokina it calls the panel flats PERFECT or BUN against a
  lensfun entry the sign test rules out.
- **Keep the nearest rule and add a line when Adobe is stricter.** The verdict and the exit code
  stay the lenient ones; the reader has to weigh the line against them.
- **Adobe on every path, by range too.** On the Samyang the Adobe row is another lens, the Nikkor
  135 f/2 DC; there the R-121 picker lets the person name their lens.
- **A `--lens` text counted as the lens's name** (the plan's first reading, taken back by the
  owner on 2026-11-02 after the second review). A text is a search: `--lens 135mm` beside the
  Samyang's range raw lists the Adobe Nikkor 135 f/2 DC and the lensfun Samyang, and the
  Nikkor, another lens, would have decided, the very case the range path keeps the nearest for.
- **The two Adobe names told apart by catalog order.** The AT-X file sorts first and would mark
  the PRO DX name the owner's lens does not carry, though the figures are the same.

## Consequences

- Measured 2026-11-02 on V-016 to V-030 from main 472bfb7b, reading every row: 3 of 15 verdicts
  change, each one step stricter. V-023 (monitor, diffuser close) goes from PERFECT on depth and
  BUN overall to ACCEPTABIL (+0.19 stops, 13.9 %); V-029 and V-030 (paper, 3 and 4 sheets) from BUN
  to ACCEPTABIL (+0.15 = 10.8 %, +0.14 = 10.1 %, at the 10 % edge). V-024, V-026, V-027 and V-028
  keep their verdicts with the Adobe row deciding; the Nikkor 35, the Tokina sky and the five
  Samyang cases are untouched.
- The evidence is two lenses on one body. For other lenses the Adobe file is taken as the more
  uniform source, not as measured truth; a lens whose Adobe file is wrong is judged against it.
- Guards: the R-128 tests (`AdobeVerdictTests`), the R-016 golden `structure_with_profiles`, the
  R-093 split tests and `validation.json` V-023 to V-030.
