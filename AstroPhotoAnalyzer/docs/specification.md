# AstroPhotoAnalyzer - specification

The complete description of what the tool does and how, at the level needed to maintain
it and to add to it. It is the requirements document: **every change to behaviour - a
measurement, a threshold, a rule, an option, a tab, a format - updates this file in the
same change.** `CLAUDE.md` says so; this file is where it is enforced by reading.

Companion documents: `implementation-plan.md` (why the phases were cut the way they were
and what each one taught), `acceptance-de-printat.md` (the run on the reference image),
`THIRD-PARTY-NOTICES.md` (licences), `README.md` (how to use it, in Romanian).

## 1. Purpose and scope

AstroPhotoAnalyzer measures a **finished, stretched** astrophotograph - the file one
would send to a print lab - and reports, in decision order, how to send it: which file
format, whether the background survives paper, what colour cast and edge departures it
has, the largest print size its real detail supports, and how much residual noise the
print version needs. Every recommendation carries its evidence: the number and the
threshold.

What it is not:

- **It exports nothing.** No print file, no preview file. Exports are made in Siril.
- **It does not stretch.** A linear image (median below `LinearMedianMax`) is refused
  with a message. The print-background "simulation" is a gamma curve on stretched data,
  labelled as such.
- **It does not colour-manage.** Pixels are measured as written, assumed sRGB. A
  non-sRGB profile is reported, not applied.
- **It is not a display.** Previews are pixel crops and downsamples, for checking what
  the numbers say; not a zoomable viewer.

Two applications share one core: a console (`AstroPhotoAnalyzer.exe`) and a window
(`AstroPhotoAnalyzer.Wpf.exe`). Neither computes a number or writes a sentence of
diagnosis; both call `AnalysisRun.Execute` and show what it returns.

## 2. Architecture

```
AstroPhotoAnalyzer.Core   net8.0        everything that computes and formats; no entry point
AstroPhotoAnalyzer        net8.0        console: Options -> Parameters -> AnalysisRun -> stdout/-o
AstroPhotoAnalyzer.Wpf    net8.0-win    window: ParameterFields -> Parameters -> AnalysisRun -> tabs
AstroPhotoAnalyzer.Tests  net8.0        core + console tests, golden output, synthetic fixtures
AstroPhotoAnalyzer.Wpf.Tests net8.0-win view-models, culture, window smoke tests
```

Invariants that the structure enforces:

- Core has **no entry point**, so nothing in it can reach `Console` or a window.
- Core types are **internal**; the console and both test projects see them through
  `InternalsVisibleTo`. WPF view-models are **public** (binding needs it) and therefore
  **cannot expose a core type** - every value shown passes through `Rows.cs`.
- One external package, `SixLabors.ImageSharp` 3.1.11, touched in exactly two core files:
  `Input/ImageSharpReader.cs` (decode) and `Analysis/JpegAnalyzer.cs` (encode). See §13.
- Version and copyright live in `Directory.Build.props` only; `VersionInfo` reads them
  from the assembly.

### The chain (`Runs/AnalysisRun.cs`)

```
ImageLoader.Load(path)            -> Raster (+ SourceInfo)
RasterOps.LooksLinear             -> RefusedImageException if median < LinearMedianMax
FileFacts.Of                      -> FileFactsResult
BitDepthAnalyzer.Analyze          -> BitDepthResult
CropAnalyzer.Analyze              -> CropResult          (needs Parameters.TargetAspect)
TonalAnalyzer.Analyze             -> TonalResult         (needs Parameters.Dmax)
PrintStretchSimulator.Simulate    -> SimulationResult    (needs TonalResult)
NoiseAnalyzer.Analyze             -> NoiseResult         (needs Parameters.Seed)
BackgroundAnalyzer.Analyze        -> BackgroundResult
StarAnalyzer.Analyze              -> StarResult
PrintSizeAnalyzer.Analyze         -> PrintSizeResult     (needs CropResult.W/H, StarResult.FwhmMedian)
QuantizationAnalyzer.Analyze      -> QuantizationResult
JpegAnalyzer.Analyze              -> JpegResult
Recommendations.Build             -> IReadOnlyList<Recommendation>   (needs everything)
ReportWriter.WriteBody            -> AnalysisResult.ReportBody
```

Progress stages reported through `IProgress<string>`, in order: `citire`, `fisier`,
`tonuri`, `zgomot`, `fundal`, `stele`, `cuantizare`, `JPEG`, `raport`. No percentage:
JPEG is about half the wall time and the rest is uneven.

`AnalysisResult` holds every result record, the parameters, the path, the raster (kept
for previews) and the report body. `Overall` is the maximum verdict over the
recommendations, `Ok` when there are none.

Timing on the reference image (23 MP, 16-bit RGB PNG): ~11 s in Release, of which
reading ~3 s, JPEG ~5 s.

## 3. Inputs

### Formats and readers (`Input/`)

`ImageLoader` picks a reader **by extension** (never by sniffing) and is the only place
that knows both. An unknown extension is an error naming the accepted ones.

| reader | extensions | what it reads |
|---|---|---|
| `FitsReader` (hand-written, streaming) | `.fit .fits .fts` | first HDU with NAXIS 2 or 3; BITPIX 8/16/32 int (BZERO/BSCALE applied) and -32/-64 float; NAXIS3 = 1 or 3, anything else refused; rows flipped when `ROWORDER = 'BOTTOM-UP'`; empty primary HDUs skipped |
| `ImageSharpReader` | `.tif .tiff .png .jpg .jpeg` | colour decoded as `Rgb48`, mono as `L16`; bit depth and channel count from the format's own metadata (PNG `BitDepth`/`ColorType`, TIFF `BitsPerSample`/`PhotometricInterpretation`, JPEG always 8); compression name; ICC profile from the **decoded** image (`Metadata.IccProfile` or `Frames[0].Metadata.IccProfile`) |

### The raster (`Input/Raster.cs`)

