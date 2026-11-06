# 0025. The dust is the catalogued dark nebulae over the image's WCS, the sky is what lies outside them, and the lift is the lowest that puts the dust on paper black

Date: 2026-10-02
Status: accepted 2026-10-02; the dust and the sky (decision 3) and the map's layers superseded by 0026; the frame rule without a WCS (decision 4) superseded by 0033; the preview's classes per pixel superseded by 0082

## Context

The print-background verdict (R-041, R-043, R-092) counts pixels under paper black and
recommends the lowest of three targets (0.15, the parameter's 0.19, 0.20) under 2 %: on the
reference image 0.19, although 0.16 already passes. The owner's question is different: the
minimum lift at which the darkest extended structure, the dust, lands on paper black without
going under it (below it the dust prints as the paper's black and the sky above it comes too
close; above the minimum the darkness the owner likes is lost), and the symmetric answer on an
image that is too bright, the minimum usable level downwards.

A parked branch (`claude/Recommend-the-lift-that-puts-the-dust-on-paper-black`, unmerged) made
the dust the darkest 1 % of 32 px patches, or the darkest 10 % of a rectangle the owner names.
Its patches stand, and it showed that the fraction is a choice, not a measurement: on the
reference PNG 1 % gives 0.17, the Great Rift 0.14, the cloud cores 0.26. The owner asked for the
tool to know on its own where the clouds are and what the sky is, from a plate-solved FITS and a
catalogue of dark nebulae, rather than ask for a rectangle.

### Measured on 2026-10-02

On the owner's plate-solved, stretched FITS
`E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida\Processed_With_1mmPTFE_Flats\CropAndPlateSolve\RBF0.5\preview\starless_rbf0.5_stretch0.17.fit`
(5837 x 3867, three float planes, starless, VeraLux stretch, background 0.1716, `ROWORDER
BOTTOM-UP`), the only image used; an earlier pass over a linear stack of the same resolution was
a different crop and is discarded. Its WCS is Siril 1.4.4's: `CTYPE RA---TAN-SIP` /
`DEC--TAN-SIP`, `CRPIX` 2919.5 / 1934.5, `CRVAL` 271.59 / -10.83, `CDELT` with a `PC` matrix (no
`CD`, no `CROTA`), `LONPOLE` 180, `EQUINOX` 2000, SIP of order 3. Scale 41.7"/px; the frame spans
RA 237 to 304, Dec -33 to +13, galactic latitude -30 to +39. Measured with astropy and numpy in
the session's scratchpad, in the way the core will compute: patches of 32 x 32 image pixels on
the stride-4 subsample, each the L\* of its mean linear luminance (R-016); paper black L\* 8.99
(baryta, Dmax 2.0).

Catalogues from VizieR: Lynds 1962 (`VII/7A`, 1791 clouds: B1950 centre, area in square degrees,
opacity class 1 to 6, Barnard cross-identifications) and Barnard 1927 (`VII/220A`, 349 objects,
292 with a diameter). A cloud is a disc of the catalogue's area around its centre; a patch is
the cloud's when its centre lies within the radius (great-circle distance).

**What the catalogue finds in the frame.** 406 LDN clouds have at least one patch, 116 at least
9, 12 at least 100; together they cover 18.2 % of the patches (opacity 4 and up: 3.2 %). 65
Barnard objects with a diameter, mostly small; the large Rift clouds carry no Barnard number.
The L\* of the patches inside each cloud (P10 = the 10th percentile of the cloud's patches):

| LDN | where | area deg² | opacity | patches | min | P10 | median | Barnard |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 621 | Aquila Rift, top of the frame | 45.0 | 3 | 382 | 3.9 | 5.7 | 8.2 | |
| 569 | Aquila Rift | 31.0 | 2 | 246 | 5.2 | 9.7 | 16.2 | |
| 34 | the Pipe region, south-east of the core | 25.0 | 3 | 211 | 2.0 | 5.1 | 12.7 | |
| 559 | Aquila Rift | 17.0 | 4 | 130 | 3.5 | 4.8 | 6.4 | |
| 572 | Aquila Rift | 14.0 | 4 | 110 | 3.5 | 4.7 | 6.5 | |
| 1769 | the Pipe, stem | 12.5 | 4 | 104 | 2.0 | 3.1 | 6.2 | 275 278 |
| 393 | Serpens, east of the band | 20.0 | 2 | 149 | 12.8 | 14.1 | 15.2 | |
| 42 | the Pipe, bowl | 1.45 | 4 | 12 | 0.7 | 0.9 | 1.4 | 78 |
| 1793 | near the Pipe | 1.08 | 2 | 9 | 2.2 | 4.7 | 7.9 | |
| 616 / 628 | Aquila Rift, small | 2.4 / 2.0 | 2 / 4 | 21 / 17 | 4.7 / 5.4 | 5.5 / 5.6 | 6.4 / 6.2 | |

The darkest sizeable clouds are the Pipe Nebula (B59, B65-67, B78 are LDN 1769, 42 and their
neighbours); the Rift is LDN 621, 559, 572, 616, 628. The opacity class does not predict the
measured darkness: the median P10 per class is 23.1, 18.2, 14.9, 12.9, 11.7, 11.9 for classes
1 to 6, with the darkest cloud of all in class 4.

**The sky.** Frame median 18.01. Outside the catalogued discs 18.08; outside them and above
galactic latitude 10° or 15°: 17.64, 18.10. On this frame the band and the plain sky sit at the
same level once the clouds are out; the choice of a latitude cut changes the sky by half an L\*.

**Candidate definitions of the dust and the lift each asks for** (gamma simulation as in
R-043, `gamma = ln(target) / ln(0.1716)` on every channel, the patches recomputed; the exact
target by bisection between 0.05 and 0.60; the sky is the median outside the discs at that
target):

| the dust is | as-is | exact | sky then |
| --- | --- | --- | --- |
| frame P1 (the branch's 1 %) | 5.81 | 0.211 | 22.7 |
| darkest cloud with 9+ patches (LDN 42, B78) | 0.94 | 0.392 | 42.4 |
| darkest cloud with 100+ patches (LDN 1769) | 3.06 | 0.277 | 30.1 |
| largest cloud (LDN 621) | 5.65 | 0.213 | 23.0 |
| cloud with most patches under black (LDN 621) | 5.65 | 0.213 | 23.0 |
| **P10 of all catalogued-cloud patches together** | **7.63** | **0.187** | **19.9** |
| P1 of all catalogued-cloud patches | 3.85 | 0.254 | 27.5 |
| P10 of opacity 4+ cloud patches | 4.68 | 0.233 | 25.2 |

The P10 over all catalogued clouds is the only definition that does not jump with a size cut (9
or 100 patches pick LDN 42 or 1769 and give 0.39 or 0.28), and on this stretch it asks for the
smallest lift of all: 0.187, grid row 0.19. The grid with it:

| target | gamma | L\* dust | L\* sky | pure black (% patches) | under L\* 15 | |
| --- | --- | --- | --- | --- | --- | --- |
| 0.10 | 1.306 | 3.0 | 9.2 | 47.6 | 86.2 | |
| 0.14 | 1.115 | 5.2 | 14.3 | 9.4 | 61.3 | |
| 0.17 | 1.005 | 7.5 | 17.9 | 4.0 | 24.0 | |
| as is (0.1716) | 1 | 7.6 | 18.1 | 3.9 | 22.4 | |
| 0.18 | 0.973 | 8.4 | 19.1 | 3.0 | 15.2 | |
| 0.19 | 0.942 | 9.3 | 20.3 | 2.2 | 11.0 | print-safe, recommended |
| 0.20 | 0.913 | 10.3 | 21.4 | 1.6 | 8.6 | print-safe |

Per cloud, the exact target that puts its own P10 on paper black: LDN 42 0.392, 1769 0.277,
1793 0.236, 572 0.230, 559 0.230, 34 0.225, 616 0.216, 563 0.215, 628 0.213, 621 0.213, 69
0.204, 1675 0.202. At the recommended 0.19 every one of these still prints under paper black;
the rows say so.

**Sensitivity.** Disc radius x0.7, x1.0, x1.4: the union covers 10.4, 18.2, 27.4 % of the
patches, the dust reads 6.76, 7.63, 8.46 and the exact target 0.197, 0.187, 0.177: a tenth of
the lift per 40 % of radius, so the radius must be the catalogue's, not a tuning. SIP against
plain TAN at the patch centres: at most 6.3 px, median 1.3, under one 32 px patch; the SIP
terms can be ignored. Dmax 1.8, 1.9, 2.0, 2.1, 2.2, 2.4: exact target 0.230, 0.208, 0.187,
0.166, 0.143, 0.085, about 0.02 per 0.1 of Dmax.

**What the stretch does.** This file is a real re-stretch to 0.17, and its dust sits at 7.6,
under paper black; the parked branch's gamma simulation on the reference PNG (background
0.103) predicted the dust above black from 0.14 to 0.17. A VeraLux stretch to a given
background keeps the shadows lower than a gamma curve from 0.103 does. The simulation stays a
simulation (ADR 0008); the report keeps saying so, and the exact target is the gamma curve's.

## Decision

The owner agreed on 2026-10-02 to points 1 to 4 as proposed, asked for the windows of points 3, 4
and 6, and took the recommendation on the four details put to them: the report lists the darkest
`DustCloudReportRows` (15) clouds, the map labels the darkest 15 and lists every cloud in its
check boxes, `--wcs` stays, and the plate-solved FITS becomes the second validation image.

1. **Catalogue.** Lynds' Catalogue of Dark Nebulae (`VII/7A`, the 1979 ADC machine-readable
   version of Lynds 1962, ApJS 7, 1), embedded in `AstroPhotoAnalyzer.Core` as a text resource
   of 1791 rows with the original data: LDN number, B1950 centre, area, opacity, Barnard
   cross-identifications. The core precesses B1950 to J2000 (the FK4 to FK5 rotation matrix,
   without the E-terms: under an arcminute, nothing at 42"/px). Not the VizieR-computed ICRS
   columns, so that what is embedded is the published table. Notices: a section in
   `THIRD-PARTY-NOTICES.md` (the data are the facts of a 1962 publication, distributed by
   NASA/ADC since 1979 and by CDS; VizieR asks for the citation of Lynds 1962 and an
   acknowledgement of CDS; VizieR's CC-BY-NC note applies to tables of AAS journals in the `J/`
   series, which `VII/7A` is not; a factual table carries no copyright in the US and the EU
   database right of a 1979 compilation has lapsed), an entry in `ThirdParty.cs` ("Despre"
   shows it), `ThirdPartyTests` extended to data components, a row in the README's table. The
   Barnard catalogue is not embedded: its numbers come with the LDN rows, and the Rift clouds
   have none.
2. **Shape.** A disc of the catalogue's area; a 32 px patch is the cloud's when its centre is
   within the great-circle radius. The radius is the catalogue's; it moves the verdict.
3. **The dust and the sky.** The dust is the `DustCloudFraction` (10 %) percentile of the patches
   of all catalogued clouds in the frame together. Every cloud with at least
   `DustCloudMinPatches` (9) patches is also a row of its own, with its L\* and the exact target
   that puts it on paper black, darkest first, so the owner reads which clouds stay under black
   at the recommended target. The sky is the median of the patches outside every catalogued
   disc (the galactic band is not excluded: on the measured frame a latitude cut moves the sky by
   half an L\* either way and adds a threshold nobody can defend). The recommendation is the
   lowest target of the grid 0.10 to 0.20 in steps of 0.01, the as-is row included, at which the
   dust is at or above paper black, downwards as readily as upwards; with the exact target by
   bisection and the Dmax from which the image passes as is. The `Because` names the dust as
   "10 % din dalele norilor catalogati (N nori, M dale)", the darkest named clouds with their
   L\*, the sky, the level and the Dmax. In the window, under the simulation table, a button
   opens a maximised window with the image fitted to it, the dust patches and the sky patches
   coloured apart, the clouds with at least 9 patches circled and labelled, and check boxes for
   each layer and each cloud: a visualisation, computing nothing.
4. **Without a WCS, or without a catalogued cloud in the frame.** The dust is the darkest
   `DustAreaFraction` (1 %) of the frame's patches and the sky the frame median; under the
   simulation table, in the report and in the window, one sentence states the definition in use
   ("praful: 10 % din dalele norilor catalogati; cerul: mediana dalelor din afara norilor" or
   "praful: cele mai intunecate 1 % din dale; cerul: mediana dalelor"), and without a WCS the
   window adds, in red, that the file carries no WCS and no astrometric identification was
   made; its map window keeps the dust and sky layers, with the labels box disabled. A TIFF or
   PNG may borrow the WCS of a FITS of the same dimensions through `--wcs <file.fit>` (the
   window: a field under the image path), because the print file is a PNG and the WCS lives in
   the FITS.
5. **Dmax.** Every `Because` names the Dmax in use; Anexa B adds, beside the exact target, the
   exact targets at Dmax -0.1 and +0.1 and the Dmax from which the as-is image passes, since
   the target moves 0.02 per 0.1 of Dmax and a paper's Dmax on a given printer is rarely known
   better than that.
6. **Per-row preview.** Every row of the simulation table, the as-is row included, has a button
   that opens a maximised window with the image lifted to that row's target (the gamma on a
   downsample, as the Preview tab's tile), the pixels under paper black and the pixels under
   L\* `LowSeparationLstar` (15) coloured apart, each layer behind a check box. The core
   supplies the lifted image and the per-pixel classes; the window only paints them.

## Alternatives rejected

- **The darkest cloud, with any size cut.** The cut chooses the answer: 9 or 100 patches give
  0.39 or 0.28 on the measured frame.
- **The largest cloud, or the cloud with the most black.** LDN 621 both times here, but a
  frame-dependent pick that changes name from one crop to the next.
- **The Barnard catalogue alone.** Public domain (1927) and the names the owner knows, but 65
  objects in the frame, small ones; the Rift has none.
- **The sky outside the galactic band.** Measured 17.64 at 10° against 18.08 outside the discs
  alone; a latitude threshold with no natural value.
- **A cloud chosen by the owner (`--dust LDN34`).** The rows show each cloud's target, which is
  the same information; a flag can come later if reading is not enough.
- **Refuse the verdict without a WCS.** TIFF and PNG are the files that go to print.
- **Embed VizieR's ICRS columns.** They are CDS additions; precession is fifteen lines.
- **Honour SIP.** Under a patch at the frame's edge; the reader would double in size.

## Consequences

- `Input/`: a WCS reader (TAN with `CD`, `PC` + `CDELT`, or `CDELT` + `CROTA2`; SIP suffix
  accepted and ignored), applied in FITS pixel convention before the `ROWORDER` flip, carried on
  `SourceInfo`; the catalogue resource and its reader; precession. `Analysis/`: `DustAnalyzer`, `PrintPreview`.
  `Thresholds`: `DustPatchPx`, `DustCloudFraction`, `DustCloudMinPatches`, `DustAreaFraction`,
  `ExactTargetLo`/`Hi`, `DmaxSensitivityStep`, `DustCloudReportRows`; `PaperBlackOkPct` and
  `PaperBlackWarnPct` go. Requirements R-041, R-043, R-092, R-113, R-122 rewritten; new rows for
  the WCS, the catalogue, the dust and the sky, `--wcs`, the two windows. The golden files
  change. `/validate` adds a case on the plate-solved FITS (a copy in `testdata/large/`) and
  re-measures V-001 to V-003, which now run the no-WCS rule.
- Known limits: the simulation's gamma curve is more generous in the shadows than a VeraLux
  stretch to the same background; a catalogued disc is a disc, so a cloud's row mixes its
  patches with the sky around an irregular cloud; the verdict moves with the disc radius, which
  is the catalogue's.
