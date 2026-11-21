# The paper fit

> Written 2026-11-06 with the feature (R-175 to R-178, ADR 0083); the comparison with the
> original added 2026-11-13 (R-179, ADR 0090); the fit's own target 2026-11-17 (R-187, ADR
> 0109). **Covers:**
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

Since R-187 the fit aims a little inside what that tab marks. Two limits stand on the one rule:
the warning's `CurveGamutDeltaE` (4), which counts everything this tab shows (the shares before
and after, the two red layers, the dark share, the tones out of reach), and the fit's
`GamutFitTargetDeltaE` (3.5), which says which pixels are moved and where they land. Wherever
the sections below say "the judge" for moving a pixel or for its edge, it is the judge asked at
the target; "outside as is" and "left" are the judge asked at the warning's limit. The figures
of the tables below were measured before R-187, when the two were one limit of 4; V-016 holds
the built code's figures.

## The table (`GamutFitTable`)

The largest chroma is a smooth surface over L\* and hue, so it is tabulated there, not over
sRGB (ADR 0042's lesson: a grid over the colour rounds the factor's jump at the edge inward).
`Build(rule, lNodes, hNodes)` takes the rule as a function of L\*, a\*, b\* giving the whole
difference (`GamutTable.ExactLab`, which `GamutAnalyzer.Build` makes from the profile:
`DeltaEOfLab`, the trip of `RoundTrip` from a Lab colour; the tests inject a rule of their own)
and, at every node (`GamutFitLstarNodes` x `GamutFitHueNodes`, 101 x 121, the rows of L\* in
parallel), bisects ten times between 0 and `Unbounded` (150) for the largest chroma the rule
keeps within the table's `Limit` (the fit's target unless `Build` is given another); a node whose grey itself fails is 0 and its L\* marked
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
colour the judge calls outside: V-016 read 0.006 % left after `Lina` against 0.001 % after
`Exacta`, under the figure's precision (0.02 % before ADR 0093, when the pixels the judge
rejected inside the edge were never moved). Since R-187 that happens at the target, half a unit
inside the warning, so the share left by the warning's count reads 0 on V-016 under all three
knees.

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
(`PctUnreachable`): no chroma brings it in, and the Tonuri tab's lift is the remedy (on a table
with a warning the tone is past the target and the warning has a word of its own: the section
"The target and the warning" below says what then happens). The table's
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

## The target and the warning (R-187)

`GamutFitTable` holds two pairs of judges. `Judge` and `JudgeOfPixel` stand at the table's
`Limit`, the target: `Decide`, `Edge` and `ExactEdge` use them, so a pixel is moved when it is
past the target and lands on the largest chroma within it. `Warning` and `WarningOfPixel` stand
at the warning's limit and count: with either set (`hasWarning` in `Apply`), `before` is
`Warns` on the pixel as it is, `after` is `Warning` on the fitted colour, or `before` again for
a pixel left as it is, and a grey pixel is counted by `Warns`. A table with neither, which is
every table of the tests but R-187's, counts by its own judge exactly as before.

In the run both are `GamutAnalyzer.Outside` on the profile's one table of round trips:
`GamutTable.WithLimit` gives a second `GamutTable` over the same grid and exact trip with the
target as its `Limit` and a cache of its own, since the cache holds a yes or no per 8-bit colour
and the answer depends on the limit.

One case needs both. Where `Decide` answers that a pixel is past the target and the grey of its
tone is too (`cmax < 0`), no chroma reaches the target. If the warning marks the pixel, the
edge is searched again with `Warning` in a band ending at the pixel's own chroma, and the knee
compresses towards that edge: the pixel ends inside the warning, as it did before R-187. If
that search finds no edge either, the pixel is copied and counted as a tone out of reach. If
the warning does not mark it, it is copied and counted nowhere: nothing nearer the grey is
better. `Warns` is asked there even without facts, so the saved file is the preview's.

Two things differ there from the landing before R-187, both small. The search starts from the
pixel and, when the warning's edge is more than twice `GamutFitEdgeMargin` under it, halves the
whole stretch down to zero six times, so the hard fit lands up to a sixty-fourth of that
stretch inside the edge (0.09 at a chroma of 7) where the cached search around the table's edge
ends within 0.02. And such a tone has no table edge for the warning, so a pixel just inside the
warning's edge keeps its chroma under every knee while one just past it is compressed towards
the edge: a run of growing saturation steps back there by a few percent of the edge under the
soft knees.

