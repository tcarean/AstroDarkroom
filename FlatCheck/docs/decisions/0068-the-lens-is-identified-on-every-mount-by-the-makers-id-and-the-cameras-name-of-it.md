# 0068. The lens is identified on every mount: the maker's ID by mount, the camera's name of the lens as a second key, Pentax pairs as libraw reads them

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0030 in part (the ID read from Nikon's field only, the ID as the one key)

## Context

ADR 0030 identifies the lens from the raw's Nikon ID (`LensInfo.Nikon.LensIDNumber`) narrowed by the
MakerNote's range; on every other make the raw carried no lens for FlatCheck, and the raw of a Canon
with an EF lens even read as "says nothing about the lens" (ADR 0065 left it for a task with a
measured raw). The owner asked for the identification to work on Canon, Sony, Pentax and the rest,
with the measurement first.

Measured on 2026-10-26 with `Sdcb.LibRaw` 0.21.1.7, the project's package, on four CC0 samples of
raw.pixls.us (kept as validation data, V-041 to V-044) and the owner's D3300 raw; the whole table is
in `docs/plans/2026-10-26-identify-the-lens-on-every-mount.md`:

- `LensInfo.MakerNotes.LensID` holds the maker's lens type on a Canon (198 = EF 50mm f/1.4 USM, 155
  = EF 85mm f/1.8 USM), a Sony (32831 = FE 16-35mm F2.8 GM) and a Pentax (2263, the two bytes 8 and
  215 as 8·256 + 215). On a Nikon it holds the 8-byte LensData signature (5356031691206446338 on the
  D3300), so the field cannot be read blindly; `MakerNotes.CameraMount` names the mount (27 Nikon
  F, 5 Canon EF, 40 Sony E, 33 Pentax K).
- `LensInfo.Lens`, the EXIF LensModel, is `EF50mm f/1.4 USM` on the Canon and `FE 16-35mm F2.8 GM`
  on the Sony, empty on the D3300 with third-party glass and on the Pentax.
- The range: the Canon fills the MakerNote's MinFocal / MaxFocal but leaves `MaxAperture4MinFocal`
  0 and writes `MaxAperture` 1.414; the Sony fills both the MakerNote and the top fields; the
  Pentax neither; the Nikon the MakerNote only.
- The Adobe files on the owner's machine: Canon's carry `stCamera:LensID` as the same number (242
  of 259; every RF lens shares 61182); Sony's A-mount files carry one, **none of the 99 E / FE files
  does**; Pentax's carry **pairs** (`8 215`), which `LcpReader` dropped (`long.TryParse` fails);
  `stCamera:Lens` is the camera's LensModel string on every make.
- `.pef` was not among the raw extensions: the K-70 sample read "the file does not begin with
  SIMPLE", while libraw opened it without trouble.
- Canon RF, measured the same day on two more CC0 samples (V-045, V-046) after the owner asked
  whether RF could be fixed: on an RF body libraw's `MakerNotes.LensID` is Canon's RF lens type
  (266 for the RF 70-200mm F2.8 L IS USM, 303 for the RF 135mm F1.8 L IS USM), not the generic
  61182 that every Adobe RF file carries, so by ID an RF lens matches nothing; `LensInfo.Lens` is
  the LensModel string every RF body writes (`RF70-200mm F2.8 L IS USM`), equal to Adobe's
  `stCamera:Lens`. On the R5 Mark II, a body newer than libraw 0.21.1 (named `EOS R5m2`),
  `MakerNotes.CameraMount` said Canon EF (5) while `LensMount` said Canon RF (6); on the five
  known bodies the two agree.
- The owner's D3300 with the Nikkor 35 DX, a lens with CPU (V-047): `LensInfo.Lens` empty too,
  `Nikon.LensIDNumber` 159, `MakerNotes.LensID` another signature; so a Nikon body writes no name
  for any lens measured, and the name key never runs there.

## Decision

- **The ID by mount** (`RawReader.LensIdOf`): `Nikon.LensIDNumber` when above 0; otherwise
  `MakerNotes.LensID` when the MakerNote names a mount other than Nikon's and the value is above 0
  and fits 31 bits; with an unknown mount nothing. `LoadedFrame.NikonLensId` becomes `LensId`,
  beside `LensMount` (libraw's mount name, `RawReader.MountLabel`, of the lens's mount, or the
  camera's when the lens's is unknown) and `LensName` (`LensInfo.Lens` trimmed).
- **Pentax pairs** (`LcpReader.ParseLensId`): two integers of a byte each read as a·256 + b, libraw's
  value; one integer as before; anything else dropped.
- **The name as a second key** (`LensCatalog.ByLensName`, `LensIdentification.CandidatesFromRaw`):
  the raw's name matched letter for letter (letters and digits, upper-cased) against each lens's
  name and aliases, the lens's maker allowed in front. The name's matches, narrowed by range, are
  the candidates when any survives; the ID's, narrowed the same way, otherwise (a name hit the
  range contradicts must not hide an ID whose files carry the right lens in `AlternateLensIDs`,
  the reviewer's case of 2026-10-26). The recommendation the owner took
  said "name hits ∪ ID hits"; the first run on the Canon sample listed six lenses, the EF 50 twice
  and three Zeiss ZE (Milvus 1.4/25, 1.4/35, Otus 1.4/100), whose Adobe files carry 198 in
  `AlternateLensIDs` because the Zeiss ZE lenses report Canon's type 198, and whose names
  ("Milvus 1.4_25 ZE") no range can read, so narrowing keeps them; on an RF body, where every lens
  is 61182, the union would have listed every RF lens. The name therefore wins where it matches,
  and the ID is the key where the camera writes no name or the name is in no file. The lensfun
  join by maker and range stays for the ID path, with the range taken from the hits' names when
  the raw gives none (`AgreedRange`), and a lensfun lens whose name is letter for letter a hit's
  name or alias joins too. On the name path no join by range: it would pull another lens of the
  same range (the A-mount 16-35 f/2.8 beside the FE).
