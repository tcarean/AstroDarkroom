# Imaging: the decoder, the memory a pair costs, padding

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "No imaging dependency", "Memory,
> measured" and "Invariants" (one of its ten). Where a paragraph became a decision record, its
> number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/FitsImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/RoutingImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs`,
> `src/ImageVisualCompare.Core/FitsHeader.cs`,
> `src/ImageVisualCompare.Core/FitsScale.cs`,
> `src/ImageVisualCompare.Core/FitsRowDecoder.cs`,
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

## FITS, the format WIC does not read

Added on 2026-10-05 (ADR 0029). The path of a FITS file, from the dialog to the bitmap:

1. `RoutingImageLoader` opens the file, and `FitsHeader.IsFits` compares its first nine bytes
   with `SIMPLE  =`. A match goes to `FitsImageLoader`, anything else to `WicImageLoader`. The
   name of the file is never looked at.
2. `FitsHeader.ReadLayout` walks the header-data units: 2880-byte blocks of 80-byte cards up to
   `END`, then the unit's data, padded to a block. It stops at the first unit that can hold an
   image (the primary, or an `IMAGE` extension) and has two axes or more, and steps over the
   others by their size, which for a table needs `PCOUNT` and `GCOUNT`. A binary table has two
   axes and 8 bits like a small image, which is why the extension's type is asked first; one
   with `ZIMAGE = T` is a tile-compressed image and is refused by name. The result is a
   `FitsImageLayout`: size, planes, `BITPIX`, `BZERO`, `BSCALE`, the row order, and the offset
   of the first sample. The length of the stream is checked here, so a file cut short fails at
   the probe, before the padding question and before any decode.
3. `FitsRowDecoder.ReadFileRow` fills one Bgra32 row. FITS is plane-sequential (all of red,
   then all of green, then all of blue), so the row is read from one place per plane; the
   decoder seeks, reads `Width` samples into its one row buffer, and writes each through
   `FitsScale.ToByte` into the channel the plane is. It also keeps the largest finite value it
   has seen.
4. `FitsImageLoader.Load` walks the rows in the order of the file, so the disk is read
   forwards three times over, and writes each into a `WriteableBitmap` at
   `FitsImageLayout.DisplayRow`, which is where the row order is applied. Then it asks
   `FitsScale.IsRescaled` about the largest sample; a yes means every byte written so far is
   wrong (nearly all white), and the same loop runs again with 65 535 as the full scale.
   Then `Freeze`.

`FitsScale.ToByte` is the one place a value becomes a level: divide by the full scale, clamp,
`round(x * 65535)`, keep the high byte. The last step is WIC's own reduction of a 16-bit
sample, measured, and it is what makes a FITS and its 16-bit export agree to the level.

What a FITS costs, on `result_3813s.fit` (5938 x 3888, three float planes, 277 MB on disk):
1.2 to 1.3 s with the file in the cache, +89.7 MB private once loaded, the same as a 16-bit
PNG of that size through WIC, and a transient peak of one more buffer during the load (peak
working set 227 MB against WIC's 139 MB). The managed alternative and the figures of a pair
are in ADR 0029 and in `../validation.md`, V-003 and V-004.

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
