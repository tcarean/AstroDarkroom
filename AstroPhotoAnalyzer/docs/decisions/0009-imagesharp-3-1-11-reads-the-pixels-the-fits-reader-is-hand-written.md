# 0009. ImageSharp 3.1.11 reads the pixels and encodes the export table; the FITS reader is hand-written

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from the old `CLAUDE.md` ("The dependencies",
"`SixLabors.ImageSharp`"), `THIRD-PARTY-NOTICES.md`, the comment of
`AstroPhotoAnalyzer.Core.csproj`, `docs/specification.md` §13 and `docs/implementation-plan.md`
("Decisions taken before the first line"); the owner approved the package before the first line,
2026-09-20. Siril writes 16-bit TIFF and PNG with an ICC profile; the export table needs a JPEG
encoder. An honest TIFF reader means strips and tiles, LZW, Deflate, PackBits, the horizontal
predictor, planar and chunky layouts, 8 and 16 bits, both byte orders; PNG adds Adam7; a JPEG
encoder needs the DCT, Huffman coding and controlled subsampling: weeks of work to reproduce, with
bugs of its own, what exists and is tested on millions of files. FITS, by contrast, is 80-byte
cards in 2880-byte blocks followed by big-endian data. ImageSharp 3.x is under the Six Labors
Split License 1.0, a conditional dual licence.

## Decision

`SixLabors.ImageSharp` decodes TIFF, PNG and JPEG and encodes JPEG and TIFF for the export table,
pinned to 3.1.11 and touched in exactly two core files: `Input/ImageSharpReader.cs` (decode) and
`Analysis/JpegAnalyzer.cs` (encode). Unlike in FlatCheck, where it decodes a camera preview that
never votes, it is not peripheral here: every pixel the tool measures comes through it. The Split
License grants the Apache-2.0 terms to software under an open-source licence; the tool is
GPLv3-or-later, so it qualifies with nothing to change, and Apache-2.0 flows into GPLv3 in that
direction ("Apache 2 software can therefore be included in GPLv3 projects", the Apache Software
Foundation). The FITS reader is hand-written: copied from FlatCheck's core and trimmed (no CFA,
no lens or focal length), streaming, onto a multi-plane raster.

## Alternatives rejected

- **A hand-written TIFF reader and JPEG encoder.** The scope above; dishonest work for a tool whose
  subject is the print, not the codec.
- **ImageSharp 4.x.** From 4.0.0 the package carries an MSBuild task that checks a licence key at
  compile time: a warning on Debug, an error on Release without one. The key is free to
  open-source projects but on application, and everyone who clones the repository would need
  their own to publish. 3.1.x has the same licence text and no task.
- **An unpinned version.** The licence text is "Version 1.0, June 2022" and does not say which
  version of it governs which release; the grant travels with the version taken, so the pin is
  legal hygiene as well as reproducibility.

## Consequences

- Four behaviours of 3.1.11 were measured, not assumed, and the code and the tests depend on them:
  the TIFF encoder cannot write 16-bit RGB, `Image.Identify` does not see a PNG's ICC profile but
  `Image.Load` does, the TIFF decoder leaves the profile on the first frame, the PNG encoder does
  not write `iCCP`. The 16-bit TIFF and PNG fixtures are hand-built for these reasons
  (`docs/design/input.md`, `docs/design/dependencies.md`).
- 3.1.x never applies an ICC profile on decode, which is the behaviour wanted (ADR 0008).
- The JPEG figures are this encoder's, not libjpeg's (ADR 0019).
- ImageSharp is pure managed, so the console publishes as one copyable file.
- A version bump is a licence re-read; the version lives in `Directory.Packages.props` (ADR 0003).
- Guards: `ThirdPartyTests`, the notices file, the reader tests on the hand-built fixtures.

Source: CLAUDE.md (2026-09-21); THIRD-PARTY-NOTICES.md; AstroPhotoAnalyzer.Core.csproj;
docs/specification.md §13; docs/implementation-plan.md.