- **The range on Canon** (`RawReader.RangeOf`): `MaxAperture4MinFocal` from the MakerNote, else
  the top field, else the MakerNote's `MaxAperture` only on a prime or when the shot's focal length
  is the lens's shortest: Canon's field is the maximum aperture at the current focal length, and
  the names state the short end's; a variable-aperture zoom at its long end was not measured, so
  there the aperture stays unknown and only the focal ends narrow (the R6 sample, a 70-200 f/2.8
  at 200 mm, is read as 70-200 mm with no aperture). The focal ends from the MakerNote, else the
  top fields.
- **The words** (`SyntheticReport.RawLensLabel`): the lens as the raw gave it, with the mount and
  the range in one parenthesis: `the lens "EF50mm f/1.4 USM" (ID 198) from the raw (Canon EF, 50 mm
  f/1.4)`, `the lens ID 2263 from the raw (Pentax K)`; "the Nikon ID" is gone from every text. A raw
  with a name or an ID is never a raw without a lens (`ShotIdentity.RawNamesLens`, R-106's sentence
  names the missing name too).
- **`.pef`** joins the raw extensions. `.orf`, `.rw2` and the rest wait for a measured file.
- **Canon RF needs nothing more**: the name path identifies the RF lens (the R6 sample resolves
  to its one Adobe file without `--lens`, the R5 Mark II to the Adobe file and the lensfun
  entry), and the ID path cannot, since libraw's RF type is in no profile and the profiles'
  61182 is in no raw.

## Alternatives rejected

- **`MakerNotes.LensID` on every make.** On a Nikon it is the LensData signature; the D3300 would
  have reported a 19-digit lens ID.
- **`MakerNotes.Lens`, libraw's own table name (`EF 50mm f/1.4 USM`), as the name.** It is libraw's
  spelling, not the file's; Adobe's `stCamera:Lens` is the camera's string, which `LensInfo.Lens`
  gives as it stands.
- **The name by containment or by the text search.** `--lens "EF 50mm f/1.4 USM"` finds only the
  lensfun entries today because the Adobe stub's name comes from the file name (`f1.4`, no bar); an
  identification must not depend on that. Letters-and-digits equality is the rule the camera lookup
  already uses (`LensfunData.CropOf`, `PreferCamera`).
- **Always joining lensfun by maker and range.** On the Sony it adds the A-mount 16-35 ZA, on a
  Pentax without a range it would add every Pentax lens.
- **Reading the Adobe `stCamera:LensInfo` range instead of the name.** The name already parses
  (`LensSpec`) and is what the lists show; a second range source would be a second rule.
- **Adding every libraw extension.** Only what was measured: `.pef` on a K-70.

## Consequences

- `LoadedFrame.LensId`, `LensMount`, `LensName`; `ShotIdentity.HasRawLensName`, `RawNamesLens`;
  `LensCatalog.ByLensName`, `Letters` (moved from `LensIdentification`); `RawReader.LensIdOf`,
  `MountOf`, `MountLabel`, `RangeOf`, `FillShootingInfo`; `LcpReader.ParseLensId`, and the LCP
  pretty name and `stCamera:Lens` both in a profile's aliases, so a lazily listed Adobe file is
  found by either spelling.
- An independent review of the branch (2026-10-26, a subagent with a clean context) found the
  name hiding the ID when its hits fail the range, the `MaxAperture` fallback read as the
  short-end aperture, the Nikon-with-a-named-lens case unmeasured, `.pef` missing from the help
  and window texts, "the lens by the lens ..." and nested quotes in two composed sentences, the
  pretty name unreachable on a lazily listed file, programming names in `MountLabel`, and the
  mount choice untested; all taken, the Nikon case settled by measuring the owner's Nikkor raw.
- Guards: `LensMountTests` (R018, R108: a fabricated frame per make with the measured values, the
  pair parsing, the rule per mount, the labels in both languages), `VignettingComparisonTests`
  (the gap), `RawReaderTests` (`.pef`), `RawSampleTests` (`Category=Validation`: the four files
  under `FLATCHECK_TESTDATA_LARGE`, V-041 to V-044). The Nikon path keeps its tests; the goldens
  do not move (no golden scenario gives a raw).
- On the four samples the console now identifies the lens without `--lens`: each lists its Adobe
  file and its lensfun entry (two of them for the EF 50 f/1.4, calibrated on two formats) and asks
  for `--source` (recorded in `docs/design/lens-profiles.md`, section 10).
- Open: the Adobe stub's name spells `f1.4` where the file says `f/1.4`, so a `--lens` text with the
  bar misses the Adobe half (seen on the Canon; not this task). An RF body without a LensModel
  would have no lens for FlatCheck, since the RF type matches no profile; none is known.
