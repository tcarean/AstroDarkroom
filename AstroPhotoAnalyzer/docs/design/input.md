# Inputs: readers, the raster, colour maths, statistics

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §3 and
> `docs/implementation-plan.md`, "Things measured about ImageSharp 3.1.11 while building phase
> 1". The behaviour is stated with IDs in `../requirements.md` (R-010 to R-017); the raster
> and the statistics are ADR 0010, the refusal of linear data ADR 0008. **Covers:**
> `AstroPhotoAnalyzer.Core/Input/` and the hand-built fixtures
> `AstroPhotoAnalyzer.Tests/TiffTestData.cs` and `AstroPhotoAnalyzer.Tests/PngTestData.cs`. A
> change to the code this describes means this note is updated in the same task, or deleted
> with the decision it explains.

## Inputs

### The Dmax of a printer profile (`IccPaperBlack`, R-028, ADR 0030)

Not an input of the analysis but of its parameters. `Read(path)` returns an `IccBlack`:
`Description` (`IccProfileInfo.Describe`, the file name without a `desc` tag), `Problem` (a
sentence for the user, null when the profile was read), `Note` (the CMYK reserve),
`BlackLstar` and `Dmax`. The header is checked first, by hand: `acsp` at 36, class `prtr` at
12, device space `RGB ` or `CMYK` at 16. Then Wacton.Unicolour evaluates the device black
(zeros on RGB, ones on CMYK) through an `IccConfiguration` built with
`Intent.RelativeColorimetric` and an XYZ configuration at D50; Y of the result is the black
relative to paper white, `BlackLstar` its L\*, `Dmax` = -log10(Y) rounded to two decimals. A Y
outside (0, 1), a Dmax outside (0, `Thresholds.DmaxMax`], the package's `Error`, or an exception
from it becomes a `Problem`; the first two are what a damaged table looks like, since the
package swallows it and returns some Y.
`Installed(folder)` lists the `.icc` / `.icm` files of a folder (default
`<System>\spool\drivers\color`) whose class is `prtr`, read the same way, by name; other
classes and non-profiles are skipped; an absent folder, or one that cannot be listed, is an empty
list. Every file is read whole and every printer profile evaluated, on the caller's thread: 31 ms
for the 15 files of the owner's machine, which is why the window does it in the click. `Describe(IccBlack)`
is the sentence the window's list shows. `Parameters.WithDmaxFromProfile(path, out error)` is
what the console calls: the same parameters with `DmaxOverride` and `DmaxProfile` set, or null
with the problem.

### Formats and readers (`Input/`)

`ImageLoader` picks a reader **by extension** (never by sniffing) and is the only place
that knows both. An unknown extension is an error naming the accepted ones.

| reader | extensions | what it reads |
|---|---|---|
| `FitsReader` (hand-written, streaming) | `.fit .fits .fts` | first HDU with NAXIS 2 or 3; BITPIX 8/16/32 int (BZERO/BSCALE applied) and -32/-64 float; NAXIS3 = 1 or 3, anything else refused; rows always flipped, the file's first row last, as Siril shows every FITS whatever `ROWORDER` says (ADR 0049; a `TOP-DOWN` keyword is noted as ignored); empty primary HDUs skipped |
| `ImageSharpReader` | `.tif .tiff .png .jpg .jpeg` | colour decoded as `Rgb48`, mono as `L16`; bit depth and channel count from the format's own metadata (PNG `BitDepth`/`ColorType`, TIFF `BitsPerSample`/`PhotometricInterpretation`, JPEG always 8); compression name; ICC profile from the **decoded** image (`Metadata.IccProfile` or `Frames[0].Metadata.IccProfile`) |

### The writer (`Input/FitsWriter.cs`, R-145, ADR 0032)

