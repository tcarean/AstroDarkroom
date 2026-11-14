# The paper fit

> Written 2026-11-06 with the feature (R-175 to R-178, ADR 0083); the comparison with the
> original added 2026-11-13 (R-179, ADR 0090). **Covers:**
> `AstroPhotoAnalyzer.Core/Analysis/GamutFit.cs`, `AstroPhotoAnalyzer.Core/Runs/GamutFitRun.cs`,
> `AstroPhotoAnalyzer.Core/Report/GamutFitTexts.cs`, `AstroPhotoAnalyzer.Core/Input/TiffWriter.cs`,
> `AstroPhotoAnalyzer.Core/Input/IccProfiles.cs`, `AstroPhotoAnalyzer.Wpf/ViewModels/GamutFitTab.cs`,
> `AstroPhotoAnalyzer.Wpf/Views/GamutFitView.xaml`, the `GamutFitView` class in
> `AstroPhotoAnalyzer.Wpf/Views/ResultViews.xaml.cs`,
> `AstroPhotoAnalyzer.Wpf/Presentation/BeforeAfterViewer.xaml`,
> `AstroPhotoAnalyzer.Wpf/Presentation/BeforeAfterViewer.xaml.cs` and
> `AstroPhotoAnalyzer.Wpf/Presentation/SplitDivider.cs`. A change to the code this describes means
> this note is updated in the same task, or deleted with the decision it explains.

## What it does

The Culori pentru hartie tab takes the pixels the Soft proof tab marks (the rule of ADR 0081:
the whole colour difference after the compensated round trip through the profile over
`CurveGamutDeltaE`) and lowers their chroma, each along its own hue and at its own L\*, to the
largest chroma the paper holds there; L\* and hue never move. Three variants differ by their
knee; the tab shows them in a gallery like Editare Curbe, each beside the original across a
line the user drags, with a red layer on each side of what the paper cannot hold there, and
saves one as a new FITS or 16-bit TIFF. The owner's question behind it, the
measurements and the alternatives are in ADR 0083.

## The table (`GamutFitTable`)

The largest chroma is a smooth surface over L\* and hue, so it is tabulated there, not over
sRGB (ADR 0042's lesson: a grid over the colour rounds the factor's jump at the edge inward).
`Build(rule, lNodes, hNodes)` takes the rule as a function of L\*, a\*, b\* giving the whole
difference (`GamutTable.ExactLab`, which `GamutAnalyzer.Build` makes from the profile:
`DeltaEOfLab`, the trip of `RoundTrip` from a Lab colour; the tests inject a rule of their own)
and, at every node (`GamutFitLstarNodes` x `GamutFitHueNodes`, 101 x 121, the rows of L\* in
parallel), bisects ten times between 0 and `Unbounded` (150) for the largest chroma the rule
keeps within the threshold; a node whose grey itself fails is 0 and its L\* marked
`GreyOutOfReach`; a node whose ceiling still fits is `Unbounded`. `CmaxAt(L, hue)` reads
bilinearly. A rule that gives NaN makes a table with only `Problem`.

Measured on 2026-11-06 (the scratchpad probe of ADR 0083): the table builds in 1.3 s on the
lab's baryta profile; against the exact per-pixel search the factor differs by 0.004 at the
median and 0.025 at most on the two baryta profiles, by up to 0.28 on the canvas, whose edge is
sharper near its black (L\* 22). Hence the edge rule below. With the code as built (the probe
over a copy of the core, the quarter reduction of each image):

| pair | outside as is | left outside, table alone | left outside, with the edge search | whole image fit |
| --- | --- | --- | --- | --- |
| plate-solved FITS, lab baryta | 1.88 % | 0.19 % | 0.001 % | 19.5 s |
| plate-solved FITS, canvas | 2.39 % | 0.59 % | 0.003 % | 27.1 s |
| Smida TIFF, WhiteWall baryta | 1.37 % | 0.36 % | 0.000 % | 10.8 s |

## The fit (`GamutFit.Apply`)

