# Take FlatCheck's lens-on-every-mount hunk into the raw copy

## Context

FlatCheck's task "Identify the lens on every mount" (its ADR 0068, R-108, merged 2026-10-26,
`main` 78da298) changed the raw reader DarksCheck's `RawReader.cs` and `ImageLoader.cs` are
copies of (ADR 0003): the lens ID is read by mount (`LensIdOf`), the mount is named
(`MountOf`, `MountLabel`), the range falls back to the top fields and to `MaxAperture` at the
short end (`RangeOf`), `FillShootingInfo` fills the frame in place, `.pef` is a raw extension,
and `LoadedFrame.NikonLensId` became `LensId`, `LensMount`, `LensName`. The owner asked on
2026-10-26 to take that hunk into DarksCheck, as ADR 0020 took the cut-FITS fix, with
`.pef` in every text that lists the extensions and the three fields in `FrameInfo`; to add in
`ScaleReport` a sentence, in both languages, for a camera `CameraFacts.For` does not know; and
tests on fabricated frames. The owner's message is the plan; no plan mode.

## Steps, one commit

1. Requirements first: R-002 and R-008 gain `.pef`, R-008 the lens fields as FlatCheck's R-108
   reads them, R-080 the sentence.
2. Tests first: the pure rules on the values libraw gave FlatCheck on 2026-10-26
   (`LensIdOf`, `MountOf`, `MountLabel`, `RangeOf`), a `LoadedFrame` fabricated per make
   (Canon, Sony, Pentax, Nikon) carried into `FrameInfo`, `.pef` claimed by the reader and
   listed by the session folder, the sentence of R-080 on a synthetic session whose FITS name
   another camera, and one whose FITS name none.
3. The hunk: FlatCheck's code token for token (`LensSpec.Close` inlined, since DarksCheck has no
   lens profiles), the comments DarksCheck's own (ADR 0019); `FrameInfo`'s three fields; the
   help text lists `.pef` through `RawReader.RawExtensions`, so the two help goldens move by
   one word each; README; ADR 0023.

## Verification

`scripts/verify.ps1` green at the commit; the goldens regenerated and every changed line tied
to `.pef`. No numeric code of the analysis changed, so `/validate` is not run.

## What deviated

Nothing in the steps. One expectation of a new test was written wrong at first (`RangeOf` keeps
the two focal ends as given, 50 and 51, and only the aperture decision treats them as one); the
test was corrected, not the code. The goldens `help.txt` and `en_help.txt` each moved by one
word, `.pef`, from the help's extension list built on `RawReader.RawExtensions`. No numeric code
of the analysis changed, so `/validate` was not run.
