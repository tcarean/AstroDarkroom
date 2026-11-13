# Fix the next five review findings (CR-006 to CR-010)

## Context

`docs/review/2026-11-13-code-review.md` holds 60 findings (ADR 0100). CR-001 to CR-005 were
fixed and merged on 2026-11-13 (`f9f7178c`). The owner asked for the next five open ones in the
same session: CR-006 to CR-010, the rest of severity 1 (a wrong figure or a wrong verdict). As
before: one branch (`claude/Fix-the-next-five-review-findings`, its worktree made already), one
commit per entry with its requirement, failing test, fix, docs and gate, then a commit that
closes the five entries in the list.

Three of the five were marked "to measure" in the list; the measurements are below. Two needed a
decision, put to the owner on 2026-11-13 with the facts; both answers are in the plan.

## What was measured (2026-11-13)

- **CR-008**, a tile-compressed FITS built as `fpack` and Siril's FITS compression write it
  (an empty primary, then `XTENSION= 'BINTABLE'`, `BITPIX 8`, `NAXIS1 8`, `NAXIS2 1024`,
  `PCOUNT` the heap, `ZIMAGE T`), run through today's code: `FitsReader.Load` gives an image
  8 x 1024 with no error; the structure mode prints a whole report and `VERDICT: REFA`, exit 3;
  `--histeq` writes a picture, exit 0; `--expo` refuses it as not a raw flat, exit 1. So it is
  a wrong verdict on the bytes of a table, not a crash.
- **CR-009**, Adobe's document (the PDF the design note cites, text extracted again): the
  descriptor reads `stCamera:ImageYCenter ... v0 / Dmax. If absent, default to 0.5`, the same
  words as for `ImageXCenter`. The installed files say otherwise: over the 1346 files that
  carry the value on a 3:2 or 4:3 frame its median is 0.5008 (3:2, 518 files) and 0.4994 (4:3,
  828 files), where v0 / Dmax of a centred axis would be 0.333 and 0.375. RawTherapee's
  `rtengine/lcp.cc` multiplies the two centres by the image's width and height and the two
  focal lengths by Dmax, with a comment that the Adobe paper has a bug there. `FocalLengthY /
  FocalLengthX` is 1.0000 in the files, so the focal lengths are both in Dmax, as the code has
  them. Only 65 of the 3531 files carry a centre in their vignette model; none of the owner's
  lens files does.
