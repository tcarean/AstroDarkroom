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
> `src/ImageVisualCompare.Core/DisplayMode.cs`,
> `src/ImageVisualCompare.Core/DisplayRenderer.cs`,
> `src/ImageVisualCompare.Core/SampleHistogram.cs`,
> `src/ImageVisualCompare.Core/LevelLookup.cs`,
> `src/ImageVisualCompare.Core/RainbowTable.cs`,
> `src/ImageVisualCompare.Core/ImagePairValidator.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs`,
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
too-large refusal, are `Bitmaps.Create` and `Bitmaps.TooLarge`, shared: the FITS reader asks for its own
wording, which starts `FITS: `, the WIC loader for the general one, both from `UiText` in the
interface's language (`language.md`).

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
   size, planes, `BITPIX`, `BZERO`, `BSCALE`, and the offset of the first sample; `ROWORDER`
   is not read (ADR 0039). The length of the stream is checked here, so a file cut short fails
   at the probe, before the padding question and before any decode.

   The header is input from outside and is not trusted. `Count` reads every keyword the
   layout is computed from: an integer, never negative except `BITPIX`, and a value that is
   neither makes the header invalid instead of falling back to a default. `Number` reads
   `BZERO` and `BSCALE`, and refuses NaN and the infinities in the same way: they parse as
   numbers ("NaN", "Infinity", an overflowing 1D999), and either would make every level black
   or white. The size of a unit's data is multiplied in a `checked` block, the number of axes
   is capped at the standard's 999, and an image's width is capped so that a row of its
   samples fits one array. The first version had none of this, and a negative axis sent the
   walk back to the header it had just read, for ever.
3. `FitsRowDecoder.ReadFileRow` fills one row of 16-bit samples, the planes one after another.
   FITS is plane-sequential (all of red, then all of green, then all of blue), so the row is
   read from one place per plane (`ReadPlaneRow`); the decoder seeks, reads `Width` samples
   into its one row buffer, and `Convert` writes each through `FitsScale.SixteenBit`, in one
   loop per sample type so that nothing is decided per sample. For float files it also counts
   the finite samples above `FitsScale.RescaleThreshold`. The stream has no buffer of its own
   (`FitsImageLoader.OpenRead`): each read is a whole row, and a buffer would be filled past
   the row and thrown away at the next seek. The overload that fills a Bgra32 row is the same
   read followed by the linear renderer, for the tests and for anything that wants levels.
4. `FitsImageLoader.Load` walks the rows in the order of the file, so the disk is read
   forwards three times over (`Pass`), each row through a `DisplayRenderer` of the mode asked
   for (the section "Display modes" below) into a `WriteableBitmap` at
   `FitsImageLayout.DisplayRow`, counted from the bottom, which is the one place the row order
   is applied; the cancellation token is checked at every row. Then it asks
   `FitsScale.IsRescaled` about the count: more
   than one sample in a hundred above 2.0 means the file counts to 65 535, every byte written
   so far is wrong (nearly all white), and the same loop runs again with 65 535 as the full
   scale. Then `Freeze`. A size WPF makes no bitmap of becomes the too-large message here.

`FitsScale.SixteenBit` is the one place a value becomes a sample: the value, already multiplied
to count in 0 to 65 535 (`ToSixteenBits`, one multiplication a sample instead of a division),
is clamped and rounded to a 16-bit sample, the one a 16-bit PNG of the image would hold;
`FitsScale.Level` is its high byte. The last step is WIC's own reduction of a 16-bit sample,
measured, and it is what makes a FITS and its 16-bit export agree to the level;
`FitsScaleTests` walks all 65 536 values on each scale a file can have.

## Display modes

Added on 2026-10-26 (ADR 0040). Between the 16-bit row and the Bgra32 row stands
`DisplayRenderer`, built per load for a `DisplayMode`, a width and a plane count, and used the
same way by both loaders:

- `Linear` renders at once: `RenderLinear` takes the high byte of each sample by a shift
  (through the 64 KB table of `LevelLookup.Linear` the same load took 0.67 s instead of 0.53 s,
  the table being larger than the first-level cache), the blue plane first in the pixel, alpha
  255, a single plane on all three channels. The loaders then do what they did before the
  modes existed: one pass, and a second one on the rescaled file.
- `Histogram` and `FalseColours` need every sample before any pixel (`NeedsCount`). The
  loader makes a counting pass, `Count` on each row, and when `FitsScale.IsRescaled` says the
  file counts to 65 535 it clears the counts and counts again on that scale; then `Prepare`
  builds the lookups and a rendering pass writes the bitmap. Three passes over the file at
  most, two as a rule: 1.1 to 1.2 s against 0.5 s for the linear load of `result_3813s.fit`.
