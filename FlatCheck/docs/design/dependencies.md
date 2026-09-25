# The dependencies: the rule, libraw and ImageSharp

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** `FlatCheck.Core.csproj`, `Directory.Packages.props`, `RawReader.cs`, `PreviewReader.cs`, `ThirdParty.cs`, `THIRD-PARTY-NOTICES.md`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## The dependencies

### The rule

**Dependencies are discussed before they are added, not avoided.** The tool still leans
toward hand-rolled where hand-rolled is honest - the FITS reader and the PNG encoder stay
as they are, and nothing replaces them. But a package is a normal thing to reach for when
it does a real job better than we would. Bring the candidate, its licence, its size and its
alternatives to the author, and let them decide. **Do not add one silently**, and do not
add one to save yourself an afternoon on something the BCL already does.

Two constraints do not bend:

- **Licence compatibility is not negotiable.** The tool is GPLv3-or-later, so anything
  linked in has to be GPLv3-compatible, and the reason it is has to be written down. See
  `THIRD-PARTY-NOTICES.md`, which is the file that records those decisions, and "Licensing"
  below.
- **The single-copyable-binary promise.** Pure-managed packages keep `PublishSingleFile`
  simple. Per-RID native binaries cost the same care `Sdcb.LibRaw` already needs, which is
  a point against, not a veto.

