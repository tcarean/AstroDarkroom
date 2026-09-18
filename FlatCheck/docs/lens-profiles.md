# Synthetic flats from lens vignetting profiles: specification and evidence

Covers also, since 2026-09-18, the structure mode's sixth test - the master's vignetting
against the lens profiles (section 11).

Covers `FlatCheck.Core/LensProfiles/` (`VignettingModel`, `LensProfile` / `LensCatalog` /
`AdobeProfileFile`, `LcpReader`, `LensfunReader`, `ProfileLocator`),
`Analysis/SyntheticFlatBuilder.cs`, `Report/SyntheticReport.cs`, `Runs/SyntheticRun.cs`,
`Runs/AgainstRun.cs`, the `--synthetic` / `--lenses` / `--against` modes in
`FlatCheck/Options.cs` and `FlatCheckCli.cs`, the window's `SyntheticTabViewModel` /
`SyntheticView`, the `lensfun/` folder, and the shooting metadata added to `LoadedFrame`.
Measured 2026-09-17 on the author's machine (Windows 10, Lightroom Classic installed,
lensfun `data/db` at upstream commit `e34596cb52`, 2026-09-16) and on the masters under
`E:\Photografie\Astro\Flats and Darks\`; sections 7-9 re-run 2026-09-18 after the Adobe
model started using the camera's crop (section 4). If the model code changes, re-run
section 9 and update the numbers.

## 1. Why the mode exists

On `Tokina\ISO400\f2.8-sky-no-difuser` (Tokina at 20 mm f/2.8 on a D3300, 15 + 15 frames
rotated 180°, twilight sky near the zenith) the master's vignetting came out at 0.26 stops
in the corner box, while the two monitor-lit masters of the same lens read 0.82 and 0.77.
Same lens, same aperture (EXIF checked). The sky brightens toward the horizon all around
the zenith and fills the corners; a monitor up close falls off with viewing angle and
deepens them. Neither is the lens, and nothing in the tool can tell which one is closer:
an isotropic contamination of the radial term is the blind spot of every test, `--ratio`
included. The only source-free number for the lens is a published profile. The user's
decisions (2026-09-17): Adobe profiles read from disk, never copied; the whole lensfun
database in the repo, not embedded, copied next to the exe; `--against` in the same
version; the sources panel in the window collapsed by default.

## 2. The Adobe model, from the source document

"Adobe Camera Model" (Adobe Lens Profile Creator technical report,
`download.macromedia.com/pub/labs/lensprofile_creator/lensprofile_creator_cameramodel.pdf`,
fetched 2026-09-17; the PDF's text was extracted with a script, formulas transcribed):

- Vignette Model (p. 8): "The vignette function can be expressed as a polynomial radial
  loss function: L(x_d, y_d) = 1 + α1 r_d² + α2 r_d⁴ + α3 r_d⁶, I(x_d, y_d) = L(x_d, y_d) ·
  I_ideal(x_d, y_d), where r_d² = x_d² + y_d² and x_d = (u_d − u0)/f_x, y_d = (v_d − v0)/f_y."
- p. 9: "Note that these model parameters are identical for all color channels."
- Vignette Model Descriptors (p. 18): "Let Dmax be the maximum of the reference image width
  or height in the number of pixels. stCamera:FocalLengthX: fx / Dmax. If absent, default to
  stCamera:FocalLength in millimeters. stCamera:ImageXCenter: u0 / Dmax. If absent, default
  to 0.5." Params 2 and 3 are optional ("Required if α3 is present").

Reading of the default: with the sensor's long side at `36 / SensorFormatFactor` mm, a pixel
offset of `Δu / Dmax` of the long side is `Δu / Dmax · 36 / SFF` mm, and dividing by the
focal length in mm gives `x_d = (Δu / Dmax) / (f · SFF / 36)`. So `fx / Dmax = f · SFF / 36`
(the `36 / SFF` long side is the *calibration body's*; on another body the pixel offset
converts to mm with that body's own crop, section 4)
and **r is the distance in mm over the nominal focal length in mm.** lensfun's own reading
agrees: `lens.cpp` says of the ACM model "Coordinates are in units of focal length", and
`lensfun-convert-lcp` passes the alphas through unchanged with `cropfactor = SensorFormatFactor`.
(lensfun's `mod-color.cpp` carries a FixMe wondering whether Adobe means the nominal or the
real focal length; we use the nominal one, which is what the LCP's own table confirms.)

Cross-check inside one file, `NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp`,
20 mm, ApertureValue 2.970854 (f/2.80), distance 10000:

| | value |
|---|---|
| a1, a2, a3 | −1.903047, 4.514725, −4.657911 |
| piecewise table | (0, 1), (0.147989, 0.941), (0.355496, 0.828), (0.709416, 0.603) |
| DX corner, 14.1 mm / 20 mm | r = 0.705 |
| polynomial at 0.7094 | 0.597 (table: 0.603) |

The table's last point is the corner, on the same axis, within 1 %. `VignettingModelTests`
pins it. Aperture is APEX: `f = 2^(Av/2)`. Some profiles are decentred (the 14-20 f/2 has
`ImageXCenter = 0.4938`, `FocalLengthX = 0.8992`) and the model honours `FocalLengthX/Y`
and `ImageXCenter/YCenter` when present.

## 3. The lensfun model, from the source code

`libs/lensfun/modifier.cpp:79` (master, 2026-09-17):
`NormScale = hypot(36.0, 24.0) / Crop / hypot(Width + 1.0, Height + 1.0) / RealFocal;` -
the library normalises every coordinate to units of the focal length.

`libs/lensfun/mod-color.cpp`, `rescale_polynomial_coefficients`:
`hugin_scale_in_millimeters = hypot(36.0, 24.0) / lcv.CalibAttr.CropFactor / 2.0;`
`hugin_scaling = real_focal / hugin_scale_in_millimeters;` and for `LF_VIGNETTING_MODEL_PA`
`Terms[0] *= hugin_scaling²`, `Terms[1] *= hugin_scaling⁴`, `Terms[2] *= hugin_scaling⁶`.
So a `pa` entry is calibrated with **r = 1 at the half-diagonal of a 36×24 frame divided by
the lens entry's `<cropfactor>`**, and `lens.cpp` documents `Cd = Cs * (1 + k1 R² + k2 R⁴ +
k3 R⁶)` (multiply to apply the vignetting, divide to correct). No `acm` entries exist in the
database at that commit; the DTD allows only `model="pa"`. Entries carry `focal`,
`aperture`, `distance` (m), `k1..k3`; cameras carry `<cropfactor>` (D3300: 1.523).

For the Tokina entry (`<cropfactor>1.61</cropfactor>`, "Taken with Canon EOS 7D M2"),
20 mm f/2.8: `k = −0.6195, 0.3276, −0.1494`; at r = 1 the polynomial is 0.5587. On a D3300
the corner sits at r = 1.06: a mild extrapolation the report names.

## 4. The model as implemented (`VignettingModel`)

Common input: `(xN, yN)` = the pixel's offset from the **image centre** in units of `Dmax`
(the long side in pixels); the builder feeds `((x + 0.5) − W/2) / Dmax` and the same for y.

- **Adobe**, everything in mm: `calibMm = 36 / SFF` (the calibration body's long side),
  `longSideMm = 36 / cameraCrop` (yours; `calibMm` when the crop is unknown, i.e. 0),
  `fxMm = FocalX · calibMm` when the profile gives `FocalLengthX` (it is in units of the
  calibration body's `Dmax`), else `FocalMm`; `xd = (xN · longSideMm − (CenterX − 0.5) ·
  calibMm) / fxMm`, likewise `yd`, `r = hypot(xd, yd)`, `V = 1 + a1 r² + a2 r⁴ + a3 r⁶`;
  with a table instead of coefficients, linear interpolation in r, clamped at both ends.
  At `cameraCrop = SFF` this is exactly `(xN + 0.5 − CenterX) / (FocalX ?? FocalMm · SFF /
  36)`, the first version's formula. **The first version used the profile's SFF for every
  camera**: right on a D3300 with the DX file to 1.5 % (1.523 vs 1.5), wrong by the whole
  corner on a full frame with a DX-calibrated file or the reverse, with no warning. The
  decentring (`ImageXCenter`) is the lens's, in mm, so it does not scale with the crop.
  Evidence that the real crop is what Adobe used too: the piecewise table in the Tokina
  file ends at r = 0.7094, i.e. 14.19 mm at 20 mm, the half-diagonal of a 36 / 1.523 =
  23.64 mm sensor - not of the 24 mm one SFF 1.5 implies (14.42 mm, r = 0.721).
  `CalibrationCornerRadius()` = `hypot(36, 24) / 2 / SFF / fxMm` (1 for lensfun), and
  `Extrapolation(w, h, crop)` = the frame's corner r over it (`SyntheticFlatResult.
  Extrapolation` is the same for the written flat). `SyntheticReport.FormatMatch(model,
  crop, w, h)` turns both into the one sentence the console prints at "format" and the tab
  shows when a lens is picked and under the written depths: the calibration format
  (`SensorFormats.LabelOf` of the profile's crop / SFF), the user's format, "acelasi
  format" when the crops are within `SameFormatTolerance` (3 %) or "alt format, raza se
  recalculeaza prin mm" otherwise, then the corner: inside the profile (below 0.97, "in
  interiorul profilului"), nothing (0.97-1.0), "extrapolare mica" up to 1.1 (the D3300 on
  the lensfun entry calibrated at crop 1.61: 1.06), "extrapolare MARE" beyond - a
  sixth-order polynomial 50 % past its data (a full frame on a DX profile) goes negative on
  the fixture. `SensorFormats.Nearest` lights the tab's chip within 5 % of a nominal crop
  (1.523 → APS-C, 1.61 → APS-C Canon, 1.15 → none). `FormatMatchIsWarning` is true only for
the large extrapolation; the tab colours the sentence amber on that alone, so "acelasi
format" never looks like a warning.
- **lensfun**: `longSideMm = 36 / cameraCrop`, `xMm = xN · longSideMm`,
  `yMm = yN · longSideMm`, `huginMm = hypot(36, 24) / CropFactor / 2`,
  `r = hypot(xMm, yMm) / huginMm`, same polynomial. Throws without a camera crop.
- `Radius(...)` exposes r for the report ("coltul cade la r = 1.06").

## 5. The catalog and the readers

- **LCP** (`LcpReader.ReadXml`): `photoshop:CameraProfiles/rdf:Seq/rdf:li`; properties as
  attributes of the child `rdf:Description` (the installed files) or as child elements of
  the `rdf:li` (the document's Appendix A); the `stCamera:VignetteModel` either with
  attributes or wrapping an `rdf:Description` (when a `VignetteModelPiecewiseParam` table
  is present, entries "r, V"); `AlternateLensNames`, `LensID` + `AlternateLensIDs`
  (Nikon lens IDs: 122 and 139 for the Tokina). Entries without a vignette model (fisheye
  files) are skipped; a file that is not XML throws, and the catalog swallows that per
  file. `Make`/`CameraPrettyName` becomes the mount label ("NIKON CORPORATION"),
  `LensPrettyName` the name, the first word of it the maker.
- **lensfun** (`LensfunReader.ReadXml`): `<camera>` (maker, model + `lang` aliases, mount,
  cropfactor) and `<lens>` (same, plus `<calibration><vignetting model="pa" .../>`);
  lenses without vignetting are dropped. `LensfunData.CropOf(name)` matches the camera
  model or an alias by containment on letters and digits only, longest match wins
  ("NIKON D3300" → 1.523).
- **The calibration crop is part of a lensfun lens's identity** (`LensChoice.Calibration`,
  "APS-C 1.528" / "full frame 1": the nominal format from `SensorFormats.LabelOf` plus the
  exact `<cropfactor>`). The database holds the same lens, on the same mount, twice when
  two contributors calibrated it on bodies of different format - 20 of the 692 lenses with
  vignetting in the shipped copy (Canon EF 50/1.4 and 50/1.8, Nikkor 35/1.8G DX, 60/2.8G
  Micro, 70-200/2.8 VR, Sigma 50/1.4, Pentax FA 50/1.4 ...). With the key
  (source, maker, name, mount) the two collapsed into one lens, `Nearest` broke the tie on
  focal, f-stop and distance by file order, and the second calibration was unreachable
  from either application. Found on the Nikkor 35 f/1.8G DX (section 8): the entry that
  won, "average crop of Nikon APS-C cameras", reads 0.19 stops at the DX corner at f/2.8;
  the hidden D780 entry reads 0.51, beside Adobe's 0.61. The label is shown on **every**
  lensfun lens, not only the duplicated ones (`LensChoice.Where`: "Nikon F AF, calibrat pe
  full frame 1", in the lists, the tables, the reports and the FITS `HISTORY`): a name that
  changed when the database gained a second entry would break `--lens` text and a saved
  choice. It is searchable ("Nikkor 35mm full"); the bare name matches both, which is the
  ordinary ambiguity refusal with the list, exit 2. Adobe entries carry no label: the
  calibration body is already the mount ("NIKON D90"), and the SFF sits in the file, not in
  its name, so it could not enter the lazily built list.
- **Laziness** (`LensCatalog`): 3531 `.lcp` files (455 MB) on this machine take 36 s to
  parse; listing them takes 2 s. So Adobe entries are `AdobeProfileFile` stubs from the
  file name: `<Camera> (<Lens>) - RAW.lcp` in `<Maker>/<Mount>/` → name = the text between
  the first " (" and its matching ")", mount = the camera text (plus ", JPEG" when the
  suffix is not RAW), maker = the lens's first word. Names without parentheses (638 files:
  phones and compacts, `Apple/iPhone 6.lcp`, `Canon/Canon PowerShot G12 - RAW.lcp`)
  become camera-named stubs with the folder as maker and no mount. A file is parsed on
  `ProfilesOf(lens)` (cached, re-keyed to the stub's identity) and on `ByLensId(id, hint)`,
  which parses in parallel only the stubs whose mount or maker contains the hint (the
  camera's first word). Aliases therefore enter `Search` only after a file was parsed.
- **Search**: every word of the text must appear in name + maker + mount (+ known
  aliases), case-insensitive; empty text lists everything; `--source` restricts.
- **Nearest** profile: nearest focal length, then nearest f-stop in stops
  (`2·log2 f`), then the farthest distance. Reported as "profil folosit" beside "cerut".
- **A lensfun pair at 10 and 1000 m with identical coefficients is one profile, distance
  unknown** (`LensfunReader.CollapseCopiedDistances`, `DistanceM = NaN`, printed
  "distanta nespecificata"). That is the lensfun convention for "measured at one distance,
  not recorded": the calibration script copies the values to both ends, and two real
  measurements never agree to four decimals. Before, the tool took the 1000 m copy and
  printed "infinit" for it - on the Nikkor 35/1.8G DX the "APS-C" entry said "infinit" while
  its floor of 0.16-0.17 stops from f/4 to f/22 is the floor of Adobe's 0.36 m profile
  (0.17-0.19), not of the 1.51 m one (0.23): most likely a wall shot up close, and the label
  was claiming the opposite (section 8). A single entry at a stated distance keeps it, and a
  pair with different coefficients is two measurements and keeps both.
- **Lens choice from text** (`SyntheticRun.ChooseLens`): search hits; an exact name match
  narrows to those; when several remain and the reference frame names a camera, the ones
  whose mount or maker contains the camera's first word win; then `PreferCamera`: the
  files named for the body itself ("NIKON D750 (lens)", letters and digits compared) beat
  the generic mount file ("NIKON CORPORATION (lens)"), because Adobe keeps per-body files
  with the body's SFF and Lightroom takes those; still several → refusal with the list,
  exit 2. No text + a Nikon lens ID from a raw → `SyntheticRun.CandidatesFromRaw`:
  `ByLensId` (the camera's maker word as hint), then `LensCatalog.Narrow` against the
  range libraw reads out of the MakerNote (`LensMinFocalMm`, `LensMaxFocalMm`,
  `LensMaxAperture` on `LoadedFrame`). The window passes the chosen `LensChoice` directly
  and skips this, but its prefill starts from the same `CandidatesFromRaw`.
- **Narrowing by name** (`LensSpec`): the Nikon lens ID is not unique - on this machine's
  Adobe folder 139 is carried by the Tokina AT-X 11-20 F2.8, the Tokina atx-i 11-20 f2.8,
  the Nikkor 18-200 f3.5-5.6G VR II, the Nikon DX 18-200 and the Sigma 18-35 F1.8 - and the
  first cut preselected the alphabetically first hit, the Nikkor, for a raw shot with the
  Tokina. `LensSpec.Parse(name)` reads the focal range (first `<n>[-<m>]mm`) and the
  maximum aperture (first `f/<n>`, `F<n>` or `f<n>-<m>` not preceded by a letter, so
  `AF-S` and `EF` do not match; a prime is `min = max`; "Zeiss Batis 2/25" parses to
  nothing). `Contradicts` is true only when both sides say something and disagree: focal
  ends within 1 mm or 5 % (names round), aperture within 0.25 stops (Nikon's codes step in
  sixths). A name that does not parse is kept, so the narrowing can only remove wrong
  lenses, never lose the right one for a strange name; when every hit contradicts the raw,
  the list is empty and the console asks for `--lens`. lensfun entries carry no Nikon IDs,
  so `CandidatesFromRaw` adds the lensfun lenses whose maker matches a surviving Adobe hit
  and whose name does not contradict the range: on this machine the raw yields the two
  Adobe generations (AT-X PRO DX, atx-i CF) and the two lensfun entries below. `LensSpec.Describe` is the
  "11-20 mm f/2.8" in `SyntheticReport.RawLensLabel`, printed in the refusal and in the
  window's prefill note.

## 6. The flat, the report, the file

`SyntheticFlatBuilder.Build(profile, W, H, cameraCrop)`: `V` at every pixel, 1.0 on the
axis (the principal point; not renormalised), the same value in all four CFA positions.
Beside it, from the model rather than the raster, the depth in stops
(`log2(V(0,0) / V(point))`) at four points expressed as fractions of the half-sides:
corner `(0.95, 0.95)` (the centre of `CornerStats`' 5 % box), long edge `(0.95, 0)`,
short edge `(0, 0.95)`, half semidiagonal `(0.5, 0.5)` - the points of the comparison
table in section 8. `SyntheticReport.DepthRows` carries a note per point (which point it
is, what it compares with: the corner is the master's own "vignetare centru -> colt", the
half semidiagonal is mid-field where the object sits); the window shows the notes, the
console prints label and value only (`DepthLines`).

`SyntheticRun.Complete` fills focal (`--focal` > `FOCALLEN` > libraw), f-stop
(`--fstop` > libraw; a FITS from Siril carries none), crop (`--crop` > lensfun cameras by
`INSTRUME` / libraw model; required by lensfun, used by Adobe when known and replaced by
the profile's SFF when not - `CameraCrop` 0, source "necunoscut"), each with its source
string for the report. The report prints the crop line for both sources
(`SyntheticReport.CropLine`: value and source only) and, under it, the "format" line
(`FormatMatch`, section 4). The lensfun camera table (1053 bodies in the shipped
database) is the one list of cameras; `SensorFormats` (six nominal crops: full frame 1.0,
APS-C 1.5, APS-C Canon 1.6, APS-H 1.3, Micro 4/3 2.0, 1" 2.7, with the sensor sizes as
notes) is the fallback the refusal text lists and the window offers as chips - a list of
our own would age at the first new body, six numbers do not. Refusals: a colour cube, no lens chosen (exit 2), no focal / f-stop / crop (exit 1).
`LoadedFrame` carries `FocalMm`, `FNumber`, `Camera`, `NikonLensId`, `LensMinFocalMm`,
`LensMaxFocalMm`, `LensMaxAperture`: `FitsReader` from `FOCALLEN` / `INSTRUME`,
`RawReader.ShootingInfo` from libraw's `ImageOtherParams` (`FocalLength`, `Aperture`),
`IParams.Make/Model`, `LensInfo.Nikon.LensIDNumber` and `LensInfo.MakerNotes`
(`MinFocal`, `MaxFocal`, `MaxAperture4MinFocal`; zero means null).
`FitsReader.LoadHeader(path)` reads only the 2880-byte header blocks, for the window's
prefill on a 96 MB master.

Output: `FitsWriter.Write(stream, flat, -32, referenceCards, history)` - float, the
reference frame's cards minus the structural ones (`BAYERPAT` included, so Siril treats
the file as a CFA master), four `HISTORY` lines (banner + mode, lens + profile + request,
model, reference file name). The console writes to `--synthetic <path>` (never over the
reference); the window to `SirilSession.SyntheticMasterPath` =
`pp_flat_stacked_sintetic.fit` beside the master, after the overwrite question, and it
never becomes the session's master.

## 7. `--against`

`AgainstAnalyzer.Analyze(a, b)`: both through `Img.Prepare`, each divided by its own
median (exception when the median is not positive), `ratio = a / b`, then
`StructureAnalyzer.AnalyzePrepared` on **all three** (A, B, ratio). `AgainstReport.Rows`
builds eight rows, each with a value for A, B and A / B and a note written about the
difference: vignetting centre→corner in stops (the ratio's is the depth difference,
positive = A falls more toward the corner), illumination-centre offset, the local spot
(value without position; the three positions are in the note), then the five tests with
their verdicts on A and B (none on the difference: the shape verdict at the top already
says whether the ratio is radial). The console prints them as a fixed-width table (label
26, columns 38) under `AgainstReport.Meaning()`, then "VERDICTUL DIFERENTEI" with its two
parts; the window shows them as `CompareRow`s: label and note in one elastic column on the
left, the three values in shared-size columns packed on the right. The map is the ratio's
residual on the automatic scale.

**The verdict** (`AgainstResult.Verdict`) is the worse of two: the *shape*, the five tests
on the ratio (`Structure.Verdict`), and the *depth*, `DepthVerdict`. The depth difference
`D = Structure.VignetteStops` (stops) is turned into the error it leaves in the corner of a
light calibrated with the wrong flat, `DepthErrorOf(D) = 2^|D| - 1` in percent - the light's
corner comes out `2^D` times too bright or too dark relative to its centre - and that goes
through the unchanged residual scale `Stats.ResidualVerdict`: PERFECT under 2 % (0.029
stops), BUN under 10 % (0.138), ACCEPTABIL under 20 % (0.263), REFA above. `StopsFor` is the
inverse (`log2(1 + e/100)`), used to print the scale in stops from `Th.Resid*`. Before this
the verdict was the shape alone, and the sky master (0.42 stops shallower than the model,
34 % in the corner) read ACCEPTABIL. The row's note also states the model's own
uncertainty: the two databases differ by ~0.2 stops on the same lens (section 8), so a
BUN/ACCEPTABIL depth is within what the profiles themselves disagree by. Exit code: the
combined verdict. Two correct flats of one lens give PERFECT and 0.00 stops; a flat
against `flat²` gives PERFECT on shape, REFA on depth (0.7 stops = 62 %) and REFA overall
(`SyntheticFlatTests`); the golden `against_radial` pair (0.45 vs 0.30 fixtures, 0.30 stops)
is REFA, exit 3.

| master vs Adobe synthetic (2026-09-18, camera crop 1.523) | stops | corner error | depth verdict |
|---|---|---|---|
| sky, no diffuser | −0.39 | 31.5 % | REFA |
| monitor close | +0.19 | 13.8 % | ACCEPTABIL |
| on monitor | +0.12 | 8.3 % | BUN |
| Adobe vs lensfun 7D2 entry | −0.18 | 13.6 % | ACCEPTABIL |
| Adobe vs lensfun D7100 entry | −0.25 | 18.9 % | ACCEPTABIL |

(With the profile's SFF 1.5 standing for the sensor, the 2026-09-17 figures were −0.42 /
+0.16 / +0.09 / −0.16: the 24 mm long side put the corner 1.5 % further out and the Adobe
synthetic 0.03 stops deeper.)

**lensfun has two entries for this lens**, and the figures above use the first:
`Tokina AT-X 11-20 F2.8 PRO DX Aspherical 11-20mm f/2.8` (`slr-tokina.xml`, crop 1.61,
"Taken with Canon EOS 7D M2", 50 profiles; at 20 mm f/2.8 `k = -0.6195, 0.3276, -0.1494`),
chosen with `--lens "Aspherical" --source lensfun`; it writes 0.85 / 0.56 / 0.26 / 0.24
stops. The second, `Tokina AF 11-20mm f/2.8 AT-X Pro DX` (crop 1.534, "Taken with Nikon
D7100", 100 profiles; at 20 mm f/2.8 `k = -1.0395, 0.9223, -0.3793`), writes
0.92 / 0.70 / 0.37 / 0.34. Measured 2026-09-17 on the sky master as reference:

| pair | stops | depth verdict |
|---|---|---|
| lensfun 7D2 entry vs lensfun D7100 entry | −0.06 | BUN |
| Adobe vs lensfun D7100 entry | −0.25 | ACCEPTABIL |
| sky master vs lensfun 7D2 entry | −0.58 | REFA |
| sky master vs lensfun D7100 entry | −0.64 | REFA |

So the three published models span 0.66-0.92 stops at the corner: the two lensfun
calibrations agree to 0.06, and Adobe is the shallow one by 0.18-0.25.

On the sky master against the Adobe synthetic (the numbers behind the user's question that
produced the table; re-run 2026-09-18): vignetting 0.27 / 0.66 / −0.39 stops; offset
2.58 / 0.10 / 0.36 %; local spot +0.28 / +0.01 / +0.36 %; corners 10.28 ACCEPTABIL / 0.00
PERFECT / 10.25; azimuthal 7.25 BUN / 0.09 PERFECT / 7.13; residual 9.31 BUN / 0.09
PERFECT / 9.31; tilt 0.49 / 0.20 / 0.53; worst tile 7.87 BUN / 0.10 PERFECT / 7.96; shape
verdict ACCEPTABIL, depth verdict REFA (31.5 %), verdict of the difference REFA. Reading:
the sky filled the corners by 0.39 stops and left its 10 % saddle; the synthetic is
radial (every test under 0.2 %), so every shape in the third column is the master's.

## 8. What the profiles say beside the real masters

Points as fractions of the half-width / half-height; masters measured as medians in 41-px
boxes at the analysis raster, averaged over the four quadrants. Percent of the centre,
stops in parentheses.

| point | Adobe LCP | lensfun (7D2 entry) | sky, no diffuser | monitor, close | on monitor |
|---|---|---|---|---|---|
| half semidiagonal | 82 % (0.28) | 85 % (0.23) | 92 % (0.13) | 78 % (0.36) | 79 % (0.35) |
| short edge | 81 % (0.31) | 84 % (0.26) | 95 % (0.08) | 79 % (0.34) | 76 % (0.40) |
| long edge | 70 % (0.51) | 68 % (0.56) | 85 % (0.24) | 63 % (0.67) | 62 % (0.68) |
| corner (5 % box) | 63 % (0.67) | 56 % (0.83) | 84 % (0.26) | 57 % (0.82) | 59 % (0.77) |

(Adobe column at the camera's crop 1.523, 2026-09-18; the lensfun D7100 entry reads 0.92
at the corner, section 7.) The sky master is 0.4-0.6 stops too flat everywhere; the monitor
masters match the databases at the corner and fall 0.1 stops too fast at mid-field (the
panel). The two databases differ by 0.16-0.25 stops at the corner, 0.05 at mid-field - the
precision one can expect of "official" data.

**Two calibrations of one lensfun lens can differ more than the two databases do.** The
AF-S DX Nikkor 35 f/1.8G, evaluated with the polynomials at the D3300 corner (14.20 mm),
stops, 2026-09-18:

| aperture | Adobe (D90, 1.51 m) | lensfun, D780 entry (crop 1) | lensfun, "average APS-C" entry (crop 1.528) |
|---|---|---|---|
| f/1.8 | 1.21 | 1.17 | 0.51 |
| f/2.8 | 0.61 | 0.51 | 0.19 |
| f/4 | 0.39 | 0.35 | 0.16 |
| f/5.6 | 0.23 | 0.28 | 0.17 |
| f/22 | 0.23 (f/11) | 0.23 | 0.17 |

Two independent calibrations on different bodies agree within 0.1 stops at every aperture;
the third gives half the wide-open falloff and a floor that does not move from f/4 to
f/22, and it was the only one the tool showed until the calibration crop entered the lens
key (section 5). Adobe's own file says where that floor comes from: at 0.36 m focus the
D90 profile reads 0.76 / 0.54 / 0.18 / 0.17-0.19 stops at f/1.8 / 2.8 / 4 / 5.6-11, against
1.21 / 0.61 / 0.39 / 0.23 at 1.51 m - this lens loses most of its mechanical vignetting when
focused close, and the "APS-C" entry's floor is the close-focus one. Its `distance` is the
copied 10 / 1000 pair, i.e. not recorded, and the tool now says so instead of "infinit".
The D780 entry has the same copied pair but matches Adobe at 1.51 m, so that contributor
shot from afar. What close focus does not explain (f/2.8 at 0.19 against 0.54) points at a
source that fills the corners or at in-camera vignette control left on; the two cannot be
told apart from the coefficients. The sky master of that lens (`Nikkor 35mm DX/f2.8-sky/ISO400`) reads 0.63
with the 5 % box operator; with the box the tool reports Adobe 0.52, D780 0.42, APS-C 0.16,
so Adobe stays nearest (+0.11, BUN) and the D780 entry lands at +0.21 (ACCEPTABIL). Since a
sky source fills the corners, the true depth is at least the master's, which rules the
0.16 entry out on its own. A single sky flat at f/1.8 would separate the two readings by
0.7 stops, far beyond any source bias.

## 9. Acceptance, with the tool itself

`--synthetic` on the sky master as reference (6016×4016, FOCALLEN 20, `--fstop 2.8`),
then `--against`:

| pair (2026-09-18) | vignetting of the ratio | shape verdict | depth verdict → verdict |
|---|---|---|---|
| monitor close / Adobe synthetic | +0.19 stops | BUN (the monitor's own structure) | ACCEPTABIL → ACCEPTABIL |
| sky / Adobe synthetic | −0.39 stops | ACCEPTABIL (the sky saddle, 10.3 % corners) | REFA → REFA |
| on monitor / Adobe synthetic | +0.12 stops | ACCEPTABIL (its blotches) | BUN → ACCEPTABIL |
| Adobe synthetic / lensfun 7D2 synthetic | −0.18 stops | PERFECT, 0.06 % residual | ACCEPTABIL → ACCEPTABIL |

Depth written by the synthetics (corner / long edge / short edge / half semidiagonal,
stops): Adobe 0.67 / 0.51 / 0.31 / 0.28 (crop line "1.523 (baza lensfun, Nikon D3300)");
lensfun 7D2 entry 0.85 / 0.56 / 0.26 / 0.24; lensfun D7100 entry 0.92 / 0.70 / 0.37 / 0.34.

```bash
flatcheck --lenses --lens "Tokina 11-20"
flatcheck "<sky master>" --synthetic s_adobe.fit --lens "Tokina atx-i 11-20mm f2.8 CF" --fstop 2.8
flatcheck "<sky master>" --synthetic s_lensfun.fit --lens "Tokina AT-X 11-20 F2.8 PRO DX Aspherical 11-20mm f/2.8" --source lensfun --fstop 2.8
flatcheck "<monitor close master>" --against s_adobe.fit
flatcheck s_adobe.fit --against s_lensfun.fit
```

Expected: the depth lines above and the four ratio figures within ±0.02 stops.

## 10. Costs and facts measured

- Adobe profiles on this machine: 3531 `.lcp` files, 455 MB; parsing all: 36 s; listing
  from names + lensfun: `--lenses` in 2.2 s. 801 files lack the ` - RAW` suffix, 638 carry
  no parentheses.
- lensfun `data/db`: 59 files, 5.19 MB, 0.58 MB gzipped; committed whole, since lens and
  camera entries share files. `lensfun/update-lensfun.py` refreshes it and rewrites
  `LENSFUN-VERSION.txt`.
- libraw on a D3300 NEF with the Tokina (`DSC_0001.NEF` of the 2026-09-16 sky session,
  dumped by reflection over `RawContext.LensInfo`): `Lens` and `LensMake` empty,
  `ImageOtherParams.FocalLength` 20, `Aperture` 2.8, `LensInfo.Nikon.LensIDNumber` 139,
  `LensFStops` 72, `MCUVersion` 133, `LensType` 6; the top-level `MinFocal` / `MaxFocal` /
  `MaxAperture4MinFocal` **0**; `LensInfo.MakerNotes.MinFocal` 11, `MaxFocal` 20,
  `MaxAperture4MinFocal` 2.8, `MaxAperture4MaxFocal` 2.8, `CurrentFocal` 20, `LensMount`
  27, `LensFeaturesPre` "AF", `LensFeaturesSuffix` "G", `LensID` 10036302762685531398 (the
  8-byte Nikon composite; not used - Adobe carries the 1-byte number). The Adobe profile
  lists 139 in `AlternateLensIDs`, and so do the two Nikkor 18-200 files and the Sigma
  18-35 (`grep -l` over the folder). The Samyang (manual) and the Nikkor 35 DX give IDs 74
  and 159.
- **The two Adobe Tokina 11-20 files are one optic.** `NIKON CORPORATION (Tokina AT-X
  11-20mm F2.8 PRO DX) - RAW.lcp` and `... (Tokina atx-i 11-20mm f2.8 CF) - RAW.lcp`
  (both dated 2026-04-06) differ only in the names and the IDs: every `VignetteModelParam`,
  `FocalLength` and `ApertureValue` hashes identically (`md5sum` over the extracted
  attributes), and a `diff` with the names masked shows nothing else. The AT-X file has
  `LensID` 122 with 139 in `AlternateLensIDs`; the atx-i file has `LensID` 139. The
  author's raw reports 139, so the copy is the atx-i (Tokina's 2020 rebadge of the 2015
  AT-X, same optical formula). The lensfun entries are named for the AT-X and apply to it.
- The test fixtures (`LensProfileTestData`) are a hand-written LCP in the attribute form
  (three entries: 20 mm f/2.8 with the piecewise table, 20 mm f/4, 18 mm decentred) on two
  mounts and a lensfun file with the Tokina, a lens without vignetting and the D3300
  camera; the golden files `synthetic_written`, `synthetic_lensfun`,
  `synthetic_ambiguous`, `lenses`, `against_radial` pin the console output on them.

## 11. The master against the profiles, inside the structure mode

Code: `Analysis/VignettingComparison.cs`, `LensProfiles/LensIdentification.cs`,
`Runs/StructureRun.cs` (`StructureQuery`, `StructureOutcome`), the profile block of
`Report/StructureReport.cs`; window `StructureTabViewModel` + `Session/ProfileCatalog.cs`.
Measured 2026-09-18 on the library below with the Debug build, `--raw` on the first raw of
each session's `flats/`.

**Why.** The tablet masters (section 8 of `tokina-tablet-flats` in the author's notes:
Tokina 20 mm f/2.8, four orientations at 90°, 7 or 15 frosted binder covers on a tablet)
read PERFECT on all five structure tests with 1.26 stops of vignetting, against 0.66-0.92
from the two databases. The five tests measure departure from radial symmetry; a radial
excess is invisible to them and to `--ratio`. The lens profile is the only external
reference the tool has.

**How.** For each candidate lens (`LensIdentification`: the raw's Nikon ID narrowed by the
MakerNote range and the camera's mount, the lensfun lenses of the same makers; or, when the
ID matches nothing, the lenses of the mount whose *name* says exactly the raw's range - the
manual-lens case), the nearest profile (`LensCatalog.Nearest`) is built as a raster at the
analysis size and measured with `Metrics.CornerStats`, the master's own operator. On a
master that is the model the difference is 0.00; the residual is the master's 2x2 block
mean, under 0.001 stops (`TheProfilesDepthIsMeasuredWithTheMastersOwnOperator`). The
difference `master - profile` in stops goes through `Stats.DepthErrorPct` onto the residual
scale (section 7's arithmetic, moved into `Stats`). The verdict is the nearest profile's;
the run's verdict is the worse of shape and depth. The scale is printed both ways - the
one-line form in stops (`AgainstReport.DepthScale`) and, in the window, four bands with the
verdict's marked (`StructureReport.DepthScaleRows`) - each followed by the sentence that it
is the residual scale translated into stops, not a new threshold: the first screen showed
the stops line right above the five tests' 2 / 10 / 20 % table and the user asked whether
they were two different scales.

**Measured** (corner, stops; "nearest" is the profile that gives the verdict):

| session | master | Adobe | lensfun | nearest | diff | depth | shape | run | exit |
|---|---|---|---|---|---|---|---|---|---|
| Tokina tablet, 7 sheets | 1.26 | 0.66 | 0.91 / 0.85 | lensfun D7100 | +0.34 | REFA | PERFECT | REFA | 3 |
| Tokina tablet, 15 sheets | 1.24 | 0.66 | 0.91 / 0.85 | lensfun D7100 | +0.33 | REFA | BUN | REFA | 3 |
| Tokina sky, no diffuser | 0.27 | 0.66 | 0.91 / 0.85 | Adobe | −0.39 | REFA | ACCEPTABIL | REFA | 3 |
| Tokina monitor, close | 0.85 | 0.66 | 0.91 / 0.85 | lensfun 7D2 | 0.00 | PERFECT | BUN | BUN | 0 |
| Tokina on monitor | 0.78 | 0.66 | 0.91 / 0.85 | lensfun 7D2 | −0.07 | BUN | ACCEPTABIL | ACCEPTABIL | 2 |
| Nikkor 35 DX sky ISO400 | 0.63 | 0.52 (1.51 m) | 0.30 / 0.16 | Adobe | +0.11 | BUN | BUN | BUN | 0 |
| Nikkor 35 DX sky ISO800 | 0.62 | 0.52 | 0.30 / 0.16 | Adobe | +0.10 | BUN | ACCEPTABIL | ACCEPTABIL | 2 |
| Samyang 135 f/2 sky ISO400 | 0.61 | Nikkor DC 0.50 | Samyang 0.46 | Adobe (Nikkor) | +0.11 | BUN | BUN | BUN | 0 |
| Samyang 135 f/2 sky ISO1600 | 0.60 | Nikkor DC 0.50 | Samyang 0.46 | Adobe (Nikkor) | +0.10 | BUN | ACCEPTABIL | ACCEPTABIL | 2 |
| Samyang 135 f/2.8 sky | 0.15 | Nikkor DC 0.12 | Samyang 0.14 | lensfun Samyang | +0.01 | PERFECT | ACCEPTABIL | ACCEPTABIL | 2 |

Readings:

- The two tablet masters are the case the mode was built for: the only REFA that the five
  tests did not see. Against Adobe they are +0.59 / +0.58 (50 %); the nearest rule takes
  the deeper lensfun entry and still reads REFA.
- The sky master is 0.4 stops too shallow against every profile (−0.39 to −0.64); the sign
  reads "the source fills the corners", which is section 8's finding.
- The two monitor masters land on the lensfun 7D2 entry: PERFECT and BUN. "Worst" would
  have made them ACCEPTABIL / BUN against Adobe (0.19 / 0.11) - the reason the verdict is
  the nearest profile's.
- The Nikkor 35 f/1.8G's Adobe file has only a 1.51 m profile at f/2.8 (`Nearest` prefers
  the farthest distance among equal focal and f-stop, and there is only one); the two
  lensfun entries for it disagree with each other by 0.14 stops and with Adobe by 0.2-0.4.
- The Samyang is a manual lens: the D3300 writes the "non-CPU lens" ID 74 and the range set
  in its menu (135 mm f/2.0), so the candidates come by range and include the Nikkor 135 f/2
  DC's Adobe file, which happens to be nearest on the two f/2 masters. The block says the
  list is by range and to read one's own lens's row (+0.15 / +0.14 against the lensfun
  Samyang: ACCEPTABIL). At f/2.8 both agree within 0.03 stops.
- Shape and depth are independent: the tablet master is PERFECT on shape and REFA on
  depth, the on-monitor one the reverse.

**What was left out, and why.** A mid-field point (half semidiagonal) as a second
informative column would show the panel's 0.1-stop mid-field excess of section 8; it needs
a new box measurement on the master and was deferred (user's decision). A "worst profile"
rule, for the reason above. A lens picker in the window for a folder without a raw: the
raws are always in `flats/` (user's decision); the card explains, the console takes
`--lens`.

## 12. Known limits, for the next change

- Achromatic by construction; the real falloff is chromatic (R deeper than B by ~25 % of
  the falloff on the sky master). A per-channel synthetic would need per-channel data no
  database has.
- No interpolation between profiles; "nearest" may sit a focal length off, and the report
  says so. If interpolation is ever wanted, do it in stops for the aperture and linearly
  in focal length, and keep reporting both neighbours.
- The camera's crop, not the pixel pitch, sets the frame's physical size (36 / crop for
  the long side); a body whose sensor is not 3:2 of that width is off by the ratio. A
  profile calibrated on another format is rescaled through mm and works inside its
  calibration corner; past it the polynomial extrapolates, and past 1.1x the report says
  it is not to be trusted - the fix there is a profile calibrated on the right format
  (Adobe's per-body files, which `PreferCamera` picks when one exists).
- A camera missing from the lensfun table gets its crop from a sensor-format chip or by
  hand; the nominal crops are within 1-2 % of the real ones. The chips otherwise only show
  the format of the crop in the box; the exact value stays the table's.
- Aliases of unparsed Adobe stubs are not searchable; the name from the file is.
- The window prefill reads the first raw in `flats/` in full (libraw) once per session,
  after the opening chain; a raw libraw cannot open leaves the f-stop empty and says so.
- The window's comparison follows the write by itself (`SyntheticTabViewModel.RunAsync`
  chains `CompareAsync(Report)` after a real write; the report is one text with one
  banner). "Compara cu masterul" runs it alone, for a file written earlier.
