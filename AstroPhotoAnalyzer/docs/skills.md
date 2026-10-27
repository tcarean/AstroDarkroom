# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018; ADR 0002). What AstroPhotoAnalyzer needs on top of them is here, one section per
skill; each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a console requirement also states the exit code (`docs/requirements.md` has the
  console contract block); a window requirement names the tab and the rows it adds.
- At step 2: the core and the console are tested in `AstroPhotoAnalyzer.Tests`, the window in
  `AstroPhotoAnalyzer.Wpf.Tests`. A change to the report or to an option also gets a golden
  scenario in `GoldenOutputTests` on a scene of `Scenes.cs` (`docs/design/console.md`); a new tab
  a line in `WindowSmokeTests`; a figure measured on the reference image a field in a case of
  `docs/validation.json`, as the skill says.
- At step 3: measure on the reference image before you design (`docs/validation.md` says where
  it is). On this project the first size rule, the first edge-region rule and the first star
  threshold were all wrong until the real image was run (ADR 0016, ADR 0017, ADR 0018).
- At step 5: `AstroPhotoAnalyzer.Core` first (`Analysis/` decides numbers, `Report/` decides
  words, `Runs/` chains them), then the console and the window, which only call `Runs/` (the rule
  "Computed only in the core"). `docs/design/extending.md` is the checklist per kind of change: a
  measurement, a window text, a recommendation, a parameter, a medium, an input format, a tab, a
  dependency.
- At step 6: numeric code is `Input/`, `Analysis/`, `Report/`, `Parameters.cs` and
  `Thresholds.cs` (the rule "Formula changes re-validate"). The golden files are regenerated with
  `APA_UPDATE_GOLDEN=1` (the rule "Golden files", the kit's ADR 0033).
- At step 8: `README.md` is in English (ADR 0050); its "The console" section lists every
  console flag and its "The window" section every tab.

## /wrap-up

- At step 2: numeric code is `AstroPhotoAnalyzer.Core/Input/`, `Analysis/`, `Report/`,
  `Parameters.cs` and `Thresholds.cs` (the rule "Formula changes re-validate").

## /validate

- At step 1: the reference images are `de_printat.png` (V-001 to V-003 and V-005), a git-ignored copy in
  `AstroPhotoAnalyzer.Tests/testdata/large/` of the owner's file in `E:\Photografie\Claude`
  (ADR 0004), and `starless_rbf0.5_stretch0.17.fit` (V-004), the plate-solved FITS of ADR 0025,
  copied from the folder `testdata/manifest.json` names, and the lab's two ICC profiles (V-006,
  V-007; ADR 0030), from the page the manifest names, and the other frames the manifest lists
  (the Rosetta, Orion and Andromeda pairs with and without stars, the Veil, the 0.1 stretch);
  the manifest holds every hash. A
  worktree has no copies: copy the images into the worktree's `testdata/large/` first, or every
  case prints `NOT MEASURED`. A case runs the whole analysis on a 23 MP image, 10 to 15 s
  (the twelve cases together about 2.5 min; before ADR 0035 a case took up to 27 s). The
  filter `Category=Validation` also runs `StarPairTests` (ADR 0054): every starless frame under
  `StarMinCount` stars, every frame with stars kept, the Rosetta pair's kept stars in the
  difference image; they print each frame's count and FWHM.
- At step 3: the table's second column is the case's parameters, not a fixture; the figures are
  the ones `ValidationTests` prints on its `MEASURED` line.

## /release

- At step 3: ask for `/validate` whether or not a formula moved; a release is the moment a stale
  figure on the reference image gets noticed.
- At step 5: the console is published as the single copyable `AstroPhotoAnalyzer.exe` under
  `console/` and the window under `wpf/`; the tag is `astrophotoanalyzer-vX.Y.Z`.
- At step 7: the README's "Building" section names the publish commands; `artifacts/` is what
  they produce.

## /mutate

- At step 1: pass a file filter on this project; `scripts/mutate.ps1 -Mutate "**/StarAnalyzer.cs"`
  is the shape. Stryker runs the whole suite of the core's test project (243 tests on 2026-10-02)
  once per mutant, so a whole-core run is long; no run has been measured yet, so write the first
  one's time here.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `THIRD-PARTY-NOTICES.md` and the
golden texts under `AstroPhotoAnalyzer.Tests/Golden/`, the console's expected output, not code)
