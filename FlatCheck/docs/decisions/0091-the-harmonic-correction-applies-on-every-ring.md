# 0091. The harmonic correction of the radial model applies on every ring, the whole rings included

Date: 2026-11-06
Status: accepted 2026-11-06
Supersedes: 0017 in part (the plain median on the whole rings)

## Context

ADR 0017 divides the even 2θ + 4θ terms out of the partial rings before their median and leaves
the whole rings the plain pixel median, on the reading that a whole ring needs no correction:
the median of a sinusoid over a full turn is its mean. On 2026-11-06 the owner asked why the
structure map of the Rosette master (`E:\Photografie\Astro\online\RosetteNebula`, Canon 40D,
17 flats, GOOD 3.76 %) draws a hard circle on the inscribed radius, bright left and right of
it, dark above and below, the inside almost flat, when the ring fit of 2026-09 had cured the
same kind of circle on the Tokina masters.

Measured that day with a Python replica of `Metrics.RadialModel` on the 2x2-binned master:

- The ring fit is right: the relative 2θ amplitude per r² is constant, 0.034-0.042e-6, from
  0.15 to 1.36 of the inscribed radius (nearer the centre the term is too small to measure).
  The model's form, one `q` per r², holds.
- The master's azimuthal profile mixes 2θ and 4θ: at the inscribed radius a2 = +1.43 % and
  a4 = -0.60 % of the level, a broad hump on the sides and two narrow notches at the top and
  bottom, not a sinusoid. The median of `1.43 cos 2θ - 0.60 cos 4θ` over a full turn is +0.60 %
  above its mean (numerically, 200 000 samples; a pure 2θ or a pure 4θ gives 0.000).
- On the whole rings the plain median therefore sat 0.2 to 0.6 % above the level, growing with
  r (fit a0 0.13714 against median 0.13775 on the last whole ring); on the first partial ring
  the corrected median is the level (0.13684). The profile fell 0.66 % there against 0.17 and
  0.22 % on its neighbours. That step is the circle.
- The Tokina sky master is a pure 2θ (a4 ≈ 0) and the rotated paper masters a pure 4θ, which
  is why neither showed it: on one harmonic the median is the mean.

## Decision

The profile is the median of `v / (1 + r² Σ q·harmonic)` on every ring once `q` exists, whole
and partial; `q` is estimated as before (ADR 0017: one median over the identifiable rings from
0.75 of the inscribed radius outward). Near the centre `q r²` is negligible, so the inner
profile is the plain median within noise. Without an identifiable ring there is no correction
and every ring takes the plain median, as before. The owner chose it on 2026-11-06 after the
measurement ("Da, fă-o aici": yes, do it here); R-014 states it and `R014_RadialModel_OnASaddleWithAFourfoldTerm_RecoversTheRadialPartOnTheFullRingsToo`
pins it.

## Alternatives rejected

- **The azimuthal mean (the fit's a0) instead of the median on every ring.** Design A of
  2026-09-17 (ADR 0017, the note's section 3): a compact blotch in a corner, symmetrised into
  two antipodal halves, is a 2θ signal on its rings, and the fit absorbs it with a0 moved by
  2.3 %, a false ring on the map. The median after the correction keeps the robustness.
- **The plain median everywhere.** The state before 2026-09-17: the lobes with arcs on the
  Tokina sky master (the note's section 1).
- **The correction from the first partial ring inward only to some radius.** A second boundary
  with a smaller step on it; the correction is smooth in r and costs nothing on the inner
  rings, so there is no reason to stop it anywhere.
- **Leave it: a 0.6 % step under a 2 % map scale.** The owner's question was about the map's
  circle, which is the model's and not the flat's; the tool must not draw what is not there.

## Consequences

- Measured on synthetics at the analysis size (2400x1600 prepared to 1200x800, the Adobe-like
  radial fixture times the even mix), worst model / truth error: the Rosette amplitudes
  (+4.6 / -1.9 % at the corner, i.e. +1.4 / -0.6 % at the inscribed radius) 0.513 -> 0.039 %;
  the pure 11 % saddle and the pure 3 % four-fold fixtures unchanged (their tests stay). End to
  end on the same scene: ring step 0.463 -> 0.232 %, residual 3.60 -> 3.31 %, worst tile 2.90
  both ways.
- On the Rosette master (the console of this branch): residual 3.76 -> 3.65 %, the other four
  tests and the verdict unchanged (1.26 / 3.00 / 0.90 / 3.72, GOOD); the map has no circle.
- On five library masters the replica predicted moves under 0.1 points. Measured on
  the 44 validation cases (`docs/validation.md`, History, 2026-11-06): 38 unchanged within
  tolerance, the residual down by 0.04 to 0.08 on V-008, V-010, V-015, V-036 and V-037, and two
  figures beyond the prediction: V-015's worst tile 3.93 -> 4.07 (the same panel blotch at 0.8
  of the inscribed radius, read 0.1 deeper now that its whole ring carries the correction, the
  effect ADR 0017 already accepted on the partial rings of the monitor master) and V-001's
  local spot -0.38 -> +0.41: the dark spot was the printed one before, so the bright spot by
  the top edge was under 0.384 then and reads +0.406 now; in the replica the dark one is
  unchanged (-0.396) and the bright one grew from +0.389 to +0.407, a tie decided by 0.02. No
  verdict changed.
- Golden files: six console snapshots move by 0.01 on one line each (the residual of the clean
  fixture 0.29 -> 0.28 in `structure_clean`, `en_structure_clean`, `against_radial`,
  `en_against_radial`; 52.02 -> 52.01 in `structure_reshoot`, `en_structure_reshoot`): on a
  fixture with no even term the estimated `q` is the fit's noise, now applied on the whole
  rings too. Four parity goldens move the same way (`structure clean` and `structure int32`
  0.29 -> 0.28, `structure heavy vign` 0.54 -> 0.53, `structure band REFA` 52.02 -> 52.01), one
  line each, recaptured only with the owner's yes and recorded in
  `docs/design/python-parity.md` (the rule "Golden files").
- Guards: the R-014 test above, `OnASaddleWithAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure`
  in `StructureAnalyzerTests`, the four radial-model tests of ADR 0017 unchanged.
