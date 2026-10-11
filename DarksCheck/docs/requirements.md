# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status
  `dropped`. Gaps in the numbering are deliberate: each block of ten is one area.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`. `implemented` means a test named
  `R###_...` exists in a test project under `tests/` (`RepoInvariantTests` checks it both ways; it
  reads the sources of the projects it cannot reference).
- Describe observable behaviour, with one concrete example that includes numbers. No adjectives.
- Error behaviour and the exit code are requirements too.

The explanations behind each row (why this way, what was measured) are in `docs/design/` and
`docs/decisions/`; the user documentation of the same behaviour, in Romanian, is `README.md`.
The nine checks of the plan (`docs/plans/2026-10-11-build-darkscheck.md`) map to the areas:
check 1 is the console contract and inventory block below; checks 2 to 8 take the blocks from
R-020; check 9 and the index take the blocks from R-050.

## Console contract and inventory (check 1)

| ID | Status | Requirement |
| --- | --- | --- |
| R-001 | implemented | Every run prints `darkscheck <version>` as its first line and `Copyright (C) 2026 Tudor Carean` as its second, for `--help`, for a session and on a failed run. The version is the `<Version>` of `Directory.Build.props`; there is no `--version` flag. The banner is written before the arguments are parsed, so no option can suppress or move it. |
| R-002 | implemented | The input is a Siril session folder or a bare folder of darks (ADR 0002). The darks are the frames of its `darks/` subfolder when that exists (empty, it is an error naming it, never a fallback to the root, which holds the stacked results). Without `darks/`, a folder that has `lights/`, `biases/` or `flats/` is a session without darks and exits `1` with a message naming the subfolder found; otherwise the frames directly in the folder are the darks (a library) and no biases or lights are looked for. In a session, the biases are the frames of `biases/` and the lights those of `lights/`, both optional (an empty one counts as absent); `flats/` and everything else are never opened. The frames of a folder are its camera raws (`.nef`, `.cr2`, `.cr3`, `.arw`, `.dng`, `.raf`) when there are any, otherwise its FITS files (`.fit`, `.fits`, `.fts`), sorted by file name ignoring case; the two kinds are never mixed. A missing folder or a folder without frames exits `1` with a message naming the folder. Example: the Smida session of 2026-08-12 reports `darks\ (20 cadre .NEF)`, `biases\ (30 cadre .NEF)` and `lights\ (32 cadre .NEF)`; the library folder `2026-10-10-ISO1600-120s-15.8celsius\darks` reports `. (30 cadre .NEF)` and `(lipsa)` for the other two. |
| R-003 | implemented | The inventory prints one table per non-empty group (`DARKS`, `BIASES`, `LIGHTS`) with a row per frame: its index, file name, time of capture (`HH:mm:ss`), exposure in seconds (one decimal from 1 s up, up to four below), ISO and, for darks and lights, the pause before it; a field the file does not carry prints `-`. The metadata comes from libraw on a raw (shutter, ISO, timestamp, camera) and from the header on a FITS (`EXPTIME`, `ISOSPEED`, `DATE-OBS`, `INSTRUME`). The session lines give the camera and, per group, the dominant exposure and ISO with `(toate cadrele)` or `(17 din 20 cadre; expuneri sau ISO diferite)`; the `pauza` column exists only in the tables of the darks and the lights. A frame that cannot be read, or a `.fit` that does not start with `SIMPLE`, prints `eroare: <message>` in its row and an `ATENTIE` line in the header with the count, and the run exits `1`; the other frames are still listed. Example: on the Smida session row 1 of DARKS reads `DSC_0034.NEF          00:48:53     119.2    400       -` and row 2 ends with `1 s`. |
| R-004 | implemented | `--help`, `-h` or no arguments prints the usage, the GPLv3 warranty disclaimer (`RunReport.WarrantyLines`) and a pointer to `LICENSE`, and exits `0`. An unknown option, an empty argument or a second folder exits `1` with a message naming the argument. `--inventory` is the metadata-only run; until the analysis of the frames exists, a run without options is the same. |
| R-005 | implemented | The console formats and parses numbers with `.` as the decimal separator whatever the Windows culture (`<InvariantGlobalization>` in `darkscheck.runtimeconfig.json`: `0.5`, never `0,5`). `DarksCheck.Tests` runs under the same switch, so the golden texts compare against the console's formatting, not the test machine's culture. |
| R-006 | implemented | The cadence of the darks and of the lights (`Th.PauseSeconds` = 5): the start of the first timed frame, the end of the last (start plus exposure), the span between them, the count of timed frames, the median interval between two consecutive starts, and the pauses: per frame, the seconds between the end of the previous timed frame and its start; a pause above 5 s is a gap, and the line says how many there are and the longest with the frame it precedes (`1 pauza peste 5 s (241 s inainte de cadrul 4)`, `fara pauze peste 5 s`). A frame without a time or an exposure is left out and gets no pause, as does the frame after it. Example: three darks of 119.2 s started 120 s apart read `3 cadre la 120.0 s; fara pauze peste 5 s`, a span of 359.2 s and pauses of 0.8 s, printed `1 s`. |
| R-007 | implemented | The match of check 1, two verdict lines whose first word is `OK`, `ATENTIE` or `NEMASURAT`. `potrivire`: the dominant exposure and ISO of the darks against the lights'; `OK` when the exposures are within `Th.ExposureTolerance` (2 %) of each other and the ISO is the same, `ATENTIE` naming both settings otherwise, `NEMASURAT` without lights or when either side lacks the exposure or the ISO. `bias`: `OK` with the exposure when the dominant bias exposure is at most `Th.BiasMaxExposureSeconds` (1 s), `ATENTIE` above it, `(lipsa)` without biases. The dominant setting of a group is the most frequent (exposure within the tolerance, ISO) among its readable frames. Example: Smida darks 119.2 s at ISO 400 against lights 119.2 s at ISO 400 read `OK: dark-urile au expunerea (119.2 s) si ISO-ul (400) lights-urilor`; the 2026-10-10 library darks (120.2 s, ISO 1600) against those lights would read `ATENTIE: dark-uri de 120.2 s la ISO 1600, lights de 119.2 s la ISO 400`. |

