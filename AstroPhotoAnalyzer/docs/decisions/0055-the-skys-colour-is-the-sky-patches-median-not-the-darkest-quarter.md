# 0055. The sky's colour is the median of the sky patches above the horizon band, not of the darkest quarter

Date: 2026-10-27
Status: accepted 2026-10-27; the regions superseded by 0056
Supersedes: 0014 in part (the colour)

## Context

ADR 0014 took the sky's colour, the reference of the cast and of the eight edge regions, from
the darkest quarter of the pixels above the bottom band: on the reference image that quarter
is sky, and the edge regions came out as ADR 0018 records. On the owner's Milky Way core of
2026-08-12, flattened in GraXpert and clean to the eye but for the horizon in the bottom-right
corner, the darkest quarter is the Great Rift's dust, brown (R 30.5, G 32.8, B 27.2 against a
plain sky of 36 / 39 / 38), and every edge reads 4 to 13 levels bluer than it: six of the eight
regions flagged "cool", and a green cast of +3.9 levels that the sky itself has at +2.2. The
reference image had passed only because its dust differs from its sky almost neutrally
(19 / 21 / 19 against 24 / 26 / 26): the same comparison of sky with dust, read as neutral there
and as coloured here. Any frame whose darkest quarter is dust, every Milky Way core, fails the
rule the same way, since the sky is always bluer than the dust.

The measurement of 2026-10-27 on five real images is in
`docs/plans/2026-10-27-sky-colour-from-the-sky-patches.md`: against the frame's median, the
median of the sky patches of R-044 over all rows, or the same above the bottom band, the two
warm horizon regions of the reference image stay flagged coloured and the false flags go; the
sky patches above the bottom band are the cleanest on the new core (three flags, the two of the
horizon and the Rift's dust in the top-left) and on Rosetta, where the frame's median holds
nebula and is three levels redder than the sky.

## Decision

The sky's colour is the per-plane median of the pixels of the sky patches, the class
`Sky` of R-044's map (`DustMap.Classes`), that lie above the bottom
`BackgroundExcludeBottomFraction` (25 %) of the rows: the sky in the tool's own sense (ADR
0013), with the band, the dust and the nebulae left out, and the horizon band left out as
before. When no sky patch lies above the band, the pixels of every patch there; when the frame
holds no patch, every pixel. The result says which of the three gave the reference, and Appendix
E prints it. The cast and the edge regions are judged against that colour, the regions still on
their own subsample and by ADR 0018's rule. `BackgroundDarkestFraction` is removed; the `<=`
tie rule of ADR 0014 no longer applies to anything.

## Alternatives rejected

- **Keep the darkest quarter.** It is the dust on every frame the Rift or a dark nebula
  dominates, and the rule then reports the sky's colour against the dust's as a gradient.
- **The frame's median above the bottom band.** Simpler, and right on the two cores, but it
  holds the nebula on a nebula frame (Rosetta: six regions flagged against it, three against the
  sky patches) and the band on a frame the band fills.
- **The sky patches over all rows.** Within a level of the chosen reference on every image; the
  horizon glow of the reference image sits in those rows at the sky's luminance, classed sky,
  and would enter the reference it is judged against.
- **Measure each region on its own sky patches.** Not measured here; it would also keep the band
  entering a corner from reading as coloured, but a horizon glow bright enough to be classed
  nebula would leave the regions with it. Measured the same day and adopted, with a floor, by
  ADR 0056.

## Consequences

- On the new core exactly the horizon's two regions and the Rift's dust in the top-left corner
  remain; on the reference image the two warm regions remain and the top-left corner, where the
  band enters, now reads +6.5 / +5.0 / +8.8, coloured by ADR 0018's rule (bluer than the sky),
  where against the dust it read +11 / +10 / +16, neutral. The band is bluer than the sky; the
  rule cannot tell it from a bluish gradient while the band's pixels count (ADR 0056 leaves them
  out).
- The reference image's green excess falls from +1.6 to +0.9 levels, under the level rule's
  threshold, but red minus blue goes from +0.3 to -1.9: under that rule its cast became a cool
  one, and the new core kept a green one, +2.2. ADR 0057 then replaced the rule by the
  chroma in L*a*b*, under which neither has a cast.
- Validation cases V-001, V-004, V-010 and V-013 re-measured; the goldens of the dark scene
  change where the background's levels and the regions' deltas are printed.
- Guards: `BackgroundAnalyzerTests` (R-060, R-061), `RecommendationsTests`, the golden files,
  the validation cases.