- `SampleHistogram` is Siril's histogram of one layer: 65 536 bins on the 16-bit value, bin 0
  counted and then zeroed (cheaper than a test per sample), its samples still in `Pixels`.
  `LevelLookup.Equalising` is Siril's remap: `index[0] = 0`, `index[i]` the cumulative count
  through bin `i` over the pixels, times 255, rounded as Siril's `round_to_BYTE` rounds
  (`RoundToByte`, halves up, capped). In `Histogram` there is one histogram and one lookup per
  plane, as Siril remaps each layer on its own histogram and composites the RGB view after.
- `FalseColours` has one histogram, of the layer `Layer` makes: the plane of a gray image, or
  the mean of the three 16-bit planes rounded to the nearest, `(r + g + b + 1) / 3`; the
  equalised level then goes through `RainbowTable.WriteBgra`. The table is Siril's
  `make_index_for_rainbow` with its `hsv_to_rgb`, built once: a hue from 300° at level 0 to 0°
  at level 255, saturation and value 1, so level 0 is magenta and not black. `LevelOf` is its
  inverse, for the readout, and is total because the 256 colours are distinct.

`WicImageLoader.Pass` is the same shape over the float bands: `FloatSamples.ToSixteenBit` lays a
row out plane-major as the FITS decoder does, and the renderer counts or renders it. Which
files can take a mode is `ImageInfo.ShownAsStored`, set by `FitsImageLayout.Describe` and by
`WicImageLoader.Describe` for float samples, so `ImagePairLoader` knows from the two probes
whether to ask the mode of the loads or fall back to `Linear`; `LoadedImage.Mode` says what
was applied, and the pair reports it as `ImagePair.Mode`.

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
then copies a band of rows at a time, at most 8 MB of floats (`Pass`), into
`FloatSamples.ToSixteenBit` in Core, which puts each sample through `FitsScale.SixteenBit` as
`FitsRowDecoder` does and counts the samples `FitsScale.IsAboveThreshold` takes, and the
`DisplayRenderer` of the mode asked for turns the row into Bgra32. Then, as for a FITS,
`FitsScale.IsRescaled` decides whether the image counts to 65 535, and if it does the bands
are decoded again on that scale; a mode that counts first counts them again and renders after,
three passes in all. A size WPF makes no bitmap of is the too-large message.

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

## The pair loader, and one image changed

`ImagePairLoader.LoadAsync` is the whole open: the two probes, the canvas (`ImagePairValidator.Inspect`),
the padding settled before anything is decoded (`SettlePaddingAsync`: refused with no one to ask,
otherwise asked, and declined without an error), the mode both decodes are asked for (`ModeFor`:
the one asked when both files are shown as stored, `Linear` otherwise, R-110), then the two
decodes one after the other. `ReplaceAsync` (R-009, ADR 0046) changes one image of a pair already
decoded: it probes the new file alone, puts its header beside the kept image's `Info` on the side
each belongs to, and goes through the same `SettlePaddingAsync` and `ModeFor`, so the question's
wording, the refusal and the fallback are one code path for both; then it decodes the new file,
and the kept image's file again only when the kept `LoadedImage` was rendered in another mode than
the one the pair ends in, which happens when a FITS pair in `Histogram` takes a PNG, or a
`Linear` pair of a FITS and a PNG has the PNG replaced by a FITS while `Histogram` is asked. The
kept `LoadedImage` is otherwise the very object of the pair on screen, bitmap included, which is
what makes the change cost one decode and one buffer; `ImagePairLoaderTests` asserts the object is
the same. Both methods end through `Done`, the done line and the success result, and `Failed`,
the R-005 message beside the exception (`IsExpectedLoadFailure` says which exceptions are the
file's and not the program's).

## In the diagnostic log

The loaders write what a bug report needs about a file (ADR 0043): `ImagePairLoader` the load's
start and done lines with the time of each stage, a line per file probed and decoded, the
padding's question and answer, a fallback to linear, and a failure with its exception beside the
message the user reads; `WicImageLoader` the decoder, the pixel format WPF names, or `unnamed`,
a TIFF's own sample tags and the colour profiles a file carries; and `FitsImageLoader` the layout,
and at `Trace` every header card, which `Probe` reads from the file on a stream of its own, so that
a header the reader refuses is written too. Both write the range a float file was found to count
in, with the counts `FitsScale.IsRescaled` decided on. Nothing is written per row or per band, and
nothing the log does changes what a probe or a decode returns or throws. The lines are listed in
`logging.md`.

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
