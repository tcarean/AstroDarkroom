# Flat sources: what each illumination does to the master

> Written on 2026-09-25 from the owner's session notes of 2026-09-16, 2026-09-18 and 2026-09-23,
> then cut on 2026-09-27 to what neither CLAUDE.md nor a decision record says (ADR 0051): the
> depth verdicts of these sessions are also the acceptance figures of ADR 0030, 0031 and 0032,
> the printed reading of the sign is ADR 0031, and the two tool caveats point at ADR 0017 and
> ADR 0041. **Covers:** the README's advice on how to shoot flats, the vignetting-depth
> reference used by the sixth structure test and by `--against`, and the reading of a depth
> verdict's sign. **Measured** on the sessions under `E:\Photografie\Astro\Flats and Darks` -
> `Tokina\ISO400\f2.8-sky-no-difuser` (2026-09-16),
> `Tokina\ISO400\f2.8-tableta-lipita-coperti-7-foi` and `...-15-foi` (2026-09-18),
> `Tokina\ISO400\f2.8-tableta-lipita-hartie-3-foi`, `...-4-foi` and `...-3-foi-rotiri-perfecte`
> (2026-09-23), plus the older `Tokina\ISO400\f2.8-monitor-difuzor-aproape`,
> `...\f2.8-monitor-difuzor-pe-monitor`, `Nikkor 35mm DX\f2.8-sky\ISO400`, `...\ISO800` and
> `Samyang\ISO1600\sursa_neuniforma\f2.0-lcd` (shot before 2026-09-16; dates not in the notes) -
> with flatcheck 2.1.0 to 2.2.0 and numpy on the calibrated `pp_flat_*.fit` frames. If a session
> is re-shot or the model changes, re-run the figures and update this note.

The reference depths quoted throughout, for the Tokina 11-20 at 20 mm f/2.8: Adobe 0.66-0.69
stops, lensfun 0.85-0.92 (the D7100 and 7D2 entries). The two databases disagree by ~0.2 stops
on the same lens, which is the resolution of everything below.

## 1. Twilight sky, no diffuser (Tokina, 2026-09-16)

**Setup.** D3300 + Tokina at 20 mm f/2.8, 2 s ISO 400, twilight sky near zenith, no diffuser;
frames 1-15 at 0°, 16-30 rotated 180°, dithered between shots. Siril's winsorized rejection
changed nothing (master / plain normalised mean within 0.3 %).

**What the master read.** Corner spread ~10 %; after the 2026-09-17 ring fix, residual 9.31 %,
worst tile 7.87 %, crease 0.44 %. Vignetting 0.27 stops (corner 83 %) against Adobe 0.66:
`--against` -0.39 stops, the sixth test -0.39 vs Adobe -> REFA. The two monitor masters of the
same lens at the same f/2.8 (EXIF checked) read 0.85 / 0.78 stops (corner 55-58 %).

**Angular falloff.** n = -1.4: the dome brightens away from the zenith and fills the corners.

**What the per-orientation stacks revealed.** The rotation worked on the odd part: the linear
gradient was ~22 % ptp per orientation (`--ratio` on the two 15-frame stacks: 12.85 % per
orientation) and 0.02 / -0.18 % in the mean of 30. What survived is EVEN under 180°: a diagonal
saddle (xy term, -6.3 % at the corners: TL and BR 95 %, TR and BL 105 %) plus a falloff 6x
steeper along the long axis than the short one. Per frame the saddle tracks the dithering
(correlation with the horizontal gradient +0.97 / -0.94, extrapolating to ~0 at zero
gradient), is chromatic (R -7.0, G -6.4, B -5.9 %), and the monitor masters have none of it
(-0.24, +0.04). It is the sky dome's curvature seen through a ~70° field, not the lens. On this
master R falls ~25 % deeper than B.

**Conclusion.** The sky at 20 mm is a curved, chromatic, corner-filling source: the master is
0.4 stops too shallow and carries a saddle that no 180° scheme can remove. Fine for the Nikkor
35 DX (see §5); wrong for a 70° field.

## 2. Monitor through a fabric diffuser (Tokina, before 2026-09-16)

**Setup.** D3300 + Tokina at 20 mm f/2.8, ISO 400, 30 frames, Siril float 0..1, 6016x4016
RGGB; one master with the diffuser close to the lens (`aproape`), one with it on the monitor
(`pe-monitor`, the one with fabric texture: cells ~300 px, +-3 %). Rotation scheme not in
the notes.

