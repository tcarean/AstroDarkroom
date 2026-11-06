# Classify the preview window's layers by patch, as the table counts

Date: 2026-11-06. Branch `claude/Check-the-preview-layers-against-the-row`, worktree
`.claude/worktrees/Check-the-preview-layers-against-the-row` (from `main`, the kit's rule
"Worktrees"; the branch was made for the diagnosis below and keeps its name).

## Context

On 2026-11-06 the owner reported that the preview window of a simulation row (R-131), on a row
whose dust is listed over paper black, still paints zones "under paper black" and "between paper
black and L* 15", and suspected the classes were computed on the original image. Measured in the
worktree on the two reference images (baryta, Dmax 2.4, paper black L* 3.60):

- The classes are computed on the lifted copy: the preview's "L* < 15" share equals the row's
  column at every row, and its "under black" share tracks the row's "pure black" column
  (`starless_rbf0.5_stretch0.17.fit`, row 0.20, dust L* 10.5: table 0.10 % of the patches,
  preview 0.28 % of the pixels; row 0.12, dust L* 4.1: 1.70 % against 2.61 %).
- What stays red at row 0.20 is 0.28 % of the pixels sprinkled over 7.3 % of the frame (cells of
  32 x 32 preview pixels holding at least one red pixel; the red is 3.4 % of those cells): one
  dense blob, the core of Barnard 78 (L* 1.4 at that row, under black for real), and single-pixel
  noise over the clouds whose 32 px patches sit at L* 4 to 5, just over black. Half-transparent
  and seen at screen scale, the sprinkle reads as a large reddish zone.
- The table counts 32 px patches (R-044: the L* of a patch's mean luminance, so that noise and
  specks do not move it); the preview counts pixels. The two figures for "under black" differ for
  that reason alone.

The owner chose, on 2026-11-06, to class the preview by patch, as the table does: the layers then
cover exactly the patches the table counts, the dark cores stay coloured, the sprinkle goes, and
the red legend's share is the row's "pure black" column.

## The change

