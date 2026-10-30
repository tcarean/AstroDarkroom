# Remove the Preview tab

Branch `claude/Remove-the-Preview-tab`, worktree `.claude/worktrees/Remove-the-Preview-tab`
(from `main` at 33813f6). The approved plan is copied to
`docs/plans/2026-10-30-remove-the-preview-tab.md` on the branch.
Approved 2026-10-30; implemented on the branch the same day.

## Context

The owner asked (2026-10-30) to delete all the functionality of the Preview tab, in the console
and in the window, and to clean up whatever is left unused. Two things are called "Preview":
the **Preview tab** (R-117: the centre at 100 % and 200 %, the four corners, the whole frame, the
print simulation, the crop, and the screen-versus-paper step-back rule) and the **preview
window** of a simulation row on the Tonuri tab (R-131, R-045, ADR 0064). Asked, the owner chose
the tab only, and gave the reason: the Soft proof tab (R-163, ADR 0066, merged 33813f6) shows
the image maximised with zoom and pan, through the paper's profile, and supersedes the fixed
tiles. The preview window, the map window and the curves gallery stay.

The console has no tab; its part of "Preview" is two report sentences that send the reader to
the Preview tab (the darkening of a coloured edge region and the note on neutral edge regions,
R-093) and three core members that existed only for the tab's step-back rule
(`PrintSizeResult.ScreenEquivalentWidthCm`, `ScreenStepBackFactor`, `Thresholds.ScreenPixelMm`).

## What goes (code)

**Window** (`AstroPhotoAnalyzer.Wpf/`):
- `Views/PreviewView.xaml` (delete) and the `PreviewView` partial class in `Views/ResultViews.xaml.cs`.
- `PreviewTabViewModel` in `ViewModels/ResultTabs.cs` (lines 952-1121) and the file's header
  sentence about "the Preview guide's 1 / ScreenStepBackFactor"; the header's "the six tabs
  with results" becomes five (Raport, Tonuri, Export, Rezolutie, Culoare; Curves and Soft proof
  have files of their own).
- `PreviewTile` in `ViewModels/Rows.cs`.
- `PreviewTileTemplate` and its comment in `Theme/Dark.xaml`.
- The `Preview` `TabItem` in `MainWindow.xaml`; `MainViewModel.Preview` (creation, property,
  `Populate`, `Clear`).
- `RasterBitmaps` (`Presentation/RasterBitmaps.cs`): `Whole` and the `zoom` parameter of `Region`
  were used only by the tab; remove both and reword the file comment (the bitmaps are now the
  map's reduced frame, the curves' previews, the preview window and the proof).

**Core** (`AstroPhotoAnalyzer.Core/`):
- `PrintSizeResult.ScreenEquivalentWidthCm` and `ScreenStepBackFactor` (`Analysis/PrintSizeAnalyzer.cs`).
- `Thresholds.ScreenPixelMm` with its comment (`Thresholds.cs`).
- `RasterOps.Crop` (`Input/RasterOps.cs`): only the crop tile used it.
- `Report/Recommendations.cs`: the two sentences reworded in both languages, keeping the
  prefix `RecommendationsTests` asserts (`... Praf la marginea cadrului`):
  - darkening: "... : look at the edge yourself; a gradient is corrected on the linear data
    (GraXpert), dust is the object." / "...: uita-te la margine; un gradient se corecteaza pe
    liniar (GraXpert), praful e obiectul."
  - neutral note: "... not a gradient - look at the edges before you correct anything." /
    "... nu un gradient - uita-te la margini inainte sa corectezi ceva."
  - the comment above the neutral note ("lets the user look in Preview").
  No golden scene triggers either sentence (no golden mentions Preview), so no golden changes.
- Comments: `Parameters.TargetBackground` (the Preview tile sentences), `ReportWriter.CropCutText`
  ("The same text in the recommendation and in the Preview tile").
- Stays: `RasterOps.Gamma` (the preview window), `Thresholds.PreviewMaxSide` and
  `PrintPreview` (preview window and map), `Simulation.Primary` (TonalAnalyzer, validation),
  `ReportWriter.CropCutText` (the crop recommendation), `GuideParagraph`.

**Window texts**: `ParameterFields.TargetBgHelp` loses "and is the one the print tile of Preview
shows. Empty = the grid only, and Preview takes the recommended row." -> "Empty = the grid
only." (both languages; `ParameterFieldsTests` asserts none of the removed words). The Culoare
guide paragraph in `ResultTabs.cs` (~930/936): "look in Preview before you correct anything" ->
"look at the edges before you correct anything" / "uita-te la margini inainte sa corectezi ceva".

