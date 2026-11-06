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
> `AstroPhotoAnalyzer.Core/Analysis/PrintSizeAnalyzer.cs`,
> `AstroPhotoAnalyzer.Core/Input/ImageSharpReader.cs` and
> `AstroPhotoAnalyzer.Wpf/ViewModels/MainViewModel.cs`. A change to the code this describes
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
- The diagnostic log is written beside the executable, wherever that is: a program kept inside
  an image folder writes its `log` folder there (ADR 0069).
- The profile window lists the installed profiles on the UI thread, with no busy sign: tens of
  milliseconds for a normal colour folder, but a folder of hundreds of printer profiles, or one
  damaged profile the package is slow on (4 s measured on one corrupted file), freezes the
  window that long.
- The remembered profile (R-156) is read again on the UI thread before the window shows: a
  profile chosen from a network share that is later unreachable holds the start for the
  share's timeout, tens of seconds, before the bar says in red that it cannot be read; "Scoate"
  then ends it for good.
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
- On the sky path the Great Rift is found to within a few tenths of an L\* of the band path,
  not exactly (ADR 0084): without geometry the dust is what lies 3 sigmas under the median of
  its cell, which takes 73 % of the band path's dust patches and some sky and band patches at
  the Rift's edges (the Smida frame with stars: L\* 4.90 against 4.85; the same frame starless:
  4.92 against 4.03, the class 13 % of the frame against 9 %, since without stars the sky's
  sigma is a third lower). On `de_printat.png` the class's 25 % percentile (6.1) is lighter
  than the darkest 1 % of the sky (4.4), so the sky still decides there. A plate-solved FITS, or
  `--wcs` with the stack's header, is the way to the band classes and the clouds' names.
- On the sky path a 32 px patch holding a bright star reads as nebula or bright nebula, so a
  frame with stars has speckles on the map; the anchors move by under 1 L\* (Rosetta with and
  without stars: 34.2 against 34.8 since ADR 0084; 32.0 against 32.2 before). A 3 x 3 median on the patches was measured and rejected
  (ADR 0033).
- An object that fills the frame becomes the modelled sky: on a 1.3 degree field on LDN 42 the
  cloud is the sky class and the plain sky around it the bright nebula, and the verdict's "sky"
  is the cloud's own level. The report still says what it measured. Since ADR 0084 the model
  is the median of each cell, so on a frame no wider than one cell (640 px) whose object's halo
  covers most of it, the halo lifts the model and the lower half of the plain sky falls under
  it by more than the sky's own noise: the synthetic `dark-sky` golden scene (512 px) classes
  30 % of its frame as dust and reports the dust 2 L\* over its dark blob. A print file is
  never that small; a real frame has tens of cells.
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
- A star under 12 local sigmas is not counted (ADR 0054): the FWHM is the bright stars', and a
  frame whose stars all sit on strongly textured nebulosity, where the border's sigma is the
  texture left after a plane is taken out, may fall to the dpi rule of R-074 with a count under
  20, or under 10 per megapixel (ADR 0062); the report says so. A smooth slope is not texture: the plane removes it.
- A wide star lays its own wings on the window border, which the plane cannot take out: it does
  not reject itself (all 64 synthetic stars kept at FWHM 12, 13 and 14, `R070_AWideStarDoesNotRejectItself`),
  but the wings lift the border median, its base, so it reads narrower: 12.50 px at 13, 13.19 at
  14, a bias of the estimator older than ADR 0054. The frames measured so far have stars of 2 to
  5 px, where it is nil.
- The half-max estimator overestimates a sampled Gaussian by ~2.5 %.
- On an 8-bit file the background's levels (the per-channel medians of the sky patches, R-060)
  are whole levels, so the chroma of the sky colour moves by up to about 0.7 (one level of a
  channel at a sky of level 20 to 60) between the 8-bit and the 16-bit version of the same
  image, and a cast near `CastWarnChroma` (3) can fall either side of it.
