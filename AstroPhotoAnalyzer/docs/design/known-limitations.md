# Known limitations

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §14. A limit here is
> known and accepted; read it before "fixing" one. **Covers:**
> `AstroPhotoAnalyzer.Core/Analysis/TonalAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/DustAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Analysis/PrintStretchSimulator.cs`,
> `AstroPhotoAnalyzer.Core/Input/Wcs.cs`,
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
- The simulation is a gamma curve from the measured background. A real stretch to the same
  background keeps the shadows lower: the owner's VeraLux stretch to 0.17 measured the dust at
  L\* 7.6, under paper black, where the curve from 0.103 predicted it above from 0.14 (ADR
  0025). The exact target is the curve's, a lower bound on what Siril will need.
- A catalogued cloud is a disc of its catalogue area around its centre; an irregular cloud's row
  mixes its patches with the sky around it, and the verdict moves with the disc's size (x0.7 /
  x1.4 gave 0.197 / 0.177 against 0.187 on the plate-solved FITS), which is the catalogue's.
- Without a WCS, a foreground or a silhouette larger than 1 % of the frame becomes "the dust";
  a plate-solved FITS, or `--wcs` with the stack's header, is the way out.
- The SIP terms of a WCS are ignored: at most 6 px at the edge of the plate-solved FITS, under a
  patch; a field with far stronger distortion would misplace the outer patches.
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