The one image the tool writes: a new FITS with a curve applied, hand-written like the reader.
From a FITS source the file is re-opened for reading only and copied as it is, byte for byte,
except two places of the image HDU: the header gets `HISTORY` cards before `END` (every other
card stays, in order; a full last block grows by one), and the data are written from the
raster in memory through the curve's table, with the source's `BITPIX`, `BZERO`, `BSCALE` and
row order (`WriteData` is `FitsReader.ReadData` inverted: integers are rounded and clamped to
their type). HDUs before and after the image pass unchanged. `FitsReader.ReadHeader` is shared
for the dictionary; the raw header bytes are re-read between the two stream positions it
leaves. From any other source the file is a 32-bit float FITS with a minimal header and
`ROWORDER = 'BOTTOM-UP'`. Either way the raster is written from its last row to its first, the
inverse of the reader's flip, so the file's order is the source's, or the one Siril shows the
right way up (ADR 0049). The values are the raster's: what the reader clipped to 0..1 stays
clipped. The write goes through `AtomicFile.Write` (`Input/AtomicFile.cs`), the one way the
tool writes a file, shared with the report's `ReportWriter.SaveFull` (R-005): an existing
target is opened for writing first, so that one held open by another program or refused is
reported with the system's message for the target itself; the content then goes to a
temporary file beside the target (`<name>.tmp-<guid>`; a folder that refuses to create it is
reported with the target's and the folder's names), moved over the name at the end and
deleted on any failure, a refused delete swallowed since the original exception is what
matters. An integer sample that falls outside its type by more than a
rounding (a signed 16-bit source without `BZERO` holds 0..32767) is counted, and one such
sample refuses the save: clipped silently the file would not be the curve's.

