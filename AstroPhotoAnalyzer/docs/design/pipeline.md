# Architecture and the chain

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §2, which no longer
> exists. The behaviour is stated with IDs in `../requirements.md`; the structure is ADR 0007,
> the raster ADR 0010. **Covers:** `AstroPhotoAnalyzer.Core/Runs/AnalysisRun.cs`,
> `AstroPhotoAnalyzer.Core/Runs/ProofRun.cs`,
> `AstroPhotoAnalyzer.Core/Runs/CurveRun.cs` and the solution `AstroPhotoAnalyzer.sln`. A change to the code this describes means this note is
> updated in the same task, or deleted with the decision it explains.

## Architecture

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
  `Input/ImageSharpReader.cs` (decode) and `Analysis/JpegAnalyzer.cs` (encode). See `dependencies.md`.
- NLog, the diagnostic log, is named in `Logging/`, `Runs/` and four files of `Input/` only; no
  analyzer and no report writer holds a logger (`logging.md`).
- Version and copyright live in `Directory.Build.props` only; `VersionInfo` reads them
  from the assembly.

### The chain (`Runs/AnalysisRun.cs`)

```
ImageLoader.Load(path)            -> Raster (+ SourceInfo, with the FITS's WCS)
AnalysisRun.BorrowWcs             -> SourceInfo.Wcs replaced by another FITS's (Parameters.WcsPath, R-027)
RasterOps.LooksLinear             -> RefusedImageException if median < LinearMedianMax
FileFacts.Of                      -> FileFactsResult
BitDepthAnalyzer.Analyze          -> BitDepthResult
CropAnalyzer.Analyze              -> CropResult          (needs Parameters.TargetAspect)
TonalAnalyzer.Analyze             -> TonalResult         (needs Parameters.Dmax)
PrintStretchSimulator.Simulate    -> SimulationResult    (needs TonalResult, SourceInfo.Wcs; builds the DustMap, runs DustAnalyzer per lift)
NoiseAnalyzer.Analyze             -> NoiseResult         (needs Parameters.Seed)
BackgroundAnalyzer.Analyze        -> BackgroundResult    (needs SimulationResult.Map: the sky patches give the colour)
StarAnalyzer.Analyze              -> StarResult
PrintSizeAnalyzer.Analyze         -> PrintSizeResult     (needs CropResult.W/H, StarResult.FwhmMedian)
QuantizationAnalyzer.Analyze      -> QuantizationResult
JpegAnalyzer.Analyze              -> JpegResult
Recommendations.Build             -> IReadOnlyList<Recommendation>   (needs everything)
ReportWriter.WriteBody            -> AnalysisResult.ReportBody
```

Progress stages reported through `IProgress<string>`, in order, in the interface language: `reading`, `file`,
`tones`, `dust`, `noise`, `background`, `stars`, `quantization`, `JPEG`, `report` (in Romanian `citire`, `fisier`,
`tonuri`, `praf`, `zgomot`, `fundal`, `stele`, `cuantizare`, `JPEG`, `raport`). No percentage:
JPEG is about half the wall time and the rest is uneven.

Both overloads of `Execute` run the chain through one private body, `Run`, under a `RunClock`
started before the load: each stage is timed in the diagnostic log and each result written at
Debug as soon as it is measured, and the log's stage names are its own (`load`, `wcs`, `file`,
`tones`, `simulation`, ...), never the progress's (`logging.md`).

`AnalysisResult` holds every result record, the parameters, the path, the raster (kept
for previews) and the report body. `Overall` is the maximum verdict over the
recommendations, `Ok` when there are none.

Timing on the reference image (23 MP, 16-bit RGB PNG): ~11 s in Release, of which
reading ~3 s, JPEG ~5 s.

### The second run (`Runs/CurveRun.cs`, ADR 0032)

`CurveRun.Execute(AnalysisResult, CurvePurpose, maxWidth, maxHeight)` is not a stage of the
chain: the window runs it after the analysis, and again on `Calculeaza` in the Editare Curbe tab (ADR 0051), on the result the chain
already produced (the raster, the patch map of the simulation, the noise, the parameters), and
the console never calls it. Order: the tonal subsample; the class names of the map's path
(`CurveSet.Method`, `Labels`) and the five class levels as they are
(`CurveAdvisor.ClassLevels`); the anchors; the preview of the original at the integer stride
that fits the size asked; then, unless a class is empty, the peaks are at or over the target
or the anchors do not increase (`CurveSet.Problem`), the curves (`CurveAdvisor.Curves`), the gamut table when the
purpose is print and the Dmax came from a profile (`GamutAnalyzer.Build`, once), and for each
curve its evidence on the curved subsample and its preview. The purpose changes the evidence
only: the paper checks and the gamut figure are added for print, and `CurveRun.ForScreen`
derives the screen set from a print set by dropping them, which is how the window changes the
purpose without computing anything. `CurveExport.Save` writes the curve's image under
`CurveExport.Guarded`, the guards every export of the tool shares (the input gone, its own path
refused, the input held open without sharing while the write lasts): it hands the raster, the
curve and the `HISTORY` text to `FitsWriter` (`input.md`); the paper fit's export is the other
caller (`paper-fit.md`). The run is timed by a `RunClock`
(`anchors`, `gamut`, `suggestions`); a gamut table that cannot be built is logged with
`GamutTable.Cause`, the exception behind its `Problem`.

### The third run (`Runs/ProofRun.cs`, ADR 0066)

`ProofRun.Prepare(AnalysisResult)` and `ProofRun.Render(set, intent, simulate)` are the soft
proof's run, started by the window beside the curves' once the tabs are filled and again on a
change of intent or of the simulation; the console never calls it. Prepare opens the profile the
Dmax came from, reduces the image to `ProofMaxSide` and marks the pixels outside the paper's
gamut once (the rule of R-144); Render proofs the reduction through the intent's table, built on
its first use and kept on the profile. The chain itself is in `soft-proof.md`. Prepare is timed
by a `RunClock` (`profile`, `gamut`, `mask`), Render writes one Debug line per render.

### The fourth run (`Runs/GamutFitRun.cs`, ADR 0082)

`GamutFitRun.Execute(AnalysisResult, maxWidth, maxHeight)` is the paper fit's run, started by the
window beside the other two once the tabs are filled and again on `Calculeaza`; the console
never calls it. It builds the gamut table of the profile the Dmax came from, the fit's table of
the largest chroma over L\* and hue from it, then the three variants on the gallery's reduction
(their previews and masks) and on the tonal subsample (their evidence), and the recipe. The save
of a variant, `GamutFitExport.Save`, fits the whole image and writes a FITS or a 16-bit TIFF
under `CurveExport.Guarded`. The chain itself is in `paper-fit.md`. The run is timed by a
`RunClock` (`profile`, `table`, `original`, each variant, `recipe`).

Timing (Release, 2026-10-05): the two validation cases V-008 and V-009, each an analysis of a
23 MP image, a curve run and a save of the full image, take 85 s together.
