# 0035. A float TIFF is reported and shown as a float FITS is: as stored, not through WIC's conversion

Date: 2026-10-22
Status: accepted 2026-10-22
Supersedes: 0022 in part (the conversion of float samples by WIC)

## Context

ADR 0022 sends every image WIC decodes through `FormatConvertedBitmap` to Bgra32 and lets
nothing read the format but the status bar. ADR 0029 shows a FITS as stored: a value is divided
by the full scale of its type and reduced to a level as WIC reduces a 16-bit PNG, so a FITS and
its 16-bit export show the same levels. The code review of the documentation backfill
(2026-10-21) found that a TIFF of 32-bit float samples, as Siril and GraXpert export, fell
between the two. `WicImageLoader.Classify` named WPF's float formats, but `Describe` never set
`ImageInfo.IsFloat`, and the 96 bpp RGB float those programs write has no name in WPF, so it
was reported `96-bit`; and WIC converts float samples as linear light, so 0.5 showed as 188
where a FITS shows it as 128. Measured on 2026-10-22 on two GraXpert exports, 6016 × 4016: WIC's
level was above the stored one on every sample, by 73 on average, the medians 62 against 135
and 67 against 141. Siril's 16-bit `stacked.tif` and GraXpert's float `stacked_GraXpert.tiff`
made from it, the before and after the owner compares, had the medians 70/72/64 against
142/140/140, a difference that is not in the data (67 as stored). On 2026-10-22 the owner chose
to report and show such a TIFF as a FITS, over reporting it as float only, or leaving it.

## Decision

- A frame's samples are float when WPF names its format `Gray32Float`, `Rgb128Float`,
  `Rgba128Float` or `Prgba128Float`, or, for a TIFF, when its `SampleFormat` tag is 3; the
  channels then come from `SamplesPerPixel`, 1, 3 or 4, and the depth from `BitsPerSample`
  (`WicImageLoader.FloatSamplesOf`). Such a file is reported as a FITS of the same depth and
  channels is (R-090): `32-bit float RGB`, with `IsFloat`.
- WIC converts the samples to a float format, which copies the values, and
  `FloatSamples.ToBgra32` in Core turns them into levels through `FitsScale`, as a FITS's are:
  read as 0 to 1, and once more as 0 to 65535 when `FitsScale.IsRescaled` finds more than 1 in
  100 of them above 2.0, with ` (0..65535)` in the status bar; alpha is 255 (R-091). The rows
  go into a `WriteableBitmap` a band at a time.
- Every other format goes through WIC's conversion as ADR 0022 says, and nothing after the load
  reads the format.

## Alternatives rejected

- **Reporting the float TIFF as float, and keeping WIC's levels.** The status bar would tell the
  truth about the file and the picture would not: a FITS and its float TIFF export, or a 16-bit
  stack and its GraXpert float result, would still differ by about 73 levels that are not in the
  data, in a tool built to see one or two.
- **Leaving it as it was.** The same difference, with `96-bit` in the status bar.

## Consequences

- On real data, measured on 2026-10-22 with this build: `stacked_GraXpert.tiff` reads
  `32-bit float RGB`, with the medians 69/67/67 next to `stacked.tif`'s 70/72/64. WIC's
  conversion to `Rgb128Float` gives the floats stored in the file bit for bit, all 72 480 768
  samples of each of the two exports, read straight from their strips. The load takes 1.4 to
  1.5 s, against 0.8 to 1.4 s through WIC's conversion, the start of the process included.
- A float TIFF in 0 to 65535 is decoded twice, as a float FITS is. Float samples in another
  container whose format WPF does not name, a 96 bpp RGB JPEG XR say, still go through WIC's
  conversion.
- `IsAboveThreshold` moved from `FitsRowDecoder` to `FitsScale`, so the two readers share the
  rule that decides the rescale.
- Guards: `WicImageLoaderTests.R090_AFloatTiff_IsReportedAsFloat` (gray, RGB at 96 bpp, RGBA),
  `R091_AFloatTiff_IsShownAsStored_NotAsWicConvertsIt` and `R091_AFloatTiffInZeroTo65535_IsReadSo`,
  on TIFFs `TempImages.WriteFloatTiff` writes as libtiff does; `FloatSamplesTests` for the
  levels, gray on three channels, and the count.