Per pixel, the rows in parallel: the D50 Lab of the sRGB colour (`ColorMath.LabD50OfSrgb`, the
Bradford-adapted matrix the soft proof's display step shares, so the fit and the proof speak
one Lab); a chroma under `GamutFitMinChroma` is grey and copied, and with the facts asked for a
grey pixel the judge rejects is counted outside before and after and as a tone out of reach
(`Rejects`, ADR 0094; a mono raster is copied whole, nothing counted); `cmax` from the table, unless
the pixel's chroma reaches within `GamutFitEdgeMargin` of it or passes it, so that the fit would
land it on the edge, and the table has a judge: then `ExactEdge` searches the judge along the
pixel's own hue at its own L\* within that band (from zero when the band's bottom is already
outside; the band's top kept when it is still inside), halving the bracket `EdgeHalvings` (6)
times, and answers the bracket's bottom, a chroma the judge keeps; once per 16-bit colour
(`EdgeCache`). The judge (`GamutFitTable.Judge`) is the rule against the threshold by default
(the tests), and in the run the Soft proof tab's own: `GamutAnalyzer.Outside` on the colour's
sRGB, the hybrid of ADR 0042 on its 8-bit colour, so that a pixel the fit lands on the edge is
inside for that tab too (measured 2026-11-06 on the Smida TIFF: landed by the exact Lab rule,
0.001 % were left by that rule but 0.71 % by the tab's judge, which quantises; landed by the
judge, both agree). Why not once per 8-bit
colour, or per cell of L\* and hue, as ADR 0042's hybrid does: measured 2026-11-06, half of the
fitted pixels of the plate-solved FITS were still outside by the exact rule with either key,
since the pixels outside are dark, where one 8-bit step is a unit of L\* and tens of degrees of
hue and the edge moves by more than the tolerance within a cell. The judge is a step function
of the 8-bit colour, not monotone in chroma, so a pixel the knees move inward can land on a
colour the judge calls outside: V-016 reads 0.006 % left after `Lina` against 0.001 % after
`Exacta`, under the figure's precision (0.02 % before ADR 0093, when the pixels the judge
rejected inside the edge were never moved).

Whether a pixel is outside is not read off that edge (`Decide`, ADR 0093): the judge is asked
about the pixel as it is, on its own samples (`GamutFitTable.JudgeOfPixel`, which the run sets
to `GamutAnalyzer.Outside`; a table of the tests, without it, asks `Judge` on the pixel's Lab),
for every pixel, so "outside as is" is the Soft proof tab's own count. The edge is then made to
agree: a pixel the judge rejects although the edge holds its chroma (the table's edge, far
inside it, or the searched one, since the judge is not monotone) has the edge searched again by
`ExactEdge` in a band that ends at the pixel's own chroma, and a pixel the judge keeps past the
edge takes its own chroma as the edge, so the hard fit leaves it and the soft knees compress it
from a share of its own chroma. Where the search found no edge at all under such a pixel (the
judge rejects the grey of its tone), it has no edge and no knee moves it: nothing nearer the
grey is known to be inside. One case is left as it falls: `JudgeOfPixel` judges the samples
and `ExactEdge` asks `Judge` on their Lab round trip, so on a rare pixel the two can differ
about the pixel's own colour; it then stays as it is and is counted in what is left. Before this (the code review
of 2026-11-13, CR-001) the edge alone decided, and measured 2026-11-14 on the tonal subsamples,
`Exacta`:

| pair | outside as is, judge / fit | left, judge / reported | rejected by the judge, never moved | after ADR 0093: outside as is, left |
| --- | --- | --- | --- | --- |
| plate-solved FITS, lab baryta | 1.9427 % / 1.9073 % | 0.0416 % / 0.0005 % | 581 of 1 411 820 | 1.9427 %, 0.0004 % |
| plate-solved FITS, canvas | 2.4296 % / 2.3984 % | 0.0561 % / 0.0001 % | 791 | 2.4296 %, 0.0003 % |
| `de_printat.png`, lab baryta | 0.2916 % / 0.2547 % | 0.0402 % / 0.0000 % | 580 of 1 443 420 | 0.2916 %, 0.0000 % |
| Smida TIFF, WhiteWall baryta | 1.3609 % / 1.3633 % | 0.0042 % / 0.0000 % | 60 | 1.3609 %, 0.0000 % |

