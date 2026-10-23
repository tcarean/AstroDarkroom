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
- Paper black is a clip: every pixel under L\*(10^-Dmax) is taken to print as the same black.
  That is a relative colorimetric conversion without black point compensation. A lab that
  converts with BPC (the owner's does, ADR 0028) compresses the shadows onto paper black
  instead: nothing prints as pure black, the separation in the shadows shrinks. The tool does
  not model BPC; "the dust prints pure black" is then the pessimistic reading.
- The visibility threshold, L\* 15, is a rule of thumb with no measurement behind it, and one
  number for every room: a print under gallery light shows tones well under it, one in a dim
  hallway loses tones above it. The tool does not know the viewing light (ADR 0029).
- The profile window lists the installed profiles on the UI thread, with no busy sign: tens of
  milliseconds for a normal colour folder, but a folder of hundreds of printer profiles, or one
  damaged profile the package is slow on (4 s measured on one corrupted file), freezes the
  window that long.
- A Dmax read from a profile replaces the medium's Dmax and leaves its name: a canvas profile
  used with `baryta` chosen prints `Mediu: baryta (Dmax 1.49, ...)`. Anexa F names the profile.
- The simulation is a gamma curve from the measured background. A real stretch to the same
  background keeps the shadows lower: the owner's VeraLux stretch to 0.17 measured the dust at
  L\* 7.6, under paper black, where the curve from 0.103 predicted it above from 0.14 (ADR
  0025). The exact target is the curve's, a lower bound on what Siril will need.
- A catalogued cloud is a disc of its catalogue area around its centre; an irregular cloud's row
  mixes its patches with the sky around it. The disc no longer sets the verdict (ADR 0026).
- The classes (band, sky model, dust under the band's local level) are for wide fields with the
  galactic band and plain sky both in the frame. The winter band is a few L\* above the sky and
  its dark clouds lie outside |b| < 10°; a deep-sky field is wholly in or wholly out of the band,
  or, inside it, too narrow to show it as a strip (under 20° of galactic latitude, ADR 0037).
  Those frames take the classes relative to the modelled sky (ADR 0033), named in the report. A
  Milky Way landscape at a long focal length (85 mm spans about 16° held horizontal, 28° on the
  diagonal) falls on either side of that gate by its orientation. A horizon corner darker than
  the modelled sky counts as dust on either path.
- On the sky path the Great Rift is not found: without geometry the band is the sky's level and
  only its darkest knots fall under the model, so on `de_printat.png` the dust is the darkest sky
  (L\* 4.2), not the Rift's 25 % percentile (7.9 with the band). A plate-solved FITS, or `--wcs`
  with the stack's header, is the way to the band classes.
- On the sky path a 32 px patch holding a bright star reads as nebula or bright nebula, so a
  frame with stars has speckles on the map; the anchors move by under 1 L\* (Rosetta with and
  without stars: 32.0 against 32.2). A 3 x 3 median on the patches was measured and rejected
  (ADR 0033).
- An object that fills the frame becomes the modelled sky: on a 1.3 degree field on LDN 42 the
  cloud is the sky class and the plain sky around it the bright nebula, and the verdict's "sky"
  is the cloud's own level. The report still says what it measured.
- The dark globules inside a nebula are not dust on the sky path: they are under the nebula,
  not under the modelled sky; that would need the band path's local level over the whole frame.
- Without a WCS, a foreground or a silhouette darker than the modelled sky by the sky's noise and
  larger than 5 % of the frame becomes "the dust".
- On the sky path a posterised or synthetic sky, where more than half the patches share one
  value, has a sigma of 0: the thresholds collapse to the model and a patch one level off it
  is nebula or dust. Anexa B then shows a threshold of 0.0 L\*.
- The SIP terms of a WCS are ignored: at most 6 px at the edge of the plate-solved FITS, under a
  patch; a field with far stronger distortion would misplace the outer patches.
- Stars on bright nebulosity measure wider (border median overestimates the base); the
  centre region of a Milky Way core is not a sharpness statement.
- The half-max estimator overestimates a sampled Gaussian by ~2.5 %.
- On an 8-bit file the background's levels (the per-channel medians of the darkest quarter, R-060)
  are whole levels, so a cast near `CastWarnLevels` (1 level) depends on the tie rule of the
  darkest-quarter cut: `<=` keeps the pixels that sit exactly at the 25th percentile and `<`
  drops them, +2.0 against +1.0 levels of green on the reference image's 8-bit version, either
  side of the threshold. The code keeps `<=`, as ADR 0014 decided (its correction of 2026-10-23
  records the ties); on the 16-bit version no printed figure moves.
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
- The suggested curves (ADR 0032) are one recipe: the segment slopes come from one curve the
  owner liked on one image, and no point ever goes under its input, so a frame whose peaks are
  already at or over 0.92 of the scale gets no suggestion, only the reason (ADR 0033); the
  shoulder that would make room by compressing the highlights is not built. A frame whose five
  anchors do not increase, or with an empty class, gets none either; a dust at black is not a
  refusal, its point is the origin.
- A saved FITS holds the raster's values: what the reader clipped to 0..1 (and a NaN, to 0) is
  saved clipped. The ICC profile of a PNG or TIFF source is not carried, and a borrowed WCS
  (`--wcs`) is not written. SIP cards are copied with the rest and still ignored by the classes.
- The gamut figure counts the chroma only, through relative colorimetric tables without black
  point compensation: not a soft proof. Near the threshold it is decided on the pixel's 8-bit
  colour (ADR 0042), so on a 16-bit or float image it is within 0.2 points of the exact per-pixel
  figure, not equal to it (measured 2026-10-23: the canvas sky 38.24 % against 38.08 %). `Maxima`
  clips more pixels than the other suggestions (0.18 % against 0.10 % on the reference PNG).
- A saved FITS copies every card of the source, so a `CHECKSUM` / `DATASUM` pair, or `DATAMIN` /
  `DATAMAX`, would describe the old data. Siril writes none of them by default; a source that
  carries them yields a file that fails checksum verification.
- The protection of the input against a save over it by another spelling of its path relies on
  Windows file sharing; elsewhere only the path compare stands (the window, the only caller, is
  Windows-only). The file a WCS was borrowed from and the ICC profile are not guarded: the save
  dialog asks before replacing any existing file.
- `Salveaza ca...` writes on the UI thread, with a wait cursor: about a second for a 24 MP float
  FITS on an SSD.
