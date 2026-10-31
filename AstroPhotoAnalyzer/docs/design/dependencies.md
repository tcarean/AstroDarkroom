# Dependencies

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §13 and from the old
> `CLAUDE.md`, "The dependencies" and "`SixLabors.ImageSharp`". The rule is now the kit's rule
> "Packages" and this project's rule "Package notices" in `CLAUDE.md`; the choice of
> ImageSharp 3.1.11, its licence and its pin are ADR 0009, and the place of the notices ADR
> 0023. This note keeps where the package is touched and the behaviours of 3.1.11 the code
> depends on, and the judgment on each security advisory NuGet's audit reports against it
> (ADR 0036). **Covers:** `Directory.Packages.props`, `Directory.Build.props`,
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
predictor to be competitive. `Wacton.Unicolour` **8.0.0** (MIT, pure
managed, no dependencies): evaluates the tables of an ICC printer profile for
`IccPaperBlack` (ADR 0030). Measured behaviours the code depends on: a file that is not a
profile does not throw, the configuration carries an `Error` and the colour is computed
"naively" (L\* 100), so the header is checked before and the error after; the perceptual
intent answers L\* 3.14 for every profile, so the intent is always asked for; a profile
header without a date is refused, so the hand-built fixtures carry one. Adding a package: discuss first, then csproj + notices +
`ThirdParty.cs` + README table (`CLAUDE.md`, the rule "Package notices"). Embedded data is a
component too: the Lynds catalogue (`Input/ldn.txt`, an `EmbeddedResource` with a
`LogicalName`) has a `ThirdPartyComponent` with `Resources` instead of `Packages`, and
`ThirdPartyTests` ties the csproj's resources to the components the same way (ADR 0025). Since 2026-10-02
the version is in `Directory.Packages.props` and the csproj names the package only (ADR 0003).

`NLog` **6.2.2** and `NLog.Targets.AtomicFile` **6.2.2** (BSD-3-Clause, one project, one commit,
pure managed): the diagnostic log (ADR 0069, `logging.md`), one component with two packages in
`ThirdParty.cs`. The version is the suite's: AstroDarkroom publishes every window into one
folder, so the DLLs must be the same bytes as FlatCheck's, DarksCheck's and LightsCheck's, and a
change of version moves in every tool the same day. `NLog.Targets.AtomicFile` ships two builds,
`net8.0` (the console's and the core's tests') and `net8.0-windows7.0` (the window's): the
two-writer check runs on both.

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

### Security advisories against 3.1.11

NuGet's audit reports every published advisory against the pinned version at restore (NU1902
moderate, NU1903 high); `Directory.Build.props` keeps the two codes as warnings under the gate's
`-warnaserror`, so the gate shows them and stays green (ADR 0036). Each one is read against what
the tool does with the package and judged here; one that applies opens the upgrade task. The
five published on 2026-10-07, all patched in 4.1.2 only:

| advisory | severity | what | applies? |
| --- | --- | --- | --- |
| GHSA-j3p4-wp97-rph4 | high | out-of-bounds read in `HistogramEqualization` on a float TIFF with infinite samples | no: never called |
| GHSA-jjfr-hcj7-qf5w | high | buffer overrun in the 1-bit CCITT Group 4 TIFF encoder | no: the tool encodes 8-bit LZW only |
| GHSA-j9gm-c75j-xc9q | high | the same in the CCITT Group 3 encoder | no: as above |
| GHSA-gwg2-r3hj-4w44 | moderate | large allocation from a malformed embedded ICC profile's CLUT dimensions | no: the profiles decoded are those of the owner's own files; worst case a failed allocation of a local tool |
| GHSA-wmxv-xphr-5c9g | moderate | a crafted BigTIFF makes the decoder loop without progress | no: the owner's own files; worst case a hung local tool |

The implementation plan of 2026-09-20 recorded the choice as: "SixLabors.ImageSharp 3.1.11 for
TIFF/PNG/JPEG; approved dependency; same licence reasoning as FlatCheck; hand-rolled TIFF+JPEG
would be dishonest work".