**What the master read.** Vignetting 0.85 / 0.78 stops. Against the Adobe synthetic
(`--against`, 2026-09-18): close +0.19, on-monitor +0.12. Sixth test (nearest profile):
close 0.00 vs the lensfun 7D2 entry -> PERFECT; on-monitor -0.07 -> BUN. Structure: the
on-monitor master's worst tile carries ~2 points of real even structure (the texture);
the `--smooth` figures for both are in `smooth-validation.md`.

**Angular falloff.** n = 0.5-0.8; the panel adds ~0.1 stops at mid-field.

**Conclusion.** The best depth of the library, within the databases' own spread. The price is
the fabric's texture, which is what `--smooth` was built for.

## 3. Tablet through frosted binder covers (Tokina, 2026-09-18)

**Setup.** D3300 + Tokina 11-20 at 20 mm f/2.8, ISO 400, 1/20 s; an old Samsung Galaxy Tab at
max brightness, 7 or 15 A4 frosted binder covers pressed between panel and lens, no hood;
4 groups of 10 frames rotated 90° between groups, 40 per master.

**What the master read.** Structure PERFECT / BUN (1.5-2.5 % on every test), yet vignetting
1.26 / 1.24 stops against Adobe 0.67-0.69 and lensfun 0.85-0.92. `--against` the Adobe
synthetic: +0.57 / +0.56 stops, shape PERFECT - the excess is purely radial. Sixth test:
+0.34 / +0.33 vs the nearest (lensfun D7100, 0.91) -> REFA, exit 3. The tool's corner/centre
ratio matches the profile bin for bin: the excess is the method, not the arithmetic.

**Angular falloff.** The excess over the model follows cos^n with n = 2.16 (7 sheets) and
2.08 (15), rms 1 % - ~33 % at the 35° field corner. 7 vs 15 sheets: level -0.28 stops (0.035
stops per sheet, i.e. nearly clear), anisotropy reduced a little, isotropic excess unchanged.

**What the per-orientation stacks revealed.** Each 10-frame orientation divided by the 4-way
mean is a clean +-5.5 % (7 sheets) / +-3.5 % (15 sheets) SADDLE (x² - y²): 0/180 alike,
90/270 inverted. The panel's angular falloff is anisotropic (long vs short axis of the tablet,
as LCD viewing angles are), rotates with the WORLD, and the 90° scheme averaged its two axes
into a radial term indistinguishable from vignetting. Frame-to-frame scatter inside a group
0.6-1.1 % p1..p99 (panel stable).

**Conclusion.** Frosted covers are not a diffuser at this level: they do not randomise the
panel's angular distribution. A PERFECT shape with 0.57 stops of excess is the documented
`--ratio` blind spot seen for real, and the reason the sixth test was built the same day.

## 4. Tablet through plain paper (Tokina, 2026-09-23)

**Setup.** D3300 + Tokina 11-20 at 20 mm f/2.8, ISO 400, 1/2 s (3 sheets) and 1 s (4 sheets);
the same tablet at max brightness, 3 or 4 sheets of plain A4 paper, lens resting on the paper,
no hood; 4 x 10 frames rotated 90°. A third session the same day, `rotiri-perfecte`, with the
lens placed in a pencil outline and rotated in place. Exposure 40-44 % centre, no clipping
("cam jos", +0.4-0.5 stops available).

**What the master read.** BUN on all five tests (3.8 / 5.1 % residual), 0.81 / 0.80 stops
against Adobe 0.66 and lensfun 0.85 / 0.91. The excess over Adobe is a clean cos^n (rms < 1 %),
+0.15 stops; against lensfun it is non-monotonic (-5 % mid-field, +4 % corner), a shape no
source produces, so Adobe's *shape* is the one the master follows. One sheet of paper costs
~1 stop of light (0.5 s at 3 sheets = 1 s at 4) and buys ~0.02 stops of corner excess.
`rotiri-perfecte`: corners 1.2 %, tilt 0.6 % (from 4.7 / 3.9), residual 3.8 %, worst tile
4.6 %, 0.76 stops.