How often: measured 2026-11-17 with the hard fit on the tonal subsamples, the tones no chroma
brings within 3.5 hold none of the pixels on the three baryta profiles (the plate-solved FITS
and `de_printat.png` on the lab's, the Smida TIFF on all three), 0.056 % of the plate-solved
FITS on the canvas profile, where none was out of reach at 4, and 0.050 % of `de_printat.png`
on it, where 0.026 % were. The built code leaves no more outside than the fit to 4 did on
either: 0.0000 % of the FITS (0.0001 % at 4), none of it out of reach, and 0.0261 % of the PNG,
all of it tones out of reach, as at 4.

The evidence's sentence on where the fit stops (`GamutFitTexts.EvidenceRows`) is added only when
more pixels changed than the warning marks, by the two shares as the rows print them.

Measured 2026-11-17 on the Smida TIFF of that day (`Lina`, the tonal subsample; the limit made
settable in a scratchpad copy of the core, ADR 0109), pixels changed and chroma given up at the
median:

| profile | fit to 4 | fit to 3.5 | fit to 3 |
| --- | --- | --- | --- |
| WhiteWall Photo Rag Baryta | 4.33 %, 1.19 | 5.52 %, 1.43 | 6.91 %, 1.72 |
| Hahnemuhle P900 Photo Rag Baryta | 6.79 %, 1.68 | 7.85 %, 1.97 | 8.98 %, 2.33 |
| the lab's baryta | 1.49 %, 0.16 | 2.70 %, 0.29 | 4.09 %, 0.53 |

The built code, run through `GamutFitRun` and `GamutFitExport` on that TIFF with the P900
profile, wrote a file equal sample for sample to the trial file fitted to 3.5 (0 of 67 715 037
samples differ), with the same 7.85 % and 1.97, 5.14 % outside before by the warning and none
after; no tone was out of reach on any of the three.

## The recipe (`GamutFit.Recipe`)

In this section "the pixels outside" are the recipe's own: those past the fit's target (R-187;
the last sentences say what that changed). What a person would do by hand (a Lightroom
luminance-range mask, the owner's first fix): the
smallest whole L\* under which `GamutFitBandShare` of the pixels outside lie, and the largest
saturation factor in steps of `GamutFitRecipeStep` whose uniform scaling of the chroma of every
pixel under that L\* brings `GamutFitRecipeShare` of the pixels outside in (decided with the
pixel's own `cmax`, so no further trips), the factor decided on the band's own pixels outside
whose tone is in reach (those above the band, and those whose tone is out of reach, the grey
ones the judge rejects among them, are beyond the mask and are counted in what is left; before this,
on the Smida TIFF, where 10 % of the pixels outside sit above the band, the factor ran down to
5 %), with the share of all the pixels outside it leaves. Measured on the tonal subsample, where
the evidence is measured too. It reads `Decide`, so since R-187 it aims at the fit's target: its
band and factor bring the pixels past 3.5 within 3.5, and the share it leaves is of those
pixels, not of the warning's (on V-016 the factor went from 70 % to 65 % and the share left
from 5.0 % to 8.0 %); its sentence says so ("of the colours past the limit the fit stops at"),
where the rows above it count by the warning.

## The run (`Runs/GamutFitRun.cs`)

The fourth run after `AnalysisRun`, started by the window beside the curves' and the proof's
(ADR 0051), never by the console. `Execute(a)`: no profile behind the Dmax
gives `ProofTexts.NoProfile` as the set's `Problem`; `GamutAnalyzer.Build` (the run's own table;
its `Problem` is the set's); the fit's table from its `ExactLab`, built at the target, with the
two pairs of judges of the section above; the original itself, the set's `Stride` 1, or over
`WholeImageMaxPixels` its subsample at the smallest step that brings it within
(`RasterOps.WholeImageStride`, R-188, ADR 0118: whatever area the tab has), and the tonal
subsample (`TonalStride`); for each knee
of `Knees` (`GamutFitKneeExact`, `GamutFitKneeSoft`, `GamutFitKneeSofter`) the fit of the
preview with facts (its image and mask are the gallery's) and of the subsample with facts (its
figures are the evidence, `GamutFitEvidence.Of`); the recipe on the subsample. Timed by a
`RunClock` named `gamutfit` (`profile`, `table`, each variant's name, `recipe`),
each variant's figures at Debug, the done line with the share outside, the fit's target, the
recipe and the profile's name. The set keeps the table and the variants' knees for the save; the window drops
the previews once drawn. On the whole 22.6 MP reference image the run takes about 11 s and
holds 0.9 GB while it lasts (ADR 0118).

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
result, `MainViewModel` starts `ComputeAfterAsync` once every tab is filled, handing it the soft
proof's computation: the fit shows "computing" at once and starts the core when the proof has
ended, since started together the two take every core from each other (ADR 0118: the proof
shown after 9.5 to 14.5 s for 3.2 s, the fit after 18 s for 15 s); another image opened while
it waits abandons it before the core starts. Once per image: the images are the whole image whatever the tab's area, so the view reports no
size and nothing is computed again (R-188, ADR 0118; `Compute()` for the tests, `Computation`
to await); the run number `_run` makes a late result nobody's. `Prepare` makes, once per set and
on the computation's thread (0.9 s on the window's would hold it), the original's item and one
item per variant: the bitmap (`RasterBitmaps.Region`) and the red layer (`RasterBitmaps.Overlay`
over the set's mask, the soft proof's half-transparent red; null when nothing is outside), and,
for a variant, the evidence rows from `GamutFitTexts.EvidenceRows`, with the variant kept
without its preview, so the rasters are dropped once drawn; `Show`, on the window's thread,
only takes that `Gallery`. `IsReducedCopy` (the set's `Stride` over 1) words the core's note
and the tab's `ViewerHelp`. The original is no position of the
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

`FitEnlarges` is false in `GamutFitView`: an image smaller than the area stays at its own size
and nothing is enlarged to fill it (R-188; until ADR 0118 it followed a preview held under its
cap).
