# 0029. The nearest profile is used without interpolation, and the camera's crop sets the frame's physical size

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-synthetic.md and
docs/design/lens-profiles.md; the decision itself dates from 2026-09-17 (nearest profile) and
2026-09-18 (the crop, the calibration crop in the lens key, the collapsed distance pair). Adobe's
model puts r in units of the focal length with the frame's long side at `36 / SensorFormatFactor`
mm; the first cut took the profile's SFF - the calibration body - for every camera: 1.5 % off on
a D3300 with the DX file (1.523 vs 1.5; the Adobe synthetic read 0.69 stops instead of 0.67),
and the whole corner off on a full frame with a DX-calibrated file, silently. Adobe's own
piecewise table in the Tokina file ends at r = 0.7094, i.e. 14.19 mm at 20 mm, the half-diagonal
of a 36 / 1.523 = 23.64 mm sensor, not of the 24 mm one SFF 1.5 implies: the real crop is what Adobe used.

## Decision

Nearest profile, reported, no interpolation: nearest focal, then nearest f-stop in stops, then
the farthest distance (flats are at infinity); the report prints "cerut" beside "profil folosit".
A request far from every profile is warned about, not refused (`SyntheticReport.ProfileMismatch`:
focal more than `Th.ProfileFocalTolMm` / `Frac` = 1 mm or 5 % away, aperture more than
`Th.ProfileStopsTol` = 0.5 stops away; half a stop because Adobe's grid is at whole stops). The
camera's crop sets the frame's physical size for both models (`36 / crop` for the frame,
`36 / SFF` for the profile's focal-in-pixels and decentring); the SFF is the fallback when the
crop is unknown and the report says so; a crop more than 3 % from the SFF prints "profil calibrat
pe alt format"; `Extrapolation` above 1.1 prints "extrapolare MARE". `SyntheticRun.PreferCamera`
takes a per-body Adobe file over the mount's generic one. Two owner decisions of 2026-09-18: a
lensfun lens's identity includes its calibration crop (`LensChoice.Calibration`), and a lensfun
pair at 10 and 1000 m with identical coefficients is one profile, printed "distanta nespecificata".

## Alternatives rejected

- **Interpolating between profiles.** Not built; "nearest" may sit a focal length off and the
  report says so (if ever wanted: in stops for the aperture, linearly in focal, both neighbours reported).
- **Refusing a request far from every profile.** 500 mm f/1.0 on the Nikkor 35 f/1.8 silently
  became 35 mm f/1.8 with "cerut" as the only sign; a warning fixes that without moving exit
  codes (f/4.7 → f/4.0 at 0.46 stops does not warn).
- **The profile's SFF as the frame size.** The 1.5 % and whole-corner errors above.
- **Lens key without the calibration crop.** The database holds 20 lenses twice; the two collapsed
  and `Nearest` took the first in file order - on the Nikkor 35/1.8G DX the poor one (0.19 stops
  at the DX corner at f/2.8 against 0.51 from the hidden D780 entry and 0.61 from Adobe).
- **Taking the 1000 m copy as "infinit".** A copied value presented as a fact: that Nikkor
  entry's floor of 0.16-0.17 stops matches Adobe's 0.36 m profile, not the 1.51 m one.

## Consequences

- The label "calibrat pe ..." is on every lensfun lens, so a name cannot change when the database
  gains an entry; Adobe stubs carry none. A single lensfun entry at a stated distance keeps it;
  a pair with different coefficients keeps both.
- A corner past r = 1 (1.06 on the D3300 with the lensfun crop-1.61 entry) prints "extrapolare
  mica"; a DX file on a full frame is refused in words, not numbers, since a sixth-order
  polynomial 50 % past its data goes negative. `SyntheticReport.FormatMatch` is the one sentence
  for all of it, printed by the console at "format" and shown by the tab.
- Guards: `VignettingModelTests` (the piecewise cross-check), `Lensfun_TheSameLensCalibratedOnTwoFormatsIsTwoLenses`,
  `ALensfunLensCalibratedOnTwoFormatsIsTwoRows` and the two tab tests of the same name, the golden
  `synthetic_far_request`; the depths of lens-profiles.md section 9 (Adobe 0.67 / 0.51 / 0.31 /
  0.28 stops at crop 1.523) are the acceptance numbers to re-run when the model changes.
- Source notes: docs/design/modes-synthetic.md, lens-profiles.md (sections 4, 5, 8).
