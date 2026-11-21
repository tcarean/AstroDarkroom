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
  not exactly (ADR 0085): without geometry the dust is what lies 3 sigmas under the median of
  its cell, which takes 73 % of the band path's dust patches and some sky and band patches at
  the Rift's edges (the Smida frame with stars: L\* 4.90 against 4.85; the same frame starless:
  4.92 against 4.03, the class 13 % of the frame against 9 %, since without stars the sky's
  sigma is a third lower). Since ADR 0099, which takes the patches lighter than the sky out
  of the class on both paths, the frame with stars reads 4.3 against 4.5 (4.9 against 5.0
  just before it). On `de_printat.png`
  the class keeps 4.2 % of the patches, under the 5 % floor, so the darkest 1 % of the sky
  (4.4) decides there. A plate-solved FITS, or
  `--wcs` with the stack's header, is the way to the band classes and the clouds' names.
- On the sky path a 32 px patch holding a bright star reads as nebula or bright nebula, so a
  frame with stars has speckles on the map; the anchors move by under 1 L\* (Rosetta with and
  without stars: 34.2 against 34.8 since ADR 0085; 32.0 against 32.2 before; the starless
  figure is 33.6 since ADR 0099, the one with stars not re-measured). A 3 x 3 median on the patches was measured and rejected
  (ADR 0033).
- An object that fills the frame becomes the modelled sky: on a 1.3 degree field on LDN 42 the
  cloud is the sky class and the plain sky around it the bright nebula, and the verdict's "sky"
  is the cloud's own level. The report still says what it measured. Since ADR 0085 the model
  is the median of each cell, so on a frame no wider than one cell (640 px) whose object's halo
  covers most of it, the halo lifts the model and the lower half of the plain sky falls under
  it by more than the sky's own noise: the synthetic `dark-sky` golden scene (512 px) classes
  30 % of its frame as dust and reports the dust 2 L\* over its dark blob. A print file is
  never that small; a real frame has tens of cells.
- The dark globules inside a nebula are not dust on the sky path: they are under the nebula,
  not under the modelled sky; that would need the band path's local level over the whole frame.
