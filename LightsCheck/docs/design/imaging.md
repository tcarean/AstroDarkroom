# The readers copied from FlatCheck

**Covers:** `src/LightsCheck.Core/Imaging/RawReader.cs`, `src/LightsCheck.Core/Imaging/ImageLoader.cs`, `src/LightsCheck.Core/Imaging/FitsReader.cs`, `src/LightsCheck.Core/Imaging/CfaPattern.cs`, `src/LightsCheck.Core/Imaging/Img.cs`, `src/LightsCheck.Core/Imaging/Mosaic.cs`, `src/LightsCheck.Core/Imaging/Stats.cs`, `src/LightsCheck.Core/Imaging/PngWriter.cs`, `src/LightsCheck.Core/Imaging/BitmapFont.cs`.

The decision and the reasons are ADR 0003; this note says what each file is for here and what
differs from its source, FlatCheck 2.3.0 (`E:\Photografie\Code\FlatCheck\FlatCheck.Core`),
whose design notes (`docs/design/modes-expo.md`, `pipeline.md`, `png-output.md`,
`dependencies.md`) explain the mechanics in full and are not repeated.

## What each file does here

- `ImageLoader` is the one place that decides raw or FITS, by extension, with FITS as the
  fallback for anything unclaimed. `LoadedFrame` is what every reader returns: the `Img`
  (null for a metadata-only load), the FITS header (empty, not null, for a raw), the exact
  `AduScale` (null for a FITS), the `CfaPattern` (null when the file is silent), and the shot
  fields: focal length, f-number, camera, exposure, ISO, time of capture, pixel size, Nikon
  lens ID and lens range.
- `RawReader` reads the visible CFA mosaic at the original ADU scale through libraw, with the
  black level as the median of the four channel blacks, the white as `color.maximum`, the
  colour order from `filters` and `cdesc` evaluated at the visible origin, and the shot
  fields from `imgother` (`Shutter`, `IsoSpeed`, `Timestamp`, `FocalLength`, `Aperture`) and
  `LensInfo`. `LoadInfo` opens the file without `Unpack`: tens of milliseconds per NEF (the
  87 frames of the Veil session in 1.2 s), which is what the inventory uses.
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
- Renamed: `ExposureScale` is `AduScale`; the namespace is `LightsCheck.Core`.
- Added: the shot metadata above; `Camera` reads "Nikon D3300" (libraw gives make "Nikon",
  model "D3300"; the model is prefixed with the make only when it does not already start with
  the make's first word, so "SONY" + "ILCE-7M3" reads "SONY ILCE-7M3").
- Exceptions: `FitsReader` throws `InvalidDataException` where FlatCheck threw `Exception`
  (the kit's analyzer CA2201); `FrameInfo` and the console catch it. `RawReader` throws the
  same type, naming the file in the run's language, when libraw refuses to open or unpack one (`Open`,
  `Decode`; libraw's `LibRawException` and its English words only as the inner exception,
  ADR 0020), so a raw that is not a raw is one more unreadable frame to every caller (ADR 0019).
- Taken later from FlatCheck: the cut-data check of `LoadFull` (FlatCheck's R-009, 469da9b,
  ADR 0021). Before the sample loop of every HDU, a data area shorter than `NAXISn` x
  `|BITPIX| / 8` throws `InvalidDataException` with both byte counts (`DataCut`); the padding
  to 2880 bytes is not required. FlatCheck's FITS-magic check is not taken (ADR 0018, ADR 0021).
- Taken later from FlatCheck: the English/Romanian wordings of `FitsReader`'s four messages
  (FlatCheck's 55e959c, ADR 0022), word for word, through `using static LightsCheck.Core.Ui`;
  FlatCheck holds the incomplete-header message in a `HeaderIncomplete` property, the copy writes
  the same `T(...)` inline where it throws, so a recopy shows that one difference.
  `RawReader`'s messages are paired here, in FlatCheck's English where FlatCheck has the same
  message.
- Time: libraw's `Timestamp` is the EXIF `DateTimeOriginal` run through `mktime` on this
  machine, so `DateTimeOffset.FromUnixTimeSeconds(..).ToLocalTime()` gives back the camera's
  wall-clock to the second; EXIF `SubSecTime` is not exposed. Siril's `DATE-OBS` is UTC and is
  parsed as such; the two are never mixed in one session, so neither is converted.

## The one mechanic worth repeating

No field of `RawReader` may be a `Sdcb.LibRaw` type (`RawReaderTests` pins it): fields are
resolved when the type loads, which happens on every run because `ImageLoader` asks it
`Handles()`, while method bodies resolve at JIT time. With that, a FITS run never loads the
libraw assembly and a copy of `lightscheck.exe` without the native DLLs still reads FITS.
