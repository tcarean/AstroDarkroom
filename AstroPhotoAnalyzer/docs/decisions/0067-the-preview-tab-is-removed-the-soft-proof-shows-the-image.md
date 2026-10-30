# 0067. The Preview tab is removed: the Soft proof tab shows the image

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The window had a Preview tab (R-117, since phase 7 of the first plan): fixed tiles of the
analysed image (the centre at 100 % and at 200 %, the four corners at 100 %, the whole frame
reduced, the print simulation at the primary target as a gamma on a downsample, and the crop
when the target ratio cut), each with a sentence saying what to check in it, and a guide card
with the screen-versus-paper rule: the width the file would print at a monitor's pixel pitch
(`PrintSizeResult.ScreenEquivalentWidthCm`, from `Thresholds.ScreenPixelMm` = 0.25 mm) and how
many times the monitor's distance to step back to see the reference print
(`ScreenStepBackFactor`). The three members existed for that card alone (ADR 0007 and ADR 0020
cite them as the example of a window figure computed in the core; ADR 0012 lists
`ScreenPixelMm` among the literals lifted into `Thresholds` for a window text). Two sentences
of the report sent the reader to the tab: the darkening of a coloured edge region ("check in
Preview") and the note on the neutral edge regions ("check in Preview before you correct
anything", R-093).

On 2026-10-30 the Soft proof tab arrived (R-163, ADR 0066): the whole image through the paper's
ICC profile, maximised in its own window, with wheel zoom and drag pan, the original beside it.
It shows the image, and the paper's effect on it, better than fixed tiles at two magnifications.
The owner then asked for all of the Preview tab's functionality to be deleted, in the console
and in the window, and for whatever was left unused to go with it; asked whether the preview
*window* of a simulation row (R-131, ADR 0064) was meant too, the owner chose the tab only,
and gave the reason: the Soft proof tab replaces it.

## Decision

The Preview tab is removed: `PreviewView.xaml`, `PreviewTabViewModel`, `PreviewTile`,
`PreviewTileTemplate`, the tab item and `MainViewModel.Preview`. With it go the members nothing
else read: `ScreenEquivalentWidthCm`, `ScreenStepBackFactor` and `ScreenPixelMm` in the core,
`RasterOps.Crop` (only the crop tile cropped pixels; `CropAnalyzer` computes the rectangle
and the report describes it), and in the window `RasterBitmaps.Whole` and the `zoom`
parameter of `RasterBitmaps.Region` (only the 200 % tile magnified). R-117 is `dropped`; its
ID is not reused. The window has seven tabs.

The report's two edge-region sentences no longer name a tab: they say to look at the edge, or
at the edges before correcting anything, in both languages; the Culoare tab's guide and the
target-background help text say the same. No golden scene triggers either sentence, so the
goldens do not change.

The preview window of a simulation row (R-131), the map window (R-130), `PrintPreview`,
`PreviewMaxSide` and `RasterOps.Gamma` stay: they are the Tonuri tab's, not the Preview tab's.

## Alternatives rejected

- **Keeping the tab beside the Soft proof.** Two places showed the same pixels, one of them
  colour-managed and zoomable, the other fixed crops assumed sRGB; the tab was no longer the
  better one for anything it showed, and the owner asked for it to go.
- **Keeping the step-back rule in the report.** It was never in the report: a window-only
  explanation of how to read the 100 % tiles. Without the tiles the figure explains nothing.
- **Moving the 100 % and 200 % tiles into the Soft proof window.** The proof's viewer zooms to
  any magnification already; fixed tiles would add nothing it does not have.
- **Removing the preview window as well.** A different feature (the lifted image of one
  simulation row with its two classes painted, ADR 0064), opened from the Tonuri tab; the
  owner was asked and kept it.

## Consequences

- R-117 is `dropped`; R-110 lists seven tabs; R-023 and R-093 no longer mention the tab. The
  tests of the tab (`R117_...`, the `tab-preview` renders, the tile assertions of
  `WindowSmokeTests`) are removed, as are the tests of `ScreenEquivalentWidthCm` and
  `RasterOps.Crop`. `R111_ClearEmptiesEveryTab`, which checked the Report and Preview tabs,
  now checks the five tabs of `ResultTabs.cs`; that showed the Export, Rezolutie and Culoare
  tabs cleared only `HasResult` and kept their rows, so the three got the `Clear` override the
  Report and Tonuri tabs already had (R-111; a Fixed line in the CHANGELOG).
- ADR 0007 and ADR 0020 cite the step-back rule as their example of a window figure computed in
  the core; the example no longer exists, and the rule stands (the Editare Curbe and Soft proof
  figures are its examples now). The ADRs are not edited (the kit's ADR 0024).
- `README.md` loses the tab's row; `window.md`, `measurements.md`, `thresholds.md`,
  `known-limitations.md`, `extending.md`, `console.md` and `recommendations-and-report.md`
  lose their mentions; `CHANGELOG.md` has a Removed entry.
- The rule "Formula changes re-validate" applies (`Analysis/`, `Report/`, `Input/`,
  `Thresholds.cs` and `Parameters.cs` changed); no figure is computed differently, and the
  validation run confirms it.
