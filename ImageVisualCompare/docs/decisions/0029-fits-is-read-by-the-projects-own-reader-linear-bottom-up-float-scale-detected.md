# 0029. FITS is read by the project's own reader in Core: shown linearly, bottom-up unless the file says otherwise, the float scale detected

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

The owner's processing lives in FITS files (Siril, GraXpert), and WIC has no codec for them, so
comparing two variants meant exporting each first. Three choices had more than one reasonable
answer and were put to the owner on 2026-10-05, with a recommendation each; the answers are the
decision below. Measured the same day: WIC reduces a 16-bit sample to 8 bits by keeping its high
byte (`v >> 8`, all 65 536 values through a PNG round trip; rounding matches only 49 280 of
them). The five files of `E:\Photografie\Claude\Processed_WithFlats_RBF_light` are all 5938 x
3888, three planes of 32-bit float, `ROWORDER = 'BOTTOM-UP'`, written by Siril 1.4.4, with no
NaN and no negative sample; their largest samples are 0.017, 0.025, 1, 1 and 1.0005. On
`result_3813s.fit` (277 MB), decoding rows into a `WriteableBitmap` took 1.2 to 1.3 s and left
+89.7 MB private, as a WIC load of `de_printat.png` does (+89.5 MB); decoding into one managed
buffer handed to `BitmapSource.Create` took 1.2 to 1.5 s and left +179 MB, which only an
aggressive, compacting collection gave back.

An independent review of the first version, the same day, found that it trusted the header (a
negative `NAXISn` or `PCOUNT`, or `NAXIS = 2147483647`, made `ReadLayout` loop for ever; a size
that overflowed 64 bits passed the length check and ended in an `OverflowException` nothing
caught), and that deciding the float scale on the largest sample let one hot pixel above 2 turn
a whole image in 0 to 1 black. The owner's answer to the review was to fix all of it.

## Decision

- **The project's own reader, in Core, no package.** `FitsHeader`, `FitsScale` and
  `FitsRowDecoder` are pure code over a `Stream`, started from AstroPhotoAnalyzer's
  `FitsReader`; `FitsImageLoader` in the Wpf assembly is a second class behind `IImageLoader`,
  and `RoutingImageLoader` sends a file to it when the file starts with `SIMPLE  =`, to
  `WicImageLoader` otherwise. ADR 0008 stands: WIC still decodes everything it can, and
  `ImagePairLoader` did not change.
- **Linear, as stored (the owner's answer).** A value is divided by the full scale of its type,
  brought to 16 bits and reduced as WIC reduces a 16-bit PNG, so a FITS and its own export show
  the same levels on the two sides of the divider. Nothing stretches: a linear stack looks
  nearly black, and that is accepted.
- **Bottom-up unless the file says `TOP-DOWN` (the owner's answer).** `ROWORDER` is obeyed; a
  file without it has its first row at the bottom, the FITS convention and what Siril shows.
- **The float scale is detected (the owner's answer), by a share of the samples.** A float file
  is read as 0 to 1 unless more than `FitsScale.RescaleFraction`, 1 in 100, of its samples are
  finite and above `FitsScale.RescaleThreshold`, 2.0; then it is read as 0 to 65 535, decoded a
  second time, and the status bar says `(0..65535)`. 2.0 is above the largest overshoot
  measured, 1.0005; none of the five files has a sample above it. The share is a choice, not a
  measurement: no float file in 0 to 65 535 was at hand, and in one nearly every sample is
  above 2, so anything from a few in a million to a half would tell the two apart.
- **Rows go straight into a `WriteableBitmap`**, one row buffer at a time, in the order of the
  file; the image is never held as floats.
- **The header is not trusted.** The keywords the layout is computed from must be integers,
  and none but `BITPIX` negative; a unit has at most 999 axes; sizes are multiplied with
  overflow checked. Anything else is `FITS: the header is not valid.` or `FITS: an image of
  W × H is too large to show.`, at the probe, never a loop and never an exception the pair
  loader does not expect.

## Alternatives rejected

- **A stretch, always or on a key.** Always: a processed FITS would no longer look like its
  export, and the RGB readout would not be the file's. On a key: a feature of its own
  (requirements, a decision on which stretch), not part of opening a format.
- **Top-down unless `BOTTOM-UP`**, as AstroPhotoAnalyzer's reader does. A file from another
  program, without the keyword, would show upside down against Siril's display of it.
- **Clipping floats to 0..1 and nothing else.** A float file written in 0 to 65 535 would be
  white everywhere.
- **Trusting `DATAMAX` for the scale.** None of the five files has the keyword.
- **Deciding the scale on the largest sample.** The first version did: a 2 x 1 file holding
  0.5 and 2.5 loaded as two black pixels marked `(0..65535)`.
- **A FITS package, or a reference to AstroPhotoAnalyzer's assembly.** The format is 80-byte
  cards and big-endian samples, about 300 lines; the rule "No imaging dependency" asks for no
  package, and the sibling projects share no assemblies. Its reader also keeps the planes as
  floats, 277 MB an image here.
- **One managed Bgra32 buffer and `BitmapSource.Create`.** Twice the memory until a compacting
  collection, which `ReleaseImageBuffers` does not do (ADR 0016), for no gain in time.
- **Rounding to the nearest level instead of the high byte.** More faithful taken alone, but a
  FITS against its own 16-bit export would then differ by one level on about a quarter of the
  samples, in a tool built to show differences of one level.

## Consequences

- `IImageLoader` now has three implementations; the application constructs
  `RoutingImageLoader`. A FITS under any extension is recognised, and a PNG named `.fit` still
  goes to WIC.
- `ImageInfo` gained `IsFloat` and `ValueRange`, both optional. `Probe` cannot know the scale,
  so for a rescaled file the loaded image's `ImageInfo` differs from the probed one by
  `ValueRange` alone; R-011's "exactly what a full load does" holds for every other file.
- The pixel readout stays 0 to 255 (ADR 0022): a float FITS loses more to it than a 16-bit PNG.
- Not read, each with its own message (R-019): tile-compressed images, `BITPIX` 64, more than 3
  axes, 2 or more than 3 planes. A colour filter array is shown as the gray mosaic it is.
- A float file in 0 to 65 535 is decoded twice. A float file in some third scale (0 to 255, 0 to
  4095) is read as 0 to 65 535 and looks dark; none has been met. A file in 0 to 65 535 in
  which 99 samples in 100 are 2 or less would be read as 0 to 1, and be white where it is not
  black.
- The decode is one loop per sample type over a stream with no buffer of its own: on
  `result_3813s.fit`, repeated loads in one process took 0.92 to 0.98 s before that change and
  0.59 to 0.61 s after it. The load checks its cancellation token at every row, and
  `RoutingImageLoader.LoadAsync` opens the file on the thread pool, not on the caller's thread.
- Where WPF's limit on a bitmap lies was not found: 24 000 x 24 000 was created, 46 341 x
  46 341 threw `OverflowException`. `FitsImageLoader` turns that into the too-large message;
  no test reaches it, since the file would have to hold the pixels.
- A first FITS load in a process leaves about 29 MB of private memory that later loads do not
  add to (five load and release cycles of V-003's pair: +29.7, +29.5, +29.8, +29, +30.1 MB).
- Guards: `FitsHeaderTests`, `FitsScaleTests`, `FitsRowDecoderTests` (Core, on files built in
  memory by `FitsFixture`), `FitsImageLoaderTests` (the same samples as 16-bit FITS and as
  16-bit PNG give identical bytes; routing; the messages), and V-003 and V-004 of
  `docs/validation.json` on the real files.
