# 0020. --light is gated by the diagonal field of view, and an unknown field runs with a note

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-light.md; the decision itself dates
from the guard added after the 2026-08-12-Smida session (the note gives no other date). The
mode fits `bg = a + b·X + c·Y + d·R² + e·R⁴` on tile backgrounds and reads the radial part as
flat error. That split holds only while the light-pollution dome is quasi-linear across the
frame: over a wide field the dome curves inside the frame and the radially symmetric part of
that curvature has exactly the shape of vignetting. Measured on `Milkyway-Core/2026-08-12-Smida`
(Tokina 20 mm f/2.8 on a D3300, ~70 degrees diagonal): one frame reported `+3.13 % SUSPECT,
under-correction`; across the 32 calibrated subs the figure swept from +3.13 % through -19.20 %
to +35.73 % in 81 minutes, 1.36 points between consecutive frames, with a correlation of -0.94
(R² = 0.88) against the sky gradient. Eighty-eight percent of what the tool called "flat" was
sky. This is a geometric degeneracy, not an implementation limit.

## Decision

`LightSuitability.Check` derives the diagonal field of view in layers, most trusted first:
`--fov <deg>` from the user; WCS `CDELT1` / `CD1_1` from a plate solve; `XPIXSZ` + `FOCALLEN`
(+ binning) for astro cameras; `FOCALLEN` alone for DSLRs (Siril writes no `XPIXSZ` for them);
nothing, which runs anyway with a note. Below `Th.LightFovOk` (20 degrees) it is `Ok`; up to
`Th.LightFovMax` (40 degrees) `Marginal`, which runs with a warning; above that `TooWide`,
refused with exit 1 and no figure produced. The focal-only fallback prints no field figure and
grades the focal length against thresholds computed for a full-frame sensor (43.27 mm
diagonal), which is conservative on a smaller sensor. `--fov` states the truth and is not a
force flag: a wide field declared by hand is refused like a deduced one.

## Alternatives rejected

- **A better fit.** Two things with the same shape cannot be separated by a fit, however well
  written; the note says explicitly not to try.
- **Refusing on a missing header.** Every synthetic fixture in the test suite and every minimal
  FITS would become unanalysable, and the guard would do more harm than the problem it fixes.
- **Inventing a field figure from the focal length alone.** The field depends on the sensor,
  which is unknown at that point; printing a number would be inventing one.

## Consequences

- Unknown is not a refusal; pinned by `AnEmptyHeaderRunsInsteadOfRefusing`.
- The focal-only path is graded on the focal length, not on a fabricated field; pinned by
  `TheFocalOnlyFallbackIsGradedOnTheFocalLength`. A good frame on a small sensor can come out
  `Marginal`, and `--fov` exists for that.
- The thresholds `Th.LightFovOk/Max` and `Th.LightFocalOk/Min` (120 mm ok, below 60 mm
  refused) live in `Th` and nowhere else.
- The residual scale's `ACCEPTABIL` advice ("run `--light` to confirm") applies only when the
  field is narrow enough for the mode; the wording elsewhere must keep saying so.
- Source note: docs/design/modes-light.md ("The validity limit").