`Raster`: `W`, `H`, `Planes` = 1 or 3 `float[]` in **0..1**, row 0 at the **top**.
Normalisation: 8-bit `/255`, 16-bit `/65535`, 32-bit int `/4294967295`, float as is,
clipped to [0, 1] with the count noted. `Reference` is the green plane on colour, the
only plane on mono: the channel every single-channel measurement uses.

`SourceInfo` (attached by the reader): `Path`, `Format`, `BitDepth`, `IsFloat`,
`PlanesInFile`, `Compression`, `Icc` (bytes), `IccDescription` (from the `desc` tag, v2
text or v4 mluc, parsed by `IccProfileInfo`; null when absent or malformed),
`FileBytes`, `Note` (reader observations: clipped values, flipped rows, palette, multi-page).

Memory: float planes cost `4 x W x H x planes` bytes - 277 MB for 23 MP RGB. Float was
chosen over double (554 MB) because 24 mantissa bits recover every 16-bit level exactly
(`RasterOps.Level16` round-trips all 65536 values; tested).

### Refusal of linear data

`RasterOps.LooksLinear`: median of the reference plane at stride 8 `< LinearMedianMax
(0.02)` -> `RefusedImageException` with the median in the message. A stacked linear
frame sits at 0.001-0.01; a finished image at 0.08-0.25.

### Colour maths (`Input/ColorMath.cs`)

sRGB <-> linear per IEC 61966-2-1; Y = 0.2126 R + 0.7152 G + 0.0722 B on linear values;
L\* per CIE 1976 with the linear segment below Y = 0.008856; **paper black** =
L\*(10^-Dmax): baryta 2.0 -> 9.0, canvas 1.5 -> 20.7, acrylic C-print 2.2 -> 5.7.

### Statistics (`Input/Stats.cs`)

Percentiles use numpy's default linear interpolation between ranks (the acceptance
numbers were made with numpy). `SeededRandom` is xorshift64\*, so tile selection is the
same on every runtime and every year.

## 4. Parameters (`Parameters.cs`)

| parameter | default | console flag | window field | used by |
|---|---|---|---|---|
| `Medium` | `Baryta` | `--medium baryta\|canvas\|acrilic` | combo | Dmax |
| `DmaxOverride` | null | `--dmax n` (0 < n <= 4) | "Dmax explicit" | paper black |
| `Dmax` (derived) | baryta 2.0 / canvas 1.5 / acrylic 2.2 | | | Tonal, Simulation |
| `CandidateWidthsCm` | 50, 60, 70, 80, 90, 100 | `--sizes a,b,c` | "Latimi candidate" | PrintSize |
| `ViewingDistanceM` | null = 1.5 x diagonal per row (then no row is recommended) | `--distance m` | "Distanta de privire" | PrintSize |
| `PassepartoutCm` | 7 | `--passepartout cm` | field | PrintSize (outer size) |
| `FrameCm` | 3 | `--frame cm` | field | PrintSize (outer size) |
| `TargetAspect` | null = native | `--aspect w:h` (also `wxh`) | "Raport de aspect" | Crop, PrintSize |
| `TargetBackground` | 0.19 | `--target-bg 0..1` | "Fundal-tinta" | Simulation (primary row) |
| `SimulationTargets` | 0.15, 0.20 | - | - | Simulation (extra rows) |
| `Seed` | 0 | - | - | Noise tiles |

The console (`Options.cs`) and the window (`ParameterFields.cs`) validate syntax only,
with the invariant culture (a comma decimal is an error that says to use the point).
Neither holds a copy of the defaults: an unset field means "the core's default".

Rules between parameters are the core's: `Parameters.Validate()` returns a message or
null, and both the console (exit 1, before any reading) and the window (the error text)
call it. Currently one rule: **the passepartout must be at least
`PassepartoutMinFrameRatio` (2) x the frame width**; 0 means no passepartout and is not
checked. The window's distance, aspect and framing fields carry help text
(`DistanceHelp`, `AspectHelp`, `PassepartoutHelp`, `FrameHelp`) as tooltips on the field
and its label. Each text is two to four paragraphs separated by a blank line (`\n\n`),
which the tooltip wraps at 420 px with a 17 px line height (`Theme/Dark.xaml`); a test
checks the paragraphs. The distance one says what the size test does with it (and that without
stars it sets the pixel-grid floor), that empty means 1.5 x each size's own diagonal and
then no size is recommended (the wall chooses), and
where 1.5 comes from: the print-and-framing rule of thumb "1-2 x the diagonal", 1 x
being the lens's own perspective (a normal focal length equals the frame diagonal) and
2 x gallery practice; at 1.5 x the print subtends ~37 degrees. The aspect one says that
empty means the native ratio with no crop (what a fine-art lab needs), that a ready-made
frame does not constrain it either (the passepartout absorbs the difference), and that it
is for labs with fixed formats.

## 5. Measurements

Each analyzer is a pure function of a `Raster` (and sometimes `Parameters` or an
earlier result) returning a record. Nothing formats, nothing decides beyond the boolean
flags listed. All thresholds are in `Thresholds.cs` (§7) with a comment saying where
they came from.

### A1 - `FileFacts`

Copies `SourceInfo` and the dimensions into `FileFactsResult`: `W`, `H`, `Megapixels`,
`NativeAspect`, `IsColor`, `Format`, `BitDepth`, `IsFloat`, `Compression`, `HasIcc`,
`IccDescription`, `FileBytes`, `ReaderNote`, and `IccLooksSrgb` (null without a profile;
otherwise whether the description contains "srgb", case-insensitive).

### A2 - `BitDepthAnalyzer` - genuine 16 bits or an 8-bit upscale

Applicable only when the source is 16-bit integer. Over **all** pixels of every plane:
distinct 16-bit levels per plane (`DistinctLevels`), and the fraction of samples with
`v == (v >> 8) * 257` (`UpscaledFraction`) - an 8-bit file saved as 16 has every sample
of that form; genuine data has ~0.4 % by coincidence. `IsUpscaled` when the fraction
exceeds `UpscaledFraction` (0.5).

