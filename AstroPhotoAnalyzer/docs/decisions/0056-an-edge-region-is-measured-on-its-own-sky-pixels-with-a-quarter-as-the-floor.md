# 0056. An edge region is measured on its own sky pixels, on every pixel when under a quarter of it is sky

Date: 2026-10-27
Status: accepted 2026-10-27
Supersedes: 0055 in part (the regions); 0018 in part (the names and the advice of a region darker than the sky)

## Context

ADR 0055 judges the eight edge regions against the colour of the sky patches, but each region
still took its medians on every pixel it holds. The Milky Way band entering the reference
image's top-left corner, bluer than the sky, therefore read +6.5 / +5.0 / +8.8 and was flagged
coloured by ADR 0018's rule, as a bluish gradient would be; ADR 0055 left measuring each region
on its own sky patches as an open option, with the risk that a horizon glow bright enough to be
classed nebula would leave the region with the object.

The measurement of 2026-10-27 on the five real images is in
`docs/plans/2026-10-27-edge-regions-on-sky-patches.md`: on the sky pixels alone, two of forty
readings change their verdict, the band entering the reference image's top-left corner
(+4.2 / +2.9 / +5.6, neutral) and the band crossing the plate-solved FITS's top edge
(-1.3 / -1.7 / -4.2, neutral); the two horizon regions of both cores keep their coloured flag
(the reference image's corner +4.1 / -4.0 / -17.1); the lowest sky share of any region is 41 %,
the bottom edge of the two cores, where the glow's brightest part is classed band or bright
cloud and its blue deficit keeps the flag on the sky pixels. Rosetta and the Veil do not move.

## Decision

Each edge region takes its per-plane medians on its sky pixels, those of the patches R-044's
classification calls sky, when they are at least `RegionSkyMinFraction` (25 %) of the region.
Under that share the region is measured on every pixel, as before, and its row in Appendix E
and in the Culoare tab says so, `(sky 8 %: every pixel)`, as does the `Because` of a coloured
region; the floor keeps a glow that fills a region and is classed nebula from vanishing with
the object. The regions are taken on the map's subsample (stride 4), where the classes can be
read, instead of the acceptance script's stride 3: `BackgroundStride` is removed, and the
regions' levels in `docs/validation.json` move by tenths of a level with the subsample. ADR
0018's rule for coloured against neutral is unchanged.

## Alternatives rejected

- **Every pixel, as before.** Two readings of the band as a coloured gradient on the two
  reference frames; the owner's complaint was the band passing for a gradient.
- **Sky pixels with no floor.** A region the object fills would read on its last sky pixels,
  or on none; a glow classed nebula over the whole region would go unreported.
- **Flag on either reading.** Brings the band's own colour back as a gradient.

## Consequences

- On the reference image the two warm horizon regions remain and the top-left corner is
  neutral, informative; on the 2026-08-12 core the horizon's two regions remain and the
  top-left corner, the Rift's diffuse foot, reads darker and browner on its sky pixels
  (-5.4 / -4.5 / -8.7) than on every pixel: the sky there is darker and less blue than the
  frame's, which the eye must judge.
- The glow's magnitude on a region it fills in part is the sky pixels' (the plate-solved
  FITS's bottom edge +1.4 / -4.0 / -5.0 where every pixel gave +12.3 / -0.9 / -12.1); the flag
  does not change on any measured image.
- Validation cases V-001, V-004, V-010 and V-013 re-measured; the goldens of the dark scene
  change where the regions' levels are printed.
- A coloured region under the sky on every channel is named a darkening, browner or bluer by
  the channel missing most, with dust or a leftover gradient as the reading and the Preview as
  the judge; the "horizon glow / light pollution" wording was written for a brightening and
  read wrong on that corner (the owner's remark of 2026-10-27). On the bottom row, where the
  horizon sits (ADR 0014), red over blue keeps the horizon's name whether the region is
  brighter or darker: the 2026-08-12 core's bottom edge, -0.6 / -2.2 / -4.5, is the horizon.
- Guards: `BackgroundAnalyzerTests` (R-061), `RecommendationsTests`, the golden files, the
  validation cases.
