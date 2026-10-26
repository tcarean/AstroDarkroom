# 0065. A raw that carries no lens is said so, never reported as a missing raw

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The owner opened a Siril session shot with a Canon EOS 40D (17 CR2 flats in `flats/`). The
Structure tab's sixth test said `no raw beside the master (in flats/), so no lens ID`, the
Synthetic tab prefilled neither the aperture nor a lens and said nothing, while the Exposure and
Histogram tabs read the same CR2 files. Measured on 2026-10-26 with two independent readers
(libraw through the project's package, and a TIFF/EXIF parser written for the check), the flats
and the lights of both Canon sessions on the machine carry no lens at all: EXIF focal length 0,
f-number 0, Canon `CameraSettings` lens type 0, no range, no lens name, and libraw's
`Nikon.LensIDNumber` 0. The camera sat on a telescope or on an adapter without contacts; Siril
wrote no `FOCALLEN` in the master either. On the owner's D3300 a manual lens is different: the
body writes the "non-CPU lens" ID 74 and the range set in its menu (ADR 0030).

`VignettingComparer.Analyze` turned "no candidate and no Nikon lens ID" into `ComparisonGap.NoRaw`,
so the console and the Structure tab told the person to look for a file that was there, and
`--raw IMG_7290.CR2` given by hand said the same. The Synthetic tab's prefill offers candidates only
on a lens ID, and left the fields empty without a word.

A second defect was found and left for its own task: `RawReader.ShootingInfo` reads the lens ID
from `LensInfo.Nikon.LensIDNumber` only, while on a Canon with an electronic lens libraw puts the
maker's lens ID in `LensInfo.MakerNotes.LensID`, the number Adobe's Canon profiles carry as
`stCamera:LensID` (242 of the 259 Canon files on this machine have one). No Canon raw with a lens
exists on the machine to measure it on, and the branch `claude/Read-Lightrooms-lens-profiles-too`
already touches `LensProfiles/`. The owner chose the wording alone.

## Decision

A raw that was read but says nothing about the lens - neither a lens ID nor a focal range - is
its own case, `ComparisonGap.RawWithoutLens`, chosen before `NoRaw` when there is no candidate
and no lens ID; `ShotIdentity.RawSaysNoLens` is the predicate, false without a raw and false for
a Nikon manual lens. One sentence in the core, `SyntheticReport.RawWithoutLensNote`, names the
file and what it lacks (`IMG_7290.CR2 says nothing about the lens (no lens ID, no focal range): a
telescope, or a lens without contacts`); `LensIdentification.Identify` adds it to the notes, so
the Synthetic tab shows it under its fields as it shows every note, and the Structure report's
`GapText` prints it with the console's remedy, `Give --lens <text>` plus `--focal <mm>` and
`--fstop <f>` for whichever of the two is unknown too (`StructureReport.RawWithoutLensRemedy`).
The Structure tab's card takes `GapText` as before. `NoRaw` keeps the run without a raw and the
raw that could not be read (R-106).

## Alternatives rejected

- **Reading the maker's lens ID in the same task.** It would not have helped this session (the
  files carry nothing) and it needs a measured Canon raw with a lens, which the machine does not
  have; a design chosen without a measurement is against the kit's rule "Measured numbers".
- **A telescope path for the comparison.** There is no Adobe or lensfun profile for a telescope,
  so there is nothing to compare with; the honest answer is the sentence, and `--lens` for a
  photographic lens the file does not name.
- **Saying it in the window only.** The console says the same thing for `--raw`, and the
  Structure tab's card is the console's `GapText` by design (ADR 0033); one sentence in the core
  keeps both applications telling the same story.
- **Treating a raw with a range but no ID and no lens of that range** the same way. It cannot
  happen on a Nikon (the ID is always written) and belongs with the maker's-ID task; it still
  reads `NoRaw`, noted in the plan.

## Consequences

- `ComparisonGap` gains `RawWithoutLens`; `StructureReportTests.EveryGapHasWords...` covers its
  words in both languages without diacritics.
- Guards: `LensProfileTests` (the predicate and the note), `VignettingComparisonTests` (the gap),
  `StructureReportTests.R106_...` (the words, the remedy with and without FOCALLEN, the raw as the
  reference), `StructureTabTests` (the card), `SyntheticTabTests` (the note under the fields).
  No golden changes: no golden scenario gives a raw.
- The maker's lens ID beyond Nikon stays open, with its facts in
  `docs/plans/2026-10-26-say-when-a-raw-carries-no-lens.md`.