### A3 - `CropAnalyzer` - what a target aspect cuts

Given `W`, `H` and `TargetAspect`: no crop when null or equal to native (1e-6). If the
target is narrower, the new width is `round(H x target)`, cut equally from both sides;
otherwise the height. Result: the centred rectangle (`X0 Y0 W H`), `Axis` ("latime" /
"inaltime"), `CutPerSidePx`, `CutPct`. The reference image at 50:35: 5938 x 3888 ->
5554 x 3888, 192 px per side, 6.5 %.

### B1/B2 - `TonalAnalyzer` - where the tones sit, and how many fall under paper black

B1 in **levels** (0..255), on all pixels: `MeanLevels` per plane; `PctBelowDarkA` /
`PctBelowDarkB` = share of samples (all planes) below 32/255 and 64/255;
`ReferencePercentiles` at ranks 0.1, 1, 50, 90, 99, 99.9, 100 of the reference plane at
stride `PercentileStride` (2); `BackgroundMedian` = median of the reference plane at
stride `TonalStride` (4) - the "background" the simulation lifts (correct whenever the
sky dominates the frame, i.e. any astrophoto).

B2 in **L\***, on the stride-4 subsample, through `RasterOps.LstarPlane` (Rec. 709
luminance of the linearised channels): `LstarMedian`, `PaperBlackLstar` (from
`Parameters.Dmax`), `PctBelowPaperBlack`, `PctBelowLowSeparation` (< 15),
`PctAboveHighlight` (> 50), `LstarHistogramPct` (101 bins, 0..100, percent).

Flags: `PrintSafe` = below paper black < `PaperBlackOkPct` (2 %);
`BackgroundTooLowForPaper` = > `PaperBlackWarnPct` (20 %).

`LstarStats(raster, dmax, stride)` is public for the simulator.

### B4 - `PrintStretchSimulator` - which target background becomes print-safe

For each target in `SimulationTargets ∪ {TargetBackground}` (ascending): gamma =
`ln(target) / ln(BackgroundMedian)`, applied identically to all channels of the stride-4
subsample (`RasterOps.Gamma`), then B2 recomputed at stride 1. Rows: "as is" first (gamma
1, the tonal numbers), then one per target with `Target`, `Gamma`, `LstarMedian`,
`PctBelowPaperBlack`, `PctBelowLowSeparation`, `PctAboveHighlight`, `PrintSafe`.
`AlreadyPrintSafe` = the as-is row is safe. `RecommendedTarget` = the lowest safe target
above the measured background, null when already safe or none qualifies. `Primary` = the
row of `TargetBackground` (the one the report and the preview use). A background of 0 or
1 yields only the as-is row.

Known limit: the median is not the sky when the sky does not dominate the frame.

### B3 - `NoiseAnalyzer` - residual noise in the sky, in 8-bit LSB

