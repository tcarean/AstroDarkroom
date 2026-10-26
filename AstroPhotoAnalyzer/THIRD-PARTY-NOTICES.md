# Third-party components

AstroPhotoAnalyzer is distributed under the **GNU General Public License v3 or later** (see
[LICENSE](LICENSE)). This file lists the third-party code it links, the licence of each component
and the reason it is compatible with GPLv3.

It is not a formality: ImageSharp's licence is conditional, and it has to be shown that the
condition is met. A package added without an entry here is a package whose compatibility nobody
has checked.

Written by hand, and **not** third-party code: the FITS reader, the WCS reader, every
measurement, every threshold and all the text of the recommendations. Besides code, the tool
ships one set of **data** written by others, the Lynds catalogue; it is treated here as a
component, with the same kind of notice.

---

## Lynds' Catalogue of Dark Nebulae

**Role.** The catalogue of dust clouds by which the tool recognizes, in a plate-solved FITS, which
dark nebulae are in the frame and where: 1787 clouds with their centre, area and opacity class,
plus the Barnard numbers they are also known by (B78 for LDN 42, for instance). The background's
verdict does not depend on it (ADR 0026); it names the clouds: their rows in the report and their
circles on the map (ADR 0025, R-019, R-044). It lives in
`AstroPhotoAnalyzer.Core/Input/ldn.txt`, a 52 KB text resource read by `DarkNebulaCatalogue`.

