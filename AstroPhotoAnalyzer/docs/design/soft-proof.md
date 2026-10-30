# The soft proof

> Written 2026-10-30 with the feature (R-160 to R-164, ADR 0066, ADR 0068). **Covers:**
> `AstroPhotoAnalyzer.Core/Analysis/SoftProof.cs`, `AstroPhotoAnalyzer.Core/Runs/ProofRun.cs`,
> `AstroPhotoAnalyzer.Core/Report/ProofTexts.cs`,
> `AstroPhotoAnalyzer.Wpf/ViewModels/SoftProofTab.cs`,
> `AstroPhotoAnalyzer.Wpf/Views/SoftProofView.xaml`,
> `AstroPhotoAnalyzer.Wpf/Views/SoftProofControls.xaml`,
> `AstroPhotoAnalyzer.Wpf/Views/SoftProofWindow.xaml` and
> `AstroPhotoAnalyzer.Wpf/Presentation/ZoomPanViewer.cs`. A change to the code this describes means
> this note is updated in the same task, or deleted with the decision it explains.

## What it shows

The Soft proof tab shows what the paper would do to the image, through the ICC printer profile
the Dmax came from (R-028, the top bar's profile, R-155), the way Lightroom Classic's soft
proofing does: a rendering intent, the paper's white and black shown as they are, the paper's
gamut warning, the image inside a margin, full screen on request. Viewing only: nothing is edited, nothing is written. Without a
profile the tab shows the core's sentence and nothing else (R-162).

## The chain (`Analysis/SoftProof.cs`)

`ProofProfile.Open(path)` opens the profile for the proof: the checks of `IccPaperBlack.Read`
(a readable printer profile, RGB or CMYK), then the tag table of Unicolour's `Profile` for the
tables towards the printer, B2A0 and B2A1, both required (the ICC specification makes B2A0
mandatory in a printer profile and the package refuses one without it, so a profile missing
either is refused with the table named), and the media white of the `wtpt` tag (D50 when
absent). It keeps two package configurations, one per direction and intent, the device black
through A2B1 in XYZ (`PaperBlack`; its L\* and Dmax are the ones `IccPaperBlack` reads), the
media white's L\*a\*b\* against D50 (`PaperWhiteLab`) and the display colour of sRGB white under
the simulation (`PaperWhiteSrgb`, the margin). `ForTests` makes a profile with the facts alone.

`ProofProfile.Table(intent)` builds, once per intent and keeps (a `Lazy` per intent in a
concurrent dictionary), a `ProofTable`: for every node of an sRGB cube of `ProofGrid` (65) nodes
per channel, the colour the paper shows, in XYZ relative to the paper's white. The trip of a
node: sRGB to XYZ D50 (the package); for `Relative` the ICC black point compensation towards the
paper, `XYZ' = XYZ x (W - K) / W + K`, W the D50 white and K the device black taken as the
neutral of its Y, as ISO 18619 and lcms do, so that a slightly coloured black does not tint the
shadows (the package has no BPC, so it is written here); the B2A table of the intent (B2A0 for `Perceptual`, B2A1 for the two
relative ones) to device values; A2B1 back to XYZ. The rows of the cube are built in parallel; a
NaN anywhere (no usable table) makes a table with a `Problem`. `RelativeNoBpc` skips the
compensation, so every tone darker than the paper's black lands on the device black and prints
as that black: the Tonuri tab's model (ADR 0025), which is why it exists beyond Lightroom's two.

`SoftProof.Render(profile, table, raster, simulate)` proofs a reduced image: for
each pixel the paper colour read trilinearly from the table (`Trilinear.Corners`, the one read
`GamutAnalyzer.Interpolated` uses too), then the display step `SoftProof.Display`: with
`simulate` true, what the window always asks for, the absolute scaling by the media white,
`XYZ x wtpt / W`, so the paper's white and black show as they are; with it false, kept in the
core for the figures (V-015), BPC towards the screen, `(XYZ - K) x W / (W - K)`, so the paper's
black is the screen's black and its white the screen's white; then the Bradford-adapted
sRGB matrix (XYZ D50 to linear sRGB) and the encoding, clamped. A mono image goes through as
grey; the result always has three planes. The rows run in parallel. Why the table holds XYZ and not the
display colour: the display step is a per-pixel scaling, so one table per intent serves both
states of `simulate`.

The warning: the pixels outside the paper's gamut by the rule of R-144 (`GamutAnalyzer.Outside`
on the existing `GamutTable`, the exact trip near the threshold, ADR 0042), decided once per
image in `ProofRun.Prepare` on the source pixels of the reduction, since it does not depend on
the intent; the share equals the Editare Curbe tab's figure for the original. There is no
screen-gamut warning: with an sRGB source nothing can leave the screen's gamut (ADR 0066); a
clipped channel is clipped silently in `Display`.