**A new package means three places, and a test enforces two of them.** Besides the
`PackageReference` itself: an entry in `THIRD-PARTY-NOTICES.md` (the *why* of the
compatibility) and an entry in `FlatCheck.Core/ThirdParty.cs` (the name, role, licence,
copyright notice and URL that the window's "Despre" **displays**).
`ThirdPartyTests.R095_EveryPackageTheCoreLinksIsListed` fails if a package in the Core csproj
is not claimed by exactly one component, or a component claims a package that is gone;
`R095_EveryComponentHasAnEntryInTheNoticesFile` fails if the notices file has no heading for
it. The README's table under "Componente terțe" is the fourth copy and is kept by hand.

**A data component is the exception the test knows about.** The lensfun database is
shipped as files, not linked as a package, so its `ThirdPartyComponent` claims no package
(`IsData`), and `EveryComponentNamesItsLicenceAndUrl` allows exactly one such component.
It is still displayed in "Despre": CC BY-SA asks for attribution, and that list is where
the program shows its notices. The Adobe profiles are deliberately **not** a component:
they are read from where Adobe installed them and never copied - the notices file says
so, under its own heading, so that nobody adds them.
The list is displayed rather than only shipped because the single-file exe carries no
notices file beside it, and because LGPL-2.1 §6 asks for it - see "Licensing".

### `Sdcb.LibRaw`

Plus its `runtime.win64` / `runtime.linux64` native packages. It exists for
one reason: `--expo` on a camera raw, which was the last feature gap against
`flatcheck.py`.

**The alternatives were measured, not assumed.** There is no pure-managed .NET decoder for
a Nikon NEF - the pixels are Huffman-coded with a predictor and passed through a
linearization curve out of the MakerNote. What NuGet actually offers: `FC.SDK.Raw` is pure
managed but Canon-only; `NDCRaw` shells out to `dcraw.exe`; `HurlbertVisionLab.LibRawWrapper`
is C++/CLI and Windows-only; Vintasoft / Accusoft / LEADTOOLS / Atalasoft are commercial
*and* demosaic, which destroys the exact thing `--expo` measures; ImageSharp, SkiaSharp
and Magick.NET have no raw mosaic access at all. `Sdcb.LibRaw` binds the same libraw that
`rawpy` binds, so the numbers are bit-identical rather than close - verified on a real
D3300 NEF: same pixel sum (35598956557), same mean to twelve decimals, same four CFA
medians, same report text apart from the scale-source label.

Three properties keep the cost contained, and all three are worth preserving:

- **It is reached from exactly one file.** `RawReader.cs` is the only thing that names a
  `Sdcb.LibRaw` type. Everything downstream still sees an `Img`.
- **It is loaded lazily**, and that survives even deleting `Sdcb.LibRaw.dll` itself, not
  just the native DLLs: a FITS run exits 0, a raw run exits 1 with a message naming what
  is missing rather than letting a `0x8007007E` reach the user. `RawReader.Load` catches
  `DllNotFoundException` and friends for that.

  What makes it lazy is narrower than it looks, and was measured rather than assumed.
  `RawReader`'s **type** is loaded on every run, FITS included, because `ImageLoader` asks
  it `Handles`. Method bodies are only resolved at JIT time, so `Decode` costs nothing
  until a raw is actually opened - but **fields are resolved when the type is loaded**. A
  static field of a libraw *reference* type turns out to be harmless (the CLR defers), a
  field of a libraw *value* type breaks every FITS run. So: no libraw-typed fields on
  `RawReader`, of either kind. `HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded`
  pins it; nothing else would notice.
- **Nothing is developed.** Only `raw_image` (cropped to the visible area, like rawpy's
  `raw_image_visible`), `color.maximum` and the black levels, plus `idata.filters` /
  `idata.cdesc` for the mosaic order and the embedded preview bytes. No demosaic, no white
  balance, no gamma - all of which would make the measurement meaningless.

**Its licence takes a decision, so it is recorded rather than assumed.** The C# wrapper is
MIT. The native `runtime.*` packages are LibRaw itself, offered as **LGPL-2.1-only OR
CDDL-1.0**. We take the LGPL option: CDDL is not GPL-compatible, so choosing it would break
the tool's own licence. LGPL-2.1 §3 permits applying GPL v2 "or any later version" to a
copy, which is what brings it into a GPLv3 work.

### `SixLabors.ImageSharp`

Decodes the JPEG preview the camera embeds in a raw - the image the camera's own screen
histogram was computed from. Reached from exactly one file, `PreviewReader.cs`, the same
containment `RawReader.cs` has.

It is not there to replace anything hand-rolled: the PNG encoder still writes our own
bytes. It is there because the alternative was a hand-rolled JPEG decoder - Huffman, DCT,
chroma subsampling, YCbCr, progressive mode - which is an order of magnitude beyond an
encoder that only writes, for a picture that never votes on a verdict.

**The licence needs reading, and it is fine here.** ImageSharp 3.x and later are under the
Six Labors Split License 1.0, which is a *conditional dual* licence, not a restrictive one.
It grants Apache-2.0 terms when "You are consuming the Work in for use in software licensed
under an Open Source or Source Available license"; a commercial licence is required only
for the other cases, essentially proprietary use above 1M USD revenue. FlatCheck is GPLv3,
so it qualifies on the first condition with nothing to change. Apache-2.0 then flows into
GPLv3 one-directionally, which the ASF states plainly: "Apache 2 software can therefore be
included in GPLv3 projects."

Two consequences worth keeping:

- **Pin the version.** The licence text is "Version 1.0, June 2022" and does not say which
  licence version governs which release. The grant travels with the version taken, so
  pinning is legal hygiene as well as reproducibility.
- **It is pinned to 3.1.x, not 4.x, and that is about the build, not the licence.** From
  4.0.0 the package ships an MSBuild task that validates a licence key at compile time: a
  warning on Debug, an **error on Release** without a `sixlabors.lic` or
  `$(SixLaborsLicenseKey)`. Six Labors grants that key free to open-source projects, but on
  application, and anyone cloning this repo would need their own to publish. 3.1.x carries
  the identical Split License 1.0 text, no task, and the only API difference that matters
  here - it never applies an ICC profile on decode - is the behaviour wanted anyway. Moving
  to 4.x means obtaining the key *and* setting `ColorProfileHandling.Preserve` explicitly.
- **Because GPLv3 forces every derivative to stay GPLv3**, and GPLv3 is an open source
  licence, everyone downstream qualifies on the same condition. The eligibility does not
  break as the tool is passed on.

`StbImageSharp` (Unlicense/MIT, zero dependencies, ~identical output measured against an
independent decoder on real previews) was the alternative, and would have carried no licence
footnote at all. ImageSharp was chosen for robustness on the awkward cases a camera preview
can be - progressive, CMYK/YCCK, unusual sampling - and for scaled decoding. If its licence
ever moves, StbImageSharp is the drop-in fallback and the seam is one file wide.

