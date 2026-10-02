# Dependencies

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §13 and from the old
> `CLAUDE.md`, "The dependencies" and "`SixLabors.ImageSharp`". The rule is now the kit's rule
> "Packages" and this project's rule "Package notices" in `CLAUDE.md`; the choice of
> ImageSharp 3.1.11, its licence and its pin are ADR 0009, and the place of the notices ADR
> 0023. This note keeps where the package is touched and the behaviours of 3.1.11 the code
> depends on. **Covers:** `Directory.Packages.props`,
> `AstroPhotoAnalyzer.Core/AstroPhotoAnalyzer.Core.csproj`,
> `AstroPhotoAnalyzer.Core/ThirdParty.cs`,
> `AstroPhotoAnalyzer.Core/Input/ImageSharpReader.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/JpegAnalyzer.cs` and
> `AstroPhotoAnalyzer.Tests/ThirdPartyTests.cs`. A change to the code this describes means
> this note is updated in the same task, or deleted with the decision it explains.

## Dependencies

`SixLabors.ImageSharp` **3.1.11**, pinned: decodes TIFF/PNG/JPEG, encodes JPEG and TIFF
for the size and error measurements. Licence reasoning in `THIRD-PARTY-NOTICES.md`;
displayed in "Despre" through `ThirdParty.Components`; bound to the csproj by
`ThirdPartyTests`. Measured behaviours the code depends on: the TIFF encoder writes 8
bits per sample for `Rgb48` even when asked for 48; `Image.Identify` does not read a
PNG's `iCCP` (`Image.Load` does); the TIFF decoder puts the ICC profile on
`Frames[0].Metadata`; the PNG encoder does not write `iCCP`; LZW needs the horizontal
predictor to be competitive. Adding a package: discuss first, then csproj + notices +
`ThirdParty.cs` + README table (`CLAUDE.md`, the rule "Package notices"). Since 2026-10-02
the version is in `Directory.Packages.props` and the csproj names the package only (ADR 0003).

## The rule, as the first CLAUDE.md wrote it

**Dependencies are discussed before they are added, not avoided.** Bring the candidate,
its licence, its size and its alternatives to the author, and let them decide. Do not add
one silently, and do not add one to save an afternoon on something the BCL already does.

Two constraints do not bend: **licence compatibility with GPLv3**, written down in
`THIRD-PARTY-NOTICES.md`, and **the single-copyable-binary promise** for the console
(pure-managed packages keep `PublishSingleFile` simple).

**A new package means three places, and a test enforces two of them**: the
`PackageReference`, an entry in `THIRD-PARTY-NOTICES.md` (the *why*), and an entry in
`AstroPhotoAnalyzer.Core/ThirdParty.cs` (what the window's "Despre" displays).
`ThirdPartyTests` binds the csproj and the list in both directions and each name to a
heading in the notices file. The README's table is the fourth copy, kept by hand. Since
2026-10-02 a version also goes in `Directory.Packages.props`, with its "why".

## `SixLabors.ImageSharp`

Approved before the first line was written. It decodes TIFF and PNG (8/16-bit, ICC
metadata) and encodes/decodes JPEG for the compression table. Unlike in FlatCheck it is
**not** peripheral: the pixels every measurement runs on come through it. Pinned to
3.1.11 - 4.x adds a licence-key MSBuild task that errors on Release; 3.1.x has the same
licence text and no task. 3.1.x never applies the ICC profile at decode, which is the
behaviour we want. See the notices file for the licence reasoning.

Four things about 3.1.11 were measured rather than assumed, and the code depends on
them: its TIFF encoder cannot write 16-bit RGB, `Image.Identify` does not see a PNG's
ICC profile but `Image.Load` does, the TIFF decoder leaves the profile on the first
frame, and the PNG encoder does not write `iCCP`. Details and consequences in
`input.md`, "Things measured about ImageSharp 3.1.11 while building phase 1". The 16-bit
TIFF and PNG fixtures in the tests are hand-built for exactly these reasons - don't replace
them with ImageSharp-encoded ones.

The implementation plan of 2026-09-20 recorded the choice as: "SixLabors.ImageSharp 3.1.11 for
TIFF/PNG/JPEG; approved dependency; same licence reasoning as FlatCheck; hand-rolled TIFF+JPEG
would be dishonest work".
