# 0008. WIC decodes through IImageLoader, one seam typed on BitmapSource in the Wpf assembly; no imaging NuGet package

Date: 2026-10-02
Status: accepted 2026-10-02; the application referencing no NuGet package superseded by 0043

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` (the opening lines, ".NET 9, Windows, x64,
zero NuGet dependencies outside the test project"; "No imaging dependency"; "Structure",
`Imaging/IImageLoader.cs` and `Imaging/WicImageLoader.cs`), the summaries of
`src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs` and
`src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs`, and the second paragraph of `README.md`;
the decision itself dates from a6e5f10 (the base version, 2026-09-08), and the comparison behind
it was written up in 591f433 (the documentation, 2026-09-08). The tool opens PNG and TIFF files
of any bit depth; a 16-bit 5794 x 3637 export is about 108 MB on disk and becomes an 84 MB
Bgra32 bitmap for WPF's `Image` control. SkiaSharp, ImageSharp and Magick.NET were compared with
the Windows Imaging Component (WIC), the codec WPF itself decodes with. `CLAUDE.md` gives WIC
four advantages: the most complete PNG implementation on Windows, nothing to pay at build time,
the only path to the `Image` control without an intermediate copy, and access to the file's true
source format, which the status bar reports.

## Decision

- `WicImageLoader` decodes, through WPF's own imaging classes: `Load` asks WIC for the file as it
  was written, converts it to `PixelFormats.Bgra32` with a `FormatConvertedBitmap`, realises the
  pixels with a `CachedBitmap` (`BitmapCacheOption.OnLoad`) while the stream is still open, and
  freezes the result, so the UI thread can use a bitmap decoded on a worker.
- `IImageLoader` is the one seam between the application and the decoder: `Probe`, which reads
  the header only, and `LoadAsync`, which returns a `LoadedImage` (an `ImageInfo` and a
  `BitmapSource`). It is typed on `BitmapSource` because any replacement decoder still has to
  produce one for the `Image` control, and for the same reason it is declared in
  `ImageVisualCompare.Wpf`, not in Core. Replacing the decoder is replacing one class.
- `ImageVisualCompare.Core` and `ImageVisualCompare.Wpf` reference no NuGet package; the four
  packages of the test project are the only ones.

## Alternatives rejected

- **SkiaSharp, ImageSharp or Magick.NET.** Compared and rejected: none reaches the `Image`
  control without an intermediate copy, which at 84 MB an image is the copy that matters; each
  costs a package at build time, where WIC costs nothing; and WIC's PNG implementation is the
  most complete on Windows.
- **A generic seam, or the seam in Core.** Whatever decodes still has to end in a `BitmapSource`
  for the `Image` control, so a type parameter would buy nothing real, and an interface that
  names a WPF type cannot live in Core (ADR 0009).

## Consequences

- WIC streams the Rgb48 to Bgra32 conversion instead of materialising an intermediate, and its
  pixels are unmanaged; what follows for the memory a pair costs, and for giving it back, is in
  ADR 0015 and ADR 0016. What the status bar says about the source format is ADR 0022.
- A new decoder is one class behind `IImageLoader`, and `ImagePairLoader` does not change. A
  package for it has to answer the comparison above first (`docs/design/imaging.md`), and the
  kit's rule "Packages" asks the owner in any case.
- Guards: `WicImageLoaderTests` (the real codecs, on fixtures generated at run time) and
  `ImagePairLoaderTests` (the real loader, and a recording fake behind `IImageLoader`). No test
  checks that Core and Wpf reference no package: `Directory.Packages.props` lists only the four
  test packages, and the `packages.lock.json` of the two projects list none (ADR 0003).

Source: CLAUDE.md (opening lines, "Structure", "No imaging dependency", "Memory, measured");
src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs;
src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs; README.md (second paragraph);
Directory.Packages.props; src/ImageVisualCompare.Core/packages.lock.json;
src/ImageVisualCompare.Wpf/packages.lock.json.
