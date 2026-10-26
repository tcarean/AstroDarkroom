# Say when a raw carries no lens

## Context

The owner opened a Siril session shot with a Canon EOS 40D (`E:\Photografie\Astro\online\RosetteNebula`,
17 CR2 flats). The Structure tab's sixth test said `no raw beside the master (in flats/)` and the
Synthetic tab prefilled nothing, while the Exposure and Histogram tabs read the same CR2 files
without trouble.

Measured on 2026-10-26, with two independent readers (libraw through the project's own package,
and a TIFF/EXIF parser written for the check):

| field | Canon EOS 40D, flat and light, both online sessions | Nikon D3300, Samyang 135 |
|---|---|---|
| EXIF FocalLength / FNumber | 0 / 0 | 135 / 2 |
| Canon CameraSettings LensType, MinFocal, MaxFocal | 0, 0, 0 | - |
| LensModel (EXIF and MakerNote) | empty | empty |
| libraw `Nikon.LensIDNumber` | 0 | 74 |
| libraw `MakerNotes` MinFocal / MaxFocal / MaxAperture | 0 / 0 / 0 | 135 / 135 / 2 |

The camera sat on a telescope or on an adapter without contacts: the files carry no lens, and
Siril wrote no `FOCALLEN` in the master. No program can identify a lens from them, and there is no
Adobe or lensfun profile for a telescope anyway.

Two things are wrong in FlatCheck on such a session, and only the first is this task:

1. **The wording.** The raw is found and read (`SirilSession.ReferenceRawPath`, `--raw`), but
   `VignettingComparer.Analyze` turns "no candidate and no Nikon lens ID" into
   `ComparisonGap.NoRaw`, so the console and the Structure tab say there is no raw. The same with
   `--raw IMG_7290.CR2` given by hand. The Synthetic tab leaves the focal length and the aperture
   empty (the file has none) and says nothing about why.
2. **The Nikon-only lens ID** (`RawReader.ShootingInfo` reads `LensInfo.Nikon.LensIDNumber`
   only; on a Canon with an EF lens libraw puts the maker's ID in `LensInfo.MakerNotes.LensID`,
   and Adobe's Canon profiles carry that number as `stCamera:LensID`). Needs a Canon raw with an
   electronic lens to measure; none on this machine. Out of scope; the owner chose part 1 alone.

The chain works on a Canon raw once the lens is named: `flatcheck pp_flat_stacked.fit --raw
IMG_7290.CR2 --lens "85mm f1.4L IS" --focal 85 --fstop 2.8` compared with the Adobe Canon profile
(+0.10 stops, GOOD), and `--synthetic` on the CR2 took the crop 1.622 from the lensfun table for
"Canon EOS 40D".

## Owner's decision (2026-10-26)

Part 1 only: a raw that was read but says nothing about the lens gets its own words, in the
console and in the window. Part 2 (the maker's lens ID beyond Nikon) waits for a measured raw, and
for the branch `claude/Read-Lightrooms-lens-profiles-too`, which already touches `LensProfiles/`.

## Design

- `ShotIdentity.RawSaysNoLens`: a raw was read (`Raw != null`) and it carries neither a lens ID
  nor a focal range. A Nikon manual lens (ID 74 plus the range from the menu) is not this case.
- `ComparisonGap.RawWithoutLens`, chosen by `VignettingComparer.Analyze` when there is no
  candidate, no lens ID and `RawSaysNoLens`; `NoRaw` stays for a run without a raw (and for a raw
  that could not be read, which keeps `RawError`).
- One sentence in the core, `SyntheticReport.RawWithoutLensNote(name)`: "IMG_7290.CR2 says nothing
  about the lens (no lens ID, no focal range): a telescope, or a lens without contacts". The
  Structure report's `GapText` prints it with the console's remedy (`--lens <text>`, plus `--focal`
  and `--fstop` when those are missing too); `LensIdentification.Identify` adds it to the notes,
  which the Synthetic tab shows under its fields as it shows every note.
- Out of scope, noted: a raw with a range but no ID and no lens of that range still reads `NoRaw`;
  it cannot happen on a Nikon (the ID is always written) and belongs with part 2.

## Requirement

R-106 in `docs/requirements.md`, `implemented`; R-017 names the new reason.

## Steps, in order

1. Worktree `.claude/worktrees/Say-when-a-raw-carries-no-lens`, branch
   `claude/Say-when-a-raw-carries-no-lens`.
2. R-106, and the failing tests: `LensProfileTests` (`RawSaysNoLens` and the note),
   `VignettingComparisonTests` (the gap), `StructureReportTests` (`R106_...`, the words with the
   file's name and the remedy), `StructureTabTests` (the card), `SyntheticTabTests` (the note under
   the fields).
3. The core: the property, the gap, the sentence, the note.
4. ADR 0065, CHANGELOG (Fixed), `docs/design/modes-structure.md` and `lens-profiles.md`, the
   README's Structure-tab paragraph.
5. Gate, commit, merge proposal. No golden changes expected: no golden scenario gives a raw.
