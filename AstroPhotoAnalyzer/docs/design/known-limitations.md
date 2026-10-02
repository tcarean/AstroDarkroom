# Known limitations

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §14. A limit here is
> known and accepted; read it before "fixing" one. **Covers:**
> `AstroPhotoAnalyzer.Core/Analysis/TonalAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/StarAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/BackgroundAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/JpegAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/QuantizationAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/PrintSizeAnalyzer.cs` and
> `AstroPhotoAnalyzer.Core/Input/ImageSharpReader.cs`. A change to the code this describes
> means this note is updated in the same task, or deleted with the decision it explains.

## Known limitations

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
- The Preview tab's step-back rule presumes a monitor pixel of `ScreenPixelMm` (0.25
  mm); on a HiDPI screen (0.16 mm on a 27" 4K) the 100 % view is harsher still and the
  factor understates the step.
- Palette PNG/TIFF and multi-page TIFF are read through ImageSharp's conversion and noted,
  not measured natively.
- The plateau needs flat sky; a frame full of structure may yield few or no tiles.
