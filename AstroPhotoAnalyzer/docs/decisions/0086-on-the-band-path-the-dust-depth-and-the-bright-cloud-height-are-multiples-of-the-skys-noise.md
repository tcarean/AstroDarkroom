# 0086. On the band path, the dust's depth and the bright clouds' height are multiples of the sky's noise, not fixed L* values

Date: 2026-11-07
Status: accepted 2026-11-07
Supersedes: 0026 in part (the fixed depth and height in L* of the dust and the bright clouds)

## Context

ADR 0026 made a band-path patch dust when `DustDepthLstar` (5) L\* under the band's local level
(or the sky model) and a bright cloud when `BrightCloudLstar` (10) over it: absolute depths,
chosen on the reference frame at a background of 0.17. ADR 0085 made the sky path's thresholds
multiples of the sky's noise measured on the image, and its measurement at two stretches showed
the fixed depth's weakness: on the same frame re-stretched to 0.10 the 5 L\* found 5.7 % of the
patches (L\* 3.20) against 9.4 % (7.90) at 0.17, since a darker stretch compresses the Rift's
depth under 5 L\*. The owner asked on 2026-11-06 for the band path's threshold to be fixed too,
then whether the WCS and a better catalogue could say where the dust is instead, and had the
dust-map idea measured before deciding.

### Measured on 2026-11-06 and 2026-11-07

With scratchpad probes over the core, on the 32 px patches, baryta at Dmax 2.4; four frames with
a WCS: the plate-solved reference at backgrounds 0.10 and 0.17 (`starless_rbf0.5_stretch0.1.fit`,
`..._0.17.fit`), the Smida frame of 2026-08-12 with its stars and the same frame starless.

**Three noise figures, each with factors 2 to 4** (the dust at k x sigma under the local level or
the sky model, the bright clouds at 2k x sigma over the local level):

- The sky outside the band against the sky model: 0.5 to 0.8 L\*, small because that sky is
  smooth; depths of 1 to 3 L\* make 13 to 20 % of the frame dust, with 30 to 70 % of it the
  band, not the Rift.
- The band against its local level: 2.2 to 3.5 L\*, inflated by the Rift itself, the largest
  part of what departs from the local level; only k = 2 reaches 7 %, under today's 9.4 %.
- All patches against the median-cell model, the sky path's own `SkySigma` (1.11 L\* at 0.17,
  0.75 at 0.10, 1.17 on Smida, 0.81 on Smida starless):

| frame | today, 5 L\* fixed | 4 sigmas (depth) | 3 sigmas (depth) |
| --- | --- | --- | --- |
| reference 0.10 | 5.7 %, L\* 3.20, bright 1.4 % | 10.1 %, 3.74 (3.0 L\*), bright 3.1 % | 12.4 %, 3.99 (2.2 L\*) |
| reference 0.17 | 9.4 %, 7.90, bright 2.0 % | 10.3 %, 8.11 (4.5 L\*), bright 2.4 % | 12.6 %, 8.59 (3.3 L\*) |
| Smida with stars | 9.5 %, 4.85, bright 1.7 % | 9.9 %, 5.00 (4.7 L\*), bright 1.9 % | 11.7 %, 5.48 (3.5 L\*) |
| Smida starless | 9.1 %, 4.03 | 11.9 %, 4.62 (3.3 L\*) | 13.7 %, 5.00 (2.4 L\*) |

With 4 sigmas every patch of the 5 L\* class at 0.17 stays dust (recall 100 %) and 92 % of the
new class is that class; the share of the frame is the same at both stretches (10.1 and
10.3 %), where the fixed depth gave 5.7 and 9.4 %. With 3 sigmas the class grows past the
9.4 % that ADR 0025 checked against the catalogue.

