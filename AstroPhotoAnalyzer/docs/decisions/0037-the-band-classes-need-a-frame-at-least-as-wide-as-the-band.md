# 0037. The band classes need a frame that spans at least the band's width in galactic latitude

Date: 2026-10-11
Status: accepted 2026-10-11
Supersedes: 0026 in part (the two fraction tests were the only gate to the band path)

## Context

The band path of ADR 0026 was taken whenever a WCS existed and the galactic band |b| < 10°
and the sky outside it each held at least `BandMinFraction` (10 %) of the patches. The owner
found `Veil_Starless.fit` (5601 x 3692 px, 6.2"/px, a 9.6° x 6.4° field in Cygnus) classed
that way: the frame spans b from -14.6° to -3.6°, so 65 % of the patches are "banda" and 35 %
"cer" by geometry, both over the floor, and the tool drew a band map over a deep-sky nebula and
anchored the suggested curves on a "banda" class that was plain sky.

Measured on 2026-10-11 with a probe over the core, on the 32 px patches (the band's median
patch minus the median sky model built from outside the band; sigma is the sky path's robust
sigma of the residual, ADR 0033):

| image | field | span in b | geometric band | band over sky | sigma | ratio |
| --- | --- | --- | --- | --- | --- | --- |
| `starless_rbf0.5_stretch0.17.fit` | 67.6° x 44.8° | 68° (-29 .. +39) | 33 % | +4.1 L\* | 1.04 | 3.9 |
| `starless_rbf0.5_stretch0.1.fit` | the same | the same | 33 % | +2.8 L\* | 0.69 | 4.1 |
| `Veil_Starless.fit` | 9.6° x 6.4° | 11° (-14.6 .. -3.6) | 65 % | +0.5 L\* | 0.82 | 0.6 |

The profile of the median patch L\* against |b| in 2° bins is flat on the Veil (9.1 to 10.5 in
every bin) and rises from 15 to 25.8 inside the band on the reference. The Veil's geometric
band is not a band in the picture: at 10° the Milky Way is a smooth gradient across the whole
frame, not a strip with sky beside it. The LDN catalogue does not separate the two cases: the
Veil frame holds six catalogued clouds with 9 or more patches (LDN 870 with 1712), because the
dark nebulae lie all along the plane; the content of the sky is the same, the scale is not.

The band classes were designed for one kind of image, the landscape with the Milky Way, where
the band is a strip and the subjects are the Rift (the dust under the band's local level) and
the bright clouds. On every other frame, the nebulae inside the band included (Veil, North
America, Sadr, M8), the subject is the object and the classes relative to the modelled sky
(ADR 0033) are the right ones. Every deep-sky focal length gives a field under the band's
width: 135 mm on full frame is 15° x 10°.

On the sky path the Veil gives: sky 96.4 %, nebula 2.9 % (over the modelled sky by 3.3 L\*,
4 sigma), bright nebula 0.5 % (over 15.9 L\*), peaks 0.1 %, dust class 1 patch; the dust the
darkest sky at L\* 6.3 (the band path said 7.1), the sky 9.3 (9.5), the recommended row 0.16 on
both paths. The curves' anchors move from a "banda" that was sky to the nebula's own classes:
0.078, 0.102, 0.166, 0.359, 0.498, four suggestions, `Maxima` taking the peaks to 0.92 (V-014).

## Decision

The band path has a third condition, tested after the two fractions: the patch centres must
span at least `BandMinLatitudeSpanDeg` (20°) of galactic latitude, the band's full width
(2 x `BandGalacticLatitudeDeg`). A narrower frame cannot show the band as a strip with sky
beside it, whatever the share of patches on each side of |b| = 10°, and takes the sky path with
the reason `SkyReason.NarrowField`, which the report, the Tonuri tab, the map and the Curbe tab
name with the frame's own span: `camp prea ingust pentru banda (10.9 grade de latitudine
galactica, sub 20)`. The span is the maximum minus the minimum galactic latitude of the patch
centres (`DustMap.LatitudeSpanDeg`), known from the WCS before any patch is measured: a fact
of the frame, not a measurement of the image. The owner chose 20° on 2026-10-11.

## Alternatives rejected

- **A photometric test: the band's median patch at least k sigma (or k L\*) over the outside
  sky.** It separates the measured frames (3.9 and 4.1 against 0.6 sigma; 4.1 and 2.8 against
  0.5 L\*), but it asks the image whether the Milky Way is visible when the WCS already says
  whether the frame can show it as a band; a faintly stretched landscape would have fallen to
  the sky path, where the Rift is not found (ADR 0033's known limit). The owner preferred the
  geometric fact.
- **Confirming the band through catalogued objects in the frame.** Every deep-sky frame inside
  the band holds LDN clouds, the Veil six of them; the test would say yes on both kinds of
  frame.
- **A deep-sky object catalogue (OpenNGC) to recognise the frame's subject.** The way to
  classes relative to the object's ellipse, which ADR 0026 left open; new data in the project,
  and not needed for this decision.
- **A narrower band (a smaller `BandGalacticLatitudeDeg`).** The Veil is at b = -8.5°, inside
  any sensible band; the problem is the frame's size, not the band's.

## Consequences

- `Thresholds.BandMinLatitudeSpanDeg`; `SkyReason.NarrowField`; `DustMap.LatitudeSpanDeg`;
  `DustAnalyzer.Classify` tests the span after the two fractions; `SimulationResult.MethodNote`
  has the fourth sentence; the Tonuri guide names the condition. R-044 rewritten.
  `WcsFixtures.NearTheVeil` and `BandScene(wcs, withDust)`: the band scene drawn on a 10° field
  near the Veil, with both sides of |b| = 10° over the floor, so that only the span decides.
- `Veil_Starless.fit` joins the manifest; V-013 (the analysis) and V-014 (the curves) record
  the sky path on it. V-001 to V-012 did not move: the reference image spans 68°.
- Known limit: a Milky Way landscape at a long focal length (an 85 mm frame held horizontal
  spans about 16° of latitude, diagonal about 28°) may fall on either side of the gate
  depending on its orientation; on the sky path its band reads as nebula and its Rift is found
  only where it falls under the modelled sky.
