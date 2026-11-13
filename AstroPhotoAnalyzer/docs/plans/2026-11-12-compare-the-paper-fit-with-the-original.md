# Compare the paper fit with the original

Date: 2026-11-12. Branch `claude/Compare-the-paper-fit-with-the-original`, worktree
`AstroPhotoAnalyzer/.claude/worktrees/Compare-the-paper-fit-with-the-original` (from `main` at
5a9befbd; the kit's rule "Worktrees"). Approved by the owner on 2026-11-13.

## Context

The Colours for paper tab (`Culori pentru hartie`, R-178) shows one image at a time inside a
`Viewbox`: the original, then the three fits, browsed with Back / Next. It cannot be zoomed, and
comparing a fit with the original means pressing Back and remembering what was there.

The owner asked for two things: zoom and pan on this tab's image too (the Soft proof tab has
them), and a comparison with the unmodified original made the way ImageVisualCompare makes it,
with clear Before and After labels: the buttons change only After, Before is always the original.

What exists and is reused:

- `AstroPhotoAnalyzer.Wpf/Presentation/ZoomPanViewer.cs`: the Soft proof's viewer (wheel zoom at
  the cursor up to 8:1, left-drag pan, double click fits; R-163). View state only.
- ImageVisualCompare's comparison is one thing: a vertical line across the view, image A left of
  it, image B right, the line dragged with the mouse and kept in place while the images zoom and
  pan under it (`ImageVisualCompare.Core/DividerModel.cs`, 143 lines, pure; its ADRs 0013, 0021).
  There is no shared library in the monorepo: tools share code by copying it.
- The tab's core run already returns the original's preview and its mask
  (`GamutFitSet.OriginalPreview`, `OriginalOutside`), all four previews the same size.

## The owner's answers (2026-11-12, in chat; recorded in ADR 0090)

1. The original leaves the Back / Next list: After goes through the three fits only (`1 / 3`),
   the tab opens on `Exacta` beside the original.
2. With the box `show what does not fit` on, red is painted on both sides, each its own: Before
   shows what the original has outside the paper, After what is left after the fit.
3. Romanian: the labels are `Inainte` / `Dupa`; the buttons become `< Precedenta` /
   `Urmatoarea >` in both gallery tabs (Editare Curbe too), so the word `Inainte` has one meaning.
   English stays `Before` / `After`, `< Back` / `Next >`.
4. The zoom works on the reduced copy, by the Soft proof's rules. Real pixels at high zoom would
   be a larger task of its own; not in this one.

Assumptions I made (say if one is wrong):

- The line is always there, as in ImageVisualCompare (no on/off switch); dragged to the left edge
  it leaves only After, to the right edge only Before.
- The left and right arrow keys stay the gallery's (Back / Next); the line has no keys.
- The Editare Curbe tab keeps its image as it is (only its two Romanian button names change).
  The new control is reusable there later, as a separate task.

## The change

### 1. Requirements first (`docs/requirements.md`)

- **R-179 (new):** the tab's image is a Before / After comparison: the original left of a
  vertical line, the fit chosen with Back / Next right of it; the labels `Before` / `After`
  (`Inainte` / `Dupa`) beside the line at the top, a label hidden when its side is too narrow
  for it; the line starts in the middle, is dragged with the left button (grabbed within 8 DIPs
  of it or on its 26 x 56 handle, cursor `SizeWE`), stops at the view's edges, belongs to the
  view (zoom and pan move both images under it) and returns to the middle with a new image; the
  zoom and pan of R-163 on both images together; the red layer on each side its own.
- **R-178 (rewritten parts):** the list is the three variants (`Exacta 1 / 3`, `Lina`,
  `Foarte lina`), no `Originalul` position, `Salveaza ca...` enabled on each; the box paints
  both sides; `Precedenta` / `Urmatoarea`. The sentence "under the original, the figures of the
  image as it is" goes (each variant keeps its row `before: x % of the pixels`).
- **R-146:** the Romanian button names.

### 2. Tests (fail first), in `AstroPhotoAnalyzer.Wpf.Tests`

- `SplitDividerTests` (no window): starts at 0.5, clamps to 0..1, `MoveTo`, the hit test along
  the whole line and on the handle, a press elsewhere is not a hit (`R179_...`).
- `BeforeAfterViewerTests` (STA, like `ZoomPanViewerTests`): the Before layer's clip ends exactly
  at the line at the fit, after `ZoomAt` and after `PanBy`; moving the line moves the clip and
  nothing else; a label hides when its side is narrower than it; the line is back in the middle
  after the sources go null.
- `ZoomPanViewerTests`: with `FitEnlarges` false a child smaller than the viewport stays at
  scale 1; `ViewChanged` fires on zoom, pan and arrange.