**Angular falloff.** n = 0.55 (3 sheets) / 0.46 (4 sheets) - the monitor masters' mild falloff,
against 2.1 for the frosted covers. With a cos^0.5 source a 5 % plane would need ~4° of tilt.

**What the per-orientation stacks revealed.** Each orientation differs from the four-way mean
by a PLANE of 2-6.6 % ptp in a random direction per placement (not the fixed saddle of the
covers) plus an isotropic r² term of +-2-3 % (0.74-0.86 stops per placement; 180° shallowest in
both sessions). The `--ratio` error bar on opposite pairs is +-3-4 %: the differences do not
invert under rotation, so they are not world-fixed - the camera sits differently every time it
is put down. In the 3-sheet session the camera was re-seated mid-group: 5-11 s pauses after
frames 5, 15, 25 and 38 (2 s otherwise), and the corner ratios jump by 3 points there (6 % plane
at 90°, 10 % at 270° on frames 39-40, +6 % r² at 0°). The 4-sheet session has no pauses and is
stable inside groups (< 0.4 %) except a 2.4 % creep across the 270° group. Blotches: a 200-px
pattern (0.15 % rms) is camera-fixed; a 500-px one (0.5 % rms) does not reproduce between
placements. With the lens in the outline, frames are stable to 0.0003 inside groups and 0° vs
rot180(180°) of the source map agree at corr 0.97 / rms 0.7 % (0.49 and -0.02 the day before):
placement was the whole gradient story. What remains is a WORLD-FIXED blotch field of +-4-5 %
(500-1500 px), rms 2.3 % on one orientation, p95-p5 7.6 % on tiles, which the 4-way rotation
smears into a 4-fold pattern (2.9 % on the master). Not the lens (the non-radial part of the
8 Tokina masters correlates 0.05-0.2 across sources in sensor coordinates), not stable between
lay-ups either (0.1-0.25 with the previous day, 0.58 between that day's two sessions shot
minutes apart): paper stack, air gaps, a spot on the panel.

**Conclusion.** Paper diffuses; the covers do not. The remaining structure is mechanical - the
seating of the camera - and the remaining 0.14-0.15 stops is real source falloff, "nu e rau dar
nici perfect" in the owner's words; better needs a Lambertian diffuser.

## 5. The other lenses (Nikkor 35 DX sky, Samyang 135 LCD)

The depth verdicts of the Nikkor 35 DX sky masters (ISO 400 and 800) and of the Samyang 135 f/2
on an LCD (`sursa_neuniforma`) are ADR 0031's acceptance figures and sit in the summary table
below; how the Samyang, a manual lens whose "non-CPU" ID 74 matches no profile, gets its
candidates by range is ADR 0030. What the sessions add: the Adobe file for the Nikkor has only
a 1.51 m profile at f/2.8, and on these narrow fields the sky's curvature stays under the
reference's resolution; n was not measured.

## What the rotation schemes can and cannot cancel

- **180°** cancels the ODD terms only: the linear gradient (22 % -> 0.02 % on the sky session).
  A saddle, or a falloff steeper along one axis, is even and passes through untouched (§1).
- **90° x 4** cancels the 2-fold part as well (the covers' saddle, the sky's xy term) - but it
  smears the world's blotches into a 4-fold pattern (§4, 7.6 % -> 2.9 %), and it turns an
  anisotropic angular falloff into a radial term indistinguishable from vignetting (§3).
- **The isotropic part of any source's falloff is invisible to every rotation**, and so is
  anything that rotates with the camera. That is why the lens's published profile is the only
  external reference the tool has, and why the sixth test exists. Only a *distance* test
  (lens 2-3 cm off the paper) would split panel from reference; not yet done.

## Reading the depth verdict's sign

- **Plus**: the master falls faster than the model - the source falls off with angle. A panel
  too close or without a real diffuser; frosted sheets do not diffuse (n ~ 2, §3), paper and the
  fabric do only partly (n ~ 0.5, §2, §4).
- **Minus**: the source fills the corners - the sky dome (n = -1.4, §1), ambient light, a
  reflection.
- Neither `--ratio` nor more rotations help: the excess is radial by the time it is measured.
  The fix is a Lambertian diffuser (opal acrylic, a thick white paper stack) or a Lambertian
  reflector (a white wall lit from the side), and a camera that is not re-seated during a
  group: a spacer ring or the hood as a stand, shot with a timer, never touched.
