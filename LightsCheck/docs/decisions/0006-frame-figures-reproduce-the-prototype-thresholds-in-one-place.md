# 0006. The frame figures reproduce the Python prototype step for step; every constant lives in Thresholds.cs

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The analysis the tool automates was done once by hand, with Python scripts (rawpy, numpy,
scipy) on the 87 Veil lights, and its report was discussed with the owner and accepted: the
frames it called trailed were checked by eye, the dithering tables were argued over, the
thresholds (elongation 1.35 and 1.6) were chosen on its figures. Those scripts are frozen in
`prototype/` (ADR 0003). The C# analyzer could either port them step for step or take the
occasion to pick better algorithms (a PSF fit instead of moments, a star finder with
deblending, a Gaussian background instead of tile medians).

## Decision

The frame analyzer reproduces the prototype's method step for step, with the same windows,
kernels and sampling (`docs/design/frame-analysis.md` lists them), so that its figures on the
Veil frames equal the prototype's within the validation tolerances, and the report the owner
read stays true of the tool. Every constant the method uses is a named field of `Th`
(`Thresholds.cs`), never a literal in code or text; the requirement rows quote it by name.
Where the prototype was wrong (the saturated-star count read on the background-subtracted
raster) the C# is right and the design note says so. A better algorithm is a later decision,
taken after a measurement on the sessions, not a port-time improvement.

## Alternatives rejected

- **A PSF fit (Gaussian or Moffat) per star.** More exact FWHM on a trailed star, but 400 fits
  per frame and a new set of figures to re-validate the thresholds against; moments give the
  same verdicts on the Veil frames.
- **Tuning the constants while porting.** Any change would break the equality with the
  figures the owner judged; a change goes through `/validate` with the drift table.
- **Constants beside their use.** FlatCheck's rule "Thresholds are empirical rules of thumb in
  `Th`" has kept its thresholds findable; the same here.

## Consequences

V-002 to V-007 guard the equality on six frames of the Veil session (counts to 1 %, FWHM to
0.05 px, elongation to 0.05). A formula change re-runs them (CLAUDE.md, the rule "Measured on
the sessions"). The window and the console print the same figures, from the same `Th`.
