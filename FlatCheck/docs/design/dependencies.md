# The dependencies: the rule, libraw and ImageSharp

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the rule "Package notices" and ADR 0008 hold the
> rule, ADR 0009 and ADR 0010 the two packages, ADR 0028 and ADR 0040 the data component and the
> displayed list. **Covers:** by pointer, `FlatCheck.Core.csproj`, `Directory.Packages.props`,
> `RawReader.cs`, `PreviewReader.cs`, `ThirdParty.cs`, `THIRD-PARTY-NOTICES.md`; in full, one
> mechanic of the lazy loading of libraw. Cross-references such as "see X below" point at the
> sibling notes in this folder; the index in `README.md` here maps them. A change to the code this
> describes means this note is updated in the same task, or deleted with the decision it explains.

## The dependencies

### The rule

The kit's rule "Packages" and CLAUDE.md's rule "Package notices" state it; ADR 0008 holds the
reasons: hand-rolled where hand-rolled is honest (the FITS reader and writer, the PNG encoder), a
package only after its candidate, licence, size and alternatives were brought to the author and
measured, GPLv3 compatibility argued in `THIRD-PARTY-NOTICES.md`, per-RID native binaries a point
against and not a veto, the three places a package touches and the two of them `ThirdPartyTests`
enforces (the README table under "Componente terțe" is the fourth copy, kept by hand), and the one
data component the tests allow. ADR 0028 holds why the lensfun database is that component and the
Adobe profiles deliberately are not; ADR 0040 holds why the list is displayed in "Despre" rather
than shipped as a file (LGPL-2.1 §6 asks for it, and nothing rides beside a single-file exe).

### `Sdcb.LibRaw`

ADR 0009 holds the whole of it: why a package at all (no pure-managed NEF decoder exists), the
alternatives measured and rejected, the LGPL-2.1 option over CDDL, the one file that names a
libraw type (`RawReader.cs`), what is read and what is never developed, the bit-identical
figures against `rawpy`, and the lazy loading with its guard
(`HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded`). One mechanic the ADR does not
spell out: `RawReader`'s type is loaded on every run, FITS included, because `ImageLoader`
asks it `Handles`, and that is harmless only because method bodies are resolved at JIT time,
so `Decode` costs nothing until a raw is opened; fields are resolved when the type loads,
which is why `RawReader` holds no field of a libraw type, reference or value.

### `SixLabors.ImageSharp`

ADR 0010 holds the whole of it: why a JPEG decoder is not hand-rolled, the Split License read
and why FlatCheck qualifies for its Apache-2.0 terms all the way downstream, the pin to 3.1.x
and what 4.x would cost (a licence key at compile time, `ColorProfileHandling.Preserve`), why
the version is pinned at all, the one file that names an ImageSharp type (`PreviewReader.cs`),
and `StbImageSharp` as the measured drop-in fallback if the licence ever moves.
