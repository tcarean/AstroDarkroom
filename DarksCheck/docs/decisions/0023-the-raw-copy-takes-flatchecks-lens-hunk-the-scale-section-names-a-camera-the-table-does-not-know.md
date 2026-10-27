# 0023. The raw copy takes FlatCheck's lens-on-every-mount hunk; the scale section names a camera the table does not know

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

`RawReader.cs` and `ImageLoader.cs` are copies of FlatCheck's through LightsCheck (ADR 0003),
and a copy takes a FlatCheck change as its own hunk, without the rest of FlatCheck's later code
(ADR 0020). On 2026-10-26 FlatCheck's task "Identify the lens on every mount" (its ADR 0068,
R-108, `main` 78da298) changed the reader: the lens ID was read from Nikon's field alone, so a
Canon, a Sony or a Pentax raw said nothing about its lens, and on a Nikon the MakerNote's
`LensID` holds the 8-byte LensData signature, so it cannot be read blindly; `.pef` was not among
the raw extensions, so a Pentax K-70 raw was refused as a FITS that does not begin with SIMPLE.
FlatCheck measured the rule per mount on four CC0 samples of raw.pixls.us and the owner's D3300
raws (its ADR 0068 holds the table). DarksCheck reads darks of the owner's D3300 today and
carries the lens fields in `FrameInfo` without printing them; the owner asked the same day to
take the hunk, so the copy stays FlatCheck's code and a session of another make lists its frames.
LightsCheck, the copy's nominal source, has not taken the hunk yet.

The same message asked for a sentence in the scale section when `CameraFacts.For` does not know
the camera: the `electroni` row said `necunoscut pentru <camera>`, the `saturatie` and
`zgomot de citire` rows silently dropped the source's figures, and the four paragraphs never said
that the electrons were missing.

## Decision

- The copy takes FlatCheck's hunk token for token: `FillShootingInfo` fills the frame in place,
  `LensIdOf`, `MountOf`, `MountLabel` and `RangeOf` as FlatCheck wrote them, `.pef` as the seventh
  extension, and `LoadedFrame.LensId`, `LensMount`, `LensName` in place of `NikonLensId`, carried
  into `FrameInfo` under the same names. One adaptation: `RangeOf` calls FlatCheck's
  `LensSpec.Close` (two focal lengths within 1 mm or 5 % of the larger), which lives in
  FlatCheck's lens profiles; DarksCheck has none, so `RawReader.Close` holds the same two
  tolerances beside it. The comments are DarksCheck's (ADR 0019); DarksCheck's shot fields
  (exposure, ISO, time) stay where LightsCheck's adaptation put them. The hunk comes from
  FlatCheck directly, not through LightsCheck: the rule "Copied files stay copies" sends a fix
  to its source first, and FlatCheck is the source of both.
- The first paragraph of the scale section ends, for a camera the table does not know, with the
  sentence of R-080: the camera's name, that it is not in the application's table, that no
  figure of the section is in electrons and that the source's full well and read noise are
  missing; `Cadrele nu spun aparatul` when no readable dark names one.

## Alternatives rejected

- **Waiting for LightsCheck to take the hunk and refreshing from there.** LightsCheck has not
  taken it; the owner asked for DarksCheck now, and the code is FlatCheck's either way.
- **Keeping `NikonLensId` and adding the three fields beside it.** FlatCheck replaced it, and a
  copy that keeps a field its source dropped diffs on every refresh.
- **A `LensSpec` of DarksCheck's own for `Close`.** A type for one comparison; the two
  tolerances beside their one caller keep the hunk readable against its source.
- **Saying the camera is unknown in the `electroni` row only, as before.** The row is one of
  eleven; the paragraphs are what the owner reads for the why, and they claimed nothing about
  the electrons.

## Consequences

- `RawReaderTests` pins the four rules on the values libraw gave FlatCheck on 2026-10-26 (R008),
  a `LoadedFrame` fabricated per make carried into `FrameInfo`, and `.pef`; `SessionFolderTests`
  lists a `.pef` session; `DarkSeriesTests` pins the sentence in both languages (R080). The two
  help goldens move by one word, `.pef`.
- The next refresh of `RawReader.cs` from LightsCheck must carry this hunk or come after
  LightsCheck takes it; `imaging.md` says so.
- Nothing prints the lens yet: the inventory shows the camera, the exposure, the ISO and the
  time. A later task may add the lens to the session lines.
