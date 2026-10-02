# Architecture and the chain

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §2, which no longer
> exists. The behaviour is stated with IDs in `../requirements.md`; the structure is ADR 0007,
> the raster ADR 0010. **Covers:** `AstroPhotoAnalyzer.Core/Runs/AnalysisRun.cs` and the
> solution `AstroPhotoAnalyzer.sln`. A change to the code this describes means this note is
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