- A dust patch must also be no lighter than the average sky, the median of the sky class (ADR
  0099). That narrows, without removing it, the limit of the object's halo that lifts the
  model, two bullets up: a sky-level patch beside a bright
  object, under the lifted model and at or under the sky's median, still counts as dust
  (`Rosetta_starless_no_WCS.fit` keeps 105 such patches, 1.3 % of the frame, of the 554 it had;
  the `dark-sky` scene keeps its 30 %, all at the sky's level). And a dark cloud lighter than
  the image's average sky, on the bright side of a strong gradient, is not dust: it is darker
  than its surroundings and still not the darkest part of the print. A patch that leaves the
  dust class on the sky path is nebula at most, never bright nebula, however far over the
  average sky it sits (ADR 0099).
- With a range shown in the Tonuri tab (R-046), a dust figure that comes from the darkest sky
  can stand outside the dust zone's bracket (`de_printat.png`: `4.4 (4.9 - 7.6)`): the bracket
  is the dust class's, the figure is not. The note under the table says so, with the reason;
  the owner chose the bracket always shown over a dust without one (ADR 0100).
- With a range shown, a row of the simulation table is 28 characters wider: in a window narrower
  than about 1330 px the end of the row (the last column and the mark) is cut at the right,
  where the plain table fits down to about 1135 px. Computed, not rendered at those widths:
  the longest row with its mark (125 characters against 97) at the 7 px a character of Consolas
  12 px in the window's Display mode, plus the Preview button (74 px), the card and the
  scroll padding (70), the scroll bar (11) and the parameter column (300); the frame of a
  window that is not maximised comes on top. And a bracket with an end at L\* 100.0 and the
  other at 10.0 or over is wider than the 13 characters it is padded to and pushes the rest of
  its row; it takes a zone whose upper part is white.
- Without a WCS, a foreground or a silhouette darker than the modelled sky by the sky's noise and
  larger than 5 % of the frame becomes "the dust".
- On either path a posterised or synthetic sky, where more than half the patches share one
  value, has a sigma of 0: on the sky path the thresholds collapse to the model and a patch one
  level off it is nebula or dust; on the band path (ADR 0086) a patch a hair under the band's
  local level is dust and one a hair over it a bright cloud. Anexa B then shows a threshold of
  0.0 L\*.
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
  not measured natively; of a multi-page TIFF only the first page is read, so a file whose
  first page is a small preview is measured as that preview, the size on the file line and
  the note being the only signs.
- An animated PNG is refused (`imagine stricata: Invalid PNG data`), and so is a TIFF whose
  first directory holds no image (ADR 0112).
- The Content Credentials Lightroom adds to an exported TIFF are stepped over, not read, and
  no file the tool writes carries them (ADR 0112).
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
- The dust move (ADR 0101) puts the dust's figure on paper black, and that figure is the dark
  part of the dust zone (the level under which a quarter of its patches lie): the patches
  darker than it end under paper black, as solid black (0.2 % of the frame before, 2.2 % after
  on the plate-solved FITS at Dmax 2.4). The stretch it costs falls in the lighter part of the
  dust, up to 2.5 times at its steepest on that image; the evidence gives its mean, as a
  percentage, and nothing caps it but the spline staying monotone. The noise there was measured
  on two smooth tiles only. Whether the move can be had is told for every image, but that a
  move leaves no monotone curve is found only when it is asked for: the box then goes off after
  the computation. With the box on, Print and Screen show different curves, so the first change
  of purpose computes the other gallery. An image whose dust figure is the darkest sky
  (`de_printat.png`, Rosetta, the Veil) is not offered the move at all.
- A saved FITS holds the raster's values: what the reader clipped to 0..1 (and a NaN, to 0) is
  saved clipped. The ICC profile of a PNG or TIFF source is not carried, and a borrowed WCS
  (`--wcs`) is not written. SIP cards are copied with the rest and still ignored by the classes.
- The gamut figure is one distance per pixel, the whole colour difference after the compensated
  round trip (ADR 0081): it says the paper changes the tone or the colour, not which, and it is
  not Lightroom's rule, which is unpublished (on the Smida TIFF through the WhiteWall baryta
  profile the two agree on half of the pixels either one marks). Near the threshold it is
  decided on the pixel's 8-bit colour (ADR 0042), so on a 16-bit or float image it is within
  0.14 points of the exact per-pixel figure, not equal to it (measured 2026-11-05: the FITS's
  dust on baryta 11.10 % against 10.96 %). The paper fit therefore aims a little inside the
  warning (R-187, ADR 0109): 3.5 was found on one image against two profiles and Lightroom
  Classic alone, a few points can still be marked there after the fit (at most 0.005 % of the
  Smida TIFF on the WhiteWall baryta), another paper may need more, and on a paper where
  Lightroom marks nothing even before the fit (the lab's baryta) the reserve gives up some
  colour for no visible gain (2.70 % of the pixels changed instead of 1.49 %). No limit could be
  read from the profile: its own round-trip error does not predict where Lightroom stops. `Maxima`
  clips more pixels than the other suggestions (0.18 % against 0.10 % on the reference PNG).
- A FITS on another scale than 0..1 is refused only when more than half of its samples are
  over 1 (ADR 0096). One with half or more at or under 1, such as a background-subtracted
  linear stack on 0..65535 whose sky sits under 1, passes that check, and passes the linear
  refusal too when its clipped median is over 0.02: it is analysed clipped, with the count in
  the note. Not met on a real file.
- A mono image has nothing outside the paper's gamut, in the Editare Curbe figure, in the Soft
  proof tab's red layer and in the Culori pentru hartie tab, though the rule counts the tone too
  (ADR 0081): the same image stored as RGB can show its darkest greys as tones the paper cannot
  make. The owner left it so (ADR 0094). Measured 2026-11-14: the grey copy of `de_printat.png`
  has 0.03 % of its pixels outside on the canvas profile, all under L\* 0.13, and none on the
  baryta; the grey copy of the plate-solved FITS has none on either.
- A saved FITS copies every card of the source, so a `CHECKSUM` / `DATASUM` pair, or `DATAMIN` /
  `DATAMAX`, would describe the old data. Siril writes none of them by default; a source that
  carries them yields a file that fails checksum verification.
- The protection of the input against a save over it by another spelling of its path relies on
  Windows file sharing; elsewhere only the path compare stands (the window, the only caller, is
  Windows-only). The file a WCS was borrowed from and the ICC profile are not guarded: the save
  dialog asks before replacing any existing file.
- The console's `-o` is refused over the image, the `--wcs` file and the `--icc` profile by
  comparing full paths (R-005, ADR 0107); another spelling of one of them is stopped by Windows
  file sharing alone, the three held open while the report is saved, and is told with the
  system's reason. The console compares the paths without regard to case, as the exports' guard
  does, so on a file system that tells `IMAGE.TIF` from `image.tif` an `-o` that differs from
  an input only in case is refused too. The window's "Save the report..." has no such guard: its
  dialog asks before replacing a file.
- `Salveaza ca...` writes on the UI thread, with a wait cursor: about a second for a 24 MP float
  FITS on an SSD.
- On a gallery area over 8 MP (a 5K screen; a 4K screen's gallery area stays under it, the
  window's chrome and the rows under the gallery taken off its 8.3 MP) with an image over 8 MP
  but smaller than the area, the cap of `CurvePreviewMaxPixels` holds the preview under its cap
  and the view enlarges it past the image's own size (ADR 0084); on a 4K screen and under, a
  small image stays at its own size. This is the Editare Curbe tab's alone since ADR 0118.
- Culori pentru hartie and Soft proof work on the whole image (R-188, ADR 0118), and both runs
  start with every image opened, whether the tabs are looked at or not: on a 22.6 MP image the
  proof is shown about 3 s after the analysis and the fit, which waits for it, about 15 s after
  (the tab shows "computing" until then); the two tabs then hold some 30 MB for each megapixel,
  and while the runs last about 115 MB for each are needed, 2.6 GB on that image and about
  7 GB on one at the limit; a comparison in Soft proof holds the second image too. An image
  over `WholeImageMaxPixels` (64 MP) is still shown as a reduced copy, and the texts under it
  say so. How smoothly the window zooms and pans bitmaps of that size was not measured.
- A fit's computation that another analysis overtakes (Analyse pressed again, another image
  opened while it runs) is not stopped: its result is dropped when it ends, but until then it
  runs beside the new one, each with its own rasters (ADR 0084 chose not to cancel; on the
  whole image that is about 12 s and 0.9 GB, where it was 2 s and 0.1 GB). One still waiting
  for the proof is abandoned before it starts.
- The paper fit never moves a tone: a pixel whose tone itself is out of the paper's reach (its
  grey fails the rule, which happens under the paper's black of a canvas) stays as it is and is
  counted in the evidence; the Tonuri tab's lift is the remedy (ADR 0083). Far past the paper's
  edge every variant ends flat: the knees keep the steps of a saturation run only near the edge.
- The fit's 16-bit TIFF is uncompressed: 135 MB for a 22.6 MP image, the size Siril writes the
  inputs at (ADR 0083). The fit of a whole image for the save runs on the UI thread with a wait
  cursor, as the curves' save does, and takes 11 to 27 s for a 22.6 MP image (measured
  2026-11-06 on the three pairs of `paper-fit.md`): every pixel that lands on the paper's edge
  is searched exactly through the profile.
