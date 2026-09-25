# Mode 7: the synthetic flat from the lens profile (--synthetic, --lenses, --against)

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `LensProfiles/`, `Analysis/SyntheticFlatBuilder.cs`, `Report/SyntheticReport.cs`, `Report/AgainstReport.cs`, `Runs/SyntheticRun.cs`, `Runs/AgainstRun.cs`, the Sintetic tab; the model definitions and acceptance figures are in `lens-profiles.md`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 7. Synthetic flat - `flatcheck ref.fit --synthetic out.fit --lens <text>`, `--lenses`, `--against`

Not a test: it **writes a radial flat from the lens's published vignetting profile** -
the lens's model, not a measurement of the user's copy - and `--against` reads how much
and in what shape a real flat differs from it (or from any other flat). Built after the
Tokina 20 mm session showed that no illumination available here gives the vignetting
depth to better than half a stop: sky 0.27 stops at the corner (the dome brightens
toward the horizon all around the zenith and fills the corners), monitor up close
0.78-0.85 (the panel's angular falloff), the two public databases 0.66 (Adobe) and 0.83
(lensfun). `docs/lens-profiles.md` is the evidence file.

**Two sources, one list, both read by our own code** (no new package - `System.Xml.Linq`):

- **Adobe LCP**, from the machine only, never copied:
  `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0\**` and
  `%APPDATA%\Adobe\CameraRaw\LensProfiles`; `--profiles <dir>` replaces both (the tests run
  on hand-written XML that way, `LensProfileTestData`). The model, from the "Adobe Camera
  Model" document (Vignette Model, p. 8; descriptors, p. 18): `V = 1 + a1 r² + a2 r⁴ + a3 r⁶`,
  `r² = ((u-u0)/fx)² + ((v-v0)/fy)²` with `FocalLengthX = fx / Dmax`, `ImageXCenter = u0 / Dmax`,
  and when `FocalLengthX` is absent the nominal focal length in mm - so with the sensor's
  long side at `36 / SensorFormatFactor` mm, `fx / Dmax = f · SFF / 36`: **r is the distance
  in mm over the focal length in mm.** Cross-checked inside the same file: the
  `VignetteModelPiecewiseParam` tables end at r = 0.7094, which is the DX corner
  (14.1 mm / 20 mm), where the table reads 0.603 and the polynomial 0.597. "These model
  parameters are identical for all color channels" - the model is achromatic, and the
  report says so. Aperture is APEX (`f = 2^(Av/2)`). Some profiles are decentred
  (`ImageXCenter` 0.4938 on the 14-20 f/2) and the model honours it.
- **lensfun**, the `pa` model: same polynomial, r normalised to the half-diagonal of a
  36x24 frame divided by the lens entry's `<cropfactor>` (`mod-color.cpp`:
  `hugin_scale_in_millimeters = hypot(36,24)/CropFactor/2`, then rescaled to focal units
  by `(f / that)^(2,4,6)`; `modifier.cpp:79` normalises everything to focal units). So it
  needs the user's camera crop, taken from lensfun's own `<camera>` entries by `INSTRUME`
  (D3300: 1.523) or from `--crop`; on a body with a different crop the corner can sit past
  r = 1 (1.06 here) and the report says "extrapolare mica". The polynomial *is* the
  vignetting factor in both (lensfun multiplies to apply, divides to correct).
