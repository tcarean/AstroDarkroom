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
  fields: focal length, f-number, camera, exposure, ISO, time of capture, pixel size, the
  raw's 35 mm-equivalent focal length (`FocalLength35Mm`, libraw's
  `LensInfo.FocalLengthIn35mmFormat`) and visible size (`Width`, `Height`, from libraw's sizes
  at the open, before any decode; 0 on a FITS), which `PixelPitch` turns into a pixel size
  (`series-analysis.md`, R-066), and the lens as the raw gives it: `LensId`, `LensMount`,
  `LensName` and the range.
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
  to 2880 bytes is not required.
- Taken later from FlatCheck: the FITS-magic check of its ADR 0057 (R-008, ADR 0027). One
  keyword reader, `KeyOf`, and one check, `RequireSimple`, serve both reads: `LoadHeader` runs
  it on the first block read and refuses a block shorter than 2880 bytes (`HeaderIncomplete`),
  `LoadFull` runs it on the whole file before the header loop. A file whose first 8 bytes are
  not the keyword `SIMPLE`, an empty file included, throws `InvalidDataException` with
  `NotSimple`; a header that ends before `END` throws `HeaderIncomplete`; the two reads refuse
  the same files with the same words, so no caller recognises a bad file by an empty header.
- Taken later from FlatCheck: the English/Romanian wordings of `FitsReader`'s messages
  (FlatCheck's 55e959c, ADR 0022), word for word, through `using static LightsCheck.Core.Ui`.
  `RawReader`'s messages are paired here, in FlatCheck's English where FlatCheck has the same
  message.
- Taken later from FlatCheck, directly into the adapted copies (ADR 0025, R-065): the lens hunk
  of its ADR 0068 (R-108, 2026-10-26). `FillShootingInfo` reads the lens in one `try`: the
  mount is the lens's (`MakerNotes.LensMount`) or, when that is 0, the camera's (`MountOf`);
  the ID is Nikon's `LensIDNumber` when above 0, else the MakerNote's `LensID` on a mount that
  is not Nikon's, above 0 and within 31 bits (`LensIdOf`; on a Nikon that field is the LensData
  signature); the mount's label comes from a table over libraw's `CameraMount` enumeration
  (`MountLabel`, null for a mount it does not spell); the name is `LensInfo.Lens` trimmed; the
  range takes the MakerNote's focal ends, else the top fields, and the aperture from
  `MaxAperture4MinFocal`, else the top field, else `MaxAperture` only on a prime or at the
  short end (`RangeOf`, with `Close` inlined from FlatCheck's `LensSpec.Close`: 1 mm or 5 %).
  A failure in the block leaves the lens fields null and the shot fields as read. `.pef` is the
  seventh extension. The comments are LightsCheck's: what the fields are for here is the
  inventory's lens line (`session-and-console.md`).
- The diagnostic log (ADR 0029): `ImageLoader` writes the line per file read, the FITS
  keywords at Debug and every card at Trace from what the reader returns, and `RawReader`
  libraw's figures at Debug, composed by `FiguresLine` from plain values so no closure of a
  libraw local is ever made (the mechanic below), its lens block's failure at Warn, and the
  runtime check's line at Warn, once (above);
  `FitsReader` does not log, where FlatCheck's does since its ADR 0070, so the copy stays as it
  was (`logging.md`).
- LightsCheck's own, when libraw cannot be loaded (R-074, ADR 0030). `Load` and `LoadInfo`
  turn the loader's failures (`DllNotFoundException`, a `FileNotFoundException` for another file
  than the raw, `BadImageFormatException`, `TypeInitializationException`) into `Missing`'s
  `InvalidOperationException`. Its message names Microsoft's Visual C++ runtime as the likely
  cause and ends with `RuntimeUrl`, and keeps the loader's exception, whose words name
  `libraw.dll`, as the inner one. The DLLs travel with the program in both forms it ships: beside
  it in the suite folder, inside the single file of the own release, which extracts them under
  `%TEMP%\.net\<program>\<bundle id>\` at start. So the message says nothing of where they sit.
  `Missing` first asks `RuntimeCheck.FirstLine`, which gives the log line once per check and null
  afterwards, an `Interlocked` flag deciding among the parallel frames. The process's check,
  `Runtime`, asks `CanLoad` (`NativeLibrary.TryLoad` by bare name, freed at once) for each of
  `RuntimeFiles`, and `RuntimeLine` words the answer for the Warn line. A failing series fails
  twice per frame, the measurement then the metadata, so asking every time would cost the PATH
  search 174 times on the Veil session. The bare name is searched for from the program's folder,
  then the system folders, the current folder and PATH. `raw_r.dll`'s own imports are searched
  for from `raw_r.dll`'s folder instead, since .NET loads it by full path with
  `LOAD_WITH_ALTERED_SEARCH_PATH`, then the same places. The two differ only in the single-file
  release, where that folder is the extraction folder, and only for a runtime DLL copied into one
  of them. `RuntimeDlls` is what the import tables of `Sdcb.LibRaw.runtime.win64` 0.21.1's DLLs
  name. `RawReaderTests` reads those tables from the build's `runtimes/win-x64/native` with
  `PEReader`, so another LibRaw build is read again before it ships.
- Taken later from FlatCheck, the last sentence of its R-009 (469da9b, ADR 0025): `LoadHeader`'s
  comment says the header read does not check the data area, so a cut file still answers the
  metadata read and only the full load refuses it.
- Time: libraw's `Timestamp` is the EXIF `DateTimeOriginal` run through `mktime` on this
  machine, so `DateTimeOffset.FromUnixTimeSeconds(..).ToLocalTime()` gives back the camera's
  wall-clock to the second; EXIF `SubSecTime` is not exposed. Siril's `DATE-OBS` is UTC and is
  parsed as such; the two are never mixed in one session, so neither is converted.

## The one mechanic worth repeating

No field of `RawReader` may be a `Sdcb.LibRaw` type (`RawReaderTests` pins it): fields are
resolved when the type loads, which happens on every run because `ImageLoader` asks it
`Handles()`, while method bodies resolve at JIT time. With that, a FITS run never loads the
libraw assembly and a copy of `lightscheck.exe` without the native DLLs still reads FITS.
