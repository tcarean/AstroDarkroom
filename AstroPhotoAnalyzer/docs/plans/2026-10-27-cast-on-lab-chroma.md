# The cast is judged on the sky colour's chroma in L*a*b*, not on an excess of one level

Branch `claude/Sky-colour-from-the-sky-patches`, the same branch and worktree as ADR 0055 and
ADR 0056, on the owner's word. The approved plan is saved to
`docs/plans/2026-10-27-cast-on-lab-chroma.md`.

## Context

The owner asked whether the green cast the report gives the 2026-08-12 core (G over the mean of
R and B by +2.2 levels, threshold `CastWarnLevels` 1.0) is real: the frame is calibrated
photometrically and neutral to the eye. Measured on 2026-10-27 in CIE L*a*b* on the five real
images' sky colours (the reference of ADR 0055):

| Image | sky R / G / B | L* | a* | b* | chroma | G - (R+B)/2 | R - B |
|---|---|---|---|---|---|---|---|
| the 2026-08-12 core | 35.4 / 38.5 / 37.2 | 15.0 | -1.7 | +0.4 | 1.8 | +2.2 | -1.8 |
| `de_printat.png` | 24.0 / 25.9 / 25.9 | 9.0 | -0.9 | -0.3 | 1.0 | +0.9 | -1.9 |
| `starless_rbf0.5_stretch0.17.fit` | 40.9 / 43.4 / 41.1 | 17.4 | -1.7 | +1.1 | 2.0 | +2.4 | -0.2 |
| `Rosetta_starless_no_WCS.fit` | 26.3 / 23.8 / 23.3 | 8.4 | +1.1 | +0.8 | 1.4 | -1.0 | +3.0 |
| `Veil_Starless.fit` | 19.0 / 27.0 / 23.6 | 8.9 | -4.6 | +1.2 | 4.8 | +5.7 | -4.6 |

One level of green excess is a* -0.7 at any sky level from 20 to 64 (L* 7 to 27): a
difference no eye sees; the usual just-noticeable figure beside a grey reference is about 2.3,
and on paper, where the only reference is the white far above, a sky's cast begins to read at
3 to 5. A gamma lift keeps the channels' ratio: the 2026-08-12 sky lifted to L* 20 or 25 stays
at a* -1.9, chroma 2.0, so "with a raised background it will show", the rule's reason, does
not hold, and for that frame no lift is recommended at all. A star-based calibration leaves
the sky its physical colour, airglow green and pollution yellow: a faint green after it is
expected, not a defect. The level rule flagged four of the five images; a chroma of 3 flags
the Veil alone, visibly green.

## Decision

A cast is a sky colour whose chroma in CIE L*a*b*, sqrt(a*^2 + b*^2), exceeds
`CastWarnChroma` (3.0); `CastWarnLevels` goes. G - (R+B)/2 and R - B stay as figures and
name the cast's direction (green, magenta, warm, cool) as before. The report's `Because` gives
the chroma with a* and b* and the threshold, Appendix E the L*, a*, b* and chroma, and the
Culoare tab a chroma row above the two level rows. ADR 0057 records it.

## Steps

1. R-060 and R-093 restated; tests: a two-level tint measured but no cast, a five-level one a
   cast, grey and mono at 0; the dark scene's two levels no longer a cast, a flat green sky
   the cast's test.
2. `ColorMath.LabOfSrgb`; `BackgroundResult.Lab`, `Chroma`, `HasCast`; the texts.
3. Goldens, `/validate` (`background.hasCast` moves on V-001, V-004, V-010; `background.lab.*`
   and `background.chroma` recorded), the drift recorded.
4. ADR 0057; `measurements.md`, `thresholds.md`, `known-limitations.md`, `validation.md`,
   CHANGELOG.