**A dust map instead of a threshold** (the owner's question of 2026-11-06): the SFD98 E(B-V)
map (Schlegel, Finkbeiner and Davis 1998, two Lambert maps of 4096 x 4096, 6.1 arcmin FWHM,
CC0 1.0 on the Harvard Dataverse, doi:10.7910/DVN/EWCNL5) read at every patch centre through
the WCS (the core's galactic latitude agrees with astropy's to 0.000 degrees). On today's dust
class the extinction is high (median E(B-V) 1.9 to 2.1 mag), on the sky class low (0.21), but
the band patches that are not dust reach the same extinction (their 90th percentile 2.6 to
4.0): dust in front of the band's fainter parts, or thinner, does not read as dark. Map dust =
E(B-V) over t: at t = 1 it is 17 % of the frame, finds 87 to 94 % of today's dust class, and 31
to 50 % of it is that class; at t = 2, 8 % of the frame, 48 to 53 % and 38 to 56 %. No t passes
57 % precision: about half of the map's dust is not dark in the picture (the Rift's flanks, the
band's fainter parts, the Ophiuchus clouds outside the band). The verdict's figure on the map's
selection at t = 1.5 to 2 is within 0.2 to 0.4 L\* of the 4-sigma rule's (3.81 / 8.28 / 5.41
against 3.74 / 8.11 / 5.00). A copy of the map averaged to 512 x 512 per hemisphere (21 arcmin,
0.5 MB as bytes on a log scale) gives the same figures to 0.05 L\*, so a finer map (Planck,
Dobashi 2005) would not change the result: the difference is physical, not resolution. What a
map could add: a veto on image-only dust with no extinction behind it (the reference frame's
bottom-right gradient corner, 1 to 5 % of the class) and the clouds' extent for the map window.

## Decision

The owner chose, on 2026-11-07, the 4-sigma rule, after the map study: on the band path a
patch is dust when `BandDustK` (4) sigmas of the sky's noise under the band's local level (or
under the sky model, anywhere), a bright cloud when `BandBrightK` (8) sigmas over the local
level (outside the band, a patch as much over the sky model is band), twice the dust's depth
as before. The noise is the one `DustAnalyzer.SkyNoise` gives both paths: the robust sigma of
every patch's residual from the median-cell model over all the patches, after one clipping
pass (ADR 0033, ADR 0085); the map carries it as `SkySigma`, with `NoiseThreshold` and
`BrightThreshold` now set on both paths. The report's definition of the dust class and the
map's legend say the depth in L\* and the factor, as the sky path's already did. The dust map
is not adopted for the verdict.

## Alternatives rejected

- **3 sigmas, one factor for both paths.** One constant fewer, but 12 to 13 % of the reference
  frame, past the catalogue-checked 9.4 %.
- **The outside-band sky's noise, or the band's residual from its local level, as the sigma.**
  The first is too small on a smooth sky and fills the class with band; the second is inflated
  by the dust it is meant to find.
- **The SFD98 map as the selector.** Half of what it selects is not dark in the picture, and
  its verdict figure is the 4-sigma rule's within a few tenths; it would add a 0.5 MB resource
  and a Lambert lookup for no better figure. Kept as an idea for a veto or for the map window.
- **A depth as a share of the sky's level.** A hand-chosen constant in place of one read from
  the image.

## Consequences

- `Thresholds.cs`: `DustDepthLstar` and `BrightCloudLstar` replaced by `BandDustK` and
  `BandBrightK`; `DustAnalyzer.Classify` takes the sigma from the new `SkyNoise`, which
  `ClassifyBySky` shares; `DustMap.SkySigma`, `NoiseThreshold`, `BrightThreshold` set on the
  band path too. The texts: `PrintStretchSimulator.DustClassDefinition`, the map window's
  legend (`MapViewModel`), the Tonuri tab's guide (`ResultTabs`). R-044, the README's band-path
  paragraph, `docs/design/measurements.md`, `thresholds.md`.
- The figures of the band-path cases move: V-004 (the dust class 9.4 to 10.3 % of the patches,
  L\* 7.90 to 8.11, the bright clouds 2.0 to 2.4 %) and V-008 (the curve anchors). The sky-path
  cases do not.
- The synthetic band scene of the tests (`WcsFixtures.BandScene`) was flat and noiseless, so
  its sigma was 0 and its thresholds 0: the band patches at the strip's smoothed hard edge
  read as bright clouds by rounding alone (42 of them at the band's level, so the bright
  anchor of the curves fell to the band's and the curve window's tests failed). Ramps at the
  strip's edge do not help: on a 16 x 16 patch frame, one cell of the sky model, a ramp's
  spread becomes the "noise" (sigma 5 to 7 L*, thresholds over 20, the bright blob no longer
  bright). The scene now carries a gentle ripple of 0.01 on the sky and the band (about 1.5 L*
  either way, the unevenness a real sky has at the patch scale) and pixel noise of 0.3 of 255:
  sigma 0.69 L*, thresholds 2.8 and 5.5 L*, the blob dust, the bright blob's patches the bright
  class, the strip's mixed edge patches dust as before. No golden file moved.