**Source.** Lynds, B. T., *Catalogue of Dark Nebulae*, Astrophysical Journal Supplement 7, 1
(1962). The file shipped is the machine-readable version of 1979, prepared by NASA/ADC from the
author's copy (1791 entries, with the Barnard identifications added from the table of the
original publication), distributed by CDS Strasbourg through VizieR as **VII/7A**
(<https://cdsarc.cds.unistra.fr/viz-bin/cat/VII/7A>), taken on 2026-10-02. The original columns
are kept (the centre in B1950, the area, the opacity, the Barnard numbers), not the ICRS columns
VizieR computes; the tool precesses the centres to J2000 itself. The four entries without an LDN
number at the end of the table are left out.

**Licence.** No formal licence comes with the catalogue: it is a table of facts from a scientific
publication of 1962, distributed by the astronomical data centres for 47 years. VizieR's rules
(<https://cds.unistra.fr/vizier-org/licences_vizier.html>) require a citation of the author and
of the original publication, and appreciate a mention of the VizieR service
(DOI 10.26093/cds/vizier); their note on the CC-BY-NC licence concerns the tables of the AAS
journals in the `J/` series (J/ApJ, J/ApJS, J/AJ), to which VII/7A does not belong. The
catalogue's ReadMe holds no copyright clause.

**Compatibility.** Data are not code and are not bound by the program's licence: GPLv3 covers the
program, and a table of positions, areas and classes is a set of facts, which US copyright law
does not protect (Feist, 1991); the European *sui generis* database right lasts 15 years from the
making of the database, and the compilation dates from 1979. What remains is the duty to cite,
which the tool fulfils here, in `ThirdParty.cs` (what **About** shows) and in the README. A
derivative of AstroPhotoAnalyzer takes on the same duty, nothing more.

**One thing to know.** The catalogue gives a cloud's area, not its outline: the tool treats it as
a disc of the same area around the centre (ADR 0025), and the row of an irregular cloud mixes its
patches with the sky around it.

Lynds B. T., 1962; machine-readable version NASA/ADC, 1979; distributed by CDS, Strasbourg.

- Catalogue: <https://cdsarc.cds.unistra.fr/viz-bin/cat/VII/7A>
- Paper: <https://ui.adsabs.harvard.edu/abs/1962ApJS....7....1L>

---

## SixLabors.ImageSharp

**Role.** It decodes TIFF and PNG (8 and 16 bits per channel, RGB and mono, with the embedded ICC
profile) and JPEG, that is, the pixels every measurement is made on; it encodes and decodes the
JPEGs of the compression table (q100 / q95 / q90, 4:4:4), and encodes the 8-bit LZW TIFF whose
size the table gives. Unlike its role in FlatCheck, here the package is **not** peripheral: the
figures of the JPEG table are its encoder's, and what the tool sees in a TIFF is what the package
decoded.

**Why not written by hand.** An honest TIFF reader means strips and tiles, LZW, Deflate,
PackBits, the horizontal predictor, planar and chunky layouts, 8 and 16 bits, big and little
endian, plus PNG with Adam7, and a JPEG encoder with DCT, Huffman coding and controlled
subsampling. That is weeks of work to reproduce, with bugs of its own, something that exists and
is tested on millions of files. The FITS reader stays hand-written, because there it really is
simple: 80-byte cards in blocks of 2880 bytes, then big-endian data.

**Licence.** Six Labors Split License, version 1.0 (June 2022).

**Compatibility.** It is not a single restrictive licence but a **conditional dual** one. Its text
grants the Apache 2.0 terms when:

> "You are consuming the Work in for use in software licensed under an Open Source or
> Source Available license."

AstroPhotoAnalyzer is under GPLv3, so it is software under an open source licence and meets
this condition. The commercial licence is required only in the other cases, in essence
proprietary use by an entity with an annual gross revenue of at least 1M USD.

And Apache 2.0 code may go into GPLv3 work, in that direction only, as the Apache Software
Foundation states explicitly:

> "Apache 2 software can therefore be included in GPLv3 projects, because the GPLv3 license
> accepts our software into GPLv3 works."

AstroPhotoAnalyzer's licence needs no change.

**Two things to preserve.**

1. **The version is pinned**, in `Directory.Packages.props`. The licence's text says "Version
   1.0, June 2022" and does not say which version of the licence governs which release. The
   grant comes with the version taken, so the pin is legal hygiene, not only reproducibility.
   The pinned version is **3.1.11**, not 4.x: from 4.0.0 the package brings an MSBuild task that
   validates a licence key at build time, a warning in Debug and an **error in Release** without
   `sixlabors.lic` or `$(SixLaborsLicenseKey)`. Six Labors gives the key free of charge to open
   source projects, but on request, and anyone who clones the repository would need their own to
   publish. 3.1.x has exactly the same licence text and no task (ADR 0009). The security
   advisories published against 3.1.11, and why the pin stays despite them, are in ADR 0036.
2. **The condition stays met downstream.** GPLv3 requires every derivative to stay under GPLv3,
   that is, under an open source licence still, so anyone who takes up AstroPhotoAnalyzer meets
   the same condition in turn.

**One thing to know about decoding.** 3.1.x never applies the ICC profile when it decodes: the
pixels come out exactly as they are in the file, and the profile is available separately in the
metadata. That is the behaviour wanted: the tool measures what Siril wrote, not a colour
conversion of it.

Copyright (c) Six Labors.

- Project: <https://github.com/SixLabors/ImageSharp>
- Licence: <https://github.com/SixLabors/ImageSharp/blob/main/LICENSE>

---

## Wacton.Unicolour

**Role.** It evaluates the tables of a printer's ICC profile. The tool gives it the device's
black and the relative colorimetric intent, through the table from the device (v2
`lut8`/`lut16`, v4 `lutAToB`), and gets back that black's colour against the paper's white, from
which comes the paper's Dmax on that printer (`--icc`, the window's **From ICC profile...**
button). For the out-of-gamut figure of the **Curves** tab it also takes the image's colours to
the printer and back through the profile's relative colorimetric tables (R-144). It never
changes the image itself.

**Why not written by hand.** A hand-written reader for the lab's two profiles gave the same
figures to the last decimal, but knew a single kind of table; the second kind (`lutAToB`, in the
v4 profiles) means parametric curves, matrices and a colour lookup table, each of them optional.
The package has all of them and is tested on real profiles.

**Licence.** MIT.

**Compatibility.** MIT is a permissive licence, compatible with GPLv3 with no condition beyond
keeping the copyright notice and the licence text, reproduced below. The package is pure managed
code, with no dependencies and no native components, so the console stays a single file that can
be copied.

**One thing to know.** On a file that is not a profile, the package does not throw: it notes an
error and goes on computing "naively" (L\* 100). The tool checks the header before and the error
after. The perceptual intent gives the same black (L\* 3.14) for every profile; the tool always
asks for the relative colorimetric intent.

The licence's text, which the licence requires to be kept in every copy:

> MIT License
>
> Copyright (c) 2022-2026 William Acton
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

- Project: <https://github.com/waacton/Unicolour>
- Licence: <https://github.com/waacton/Unicolour/blob/main/LICENSE>

---

## Test components

Used only to build and run the tests, so they are not distributed with the tool.

| package | licence |
|---|---|
| xunit | Apache-2.0 |
| xunit.runner.visualstudio | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| coverlet.collector | MIT |

---

## How to add an entry

See the kit's rule "Packages", in `.claude/rules/kit/process.md`, the rule "Package notices" in
[CLAUDE.md](CLAUDE.md), and ADR 0009, in `docs/decisions/`. In short: a package is discussed
before it is added, not after; its compatibility with GPLv3 is shown, not assumed; and the reason
is written here, so that the next reader does not have to find it again.
