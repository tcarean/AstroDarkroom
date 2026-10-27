# 0057. A cast is a sky colour with a chroma over 3 in L*a*b*, not an excess of one level on a channel

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

The cast rule of the acceptance script, kept by ADR 0014's measurement of the colour, flagged
the background when green exceeded the mean of red and blue, or red exceeded blue, by more
than `CastWarnLevels`, one level, on the reasoning that a cast invisible on a dark screen
shows once the background is lifted for paper. On the owner's 2026-08-12 core, calibrated
photometrically and neutral to the eye, the sky patches of ADR 0055 read +2.2 levels of green
and the report called for a neutralisation. Measured on 2026-10-27 in CIE L*a*b*
(`docs/plans/2026-10-27-cast-on-lab-chroma.md`): that sky sits at a* -1.7, b* +0.4, a chroma
of 1.8; one level of green excess is a* -0.7 at any sky level, under anything an eye sees; the
just-noticeable difference beside a grey is about 2.3, and on paper, with no grey beside the
sky and the white far above, a cast begins to read at 3 to 5. A gamma lift keeps the channels'
ratio, so the chroma does not grow with the background (that sky lifted to L* 25 stays at 2.0),
and for that frame no lift is recommended. A star-based calibration leaves the sky its physical
colour, airglow green and pollution yellow; a faint green after it is expected. The level rule
flagged four of the five real images (chromas 1.0, 1.4, 1.8, 2.0 and 4.8); the Veil alone, at
4.8, is green to the eye.

## Decision

The sky's colour (ADR 0055) is converted to CIE L*a*b* (D65, `ColorMath.LabOfSrgb`), and a
cast is a chroma, sqrt(a*^2 + b*^2), over `CastWarnChroma`, 3.0: the size of the cast in the
unit the eye works in, the same one Dmax and the dust's L* use elsewhere in the report, and
one a lift barely moves. `CastWarnLevels` is removed. G - (R+B)/2 and R - B remain figures,
and the larger of the two names the cast green, magenta, warm or cool, as before. The
recommendation's `Because` gives the colour, the chroma with a* and b*, the threshold and the
two level figures; Appendix E prints L*, a*, b* and the chroma; the Culoare tab has a chroma
row above the two level rows, which carry no verdict of their own.

## Alternatives rejected

- **Raise `CastWarnLevels` to 3.** The same images pass and fail, but a level of excess is a
  different chroma at different sky levels and in different directions (one level of green is
  a* 0.7, one of red over blue about half that), and the rule would keep saying a cast grows
  with the lift, which it does not.
- **A chroma of 2.3, the side-by-side figure.** The plate-solved FITS, at 2.0, would sit at the
  edge; a print offers no grey beside the sky.
- **Judge the chroma on the lifted sky.** Within 0.2 of the as-is figure on every image, and
  the lift is a recommendation the owner may not follow.

## Consequences

- Of the five real images only the Veil keeps a cast; the reference image's `hasCast` (true,
  for +1.6 of green against the darkest quarter and -1.9 of red minus blue against the sky
  patches) turns false, as do the plate-solved FITS's and Rosetta's; `background.lab.*` and
  `background.chroma` are recorded for the four colour cases.
- The dark scene's two levels of green (chroma 1.7) are no longer a cast; the cast's own test
  is a flat sky with five levels of green (R-093).
- Guards: `BackgroundAnalyzerTests` (R-060), `RecommendationsTests` (R-093), the golden
  files, the validation cases.
