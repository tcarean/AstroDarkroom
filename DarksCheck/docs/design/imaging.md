# The readers copied from LightsCheck

**Covers:** `src/DarksCheck.Core/Imaging/RawReader.cs`, `src/DarksCheck.Core/Imaging/ImageLoader.cs`, `src/DarksCheck.Core/Imaging/IFrameReader.cs`, `src/DarksCheck.Core/Imaging/FitsReader.cs`, `src/DarksCheck.Core/Imaging/CfaPattern.cs`, `src/DarksCheck.Core/Imaging/Img.cs`, `src/DarksCheck.Core/Imaging/Mosaic.cs`, `src/DarksCheck.Core/Imaging/Stats.cs`, `src/DarksCheck.Core/Imaging/PngWriter.cs`, `src/DarksCheck.Core/Imaging/BitmapFont.cs`.

The decision and the reasons are ADR 0003; the files are LightsCheck's
(`E:\Photografie\Code\LightsCheck\src\LightsCheck.Core\Imaging`, `main` 6cc0078), their code
unchanged but for the namespace. Their comments are DarksCheck's own, English, and describe what
the files do here (ADR 0019): a refresh from LightsCheck takes its code and keeps these comments,
and a member DarksCheck does not call says so. This note says what each file is for here and
what LightsCheck changed against its own source, FlatCheck 2.3.0 (`E:\Photografie\Code\FlatCheck\FlatCheck.Core`),
whose design notes (`docs/design/modes-expo.md`, `pipeline.md`, `png-output.md`,
`dependencies.md`) explain the mechanics in full and are not repeated.

## What each file does here

- `ImageLoader` is the one place that decides raw or FITS, by extension, with FITS as the
  fallback for anything unclaimed. `LoadedFrame` is what every reader returns: the `Img`
  (null for a metadata-only load), the FITS header (empty, not null, for a raw), the exact
  `AduScale` (null for a FITS), the `CfaPattern` (null when the file is silent), and the shot
  fields: focal length, f-number, camera, exposure, ISO, time of capture, pixel size, and the
  lens as the raw gives it: `LensId`, `LensMount`, `LensName` and the range.
- `RawReader` reads the visible CFA mosaic at the original ADU scale through libraw, with the
  black level as the median of the four channel blacks, the white as `color.maximum`, the
  colour order from `filters` and `cdesc` evaluated at the visible origin, and the shot
  fields from `imgother` (`Shutter`, `IsoSpeed`, `Timestamp`, `FocalLength`, `Aperture`) and
  `LensInfo`. `LoadInfo` opens the file without `Unpack`: milliseconds per NEF (the 82 raws
  of the Smida session in 0.3 s, measured on 2026-10-11), which is what the inventory uses.
- `FitsReader` parses the header and the first 2D HDU; `LoadInfo` reads the header only.
  Its `FillShootingInfo` also reads `EXPTIME`, `ISOSPEED`, `DATE-OBS` and `XPIXSZ`.
- `CfaPattern` names the four Bayer positions from `filters`+`cdesc` or `BAYERPAT`, shifted
  by `XBAYROFF`/`YBAYROFF` and by `ROWORDER`; `Mosaic` splits an `Img` into its four
  positions; `Img` is the double raster with block means and tile medians; `Stats` keeps
  percentiles, interpolation and a Gaussian solver; `PngWriter` encodes RGB and grey rasters
  with a hand-rolled deflate stream and offers the diverging colormap; `BitmapFont` draws
  labels into a raster.

## What differs from the source

- Cut: the embedded JPEG preview (`EmbeddedPreview`, `PreviewReader`, ImageSharp), the
  residual-map and mosaic encoders of `PngWriter` (`MapRange`, `AutoRange`, `EncodeMosaic`),
  and the verdict ladder of `Stats` (`Verdict`, `ResidualVerdict`, `Severity`), which read
  FlatCheck's thresholds.
- Renamed: `ExposureScale` is `AduScale`; the namespace is `DarksCheck.Core`.
- Added: the shot metadata above; `Camera` reads "Nikon D3300" (libraw gives make "Nikon",
  model "D3300"; the model is prefixed with the make only when it does not already start with
  the make's first word, so "SONY" + "ILCE-7M3" reads "SONY ILCE-7M3").
- Exceptions: `FitsReader` throws `InvalidDataException` where FlatCheck threw `Exception`
  (the kit's analyzer CA2201); `FrameInfo` and the console catch it.
- Taken later from FlatCheck: the cut-data check of `LoadFull` (FlatCheck's R-009, 469da9b,
  ADR 0020). Before the sample loop of every HDU, a data area shorter than `NAXISn` x
  `|BITPIX| / 8` throws `InvalidDataException` with both byte counts (`DataCut`); the padding to
  2880 bytes is not required. FlatCheck's FITS-magic check is not taken (ADR 0020).
- Taken later from FlatCheck, directly (ADR 0023): the lens hunk of its ADR 0068 (R-108,
  2026-10-26). `FillShootingInfo` reads the lens in one `try`: the mount is the lens's
  (`MakerNotes.LensMount`) or, when that is 0, the camera's (`MountOf`); the ID is Nikon's
  `LensIDNumber` when above 0, else the MakerNote's `LensID` on a mount that is not Nikon's,
  above 0 and within 31 bits (`LensIdOf`; on a Nikon that field is the LensData signature); the
  mount's label comes from a table over libraw's `CameraMount` enumeration (`MountLabel`, null
  for a mount it does not spell); the name is `LensInfo.Lens` trimmed; the range takes the
  MakerNote's focal ends, else the top fields, and the aperture from `MaxAperture4MinFocal`,
  else the top field, else `MaxAperture` only on a prime or at the short end (`RangeOf`, with
  `Close` as FlatCheck's `LensSpec.Close`: 1 mm or 5 %). A failure in the block leaves the lens
  fields null and the shot fields as read. `.pef` is the seventh extension. The next refresh
  from LightsCheck must carry this hunk or wait until LightsCheck takes it.
- Taken later, the messages' English: every message the readers throw is a `T(english,
  romanian)` pair (ADR 0021). `FitsReader`'s English is FlatCheck's, word for word;
  `RawReader`'s messages are LightsCheck's adaptations and their English was written here, for
  LightsCheck to take back. A refresh from LightsCheck keeps the pairs.
- Time: libraw's `Timestamp` is the EXIF `DateTimeOriginal` run through `mktime` on this
  machine, so `DateTimeOffset.FromUnixTimeSeconds(..).ToLocalTime()` gives back the camera's
  wall-clock to the second; EXIF `SubSecTime` is not exposed. Siril's `DATE-OBS` is UTC and is
  parsed as such; the two are never mixed in one session, so neither is converted.

## The one mechanic worth repeating

No field of `RawReader` may be a `Sdcb.LibRaw` type (`RawReaderTests` pins it): fields are
resolved when the type loads, which happens on every run because `ImageLoader` asks it
`Handles()`, while method bodies resolve at JIT time. With that, a FITS run never loads the
libraw assembly and a copy of `darkscheck.exe` without the native DLLs still reads FITS.
