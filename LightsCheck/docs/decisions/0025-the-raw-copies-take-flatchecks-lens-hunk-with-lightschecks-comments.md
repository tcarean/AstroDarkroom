# 0025. The raw copies take FlatCheck's lens-on-every-mount hunk, with LightsCheck's comments; the inventory names the lens as the raw gave it

Date: 2026-10-27
Status: accepted 2026-10-27; the SIMPLE check left out superseded by 0027
Supersedes: 0003 in part (`LoadedFrame.NikonLensId`; `.pef` not an extension)

## Context

`RawReader.cs` and `ImageLoader.cs` are adapted copies of FlatCheck's (ADR 0003), LightsCheck's
to change, and a FlatCheck fix reaches a copy as its own hunk (ADR 0021). On 2026-10-26
FlatCheck's task "Identify the lens on every mount" (its ADR 0068, R-108, `main` 78da298)
changed its reader: the lens ID was read from Nikon's field alone, so a Canon, a Sony or a
Pentax raw said nothing about its lens; on a Nikon the MakerNote's `LensID` holds the 8-byte
LensData signature, so it cannot be read blindly; `.pef` was not a raw extension, so a Pentax
K-70 raw was refused as a FITS that does not begin with SIMPLE. FlatCheck measured the rule per
mount on six CC0 samples of raw.pixls.us and the owner's D3300 raws (its ADR 0068 holds the
table). The owner asked, on 2026-10-26, for the hunk to be recopied into LightsCheck with
comments that describe LightsCheck, tested on fabricated frames, no raw in the suite.
DarksCheck took the same hunk the same day (its ADR 0023), so its copy was the template here.

Measured on 2026-10-27 in the shared checkout: FlatCheck's two files have not changed since
78da298; `RangeOf` calls `LensSpec.Close`, a type of FlatCheck's lens profiles that LightsCheck
has no use for; FlatCheck's R-009 (the cut-data refusal) is already whole in `FitsReader.cs`
(ADR 0021) except one sentence of 469da9b on `LoadHeader`'s doc comment; nothing in the window
shows the lens, and the goldens' lens line comes from FITS fixtures, so no golden moves.

## Decision

- **The hunk, token for token** (R-065): `RawReader.FillShootingInfo` reads the lens in one
  `try` as FlatCheck's does, with `LensIdOf`, `MountOf`, `MountLabel` and `RangeOf` as FlatCheck
  wrote them; `Close` is inlined (1 mm or 5 %, FlatCheck's two tolerances), the one deviation,
  because the type it lives in does not exist here. `.pef` is the seventh extension.
- **The fields**: `LoadedFrame.NikonLensId` becomes `LensId`, beside `LensMount` and
  `LensName`; `FrameInfo` carries the three.
- **The comments are LightsCheck's**: no synthetic flat, no Adobe profiles, no "the name as the
  key"; what the fields are for here is the inventory's lens line, and the comments say so.
  FlatCheck's measurements are cited as FlatCheck's.
- **The words** (`InventoryReport.Lens`): after the shot and the chip's range, the lens as the
  raw names it, the name in quotes with the ID and the mount in one parenthesis:
  `"EF50mm f/1.4 USM" (ID 198, Canon EF)`, `ID 74 (Nikon F)`, `"FE 16-35mm F2.8 GM" (Sony E)`,
  `ID 2263 (Pentax K)`; a mount alone prints nothing. The same in Romanian: `ID` is one word in
  both. The frame read is the first readable one with a focal length, an ID or a name.
- **The FITS residue**: `FitsReader.LoadHeader`'s comment gains FlatCheck's sentence that the
  header read does not check the data area, in the copy's own words, since its comment has no
  ADR 0057 to lean on. FlatCheck's SIMPLE check (its R-008 / ADR 0057) stays out, as ADRs 0018
  and 0021 left it: a behaviour change for a task of its own.

## Alternatives rejected

- **Copying `LensSpec` whole for `Close`.** A lens-profile type with parsing and description
  for two tolerances; the copy would carry code nothing here calls.
- **Keeping FlatCheck's comments as they stand.** They explain the synthetic flat and the Adobe
  key; here they would describe a program that is not this one, against the owner's ask.
- **Printing the mount when the raw gives no ID and no name.** A D3300 on a telescope would
  read `(Nikon F)` with nothing in front of it: a mount without a lens says nothing about the
  lens.
- **Taking the SIMPLE check with the residue.** The owner named R-009, whose check is already
  here; the SIMPLE check changes what a non-FITS file gives.

## Consequences

- `RawReader.LensIdOf`, `MountOf`, `MountLabel`, `RangeOf`, `Close`; `LoadedFrame.LensId`,
  `LensMount`, `LensName`; `FrameInfo` with the three; `InventoryReport.Lens`.
- Guards: `RawReaderTests` (`R065_`: the four rules on the values libraw gave FlatCheck, a
  fabricated frame per make through `FrameInfo.From`, `.pef`), `InventoryReportTests` (`R065_`:
  the lens line per make in both languages, the FITS wording unchanged). No golden changes.
- The Veil session's inventory reads `ID 74 (Nikon F)` where it read `Nikon ID 74`.
- A refresh of the copies from FlatCheck must keep `Close` and these comments.