- `GamutFitTabTests`: the R-178 gallery test follows the rewritten requirement (three items,
  `1 / 3`, `Before` present, its layer and the current item's follow the box, `SaveAs` enabled on
  all three); the render test stays, with `APA_DUMP_SCREENS` for a look at the PNG.
  These tests change because the requirement changes, not to make anything pass.

### 3. The view (window project only)

- **`Presentation/SplitDivider.cs` (new, pure):** ImageVisualCompare's `DividerModel` adapted:
  the position as a fraction of the viewport width, `MoveTo`, `Reset`, `HitTest`
  (`LineGrabTolerance` 8, handle 26 x 56), the line's x and the handle's rectangle.
- **`Presentation/ZoomPanViewer.cs`:** two additions, the Soft proof untouched by both:
  - `FitEnlarges` (dependency property, default true): false caps the fit scale at 1, so an
    image smaller than the area keeps its own size (the rule of R-146 and ADR 0084 that the
    `Viewbox`'s `DownOnly` held); the tab binds it to `PreviewCapped`.
  - `ViewChanged` event raised at the end of `Apply`, and `ToChildX(x)` computed from the two
    transforms themselves, so a listener inside an arrange pass reads the new view, not the old
    `RenderSize`.
- **`Presentation/BeforeAfterViewer.xaml(.cs)` (new control):** dependency properties for the
  four bitmaps (`Before`, `BeforeOverlay`, `After`, `AfterOverlay`), the content's width and
  height, the two label texts and `FitEnlarges`. Inside: one `ZoomPanViewer` whose child grid
  holds the After image and layer and, above them, a Before layer (image and layer) clipped, in
  the child's own units, at `ToChildX(line x)`; the clip is set again at every `ViewChanged` and
  every move of the line. Over the viewer, not hit-testable: the 2-DIP line, the handle, the two
  label badges. One viewer and one transform, so the two images cannot drift apart.
  The line wins over panning as in ImageVisualCompare: `PreviewMouseLeftButtonDown` asks
  `SplitDivider.HitTest` first and, on a hit, captures the mouse and handles the event; anything
  else falls through to the viewer (pan, double click). The wheel is never intercepted.
- **`Views/GamutFitView.xaml`:** the `Viewbox` block becomes the new control, bound to
  `Before.Image`, `Before.Overlay`, `Current.Image`, `Current.Overlay`, `Current.Width/Height`,
  `PreviewCapped`; a faint help line under the core's note (`ViewerHelp`); the Romanian button
  names. `Views/CurvesView.xaml`: the Romanian button names only.
- **`ViewModels/GamutFitTab.cs`:** `Items` holds the three variants; a new `Before` item (the
  original's bitmap and red layer, made once in `Show`, the box pushed to it like to the others);
  `Item(...)` loses its `before` parameter; `ViewerHelp` (a view-model `*Help` text, its `8`
  interpolated from `ZoomPanViewer.MaxScale`, never typed). `GallerySizeChanged`, the automatic
  recompute, `GalleryKeys` and the save stay as they are.
- **Core, `Report/GamutFitTexts.cs`:** `OriginalRows` is removed (nothing shows it any more). No
  number or sentence is added outside the core; the line, the zoom and the labels are view state.

Known behaviour kept from R-163: when the window grows enough for a finer preview, the gallery
is recomputed at another size and the zoom goes back to the fit; the line stays where it is.

### 4. Texts (the rule "Plain words": original beside proposal; approving the plan approves them)

| Where | Now | Proposed |
| --- | --- | --- |
| Labels on the image | (none) | `Before` / `After`; `Inainte` / `Dupa` |
| Buttons, Romanian, both gallery tabs | `< Inapoi` / `Inainte >` | `< Precedenta` / `Urmatoarea >` |
| The box's tooltip, English | Paints in red the pixels whose colour the paper still cannot reproduce on the image shown; on the original, the same warning as in the Soft proof tab | Paints in red the pixels whose colour the paper cannot reproduce: on the Before side those of the original (the same warning as in the Soft proof tab), on the After side those still left after the fit |
| The box's tooltip, Romanian | Coloreaza in rosu pixelii a caror culoare hartia tot nu o poate reda pe imaginea aratata; pe original, acelasi avertisment ca in tabul Soft proof | Coloreaza in rosu pixelii a caror culoare hartia nu o poate reda: in partea Inainte pe cei din original (acelasi avertisment ca in tabul Soft proof), in partea Dupa pe cei ramasi dupa potrivire |
| New help line, English | (none) | Left of the line is the image as it is (Before), right of it the fitted one (After); drag the line to move it. The mouse wheel zooms around the cursor, up to 8:1 (one pixel of the copy becomes a square of 8 x 8 screen pixels, with Windows display scaling at 100 %); hold the left button and drag to move the image; a double click fits it again. |
| New help line, Romanian | (none) | In stanga liniei e imaginea asa cum este (Inainte), in dreapta cea potrivita (Dupa); tragi de linie ca sa o muti. Rotita mouse-ului mareste in jurul cursorului, pana la 8:1 (un pixel al copiei devine un patrat de 8 x 8 pixeli pe ecran, cu scalarea Windows la 100 %); tii apasat butonul stang si tragi ca sa muti imaginea; dublu-clic o potriveste din nou. |

The zoom sentences are the Soft proof tab's own, word for word.

### 5. Record

- **ADR 0090** `the-paper-fit-is-compared-with-the-original-across-a-line`: the four answers
  above, the divider copied from ImageVisualCompare and not shared, the clip in the child's units
  kept in step by `ViewChanged`, real-pixel zoom rejected for now with its cost. ADR 0084 gets
  its "superseded in part by 0090" line (the original's figures under it), in the way the kit's
  ADR 0024 allows.
- `docs/design/paper-fit.md`: the tab section rewritten, its `Covers:` taking the two new files;
  the slip "`Reset` turns the box off" corrected (the code turns it on). `soft-proof.md`: the
  viewer's two additions. `window.md`: the viewer is no longer only the soft proof's.
- `README.md` ("The window": the tab's paragraph, the Romanian button names), `CHANGELOG.md`
  (Added: the comparison and the zoom; Changed: the list of three, the button names).
- License headers on the three new source files; XML documentation comments on every type and
  method added or edited. No new package.

## Order of work

1. Worktree and branch; requirements (R-179 draft, R-178, R-146); ADR 0090 as accepted (the
   answers were given in chat today).
2. The failing tests of step 2, with their failing output pasted.
3. `SplitDivider`, the two `ZoomPanViewer` additions, `BeforeAfterViewer`.
4. The view-model, the two views, `GamutFitTexts`.
5. Docs, README, CHANGELOG; `/wrap-up`.

## Verification

- The gate: `scripts/verify.ps1` green in the worktree, with `--no-incremental -warnaserror`
  before the last run.
- `APA_DUMP_SCREENS=<dir> dotnet test AstroPhotoAnalyzer.Wpf.Tests --filter GamutFitTabTests`:
  the PNG of the tab read back (line in the middle, both labels, red on both sides).
- `/validate` with the drift table pasted: `Report/` is touched (a removed text), no figure is
  expected to move.
- The mouse is not driven by the tests (the hit test and the clip are; the wheel, the drag and
  the cursor are not): the window started from the worktree build on
  `starless_rbf0.5_stretch0.17.fit` with the baryta profile, for the owner to try the line, the
  wheel, the pan and the double click before the merge. The window is closed before any build.
- A subagent code review before the merge proposal, and one on the fix commit.

## Not in this task

- Real pixels at high zoom (a separate task, if wanted).
- The same comparison in the Editare Curbe tab.
- A version bump: a new feature seems to warrant a minor one; the owner's word decides.

## Record

What deviated from the plan above, and why:

- The line, the handle and the labels are placed by render translations inside an overlay grid,
  not by `Canvas` positions and a `Height` set from code. The first version set them inside the
  viewer's arrange pass, and the tab's render showed the line starting 66 pixels under the top
  once the area had changed size between two layout passes; the test
  `R179_TheLineRunsTheWholeHeightWithItsHandleInTheMiddleWhateverTheAreaBecomes` was written on
  that failure, then the placement changed.
- `ZoomPanViewer` gained a third member beside `FitEnlarges` and `ViewChanged`: `Viewport`, the
  size the view was last set for, so that the comparison reads the new width inside an arrange
  pass.
- The branch was cut from `main` at 5a9befbd, not 30a07e76: `main` had moved by the time the
  plan was approved.
- Verification: the gate green, `/validate` with all 27 cases passing (recorded in
  `docs/validation.md`), the tab's render read back.
- The review of the first commit (a subagent, 2026-11-13) found no confirmed defect and led to
  a second commit: the line keeps the distance from the pointer it was grabbed at, the wheel
  zooms while the line is held, a recompute keeps the variant shown, the drag is testable
  without a mouse, and tests were added for the two layers, an area the image does not fill and
  the view's bindings; `window.md` no longer says this tab uses a `Viewbox`.
- A third review, of the whole branch, asked for by the owner (a subagent, 2026-11-13), found
  one risk that two new tests then confirmed: the zoom survived a new image of the same size as
  the last, since the viewer resets only for a child of another size. The comparison now puts
  the viewer back at the fit when it empties. The same commit holds, by tests, that a new image
  starts at the first variant, that a finer copy goes back to the fit and keeps the line, and
  that the box reaches every variant.
