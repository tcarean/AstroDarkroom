# Implementation plan

Status: **phases 0-2 done** (skeleton; input layer; file/tonal/noise/background
analyzers). Phases 3-7 pending. Each phase compiles and passes its tests before the next
starts; the user commits per phase.

## Decisions taken before the first line

| decision | choice | why |
|---|---|---|
| input state | **stretched, final** images only; no linear stretch simulation | the user exports from Siril; the tool judges what will be printed, not what could be |
| image library | **SixLabors.ImageSharp 3.1.11** for TIFF/PNG/JPEG | approved dependency; same licence reasoning as FlatCheck; hand-rolled TIFF+JPEG would be dishonest work |
| structure | Core / console / WPF / Tests / Wpf.Tests, same versioning, licence, dark theme as FlatCheck | one set of conventions across the monorepo |
| FITS reader | **copied** from FlatCheck.Core and trimmed (no CFA, no lens/focal), `Img` becomes a multi-plane `Raster` | tools in the repo are independent; a 3-plane raster is a different type |
| exe name | console `AstroPhotoAnalyzer.exe`, window `AstroPhotoAnalyzer.Wpf.exe` | user's choice |
| pixel storage | `float[]` per plane, values 0..1 | 23 MP x 3 planes = 277 MB; `double` would be 554 |
| exports | **none** | the tool recommends; Siril produces |

## What it measures, and what each measurement recommends

Inputs: TIFF / PNG (8/16-bit, RGB or mono, ICC read from metadata) and FITS (float32,
int16/32, 1 or 3 planes). A file whose median is far below a stretched image's is refused
as "probably linear" rather than mis-measured.

Parameters (defaults from the originating conversation): print medium -> Dmax (Gloss
Baryta 2.0 default, matte canvas 1.5, acrylic C-print 2.2, or explicit), candidate print
widths (50,60,70,80,90,100 cm), viewing distance (default 1.5 x diagonal), passepartout
7 cm, frame 3 cm, target aspect (default native), simulation targets (0.15, 0.19, 0.20).

### A. File and format

| # | measurement | recommendation |
|---|---|---|
| A1 | dimensions, planes, bit depth, compression, ICC profile name, file size | no ICC -> "add sRGB"; non-sRGB -> note for the lab |
| A2 | genuine 16-bit or 8-bit upscaled: distinct levels per channel, fraction of `v == hi*257` | > 50 % -> "8-bit in disguise" |
| A3 | aspect vs target | pixels and % cut per side; centred crop rectangle |

### B. Tonal distribution (display-referred)

| # | measurement | recommendation |
|---|---|---|
| B1 | per-channel mean / median / percentiles; % below level 32 and 64 | context line |
| B2 | L\* median; % below L\*(paper black); % below L\*15; % above L\*50. Paper black = L\* of reflectance 10^-Dmax | > 20 % below paper black -> "background too low for print; target X" (X from B4) |
| B3 | residual noise sigma in smooth 64x64 tiles (range < 6 levels, mean < 70), sigma = std(horizontal diff)/sqrt 2, in 8-bit LSB; % tiles below 0.5 / 1.0 | < 0.5 -> banding risk; 0.8-1.5 -> good for print; > 2 -> under-denoised for 100 % viewing. Also the denoise target for the print version |
| B4 | print-background simulation: gamma = ln(target)/ln(measured background), rerun B2 at 0.15 / 0.19 / 0.20 / custom | the lowest target with < 2 % below paper black -> "re-stretch to X from linear" |

### C. Export and bit depth

| # | measurement | recommendation |
|---|---|---|
| C1 | plateau: mean run length of identical 8-bit values in smooth tiles, converted to mm at the print's dpi | > 1 mm -> visible banding |
| C2 | 16 -> 8-bit error: RMSE, max, p99.9 | "conversion is pure rounding" |
| C3 | JPEG table q100 / q95 / q90, 4:4:4: size, RMSE, max, p99, p99.9, % pixels with error > 2, mean level of those pixels | TIFF-8 vs JPEG-q100 with numbers; never below q100 if max error > 10 |
| C4 | shadow-lift stress: levels used / missing in [8,120] after a lift from 8-bit vs from 16-bit | missing > 20 -> "keep the 16-bit master" |

### D. Real resolution and print size

| # | measurement | recommendation |
|---|---|---|
| D1 | star FWHM (median, Q1, Q3): isolated local maxima in 25x25, amplitude > 3000/65535 and unsaturated, half-max width with sub-pixel linear interpolation, mean of X and Y | "effective detail ~ W / FWHM px" |
| D2 | FWHM per region: centre + four corners (20 % of each side) | corner / centre > 1.4 -> "soft corners; consider a crop before enlarging" |
| D3 | size table: for each candidate width -> dpi, pixel pitch, star size on paper (FWHM x pitch), minimum distance at which it is a point (acuity 0.29 mm at 1 m), outer frame size | the recommended row for the given distance; note above 100 cm (glass, transport) |

### E. Background colour and gradient

| # | measurement | recommendation |
|---|---|---|
| E1 | background balance: R/G/B medians of the darkest 25 % excluding the bottom 25 % band; `G - (R+B)/2`, `R - B` | \|delta\| > 1 level -> "cast; neutralise before print, it shows once the background is lifted" |
| E2 | corners and edges: R/G/B medians of 4 corners + 4 edges vs background | a corner > 3 levels above -> "residual gradient / horizon glow; GraXpert on linear, or a composition decision" |

### F. Previews (on screen, from memory)

100 % / 200 % crops (centre, corners, sky); side-by-side screen vs simulation; the crop at
the target aspect. Nothing is written to disk except `Raport.txt`.

### The report