- **CR-010**, in C#: `StructureAnalyzer` on 918 x 610 (raster 459 x 305) gives an azimuthal
  asymmetry of 4.216432 points, 4.218383 read upside down and 4.215500 mirrored left to right.
  `Metrics.AzimuthalAsymmetry` on a 1376 x 917 raster (the 8256 x 5504 frame R-131 names):
  4.239522 against 4.237840. On even rasters (264 x 260; the D3300's 1504 x 1004) nothing
  differs. Both candidate rules give the same figure every way up: the boundary pixel in both
  sectors 4.236745, in neither 4.240617.

Corrected after the approval, on the same day (the figures above are the ones the owner
saw): the count of CR-009 took only the profiles that write a property as an attribute. With
the element spelling too, 1499 files carry a centre (640 of 3:2, 840 of 4:3), 126 carry one in
a vignette model, and the owner's Nikkor 35 DX file is one of them, though the seven models
the program takes from it are centred. ADR 0106 has the table. And for CR-010 the largest
difference measured was 0.010 points (526 x 259), not 0.002.

## The fixes

Each: requirement, the failing test seen red, the code, the note, ADR and CHANGELOG line, the
commit (the hook runs the gate).

### 1. CR-006: the Synthetic tab's comparison belongs to the figures above it (R-08x of the tab extended)

- `FlatCheck.Wpf/ViewModels/SyntheticTabViewModel.cs`, `RunAsync`: the comparison on screen
  (verdict, table, map) is cleared when the new write's figures are shown, before the
  overwrite question. After a "No" the old file stays and is not compared by itself (it may be
  another lens's); "Compare with the master" still does it.
- Case B: a comparison that fails after the file was written must not take the write with it.
  `CompareAsync` reports its own failure in a line of its own and leaves the written flat's
  figures and the "saved" line on screen. New text, in `SessionMessages`:
  `The flat was written, but the comparison with the master failed:` /
  `Flatul a fost scris, dar comparatia cu masterul a esuat:` followed by the error.
- Tests (`FlatCheck.Wpf.Tests/SyntheticTabTests.cs`): write lens X, select lens Y, answer No:
  no comparison on screen, Y's figures, the not-written note; a comparison made to fail after a
  write: `SavedTo` kept, the new line shown, no stale comparison. The existing
  `ANoToTheOverwriteQuestion...` test stays untouched (it asserts nothing about the comparison).

### 2. CR-007: `--expo` refuses a white level at or below the black level (R-041 extended)

- `FlatCheck.Core/Analysis/ExposureAnalyzer.cs`, `Analyze`: after the precedence is resolved,
  `white > black` or an `InvalidOperationException` (what `RawReader` already throws for a
  file's own impossible scale), so the console prints it and exits 1. The text names both
  values and where each came from, in `Report/ExposureReport.cs`:
  `--expo: the white level (100, from --sat) must be above the black level (150, from the
  file). Give --sat and --black with the right values.` /
  `--expo: nivelul de alb (100, din --sat) trebuie sa fie peste nivelul de negru (150, din
  fisier). Da --sat si --black cu valorile corecte.` The sources are `--sat`, `--black`, `the
  file` / `fisier`, `guessed from the brightest pixel` / `ghicit din cel mai luminos pixel`,
  `none given` / `nedat`.
- Tests (`ExposureAnalyzerTests`, `CliTests`): `--sat 100` on a raw scale with black 150;
  `--black 8192` on a 12-bit FITS; `--black` equal to the white; each exits 1 with the message
  and gives no verdict. The parse-time check in `Options` stays.

### 3. CR-008: a compressed FITS is refused by name (R-009 extended, ADR 0105)

- `FlatCheck.Core/FitsReader.cs`, `LoadFull`: an extension is an image only when its
  `XTENSION` is `IMAGE`; a table whose `ZIMAGE` is `T` is refused at once with
  `FITS: the image is stored compressed (tile compression, as Siril's FITS compression or
  fpack write it), which FlatCheck does not read. Save the file without compression.` /
  `FITS: imaginea e stocata comprimat (compresie pe dale, cum o scriu compresia FITS din Siril
  sau fpack), iar FlatCheck nu o citeste. Salveaza fisierul fara compresie.`; any other table
  is skipped whole (its heap, `PCOUNT`, counted) and the search goes on, ending in the existing
  "no HDU with a 2D image".
- Tests (`FitsReaderTests`): the measured file above is refused with the text, from every mode
  of the console (exit 1); a file with a plain `BINTABLE` before an `IMAGE` extension gives the
  image; a plain table alone gives "no HDU with a 2D image".
- ADR 0105: refused, not decompressed (Rice and gzip tile decoding is a reader of its own, and
  the FITS reader stays hand-rolled).
- CR-013 (unchecked `NAXISn` / `BITPIX`) and CR-029 (the header-only read) stay open.

### 4. CR-009: the vertical centre of an Adobe profile is in image heights (R-072 extended, ADR 0106)

Owner's choice: **`ImageYCenter` counts in units of the calibration image's height**, as the
files and RawTherapee have it, not in units of the long side as Adobe's sentence says.

- `FlatCheck.Core/LensProfiles/VignettingModel.cs`: the model takes the calibration image's
  short-to-long side ratio (new optional argument, null when the profile does not say);
  `Radius` shifts y by `(CenterY - 0.5) * calibMm * ratio`. Without the ratio the reading stays
  today's. `FocalY` stays in Dmax.
- `FlatCheck.Core/LensProfiles/LcpReader.cs`: the ratio from the entry's `stCamera:ImageWidth`
  and `ImageLength` when both are there.
- Tests (`VignettingModelTests`, `LensProfileReaderTests`): a profile with `ImageYCenter`
  0.536912 on a 3:2 calibration body moves the axis 0.56 mm, not 0.84 (the review's Canon EF-S
  17-55 case); a centred profile gives the same radius as before to the last digit; a profile
  without the image size keeps today's shift; the reader hands the ratio over.
- `docs/design/lens-profiles.md` sections 2 and 4: Adobe's sentence for `ImageYCenter` quoted
  beside the one for `ImageXCenter`, then the counts and RawTherapee's lines that contradict it.

### 5. CR-010: a pixel on a sector boundary counts in both sectors (R-131 extended, ADR 0107)

Owner's choice: **in both neighbouring sectors**, the rule ADR 0095 already has for the
percentile tiles of `--light`.

- `FlatCheck.Core/Metrics.cs`, `AzimuthalAsymmetry`: a pixel whose `dx` or `dy` is exactly 0
  (the centre column or row of an odd raster) goes whole into the two sectors that meet there.
  `Azimuth` and the ring fit are not touched (their input is symmetrised).
- Tests: `R131_TheFiguresAreTheSame_WhenTheRasterIsMirrored` gets two more sizes whose rasters
  are odd (918 x 610 and a frame like 8256 x 5504 scaled down); a direct test on
  `Metrics.AzimuthalAsymmetry` for 263 x 259 and 1376 x 917.
- A formula change: `ParityGoldenTests` must stay byte for byte (their rasters are even; a
  changed line is a finding and stops the task) and `/validate` must show no drift (the
  masters' rasters are 1504 x 1004).

### 6. Close the entries

The Status column of the five rows and one closing line per entry, as for CR-001 to CR-005.

## Docs in the same commits

`docs/requirements.md`; `docs/decisions/0105` to `0107`; `CHANGELOG.md` (five `Fixed` lines);
`docs/design/lens-profiles.md` (CR-009), `pipeline.md` or the note that covers `Metrics.cs`
(CR-010), `modes-expo.md` (CR-007), the FITS reader's note (CR-008), `modes-synthetic.md` /
`two-applications.md` (CR-006); `README.md` where it speaks of compressed FITS or `--sat`.
This plan saved as `docs/plans/2026-11-13-fix-the-next-five-review-findings.md`.

## Verification

- Each new test seen failing before its fix, then green; the gate at every commit; a
  `--no-incremental -warnaserror` build before the last one.
- Goldens: no line expected to change; any changed line is read and reported, and a parity
  golden line stops the task.
- `/validate` on the real masters after fixes 4 and 5; nothing in `docs/validation.json` is
  edited without the owner's word.
- Then a subagent review of the branch and one of its fix commit, as the owner asked last time,
  and the merge proposed and asked.