- **The camera's crop sets the frame's physical size for Adobe too.** The first cut took
  the profile's `SensorFormatFactor` - the calibration body - for every camera: 1.5 % off
  on a D3300 with the DX file (1.523 vs 1.5; the Adobe synthetic read 0.69 stops instead of
  0.67), the whole corner off on a full frame with a DX-calibrated file, silently. Now
  everything goes through mm (`36 / crop` for your frame, `36 / SFF` for the profile's
  focal-in-pixels and its decentring, see `VignettingModel`'s header), the SFF is the
  fallback when the crop is unknown and the report says so, a crop more than 3 % from the
  SFF prints "profil calibrat pe alt format", and `SyntheticFlatResult.Extrapolation`
  (corner r over `CalibrationCornerRadius`) prints "extrapolare MARE" above 1.1 - a
  sixth-order polynomial 50 % past its data goes negative, so a DX file on a full frame is
  refused in words, not numbers - all of it in one sentence, `SyntheticReport.FormatMatch`
  ("profil calibrat pe APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format",
  or "alt format, raza se recalculeaza prin mm; coltul tau cade la 1.50 x coltul de
  calibrare: extrapolare MARE"), printed by the console at "format" and shown by the tab
  when a lens is picked and again under the written depths. Adobe's own piecewise table ends at 14.19 mm, the corner
  of a 23.64 mm sensor (crop 1.523), which is the evidence the real crop is what Adobe
  used. `SyntheticRun.PreferCamera` picks a per-body Adobe file ("NIKON D750 (lens)") over
  the mount's generic one when the reference names that body, since those carry the
  body's SFF. Console and window agree, and the tab's chips are only for a body the
  lensfun table lacks: the note under the fields says so and the chosen chip's crop lands
  in the box.

**Decisions.** Nearest profile, reported, no interpolation: nearest focal, then nearest
f-stop in stops, then the farthest distance (flats are at infinity); the report prints
"cerut" and "profil folosit". **A request far from every profile is not refused, it is
warned about** (`SyntheticReport.ProfileMismatch`: the focal more than `Th.ProfileFocalTolMm`
/ `Frac` from the profile's, or the aperture more than `Th.ProfileStopsTol` = 0.5 stops
away): 500 mm f/1.0 on the Nikkor 35 f/1.8 silently became 35 mm f/1.8 with "cerut" as the
only sign. The sentence names what was asked, what was taken and the lens's range, and
that the written depth is the profile's; the console prints it under "profil folosit", the
Sintetic tab shows it under the fields while typing (`RequestNote`, computed with
`FormatNote`) and under "profil folosit" after the write (`WrittenRequestNote`), and the
structure comparison appends it to the row's note with `IsWarning` when the raw's focal /
f-stop is far from the lens's profiles. Half a stop because Adobe's grid is at whole
stops: anything inside the range is under it, and only requests outside the range fire.
Exit codes do not change. `SyntheticFlatResult.LensProfiles` and `ProfileDepth.LensProfiles`
carry the lens's whole profile list so the sentence can state the range. The same lens
exists per mount in Adobe, so the mount is
chosen by the reference frame's camera maker word (`INSTRUME` / libraw) and an exact
name match beats partial ones; what still ties is listed and the exit code is 2. Focal
from `FOCALLEN` or libraw, f-stop from libraw only (Siril writes no f-number) or
`--fstop`; `LoadedFrame` grew `FocalMm`, `FNumber`, `Camera`, `NikonLensId` for it, filled
by both readers (`FitsReader.LoadHeader` reads the header alone, for the window's prefill
on a 96 MB master). libraw gives no lens *name* on a D3300 with a third-party lens, but it
gives the Nikon lens ID (139 for the Tokina 11-20), which Adobe's profiles carry in
`AlternateLensIDs`, and - in `LensInfo.MakerNotes`, where the top-level `MinFocal` /
`MaxFocal` are zero - the lens's range, 11-20 mm f/2.8. **The ID alone is not enough**: 139
is also the Nikkor 18-200 f/3.5-5.6 and the Sigma 18-35 f/1.8, and the first cut took the
alphabetically first hit, a Nikkor, for a raw shot with the Tokina. `LensSpec.Parse` reads
the range out of a lens *name* ("11-20mm F2.8", "18-200mm f3.5-5.6G", "135mm f/2.0";
nothing out of "Zeiss Batis 2/25", which then contradicts nothing) and
`LensCatalog.Narrow` drops the hits that contradict the raw (1 mm or 5 % on a focal, a
quarter stop on the aperture). Only Adobe files carry Nikon IDs, so the lensfun lenses of
the same maker(s) as the surviving hits, narrowed by the same range, join the candidates -
without that the narrowed list showed the Adobe half only and the user asked where the
lensfun Tokina had gone. `SyntheticRun.CandidatesFromRaw` is the one place both
applications start from; the window then preselects a single survivor and, when several
survive (the two Adobe generations of the Tokina 11-20 and its two lensfun entries),
narrows the list to them and says so (`OfferCandidates`) rather than picking one. For
this lens the choice is moot: the atx-i 11-20 F2.8 CF (2020) is the AT-X 11-20 F2.8 PRO
DX (2015) in a new barrel, Adobe's two files carry byte-identical vignetting parameters
(checked), and the raw's primary ID 139 is the atx-i file's `LensID` while the AT-X file
has 122 with 139 as an alternate - so the user's copy is the atx-i. Output: float, 1.0 on the
axis, the reference frame's cards minus the structural ones (`BAYERPAT` included, so Siril
takes it as a CFA master), `HISTORY` naming source, lens, profile and model; nothing
renormalised. Exit 0 written, 1 refused (cube, or focal / f-stop / crop unknown), 2 the
lens could not be chosen. `--lenses` lists lenses with focals, apertures and distances
(`SyntheticReport.LensDetails`: metres ascending, then "infinit", then "nespecificata" for
the collapsed lensfun pair, plus the profile count) only when the filtered list is at most
`SyntheticReport.ListDetailsMax` (40) long, because the details need the Adobe file parsed.
The Sintetic tab shows the same line under each row, by the same rule: `LensRow.Details`
is filled in the background (`RefreshDetailsAsync`, generation-counted, parallel over the
visible rows, `DetailsPending` for the tests), and on a longer list the rows stay bare and
`ListNote` says why (`SyntheticReport.ListDetailsHidden`). lensfun rows would be free to
fill on any list, and are deliberately held to the same rule so a long list is not half
with details and half without.

**`--against`** (`AgainstAnalyzer`, `AgainstRun`, `AgainstReport`): both frames prepared,
each normalised to median 1, and **three** structure analyses - A, B and the ratio A / B -
printed as a table of three columns (`AgainstReport.Rows`: vignetting in stops, illumination
offset, local spot, then the five tests with their verdicts). The first version showed the
battery on the ratio alone, and "vignetare -0.42 stopuri" could not be read without knowing
that the master has 0.27 and the synthetic 0.69 (0.66 since the crop correction); the user asked for the three values side by
side. In the third column the figures are about the *difference*: the vignetting is the
depth difference (positive = A falls more toward the corner), the five tests say whether the
difference is radial, the map shows its shape. The rows carry their own notes
(`AgainstReport.TestNote`), written about the difference - the Structure tab's notes said
"a property of the lens, not an error" under a row that here *is* the error. Not `--ratio`,
which separates camera-fixed from world-fixed by rotation.

**The verdict of the difference has two parts, and the exit code follows the worse.** The
shape is the five tests on the ratio. The depth is the vignetting difference in stops, and
it got a verdict after the sky master - 0.42 stops shallower than the model, "clearly
enormous" to the user - came out ACCEPTABIL on shape alone. No new threshold: a difference
of D stops leaves `2^|D| - 1` of error in the corner of a light calibrated with the wrong
flat (`AgainstResult.DepthErrorOf`), a fractional amplitude like every other figure, so it
goes through `Stats.ResidualVerdict` unchanged - under 0.03 stops PERFECT, 0.14 BUN, 0.26
ACCEPTABIL, else REFA (`AgainstReport.DepthScale`, derived from `Th.Resid*` by
`StopsFor`, never written out). On the Tokina masters (2026-09-18, Adobe at crop 1.523):
sky -0.39 → 31.5 % REFA, close monitor +0.19 → 13.8 % ACCEPTABIL, on-monitor +0.12 → 8.3 %
BUN, Adobe vs lensfun 0.18-0.25 → ACCEPTABIL; the row's note says that under ACCEPTABIL the
difference is within what the two databases disagree by (~0.2 stops). The depth row is the only one with a verdict in the
difference column - the five tests carry theirs on A and B, and "PERFECT next to 0.11 % on a
ratio" said nothing the shape verdict at the top does not. The golden `against_radial`
pair is 0.30 stops apart, hence REFA and exit 3 there. The window shows the same rows
through `CompareRow` (public): label and note on the left in the one elastic column, the
three values in shared-size `Auto` columns packed on the right - the first cut gave the
values the width and let the note run under them. The four depth rows of the written flat
carry notes too (`SyntheticReport.DepthRows`; the console prints label and value only),
through their own `DepthRowTemplate` in `SyntheticView.xaml` - label and wrapping note in
the left column, the value alone on the right - because the shared `MetricRowTemplate`
runs the note under the value, which is what the structure tabs want and this one does not.

**Measured on the Tokina masters against the Adobe synthetic** (corner, stops, 2026-09-18
with the camera's crop): the close-monitor master +0.19, the sky master -0.39, the
on-monitor master +0.12; Adobe against the lensfun 7D2 entry -0.18 with a PERFECT ratio
(both radial), against the D7100 entry -0.25. These are the acceptance numbers of the mode,
and they say which real master was wrong (the sky one, by 0.4 stops) and by how much the
panel adds at mid-field (0.1).

**What the report says it cannot contain:** the chromatic part of the falloff (on the sky
master R falls ~25 % deeper than B), body-specific shading, dust, any non-radial
structure, the user's own copy; and that the two databases differ by ~0.2 stops on the
same lens. Pinned by `VignettingModelTests` (the piecewise cross-check, the decentred
model, the lensfun normalisation at r = 1), the reader tests on hand-written XML, the
catalog tests, `SyntheticFlatTests` (a synthetic is PERFECT for the structure battery;
against itself it is PERFECT at zero stops), `SyntheticRunTests`, and the golden files
`synthetic_written`, `synthetic_lensfun`, `synthetic_ambiguous`, `lenses`, `against_radial`.

**The window's Sintetic tab** (`SyntheticTabViewModel`, `SyntheticView`): a searchable
list with a source chip and the mount per row, focal / f-stop / crop text boxes prefilled
from the master's header and from the first raw in `flats/` (after the opening chain, in
the background; a raw libraw cannot open leaves the f-stop empty and says so), the
sources panel **collapsed by default** (it is documentation, not a precondition: where the
Adobe profiles are, which lensfun copy is in use with its commit and date, how to replace
it - `SyntheticReport.AdobeWhere` / `LensfunWhere`, so console and window say the same),
the write button on the Netezire pattern (waits for idle, asks before overwriting, `Saved`
event, never the session's master: `SirilSession.SyntheticMasterPath`), and the comparison
- `AgainstRun` on the master and the file just written: the verdict, the three-column rows
and the map on a switchable scale. **The comparison follows the write by itself**
(`RunAsync` chains `CompareAsync(Report)`, which appends to the write's report instead of
starting a second banner) after a real write - not after a refused overwrite, whose old
file may be another lens's, and not after a write error. The first cut needed a second
click, and the screen after "Scrie" - depth table, no comparison - was reported as a bug.
"Compara cu masterul" stays for a file written in an earlier session. The catalog is located once, in the
background, on the first `Attach` (`Func<ProfileSources>` in the `MainViewModel`
constructor for the tests). `SyntheticTabTests` and two smoke renders pin it.

