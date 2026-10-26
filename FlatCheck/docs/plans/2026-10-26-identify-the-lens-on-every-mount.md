# Identify the lens on every mount, not only by the Nikon ID

Approved 2026-10-26 (the owner: "merg pe recomandările tale"). Branch
`claude/Identify-the-lens-on-every-mount`, worktree
`FlatCheck/.claude/worktrees/Identify-the-lens-on-every-mount`, cut from `main` at 6755822 (after
the merge of `claude/Read-Lightrooms-lens-profiles-too`).

## Context

`RawReader.ShootingInfo` read the lens ID from `LensInfo.Nikon.LensIDNumber` only, so a raw from
any other make carried no lens for FlatCheck, and the raw of a Canon with an EF lens even read as
"says nothing about the lens" (ADR 0065 left this for its own task, with a measured raw).

Measured on 2026-10-26 with `Sdcb.LibRaw` 0.21.1.7, the project's package, on four CC0 samples of
raw.pixls.us (SHA-256 identical to the site's; the files and the hashes are in
`docs/validation.md`, V-041 to V-044) and on the owner's D3300 raw:

| libraw field | Nikon D3300, Samyang 135 | Canon EOS 7D Mark II, EF 50 f/1.4 USM | Canon EOS 6D, EF 85 f/1.8 USM | Sony ILCE-7M3, FE 16-35 F2.8 GM | Pentax K-70, DA 18-135 |
|---|---|---|---|---|---|
| `Nikon.LensIDNumber` | 74 | 0 | 0 | 0 | 0 |
| `MakerNotes.LensID` | 5356031691206446338 (the 8-byte LensData signature, not an ID) | 198 | 155 | 32831 | 2263 = 8·256 + 215 |
| `MakerNotes.CameraMount` | 27 NikonF | 5 CanonEf | 5 CanonEf | 40 SonyE | 33 PentaxK |
| `LensInfo.Lens` (EXIF LensModel) | '' | 'EF50mm f/1.4 USM' | 'EF85mm f/1.8 USM' | 'FE 16-35mm F2.8 GM' | '' |
| `MakerNotes.Lens` (libraw's table) | '' | 'EF 50mm f/1.4 USM' | 'EF 85mm f/1.8 USM' | '' | '' |
| `MakerNotes` MinFocal / MaxFocal | 135 / 135 | 50 / 50 | 85 / 85 | 16 / 35 | 0 / 0 |
| `MakerNotes.MaxAperture4MinFocal` | 2 | 0 | 0 | 2.8 | 0 |
| `MakerNotes.MaxAperture` | 0 | 1.414 | 1.834 | 0 | 0 |
| `LensInfo` top MinFocal / MaxFocal / MaxAperture4MinFocal | 0 / 0 / 0 | 50 / 50 / 0 | 85 / 85 / 0 | 16 / 35 / 2.8 | 0 / 0 / 0 |

The Adobe profiles on the owner's machine (`C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`):

| make | `.lcp` files | with `stCamera:LensID` | the ID's form | `stCamera:Lens` |
|---|---|---|---|---|
| Canon | 259 | 242 (the 17 without: FD, FL, LTM, PowerShot, one Tamron) | one number; EF 50 f/1.4 USM = 198, EF 85 f/1.8 USM = 155; every RF lens 61182 | the camera's LensModel string, `EF50mm f/1.4 USM` |
| Sony | 150 | 47, all A-mount; none of the 99 E / FE files, and 32831 appears nowhere | one number | `FE 16-35mm F2.8 GM` |
| Pentax | 93 | 90 | two numbers, `8 215` (18588 IDs, all pairs; `AlternateLensIDs` holds `8 58` and `4`) | `smc PENTAX-DA 18-135mm F3.5-5.6 ED AL[IF] DC WR` |
| Nikon | 156 | 120 | one number | |

What FlatCheck did on these raws before the change (`flatcheck <raw> --synthetic`): Canon, "name the
lens with --lens", no candidate, and the raw counted as one that says nothing about the lens; Sony,
the range path alone, 5 candidates including the two A-mount 16-35 ZA; Pentax, `FITS: the file does
not begin with SIMPLE`, because `.pef` was not a raw extension. With `--lens` given by hand the rest
of the chain worked on all three (crop 1 for the ILCE-7M3 and 1.534 for the K-70 from the lensfun
table).

## Canon RF, measured after the owner's question (2026-10-26)

Two more CC0 samples, approved the same day: EOS R6 (4659, RF 70-200mm F2.8 L IS USM) and EOS R5
Mark II (7884, APS-C crop CRAW, RF 135mm F1.8 L IS USM). libraw's `MakerNotes.LensID` is Canon's
RF lens type (266, 303), not the 61182 every Adobe RF file carries; `LensInfo.Lens` is the
LensModel string (`RF70-200mm F2.8 L IS USM`), equal to Adobe's `stCamera:Lens`; `LensMount` 6
(Canon RF) on both, `CameraMount` 5 (Canon EF) on the R5 Mark II, a body libraw 0.21.1 does not
know. So RF is identified by the name path as it stands; the mount label takes the lens's mount
first. V-045, V-046.

## The review (2026-10-26)

The owner asked for an independent review before the merge. Its ten findings and what was done
are in ADR 0068's consequences; the Nikon question was settled by measuring the owner's Nikkor
35 DX raw (no name written, V-047). The change from "name hits ∪ ID hits" to "the name decides
where it matches" was reported to the owner at the first wrap-up, with the Zeiss ZE measurement;
the owner's answer is recorded below when given.

## Decisions (the owner took the recommendation on every point)

1. The ID by mount: `Nikon.LensIDNumber` when above 0; otherwise `MakerNotes.LensID` when the
   MakerNote names a mount other than Nikon's and the value is above 0 and fits 31 bits.
   `LoadedFrame.NikonLensId` becomes `LensId`, beside `LensMount` (libraw's mount name).
2. Adobe's two-number Pentax IDs read as `a*256+b`, libraw's convention, in `LensID` and
   `AlternateLensIDs`.
3. The lens's name (`LensInfo.Lens`, the EXIF LensModel) as a second key, matched letter-for-letter
   (case and punctuation aside, the maker allowed in front) against the Adobe `Lens` /
   `LensPrettyName` and the lensfun names; candidates = name hits ∪ ID hits, narrowed by range.
   `MakerNotes.Lens` is not used (libraw's table, not the file).
4. The range on Canon: `MakerNotes.MaxAperture` as the fallback of `MaxAperture4MinFocal`, and the
   top-level `LensInfo` fields as the last one.
5. The wording names the lens as the raw gave it, with the mount: `the lens "EF50mm f/1.4 USM" (ID
   198) from the raw (Canon EF, 50 mm f/1.4)`, `the lens ID 2263 from the raw (Pentax K)`.
6. R-018 rewritten and `implemented`; R-108 new; R-004 and R-106 touched.
7. `.pef` added to the raw extensions; `.orf`, `.rw2` not (unmeasured).
8. Tests on fabricated frames per make, no raw in the suite; the four samples kept outside git as
   validation data (V-041 to V-044, a `Category=Validation` test).

## Steps

1. Worktree and branch (done).
2. `docs/requirements.md`: R-004, R-018, R-106, R-108.
3. Tests first: `LensProfileTestData` grows the Canon, Sony (E and A) and Pentax fixtures;
   `LensMountTests` (R018, R108); `RawReaderTests` takes `.pef`; the renames in the existing tests;
   `RawSampleTests` (validation, the four files).
4. Core: `LoadedFrame` (`LensId`, `LensMount`, `LensName`), `RawReader` (the rule, the mount label,
   the range fallbacks, `.pef`), `LcpReader.ParseLensId`, `LensCatalog.ByLensName` and `Letters`,
   `LensIdentification` (the predicates, the union, the lensfun join by name), the labels in
   `SyntheticReport`, the predicate in `StructureReport`, `VignettingComparer` and the Synthetic
   tab's prefill.
5. ADR 0068 (0030 superseded in part), CHANGELOG, `lens-profiles.md`, `modes-synthetic.md`,
   `code-map.md`, `README.md`, `validation.md` + manifest.
6. Gate, commit, merge proposal. No formula moved, no golden gives a raw: no golden should change.

## Outcome and deviations (2026-10-26)

Merged into `main` as 876782d after an independent review and three commits (9a5cc52, 50cd7ef,
9bc4e94) plus the merge of `main` into the branch (9b0cd8a). What deviated from the plan above:

- Decision 3 said candidates = name hits ∪ ID hits. The first run on the Canon 7D Mark II sample
  listed six lenses (three Zeiss ZE files share ID 198 and their names parse no range), so the
  name decides where a hit of it survives the range, the ID otherwise; the review then made the
  fallback to the ID real when the name's hits all fail the range. The owner was told at the
  first wrap-up and merged the result.
- Decision 4 said `MaxAperture` as the fallback of `MaxAperture4MinFocal`; the review noted it is
  Canon's aperture at the current focal length, so it is taken only on a prime or a shot at the
  short end (unmeasured on a variable-aperture zoom).
- Step 5 said ADR 0067; `main` took 0067 for the ratio removal meanwhile, so the lens ADR is 0068,
  renumbered on the branch when `main` was merged into it.
- Six raw.pixls.us samples instead of four (two Canon RF after the owner's question), plus the
  owner's Nikkor 35 DX and Tokina raws as V-047 and V-048: V-041 to V-048.
- `.pef` also entered the help and the window texts, after the review; the two help goldens moved
  one line each.
- The mount label takes the lens's mount first (`RawReader.MountOf`), measured on the R5 Mark II.

