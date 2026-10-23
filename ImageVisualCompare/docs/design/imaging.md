# Imaging: the decoder, the memory a pair costs, padding

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "No imaging dependency", "Memory,
> measured" and "Invariants" (one of its ten). Where a paragraph became a decision record, its
> number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/FitsImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/RoutingImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/Bitmaps.cs`,
> `src/ImageVisualCompare.Core/FitsHeader.cs`,
> `src/ImageVisualCompare.Core/FitsScale.cs`,
> `src/ImageVisualCompare.Core/FitsRowDecoder.cs`,
> `src/ImageVisualCompare.Core/FloatSamples.cs`,
> `src/ImageVisualCompare.Core/ImagePairValidator.cs`,
> and, for the padding, `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`,
> `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## No imaging dependency

WIC was chosen over SkiaSharp, ImageSharp and Magick.NET after comparing them, and the
reasoning still holds: it is the most complete PNG implementation on Windows, it costs
nothing at build time, and it is the only one that reaches the `Image` control without an
intermediate copy — which matters at 84 MB per image. It also gives access to the true source
format, which the status bar reports. See ADR 0008.

If a decoder ever has to change, `IImageLoader` is the seam. Replacing it is one class.

The seam carries one more promise: a loader that cannot read a file throws a type
`ImagePairLoader.IsExpectedLoadFailure` turns into a message, and a decoder's own failure types
are turned into those by its loader, never added to that list (ADR 0031). `WicImageLoader`
catches a `COMException` around everything WIC does in `Probe` and in `Load`, which a codec
throws for a damaged file (0xC00D36BE from Windows' WebP, HEIC and AVIF codecs), and rethrows a
`FileFormatException` with the codec's message. `Realise`, the `CachedBitmap` that sets the
converted pixels aside, turns WIC's `OverflowException` for an image too large for it into the
too-large message the FITS reader gives too. The bitmap the two loaders write rows into, and the
too-large refusal, are `Bitmaps.Create` and `Bitmaps.TooLarge`, shared: the FITS reader passes
the prefix `FITS: `, the WIC loader none.

## FITS, the format WIC does not read

Added on 2026-10-05 (ADR 0029). The path of a FITS file, from the dialog to the bitmap:

1. `RoutingImageLoader` opens the file, and `FitsHeader.IsFits` compares its first nine bytes
   with `SIMPLE  =`. A match goes to `FitsImageLoader`, anything else to `WicImageLoader`. The
   name of the file is never looked at. `LoadAsync` does this on the thread pool: the pair
   loader can resume on the UI thread after the padding question, and opening a file is disk
   work.
2. `FitsHeader.ReadLayout` walks the header-data units: 2880-byte blocks of 80-byte cards up to
   `END`, then the unit's data, padded to a block. It stops at the first unit that can hold an
   image (the primary, or an `IMAGE` extension) and has two axes or more, and steps over the
   others by their size (`DataBlockBytes`), which for a table needs `PCOUNT` and `GCOUNT`, for
   random groups leaves out the first axis, and for a unit with no axes is nothing, whatever
   its `PCOUNT` says (the standard: no data follow such a header). A binary table has two axes
   and 8 bits like a small image, which is why the extension's type is asked first; one with
   `ZIMAGE = T` is a tile-compressed image and is refused by name. After the primary, a block
   that does not start with `XTENSION` is not a header, and the search ends there with "no
   image": programs leave padding after the last unit. The result is a `FitsImageLayout`:
   size, planes, `BITPIX`, `BZERO`, `BSCALE`, the row order, and the offset of the first
   sample. The length of the stream is checked here, so a file cut short fails at the probe,
   before the padding question and before any decode.

   The header is input from outside and is not trusted. `Count` reads every keyword the
   layout is computed from: an integer, never negative except `BITPIX`, and a value that is
   neither makes the header invalid instead of falling back to a default. `Number` reads
   `BZERO` and `BSCALE`, and refuses NaN and the infinities in the same way: they parse as
   numbers ("NaN", "Infinity", an overflowing 1D999), and either would make every level black
   or white. The size of a unit's data is multiplied in a `checked` block, the number of axes
   is capped at the standard's 999, and an image's width is capped so that a row of its
   samples fits one array. The first version had none of this, and a negative axis sent the
   walk back to the header it had just read, for ever.
3. `FitsRowDecoder.ReadFileRow` fills one Bgra32 row. FITS is plane-sequential (all of red,
   then all of green, then all of blue), so the row is read from one place per plane; the
   decoder seeks, reads `Width` samples into its one row buffer, and `Convert` writes each
   through `FitsScale.Level` into the channel the plane is, in one loop per sample type so
   that nothing is decided per sample. For float files it also counts the finite samples
   above `FitsScale.RescaleThreshold`. The stream has no buffer of its own
   (`FitsImageLoader.OpenRead`): each read is a whole row, and a buffer would be filled past
   the row and thrown away at the next seek.
4. `FitsImageLoader.Load` walks the rows in the order of the file, so the disk is read
   forwards three times over, and writes each into a `WriteableBitmap` at
   `FitsImageLayout.DisplayRow`, which is where the row order is applied; the cancellation
   token is checked at every row. Then it asks `FitsScale.IsRescaled` about the count: more
   than one sample in a hundred above 2.0 means the file counts to 65 535, every byte written
   so far is wrong (nearly all white), and the same loop runs again with 65 535 as the full
   scale. Then `Freeze`. A size WPF makes no bitmap of becomes the too-large message here.

`FitsScale.Level` is the one place a value becomes a level: the value, already multiplied to
count in 0 to 65 535 (`ToSixteenBits`, one multiplication a sample instead of a division),
is clamped, rounded to a 16-bit sample, and its high byte kept. The last step is WIC's own
reduction of a 16-bit sample, measured, and it is what makes a FITS and its 16-bit export
agree to the level; `FitsScaleTests` walks all 65 536 values on each scale a file can have.

What a FITS costs, on `result_3813s.fit` (5938 x 3888, three float planes, 277 MB on disk):
about 0.6 s with the file in the cache (1.1 to 1.2 s for the first load of a process), +89.7
MB private once loaded, the same as a 16-bit PNG of that size through WIC, and a transient
peak of one more buffer during the load (peak working set 227 MB against WIC's 139 MB). The
managed alternative and the figures of a pair are in ADR 0029 and in `../validation.md`,
V-003 and V-004.
## Float samples in a TIFF

Added on 2026-10-22 (ADR 0035). WIC decodes a float TIFF, but its conversion to Bgra32 takes
float samples for linear light and brightens them, by 73 levels on average on GraXpert's
exports, where a FITS of the same samples is shown as stored. So `WicImageLoader` asks one
question of the format, whether its samples are floats (`FloatSamplesOf`): a format WPF names,
`Gray32Float`, `Rgb128Float`, `Rgba128Float` or `Prgba128Float`, or a TIFF whose `SampleFormat`
tag is 3, read from the frame's `BitmapMetadata` at `/ifd/{ushort=339}`, with
`SamplesPerPixel` (277) for the channels and `BitsPerSample` (258), which must be 32: a 16-bit
half or a 64-bit double would go through a conversion to a 128-bit float format that was never
measured, so those keep WIC's own path. The tags are asked only of a `TiffBitmapDecoder`'s
frame, and the header-only frame of `Probe` has them too.
The 96 bpp RGB that Siril and GraXpert write is the case that needs them: WPF has no name for
it, and shows its format as "Default" with 96 bits.

`LoadFloat` has WIC convert the frame to `Gray32Float`, `Rgb128Float` or `Rgba128Float`, which
copies the values: on two GraXpert exports, 6016 × 4016, its `Rgb128Float` floats equal the ones
read straight from the file bit for bit, all 72 480 768 samples of each (2026-10-22). `Decode`
then copies a band of rows at a time, at most 8 MB of floats, into
`FloatSamples.ToBgra32` in Core, which puts each sample through `FitsScale.Level` as
`FitsRowDecoder` does and counts the samples `FitsScale.IsAboveThreshold` takes. Then, as for a
FITS, `FitsScale.IsRescaled` decides whether the image counts to 65 535, and if it does the
bands are decoded again on that scale. A size WPF makes no bitmap of is the too-large message.

## Memory, measured

Measured on a 5794×3637 16-bit pair, ~108 MB each on disk:

| | private | working set |
|---|---|---|
| headless load, both images | 173 MB | 199 MB |
| after release | 12 MB | 38 MB |
| loaded and rendered, at fit | 283 MB | 290 MB |
| plateau after visiting several zoom levels | ~570 MB | ~300 MB |

The plateau is bounded, not a leak: five zoom cycles left it flat and it drifts down when
idle. The extra over the headless figure is WPF's render thread keeping scaled intermediates
for the Fant resampling path.

Two things follow. WIC **streams** the Rgb48 to Bgra32 conversion rather than materialising a
126 MB intermediate, so there is no transient spike above the steady state. And the managed
heap stays near zero because WIC pixels are unmanaged — which is why `ReleaseImageBuffers`
forces a finaliser pass and does *not* bother with large object heap compaction. See ADR 0016.

The same table is kept, as the history of the figures, in `../validation.md`; the figures
measured since on a real pair are its cases.

## Invariants

This is one of the ten invariants of the former `CLAUDE.md`; the list's introduction and seven
others are in `rendering.md`, two in `shell.md`.

**Padding is layout, never pixels.** When the two images differ in size they are compared on
a canvas as large as the pair needs, and the smaller one simply does not reach the far edge —
the backdrop is what fills the gap. Nothing is copied into a larger buffer — at 84 MB an
image, that copy would be the most expensive thing here. Both are anchored top-left, and that is what
keeps a content coordinate equal to a pixel coordinate in *both* images: the pixel readout
needs no offsets and prints `—` past the end of an image on its own bounds check. Centring
would break that, and `PadPlan` is where the offsets would have to go. See ADR 0014.