1. **`Analysis/PrintPreview.cs`** (the core; the window computes nothing). `Render(img, gamma,
   dmax)` keeps its signature and its reduced, lifted image. The classes: the patch L*s at the
   row's gamma, `DustAnalyzer.PatchLstars(RasterOps.Subsample(img, Thresholds.TonalStride), new
   LiftTable(gamma))`, the very computation `PrintStretchSimulator.Simulate` runs for the row
   (ADR 0035); each patch's class from its L* against paper black and `LowSeparationLstar`;
   every preview pixel inside a patch takes the patch's class, by the map's geometry
   (`bx = x * stride / (Side * TonalStride)`, as `MapViewModel.ClassAt` does, with the
   preview's `ReductionStride`); the pixels past the last whole patch, at the right and the
   bottom, are `None`, as they belong to no patch of the table either (R-044). The two shares
   become shares of the patches (`patchesUnder / patchCount`), so `PctUnderPaperBlack` is the
   row's `PctPatchesBelowPaperBlack` to the last decimal, and `PctUnderLowSeparation` the same
   count against 15 (a patch share, which the table's "L* < 15" pixel column is not; the ADR says
   so). `DustAnalyzer.Grid(w, h, stride)` becomes internal for the geometry; no new threshold.
   The file's head comment and the XML comments say "patch".
2. **`ViewModels/PreviewWindowViewModel.cs`**: the legends name the patches, and a `Note`
   property replaces the static note of `Views/PreviewWindow.xaml` so that the patch side is
   interpolated from `Thresholds.DustPatchPx` (the rule "Explanation is not diagnosis"). Texts,
   original and proposal (the rule "Plain words"; edited on the owner's approval of this plan):
   - Red legend, RO: "sub negrul hartiei (L* 3.6): 0.3 % din pixeli - se tiparesc cu acelasi
     negru" -> "sub negrul hartiei (L* 3.6): 0.1 % din dalele de 32 px - se tiparesc cu acelasi
     negru"; EN: "under paper black (L* 3.6): 0.3 % of the pixels - they print as the same
     black" -> "under paper black (L* 3.6): 0.1 % of the 32 px patches - they print as the same
     black".
   - Amber legend, RO: "intre negrul hartiei si L* 15: 10.0 % din pixeli - pe hartie se
     deosebesc, dar ochiul abia le vede diferenta" -> "intre negrul hartiei si L* 15: 9.8 % din
     dalele de 32 px - pe hartie se deosebesc, dar ochiul abia le vede diferenta"; EN the same
     with "of the 32 px patches".
   - The note under the boxes, RO: "E o copie micsorata, cu tonurile ridicate printr-o curba
     simpla (gamma), nu prin stretch-ul din Siril. Pe ecran pare stearsa, dar pe hartie revine.
     Programul presupune ca pixelii sunt sRGB." -> the same, then "Culorile acopera dale de
     32 px, cele pe care le numara tabelul, nu pixeli: un pixel razlet mai intunecat nu se vede
     pe hartie."; EN: "... The colours cover 32 px patches, the ones the table counts, not
     pixels: a stray darker pixel does not show on paper."
   The caption, the `Preview` tooltip and the Tonuri guide speak of "what would come out black
   on paper" and stay.
3. **Tests first** (the rule "Test before fix"), both red before the code moves:
   - `DustAnalyzerTests.R045_...`: the flat cases stay (a 64 x 64 flat raster is four whole
     patches); added: a 64 x 64 raster at 0.19 with one pixel at 0 in every patch is entirely
     `None` (today those four pixels are `UnderPaperBlack`); one whole patch at 0.02 among three
     at 0.19 gives that patch's 1024 pixels `UnderPaperBlack` and `PctUnderPaperBlack` 25; a
     70 x 70 raster leaves its 6 px margins `None`; and on a raster run through
     `AnalysisRun.Execute`, `PctUnderPaperBlack` equals the row's `PctPatchesBelowPaperBlack`.
   - `OverlayWindowTests.R131_...`: the legends read "din dalele de 32 px"; the note names the
     patches.
4. **Docs**: `docs/requirements.md` R-045 and R-131 (per patch, the shares of the patches, the
   margins, the note); ADR 0082 "The preview window's layers class the 32 px patches, not the
   pixels" (context: the measurement above; alternatives: per pixel with a sentence, a smoothed
   pixel class); `docs/design/measurements.md` B6 and `docs/design/window.md`'s
   `PreviewWindowViewModel` row; `README.md`'s **Preview** paragraph ("colour the 32 px patches
   ... the share of the patches"); `CHANGELOG.md` under Changed; this plan saved as
   `docs/plans/2026-11-06-classify-the-preview-by-patch.md`.
5. **Validation** (the rule "Formula changes re-validate": `Analysis/` moved). The preview has
   no figure in `docs/validation.json`, so no drift is expected; the full `/validate` runs
   anyway, with the other large files copied into the worktree first, and the drift table is
   pasted at wrap-up.

## Verification

- `scripts/verify.ps1` green (build with warnings as errors, both test projects, format, the
  design-note index); the golden console texts do not change (the console has no preview).
- `/validate`: every case `OK`, the drift table pasted.
- `APA_DUMP_SCREENS` render of the preview window, or the window run on
  `starless_rbf0.5_stretch0.17.fit` at row 0.20: the red layer is the core of B78 alone, no
  sprinkle, and the legend reads 0.1 % of the patches, the table's figure.

## Record (2026-11-06)

Followed as written, with these deviations:

- `Render` gained a fourth parameter, `asIs`: the review found that a grid row whose target
  equals the background has a gamma of exactly 1, which the simulation measures through its
  lift table and the preview, keyed on the gamma alone, would have measured exactly; the row's
  own flag now picks the path, as the simulation does.
- The review's other items applied: a test of the pixel-to-patch mapping at a reduction step of
  3 (`R045_ThePreviewMapsThePatchesThroughTheReductionStep`), the preview's subsample reused
  for the patches when the two steps coincide, and a guard for an impossible empty grid removed.
- Left to the owner: storing each row's patch L\* on `SimulationRow` (about 87 KB a row) so
  that the preview projects the row instead of recomputing its patches; not done, the
  recomputation being a few tens of milliseconds per preview.
- Validation re-run before the first commit: V-001 to V-015 unchanged (the preview has no
  figure there).

