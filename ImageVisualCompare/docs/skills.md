# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018; ADR 0002). What ImageVisualCompare needs on top of them is here, one section per
skill; each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a requirement about the surface names the input (wheel, key, drag, button) and
  the unit it is measured in (physical pixels, DIPs or image pixels: the rule "Physical pixels
  below, device-independent pixels above"); one about a file names what the status bar shows.
- At step 2: every test lives in `tests/ImageVisualCompare.Tests`. A number the surface puts on
  screen gets its test on the Core record that computes it (`RenderPlan`, `DividerLayout`,
  `MinimapLayout`, `ScrollBarState`), not on the control (the rule "Core has no UI"). A format
  or a loader change gets a fixture from `TempImages`, never a binary file. A figure measured
  on the real pair is a field in a case of `docs/validation.json`.
- At step 5: `ImageVisualCompare.Core` first, then the control that assigns the record. A
  change in `Controls/` or `MainWindow` is not done until the application was launched on the
  real pair and a screenshot looked at (the rule "Run the application"; ADR 0026).
- At step 6: numeric code is `src/ImageVisualCompare.Core/` and `src/ImageVisualCompare.Wpf/Imaging/`.
- At step 8: `README.md` is in Romanian, with diacritics; its "Controale" table lists every
  input and its "Ce îți arată bara de stare" section every readout.

## /wrap-up

- At step 2: numeric code is `src/ImageVisualCompare.Core/` and
  `src/ImageVisualCompare.Wpf/Imaging/` (a memory figure that moved is a finding).
- At step 9: a change under `src/ImageVisualCompare.Wpf/` also launches the application once,
  on the real pair, before the commit (the rule "Run the application").

## /validate

- At step 1: the files are `de_printat.png`, `de_printat_PRINT_preview_16bit.tif` and
  `de_printat_50x35_TIFF8.tif`, git-ignored copies in `tests/testdata/large/` of the owner's
  files in `E:\Photografie\Claude` (ADR 0005); `tests/testdata/manifest.json` holds their
  hashes. A worktree has no copy: copy the three files into the worktree's
  `tests/testdata/large/` first, or every case prints `NOT MEASURED`. A case probes and decodes a 23 MP pair, a few
  seconds.
- At step 3: the table's second column is the case's two files, not a fixture; `decode.seconds`
  is printed and never guarded; the memory figures are megabytes with a 60 MB tolerance, so a
  drift there is a machine before it is a regression.

## /release

- At step 5: the application is published self-contained for win-x64 into `artifacts/X.Y.Z/`
  itself; the tag is `imagevisualcompare-vX.Y.Z`.
- At step 7: the README's "Compilare" section names the build and test commands; `artifacts/`
  is what a release produces.

## /mutate

- At step 1: Core is small (eleven files; 149 tests in the gate and the two validation cases on
  2026-10-02), so a whole-core run is reasonable; `scripts/mutate.ps1 -Mutate "**/Viewport.cs"` is the shape for one file. No run
  has been measured yet; write the first one's time and score here.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `README.md`, the user
documentation, which states behaviour and is not code)
