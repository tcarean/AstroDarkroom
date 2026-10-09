# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status
  `dropped`. Gaps in the numbering are deliberate: each block of ten is one area.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`. `implemented` means a test named
  `R###_...` exists in `tests/LightsCheck.Tests` (`RepoInvariantTests` checks it both ways).
- Describe observable behaviour, with one concrete example that includes numbers. No adjectives.
- Error behaviour and the exit code are requirements too.

The explanations behind each row (why this way, what was measured) are in `docs/design/` and
`docs/decisions/`; the user documentation of the same behaviour, in Romanian, is `README.md`.

## Console contract

| ID | Status | Requirement |
| --- | --- | --- |
| R-001 | implemented | Every run prints `lightscheck <version>` as its first line and `Copyright (C) 2026 Tudor Carean` as its second, for `--help`, for a session and on a failed run. The version is the `<Version>` of `Directory.Build.props`; there is no `--version` flag. The banner is written before the arguments are parsed, so no option can suppress or move it. |
| R-002 | implemented | The input is a Siril session folder (ADR 0004). The lights are the frames of its `lights/` subfolder when that exists (empty, it is an error naming it, never a fallback to the root, which holds the stacked results); without `lights/`, the frames directly in the folder. The biases are the frames of `biases/`, optional. The frames of a folder are its camera raws (`.nef`, `.cr2`, `.cr3`, `.arw`, `.dng`, `.raf`) when there are any, otherwise its FITS files (`.fit`, `.fits`, `.fts`), sorted by file name ignoring case; the two kinds are never mixed. A missing folder or a folder without frames exits `1` with a message naming the folder. Example: a folder with `lights/DSC_0001.NEF` to `DSC_0087.NEF` and `biases/` with 30 raws reports `lights\ (87 cadre .NEF)` and `biases\ (30 cadre .NEF)`. |
| R-003 | implemented | For every light the console prints a row with its index, file name, time of capture (`HH:mm:ss`), exposure in seconds (one decimal), ISO, f-number, focal length and the pause before it; a field the file does not carry prints `-`. The metadata comes from libraw on a raw (shutter, ISO, timestamp, focal length, aperture, camera, Nikon lens ID) and from the header on a FITS (`EXPTIME`, `ISOSPEED`, `DATE-OBS`, `FOCALLEN`, `INSTRUME`, `XPIXSZ`). A frame that cannot be read, or a `.fit` that does not start with `SIMPLE`, prints `eroare: <message>` in its row and an `ATENTIE` line in the header, and the run exits `1`; the other frames are still listed. Example: on the Veil session of 2026-10-06 row 1 reads `DSC_0001.NEF  23:35:03  120.2  1600  2.8  135 mm`. |
| R-004 | implemented | `--help`, `-h` or no arguments prints the usage, the GPLv3 warranty disclaimer (`RunReport.WarrantyLines`) and a pointer to `LICENSE`, and exits `0`. An unknown option or a second folder exits `1` with a message naming the argument. |
| R-005 | implemented | The console formats and parses numbers with `.` as the decimal separator whatever the Windows culture (`<InvariantGlobalization>` in `lightscheck.runtimeconfig.json`: `0.5`, never `0,5`). `LightsCheck.Tests` runs under the same switch, so the golden texts compare against the console's formatting, not the test machine's culture. |
| R-006 | implemented | The cadence of the session: the start of the first timed frame, the end of the last (start plus exposure), the span between them, the integration (the sum of the exposures), the duty (integration over span, in percent) and, per frame, the pause in seconds between the end of the previous timed frame and its start. A frame without a time or an exposure is left out and gets no pause, as does the frame after it. Example: three frames of 120 s started 150 s apart give a span of 420 s, an integration of 360 s, a duty of 86 % and pauses of 30 s. |

## Reading frames

| ID | Status | Requirement |
| --- | --- | --- |
| R-007 | implemented | A camera raw is dispatched by extension to libraw and read undebayered: the visible CFA mosaic at the original ADU scale, the exact white and black levels (the median of the four channel blacks), the colour order from `filters` and `cdesc`, and the shot metadata. The metadata-only load (`LoadInfo`) returns the same shot fields as the full load, without pixels. No field of `RawReader` is a libraw type, so a FITS run never loads the libraw assembly; when the assembly or its native DLLs are missing a raw run exits `1` with a message naming what is missing. A FITS never goes to libraw. |
| R-008 | accepted | A FITS light is read by the hand-rolled reader copied from FlatCheck (ADR 0003): BITPIX 8, 16, 32, -32, -64 with `BZERO` / `BSCALE`, a colour cube averaged over its planes, `BAYERPAT` shifted by `XBAYROFF` / `YBAYROFF` and by one row under `ROWORDER = 'BOTTOM-UP'` on an even height. A corrupt FITS fails with `InvalidDataException` naming the problem. Its tests came with the copy and carry no ID. |

