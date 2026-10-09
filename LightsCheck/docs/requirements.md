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

## Repository and legal

| ID | Status | Requirement |
| --- | --- | --- |
| R-009 | implemented | Every source file (`.cs`, `.xaml`, `.py`) under the project folders and `prototype/` starts with the GPL notice (SPDX line, copyright, the three paragraphs), and `LICENSE` is the verbatim GPL v3 text. |
| R-010 | implemented | Every NuGet package the core links is claimed by exactly one component of `ThirdParty.Components`, every claimed package is linked, and every component has a heading in `THIRD-PARTY-NOTICES.md`. LibRaw is taken under LGPL-2.1 and carries LibRaw LLC's copyright line. |
| R-011 | implemented | The version and the copyright come from `Directory.Build.props` alone (no csproj overrides them); the version is a three-part number without build metadata. |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