After it the judge and the fit agree on every pixel of the four pairs, before and after the
fit, under the three knees. The whole plate-solved FITS (22.6 MP) fits in 3.9 s on baryta and
3.7 s on canvas with cold caches, against 3.2 s and 3.3 s before: the judge's word on every
pixel costs about half a second. The share left after `Exacta` on a float source differs by a
few pixels from run to run (0.0004 to 0.0006 % on the plate-solved FITS that day, 0.0005 to
0.0008 % before): the edge is cached per 16-bit colour, several float colours share a key, and
the rows run in parallel, so which of them gives the key its edge is not fixed. On `de_printat.png` through the canvas profile the two agree
once the grey pixels are counted too (ADR 0094): 2.7695 % outside as is by both, 0.0261 % left,
all of it tones out of reach (377 pixels, 334 of them grey).

A pixel the judge rejects is outside; if `cmax` is negative (the judge rejects even the grey
of that tone: the edge search's answer for it, apart from a tiny edge over a grey it keeps,
which fits the pixel to that grey) it is copied and counted
(`PctUnreachable`): no chroma brings it in, and the Tonuri tab's lift is the remedy. The table's
`GreyOutOfReach` marks the nodes where that happens, for the record; the decision is the
judge's, since within one L\* of such a node the two can disagree. Otherwise the new chroma is `Knee(c, cmax, knee)`: unchanged up to
`knee x cmax`, then `start + room x (1 - exp(-(c - start) / room))` with `room = cmax - start`,
asymptotic to the edge; a knee of 1 is a hard clip at the edge. The colour goes back through
`ColorMath.SrgbOfLabD50`, clamped. With `keepFacts` the outcome also holds the masks before and
after (before: the judge on the pixel as it is; after: the judge on the fitted colour, or its
first answer for a pixel left as it is), the shares, the
chroma given up at the median and at the 99 % mark of the touched pixels, and the share of the
pixels outside under L\* `LowSeparationLstar`; the save of a whole image runs without facts.

Why the knee is asymptotic and not linear: a run of growing saturation past the edge keeps
its steps near the edge (where the eye compares neighbours) and ends flat far past it, where
every variant must end at the edge anyway; the test `R176_...` measures the steps at 10.05
and 11.05 against an edge of 10.

## The recipe (`GamutFit.Recipe`)

