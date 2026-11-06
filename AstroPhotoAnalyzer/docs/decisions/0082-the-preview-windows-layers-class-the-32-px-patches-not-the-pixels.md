# 0082. The preview window's layers class the 32 px patches, not the pixels

Date: 2026-11-06
Status: accepted 2026-11-06
Supersedes: 0025 in part (the preview's classes per pixel)

## Context

The preview window of a simulation row (R-131) paints two layers over the image lifted to the
row's target: red where "under paper black", amber where "under L\* 15" but over black, each with
a legend giving the share (R-045, ADR 0025). Since the window was added the class was each
pixel's own, from its L\* on the reduced, lifted copy, while the simulation table counts 32 px
patches (R-044: the L\* of a patch's mean luminance, so that noise and specks do not move it).

On 2026-11-06 the owner reported that on a row listed with the dust over paper black the red
layer still showed zones, and suspected the classes were computed on the original image. Measured
the same day in the task's worktree on the plate-solved FITS (`starless_rbf0.5_stretch0.17.fit`,
baryta, Dmax 2.4, paper black L\* 3.60), a scratch test rendering every row:

| row | dust L\* | table: patches under black | preview: pixels under black | of them in a patch over black |
| --- | --- | --- | --- | --- |
| 0.12 | 4.1 | 1.70 % | 2.61 % | 1.14 % |
| 0.172 (as is) | 7.9 | 0.19 % | 0.50 % | 0.29 % |
| 0.20 | 10.5 | 0.10 % | 0.28 % | 0.17 % |

- The classes were on the lifted copy: the preview's "L\* < 15" share equalled the row's pixel
  column at every row, and its share under black tracked the row's "pure black" column.
- At row 0.20 the red was 0.28 % of the pixels sprinkled over 7.3 % of the frame (cells of
  32 x 32 preview pixels holding at least one red pixel; the red filled 3.4 % of those cells):
  one dense blob, the core of Barnard 78 (L\* 1.4 at that row), and single-pixel noise over the
  clouds whose patches sat at L\* 4 to 5, just over black. Half-transparent and at screen scale,
  the sprinkle read as a large reddish zone; the owner saw "zone foarte mari".
- The same on `de_printat.png` (row 0.20: 0.04 % of the patches, 0.10 % of the pixels).

The owner chose the patches: "Classify by patch".

## Decision

`PrintPreview.Render` classes the 32 px patches of the simulation's grid, not the pixels: the
patches' L\* at the row's lift (`DustAnalyzer.PatchLstars` through the row's `LiftTable`, the
computation `PrintStretchSimulator.Simulate` runs for the row; exact on the "as is" row, as
there, which the row's `AsIs` flag says, since a grid row can have a gamma of 1 too) against
paper black and `LowSeparationLstar`; every reduced pixel takes its patch's class
by the map's geometry, and the pixels past the last whole patch, at the right and the bottom,
take none, as they belong to no patch of the table either. The two shares are shares of the
patches, so the red legend's figure is the row's "pure black" column to the last decimal; the
amber legend's is a patch share too, which the table's "L\* < 15" column (a pixel share, R-041)
is not. The legends say "din dalele de 32 px" / "of the 32 px patches", and the note under the
boxes says the colours cover the patches the table counts, not pixels, a stray darker pixel not
showing on paper; the patch side is interpolated from `Thresholds.DustPatchPx`.

## Alternatives rejected

- **Per pixel, with a sentence saying the stray pixels are noise.** Nothing computed changes, but
  the sprinkle stays, and with it the impression of zones the table does not count.
- **A smoothed pixel class** (the pixel's L\* after a small blur). Removes the sprinkle but
  invents a third measurement, neither the table's nor the pixel's, with a radius to justify.
- **Per pixel for the amber layer, per patch for the red.** The two layers would then not be
  of one kind, and the amber sprinkle at the threshold would stay.
- **Changing the definition of the dust** (the 25 % level of the dust patches, ADR 0033) so that
  "dust over black" means no patch under black. The dark cores are under black for real; the
  figure is right, the preview now shows exactly which patches it counts.

## Consequences

- R-045 and R-131 say the classes are the patches', the shares shares of the patches, and the
  margins unclassed. `R045_ThePreviewClassesAreThePatches` covers a stray pixel, a whole dark
  patch, the margins and the equality with the row's column on a whole run;
  `R131_ThePreviewLiftsToTheRowAndPaintsTheTwoClasses` the legends and the note.
- `DustAnalyzer.Grid` is internal, for the preview's geometry.
- The note under the boxes moved from `PreviewWindow.xaml` into the view-model (`Note`), since
  the patch side is a number and a view-model text interpolates it (ADR 0020).
- The README's **Preview** paragraph and `docs/design/measurements.md` B6 say the patches.
- `/validate` re-run on 2026-11-06: the preview has no figure in `docs/validation.json`; no case
  moved.
