# 0008. The window shows the core's report rows and computes nothing; its tests are a second project

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The owner asked for a WPF window "like FlatCheck": the same licence, versioning, dark theme,
About window and general layout, a table of how often the dithering happened, a chart or table
of how much, the list of frames to drop with reasons, and the inputs a raw does not carry
(the pixel size) with the D3300's values. The plan (step 4) copies FlatCheck's WPF pieces
rather than sharing a library (decision 1 of the plan). The console already prints every
sentence and every figure (R-027), and the rule "Computed only in the core" forbids a second
place that decides numbers or words.

## Decision

- The window is a second application over `Runs/` only. `SeriesReport` produces its sections
  as rows (`ReportRow`: label, value, an optional verdict word) and `Write` prints those same
  rows; the window binds the rows as cards and shows the verdict word as a coloured chip.
  Nothing is reworded, rounded or summed in the view-model: the Raport tab holds the console's
  text byte for byte, and the export writes the same three files as `--out` (R-032). What the
  window adds is arrangement: the charts over the night, the dither map, the frames table, and
  the star cut-outs of the selected frame, which are cut in the core (`StarCrops`) from the
  measured star list and only painted in the window (R-031).
- The inputs the files do not carry are two fields in the top bar, the pixel size (the D3300's
  3.92 um) and the thread count (half the cores when blank), parsed with the invariant culture
  (R-029). The export asks for a folder each time, proposing the session root; nothing is
  ever written under `lights/` or `biases/` (ADR 0002).
- The window's tests are a project of their own, `tests/LightsCheck.Wpf.Tests`, because they
  must target `net9.0-windows` to load WPF while `LightsCheck.Tests` stays portable; they link
  the session fixture and the synthetic frames from `LightsCheck.Tests` rather than copying
  them. `RepoInvariantTests` therefore counts the `R###_` tests of the other projects under
  `tests/` by reading their sources, since it cannot reference a Windows-only assembly.
- Three deviations from the kit's strictness, each local: the view-models silence CA1822
  (a bound property must be an instance member even when it forwards a constant); the window
  project allows unsafe blocks for the one `LibraryImport` that darkens the title bar; the
  window tests silence CA1305 for the linked FITS fixture, which is clean in `LightsCheck.Tests`
  only because that project's `InvariantGlobalization` switch tells the analyzer the culture is
  fixed, a switch a WPF project cannot have (FlatCheck's ADR 0037: WPF cannot bind without
  culture data, so `App.ForceInvariantNumbers` pins the number culture by hand instead).

## Alternatives rejected

- **A shared WPF library with FlatCheck.** Decision 1 of the plan: the two windows show
  different things and would drift; the copied pieces (theme, converters, palette, title bar,
  MVVM base, charts) are small and marked as copies.
- **The window formatting its own figures from the analysis records.** It would be a second
  place deciding decimals and words, and the Raport tab could disagree with the console.
- **One test project with a `net9.0-windows` target.** The core's tests, the goldens and the
  validation cases would then need Windows to run at all.
- **Writing the export beside the raws by default.** ADR 0002: the session folder is read
  only; the user chooses where the files go.

## Consequences

`WindowSmokeTests` renders every tab with a real result and fails on any binding error; with
`LIGHTSCHECK_DUMP_SCREENS=<folder>` it also saves a PNG per screen, so the window can be
looked at without starting it. `CultureTests` proves the report is identical under a Romanian
culture. `IconTests` ties both executables to the one icon file. The design note
`docs/design/window.md` holds the mechanics.
