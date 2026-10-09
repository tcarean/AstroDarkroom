# 0003. The readers are copied from FlatCheck, libraw is taken under LGPL-2.1, the Python prototype is frozen

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

FlatCheck already reads camera raws undebayered through libraw (its ADR 0009: `Sdcb.LibRaw`,
LGPL-2.1 chosen over CDDL, the type loaded lazily from one file), parses FITS by hand, encodes
PNG by hand and draws labels with a bitmap font; its numbers are bit-identical to rawpy's.
LightsCheck needs exactly that: the CFA mosaic at the original ADU scale, the black and white
levels, the colour order, and the shot metadata. The owner chose, on 2026-10-09, copying over a
shared library in the monorepo: each project stays self-contained, as the kit itself is copied.
The analysis the tool reproduces was done with six Python scripts (rawpy, numpy, scipy) whose
figures on the Veil session are the reference.

## Decision

`RawReader`, `FitsReader`, `ImageLoader`, `IFrameReader`, `CfaPattern`, `Img`, `Mosaic`,
`Stats`, `PngWriter` and `BitmapFont` are copied from FlatCheck 2.3.0 into
`src/LightsCheck.Core/Imaging/`, with the namespace changed, the parts LightsCheck has no use
for cut (the residual-map and mosaic encoders of `PngWriter`, the verdict ladder of `Stats`),
and FlatCheck's Romanian comments and style kept as they are: a section of `.editorconfig`
relaxes the style rules for that folder and each file carries `#nullable disable` with the
reason. Two files are adapted and rewritten in English: `RawReader` loses the embedded preview
(and with it ImageSharp) and gains exposure, ISO and time of capture from libraw's
`imgother`; `ImageLoader`'s `LoadedFrame` carries those fields and `AduScale` replaces
`ExposureScale`. `FitsReader` gets one addition in the same language as the file: `EXPTIME`,
`ISOSPEED`, `DATE-OBS` and `XPIXSZ`. The compile fixes the kit's analyzers demanded are
`InvalidDataException` instead of `Exception` in `FitsReader`, a `MemoryStream` parameter in
`PngWriter`, and the unused usings removed. LibRaw is taken under LGPL-2.1 for the reasons
FlatCheck's ADR 0009 gives, with its notice in `ThirdParty.cs` and `THIRD-PARTY-NOTICES.md`.
The six Python scripts are committed as `prototype/`, frozen: not maintained, not run by the
gate, the numeric oracle of `docs/validation.json`, as FlatCheck's ADR 0047 froze
`flatcheck.py`. The tests FlatCheck had for the copied files (`CfaPatternTests`,
`FitsReaderTests`, the seam tests of `RawReaderTests`) come with them, adjusted to the
exception type.

## Alternatives rejected

- **A shared `Photo.Imaging` library in the monorepo.** One bug fixed once, but a change for
  one tool would rebuild and risk the other; the owner chose independence.
- **Translating the copied comments to English.** A diff of hundreds of lines with no
  behaviour behind it, and the copy would no longer diff cleanly against its source.
- **Bringing the copied files up to the kit's style and nullable rules.** Same reason; the
  compiler's own warnings still apply to them, only the style category is silenced.
- **Keeping ImageSharp for the embedded preview.** Nothing in LightsCheck shows the camera's
  JPEG; one licence to argue less.
- **Reading the pixels with the inventory.** The metadata-only load takes tens of milliseconds
  per raw; a decode takes seconds and hundreds of megabytes.

## Consequences

A fix in a copied file goes to FlatCheck first, then the copy is refreshed (CLAUDE.md, the rule
"Copied files stay copies"); `RawReader.cs` and `ImageLoader.cs` are LightsCheck's. The
validation figures (`docs/validation.json`) can be traced to `prototype/` and
`docs/validation/2026-10-06-veil/frames.csv`. `RawReaderTests` keeps the lazy-loading guard
(no libraw-typed field) and the real-decode test gated on `LIGHTSCHECK_NEF`.
