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
outside (0, 1), the package's `Error`, or an exception from it becomes a `Problem`.
`Installed(folder)` lists the `.icc` / `.icm` files of a folder (default
`<System>\spool\drivers\color`) whose class is `prtr`, read the same way, by name; other
classes and non-profiles are skipped, an absent folder is an empty list. `Describe(IccBlack)`
is the sentence the window's list shows. `Parameters.WithDmaxFromProfile(path, out error)` is
what the console calls: the same parameters with `DmaxOverride` and `DmaxProfile` set, or null
with the problem.

### Formats and readers (`Input/`)

`ImageLoader` picks a reader **by extension** (never by sniffing) and is the only place
that knows both. An unknown extension is an error naming the accepted ones.

| reader | extensions | what it reads |
|---|---|---|
| `FitsReader` (hand-written, streaming) | `.fit .fits .fts` | first HDU with NAXIS 2 or 3; BITPIX 8/16/32 int (BZERO/BSCALE applied) and -32/-64 float; NAXIS3 = 1 or 3, anything else refused; rows flipped when `ROWORDER = 'BOTTOM-UP'`; empty primary HDUs skipped |
| `ImageSharpReader` | `.tif .tiff .png .jpg .jpeg` | colour decoded as `Rgb48`, mono as `L16`; bit depth and channel count from the format's own metadata (PNG `BitDepth`/`ColorType`, TIFF `BitsPerSample`/`PhotometricInterpretation`, JPEG always 8); compression name; ICC profile from the **decoded** image (`Metadata.IccProfile` or `Frames[0].Metadata.IccProfile`) |

### The WCS (`Input/Wcs.cs`, `Input/Astrometry.cs`)

`Wcs.TryFrom(header, w, h, rowsFlipped)` reads the gnomonic projection Siril writes, or
returns null (never throws): `CTYPE1` starting `RA---TAN` and `CTYPE2` starting `DEC--TAN`
(a `-SIP` suffix accepted, its terms ignored: under one 32 px patch at the frame's edge, ADR
0025), `CRPIX`, `CRVAL`, and the linear part as `CD1_1..CD2_2`, as `PC` x `CDELT` (Siril 1.4),
or as `CDELT` + `CROTA2` (the AIPS convention). `PixelToSky` is the TAN deprojection of FITS
WCS Paper II with `LONPOLE` 180; `SkyToPixel` its inverse (NaN behind the tangent plane).
Both take **raster** coordinates (0-based, row 0 at the top) and convert to FITS pixels
inside: `y_fits = H - y` when the rows were flipped (`ROWORDER = BOTTOM-UP`), `y + 1`
otherwise; `WithRowsFlipped` re-targets a WCS lent to a raster of another orientation.
`ScaleDegPerPx` is the root of the determinant. `FitsReader` attaches the WCS to
`SourceInfo.Wcs`; `FitsReader.ReadImageHeader(path)` reads a header without its data, for
`AnalysisRun.BorrowWcs` (`--wcs`, R-027), which refuses a different size or a header without
a WCS. `Astrometry` holds the sphere: unit vectors, angular separation, the radius of a disc
of a given area, and the FK4-to-FK5 rotation (B1950 to J2000, no E-terms; within 0.02' of
VizieR's own precession over the whole catalogue).

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
flipped rows, palette, multi-page).

Memory: float planes cost `4 x W x H x planes` bytes - 277 MB for 23 MP RGB. Float was
chosen over double (554 MB) because 24 mantissa bits recover every 16-bit level exactly
(`RasterOps.Level16` round-trips all 65536 values; tested).

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

### Statistics (`Input/Stats.cs`)

Percentiles use numpy's default linear interpolation between ranks (the acceptance
numbers were made with numpy). `SeededRandom` is xorshift64\*, so tile selection is the
same on every runtime and every year.

## Things measured about ImageSharp 3.1.11 while building phase 1

Written down because each one cost a probe to find, and each one shapes code or tests:

- **The TIFF encoder writes 8 bits per sample for `Rgb48`, even with
  `BitsPerPixel = Bit48` requested.** 16-bit grey (`L16`) does encode. So a 16-bit RGB
  TIFF - the file Siril writes - cannot be produced through ImageSharp; the reader tests
  use a hand-built uncompressed TIFF (`TiffTestData`) instead, which is also the more
  honest test: decode of a foreign file, not encoder-decoder symmetry.
- **`Image.Identify` does not read a PNG's `iCCP` chunk** (it stops at the header) and
  returns no frame metadata for PNG at all. `Image.Load` does populate
  `Metadata.IccProfile`. The reader therefore takes the profile from the decoded image.
- **The TIFF decoder puts the ICC profile on the first frame's metadata**
  (`Frames[0].Metadata.IccProfile`), not on `Image.Metadata`. The reader checks both.
- The PNG encoder does not write `iCCP` from `Metadata.IccProfile`; the PNG-with-profile
  test uses a hand-built PNG (`PngTestData`, with its own CRC-32 and `ZLibStream`).