## Reading frames

| ID | Status | Requirement |
| --- | --- | --- |
| R-008 | implemented | A camera raw is dispatched by extension to libraw and read undebayered: the visible CFA mosaic at the original ADU scale, the exact white and black levels (the median of the four channel blacks), the colour order from `filters` and `cdesc`, and the shot metadata. The metadata-only load (`LoadInfo`) returns the same shot fields as the full load, without pixels. No field of `RawReader` is a libraw type, so a FITS run never loads the libraw assembly; when the assembly or its native DLLs are missing a raw run exits `1` with a message naming what is missing. A FITS never goes to libraw. Example: `DSC_0034.NEF` of the Smida session reads as a 6016 x 4016 mosaic on the scale 150 .. 4095 ADU (`libraw (exact)`), with 119.2 s at ISO 400 as its metadata. |
| R-012 | accepted | A FITS frame is read by the hand-rolled reader copied from FlatCheck through LightsCheck (ADR 0003): BITPIX 8, 16, 32, -32, -64 with `BZERO` / `BSCALE`, a colour cube averaged over its planes, `BAYERPAT` shifted by `XBAYROFF` / `YBAYROFF` and by one row under `ROWORDER = 'BOTTOM-UP'` on an even height. A corrupt FITS fails with `InvalidDataException` naming the problem. Its tests came with the copy and carry no ID. |

## The repository

| ID | Status | Requirement |
| --- | --- | --- |
| R-009 | implemented | Every source file (`.cs`, `.xaml`, `.py`) under `src/`, `tests/` and `Icon/` starts with the SPDX line and the GPLv3 notice (`LicenseHeaderTests`); every script carries the SPDX line. |
| R-010 | implemented | Every NuGet package the core links is listed exactly once in `Directory.Packages.props` with a "why", has a heading in `THIRD-PARTY-NOTICES.md` and an entry in `ThirdParty.cs` (`ThirdPartyTests` binds the three); LibRaw is taken under LGPL-2.1 and its copyright line is the one of its COPYRIGHT file (ADR 0003). |
| R-011 | implemented | The version and the copyright come from `Directory.Build.props` alone (`VersionTests`): `VersionInfo.Value` is its `<Version>`, `VersionInfo.Copyright` its `<Copyright>`, and no source file hard-codes either. |