`NoiseTileCandidates` (400) random 64 x 64 positions from `SeededRandom(Parameters.Seed)`
on the reference plane; a tile is smooth when `max - min <= NoiseTileMaxRangeLevels`
(6000/257 = 23.3 levels - the acceptance script's wide criterion). Per smooth tile, sigma
= population std of horizontal neighbour differences / sqrt 2, x 255. Result: `Tiles`
(x, y, sigma), `SmoothTiles`, `SigmaMedianLsb`, `Sigma10thLsb`, `SigmaMinLsb`,
`PctTilesBelowBanding` (< 0.5), `PctTilesBelowOne`.

Flags: `Insufficient` (< `NoiseMinTiles` = 10 smooth tiles; sigmas NaN); `BandingRisk`
(median < 0.5); `GoodForPrint` (0.8..1.5); `UnderDenoised` (> 2.0). Rasters smaller than
a tile have zero candidates.

### E1/E2 - `BackgroundAnalyzer` - sky colour, and edge regions against it

On the stride-3 subsample. Luminance = mean of the planes. **Background** = pixels whose
luminance is `<=` the 25th percentile (computed over the whole subsample), excluding the
bottom 25 % of rows (the horizon). `Levels` = per-plane medians x 255. On colour:
`GreenExcess` = G - (R+B)/2, `RedMinusBlue`; `HasCast` when either exceeds
`CastWarnLevels` (1.0). `<=` and not `<` because a flat synthetic sky puts every pixel
exactly at the percentile.

**Regions**: four corners (15 % of height x 20 % of width) and four edges (the same bands
between the corners). Each `EdgeRegion` has per-plane medians (`Levels`), `Deltas` =
region - background per plane, `MaxAbsDelta`, `Flagged` (> `CornerGradientLevels` = 3),
and `Coloured`: spread (max delta - min delta) > 3 **and** > `CornerColourSpreadRatio`
(0.5) x mean |delta| - the channels move differently. The distinction came from the
reference image: the horizon corner is +9.4 / +1.7 / -9.9 (coloured, light pollution);
the corner the Milky Way enters is +11 / +10 / +16 (neutral, the object). On mono there
is one plane and `Coloured` is always false.

### D1/D2 - `StarAnalyzer` - the real resolution

Candidate = pixel of the reference plane above `mean + StarCandidateAboveMean16/65535`
(3000/65535 = 0.046 above the plane mean), not above `StarMaxPeak16/65535` (saturated),
and the maximum (ties allowed) of its 25 x 25 window (`StarWindow`). Base = median of the
window's 96 border pixels; amplitude = peak - base >= `StarMinAmplitude16/65535`. Width
at half maximum is measured on the horizontal and vertical lines through the peak:
descend from the centre until below `base + amplitude/2`, interpolate the crossing
linearly (the acceptance script's function, epsilon included), keep the star when both
widths are in (`StarFwhmMin` 0.8, `StarFwhmMax` 15) and record their mean.

Result: `Stars` (x, y, fwhm, peak), `FwhmMedian` / `Q1` / `Q3` (NaN below
`StarMinCount` = 20), `Regions` (centre = central 40 % x 40 %, four corners 20 % x 20 %;
median per region when >= `RegionMinStars` = 5), `CornerToCenterRatio` = widest known
corner / centre, `SoftCorners` when > `CornerSoftRatio` (1.4), `EffectiveDetailPx(w)` =
w / FwhmMedian.

Known: the estimator overestimates a sampled Gaussian by ~2.5 % (linear interpolation on
a convex curve); the candidate threshold is `mean + minimum amplitude`, not the script's
`3 x mean`, because at a 0.25 background the latter rejected every star (the print
version of a real image). On a nebulous core stars measure wider (bright, structured
base) - see the acceptance record.

### D3 - `PrintSizeAnalyzer` - the size table

For each candidate width W (cm), with pixel size (Wpx, Hpx) = the crop rectangle:
height = W x Hpx / Wpx; dpi = Wpx / (W / 2.54); pitch = 25.4 / dpi mm; `StarMm` = FWHM x
pitch; `MinDistanceM` = StarMm / `AcuityMmAt1m` (0.29) - the distance from which the
star is a point; `NaturalDistanceM` = `ViewingDistanceFactor` (1.5) x the print's
diagonal; `ViewingDistanceM` = the parameter or the natural distance;
`PointAtViewingDistance` = MinDistance <= ViewingDistance when the distance is explicit,
MinDistance <= NaturalDistance x `NaturalDistanceTolerance` (1.15) when it is not; without
FWHM, dpi >= `FallbackDpi`, which is 25.4 / (0.29 x d) with an explicit distance - the
pixel grid invisible from d - and `FallbackMinDpi` = 180 without one (the lab convention:
0.14 mm pitch, invisible from ~0.5 m); `PixelDistanceM` = pitch / 0.29 on every row;
outer size = image + 2 x (passepartout +
frame); `Oversize` = W > `OversizeCm` (100). **`ReferenceRow`** = the largest row that
passes and is not oversize; if none passes, the smallest - it always exists (given one
candidate). **`CanRecommend`** = the distance is explicit **and** FWHM is known - the
only case where the rows separate on a measurement; only then is the reference row
`Recommended` and `RecommendedRow` non-null. `NaturalRatio` = MinDistance / NaturalDistance, the same on
every row (NaN without FWHM). `EffectiveDetailPx` = Wpx / FWHM.

**The natural distance cannot pick a size.** MinDistance and NaturalDistance both scale
with the width; their ratio is `fwhm x 1000 / (Wpx x 0.29 x 1.5 x sqrt(1 + (H/W)^2))`, a
constant of the image (1.018 on the reference image) - `NaturalRatio`. So without an
explicit distance the test answers "is the image sharp enough for its natural viewing
distance" - the same answer at every size - and **no row is recommended**: a star would
have landed on the largest candidate under the limit, i.e. on `--sizes`, not on the
image. The reference row is what the recommendation text quotes ("up to X cm") and where
the plateau (C1) is judged; the size table replaces the `*` legend with two lines saying
why there is no recommended row, with the ratio. The report asks for a distance if the
print will be looked at from closer. The tolerance absorbs the FWHM estimator's spread;
before it, the reference image failed every row by 2 % and the reference row fell to 50
cm. With an explicit distance the rows genuinely separate and the recommendation rests
on them.

**Without stars nothing about detail is measured.** The dpi floor is a fact about the
file - enough pixels for the grid to vanish from the viewing distance - not about the
image; a soft nebula may support far less. So no row is recommended there either: the
recommendation states the rule (the physical floor from the given distance, else the
180 dpi convention) and how far the file reaches, or that even the smallest width misses
the floor; the legend under the table says why there is no star; without a distance it
asks for one so the floor is physical rather than conventional. The Rezolutie tab
carries a static note with both rules.

`DpiLadder(viewingDistance)` is a reference scale, not a measurement: for 300 / 240 /
180 / 100 / 60 dpi (and the given distance's own floor when there is one) the pitch
25.4 / dpi and the distance from which the pixel grid vanishes, pitch / 0.29, each with
what it means (reading, printer native, the no-stars convention, poster, sofa). The window
shows it under the size table so the dpi column reads against physics rather than against
300; the report does not repeat it.

### C1/C2/C4 - `QuantizationAnalyzer`

**C1 plateau**: up to `PlateauTileCount` (120) tiles from `SeededRandom(PlateauSeed = 1)`
within `PlateauMaxAttempts` (5000), accepted when the reference plane's range < 6 levels
and mean < 70 levels (`PlateauTile*` - the strict criterion of the other acceptance
script). On each, the 8-bit quantised values: changes of value per row, averaged;
plateau = 64 / (1 + changes). `PlateauPx` = mean over tiles (NaN without tiles);
`PlateauMmAt(dpi)` = PlateauPx x 25.4 / dpi; `PlateauVisibleAt(dpi)` when >
`PlateauVisibleMm` (1.0).

**C2 rounding** (when the source has more than 8 bits): |v x 255 - round(v x 255)| on all
planes at stride 2 -> `RoundingMeanAbs`, `RoundingMax`, `RoundingP999` (from a 1000-bin
histogram).

**C4 shadow-lift stress**: the reference plane at stride 2, lifted with x^(1/1.9)
(`ShadowLiftGamma`), quantised to 8 bits, once from the float data and once from data
first rounded to 8 bits. Levels used strictly inside (`ShadowLiftLevelLo` 8,
`ShadowLiftLevelHi` 120) and missing between the first and last used:
`LevelsUsedFrom16` / `MissingFrom16` / `LevelsUsedFrom8` / `MissingFrom8`.
`Keep16BitMaster` when MissingFrom8 > `MissingLevelsWarn` (20).

### C3 - `JpegAnalyzer`

The raster quantised to 8 bits (`Rgb24` / `L8`), encoded in memory at each quality in
`JpegQualities` (100, 95, 90) with ImageSharp's encoder, 4:4:4 (`YCbCrRatio444`;
`Luminance` on mono), decoded back and compared **against the unrounded float reference
x 255** - so the error includes the 8-bit rounding, as the acceptance table did. Per
row: `Bytes`, `MeanAbsError`, `MaxError`, `P99`, `P999` (from a 0.01-level histogram, no
sort), `PctOverThreshold` (error > `JpegErrorCountLevels` = 2) and
`MeanLevelOverThreshold` (where those samples sit), `Acceptable` = MaxError <=
`JpegMaxErrorReject` (10). `Tiff8LzwBytes` = the same 8-bit image as TIFF LZW with the
horizontal predictor (without it ImageSharp gave 38.6 MB where ImageMagick gives 20).

Measured and relied upon: mean error is **not** monotone in quality this close to
lossless (integer YCbCr costs 1-2 levels at q100); max error and the count over 2 levels
are, and only those feed a verdict. The numbers are this encoder's, not libjpeg's.

## 6. Recommendations (`Report/Recommendations.cs`)

`Recommendation(Topic, Verdict, Text, Because)`; the constructor refuses an empty
`Because`. Verdicts are monotone `Ok < Attention < Problem`; a section's verdict is its
maximum, the overall verdict the maximum of all. Topics in decision order: Format,
Background, Colour, Size, Denoise, Warnings.

| topic | condition | verdict | text (gist) |
|---|---|---|---|
| Format | source > 8 bits | Ok | send TIFF 8-bit LZW (size); 16 -> 8 is rounding (mean/max) |
| Format | source 8 bits | Ok | send TIFF 8-bit LZW; nothing to convert |
| Format | q100 row exists | Ok, or Attention when not `Acceptable` or > 5 % samples over 2 | if only JPEG: q100 4:4:4 (size, errors, where they sit; "in the dark sky" when the mean level of affected samples < 64) |
| Format | any q < 100 not `Acceptable` | Attention | never below q100 (the highest failing quality's max error) |
| Format | plateau known and visible at the reference row's dpi | Attention | banding risk: send 16-bit or dither |
| Format | plateau known and not visible | Ok | no visible banding |
| Format | plateau unknown and `BandingRisk` | Attention | banding risk from sigma alone |
| Background | `PrintSafe` | Ok | background print-safe |
| Background | `BackgroundTooLowForPaper` | Problem | too low; re-stretch to `RecommendedTarget` (with its simulated numbers) or "no target qualifies" |
| Background | otherwise | Attention | at the limit; same fix text |
| Colour | mono | Ok | nothing to measure |
| Colour | `HasCast` | Attention | green/magenta/warm/cool cast (whichever metric is larger); neutralise on linear |
| Colour | no cast | Ok | neutral background |
| Colour | each flagged **coloured** region | Attention | warm (R > B + 3) / cool (B > R + 3) / greenish / other; fix on linear or keep as composition |
| Colour | flagged **neutral** regions | Ok (one note) | most likely the object; check Preview |
| Size | stars `Insufficient` | Attention | no real resolution: the pixel-grid rule (physical floor from the given distance, else the 180 dpi convention), how far the file reaches or that the smallest width misses it; a file limit, not a detail one; no size recommended; asks for a distance when none |
| Size | explicit distance, recommended row passes | Ok | recommended size, outer size; FWHM, effective detail, star mm, point distance, the given distance |
| Size | explicit distance, no row passes | Attention | "discs at any listed size from that distance; the smallest minimises it; try smaller widths or a larger distance" |
| Size | natural distance, rows pass | Ok | "no size is recommended without a distance: up to <largest> cm the image is equally sharp at its natural distance, choose by the wall; give a distance if viewed from close" |
| Size | natural distance, rows fail | Attention | "slightly discs at any size; the smallest minimises it; give a distance" |
| Size | `SoftCorners` | Attention | do not enlarge the whole frame; consider a crop |
| Size | crop `Needed` | Attention | what the target cuts |
| Size | any oversize row passes | Ok | above 100 cm glass/weight/transport are the limit |
| Denoise | noise `Insufficient` | Attention | not enough flat sky |
| Denoise | `BandingRisk` | Attention | denoise pressed too hard for print; target 0.8-1.5; sigma after the recommended re-stretch (x the curve's slope at the background) |
| Denoise | `UnderDenoised` | Attention | visible at 100 % on screen, not on paper |
| Denoise | `GoodForPrint` | Ok | exactly where it should be |
| Denoise | otherwise | Ok | acceptable |
| Warnings | `IsUpscaled` | Attention | 8 bits in disguise |
| Warnings | no ICC | Attention | export with sRGB embedded |
| Warnings | ICC not sRGB | Attention | tell the lab; L\* assumed sRGB |
| Warnings | `Keep16BitMaster` | Attention | keep the 16-bit master (levels missing from 8 vs 16) |
| Warnings | reader note | Ok | the note |

The noise-after-restretch figure: slope = gamma x bg^(gamma - 1) at the measured
background, sigma_after = sigma x slope.

## 7. Thresholds (`Thresholds.cs`)

All empirical, most from the reference image. Change one, change its comment.

| name | value | meaning |
|---|---|---|
| `LinearMedianMax` | 0.02 | below: image is probably linear, refused |
| `UpscaledFraction` | 0.5 | above: 16-bit file is an 8-bit upscale |
| `DarkLevelA` / `DarkLevelB` | 32 / 64 | "dark pixel" levels for context |
| `LowSeparationLstar` | 15 | below: paper separates, the eye barely |
| `HighlightLstar` | 50 | above: bright |
| `PaperBlackWarnPct` / `PaperBlackOkPct` | 20 / 2 | % below paper black: problem / safe |
| `TonalStride` / `PercentileStride` / `BackgroundStride` | 4 / 2 / 3 | subsampling steps of the acceptance scripts |
| `NoiseTileSize` / `NoiseTileCandidates` | 64 / 400 | |
| `NoiseTileMaxRangeLevels` | 6000/257 = 23.3 | smooth-tile range for noise |
| `NoiseMinTiles` | 10 | fewer: insufficient |
| `PlateauTileMaxRangeLevels` / `PlateauTileMaxMeanLevels` / `PlateauTileCount` / `PlateauSeed` / `PlateauMaxAttempts` | 6 / 70 / 120 / 1 / 5000 | strict tiles for the plateau |
| `NoiseBandingLsb` / `NoisePrintLoLsb` / `NoisePrintHiLsb` / `NoiseUnderDenoisedLsb` | 0.5 / 0.8 / 1.5 / 2.0 | sigma bands |
| `PlateauVisibleMm` | 1.0 | a plateau longer than this on paper is a contour |
| `JpegMaxErrorReject` / `JpegErrorCountLevels` | 10 / 2 | |
| `ShadowLiftGamma` / `ShadowLiftLevelLo` / `ShadowLiftLevelHi` / `MissingLevelsWarn` | 1.9 / 8 / 120 / 20 | stress test |
| `StarWindow` / `StarMinAmplitude16` / `StarMaxPeak16` / `StarCandidateAboveMean16` | 25 / 3000 / 60000 / 3000 | in 16-bit units |
| `StarFwhmMin` / `StarFwhmMax` | 0.8 / 15 | accepted widths |
| `CornerSoftRatio` / `RegionFraction` / `CenterFraction` / `StarMinCount` / `RegionMinStars` | 1.4 / 0.2 / 0.4 / 20 / 5 | |
| `FallbackMinDpi` | 180 | pixel-grid floor without stars and without a distance: 0.14 mm pitch, invisible from ~0.5 m (lab convention 150-200); with a distance the floor is 25.4 / (0.29 x d) |
| `JpegQualities` | 100, 95, 90 | |
| `BackgroundDarkestFraction` / `BackgroundExcludeBottomFraction` | 0.25 / 0.25 | |
| `CastWarnLevels` / `CornerGradientLevels` / `CornerColourSpreadRatio` | 1.0 / 3.0 / 0.5 | |
| `AcuityMmAt1m` / `ViewingDistanceFactor` / `OversizeCm` / `ReferenceDpi` | 0.29 / 1.5 / 100 / 300 | 1.5 x the diagonal sits between the perspective-correct 1 x and the gallery 2 x (~37 degrees subtended); a presumption about the viewer, not a measurement |
| `NaturalDistanceTolerance` | 1.15 | allowance on the natural distance (the verdict is size-independent there) |
| `PassepartoutMinFrameRatio` | 2 | passepartout >= this x frame width, else the parameters are refused |

## 8. The report (`Report/ReportWriter.cs`, `Report/Fmt.cs`)

`WriteFull` = banner (`AstroPhotoAnalyzer <version>`, copyright) + body; `WriteBody`
alone is what the console prints after its own banner. Body layout:

```
Fisier: <path>
  <format> <bits> <RGB|mono>, W x H (MP), <compression>, <MB>, <ICC>
  Mediu: <name> (Dmax, negrul hartiei L*). Distanta de privire: <m | 1.5 x diagonala>.

VERDICT: <OK|ATENTIE|PROBLEMA>

1. Formatul de trimis  [verdict]
   <-|!|!!> <text>
     fiindca: <because>
... sections 2-6, "(nimic de semnalat)" when empty

Anexa A. Tonuri        means, % below 32/64, percentiles, L* line
Anexa B. Simularea     table: target, gamma, L*, % below black, % below 15, % above 50, print-safe
Anexa C. Export        TIFF size; JPEG table; rounding; stress; plateau
Anexa D. Rezolutie     stars, regions, size table with "*" on the recommended row, or a note on why there is none
Anexa E. Fundal        background levels and cast; the eight regions with deltas, "!" when flagged
Anexa F. Parametri     every parameter as used
```

Every number goes through `Fmt` with `CultureInfo.InvariantCulture` spelled out (`F0..F4`,
`S1` signed, `Pct`, `Mb`, `Int`, `Dim`); NaN prints as `n/a`. This is what makes the
window's saved file byte-identical to the console's output on any Windows culture
(`CultureTests`). Text is Romanian without diacritics. The format is pinned by the golden
tests: change it on purpose, re-approve, read the diff.

## 9. The console (`AstroPhotoAnalyzer/`)

```
AstroPhotoAnalyzer <image> [--medium baryta|canvas|acrilic] [--dmax n] [--distance m]
                   [--sizes cm,cm,...] [--aspect w:h] [--target-bg 0..1]
                   [--passepartout cm] [--frame cm] [-o|--out file] [-h|--help]
```

Flags may come before or after the image. `Options.Parse` never throws; a bad value is
an `Error` printed as `EROARE: ...`. Every run prints the banner and copyright on lines
1-2, including `--help` and failures; `--help` ends with the GPLv3 warranty lines.

Exit codes: **0** no recommendation above Ok, **1** error (bad argument, invalid
parameter combination, missing file, a folder, unreadable format, refused linear image,
cannot write `-o`), **2** at least one Attention, **3** at least one Problem. `-o` writes `WriteFull` and prints
`    -> raport salvat: <file>`.

## 10. The window (`AstroPhotoAnalyzer.Wpf/`)

**Layout** (`MainWindow.xaml`): top bar (banner, image path, "Deschide imagine...",
"Salveaza raportul...", "Despre" = F1); left column `ParameterFields` card with
"Analizeaza"; right: a placeholder when nothing is loaded, an error card when the run
failed, otherwise a `TabControl` with Raport, Tonuri, Export, Rezolutie, Fundal,
Preview; `BusyOverlay` over the right side while running. Starts maximised. A file from
the command line (`App.FileFromArgs`) is opened at once; otherwise the open dialog is
shown.

**`MainViewModel`**: `Path`, `HasPath`, `IsBusy`/`IsIdle`, `CanRun`, `BusyMessage`
(the progress stage), `Error`/`HasError`, `HasResult`, `CanSave`, `DefaultReportPath`
(`Raport.txt` beside the image), `Banner`, `Copyright`; `OpenAsync(path)` sets the path
and runs; `RunAsync()` validates the fields (`ParameterFields.TryBuild`), clears the
tabs, runs `AnalysisRun.Execute` via `Task.Run`, populates the tabs, or sets `Error`
(`Describe` joins inner messages, no stack); `SaveReport(file)` writes `WriteFull`. It
opens no dialogs (the window does), so it is fully testable.

**Tabs** (`ResultTabs.cs`, each a `ResultTabViewModel` with `Populate(AnalysisResult)`,
`Clear()`, `HasResult`):

| tab | shows |
|---|---|
| `ReportTabViewModel` | `VerdictLabel`; `Sections` (number, title, verdict label, `RecommendationRow`s with mark, text, because, verdict label); `Text` = the full report; `ShowText` toggle |
| `TonesTabViewModel` | `Rows` (metric rows), `Histogram` (101 values), `PaperBlack` (marker), `SimulationRows` (mono strings) |
| `ExportTabViewModel` | `JpegRows` (mono strings), `Rows` (TIFF size, rounding, stress, plateau at the reference row's dpi) |
| `ResolutionTabViewModel` | `Rows` (count, FWHM, effective detail, regions, corner/centre), `SizeRows` (mono strings from `ReportWriter.SizeTableRow`/`SizeTableFooter`: "*" on the recommended row, or the note on why there is none); a static note under the table states the star rule and the pixel-grid rule (180 dpi in bold); `DpiRows` (mono strings, the dpi - distance reference scale from `DpiLadder`, window only) |
| `BackgroundTabViewModel` | `Rows` (levels, cast metrics), `RegionRows` (mono strings, "!" when flagged) |
| `PreviewTabViewModel` | `Tiles`: centre 100 % (480 x 320), centre 200 % (nearest-neighbour zoom), four corners 100 % (320 x 220), whole frame (max side 520), the print simulation at the primary target (gamma on a downsample), the crop when needed |

Tables are pre-aligned mono strings, the same the report writes, rendered with
`MonoRowTemplate`; the alternative was a template per table for the same text.

**Presentation**: `Theme/Dark.xaml` (palette, styles, converters, templates
`VerdictChip`, `RecommendationRowTemplate`, `SectionTemplate`, `MetricRowTemplate`,
`MonoRowTemplate`, `PreviewTileTemplate`, `ComponentRowTemplate`); `VerdictPalette`
(colour from the core's label: OK green, ATENTIE amber, PROBLEMA red, anything else
neutral); `RasterBitmaps` (a raster region as `BitmapSource`, stride and integer zoom,
frozen); `BarsCanvas` (a `FrameworkElement` drawing a histogram with a marker line in
`OnRender`); `DarkTitleBar` (DWM attribute 20, falling back to 19); `AppIcon` (largest
frame of the .ico for "Despre").

**Culture**: `App.ForceInvariantNumbers` pins `DefaultThreadCurrentCulture` and
`CurrentCulture` to invariant at startup; `InvariantGlobalization` must **not** be set on
the WPF project (XAML fails to load without culture data).

## 11. Testing

| kind | where | what |
|---|---|---|
| synthetic, known answer | `AstroPhotoAnalyzer.Tests/*AnalyzerTests.cs` | Gaussian stars of known FWHM, injected noise sigma, ramps, casts, corners, upscales; each analyzer recovers the truth within a stated tolerance |
| fixtures | `SyntheticRasters`, `Scenes` (DarkSky with every defect of the reference image, CleanSky, MonoSky), `FitsTestData`, `TiffTestData` (hand-built 16-bit RGB TIFF with ICC), `PngTestData` (hand-built 16-bit PNG with iCCP, own CRC-32), `IccTestData` (v2/v4 profiles), `ImageFiles` (ImageSharp-encoded 8-bit files), `TempWorkspace` | the 16-bit TIFF/PNG fixtures are hand-built because ImageSharp 3.1 cannot encode them |
| golden | `GoldenOutputTests`, `Golden/*.txt` | the console's whole output on the scenes, version banner and temp path scrubbed; `APA_UPDATE_GOLDEN=1` re-approves |
| console | `CliTests`, `OptionsTests` | exit codes, `-o`, parsing, parameter mapping |
| recommendations | `RecommendationsTests` | every flag the reference image raised, on the dark scene |
| plumbing | `LicenseHeaderTests` (every source file starts with the notice; explicit project list), `VersionTests` (version/copyright from props, banner on line 1), `ThirdPartyTests` (csproj <-> `ThirdParty.Components` <-> notices headings) | |
| window | `Wpf.Tests/WindowSmokeTests` (renders the window's *content*, About, every tab with a real result, the error card; fails on any binding error; `APA_DUMP_SCREENS=<dir>` saves PNGs), `CultureTests` (report identical under ro-RO), `ParameterFieldsTests`, `ViewModelTests` | one STA thread with a message loop per process (`StaFixture`) |
| acceptance | `docs/acceptance-de-printat.md` | manual: the tool on the reference image against the hand-made numbers |

```bash
dotnet test AstroPhotoAnalyzer.sln
```

## 12. Build, publish, versioning, licensing

`dotnet build AstroPhotoAnalyzer.sln -c Release` (Windows-only because of WPF; the
console project builds alone anywhere). Publish: console single-file
(`-p:PublishSingleFile=true`, pure managed), window as a folder. Version: `<Version>` in
`Directory.Build.props`, bumped by the user only. Licence GPLv3-or-later; every source
file (C#, XAML, Python) starts with the notice; the console's `--help` and the window's
"Despre" carry the §5(d) warranty text from `RunReport.WarrantyLines`.

## 13. Dependencies

`SixLabors.ImageSharp` **3.1.11**, pinned: decodes TIFF/PNG/JPEG, encodes JPEG and TIFF
for the size and error measurements. Licence reasoning in `THIRD-PARTY-NOTICES.md`;
displayed in "Despre" through `ThirdParty.Components`; bound to the csproj by
`ThirdPartyTests`. Measured behaviours the code depends on: the TIFF encoder writes 8
bits per sample for `Rgb48` even when asked for 48; `Image.Identify` does not read a
PNG's `iCCP` (`Image.Load` does); the TIFF decoder puts the ICC profile on
`Frames[0].Metadata`; the PNG encoder does not write `iCCP`; LZW needs the horizontal
predictor to be competitive. Adding a package: discuss first, then csproj + notices +
`ThirdParty.cs` + README table (see `CLAUDE.md`).

## 14. Known limitations

- The background is the median of the reference channel: right when the sky dominates,
  wrong otherwise.
- Stars on bright nebulosity measure wider (border median overestimates the base); the
  centre region of a Milky Way core is not a sharpness statement.
- The half-max estimator overestimates a sampled Gaussian by ~2.5 %.
- Neutral edge brightening cannot be told from the object; it is reported as
  informative and left to the eye.
- JPEG numbers are ImageSharp's encoder's; another encoder will differ by tenths of a
  level at q100.
- Colour is measured as sRGB; other profiles are only named.
- Palette PNG/TIFF and multi-page TIFF are read through ImageSharp's conversion and noted,
  not measured natively.
- The plateau needs flat sky; a frame full of structure may yield few or no tiles.

## 15. How to extend

**A new measurement**: an analyzer class in `Core/Analysis/` - pure, `Raster` (+
`Parameters`) in, a record out, flags as booleans on the record, thresholds in
`Thresholds.cs` with a comment; call it from `AnalysisRun.Execute` in dependency order
and add a progress stage; add its numbers to an appendix in `ReportWriter`; write
synthetic tests with a known answer; re-approve the goldens and read the diff; show it in
a tab through rows in `Rows.cs`/`ResultTabs.cs` (public view-models, no core types);
**update §5, §7, §8 and §10 here**.

**A new recommendation**: a rule in `Recommendations.cs` under its topic, with a
`Because` that names the metric and the threshold; a test in `RecommendationsTests` on a
scene that triggers it (extend `Scenes` if none does); goldens; **§6 here**.

**A new parameter**: `Parameters.cs` (default + comment), `Options.cs` + `--help` +
`OptionsTests`, `ParametersFrom` in `AnalyzerCli`, `ParameterFields` + `MainWindow.xaml`
+ `ParameterFieldsTests`, appendix F; goldens (help.txt changes); **§4 here**.

**A new print medium**: `PrintMedium` + `DmaxOf` + `MediumFromName`/`NameOf` in
`Parameters.cs`; `Options.Media`; `ParametersTests`; **§4 here**.

**A new input format**: an `IFrameReader` in `Core/Input/` claiming its extensions,
registered in `ImageLoader.Readers`; fill `SourceInfo`; a hand-built fixture if the
encoder at hand cannot produce the real thing; **§3 here**.

**A new tab**: a `ResultTabViewModel` in `ResultTabs.cs`, a `UserControl` in `Views/`
(partial class in `ResultViews.xaml.cs`), a `TabItem` in `MainWindow.xaml`, a `Render`
line in `WindowSmokeTests.EveryTabRenders...`; **§10 here**.

**A new dependency**: `CLAUDE.md` "The dependencies" - discuss first; four places, two
enforced by tests; **§13 here**.

## 16. File map

```
Directory.Build.props                  version, copyright
AstroPhotoAnalyzer.sln
LICENSE, THIRD-PARTY-NOTICES.md, CLAUDE.md, README.md
Icon/make_icon.py, Icon/AstroPhotoAnalyzer.ico
docs/specification.md                  this file
docs/implementation-plan.md            phases, decisions, lessons
docs/acceptance-de-printat.md          the reference run

AstroPhotoAnalyzer.Core/
  Input/Raster.cs                      Raster, SourceInfo
  Input/RasterOps.cs                   subsample, crop, gamma, levels, L* plane, linear guard
  Input/ColorMath.cs                   sRGB, luminance, L*, paper black
  Input/Stats.cs                       percentiles (numpy), SeededRandom
  Input/IFrameReader.cs, FitsReader.cs, ImageSharpReader.cs, IccProfileInfo.cs, ImageLoader.cs
  Analysis/FileFacts.cs, BitDepthAnalyzer.cs, CropAnalyzer.cs, TonalAnalyzer.cs,
           PrintStretchSimulator.cs, NoiseAnalyzer.cs, BackgroundAnalyzer.cs,
           StarAnalyzer.cs, PrintSizeAnalyzer.cs, QuantizationAnalyzer.cs, JpegAnalyzer.cs
  Report/Recommendation.cs             Verdict, Topic, Recommendation, VerdictText
  Report/Recommendations.cs            the rules
  Report/ReportWriter.cs               the text
  Report/Fmt.cs                        invariant number formatting
  Report/RunReport.cs                  banner, warranty
  Runs/AnalysisRun.cs                  the chain, AnalysisResult, RefusedImageException
  Parameters.cs, Thresholds.cs, ThirdParty.cs, Version.cs

AstroPhotoAnalyzer/                    Program.cs, Options.cs, AnalyzerCli.cs

AstroPhotoAnalyzer.Wpf/
  App.xaml(.cs), MainWindow.xaml(.cs), Theme/Dark.xaml
  Interop/DarkTitleBar.cs, Mvvm/ObservableObject.cs, Mvvm/RelayCommand.cs
  Presentation/AppIcon.cs, BarsCanvas.cs, Converters.cs, RasterBitmaps.cs, VerdictPalette.cs
  ViewModels/MainViewModel.cs, ParameterFields.cs, ResultTabs.cs, Rows.cs, AboutViewModel.cs
  Views/ReportView, TonesView, ExportView, ResolutionView, BackgroundView, PreviewView (.xaml),
        ResultViews.xaml.cs, AboutWindow.xaml(.cs), BusyOverlay.xaml(.cs)

AstroPhotoAnalyzer.Tests/              see §11; Golden/*.txt
AstroPhotoAnalyzer.Wpf.Tests/          StaFixture, WindowSmokeTests, CultureTests, ParameterFieldsTests, ViewModelTests
```