## The run (`Runs/ProofRun.cs`)

The third run after `AnalysisRun` and `CurveRun`, called by the window alone, never by the
console. `Prepare(AnalysisResult)` gives a `ProofSet`: the profile (or the `Problem`: no profile
behind the Dmax, `ProofTexts.NoProfile`, which names the "From ICC profile..." button; an
unusable one, the core's reason), the image reduced so that its long side is at most
`ProofMaxSide` (`RasterOps.Subsample` at the integer stride), the gamut table of R-144 and the
paper mask with its share. `Render(set, intent, simulate)` builds the intent's table on its first
use and renders. The reduction is larger than the preview's (`PreviewMaxSide`, 1600) because the
proof is shown maximised on a 4K screen too.

## The texts (`Report/ProofTexts.cs`)

The sentences, all pairs in the interface language (ADR 0048): the two refusals, "computing",
the profile's line with its tables, the paper white and black lines, the legend with its share
and the threshold it quotes, the three intents' names and one-line helps (the radio
buttons' tooltips) and the caption (profile and intent). The window
writes none of them.

## The tab and the window (`ViewModels/SoftProofTab.cs`, the three views)

`SoftProofTabViewModel` holds the state of the controls (the intent as three booleans over one
`ProofIntent`, the two boxes, the margin choice) and what is shown: the proof bitmap and the
original's (`RasterBitmaps.Region` of the core's rasters), the warning's overlay
(`RasterBitmaps.Overlay`, red over the set's mask, made once per set like the original), the
margin brush, the core's sentences. `Populate` keeps the result; `MainViewModel`
starts `ComputeAsync` beside the curves' computation once every tab is filled (ADR 0051), which
prepares, renders and makes the frozen bitmaps on a pool thread (an `Outcome` record handed
back; an overlay over 2.7 MP would stall the window if made on its thread) with
"computing" shown meanwhile; a change of intent renders again the same way
(`RenderAsync`, the cached set, a new table only on a new intent), and one made while the first
computation runs is rendered right after it; the other controls raise properties. The run number
pattern of the Editare Curbe tab (`_run`): a result that ends after a `Reset` (another image, a
clear, a newer computation) touches nothing. The controls follow `HasControls`, the prepared set,
not the proof: they stay while a render runs and after one the core refused (a table with a
`Problem` throws its reason, shown in red with no image, the set kept), so another intent can be
chosen; that refusal is not reachable from the hand-built profile, so the path is read, not tested.
`Compute()` is the synchronous body for the tests; `ProofComputation` the last task
started by either path, for whoever must wait (`MainViewModel.ProofComputation` is the one the
run started). The controls keep their state from one image to the next and are
not remembered between runs of the window (ADR 0066).

The margin (`MarginBrush`): `PaperWhite`, the default, is the paper's white (`PaperWhiteColour`,
from the core's `PaperWhiteSrgb`); the five fixed
backgrounds (white, light grey, grey, dark grey, black) are Lightroom's, presentation constants
in the view-model, not thresholds, since nothing is measured against them. `ShowOriginal` swaps
the image inside the same margin and hides the layer (`ShowPaperLayer` is false over the
original).

`SoftProofControls` is one `UserControl` for the controls, used by the tab and by the window
over the same view-model; its `ShowFullScreenButton` dependency property hides the button in the
window. The radio buttons carry no `GroupName`: with one, every instance in the process would
form one group (`testing.md`), and each instance's panel is a group already. `SoftProofView`
is a grid like `CurvesView`: the controls or the red message, the proof inside a `Border` whose
background is the margin brush, in a `ZoomPanViewer`, the sentences under it.

`Presentation/ZoomPanViewer.cs` is a `Decorator` that fits its child (the grid of the proof and
its layers, sized to the proof's pixels) and keeps two numbers of its own: the zoom relative to
the fit and the child point at the viewport's centre, in child units, so that a resize keeps
what was looked at; a child of another size resets both. The wheel zooms by 1.25 around the
cursor (the child point under it stays under it), clamped between the fit and `MaxScale` (8)
device-independent pixels per child pixel (8:1 on a display at 100 % scaling), the viewport's
growth never carrying the scale past it; from `PixelScale` (2) up the bitmaps are drawn nearest
neighbour, so that the zoom shows the proof's pixels; the left button drags with the mouse captured; a double click
resets; the centre is clamped so that the child never leaves the viewport (centred along an axis
where it is smaller). The child is arranged at its natural size under a scale and a translation.
View state only: the view-model knows nothing of it.
`SoftProofWindow` is a borderless window over the whole screen (`WindowStyle` `None`,
`WindowState` `Maximized`, `ResizeMode` `NoResize`; R-164, ADR 0068), non-modal, over the tab's
own view-model (so the window's state is the tab's): the margin brush filling the window behind
the image (the same `ZoomPanViewer`), and over its top a panel with the controls, the caption,
the message ("computing" or a refused table's reason, red), the key hint and the `Close` button
(`CloseClicked` closes). The panel sits in a transparent `Border`, the band, aligned to the top;
a style trigger keeps the panel `Hidden` (not `Collapsed`, so the band keeps its height) unless
the window's `PanelShown` dependency property is true, which the band's `MouseEnter` and
`MouseLeave` set and clear. The keys are in `PreviewKeyDown`: Esc closes, G and O toggle the
boxes and I moves to the next intent through `SoftProofTabViewModel.HandleKey`, ignored while
the focus is in a text box or a modifier is held.

## What was measured

On 2026-10-30, on the lab's two profiles, the Epson P900 baryta profile and the CMYK
`RSWOP.icm` (ADR 0066): the package evaluates the three intents and lets each direction use its
own; the paper white under the absolute intent is L\* 97.6 b\* +1.1 on the Chromatone baryta,
L\* 95.0 on the canvas, L\* 95.5 on the P900 baryta; relative without BPC on the canvas sends
every grey under L\* 22 to the paper's black, with BPC nothing is crushed; a 65-grid read
trilinearly is within Delta E76 1.0 of the exact per-pixel proof at the 99th percentile on both
lab profiles (max 1.4 on the canvas), where 33 nodes reach 5.2; a 65-grid builds in 1.6 to 2.2 s
with the rows in parallel. The figures on the plate-solved FITS through the baryta profile are
V-015 (`../validation.md`): the paper white L\* 97.2, b\* +1.1 (sRGB 248, 247, 245), 1.37 % of
the proof outside the paper's gamut, and a mean L\* of
17.0, 19.7 and 18.2 on the three intents.

## The hand-built profile of the tests

`IccPrinterTestData.V2Lut16` carries, since this feature, the tables towards the printer: B2A1
as the inverse of the neutral model on L\* alone (an input table of 4096 entries turns the legacy
16-bit L\* into `(L* - black) / (100 - black)` clamped, so the kink at the black sits within
0.025 L\*; a 2-point cube whose every output channel is that axis; a\* and b\* ignored, so every
colour prints as the grey of its L\*), B2A0 and B2A2 with the black at L\* 0, and a `white`
parameter for a tinted media white. Hence the tests: a grey ramp comes back to itself under
`Relative` with the simulation off; the paper's white and black appear under the simulation;
`RelativeNoBpc` sends a grey under the black to the black; `Perceptual` lands L\* 50 at 60 on a
black of 20 where `Relative` returns about 52.8; every coloured pixel is outside the fixture's
gamut, which is how the paper mask is tested against the Curbe rule.
