# 0008. The input is a finished, display-referred image: linear data is refused, nothing is exported or colour-managed

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §1 ("What it is not") and §3
("Refusal of linear data"), the old `CLAUDE.md` ("The image is display-referred and final") and
`docs/implementation-plan.md` ("Decisions taken before the first line": input state, exports);
the decision itself dates from before the first line, 2026-09-20. The owner stacks, stretches and
exports in Siril; the question the tool answers is how to send *that* file to print. A stacked
linear frame has a median of 0.001 to 0.01, a finished image 0.08 to 0.25: measured as if it were
final, a linear frame would read almost entirely below paper black and every verdict would be
wrong.

## Decision

The tool measures a finished, stretched image, and does four things on purpose not:

- **It exports nothing.** No print file, no preview file; recommendations only. Exports are made
  cleanly from Siril.
- **It does not stretch.** A linear image, whose reference plane has a median below
  `LinearMedianMax` (0.02) at stride 8, is refused with a message and exit code 1, not
  mis-measured. The print-background simulation is a gamma curve on the stretched data, labelled
  as a simulation, not as what Siril will produce.
- **It does not colour-manage.** Pixels are measured as written and assumed sRGB; a profile that
  is not sRGB is reported, never applied (ImageSharp 3.1 does not apply it on decode, ADR 0009).
- **It is not a display.** The previews are pixel crops and downsamples, for checking what the
  numbers say, not a zoomable viewer.

## Alternatives rejected

- **Simulate a stretch of linear data.** The result would depend on a stretch the owner will not
  make; the tool would judge an image that does not exist.
- **Write the print file.** The Siril export is the controlled path; a second exporter would
  repeat Siril's work with its own errors.
- **Convert through the embedded profile.** It adds a colour engine; the lab prints what the file
  holds, and naming a non-sRGB profile is enough to warn.

## Consequences

- R-015 and `Golden/linear-fits.txt` pin the refusal; the window shows it on its error card.
- The simulation's rows and the Preview's print tile say "simulation".
- Colour figures are sRGB figures; another profile is only named (`known-limitations.md`).

Source: docs/specification.md §1, §3; CLAUDE.md (2026-09-21); docs/implementation-plan.md.