What a person would do by hand (a Lightroom luminance-range mask, the owner's first fix): the
smallest whole L\* under which `GamutFitBandShare` of the pixels outside lie, and the largest
saturation factor in steps of `GamutFitRecipeStep` whose uniform scaling of the chroma of every
pixel under that L\* brings `GamutFitRecipeShare` of the pixels outside in (decided with the
pixel's own `cmax`, so no further trips), the factor decided on the band's own pixels outside
whose tone is in reach (those above the band, and those whose tone is out of reach, the grey
ones the judge rejects among them, are beyond the mask and are counted in what is left; before this,
on the Smida TIFF, where 10 % of the pixels outside sit above the band, the factor ran down to
5 %), with the share of all the pixels outside it leaves. Measured on the tonal subsample, where
the evidence is measured too.

## The run (`Runs/GamutFitRun.cs`)

The fourth run after `AnalysisRun`, started by the window beside the curves' and the proof's
(ADR 0051), never by the console. `Execute(a, maxWidth, maxHeight)`: no profile behind the Dmax
gives `ProofTexts.NoProfile` as the set's `Problem`; `GamutAnalyzer.Build` (the run's own table;
its `Problem` is the set's); the fit's table from its `ExactLab`; the original reduced to the
gallery's size (`CurveRun.PreviewStride`) and the tonal subsample (`TonalStride`); for each knee
of `Knees` (`GamutFitKneeExact`, `GamutFitKneeSoft`, `GamutFitKneeSofter`) the fit of the
preview with facts (its image and mask are the gallery's) and of the subsample with facts (its
figures are the evidence, `GamutFitEvidence.Of`); the recipe on the subsample. Timed by a
`RunClock` named `gamutfit` (`profile`, `table`, each variant's name, `recipe`),
each variant's figures at Debug, the done line with the share outside, the recipe and the
profile's name. The set keeps the table and the variants' knees for the save; the window drops
the previews once drawn.

`GamutFitExport.Save(a, set, v, target)` runs under `CurveExport.Guarded`, the guards of R-145
lifted out of `CurveExport.Save` so both exports share them (the input gone, the same path, the
hold without sharing while the write lasts): the whole raster fitted with the set's table, then
by the target's extension a TIFF (`TiffWriter`, the input's ICC profile or `IccProfiles.Srgb`,
the description tag from `GamutFitTexts.Description`) or a FITS (`FitsWriter` with the identity
curve of two points, the `HISTORY` cards of `GamutFitTexts.History`, a FITS input's header
kept). One Info line per file written.

## The TIFF writer (`Input/TiffWriter.cs`) and the sRGB profile (`Input/IccProfiles.cs`)

A classic little-endian TIFF, uncompressed, 16 bits per sample, chunky, strips of
`RowsPerStrip` (64) rows; RGB for a colour raster (`Photometric` 2), grey for a mono one (1),
so the writer has no colour-only branch. The directory sits after the header, the data too
large for an entry's four bytes in a heap after the directory (word-aligned), the strips after
the heap; the tags are width, length, bits per sample, compression (1), photometric, the
description (270), strip offsets, samples per pixel, rows per strip, strip byte counts, 72 dpi,
planar (1), resolution unit, software (305, the banner), sample format (1) and the ICC profile
(34675). Samples are rounded to 0..65535. The crop of an 8-bit input (R-181, `crop.md`) asks for
8 bits per sample through the `bits` argument: the same file with one byte per sample, rounded
to 0..255; every other caller writes 16. An image over 4 GB is refused (a classic TIFF's
offsets are 32-bit). Written through `AtomicFile.Write`. Why not ImageSharp's encoder: it writes
8 bits whatever is asked (`dependencies.md`). Why uncompressed: ADR 0083.

`IccProfiles.Srgb` is the profile embedded when the input has none (a FITS, a PNG without
`iCCP`): a v2 display profile built at first use from the sRGB primaries adapted to D50 (the
columns of `ColorMath`'s matrix), the D50 white, and the sRGB curve as 1024 points of a
`curv` tag, with `desc` "sRGB" (which `IccProfileInfo.Describe` reads back) and a `cprt` of our
own: nothing copied from anyone's file.

## The tab (`ViewModels/GamutFitTab.cs`, `Views/GamutFitView.xaml`)

`GamutFitTabViewModel` is the `CurvesTabViewModel` pattern (`window.md`): `Populate` keeps the
result, `MainViewModel` starts `ComputeAsync` beside the other two once every tab is filled,
the tab runs it again by itself when its area allows a finer preview, as the curves tab does
(ADR 0084; `Compute(w, h)` for the tests, `Computation` to await); the run number
`_run` makes a late result nobody's, and a recompute keeps the position in the gallery (`Show`
clamps `_index`, which only `Reset` and a problem put back at the first), so the variant looked
at stays when a larger area brings finer previews. `Show` makes, once per set, the original's item and one
item per variant: the bitmap (`RasterBitmaps.Region`) and the red layer (`RasterBitmaps.Overlay`
over the set's mask, the soft proof's half-transparent red; null when nothing is outside), and,
for a variant, the evidence rows from `GamutFitTexts.EvidenceRows`, with the variant kept
without its preview, so the rasters are dropped once drawn. The original is no position of the
gallery (ADR 0090): `Items` holds the three variants and `Before` the original, the same object
whatever variant is shown, with no evidence of its own, since every variant's rows hold its
share. The box `ShowOutside`, on by default and back on at `Reset` (ADR 0084), is pushed to
every item and to `Before` (`GamutFitGalleryItem.ShowOutside`), whose `Overlay` is its layer or
null accordingly, so the view binds one property per side. `SaveAs` raises `SaveRequested` with
`GamutFitTexts.ProposedFileName` (the input's kind), the window shows a dialog whose filter puts
that kind first (`MainWindow.SaveGamutFit`), `Save(path)` calls `GamutFitExport.Save`. The view
is `CurvesView`'s grid without the purpose radios: the box, the message (red on a problem), the
comparison of the next section, Back/Name/Position/Next, `Salveaza ca...`, the stacked evidence
(only the current item's visible, so the image never moves), the core's note, the view-model's
`ViewerHelp` (which side of the line is which and the viewer's gestures, its maximum
interpolated from `ZoomPanViewer.MaxScale`), the save message's fixed place. The arrow keys are
the window's `GalleryKeys`, which serve whichever of the two gallery tabs is selected.

## The comparison (`Presentation/BeforeAfterViewer.xaml`, `Presentation/SplitDivider.cs`)

`BeforeAfterViewer` is a `UserControl` whose dependency properties are the two images, their two
layers, the size they share, the two label texts and `FitEnlarges`; its inner bindings read
them through `LayoutRoot.DataContext`, which is the control itself, so the bindings set where
the control is used keep the data context it inherits. Inside is one `ZoomPanViewer`
(`soft-proof.md`) whose child grid holds After and its layer and, above them, the `BeforeLayer`
grid with Before and its layer: one viewer, so one scale and one translation, and the two images
cannot drift apart.

The line is `SplitDivider`, ImageVisualCompare's `DividerModel` copied (ADR 0090): its place as
a fraction of the viewport's width (0.5 at the start), `MoveTo` for a drag, the handle's
rectangle, the hit test (within `LineGrabTolerance` of the line at any height, or on the
handle) and `LabelFits`. It knows no element: the control draws what it places and asks it what
a press grabs, so the drawing and the hit test cannot disagree.

The line belongs to the viewport, but the viewer transforms its child, so the cut of
`BeforeLayer` is a `Clip` in the child's units, `ToChildX(line x)` wide: `Place` sets it again
at every `ViewChanged` of the viewer (a zoom, a pan, a reset, an arrange) and at every move of
the line. `Place` also sets the render translations of the line, the handle and the two labels,
which sit in an overlay grid over the viewer, not hit-testable, each laid out once at the top
left, and hides a label whose side has no room for it. `Place` sets no layout property, on
purpose: it runs inside the viewer's arrange pass, and the first version, which set the line's
`Height` and `Canvas.Left` there, left the line where the previous pass had put it once the
tab's area changed size (the line started 66 pixels under the top in the tab's render);
`BeforeAfterViewerTests` holds that case. The line takes its height from the layout itself.

The mouse: `OnPreviewMouseLeftButtonDown` calls `BeginDividerDrag`, which asks
`SplitDivider.HitTest` and, on a hit, remembers how far from the line the press landed; the
control then captures the mouse and handles the event on the way down, so the viewer never sees
the line's press (no pan, no double-click fit). `OnPreviewMouseMove` calls `DragDividerTo`
while the line is dragged, which keeps that distance, so the line does not jump under the
pointer, and otherwise shows `SizeWE` over the line, unless the viewer is panning. Every other
press is the viewer's, and so is the wheel: while a drag holds the mouse here the wheel would
not reach the viewer, so `OnPreviewMouseWheel` hands it over (`ZoomAt` with the viewer's
`WheelFactor`). The three drag methods are internal and hold the whole rule, so the tests drive
a drag without a mouse. `After` going null (the gallery emptied by another image) hides the overlay, ends a drag in
progress, puts the line back in the middle and the viewer back at the fit (by itself the viewer
resets only for a child of another size, so two images of the same size would share a zoom); another variant replaces `After` without passing through null and
keeps it.

`FitEnlarges` is bound to `PreviewCapped`: false keeps an image smaller than the area at its own
size, the rule the `Viewbox`'s `DownOnly` held before the comparison (ADR 0084). A recompute for
a larger area changes the child's size, so the viewer goes back to the fit (R-163); the line
stays where it was put.