- What the report prints for a plus of +0.10 stops (7 % in the corner: BUN on the residual
  scale, PROBLEMA on `--light`'s, both printed, the first deciding because of the reference's
  0.2-stop spread) is ADR 0031; the mechanics are in `modes-structure.md`, "The sixth test".

## Summary table

| session | source | frames and rotation | structure verdict | depth vs Adobe (stops) | depth vs lensfun | n | note |
|---|---|---|---|---|---|---|---|
| Tokina f2.8-sky-no-difuser (2026-09-16) | twilight sky, no diffuser | 15 + 15 at 180° | not stated (corners ~10 %, residual 9.31 %, worst tile 7.87 %) | -0.39 (0.27 vs 0.66) | not measured | -1.4 | even saddle -6.3 %, chromatic |
| Tokina monitor-difuzor-aproape | monitor, fabric diffuser at the lens | 30, scheme not in the notes | not stated | +0.19 (0.85) | 0.00 vs 7D2, PERFECT | 0.5-0.8 | best of the library |
| Tokina monitor-difuzor-pe-monitor | monitor, fabric on the panel | 30, scheme not in the notes | not stated (worst tile +2 pt of texture) | +0.12 (0.78) | -0.07, BUN | 0.5-0.8 | 300-px cells +-3 %; `--smooth` target |
| Tokina coperti-7-foi (2026-09-18) | tablet, 7 frosted covers | 4 x 10 at 90° | PERFECT / BUN (1.5-2.5 %) | +0.57 (1.26) | +0.34 vs D7100 0.91, REFA | 2.16 | +-5.5 % world-fixed saddle per orientation |
| Tokina coperti-15-foi (2026-09-18) | tablet, 15 frosted covers | 4 x 10 at 90° | PERFECT / BUN (1.5-2.5 %) | +0.56 (1.24) | +0.33 vs D7100 0.91, REFA | 2.08 | +-3.5 % saddle; -0.28 stops of light vs 7 |
| Tokina hartie-3-foi (2026-09-23) | tablet, 3 sheets of paper | 4 x 10 at 90° | BUN (residual 3.8 %) | +0.15 (0.81 vs 0.66) | vs 0.85 / 0.91, not stated | 0.55 | re-seated after frames 5, 15, 25, 38 |
| Tokina hartie-4-foi (2026-09-23) | tablet, 4 sheets of paper | 4 x 10 at 90° | BUN (residual 5.1 %) | +0.15 (0.80 vs 0.66) | vs 0.85 / 0.91, not stated | 0.46 | no pauses; 2.4 % creep across 270° |
| Tokina hartie-3-foi-rotiri-perfecte (2026-09-23) | tablet, 3 sheets, lens in a pencil outline | 4 x 10 at 90°, rotated in place | BUN (corners 1.2, tilt 0.6, residual 3.8, worst tile 4.6 %) | 0.76 vs 0.66 | not measured | not measured | world-fixed blotches +-4-5 %, 4-fold 2.9 % |
| Nikkor 35 DX f2.8-sky ISO400 / ISO800 | twilight sky | 30 each, scheme not in the notes | not stated | +0.11 / +0.10, BUN | not measured | not measured | Adobe f/2.8 profile at 1.51 m only |
| Samyang 135 f2.0-lcd ISO1600 | LCD, `sursa_neuniforma` | 30, scheme not in the notes | not stated | +0.11 / +0.10 BUN (f/2), +0.01 PERFECT (f/2.8) | by range, Samyang lensfun | not measured | manual lens, candidates by range |

## Two tool caveats seen on these sessions

- **The partial-ring model on a single-orientation stack.** ADR 0017 records it: one
  orientation of the paper session read 17-18 % residual and a red-corner / blue-ring map for
  7.6 % of true non-radial content until the 2θ+4θ ring correction of 2026-09-23 (7.9 % after),
  and the sharp arcs and the crease on the inscribed circle of the sky master's map were the
  earlier face of the same artefact, fixed on 2026-09-17. The masters were fine; the
  measurements are in `radial-model-even-term.md` (§7).
- **The local-spot metric on a frame edge.** ADR 0041 records it, with the two Nikkor sky
  masters of this note as the measured cases (a stripe on an edge read as the extreme, a fifth
  of the warning): on a master with a harder edge, check whether the extreme is round before
  trusting it as dust.
