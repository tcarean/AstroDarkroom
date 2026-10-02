# Imaging: the decoder, the memory a pair costs, padding

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "No imaging dependency", "Memory,
> measured" and "Invariants" (one of its ten). Where a paragraph became a decision record, its
> number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs`,
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