The input is protected in `CurveExport.Save`, not here, and twice. The path the image was read
from is kept in full on the result (`AnalysisResult.FullPath`), so a later change of the
working directory cannot lose it; a target equal to it is refused by name. A string compare
cannot see every spelling of one file (the review of 2026-10-05 overwrote the input through
`\\?\`, `\\.\` and the local admin share), so the input is also held open for reading with
`FileShare.Read`, no write and no delete, until the save ends: the final move over it fails in
the operating system. An input that is gone refuses the save, since a FITS would otherwise be
written without its header. On the plate-solved FITS the saved file keeps every card of the
source, has the same 270 869 760 bytes, and reads back equal to the curve applied to the
raster, with its WCS (V-008).

### The WCS (`Input/Wcs.cs`, `Input/Astrometry.cs`)

`Wcs.TryFrom(header, w, h, rowsFlipped)` reads the gnomonic projection Siril writes, or
returns null (never throws): `CTYPE1` starting `RA---TAN` and `CTYPE2` starting `DEC--TAN`
(a `-SIP` suffix accepted, its terms ignored: under one 32 px patch at the frame's edge, ADR
0025), `CRPIX`, `CRVAL`, and the linear part as `CD1_1..CD2_2`, as `PC` x `CDELT` (Siril 1.4),
or as `CDELT` + `CROTA2` (the AIPS convention). `PixelToSky` is the TAN deprojection of FITS
WCS Paper II with `LONPOLE` 180; `SkyToPixel` its inverse (NaN behind the tangent plane).
Both take **raster** coordinates (0-based, row 0 at the top) and convert to FITS pixels
inside: `y_fits = H - y` on a flipped raster, which every raster the reader makes is (ADR
0049), `y + 1` on one that is not, which only tests make. A WCS lent through `--wcs` is flipped
too, since the borrowing PNG is Siril's display of that FITS, the file's first row at the
bottom whatever its `ROWORDER` says; `WithRowsFlipped`, which re-targets a flip, is called by
tests only.
`ScaleDegPerPx` is the root of the determinant. `FitsReader` attaches the WCS to
`SourceInfo.Wcs`; `FitsReader.ReadImageHeader(path)` reads a header without its data, for
`AnalysisRun.BorrowWcs` (`--wcs`, R-027), which refuses a different size or a header without
a WCS. `Astrometry` holds the sphere: unit vectors, angular separation, the radius of a disc
of a given area, the galactic latitude (the band, ADR 0026), and the FK4-to-FK5 rotation
(B1950 to J2000, no E-terms; within 0.02' of VizieR's own precession over the whole
catalogue).

### The catalogue (`Input/DarkNebulaCatalogue.cs`, `Input/ldn.txt`)

Lynds' Catalogue of Dark Nebulae as an embedded text resource (`LogicalName` `ldn.txt`, 52
KB, 1787 numbered rows of VizieR's `VII/7A`): LDN number, B1950 centre (`h m.m`, `d m` with
the sign in the text so that `-00 30` stays negative), area in square degrees, opacity class,
Barnard cross-identifications (space-separated; a designation may carry a letter, `119A`;
VizieR's column is `8A4`, so the file was written from 4-character fields, not split on
spaces). `DarkNebulaCatalogue.All` parses it once, precessing the centres to J2000;
`DarkNebula.RadiusDeg` is the disc of the cloud's area and `Name` is `LDN n` or `LDN n (Bm,
...)`. Its notice is in `THIRD-PARTY-NOTICES.md` and `ThirdParty.cs` (a data component with
`Resources`, which `ThirdPartyTests` ties to the csproj like a package).

### The raster (`Input/Raster.cs`)

`Raster`: `W`, `H`, `Planes` = 1 or 3 `float[]` in **0..1**, row 0 at the **top**.
Normalisation: 8-bit `/255`, 16-bit `/65535`, 32-bit int `/4294967295`, float as is,
clipped to [0, 1] with the count noted. `Reference` is the green plane on colour, the
only plane on mono: the channel every single-channel measurement uses.

`SourceInfo` (attached by the reader): `Path`, `Format`, `BitDepth`, `IsFloat`,
`PlanesInFile`, `Compression`, `Icc` (bytes), `IccDescription` (from the `desc` tag, v2
text or v4 mluc, parsed by `IccProfileInfo`; null when absent or malformed),
`FileBytes`, `Wcs` (R-018, null without one), `Note` (reader observations: clipped values,
an ignored `TOP-DOWN` row order, palette, multi-page).

Memory: float planes cost `4 x W x H x planes` bytes - 277 MB for 23 MP RGB. Float was
chosen over double (554 MB) because 24 mantissa bits recover every 16-bit level exactly
(`RasterOps.Level16` round-trips all 65536 values; tested).

### Raster operations and the lift table (`Input/RasterOps.cs`, `Input/LiftTable.cs`)

`RasterOps` never modifies its input, and what returns a raster returns a new one, except
`Subsample` at a stride of 1 or less, which returns its input: `Subsample` (every n-th pixel,
anchored top-left, `a[::s, ::s]`), `Crop`, `Gamma` (v^gamma on every channel through a
65 537-entry `PowerLut` of `Math.Pow(k / 65535, gamma)`), `GammaFor` (ln(to) / ln(from)),
`Level8` / `Level16`, `LstarPlane` (L\* per pixel of a subsample). `Interpolate(lut, s)` is
the one reading of such a table, shared by `Gamma` and `LiftTable`: exact on a 16-bit level,
linear between two neighbouring levels, a NaN sample taken as 0 (it would index the table at
`int.MinValue`).

The linearisation of a sample has two paths behind one interface, `ISrgbLinearizer`, and the
two per-sample loops, `LstarPlane<T>` here and `DustAnalyzer.PatchLstars<T>`, are generic on
it so that the JIT specialises each loop on its struct and inlines the call: no delegate per
sample and one loop body for both paths. `ExactLinearizer` is `ColorMath.SrgbToLinear` on
the sample as it is: every as-is measurement. `LiftTable` is built for one gamma and holds
`SrgbToLinear(PowerLut(gamma)[k])` for every 16-bit level, interpolated linearly between two
neighbouring levels **on the linear value**; the print simulation lifts and linearises each
step of its ladder through one table and never builds the lifted raster (ADR 0035). Against
the lifted-raster path it replaced, the table agrees to 7.63e-6 of L\* per patch on the
reference images (one float ulp at L\* 64 to 128, two below 64) and to 2e-7 in luminance at an exact level; the
two interpolations differ most in the first 16-bit steps at a small gamma, where the power
curve's own step is widest, and no real sample sits there (`LiftTableTests`).

### Refusal of linear data

`RasterOps.LooksLinear`: median of the reference plane at stride 8 `< LinearMedianMax
(0.02)` -> `RefusedImageException` with the median in the message. A stacked linear
frame sits at 0.001-0.01; a finished image at 0.08-0.25.

### Colour maths (`Input/ColorMath.cs`)

sRGB <-> linear per IEC 61966-2-1; Y = 0.2126 R + 0.7152 G + 0.0722 B on linear values;
L\* per CIE 1976 with the linear segment below Y = 0.008856; **paper black** =
L\*(10^-Dmax): baryta 2.4 -> 3.6, canvas 1.5 -> 20.7, acrylic C-print 2.2 -> 5.7 (ADR 0028).
`YOfLstar` is the exact inverse of `LstarOfY` (both segments); `GrayLevelOfLstar` /
`LstarOfGrayLevel` go between an L\* and the 8-bit level of a grey (R = G = B) with that
L\* - one fixed curve, used only by the window's level - L\* table and guide text.
`LabOfSrgb` gives the CIE L\*a\*b\* of an sRGB pixel under D65, the white taken as the sRGB
matrix's own row sums (0.9505, 1, 1.0890) so that a grey is exactly neutral (the standard's
0.95047 and 1.08883 give it an a\* of 0.01); the background section's cast is its chroma
(ADR 0057).

### Statistics (`Input/Stats.cs`)

Percentiles use numpy's default linear interpolation between ranks (the acceptance
numbers were made with numpy). `PercentileSorted` reads a vector already sorted, for the callers
that sort once and read several ranks (Otsu, the class sets); `Percentile`, `Percentiles` and
`Median` read an unsorted vector by selection on one copy (Hoare's quickselect, the ranks put in
place from the lowest up), which gives the same two values the sorted vector would hold and so
the same bits, in linear time where sorting the stride-2 samples of a 23 MP image took about
half a second; a vector that holds a NaN takes the sorted path, whose order a selection on `<`
would not reproduce (R-017). `RobustSigma` is 1.4826 x the median absolute deviation,
with the median as a second result; `OtsuThreshold` on a sorted vector is the split that
maximises the variance between the two halves, the lower half at or under it, with a floor on
the size of the upper half (NaN when the vector cannot leave that many above; with all values
equal, the value itself, so nothing is above it). Both serve the
sky-relative classes (ADR 0033). `SeededRandom` is xorshift64\*, so tile selection is the
same on every runtime and every year.

## Things measured about ImageSharp 3.1.11 while building phase 1

Written down because each one cost a probe to find, and each one shapes code or tests:

- **The TIFF encoder writes 8 bits per sample for `Rgb48`, even with
  `BitsPerPixel = Bit48` requested.** 16-bit grey (`L16`) does encode. So a 16-bit RGB
  TIFF - the file Siril writes - cannot be produced through ImageSharp; the reader tests
  use a hand-built uncompressed TIFF (`TiffTestData`) instead, which is also the more
  honest test: decode of a foreign file, not encoder-decoder symmetry.
- **`Image.Identify` does not return a PNG's `iCCP` chunk** (it reads the chunks, checking
  their CRCs, but decodes no pixels and keeps no profile) and
  returns no frame metadata for PNG at all. `Image.Load` does populate
  `Metadata.IccProfile`. The reader therefore takes the profile from the decoded image.
- **The TIFF decoder puts the ICC profile on the first frame's metadata**
  (`Frames[0].Metadata.IccProfile`), not on `Image.Metadata`. The reader checks both.
- The PNG encoder does not write `iCCP` from `Metadata.IccProfile`; the PNG-with-profile
  test uses a hand-built PNG (`PngTestData`, with its own CRC-32 and `ZLibStream`).
