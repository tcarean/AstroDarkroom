# Synthetic flats from lens vignetting profiles: specification and evidence

Covers `FlatCheck.Core/LensProfiles/` (`VignettingModel`, `LensProfile` / `LensCatalog` /
`AdobeProfileFile`, `LcpReader`, `LensfunReader`, `ProfileLocator`),
`Analysis/SyntheticFlatBuilder.cs`, `Report/SyntheticReport.cs`, `Runs/SyntheticRun.cs`,
`Runs/AgainstRun.cs`, the `--synthetic` / `--lenses` / `--against` modes in
`FlatCheck/Options.cs` and `FlatCheckCli.cs`, the window's `SyntheticTabViewModel` /
`SyntheticView`, the `lensfun/` folder, and the shooting metadata added to `LoadedFrame`.
Measured 2026-09-17 on the author's machine (Windows 10, Lightroom Classic installed,
lensfun `data/db` at upstream commit `e34596cb52`, 2026-09-16) and on the masters under
`E:\Photografie\Astro\Flats and Darks\`. If the model code changes, re-run section 9 and
update the numbers.

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

- **Adobe**: `fx = FocalX ?? FocalMm · SFF / 36`, `fy = FocalY ?? fx`,
  `xd = (xN + 0.5 − CenterX) / fx`, `yd = (yN + 0.5 − CenterY) / fy`, `r = hypot(xd, yd)`,
  `V = 1 + a1 r² + a2 r⁴ + a3 r⁶`; with a table instead of coefficients, linear
  interpolation in r, clamped at both ends. The camera's crop is not used: the profile's
  SFF stands for the sensor (Adobe's own assumption; on a D3300 the 24 mm long side that
  SFF 1.5 implies puts the corner at r = 0.72 instead of 0.705, i.e. 0.69 stops instead
  of 0.66 at the corner box).
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
- **Lens choice from text** (`SyntheticRun.ChooseLens`): search hits; an exact name match
  narrows to those; when several remain and the reference frame names a camera, the ones
  whose mount or maker contains the camera's first word win; still several → refusal with
  the list, exit 2. No text + a Nikon lens ID from a raw → `SyntheticRun.CandidatesFromRaw`:
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
`INSTRUME` / libraw model; needed only for lensfun), each with its source string for the
report. Refusals: a colour cube, no lens chosen (exit 2), no focal / f-stop / crop (exit 1).
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

| master vs Adobe synthetic | stops | corner error | depth verdict |
|---|---|---|---|
| sky, no diffuser | −0.42 | 33.6 % | REFA |
| monitor close | +0.16 | 11.7 % | ACCEPTABIL |
| on monitor | +0.09 | 6.4 % | BUN |
| Adobe vs lensfun synthetic | −0.16 | 11.7 % | ACCEPTABIL |

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
| Adobe vs lensfun D7100 entry | −0.22 | ACCEPTABIL |
| sky master vs lensfun 7D2 entry | −0.58 | REFA |
| sky master vs lensfun D7100 entry | −0.64 | REFA |

So the three published models span 0.69-0.92 stops at the corner: the two lensfun
calibrations agree to 0.06, and Adobe is the shallow one by 0.16-0.22.

On the sky master against the Adobe synthetic (the numbers behind the user's question that
produced the table): vignetting 0.27 / 0.69 / −0.42 stops; offset 2.58 / 0.10 / 0.35 %;
corners 10.28 ACCEPTABIL / 0.00 PERFECT / 10.26 ACCEPTABIL; azimuthal 7.25 / 0.09 / 7.13;
residual 9.31 / 0.10 / 9.37; tilt 0.49 / 0.20 / 0.53; worst tile 7.87 / 0.11 / 8.02;
shape verdict ACCEPTABIL, depth verdict REFA (34 %), verdict of the difference REFA.
Reading: the sky filled the corners by 0.42 stops and left its 10 % saddle; the synthetic
is radial (every test under 0.2 %), so every shape in the third column is the master's.

## 8. What the profiles say beside the real masters

Points as fractions of the half-width / half-height; masters measured as medians in 41-px
boxes at the analysis raster, averaged over the four quadrants. Percent of the centre,
stops in parentheses.

| point | Adobe LCP | lensfun | sky, no diffuser | monitor, close | on monitor |
|---|---|---|---|---|---|
| half semidiagonal | 82 % (0.28) | 85 % (0.23) | 92 % (0.13) | 78 % (0.36) | 79 % (0.35) |
| short edge | 81 % (0.30) | 84 % (0.26) | 95 % (0.08) | 79 % (0.34) | 76 % (0.40) |
| long edge | 70 % (0.51) | 68 % (0.56) | 85 % (0.24) | 63 % (0.67) | 62 % (0.68) |
| corner (5 % box) | 63 % (0.66) | 56 % (0.83) | 84 % (0.26) | 57 % (0.82) | 59 % (0.77) |

The sky master is 0.4-0.6 stops too flat everywhere; the monitor masters match both
databases at the corner and fall 0.1 stops too fast at mid-field (the panel). The two
databases differ by 0.17 stops at the corner, 0.05 at mid-field - the precision one can
expect of "official" data.

## 9. Acceptance, with the tool itself

`--synthetic` on the sky master as reference (6016×4016, FOCALLEN 20, `--fstop 2.8`),
then `--against`:

| pair | vignetting of the ratio | verdict of the ratio |
|---|---|---|
| monitor close / Adobe synthetic | +0.16 stops | BUN (7.0 % residual: the monitor's own structure) |
| sky / Adobe synthetic | −0.42 stops | ACCEPTABIL (the sky saddle, 10.3 % corners) |
| on monitor / Adobe synthetic | +0.09 stops | ACCEPTABIL (its blotches) |
| Adobe synthetic / lensfun synthetic | −0.16 stops | PERFECT, 0.05 % residual |

Depth written by the two synthetics (corner / long edge / short edge / half semidiagonal,
stops): Adobe 0.69 / 0.52 / 0.31 / 0.29; lensfun 0.85 / 0.56 / 0.26 / 0.24.

```bash
flatcheck --lenses --lens "Tokina 11-20"
flatcheck "<sky master>" --synthetic s_adobe.fit --lens "Tokina AT-X 11-20mm F2.8 PRO DX" --fstop 2.8
flatcheck "<sky master>" --synthetic s_lensfun.fit --lens "Aspherical" --source lensfun --fstop 2.8
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

## 11. Known limits, for the next change

- Achromatic by construction; the real falloff is chromatic (R deeper than B by ~25 % of
  the falloff on the sky master). A per-channel synthetic would need per-channel data no
  database has.
- No interpolation between profiles; "nearest" may sit a focal length off, and the report
  says so. If interpolation is ever wanted, do it in stops for the aperture and linearly
  in focal length, and keep reporting both neighbours.
- Adobe's SFF stands for the sensor; a camera whose long side differs from `36 / SFF`
  (23.5 vs 24 mm here) shifts r by that ratio. Using the real sensor width would need
  the pixel pitch, which a Siril FITS does not carry for a DSLR.
- Aliases of unparsed Adobe stubs are not searchable; the name from the file is.
- The window prefill reads the first raw in `flats/` in full (libraw) once per session,
  after the opening chain; a raw libraw cannot open leaves the f-stop empty and says so.
- The window's comparison follows the write by itself (`SyntheticTabViewModel.RunAsync`
  chains `CompareAsync(Report)` after a real write; the report is one text with one
  banner). "Compara cu masterul" runs it alone, for a file written earlier.