## Tests

- Remove `R117_PreviewTilesSayWhatToCheckAndTheGuideHasTheStepBack` (`ViewModelTests`);
  `R111_ClearEmptiesEveryTab` uses `vm.Preview`: switch it to another tab with rows
  (`vm.Background`: `Rows`, `RegionRows`, `Guide`).
- `WindowSmokeTests`: the `tab-preview` `Render` line and the three asserts on `vm.Preview.Tiles`
  at the end of the main test. `WindowEnglishTests`: the `Preview` `AssertEnglish` line.
- `RasterBitmapsGiveThePixelsAskedFor` (`ViewModelTests`): keep the test on `Region` with a
  stride (what the map uses) and drop the zoom and `Whole` assertions.
- `PrintSizeAnalyzerTests.TheHundredPercentViewIsAPrintOfTheScreensPitch` and
  `RasterOpsTests.CropTakesTheRectangleAndRejectsOneOutsideTheRaster`: removed with their
  members (neither carries an R-ID). Report every test change at wrap-up (the kit's rule "No
  test edited to pass": these follow the removal, none is edited to pass).
- `RepoInvariantTests` then holds: R-117 `dropped` has no `R117_` test.

## Documentation

- `docs/requirements.md`: R-117 -> status `dropped`, text replaced by one sentence (dropped
  2026-10-30, superseded by the Soft proof tab R-163, ADR 0067; the ID is never reused). R-110:
  seven tabs, the list without Preview. R-023: drop "and drives the Preview tab's print tile;
  when absent, the Preview tile uses the recommended row" (the row is `Simulation.Primary`).
  R-093: the two reworded sentences.
- ADR 0067 `docs/decisions/0067-the-preview-tab-is-removed-the-soft-proof-shows-the-image.md`:
  context (what the tab showed, the soft proof's arrival, the owner's ask and reason), decision
  (the tab, its tiles, the step-back rule and the three core members go; the preview window,
  the map and the gallery stay; the report no longer sends the reader to a tab), alternatives
  rejected (keep the tab beside the proof; keep the step-back figure in the report; move the
  100 % tiles into the proof window), consequences (R-117 dropped, ADRs 0007/0020 cite a rule
  that no longer exists, README). Check main's latest ADR number before wrap-up (0066 now).
- Design notes: `window.md` (tab list, the `PreviewTabViewModel` row, `PreviewTileTemplate` in
  the templates list and its wrapping sentence, the explanation-pass history bullet gets a note
  that the tab was removed, ADR 0067), `docs/design/README.md` row 30 ("the seven tabs"),
  `code-map.md` (`PreviewView` in Views), `measurements.md` (the `ScreenEquivalentWidthCm` /
  `ScreenStepBackFactor` sentences), `thresholds.md` (`ScreenPixelMm` row),
  `known-limitations.md` (the step-back bullet), `extending.md` ("a preview tile's `Check`";
  `ScreenPixelMm` in the list of lifted literals), `console.md` ("the Preview tile" twice),
  `recommendations-and-report.md` (the neutral-regions row). `testing.md` is right as is (it
  names the preview *window*).
- `README.md`: the Preview row of the tab table (the "seven tabs" sentence becomes right), and
  the two edge bullets ("one note sends you to the Preview" / "to check in the Preview").
- `CHANGELOG.md` `[Unreleased]`: `### Removed` - the Preview tab and the step-back rule,
  superseded by Soft proof; the report's two sentences now say to look at the edges.
- `docs/plans/2026-10-30-remove-the-preview-tab.md`: this plan.

## Verification

1. `dotnet build AstroPhotoAnalyzer.sln -c Release --no-incremental -warnaserror` in the
   worktree (never in the main checkout), then `scripts/verify.ps1` (format, build, tests,
   design check, goldens unchanged).
2. `scripts/check-design.ps1 -Since main` for the notes the branch touches.
3. The rule "Formula changes re-validate": `Analysis/`, `Report/`, `Input/`, `Thresholds.cs` and
   `Parameters.cs` change, so copy the reference image set from
   `AstroPhotoAnalyzer.Tests/testdata/large/` into the worktree and run `/validate`; expected
   drift: none (no figure is computed differently). Paste the drift table at wrap-up.
4. `APA_DUMP_SCREENS=<scratchpad> dotnet test AstroPhotoAnalyzer.Wpf.Tests --filter WindowSmokeTests`
   and look at `fereastra.png`: seven tabs, no Preview.
5. Wrap-up: read the whole diff, commit on the branch, propose the merge.
