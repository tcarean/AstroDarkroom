# 0026. The dust and the sky are classes of patches relative to the galactic band, not the catalogue's discs

Date: 2026-10-02
Status: accepted 2026-10-02
Supersedes: 0025 in part (decision 3, the dust and the sky; decision 4's map layers)

## Context

ADR 0025 made the dust the 10 % percentile of the patches of all catalogued clouds together and
the sky the median of the patches outside the discs, and the map window painted those two sets.
On the plate-solved FITS the owner found the map wrong to the eye: "dust" did not cover the
Great Rift (the discs of its clouds are lighter than their cores, and only the darkest 10 % of
cloud patches were painted), and "sky" left the frame's plain lateral edges unpainted while
painting the Milky Way band, which is outside every catalogued disc. The owner pointed to a
separate conversation on building an S-curve for the same image, which split the image into
five visual classes, dust, sky, band, bright clouds and peaks, and proposed a relative criterion:
the band from the galactic latitude of the WCS, the sky modelled from outside the band, the dust
as "darker than the band would be there", the bright clouds as "much brighter than the band's
local level".

Measured on 2026-10-02 on `starless_rbf0.5_stretch0.17.fit` (ADR 0025's image), on the 32 px
patches: absolute thresholds at the midpoints of that conversation's five curve points (L\* 12.2,
26.4, 46.8, 66.6) give dust 7.7 %, sky 76.7 %, band 12.3 %, bright 2.7 %, peaks 0.6 %, cover the
Rift and the edges, but also class the gradient-darkened top-left corner as dust and the faint
band as sky, and move with the stretch. The relative classes, with the band at |b| < 10° (33 % of
the frame), the sky modelled on a coarse grid from outside the band (11.7 to 24.3 L\* across the
frame), the band's local level a smoothed median over 15 patches, and the dust 4, 5 or 6 L\*
under that level: dust 11.1, 9.3, 7.9 % of the frame, sky 65 %, band 21 to 25 %, bright 1.4 to
3.1 %, peaks 0.1 %; the Rift is dust in full, the edges are sky, the band is a straight strip, the
gradient corner is no longer dust. For the verdict, the dust class needs a level: its 10 %
percentile (5.7 L\*) asks for a target of 0.213, its 25 % percentile (7.5) for 0.188, its median
(10.1) for 0.160; the sky class's median is 17.6. The owner chose variant B on 2026-10-02, and the
25 % percentile: three quarters of the dust keeps tone above paper black, the darkest quarter
(the Pipe's cores, black on screen too) prints flat black; the same recommendation as ADR 0025
gave on this image, 0.19, with the dust defined by the image rather than by the catalogue.

The classes need a galactic band and plain sky outside it in the frame. The owner asked
whether the method covers the winter Milky Way and deep-sky frames: it does not. The winter
band is a few L\* above the sky and its dark clouds (Taurus) lie outside |b| < 10°; a deep-sky
field is either wholly in the band (M8) or wholly outside it (a galaxy). Those frames fall back
to the rule of ADR 0025's decision 4, and the report says why. The same relative criterion could
later be run against a catalogued object's ellipse (OpenNGC gives positions and axes) instead
of the band, which is the way to deep-sky; not now, and not without sample images.

## Decision

The dust and the sky are two of five classes of the 32 px patches, decided once on the image as
it is, relative to the galactic band (`DustAnalyzer.Classify`):

- the band is the patches with galactic latitude |b| < `BandGalacticLatitudeDeg` (10), from
  the WCS and the equatorial-to-galactic rotation;
- the sky model is the median of the outside-band patches on cells of `SkyModelCellPatches`
  (20), cells with fewer than `SkyModelCellMinPatches` (20) sky patches taking the nearest
  cell's value, interpolated bilinearly and smoothed on `SkyModelSmoothPatches` (3);
- the band's local level is a median over `BandLocalWindowPatches` (15) smoothed on
  `BandLocalSmoothPatches` (2);
- a patch is **dust** when it is `DustDepthLstar` (5) under the local level (in the band) or
  under the sky model (anywhere); **bright cloud** when in the band and `BrightCloudLstar` (10)
  over the local level; **peak** when among the brightest `PeakFraction` (0.1 %) patches;
  **band** otherwise in the band, or outside it when `BrightCloudLstar` over the sky model;
  **sky** otherwise.

The dust's L\* is the `DustClassFraction` (25 %) percentile of the dust patches, the sky's L\* the
median of the sky patches, recomputed on every lift over the same patch sets. The classes apply
only when the band and the outside sky each hold at least `BandMinFraction` (10 %) of the
patches; otherwise, as without a WCS, the frame rule of ADR 0025 stands (the darkest 1 % of the
patches, the median), and the report names the reason (`fara WCS`, `fara banda galactica in
cadru`, `cadrul e in intregime in banda`). The catalogue no longer defines the dust: it names the
clouds, gives each one with 9 or more patches its row, and draws the circles on the map. The map
window shows the five classes as layers, each behind a check box, so the owner can judge them on
other images; the clouds' circles and labels are one more layer, with their list.

## Alternatives rejected

- **Absolute thresholds from the S-curve's percentiles.** They cover the Rift but class the
  gradient-darkened corner as dust and the faint band as sky, and they move with the stretch.
- **Keep ADR 0025's cloud-disc dust for the verdict and use the classes for the map only.**
  Two notions of "praf" in one tab; the owner asked for the classes' definitions in the verdict.
- **The 10 % percentile of the dust class.** It asks for 0.213: the Pipe's cores, which the
  owner rejected in ADR 0025 as washing the sky.
- **The median of the dust class.** It asks for 0.160 but prints half the Rift flat black.
- **A galactic-latitude cut on the sky (ADR 0025 rejected it).** It comes back here as the band's
  definition, not as a cut on the sky's median: the sky model is built from outside the band, and
  the band is a class of its own.

## Consequences

- `Astrometry.GalacticLatitude`; `DustAnalyzer` builds the map from the as-is patch L\*s
  (`Build(w, h, wcs, asIsPatchLstars)`), with `SkyModel`, `MedianFilter`, `Smooth`, `Classify`;
  `DustMap.Classes`, `GalacticLatitude`, `SkyModel`, `BandLevel`, `FractionOf`; `DustMethod`
  `Classes` / `NoWcs` / `NoBand` / `AllBand`; `PatchClass` with five classes;
  `SimulationResult.ClassFractions` in Anexa B. Thresholds: `BandGalacticLatitudeDeg`,
  `BandMinFraction`, `DustDepthLstar`, `BrightCloudLstar`, `PeakFraction`,
  `BandLocalWindowPatches`, `BandLocalSmoothPatches`, `SkyModelCellPatches`,
  `SkyModelCellMinPatches`, `SkyModelSmoothPatches`, `DustClassFraction`; `DustCloudFraction`
  stays for the clouds' rows. R-044, R-092, R-130 rewritten. `MapViewModel.Layers`,
  `ClassLayer`. V-004 re-measured.
- Known limits: the classes are for wide fields with the band and plain sky both in the frame;
  the band's width is a fixed 10°; a horizon corner darker than the sky model counts as dust.