- The crop for a frame carries the pixels and, for a TIFF or a PNG, the ICC profile, nothing
  else of those two kinds: an alpha channel, a second page, the resolution and every other tag
  are not written, a compressed TIFF comes out uncompressed, and a PNG gains a `pHYs` chunk of
  96 dpi (ADR 0098). A FITS keeps every card; of those that depend on where the image starts
  only `CRPIX` is moved, so a keyword of another convention (`LTV1`, a subframe origin) keeps
  the input's value, and a `CHECKSUM` or `DATASUM` card, which the curve's FITS copies too, no
  longer matches the data. A TIFF of a depth other than 8 or 16 bits is refused, since the reader
  only gets it converted (R-012).
- A window closed while a crop is being written waits for the file and closes then; it cannot
  be told to stop waiting. A save that never returns (a network share that stalls) keeps the
  window open until the process is ended by hand, and a Windows log-off does not wait, so the
  temporary file `<name>.tmp-<guid>` may be left beside the target then (ADR 0098).
- The crop's rectangle is placed on a reduced copy without zoom (the owner's choice, ADR
  0098): on a 23 MP image on a Full HD screen one screen pixel is about four image pixels, and
  the rectangle cannot be placed finer than that. Its proportions are the frame's within half
  a pixel, the size being in whole pixels.