## Measuring one frame

The method is the Python prototype's (`prototype/analyze.py`); every constant is in
`Thresholds.cs` (`Th`), the reasons in `docs/design/frame-analysis.md` (ADR 0006). Figures are
in pixels of the frame and ADU above black.

| ID | Status | Requirement |
| --- | --- | --- |
| R-012 | implemented | For each of the four CFA positions, named from the file's colour order (`R`, `G1`, `G2`, `B`; `CFA[y,x]` when unknown): the median above black read at every 4th pixel of the position, the count of pixels within `Th.SaturationMargin` of the top of the scale (1 ADU on an integer scale; 1/4096 of the scale on a float 0..1 frame, as Siril writes a calibrated light), and the minimum. The brightest channel is the one with the highest median. Example: a synthetic RGGB frame with backgrounds 30 / 150 / 170 reads `R 30  G1 150  G2 150  B 170`. |
| R-013 | implemented | The green channel at full resolution (green positions kept, the others the mean of their four green neighbours; the raster as it is when the colour order is unknown), its background as the medians of 128 px tiles read at every 4th pixel and stretched back bilinearly with the grid's corners on the raster's corners (a frame smaller than a tile is one tile), and the noise as 1.4826 x the median absolute deviation of the residual in the central 2000 x 2000 window read at every 3rd pixel. Reported: the median, minimum and maximum of the tiles, the mean of the central 4 x 4 tiles and of each 3 x 3 corner. Example: a flat 150 ADU green background with 12 ADU noise reads median 150 and a sigma between 7 and 13 (the interpolation halves the noise of half the pixels). |
| R-014 | implemented | Stars are the 4-connected components of the residual smoothed by a Gaussian of 1 px that exceed `Th.DetectionSigmas` (5) x the smoothed image's sigma, with an area of 5 to 400 px. Counted: all, the bright ones (peak above 50 sigma), and the saturated components (a raw pixel within 1 ADU of the top). The 400 brightest by flux among those peaking above 20 sigma and below 0.8 of the scale get second moments in a circular window of radius 6 px around a centroid refined 3 times, negative values clipped: FWHM = 2.3548 sqrt((mxx+myy)/2), elongation = sqrt(l1/l2), angle = 0.5 atan2(2 mxy, mxx-myy) in degrees from the x axis; kept when the FWHM is in 1..15 px. Reported: the median and 90th percentile of FWHM and elongation, the fraction elongated above 1.3, the mean angle and coherence of the stars above 1.2 (doubled-angle vector mean; at least 6 voters), the median FWHM and elongation per radial zone (centre under 0.2765 of the half-diagonal, mid under 0.6082, edge beyond; at least 5 stars), and the median halo (annulus 5-10 px over core 3 px) of the 40 brightest measured stars. Example: 60 synthetic round stars of FWHM 3.0 read a median FWHM within 2.8..3.6, elongation under 1.12 and no angle; the same stars at elongation 1.8 and 80 degrees read elongation within 1.6..1.95, angle within 75..85 and coherence above 0.95. |
| R-015 | implemented | A component of more than 250 px whose bounding-box diagonal exceeds 80 px and whose area is under 0.25 of the box plus 60 px is a trail (satellite, plane), reported with its length and its top-left corner; it is not a star. Example: a 3 px wide line from (100,100) to (700,400) reads one trail of length 671. |
| R-016 | implemented | The registration list is the 600 brightest star components by flux, saturated included, each with its flux-weighted centroid; a drawn star at (500, 350) is found within 0.3 px. |
| R-017 | implemented | `lightscheck <folder> --frame N` decodes the N-th light (1 = first) and prints its measurements (R-012 to R-016) under `=== CADRU N: <file> ===`, exit `0`; N outside 1..count exits `1` naming the count. A FITS without an ADU scale is read at the full range of its BITPIX (0..65535 for 16 bits, 0..1 for a float frame); a raw at libraw's exact scale. |

## Repository and legal

| ID | Status | Requirement |
| --- | --- | --- |
| R-009 | implemented | Every source file (`.cs`, `.xaml`, `.py`) under the project folders and `prototype/` starts with the GPL notice (SPDX line, copyright, the three paragraphs), and `LICENSE` is the verbatim GPL v3 text. |
| R-010 | implemented | Every NuGet package the core links is claimed by exactly one component of `ThirdParty.Components`, every claimed package is linked, and every component has a heading in `THIRD-PARTY-NOTICES.md`. LibRaw is taken under LGPL-2.1 and carries LibRaw LLC's copyright line. |
| R-011 | implemented | The version and the copyright come from `Directory.Build.props` alone (no csproj overrides them); the version is a three-part number without build metadata. |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
