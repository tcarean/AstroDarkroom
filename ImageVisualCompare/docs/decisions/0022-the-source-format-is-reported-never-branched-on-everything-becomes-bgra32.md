# 0022. The source format is reported, never branched on: everything becomes Bgra32, and an unknown format is not refused

Date: 2026-10-02
Status: accepted 2026-10-02; the conversion of float samples by WIC superseded by 0035

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Structure": `ImageInfo.cs`, and
`Imaging/WicImageLoader.cs` on `Classify`), the `ImageInfo` summary, the comments of
`WicImageLoader.Load` and `Classify`, and `README.md` ("Ce fișiere îi dai"); the decision itself
dates from a6e5f10 (the base version, 2026-09-08). The two files are exports of one processing
in whatever form it wrote them: a "32 bit" export from Siril reaches the disk as a PNG with 16
bits per channel, and the README promises that a 16-bit PNG can be compared with an 8-bit TIFF.
The user needs to know what was opened ("ca să știi mereu ce ai deschis", says the README); the
display needs "a 16-bit PNG from Siril, an 8-bit screenshot and a palettised image" to "arrive
at the same place" (the `WicImageLoader` summary).

## Decision

- `WicImageLoader.Load` decodes with `BitmapCreateOptions.PreservePixelFormat`, so WIC does not
  convert on the way in and the `ImageInfo` it returns records the format the file really has.
  `Probe` reads only the header (ADR 0015); a test pins that it reports what a full load does.
- `Classify` maps the WIC pixel format to bits per channel and a `ColorChannels` value. A format
  it does not recognise falls through to its bits per pixel and `ColorChannels.Unknown`, and is
  described by the bits alone ("N-bit"); it is never refused. `ImageInfo.FormatDescription`
  puts the result in words, such as "16-bit RGB", and the status bar shows it for each image.
- Every image is then converted to Bgra32 (`FormatConvertedBitmap`), realised at once by a
  `CachedBitmap` with `BitmapCacheOption.OnLoad` while the file is still open, and frozen, so
  the UI thread can use a bitmap decoded on a worker.
- Nothing else reads the format. `ImagePairValidator.Inspect` compares width and height only,
  so bit depth and channel layout may differ between A and B.

## Alternatives rejected

- **Letting WIC convert on the way in.** The status bar would report what WIC chose, not what
  the file has; `PreservePixelFormat` "stops WIC from quietly converting on the way in" (the
  `Load` comment).
- **Refusing a format `Classify` does not know.** The conversion to Bgra32 "handles them
  regardless" (the `Classify` comment). What WIC cannot decode at all is not a question of
  format: it becomes a message (ADR 0015).
- **Keeping the 16-bit pixels beside the display copy.** About 250 MB more for a pair: the price
  the README puts on the difference mode it does not have yet ("Ce nu face (încă)").

## Consequences

- Every image costs four bytes a pixel in memory, whatever its depth on disk
  (`ImageInfo.DisplayBytes`): 84 MB for a 21 MP frame (the README's "Memorie").
- The pixel readout reads the Bgra32 copy, so its values run from 0 to 255 for every file (the
  README's "una-două unități din 255"); two 16-bit files that differ by less than one of those
  steps can read the same.
- A pixel format WIC decodes needs no code to be loaded; at most a line in `Classify` to be
  named.
- Guards: `WicImageLoaderTests` (six pixel formats and a TIFF reported as what they are, every
  result Bgra32 and frozen, the probe equal to the load), `ImageInfoTests` (the words, an
  unknown layout included), `ImagePairValidatorTests` (bit depth and channel layout need not
  match), `ImagePairLoaderTests` (a 16-bit and an 8-bit PNG load as one pair). The fall-through
  in `Classify` has no test of its own: no fixture is in a format it does not know.

Source: CLAUDE.md ("Structure"); README.md ("Ce fișiere îi dai", "Memorie", "Ce nu face (încă)");
src/ImageVisualCompare.Core/ImageInfo.cs; src/ImageVisualCompare.Core/ImagePairValidator.cs;
src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs; src/ImageVisualCompare.Wpf/MainWindow.xaml.cs
(`Describe`).