Decision order, not metric order: 1 format to send, 2 is the background print-safe (if
not, the target), 3 colour / gradient to fix first, 4 size (recommended + table), 5
denoise (sigma now and the target), 6 warnings (8-bit in disguise, no ICC, soft corners,
crop needed). Then the appendices with the tables. Every recommendation carries its
`Because`.

## Phases

| # | deliverable | status |
|---|---|---|
| 0 | skeleton: sln, props, LICENSE, notices, CLAUDE.md, README, icon, Core plumbing (Version, ThirdParty, RunReport), console (Options, Cli), plumbing tests | **done** |
| 1 | Core `Input/`: `Raster`, `RasterOps`, `ColorMath`, `Stats`, `FitsReader` (copied, trimmed, streaming), `ImageSharpReader`, `IccProfileInfo`, `ImageLoader`; `Parameters`, `Thresholds`; synthetic rasters, hand-built FITS/TIFF/PNG and ICC fixtures for tests | **done** |
| 2 | Core `Analysis/`: `FileFacts`, `BitDepthAnalyzer`, `TonalAnalyzer`, `NoiseAnalyzer`, `PrintStretchSimulator`, `BackgroundAnalyzer`, `CropAnalyzer` + tests with known answers | **done** |
| 3 | Core `Analysis/`: `StarAnalyzer`, `PrintSizeAnalyzer`, `QuantizationAnalyzer`, `JpegAnalyzer` + tests | |
| 4 | Core `Report/`: `Recommendations`, `ReportWriter`; `Runs/AnalysisRun`; console wired; golden tests | |
| 5 | WPF skeleton: theme, title bar, MVVM, About, parameters panel, Raport tab; `WindowSmokeTests`, `CultureTests` | |
| 6 | WPF tabs: Tonuri, Export, Rezolutie, Fundal, Preview | |
| 7 | acceptance on `de_printat.png` (numbers below), `docs/acceptance-de-printat.md`, README | |

## Notes from phase 2

- **Two different tile criteria, on purpose.** The noise sigma (B3) uses tiles with a
  range up to 6000/65535 (~23 levels), seed 0, 400 candidates; the plateau (C1, phase 3)
  uses range < 6 levels, mean < 70, seed 1, 120 tiles. Those are the two scripts the
  acceptance numbers came from. `Thresholds` keeps them apart.
- **"Background" for the simulation is the median of the reference channel.** Right
  whenever the sky dominates the frame - every astrophoto - and wrong on a frame where
  it does not (`WhenTheSkyDoesNotDominateTheMedianIsNotTheSky` keeps that visible).
- **Background mask uses `<=` the percentile, not `<`.** On a flat synthetic sky every
  pixel sits exactly at the percentile and a strict `<` selects nothing. Real data has no
  ties, so the acceptance numbers are unaffected.
- **Corner regions are compared per channel, not by luminance.** The reference image's
  horizon corner has the sky's luminance (R +9, B -10); a luminance test would miss it.

## Things measured about ImageSharp 3.1.11 while building phase 1

Written down because each one cost a probe to find, and each one shapes code or tests:

- **The TIFF encoder writes 8 bits per sample for `Rgb48`, even with
  `BitsPerPixel = Bit48` requested.** 16-bit grey (`L16`) does encode. So a 16-bit RGB
  TIFF - the file Siril writes - cannot be produced through ImageSharp; the reader tests
  use a hand-built uncompressed TIFF (`TiffTestData`) instead, which is also the more
  honest test: decode of a foreign file, not encoder-decoder symmetry.
- **`Image.Identify` does not read a PNG's `iCCP` chunk** (it stops at the header) and
  returns no frame metadata for PNG at all. `Image.Load` does populate
  `Metadata.IccProfile`. The reader therefore takes the profile from the decoded image.
- **The TIFF decoder puts the ICC profile on the first frame's metadata**
  (`Frames[0].Metadata.IccProfile`), not on `Image.Metadata`. The reader checks both.
- The PNG encoder does not write `iCCP` from `Metadata.IccProfile`; the PNG-with-profile
  test uses a hand-built PNG (`PngTestData`, with its own CRC-32 and `ZLibStream`).

## Acceptance numbers

Measured in the originating conversation on `de_printat.png` (5938 x 3888, 16-bit RGB
PNG, sRGB) with Python/NumPy. The C# port must reproduce them within rounding:

| measurement | value |
|---|---|
| genuine 16-bit | 63862 / 39918 / 42878 distinct levels; 0.42 % `v == hi*257` |
| mean (8-bit scale) | 32.2 / 29.6 / 27.9 |
| % pixels below 32 / 64 | 73.8 % / 96.3 % |
| noise sigma (smooth tiles) | median 0.48 LSB, 10th pct 0.36, min 0.32; 61 % of tiles < 0.5 |
| plateau (8-bit, smooth tiles) | 2.6 px |
| L\* median; % < L\*9; % < L\*15; % > L\*50 | 9.4; 44.4 %; 81.6 %; 0.4 % (on the 50x35 crop) |
| after gamma 0.721 (bg 0.10 -> 0.19) | L\* 20.4; 0.5 %; 6.7 % |
| JPEG q100 / q95 / q90 (50x35 crop) | 13.4 / 3.2 / 1.7 MB; RMSE 0.634 / 0.727 / 0.818; max 4.5 / 26.0 / 56.2; q100: 3.03 % pixels err > 2 at mean level 31 |
| shadow-lift stress (gamma 1.9) | from 16-bit 111 levels, 0 missing; from 8-bit 60 levels, 46 missing |
| star FWHM | median 3.00 px, Q1 2.69, Q3 3.61 (193 stars) |
| background R/G/B | 19.2 / 20.6 / 18.9; G - (R+B)/2 = +1.58; R - B = +0.31 |
| bottom-right corner R/G/B | 28.6 / 22.3 / 9.0 |
