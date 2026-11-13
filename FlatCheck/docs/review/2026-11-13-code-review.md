# Code review of the whole project, 2026-11-13

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0100.

## How it was done

The code was written by an earlier model and had almost no review. On 2026-11-12 it measured
about 28,800 non-blank lines (`FlatCheck.Core/` 14,414, `FlatCheck/` 748, `FlatCheck.Wpf/`
13,622), too much for one careful reading, so it was split into ten areas. Each area was read
whole by a reviewer with a clean context (a subagent on this session's model), which first read
`CLAUDE.md`, `docs/design/known-issues.md` and the area's design notes, so that what is
deliberate was not reported. A reviewer changed nothing and built nothing. Every finding it
returned was then checked by the session that wrote this list: the cited lines were opened, and
the case followed through the callers. Last, the seams between areas were read (what `Runs/`
hands the console and the window, the language against the saved settings).

| # | Area | Reported | In this list as |
|---|------|---------:|-----------------|
| 1 | Numeric core, structure and light | 4 | CR-010, 030, 031, 060 |
| 2 | Raw, exposure, histeq | 5 | CR-007, 032, 033, 050, 051 |
| 3 | Files and log | 4 | CR-008, 013, 029, 048, 049 |
| 4 | Lens profiles | 5 | CR-001, 004, 005, 009, 014 |
| 5 | Report and language | 9 | CR-017 to 025 |
| 6 | Runs and console | 8 | CR-002, 007, 011, 012, 023, 026, 027, 028 |
| 7 | Window: session, Siril, shell | 9 | CR-015, 016, 034 to 037, 047, 052, 053 |
| 8 | View-models A (shell, Exposure, HistEq, Light) | 4 | CR-016, 038, 054, 055 |
| 9 | View-models B (Structure, Synthetic, Split) | 9 | CR-003, 006, 012, 016, 039, 040, 056, 057 |
| 10 | Views, presentation, theme | 13 | CR-042 to 047, 055, 058, 059 |
| - | Seams, read by the session | 1 | CR-041 |

70 findings were reported and none was rejected at the check; the same defect seen from two
areas became one entry, and small related ones were grouped, which gives the 60 entries below.
That no finding was rejected says the reviewers were careful, not that every entry is proved:
the check was a reading, not a run. What a reading cannot settle is marked.

Out of scope: `scripts/` and `.claude/` (the kit's), `flatcheck.py` and `crosscheck.py` (frozen,
ADR 0047), `lensfun/` (data), and the tests line by line (a reviewer opened a test only to see
whether a suspected defect was covered).

Three reviewers looked outside the repository, read-only: area 4 read the lens profiles Adobe
installed on this machine, to count file names and compare coefficients; area 7 read this
machine's Siril configuration and three public Siril 1.4 source files; areas 1 and 4 replicated
a few functions in throw-away Python to get figures. Their figures are quoted as theirs.

## How to read an entry

- **Severity.** 1 = a wrong figure or a wrong verdict. 2 = a crash, a hang, or a file
  overwritten or lost. 3 = a wrong text or a wrong display. 4 = robustness and cleanliness.
- **Reach.** Who meets it: *common* (an ordinary run), *some setups* (another camera, lens,
  Siril setting or screen than the ones this project is tested on), *slip* (a mistyped
  argument), *rare*, *crafted* (a broken or hostile file).
- **Evidence.** *confirmed*: the lines were read and the case follows from them. *to measure*:
  the code was read, but the case rests on something only a run or an outside source can show;
  the task starts by showing it. *owner*: the code does what is described, and whether that is
  a defect is the owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

An entry gives the direction of a fix, never the fix. A task takes one entry (or the group one
entry names), starts from the requirement and a failing test (the kit's rules "Requirements
first" and "Test before fix"), and a formula change re-validates on the masters.

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | some setups | confirmed | The camera lookup takes a longer model name, so a D750 gets a D7500's crop | fixed 4f5569ca |
| CR-002 | 1 | slip | confirmed | The console reads `--fstop 2,8` as f/28, and accepts NaN and Infinity | fixed 9a939c41 |
| CR-003 | 1 | common | confirmed | The Split tab ignores the folder's chosen lens and its "no filters" box | fixed 6bf3bb55 |
| CR-004 | 1 | some setups | confirmed | An Adobe non-raw profile is merged with its RAW sibling and can be the one used | fixed 6c8da74b |
| CR-005 | 1 | some setups | confirmed | The aperture read from a lens name takes a model code for the f-number | fixed b8b270cb |
| CR-006 | 1 | common | confirmed | The Synthetic tab keeps the previous comparison under a new lens's figures | fixed fa414c5e |
| CR-007 | 1 | slip | confirmed | `--expo` gives a verdict on a white level at or below the black level | fixed c3552133 |
| CR-008 | 1 | some setups | to measure | A compressed FITS (a table HDU) is read as an 8-pixel-wide image | fixed e3589ec5 |
| CR-009 | 1 | some setups | to measure | The Adobe model scales the vertical decentring by the long side | fixed 0377b8d9 |
| CR-010 | 1 | some setups | to measure | The azimuthal asymmetry depends on the orientation on an odd raster | fixed e1e81911 |
| CR-011 | 2 | slip | confirmed | `-o` naming an input file replaces that file with the PNG | open |
| CR-012 | 2 | rare | confirmed | Output files are written in place: a failed write leaves a cut file | open |
| CR-013 | 2 | crafted | confirmed | The FITS reader uses `NAXISn` and `BITPIX` before checking them | open |
| CR-014 | 2 | rare | confirmed | A lens name with non-ASCII digits throws out of the identification | open |
| CR-015 | 2 | rare | to measure | Stopping Siril can crash the window when a child process cannot be killed | open |
| CR-016 | 3 | rare | confirmed | An input folder that cannot be listed leaves a dead Run button and no log line | open |
| CR-017 | 3 | slip | confirmed | A `--lens` text that matches nothing is reported as "no raw" | open |
| CR-018 | 3 | some setups | confirmed | The "focal length / aperture not known" texts say "no raw" when a raw was read | open |
| CR-019 | 3 | some setups | confirmed | The "crop not known" text says the calibration sensor "is taken" | open |
| CR-020 | 3 | common | confirmed | "Lower the exposure by ~-0.1 stops" when the centre clips below the target | open |
| CR-021 | 3 | rare | confirmed | A series of one frame prints "NaN" and says the frames differ | open |
| CR-022 | 3 | common | confirmed | A negative sky correlation is flagged "above the threshold of 0.70" | open |
| CR-023 | 3 | common | confirmed | `--lenses <text>` lists every lens, and a refusal recommends that form | open |
| CR-024 | 3 | rare | to measure | A depth difference that is not a number prints "NaN" and "LESS than the profile" | open |
| CR-025 | 3 | rare | confirmed | A figure rounded at a boundary contradicts the word beside it | open |
| CR-026 | 3 | slip | confirmed | A folder with `--light` plus another mode flag is attempted, not refused | open |
| CR-027 | 3 | common | owner | `--against` prints `-0.00 stops` for a difference that rounds to zero | open |
| CR-028 | 3 | slip | owner | A `--raw` file that does not exist leaves the exit code at the verdict | open |
| CR-029 | 3 | some setups | confirmed | The header-only read and the full read parse the same FITS differently | open |
| CR-030 | 3 | common | confirmed | "tiles used" counts a mask the last fit never used | open |
| CR-031 | 3 | rare | confirmed | A light with one NaN background tile is refused as "too few usable tiles" | open |
| CR-032 | 3 | some setups | to measure | The raw's colour order is shifted by libraw's margins | open |
| CR-033 | 3 | some setups | confirmed | An X-Trans or monochrome raw is refused as "debayered, saved from Siril" | open |
| CR-034 | 3 | some setups | owner | The master is found only as `.fit`; a Siril set to `.fits` loops on the card | open |
| CR-035 | 3 | common | confirmed | The script list recurses into subfolders where Siril's menu does not | open |
| CR-036 | 3 | some setups | to measure | The Siril command counter depends on a line Siril translates | open |
| CR-037 | 3 | common | confirmed | Four window texts say something the code did not do | open |
| CR-038 | 3 | common | confirmed | Mosaic to another view, at a manual zoom, shows another place at half the size | open |
| CR-039 | 3 | rare | confirmed | A failed recompare wipes the whole Structure result and hides the switch | open |
| CR-040 | 3 | rare | confirmed | A save failure names the wrong file | open |
| CR-041 | 3 | some setups | confirmed | The remembered lens carries a translated word, lost on a language change | open |
| CR-042 | 3 | common | confirmed | With "Sky flat" on, the histograms lose the red "too low" zone | open |
| CR-043 | 3 | common | confirmed | Zooming about a point ignores the margin of the centred picture | open |
| CR-044 | 3 | common | to measure | Every "working" progress bar is a full, still bar | open |
| CR-045 | 3 | common | to measure | The picture's pan and double click also act on its scroll bars | open |
| CR-046 | 3 | some setups | to measure | "100 %" is not one screen pixel per sensor pixel on a scaled display | open |
| CR-047 | 4 | - | confirmed | Thresholds and lists typed as literals in texts | open |
| CR-048 | 4 | crafted | confirmed | The header-only read gives up after 1000 blocks and returns a partial header | open |
| CR-049 | 4 | some setups | confirmed | The FITS writer adds `ROWORDER` beside a copied `BAYERPAT`, and copies checksums | open |
| CR-050 | 4 | some setups | to measure | The preview rule has no lower bound: a 160 x 120 thumbnail wins | open |
| CR-051 | 4 | crafted | confirmed | `CfaPattern.Parse` accepts orders that are not Bayer | open |
| CR-052 | 4 | rare | confirmed | A relative folder on the window's command line stays relative | open |
| CR-053 | 4 | rare | confirmed | `flatcheck.json` is rewritten from this version's three keys only | open |
| CR-054 | 4 | common | owner | A superseded Exposure pick still waiting at the gate computes anyway | open |
| CR-055 | 4 | some setups | confirmed | Zooming out from a fit below 10 % zooms in | open |
| CR-056 | 4 | slip | confirmed | The Synthetic tab's number fields drop bad text in silence | open |
| CR-057 | 4 | common | to measure | The Siril log is rebuilt whole for every line | open |
| CR-058 | 4 | common | to measure | Three theme defects: tab header colours, scroll thumb, a clipped Split cell | open |
| CR-059 | 4 | - | confirmed | The text fields and pickers have no automation name | open |
| CR-060 | 4 | - | confirmed | Design notes and comments that describe code that is gone | open |

The cleanup list after the entries holds what is not a defect: dead code, duplicates, work done
twice.

## Severity 1: a wrong figure or a wrong verdict

### CR-001 The camera lookup takes a longer model name, so a D750 gets a D7500's crop

- Where: `FlatCheck.Core/LensProfiles/LensfunReader.cs:114-129` (`CameraOf`, the condition at
  126); used by `CropOf` (85) and `MountsOf` (100).
- What happens: a table name matches when `wanted.Contains(n) || n.Contains(wanted)`, and the
  longest matching name wins. The second half lets a longer model name that contains the
  camera's whole name beat the exact name.
- Case (the reviewer's emulation over the shipped lensfun table): "Nikon D750" matches "Nikon
  D7500", crop 1.534, true 1.0. "Canon EOS R5" matches "Canon EOS R50 V". "Nikon D700" matches
  "D7000"; "Nikon D80" matches "D800E" (1.0, true 1.523); "Nikon Z f" matches "Z fc"; "Pentax
  K-1" matches "K100D Super". The first half misfires on short aliases: every "QHY..." name
  contains the alias "Q" and gets the Pentax Q's crop 5.53. A name with no letter or digit
  normalises to "" and takes the longest name in the table. 145 table cameras, looked up by
  their own names, land on another body with a crop more than 3 % off.
- Effect: the crop sets the frame's physical size for both vignetting models, so the profile's
  depth and the depth verdict are wrong, and the report prints the name asked for
  (`LensIdentification.cs:205-207`), not the entry matched. The D3300 is not affected.
- Should: the exact name wins; a longer name must not beat it; a one-letter alias must not match
  inside an unrelated name; the crop's source names the model matched.
- Test: none; `LensProfileTests.cs:202-206` pins the D3300 on a one-camera table.
- Evidence: confirmed (line 126 read; the cases are the reviewer's, to become the failing test).
- Status: fixed 4f5569ca (R-133, ADR 0101). `CameraOf` compares names as words: the exact name,
  else a table name standing whole in the file's name when that name also carries the entry's
  maker (added after the branch's review, in the commit after 1d951743); never the reverse. The
  crop's source names the table's model. `R133_...` in `LensProfileTests` pins the crossed pairs and walks the
  shipped table. Left open: `LensIdentification.PreferCamera` (the cleanup list) still matches
  by containment.

### CR-002 The console reads `--fstop 2,8` as f/28, and accepts NaN and Infinity

- Where: `FlatCheck/Options.cs:291-292` (`TryNum`), used at 172, 175, 181, 195, 198, 201, 217.
- What happens: `double.TryParse(s, NumberStyles.Any, InvariantCulture)`. `Any` allows the
  thousands separator, which is `,` in the invariant culture, and .NET drops it wherever it
  stands. `Any` also parses "NaN" and "Infinity", and the range checks written as `<= 0` are
  false for NaN.
- Case: `--fstop 2,8` gives f/28 with no error, and the comparison uses the profile nearest
  f/28. `--crop 1,5` gives 15, a 2.4 mm frame. `--focal 13,5` gives 135. `--sat 4095,5` gives
  40955. `--focal NaN` and `--fstop Infinity` pass, and `LensCatalog.Nearest`
  (`LensProfile.cs:559-563`) then throws "Sequence contains no elements".
- Should: `ERROR: --fstop expects an f-number, got '2,8'`, exit 1; `docs/design/process.md:108`
  says "`0.5` parses and `0,5` does not". The window's fields take a comma as the decimal sign
  (`SyntheticTabViewModel.cs:595`), so the two applications read the same text differently; the
  owner may prefer the console to accept the comma too.
- Fix direction: `NumberStyles.Float` and a finite-value check in `TryNum`.
- Test: none parses a comma or NaN.
- Evidence: confirmed (code read; the parse itself is documented .NET behaviour, to be pinned by
  the failing test).
- Status: fixed 9a939c41 (R-005, ADR 0102). The owner chose the refusal: `TryNum` parses with
  `NumberStyles.Float` and asks `double.IsFinite`, so a comma, `NaN` and `Infinity` get the
  option's own error. `R005_...` in `OptionsTests`. The window's fields still take a comma
  (CR-056). The sentence of `process.md` that CR-060 lists is corrected with it.

### CR-003 The Split tab ignores the folder's chosen lens and its "no filters" box

- Where: `FlatCheck.Wpf/ViewModels/SplitTabViewModel.cs:518-525`; `MainViewModel.cs:280` hands
  `Split.Attach` no `SessionSettings`.
- What happens: the analysis builds `new StructureQuery { Sources, RawPath, LoadInfo }`, with no
  `LensKey`, and never looks at the box. The Structure tab puts `_settings.Lens` on its query
  (`StructureTabViewModel.cs:488`) and compares nothing when the box is off (459).
- Case A: a session whose raw names no lens, with two candidates (the Samyang 135 on the D3300:
  Adobe's Nikkor 135 f/2 DC and lensfun's Samyang). The user picks the Samyang on the Structure
  tab. Start Split compares every candidate and the nearest row decides, so `Split result.txt`
  and `Test result.txt` can disagree on the depth verdict of the same master.
- Case B: the box "I didn't use any filters on my lens" unticked. Structure shows the five
  tests' verdict only; Split still reads every column's depth against the profile and can print
  REDO.
- Should: the Split's whole column is the Structure tab's comparison (R-093, R-118, R-121, ADR
  0078).
- Test: none (`SplitTabTests` has no lens key, box or settings case).
- Evidence: confirmed.
- Status: fixed 6bf3bb55 (R-093). `Split.Attach` takes the folder's `SessionSettings`; the
  analysis builds no query with the box off and puts `LensKey` on it otherwise, and the
  reference line says which of the two happened. `R093_...` in `SplitTabTests`.

### CR-004 An Adobe non-raw profile is merged with its RAW sibling and can be the one used

- Where: `FlatCheck.Core/LensProfiles/LensProfile.cs:329-330` (`AdobeProfileFile.FromName`).
- What happens: `mount = make + (kind.Length == 0 || kind == "RAW" ? "" : ", kind")`, so
  "X (L).lcp" and "X (L) - RAW.lcp" are the same lens; `ProfilesOf` concatenates both files and
  `Nearest` picks among all their entries.
- Case (the reviewer's read of the 3531 installed Adobe files): none ends in " - JPEG"; 433
  names have no suffix, and every entry read in such a file says `CameraRawProfile` False. 276
  lenses are made of a RAW file plus a suffix-less one. For "Canon EOS-1D Mark III (Canon EF
  17-40mm f4 L USM)" at 40 mm f/11 the nearest rule picks the non-raw entry, a1 = -0.336607
  where the RAW one has -0.491574: about a third less falloff, on a raw flat.
- Also: `kind == "RAW"` is case-sensitive, and two installed files end in " - Raw.lcp", so they
  become a separate lens "X, Raw".
- Should: the two files stay two lenses, or the non-raw one is never used for a raw. The note
  (`lens-profiles.md` section 5) says the mount gets ", JPEG", a suffix that does not exist.
- Test: `AnAdobeFileNameCarriesTheCameraAndTheLens` covers only the " - JPEG" spelling.
- Evidence: confirmed (the line read; the file facts are the reviewer's and go into the test as
  real names).
- Status: fixed 6c8da74b (R-134, ADR 0103). The owner chose the file's own flag: `LcpReader`
  skips an entry whose `CameraRawProfile` is `False`, since 43 of the 434 suffix-less files are
  raw profiles and the name cannot tell; ` - RAW` is read without case. `R134_...` in
  `LensProfileTests`. The ", JPEG" sentence of `lens-profiles.md` that CR-060 lists is
  corrected with it.

### CR-005 The aperture read from a lens name takes a model code for the f-number

- Where: `FlatCheck.Core/LensProfiles/LensProfile.cs:123` (`ApertureRx`), 148-149; consumed by
  `Contradicts` (166-167).
- What happens: the first `[fF]/?\s?(\d+(\.\d+)?)` not preceded by a letter is the aperture.
- Case: "TAMRON SP 35mm F_1.4 Di USD F045" reads 45 (the underscore blocks "F_1.4", the model
  code "F045" matches). Zeiss "Distagon T 21 f28 ZE" reads 28; "Otus 55 f14 ZE" reads 14.
  lensfun's "smc Pentax-F 28mm f/2.8" reads 28. For a raw whose range says f/1.4,
  `Contradicts` finds 10 stops of difference and drops the right lens; the candidates by range
  are then other lenses, and the depth verdict comes from one of them.
- Should: a number that cannot be a maximum aperture is "not said", which contradicts nothing.
- Test: none; `LensSpec_ReadsTheRangeAndTheApertureOutOfTheName` has "F/1.8" before the code.
- Evidence: confirmed for the parse; what libraw reports for those lenses is to measure.
- Status: fixed b8b270cb (R-018, ADR 0104). A number followed by `mm` is a focal length, and
  one outside `Th.LensNameApertureMin` to `Th.LensNameApertureMax` (0.7 to 11) gives no
  aperture. `R018_...` in `LensProfileTests`. What libraw reports for those lenses was not
  measured: no such raw is in the library.

### CR-006 The Synthetic tab keeps the previous comparison under a new lens's figures

- Where: `FlatCheck.Wpf/ViewModels/SyntheticTabViewModel.cs:604-678` (`RunAsync`); the view shows
  the comparison block on `HasCompare` alone (`SyntheticView.xaml:249, 289`).
- What happens: `RunAsync` calls `Populate` for the new request (639) before the overwrite
  question and never clears the comparison, which only `PopulateCompare` replaces and only
  `Reset` clears.
- Case A: write lens X (its comparison with the master is on screen), select lens Y, press
  Write, answer No. The screen shows Y's profile line and Y's depth rows, the note that nothing
  was written, and below them the verdict, the table and the map of X's comparison, with nothing
  to say whose they are.
- Case B (to measure): the file for Y is written, then the comparison throws or is cancelled;
  the tab says "The analysis failed", the saved line is gone, and X's comparison is still shown.
- Should: no comparison on screen once the figures above it are another lens's; a written file
  whose comparison failed is still said to be written.
- Test: `ANoToTheOverwriteQuestionKeepsTheOldFile_AndTheResultStays` pins three other fields.
- Evidence: confirmed for A.
- Status: fixed fa414c5e (R-135). `RunAsync` clears the comparison when it shows the new
  request's figures; `CompareAsync` reports its own failure in `CompareError` and leaves the
  written flat and its "saved" line on screen (case B, shown by a test that spoils the file
  between the write and the comparison). `R135_...` in `SyntheticTabTests`. The branch's review
  added, in the commit after 767eb2ea: the failure line outside the written flat's panel, so it
  shows for a file of an earlier session, and the button taking unwritten figures off the
  screen before it compares the file on disk (the owner's choice).

### CR-007 `--expo` gives a verdict on a white level at or below the black level

- Where: `FlatCheck.Core/Analysis/ExposureAnalyzer.cs:273-274` (`rng = white - black`,
  unchecked); the only guard is `FlatCheck/Options.cs:239-242`, which compares `--sat` with
  `--black ?? 0` and only when `--sat` is given.
- Case: `flat.NEF --expo --sat 100` on a D3300 (the file's black is 150): the range is -50 and
  the report says "THE FILE DOES NOT LOOK LIKE A RAW FLAT ... flat-dark already subtracted", the
  outcome the comment at `RawReader.cs:322-325` says must not happen. `flat.fit --expo --black
  8192` on a 12-bit file: a pixel at 2000 reads 151 %, verdict TOO HIGH, "lower the exposure by
  ~1.4 stops". `--black` equal to the white: a range of 0 and infinities.
- Should: exit 1 with a message naming the argument, whichever side came from the file (R-005).
- Fix direction: check `white > black` in `Analyze`, after the precedence is resolved.
- Test: `ASaturationAtOrBelowBlack_IsAnError` covers only both given, and `--sat 0`.
- Evidence: confirmed.
- Status: fixed c3552133 (R-041). `ExposureAnalyzer.Analyze` refuses `white <= black` after
  the precedence, with `ExposureReport.ImpossibleScale` naming both levels and where each came
  from; the console exits 1. `R041_...` in `ExposureAnalyzerTests`.

### CR-008 A compressed FITS (a table HDU) is read as an 8-pixel-wide image

- Where: `FlatCheck.Core/FitsReader.cs:332-392` (the acceptance test is line 361).
- What happens: `LoadFull` never looks at `XTENSION`; any HDU with `NAXIS >= 2` and both sides
  above 0 is the image, decoded with its `BITPIX`, and `PCOUNT` is ignored.
- Case: a tile-compressed FITS (Siril's FITS compression, or `fpack`): an empty primary, then
  `XTENSION= 'BINTABLE'`, `BITPIX = 8`, `NAXIS1 = 8`, `NAXIS2` = the number of tiles, `ZIMAGE =
  T`. The reader returns an `Img` 8 pixels wide made of the heap descriptors, with no error.
- Should: a refusal that names the cause.
- Fix direction: accept an extension only when `XTENSION` is `IMAGE`; refuse `ZIMAGE = T` by
  name.
- Test: none (no `BINTABLE` or `.fz` fixture).
- Evidence: the reader's part is confirmed; what the run then prints (figures on an 8-column
  raster, or the index exception of known issue 3) is to measure on a real compressed file.
- Status: fixed e3589ec5 (R-136, ADR 0105). Measured first on a file built to the tile
  compression convention (no real compressed master was at hand): the structure mode printed
  a whole report and REDO, exit 3, and `--histeq` wrote a picture. `LoadFull` now takes an
  extension for an image only when its `XTENSION` is `IMAGE`, refuses `ZIMAGE = T` by name and
  skips any other table with its heap. `R136_...` in `FitsReaderTests`. CR-013 and CR-029,
  which touch the same loop, stay open.

### CR-009 The Adobe model scales the vertical decentring by the long side

- Where: `FlatCheck.Core/LensProfiles/VignettingModel.cs:178`.
- What happens: `yd = (yN * longSideMm - (CenterY - 0.5) * calibMm) / fyMm`, with `calibMm` the
  calibration body's long side, the same as for x.
- Case (the reviewer's read of the installed files): `ImageYCenter` sits near 0.5 on 3:2 bodies
  (mean 0.507 over 7574 values in the Nikon folder), which is v0 / height; v0 / Dmax would sit
  near 0.333. So the shift is 1.5 times too large: for "Canon EOS 50D (Canon EF-S 17-55mm f2.8
  IS USM)" at 35 mm, `ImageYCenter` 0.536912, the code moves the axis 0.84 mm where the file
  means 0.56 mm. At a typical 0.497 the error is 0.02 mm.
- Effect: the top-bottom asymmetry of the synthetic flat (the offset and corner-spread rows of
  `--against`); the four-corner mean hardly moves.
- Evidence: to measure, and the owner's call: the note quotes Adobe's sentence for
  `ImageXCenter` only (`lens-profiles.md:51-54`); Adobe's sentence for `ImageYCenter` has to be
  read before the model changes.
- Status: fixed 0377b8d9 (R-072, ADR 0106). Adobe's sentence was read: it says "v0 / Dmax"
  for the vertical centre too. The files say a half on 3:2 and 4:3 alike (1480 files) and
  RawTherapee multiplies the centres by the image's width and height, so the owner chose that
  reading: `VignettingModel` counts each centre in its own side, taken from the entry's
  `ImageWidth` and `ImageLength`. The value 0.536912 quoted above was not found in the
  installed Canon EOS 50D file; the test uses it as an example of the arithmetic. `R072_...`
  in `LensProfileTests`.

### CR-010 The azimuthal asymmetry depends on the orientation on an odd raster

- Where: `FlatCheck.Core/Metrics.cs:72-74` (`Azimuth`), as used at 572 (`AzimuthalAsymmetry`).
- What happens: a pixel with `dx == 0` or `dy == 0` sits on a sector boundary and always goes to
  the `>= 0` side. On an odd width or height the centre column or row is such a line; a mirror
  leaves it in place, so in the mirrored frame it joins the other sector. `AzimuthalAsymmetry`
  runs on the unsymmetrised raster, so the twelve sector medians are not the mirrored set.
- Case (the reviewer's Python replica of the two functions): on 263 x 259 the figure is
  5.671851639 and 5.671257603 after a column mirror; on 459 x 305, 4.3e-3 points apart; on an
  even 264 x 260, identical. R-131 and ADR 0095 promise the same figures within 1e-9 for every
  frame of even width and height, and name 8256 x 5504, whose analysis raster is 1376 x 917.
- Effect: 0.0003 to 0.004 points on one figure; a verdict moves only on a threshold coincidence.
  The ring fit is not affected (its input is symmetrised).
- Test: `R131_TheFiguresAreTheSame_WhenTheRasterIsMirrored` uses two sizes whose analysis
  rasters are even.
- Evidence: the mechanism is confirmed; the C# figure is to measure (add 526 x 518 to the
  theory). A formula change: the parity goldens and `/validate` decide.
- Status: fixed e1e81911 (R-131, ADR 0107). Measured in C#: 4.2164 against 4.2184 points on a
  459 x 305 raster, 3.3613 against 3.3716 on 526 x 259. The owner chose "in both sectors":
  `AzimuthalAsymmetry` adds a pixel with `dx` or `dy` of 0 to the two sectors beside that axis
  (`Metrics.AxisSectors`). No parity golden changed and `/validate` shows no drift (44 of 44).
  `R131_...` in `StructureAnalyzerTests` and `MetricsTests`; a 40th parity scenario,
  `structure odd raster 526x518`, reaches the new branch.

## Severity 2: a crash, a hang, or a file overwritten or lost

### CR-011 `-o` naming an input file replaces that file with the PNG

- Where: `FlatCheck/Options.cs:229-232` (the only guard: `--synthetic` against the positional);
  `FlatCheck/FlatCheckCli.cs:345-347` (`SaveMap`, `File.WriteAllBytes`).
- Case: `flatcheck process/pp_flat_stacked.fit -o process/pp_flat_stacked.fit` (or `A.fit
  --against B.fit -o B.fit`, or `-o` naming the `--raw` file): the run reads the file, prints
  the report, then overwrites the master, the second flat or the raw with PNG bytes and prints
  "map saved". `--synthetic` is not checked against `--raw` either.
- Should: the refusal the existing guard gives, before anything runs.
- Test: none.
- Evidence: confirmed.

### CR-012 Output files are written in place: a failed write leaves a cut file

- Where: `FlatCheck/FlatCheckCli.cs:322-323` and 347; `FlatCheck.Wpf/ViewModels/
  SyntheticTabViewModel.cs:650-666`.
- What happens: the target is opened with `FileMode.Create`, which truncates it, and about
  96 MB are streamed in.
- Case: the disk fills, the drive drops, or the window is closed during the write
  (`MainWindow.OnClosing` waits only for a split): the earlier good file is gone and a FITS with
  a valid header and cut data sits under the real name; "Compare with the master" then fails
  with the "data cut short" message.
- Should: the old file intact or a complete new one, as `SessionSettings.Write` already does
  with a temporary file and a move.
- Test: none.
- Evidence: confirmed (the window-close timing is to measure).

### CR-013 The FITS reader uses `NAXISn` and `BITPIX` before checking them

- Where: `FlatCheck.Core/FitsReader.cs:348-363, 394`.
- Case: a negative `NAXISn` gives a negative `dataBlocks`, and `pos += (int)dataBlocks` can set
  the position back so the loop never ends (`BITPIX = 8, NAXIS1 = -2880, NAXIS2 = 2` after a
  one-block header). With `BITPIX` missing, `bytesPer` is 0, which disables the R-009 bound, and
  a large `NAXIS1 x NAXIS2` ends in an out-of-memory or overflow exception instead of
  "unsupported BITPIX".
- Should: both refused by name before any allocation.
- Evidence: confirmed by reading; only a broken or crafted file reaches it.

### CR-014 A lens name with non-ASCII digits throws out of the identification

- Where: `FlatCheck.Core/LensProfiles/LensProfile.cs:120, 123, 145-149`.
- What happens: `\d` in a .NET regex matches every Unicode digit; `double.Parse` with the
  invariant culture accepts ASCII digits only.
- Case: one profile the user adds whose name has full-width digits. The range path parses every
  name in the catalogue, so a `FormatException` leaves `LensIdentification.Identify` for every
  raw that reaches it. No shipped or installed name has such digits (the reviewer checked).
- Fix direction: `[0-9]` and `TryParse`.
- Evidence: confirmed (language semantics).

### CR-015 Stopping Siril can crash the window when a child process cannot be killed

- Where: `FlatCheck.Wpf/Session/SirilScriptRun.cs:234-248` (`Kill`).
- What happens: `p.Kill(entireProcessTree: true)` throws `AggregateException` when not every
  process of the tree could be ended; the filter catches `InvalidOperationException` and
  `Win32Exception` only, though the summary says "never throws". The method runs in a
  cancellation callback, so the exception leaves `Cancel()` in `SirilRunViewModel.cs:226` and
  `SplitTabViewModel.cs:604`, which catch `ObjectDisposedException` only.
- Evidence: the code is confirmed; a child that refuses to die is to measure.

## Severity 3: a wrong text or a wrong display

### CR-016 An input folder that cannot be listed leaves a dead Run button and no log line

- Where: `FlatCheck.Wpf/Session/SirilSession.cs:227` (`Directory.EnumerateFiles(dir).Any()`,
  uncaught, in `MissingInputFolders`); reached through `Preprocess.Attach` at
  `MainViewModel.cs:241`, outside `OpenAsync`'s `try` (which starts at 253), through
  `MainWindow.xaml.cs:142`, and through `Script.Refresh()` before the `try` of
  `SplitTabViewModel.StartAsync` (424-431).
- Case: a session without a master whose `flats/` (or `lights/`, `biases/`) is under a deny rule
  or is a broken junction. `OpenAsync` faults; the window fired it with `_ =`
  (`MainWindow.xaml.cs:67, 153, 167`), and `App.xaml.cs:54` hooks only the dispatcher's
  exceptions, so nothing shows or logs it. The card shows the script list, no reason, and a Run
  button that stays grey. On the Split tab, Start appears to do nothing.
- Should: what R-080 and R-110 give elsewhere: the folder named with the error, and an Error
  line.
- Fix direction: catch the two exceptions per folder and report it as a blocker; give a faulted
  fire-and-forget task a log line.
- Test: `R080_AnUnlistableProcessFolderStillGetsTheMissingMasterCard` denies `process/`, which
  is not an input folder.
- Evidence: confirmed (three reviewers reported it independently).

### CR-017 A `--lens` text that matches nothing is reported as "no raw"

- Where: `FlatCheck.Core/Report/StructureReport.cs:472-485` (`GapText`); the gap is chosen in
  `FlatCheck.Core/Analysis/VignettingComparison.cs:291-295` and 317.
- What happens: `StructureRun.cs:225-227` passes the empty list `MatchLenses` returns, and the
  comparer picks the gap from the raw alone.
- Case: `flatcheck master.fit --lens tokna` prints "no raw beside the master ... Give --lens
  <text> or --raw <file>". With a raw that names a lens: "<the raw's lens>: no lens in the
  catalog matches it". The sentence written for this, "no lens matches the text given; see
  --lenses" (484), is reached only when lenses matched but none gave a row (317), where it is
  false; that last case is to measure (it needs a lens whose file has no vignetting model).
- Fix direction: "a text was given and matched nothing" and "matched, but no vignetting model"
  as gaps of their own.
- Test: none for the words.
- Evidence: confirmed.

### CR-018 The "focal length / aperture not known" texts say "no raw" when a raw was read

- Where: `FlatCheck.Core/Report/StructureReport.cs:486-491`.
- What happens: the two texts are fixed ("no FOCALLEN in the master, and no raw", "there was no
  raw beside it; give --fstop <f> or --raw <file>"), and the comparer returns these gaps
  whenever the value is null, raw or no raw. A raw read error (`RawError`) is printed only under
  the "no raw" gap and dropped under these two.
- Case: a Canon EOS 40D session (R-106: the CR2 gives focal 0 and aperture 0) with `--raw` and
  `--lens` given: the report says there was no raw.
- Should: with a raw read, say the raw gives no focal length or aperture, and do not offer
  `--raw` (R-106: "never as a missing raw").
- Test: `WithALensButNoFStopTheGapIsTheFStop` runs without a raw only.
- Evidence: confirmed.

### CR-019 The "crop not known" text says the calibration sensor "is taken"

- Where: `FlatCheck.Core/Report/StructureReport.cs:492-494`, interpolating `shot.CropSource`,
  which in this gap is always the text of `LensIdentification.cs:209`.
- Case: lensfun candidates only, a camera not in lensfun's table, no `--crop`: "lensfun profiles
  only, and the camera's crop is not known (unknown - the profile's calibration sensor is
  taken); give --crop <factor>". Nothing is taken: the lensfun rows were skipped.
- Also: `SyntheticReport.cs:85-86` prints "camera crop : unknown  (unknown - ...)", the word
  doubled.
- Evidence: confirmed.

### CR-020 "Lower the exposure by ~-0.1 stops" when the centre clips below the target

- Where: `FlatCheck.Core/Report/ExposureReport.cs:219-220`.
- What happens: `stops = log2(Target / cen)` and the text prints `{-stops:F1}`. The verdict "too
  high" is also reached by clipping alone (`ExposureAnalyzer.cs:557`), with the centre below the
  target.
- Case: a centre at 55 % with a burnt spot: "Lower the exposure by ~-0.1 stops."; at 40 %,
  "~-0.5 stops". The test fixture `ExposureReportTests.Clipped()` is this case. The window shows
  the same text.
- Should: no figure when the centre is at or below the target.
- Evidence: confirmed.

### CR-021 A series of one frame prints "NaN" and says the frames differ

- Where: `FlatCheck.Core/Report/LightSeriesReport.cs:82-83, 144-156`.
- Case: `flatcheck folder --light` where one file matches: "error of the mean : NaN points <-
  above the threshold of 2.00", and "its error (NaN) is larger even than the OK threshold
  (2.00), because the frames differ too much from each other". The NaN and its reason are
  deliberate (`LightSeriesAnalyzer.cs:122`); the words are not.
- Should: say the error cannot be computed from one frame.
- Evidence: confirmed.

### CR-022 A negative sky correlation is flagged "above the threshold of 0.70"

- Where: `FlatCheck.Core/Report/LightSeriesReport.cs:85-89`; the condition is on the absolute
  value (`LightSeriesAnalyzer.cs:124`). The window's note says the same
  (`SessionMessages.cs:1174-1184`: "above 0.70 ... move together").
- Case: a correlation of -0.94: "correlation w. sky grad. : -0.94 <- above the threshold of
  0.70".
- Evidence: confirmed.

### CR-023 `--lenses <text>` lists every lens, and a refusal recommends that form

- Where: `FlatCheck/Options.cs:224-227` and `FlatCheck/FlatCheckCli.cs:290-300`; the hint is in
  `FlatCheck.Core/Report/SyntheticReport.cs:390-391`.
- What happens: `--lenses` is a flag without a value; a positional is accepted as the input file
  and never used, and the list is filtered by `--lens` only.
- Case: after "no lens matches", the tool says "Try another text, or --lenses <text> to see what
  is in the list." `flatcheck --lenses tokina` then prints the whole catalogue.
- Should: the text filters the list, or the positional is refused; the hint names a form that
  exists.
- Evidence: confirmed.

### CR-024 A depth difference that is not a number prints "NaN" and "LESS than the profile"

- Where: `FlatCheck.Core/Report/StructureReport.cs:626-628` (`DepthAdvice`: `diffStops > 0 ?
  MORE : LESS`); the rows and the summary print the NaN (313, 354-356, 598-599).
- Evidence: to measure. It needs a profile whose model goes to zero or below at the master's
  corner; `VignettingModel.cs:194-196` says an extrapolated polynomial can.

### CR-025 A figure rounded at a boundary contradicts the word beside it

- Where: `FlatCheck.Core/Report/LightSeriesReport.cs:82-83, 147, 153`;
  `FlatCheck.Core/Report/ExposureReport.cs:321-330`;
  `FlatCheck.Core/Report/SyntheticReport.cs:157-162`.
- Case: an error of the mean of 2.003 prints "2.00 points <- above the threshold of 2.00"; a
  centre at 49.96 % prints "at 50.0 %, 0.0 points below the target 50-65 %"; a corner at 1.0004
  times the calibration corner prints "at 1.00 x ...: a little beyond what was measured".
- Fix direction: decide the branch on the figure as shown, as `Numbers.RoundsToZero` does for
  the local spot.
- Evidence: confirmed for the first; the other two are the reviewer's reading.

### CR-026 A folder with `--light` plus another mode flag is attempted, not refused

- Where: `FlatCheck/FlatCheckCli.cs:113` tests the flag `!o.Light`, not the mode that will run.
- Case: `flatcheck lights/ --light --histeq`: the mode is histeq, the check passes, and the user
  reads "ERROR: Access to the path '...\lights' is denied."
- Evidence: confirmed.

### CR-027 `--against` prints `-0.00 stops` for a difference that rounds to zero

- Where: `FlatCheck.Core/Runs/AgainstRun.cs:342` (`Stops`), used at 218 and 286.
- Case: two flats that agree, the ratio's depth -0.003 stops: "depth : PERFECT: -0.00 stops",
  two lines above "Two correct flats of the same lens give PERFECT and 0.00 stops."
- Evidence: owner. ADR 0092 makes "no minus on a zero" the rule of every report but `--light`'s,
  and R-129's list does not name `--against`.

### CR-028 A `--raw` file that does not exist leaves the exit code at the verdict

- Where: `FlatCheck.Core/Runs/StructureRun.cs:213-222` (every failure to read the raw becomes
  `RawError` and the run goes on); `FlatCheck/FlatCheckCli.cs:208-216`.
- Case: `flatcheck master.fit --raw flats/DSC_0001.NFE` (a typo): the report says the raw could
  not be read, and the exit code is 0 on a good master.
- Evidence: owner. Going on is right for the window, which finds the raw itself; R-002 says a
  missing file given on the command line is exit 1.

### CR-029 The header-only read and the full read parse the same FITS differently

- Where: `FlatCheck.Core/FitsReader.cs:126-162` (`LoadHeader`: the primary HDU only, its own
  value parser) against 307-330 (`LoadFull`: the first HDU with an image; the value cut at the
  first `/`, known issue 1). The comments at 91-92 and 244-245 say the two cannot differ.
- Case: an empty primary plus an image extension: the full read gives the focal length, the
  camera, the CFA and the size of the extension; the header read gives none and a size of 0 x 0
  (the Synthetic tab's master size, the Split's pixel count). `INSTRUME= 'A/B cam'`: "A" from
  one, "A/B cam" from the other.
- Fix direction: one HDU walk and one card-to-value function for both.
- Evidence: confirmed.

### CR-030 "tiles used" counts a mask the last fit never used

- Where: `FlatCheck.Core/Analysis/LightAnalyzer.cs:127` and 155.
- What happens: each of the four iterations fits on `keep`, then rewrites `keep` from that fit's
  residuals; `KeptTiles` counts the mask made after the last fit, not the one behind the printed
  figures.
- Evidence: confirmed.

### CR-031 A light with one NaN background tile is refused as "too few usable tiles"

- Where: `FlatCheck.Core/Analysis/LightAnalyzer.cs:113-135`.
- What happens: one NaN tile value makes the right-hand side NaN, every residual NaN, the
  threshold NaN, and `res[i] < thr` rejects every tile; the next solve is singular, the
  coefficients are overwritten with null and the method throws. The comment (130-131) says it
  throws only when the first iteration is singular.
- Case: a float light where more than 15 % of one tile is NaN.
- Fix direction: start `keep[i]` from `double.IsFinite(V[i])`.
- Evidence: confirmed by the trace; no real file is known to produce it.

### CR-032 The raw's colour order is shifted by libraw's margins

- Where: `FlatCheck.Core/RawReader.cs:292`; `FlatCheck.Core/CfaPattern.cs:88-119`.
- What happens: `FromLibRaw(filters, cdesc, TopMargin, LeftMargin)` evaluates the pattern at
  `(TopMargin + y, LeftMargin + x)`, taking `filters` as relative to the full raw buffer. The
  reviewer's reading of libraw is that `filters` already describes the visible origin.
- Case: a raw with an odd margin (the reviewer names Canon bodies with a top margin of 51, and a
  DNG whose active area starts on an odd row): the order comes out one row off, so R is printed
  as "G1", the raw-against-screen table pairs the wrong channels, and the `--histeq` composite
  is mis-coloured. With even margins (the D3300) the two readings agree.
- Evidence: to measure. The libraw facts were recalled, not read. One `--expo` on such a raw
  with the Debug log on settles it: `RawReader.cs:358-364` prints the margins, `filters` and
  `cdesc`. `modes-expo.md` does not mention margins.

### CR-033 An X-Trans or monochrome raw is refused as "debayered, saved from Siril"

- Where: `FlatCheck.Core/RawReader.cs:273-292` (only a missing mosaic is refused; `filters` 0, 1
  or 9 gives no CFA and goes on); `FlatCheck.Core/Analysis/ExposureAnalyzer.cs:352`.
- What happens: the analyzer always splits by the parity of x and y. On the 6 x 6 X-Trans
  mosaic the four parity classes hold the same mix of colours, the spread is about 1.00, and the
  verdict is "not a raw".
- Case: `flatcheck DSCF0001.RAF --expo` prints "THE FILE DOES NOT LOOK LIKE A RAW FLAT ...
  Typical cause: you opened the NEF in Siril and pressed Save". R-004 lists `.raf`.
- Should: an open refusal naming the sensor, as the reader gives a Foveon file.
- Evidence: confirmed by the trace; a run on a RAF would show the exact text.

### CR-034 The master is found only as `.fit`; a Siril set to `.fits` loops on the card

- Where: `FlatCheck.Wpf/Session/SirilSession.cs:81, 123-127`; `SeriesFinder.DefaultPattern`
  (`pp_light_*.fit`).
- Case: Siril's preference `extension=.fits`. "Run in Siril" ends well, Siril wrote
  `pp_flat_stacked.fits`, the folder reopens and the same "master is missing" card returns.
- Evidence: owner (R-080 names the file exactly), and Siril's side is to measure. Either the
  master is looked for under every FITS extension, or the card says the limit.

### CR-035 The script list recurses into subfolders where Siril's menu does not

- Where: `FlatCheck.Wpf/Session/SirilInstall.cs:232-241` (`SearchOption.AllDirectories`); the
  claim "the one in Siril's scripts menu" at 25-30 and in `SessionMessages.cs:743-751`.
- Case (this machine): the list holds all 17 `.ssf` of the scripts repository plus the 6 stock
  ones, among them scripts that are not preprocessing. Siril 1.4 reads the top level of each
  folder (the reviewer read `script_menu.c`).
- Fix direction: top level for the configured folders, recursion only for the fallback.
- Evidence: confirmed on FlatCheck's side.

### CR-036 The Siril command counter depends on a line Siril translates

- Where: `FlatCheck.Wpf/Session/SirilLogLine.cs:55` (`"Running command: "`).
- Case: a Siril whose interface is not English: no line matches, so there is no "command 7 of
  20", the failure message loses "at ...", and the Split bar stands still.
- Evidence: to measure with a translated Siril. Fix direction: read the untranslated line Siril
  prints beside it, or set the language in the process's environment.

### CR-037 Four window texts say something the code did not do

- Where: `FlatCheck.Wpf/Session/SessionMessages.cs:811-817, 743-751, 845-851, 1132-1147`.
- The four: (a) the "Siril is open" warning is shown on the Split tab too
  (`SplitView.xaml:151`), where it speaks of `process/` and of "Run in Siril"; a split writes
  under the temporary folder and its button is "Start Split"; the Romanian names the button
  "Ruleaza" where it is "Ruleaza in Siril". (b) "the one proposed leaves the master in
  process/", while the proposal falls back to the first script by name
  (`SirilInstall.cs:87-90`). (c) "the folder stays as it was" after a script that stopped half
  way. (d) English "almost always", Romanian "aproape sigur".
- Evidence: confirmed for (a) and (c), read; (b) and (d) are the reviewer's.

### CR-038 Mosaic to another view, at a manual zoom, shows another place at half the size

- Where: `FlatCheck.Wpf/ViewModels/HistEqTabViewModel.cs:267-289` (`RefreshImage`).
- What happens: the Mosaic bitmap has the source's size (280); every other view has the half
  size (278, 279, 284). The image is swapped and the zoom and the scroll offsets stay.
- Case: a raw flat on Mosaic at 100 %, panned to a dust spot, then G1: the picture is half as
  large and the spot is out of view. The class header (40-41) says looking at the same place
  after Mosaic "is exactly the point of the chips".
- Evidence: confirmed.

### CR-039 A failed recompare wipes the whole Structure result and hides the switch

- Where: `FlatCheck.Wpf/ViewModels/StructureTabViewModel.cs:432-439`;
  `TabViewModel.cs:147-158`.
- What happens: a flip of the filter box or a lens pick re-reads the master's header (435) even
  when no comparison is asked for. Any exception sets `HasResult = false`.
- Case: a result on screen, the master then moved or locked (a cleanup tool, Siril rewriting
  it), the box unticked: the five tests, the verdict and the map vanish behind "The analysis
  failed", and the box sits inside the result's grid, so the flip cannot be undone.
- Should: the result stays; the comparison card alone says it could not be redone.
- Evidence: confirmed.

### CR-040 A save failure names the wrong file

- Where: `FlatCheck.Wpf/ViewModels/StructureTabViewModel.cs:702-715`;
  `FlatCheck.Wpf/ViewModels/SplitTabViewModel.cs:660-677`.
- Case: `flatcheck.png` locked by an image viewer: Structure shows "Report saved" and, in red,
  "Could not save the report". On Split with a locked mosaic: the text file was written, yet the
  tab shows no saved line and says the report could not be saved.
- Evidence: confirmed.

### CR-041 The remembered lens carries a translated word, lost on a language change

- Where: `FlatCheck.Core/LensProfiles/LensProfile.cs:72` (`LensChoice.Key` ends with the
  calibration label), `LensfunReader.cs:219`, `SensorFormats.cs:88` (`Unusual` is a `T` pair).
- What happens: for a lensfun lens calibrated on a crop that is no common format (39 of the 712
  lensfun lenses, by the reviewer's count), the key holds "unusual format" or "format
  neobisnuit". The key is what the window saves as `lens` in `flatcheck.json`, and a key that
  matches no candidate is ignored.
- Case: pick such a lens with the window in English, reopen the folder with `--lang ro`: the
  choice is gone without a word.
- Fix direction: build the key from the crop's digits, not from the label.
- Evidence: confirmed (found at the seam between the language and the settings).

### CR-042 With "Sky flat" on, the histograms lose the red "too low" zone

- Where: `FlatCheck.Wpf/Presentation/HistogramCanvas.cs:206-207`; the view-model hands
  `TooLow = NaN` on the sky scale (`ExposureTabViewModel.cs:345`), and a zone with a NaN edge is
  skipped.
- Case: Sky flat ticked: nothing is shaded over 0-15 %; a sky flat at 8 %, verdict too low, has
  its marker on an unshaded strip.
- Evidence: confirmed.

### CR-043 Zooming about a point ignores the margin of the centred picture

- Where: `FlatCheck.Wpf/Views/ZoomableImage.xaml.cs:188-203` (`ZoomAbout`) and 171-180
  (`Apply`).
- What happens: the arithmetic holds only when the picture starts at the viewport's left edge;
  at "fit" a 3:2 frame is centred with margins. The toolbar's buttons do not go through
  `ZoomAbout` at all: they rescale and leave the offsets.
- Case: a double click on the picture's left edge at fit opens 100 % far from that edge; key `1`
  lands on the right edge of the frame, not its centre; the `+` button moves the middle of the
  view up and left; `100 %` from fit always shows the top-left corner.
- Evidence: confirmed (the arithmetic; the pixel figures are the reviewer's).

### CR-044 Every "working" progress bar is a full, still bar

- Where: `FlatCheck.Wpf/Theme/Dark.xaml:441-459`: the template has a track and an indicator and
  no animation.
- Evidence: to measure on screen (the reviewer recalled that WPF fills the indicator when the
  bar is indeterminate).

### CR-045 The picture's pan and double click also act on its scroll bars

- Where: `FlatCheck.Wpf/Views/ZoomableImage.xaml.cs:220-262`.
- What happens: the handlers sit on the scroll viewer and do not look at where the event came
  from; the drag ends only on a mouse-up, with no handler for a lost capture.
- Case: two quick clicks on a bar's track jump back to "fit"; a click on the track with the
  mouse moving a pixel undoes the page step; Alt+Tab during a drag leaves the picture following
  the mouse with no button pressed.
- Evidence: to measure on screen (the missing checks are confirmed).

### CR-046 "100 %" is not one screen pixel per sensor pixel on a scaled display

- Where: `FlatCheck.Wpf/Views/ZoomableImage.xaml.cs:171-180`; the claim is in
  `HistEqView.xaml:173`.
- Case: Windows scaling at 125 % or 150 %: each sensor pixel covers 1.25 or 1.5 screen pixels,
  which on the Bayer checkerboard draws a regular pattern that is not in the data, on the tab
  meant for looking for patterns.
- Evidence: to measure on a scaled display.

## Severity 4: robustness and cleanliness

### CR-047 Thresholds and lists typed as literals in texts

The rule "Ratios" says a threshold is never a literal in code or text. All of these match the
constants today; a recalibration would leave them behind, and no test binds them.

- "~0.2 stops" (`Th.ProfileDatabaseSpreadStops`): `StructureReport.cs:370-371, 652-655`,
  `SyntheticReport.cs:280, 288`, `AgainstRun.cs:289, 292`.
- The residual bands 2 / 10 / 20: `AgainstRun.cs:197-198`, `SessionMessages.cs:204-211,
  269-274`.
- "98 % of white" (`Th.ExpoClipWhite`): `ExposureView.xaml:146`, `HistogramCanvas.cs:120` (the
  stripe's default, bound by no template), `SessionMessages.cs:303-310`. Line 304 also says "If
  any part of the solid curve reaches that stripe, the verdict is TOO HIGH", while the code
  needs more than `Th.ExpoClip` of the central pixels there.
- "pixels at 255" (`ExposureReport.cs:619-620, 642`), while the count is of 250 to 255.
- The "not a raw" gate's 1.0 and 1.02: `ExposureAnalyzer.cs:351-352`, not in `Th`.
- "12 slices", "the 5 % box": `StructureReport.cs:931-933`, `SplitReport.cs:243`,
  `SyntheticReport.cs:252-254`.
- "~40-degree" (`Th.LightFovMax`) and `pp_light_*.fit` in the usage: `FlatCheckCli.cs:389,
  401, 433, 445`.
- The raw and FITS extension lists: `FlatCheckCli.cs:398, 442`, `SessionMessages.cs:167-180,
  476-485`.

### CR-048 The header-only read gives up after 1000 blocks and returns a partial header

- Where: `FlatCheck.Core/FitsReader.cs:131, 161`. A file with no `END` card in its first 1000
  blocks: `LoadInfo` succeeds with whatever it gathered, while the full read throws "incomplete
  header" (ADR 0057 says both refuse).
- Evidence: confirmed.

### CR-049 The FITS writer adds `ROWORDER` beside a copied `BAYERPAT`, and copies checksums

- Where: `FlatCheck.Core/FitsWriter.cs:52-56, 93-106`.
- What happens: when the copied cards hold no `ROWORDER`, the writer inserts `'BOTTOM-UP'`
  beside the copied `BAYERPAT`, so on an even height the copy declares a Bayer phase one row off
  its reference. `CHECKSUM`, `DATASUM` and `BLANK` are copied although the data and `BITPIX`
  changed.
- Effect: small; the synthetic flat has no per-channel structure.
- Evidence: confirmed at the level of the header.

### CR-050 The preview rule has no lower bound: a 160 x 120 thumbnail wins

- Where: `FlatCheck.Core/RawReader.cs:565-582`.
- Case: a raw whose only JPEGs are a small thumbnail and a full-size preview: the "camera
  screen" histograms come from 19,200 pixels, letterbox bars included. Also, a failed export of
  the chosen JPEG does not try the other candidates.
- Evidence: the rule is confirmed (and matches ADR 0024's wording); which cameras do this is to
  measure.

### CR-051 `CfaPattern.Parse` accepts orders that are not Bayer

- Where: `FlatCheck.Core/CfaPattern.cs:77-85`; four letters with two G pass ("RGGR", "BGGB").
  A FITS with such a `BAYERPAT` makes `--histeq` fail with "Sequence contains no matching
  element" (`HistEqAnalyzer.cs:572-573`).
- Evidence: confirmed; only a broken header reaches it.

### CR-052 A relative folder on the window's command line stays relative

- Where: `FlatCheck.Wpf/App.xaml.cs:141-149`. `FlatCheck.Wpf.exe .` shows `.` as the session,
  and every later disk access depends on the current directory.
- Evidence: confirmed; what siril-cli does with a relative `-d` is to measure.

### CR-053 `flatcheck.json` is rewritten from this version's three keys only

- Where: `FlatCheck.Wpf/Session/SessionSettings.cs:139-148`. A key written by a newer build is
  erased by one switch flip in an older one; two windows on one folder share one temporary file.
- Evidence: confirmed.

### CR-054 A superseded Exposure pick still waiting at the gate computes anyway

- Where: `FlatCheck.Wpf/ViewModels/ExposureTabViewModel.cs:231-248`: the run's token is the
  folder's, and nothing after the gate asks whether a newer pick exists.
- Case: the Down arrow held through ten raw flats: ten runs of about 4 s each, in turn.
- Evidence: owner. `two-applications.md` and ADR 0061 say a superseded run "leaves the queue
  without computing", and the Histogram tab does so; ADR 0077 declined a generation check for
  the writes after the computation and does not speak of the queue.

### CR-055 Zooming out from a fit below 10 % zooms in

- Where: `FlatCheck.Wpf/ViewModels/ImageZoomViewModel.cs:40, 49-50, 69`;
  `FlatCheck.Wpf/Views/ZoomableImage.xaml.cs:173, 191`.
- Case: a frame taller than about 5200 px in the 520 px viewport (fit 9 %): the minus button
  enlarges it to 10 %, and no manual factor shows the whole frame. Not reachable on the D3300.
- Evidence: confirmed (the arithmetic).

### CR-056 The Synthetic tab's number fields drop bad text in silence

- Where: `FlatCheck.Wpf/ViewModels/SyntheticTabViewModel.cs:594-595` (`Num`), 276-282.
- Case: "5O" (the letter O) in the focal box: the flat is written for the header's focal length
  and only the profile line shows it. "f/2.8" in the aperture box: the refusal says the aperture
  is not known. "Infinity" is accepted and makes `LensCatalog.Nearest` throw. The "crop unknown"
  note does not come back when the crop field is emptied, contrary to the field's comment.
- Evidence: confirmed.

### CR-057 The Siril log is rebuilt whole for every line

- Where: `FlatCheck.Wpf/ViewModels/SirilRunViewModel.cs:267-268, 281`: every line copies the
  whole log into a new string, through a synchronous call to the window's thread, and the view
  scrolls to the end (`SirilRunWindow.xaml.cs:93`, `SplitView.xaml.cs:36`), which also throws
  away the user's scroll position.
- Evidence: the code is confirmed; the cost on a long session is to measure.

### CR-058 Three theme defects: tab header colours, scroll thumb, a clipped Split cell

- `FlatCheck.Wpf/Theme/Dark.xaml:62-67` against 247-271: the application-wide `TextBlock` style
  sets the colour the tab header's own setters mean to vary, so every header is equally bright.
- `Dark.xaml:463-471`: the scroll thumb has a 3 px margin and no minimum length; on a list of
  thousands of lenses it is a sliver.
- `FlatCheck.Wpf/Views/SplitView.xaml:42-51`: the cell is a horizontal stack 230 wide, where
  trimming never applies; a long Romanian row cuts the end of its pill.
- Evidence: to measure on screen, all three.

### CR-059 The text fields and pickers have no automation name

- Where: `FlatCheck.Wpf/Views/SyntheticView.xaml:127, 155, 157, 159`, `StructureView.xaml:216`,
  `SplitView.xaml:145`, `HistEqView.xaml:151-152`. A screen reader announces four fields as
  "edit"; the zoom buttons are named by their signs.
- Evidence: confirmed.

### CR-060 Design notes and comments that describe code that is gone

- `docs/design/pipeline.md:75-76` (`Prepare` is said to call `BlockMean`; it calls
  `BlockMeanCentred`), 107-108 (`FitCenter` "on a strided subsample"; it reads every pixel since
  ADR 0095, and restoring the stride would break R-131), 199-200 (a `Rank` that does not exist);
  `docs/design/code-map.md:123` lists `Rank` too; `pipeline.md:44` lacks `.pef`.
- `docs/design/process.md:108` (says `0,5` does not parse: CR-002), 109 (lists `--scale`,
  removed), 145-146.
- `docs/design/lens-profiles.md` section 5 (", JPEG": CR-004).
- `docs/design/two-applications.md:355` (a Split cell "is a fixed-width grid"),
  `docs/design/code-map.md:421`.
- ADR 0054 says `parte-<i>` and `Rezultat split.txt`; the code and R-092 say `part-<i>` and
  `Split result.txt` (an ADR changes only in the ways the kit's ADR 0024 lists).
- Comments in the code: `Metrics.cs:593-595` and `StructureAnalyzer.cs:72-76` ("the attribution
  mode", removed); `VignettingComparison.cs:32-33` (known issue 5, closed);
  `Metrics.cs:667-673` (sensor pixels by a factor of 2; it is 4 on a 24 MP frame);
  `Thresholds.cs:25`; `FitsReader.cs:268-272`; `ThirdParty.cs:84-90` (`.txt` for `.md`);
  `LightSeriesRun.cs:20`, `ExposureRun.cs:32-33`, `RunOutcome.cs:45-47`;
  `SirilSession.cs:48-51, 291-300` ("never throws"); `SirilScript.cs:28-30`;
  `SessionSettings.cs:33-35`; `MainViewModel.cs:142`; `ExposureTabViewModel.cs:423-425`;
  `SyntheticTabViewModel.cs:352-354`; `StructureReport.cs:443-449` (two summaries on one
  member, none on `GapText`).

## Cleanup

Not defects. Reported by the reviewers; read by the session only where an entry above cites the
same lines.

Dead or unused:
- `Thresholds.cs:90` `Th.FullFrameDiagMm`; `ExposureReport.cs:219` the `: -1` branch;
  `Options.cs:120` `-h` / `--help` in the flag set.
- `FitsReader.cs:266, 281` and `PngWriter.cs:175, 277`: members only tests call;
  `FitsWriter.cs:88-92, 134-138`: the 16-bit path has no caller in the applications.
- `AgainstRun.cs:110, 250` pass-through wrappers; `SyntheticRun.cs:134, 191-192` an unused
  parameter and an overload for tests; `FlatCheckCli.cs:172` an alias.
- `ProfileCatalog.cs:67-73` `Loaded`; `ExposureTabViewModel.cs:174` `IsRawFlat`;
  `SyntheticTabViewModel.cs:416, 660` the `Saved` event, 620-621 two refusals the button cannot
  reach; `StructureTabViewModel.cs:283` a setter nobody calls.
- `StructureAnalyzer.cs:173-175` with `Metrics.cs:710`: the "NaN falls on the warned side" guard
  cannot fire, because `LocalSpots` never returns NaN; a NaN tile is skipped in silence.
- `Dark.xaml:55` `BadBr` unused, while the views type `#F0857E` about ten times; 126-130 a
  trigger that sets what is already set.

Duplicated:
- `ExposureAnalyzer.cs:320, 335` inline copies of `ClipShare` without its empty-list guard; 483
  duplicates `Mosaic.ColourOf`.
- `SplitTabViewModel.cs:375` repeats `SplitPlan.Divides`; 500, 509, 553 with 557: the "temporary
  folder was kept" paragraph shown twice.
- `ReportPanel.xaml:37-56` a hand-written copy of the theme's `Disclosure` template.
- `SplitView.xaml.cs:36` and `SirilRunWindow.xaml.cs:93`, the same handler.
- `MainWindow.xaml:32-33`, `Dark.xaml:29-30`: two prefixes for one namespace.

Work done twice:
- `Metrics.cs:458` a clone before a function that clones; 623 two clone-and-sort passes.
- `FitsReader.cs:363-370` a second full-size array on a mono file (known issue 7).
- `RawReader.cs:604` the thumbnail unpacked twice.
- `HistEqTabViewModel.cs:222` the composite PNG encoded on every pick and dropped.
- `LightTabViewModel.cs:158, 300`: `process/` listed and the map's range sorted on the window's
  thread, then again by the run.
- `StructureTabViewModel.cs:435, 502` the header read twice; `SyntheticTabViewModel.cs:706-708`
  one wasted encode; `HistogramSourceViewModel.cs:141-146` the rows rebuilt three times.
- `LightSeriesRun.cs:148-170` frames of different sizes are refused only after every frame was
  analysed.

Small and fragile:
- `SirilInstall.cs:378` the process list is never disposed.
- `LensProfile.cs:363, 485, 530`, `LensIdentification.cs:379`: `OrderBy` with the default
  comparer, ordinal in the console and linguistic in the window, so the two can order the same
  lenses differently (to measure).
- `LensIdentification.cs:415` `PreferCamera` matches by containment, the weakness of CR-001.
- `LcpReader.cs:171`, `LensfunReader.cs:303` accept NaN and Infinity; `LcpReader.cs:99, 119`,
  `LensfunReader.cs:228` one bad entry drops the whole file.
- `VignettingModel.cs:256` prints "fx/Dmax 0" for a decentred profile without that value.
- `FitsReader.cs:422`, `FitsWriter.cs:77` format a number with the current culture.
- `HistEqReport.cs:129` prints "0 .. 0" for levels under 0.0005; `SplitReport.cs:142` prints
  "1024 MB".
- `ExposureReport.cs:450-456` against 408-414: the sentence takes the brightest raw channel, the
  table averages the two greens.
- `Options.cs:188` accepts `--lens ""`; `SyntheticRun.cs:152` tests for null where 138 tests for
  blank.
- `SirilSession.cs:227` a folder holding only a `readme.txt` counts as present.
- `SyntheticTabViewModel.cs:772-804` `Reset` leaves the search text, so the next folder's list
  opens filtered; 343 a literal 3000 x 2000 fallback size; 636 a refusal logged as "done".
- `ExposureTabViewModel.cs:130`, `HistEqTabViewModel.cs:102`: a pick that failed cannot be
  retried by clicking the same row.
- `LightTabViewModel.cs:317-318` `Reset` clears the dispatcher a run in flight captured (not
  reachable from the screen).
- `SessionMessages.cs:1108-1120` `CountWord(1)` gives "one components" (not reached today).
- `SplitView.xaml:221` against 63, `LightView.xaml:194`: headers a few pixels off their columns.
- `ZoomableImage.xaml.cs:139-142, 162-163` "fit" computed while the bars still show; 211 a
  touchpad's small wheel deltas each zoom a full step.
- `HistogramCanvas.cs:275` a marker outside 0-100 % is not drawn.
- `ExternalLink.cs:51` any URI goes to the shell; every caller passes an https constant today.
- `SplitView.xaml:163-164` Siril's log is visible only while the run is alive, so after a failed
  part it disappears, though R-092 says the kept folder "is shown with the log".
- `FlatCheck.Wpf.csproj:22` `ApplicationHighDpiMode` is a Windows Forms property (not verified).
- `LightSuitability.cs:122-125` folds the binning into `XPIXSZ` (deliberate, R-031); some
  cameras' `XPIXSZ` already includes it. No effect on a DSLR.
- `SyntheticReport.cs:255-258` "long edge (left/right)" labels a point on the short sides of a
  landscape frame (a naming the note shares; the owner's call).
- In the tests: `FitsWriterTests.cs:135-140` a helper one block short in one case;
  `ResetKeepsTheChosenSourceButDisablesIt` asserts the opposite of its name;
  `CfaPatternTests.cs:54-66` states the convention CR-032 disputes.