- The cast is the sky's colour against a grey; a sky that is physically green (airglow) or
  yellow (pollution) after a star-based calibration reads as a cast when its chroma passes 3,
  whether or not the owner wants it neutral.
- Neutral edge brightening cannot be told from the object; it is reported as
  informative and left to the eye. A region is measured on its sky pixels so that the object's
  own colour does not pass for a gradient (ADR 0056), which leaves two limits: a region the
  object fills to over three quarters is measured on every pixel, and the object's colour then
  counts; and sky classed sky that is darker or browner than the frame's, the Rift's diffuse
  foot in the top-left corner of the 2026-08-12 core (-5.4 / -4.5 / -8.7 on its sky pixels),
  reads as a coloured departure that only the eye can tell from a residual gradient.
- A horizon glow bright enough to be classed nebula over a region leaves that region's sky
  pixels; the quarter floor keeps it in view when it fills the region, and its blue deficit on
  the remaining sky kept the flag on every measured image, but its magnitude is the sky
  pixels' (the plate-solved FITS's bottom edge +1.4 / -4.0 / -5.0 where every pixel gave
  +12.3 / -0.9 / -12.1).
- JPEG numbers are ImageSharp's encoder's; another encoder will differ by tenths of a
  level at q100.
- Colour is measured as sRGB; other profiles are only named.
- Palette PNG/TIFF and multi-page TIFF are read through ImageSharp's conversion and noted,
  not measured natively.
- The plateau needs flat sky; a frame full of structure may yield few or no tiles.
- The banding verdict counts tiles of 64 px: a flat patch smaller than a tile, or one that
  no whole tile falls inside, is not seen; and the share is estimated from 5000 random
  positions, so a share near the 0.1 % threshold (about five tiles) can fall either side of
  it between two images that differ by one patch (ADR 0060).
- The suggested curves (ADR 0032) are one recipe: the segment slopes come from one curve the
  owner liked on one image, and no point ever goes under its input, so a frame whose peaks are
  already at or over 0.92 of the scale gets no suggestion, only the reason (ADR 0033); the
  shoulder that would make room by compressing the highlights is not built. A frame whose five
  anchors do not increase, or with an empty class, gets none either; a dust at black is not a
  refusal, its point is the origin.
- A saved FITS holds the raster's values: what the reader clipped to 0..1 (and a NaN, to 0) is
  saved clipped. The ICC profile of a PNG or TIFF source is not carried, and a borrowed WCS
  (`--wcs`) is not written. SIP cards are copied with the rest and still ignored by the classes.
- The gamut figure is one distance per pixel, the whole colour difference after the compensated
  round trip (ADR 0081): it says the paper changes the tone or the colour, not which, and it is
  not Lightroom's rule, which is unpublished (on the Smida TIFF through the WhiteWall baryta
  profile the two agree on half of the pixels either one marks). Near the threshold it is
  decided on the pixel's 8-bit colour (ADR 0042), so on a 16-bit or float image it is within
  0.14 points of the exact per-pixel figure, not equal to it (measured 2026-11-05: the FITS's
  dust on baryta 11.10 % against 10.96 %). `Maxima`
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
- The paper fit never moves a tone: a pixel whose tone itself is out of the paper's reach (its
  grey fails the rule, which happens under the paper's black of a canvas) stays as it is and is
  counted in the evidence; the Tonuri tab's lift is the remedy (ADR 0083). Far past the paper's
  edge every variant ends flat: the knees keep the steps of a saturation run only near the edge.
- The fit's 16-bit TIFF is uncompressed: 135 MB for a 22.6 MP image, the size Siril writes the
  inputs at (ADR 0083). The fit of a whole image for the save runs on the UI thread with a wait
  cursor, as the curves' save does, and takes 11 to 27 s for a 22.6 MP image (measured
  2026-11-06 on the three pairs of `paper-fit.md`): every pixel that lands on the paper's edge
  is searched exactly through the profile.
