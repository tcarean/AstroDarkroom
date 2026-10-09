# The window

**Covers:** `src/LightsCheck.Wpf/`, `src/LightsCheck.Core/Analysis/StarCrops.cs`, `tests/LightsCheck.Wpf.Tests/`, `Icon/make_icon.py`.

The behaviour is R-029 to R-032; the reasons are ADR 0008. The pieces copied from FlatCheck
(`Theme/Dark.xaml`, `Presentation/*.cs`) are marked as such in their headers and are
FlatCheck's to improve first (the same rule as the readers, ADR 0003); `Charts.cs` and the
view-models are LightsCheck's own.

## The shape

`MainWindow.xaml` is the top bar (banner, folder, the two inputs, the buttons) and one content
grid holding four overlapping states: the empty state (no folder and no problem), the problem
card (`HasProblem`), the `TabControl` (`HasResult`) and the wait screen over everything
(`IsRunning`). Each state is a visibility binding on `MainViewModel`; there is no navigation
code. The six tabs are `UserControl`s under `Views/` with no code of their own beyond
`InitializeComponent`; `AboutWindow` is the only second window.

## One view-model, fed by the core

`MainViewModel` has every property the XAML binds. `OpenAsync(folder)` cancels a run in
flight, clears the result, then runs `SeriesRun.Run` on `Task.Run` with a `Progress<T>` whose
callback lands on the UI thread (the `Progress<T>` is created on it) and sets `Progress` and
`BusyMessage`. `ConfigureAwait(true)` brings `Populate` back to the UI thread; a run whose
token was cancelled returns before `Populate` so a stopped run leaves nothing. The three
exception types the core throws for a bad folder become `ProblemText`; anything else is a
bug and propagates.

`Populate(SeriesResult)` arranges, it does not compute:

- the section cards take `SeriesReport.SummaryRows(r)` and the other row producers as
  `RowVm(Label, Value, Verdict)`; the session card takes the lines `InventoryReport.WriteHeader`
  prints (split on `" : "`) plus `SeriesReport.ScaleLine`;
- the frames table takes `SeriesReport.FrameRows(r)`, the console's table cells already
  formatted (the run cell as `Spot`), plus the frame's path for the cut-outs; `Rejected` is
  the frames `SeriesReport.RejectedRows` names, `RejectedHeading` is
  `SeriesReport.RejectedHeading` and `RejectedExplanation` passes
  `SeriesReport.RejectedExplanation` through for the paragraph under the heading;
- the charts take `ChartPoint(X, Y, Label, Colour)` lists: the map from the registration's
  `Dx`/`Dy`, the steps and nearest distances from `DitherAnalysis`, the four quality series
  from `FrameMeasurement`; a frame without a figure is `NaN` and the chart leaves a gap. The
  guide lines quote `Th` (the move threshold, the 10 px coverage radius, the two elongation
  classes) so a change there moves the lines; a map point whose frame sits in a run at 10 px
  (`DitherAnalysis.SpotRun`) carries `Ring`;

`Report` is `RunReport.WriteBanner` plus `SeriesReport.Write` into a `StringWriter`: the
console's text. `Export(dir)` calls `SeriesOutputs.WriteAll` and shows the paths in `Status`.

## The star cut-outs (`StarCrops`)

In the core, because they choose which stars to show. `Build(path, m)` decodes the frame again
(the measured frames are not kept: 87 frames x 400 MB), interpolates the green channel as the
analyzer did, and picks from `m.Shapes` by flux: the two brightest within the central two
fifths, the brightest in each corner quarter. Each pick is a `(2 x Radius + 1)` square (25 px)
stretched from its own 5th percentile to its own maximum into bytes, with the position word
and the `StarShape`; `SeriesReport.CropCaption` writes the caption (the words stay in
`Report/`), and `RasterBitmaps.Grey` turns the bytes into a frozen `Gray8` bitmap in the
window, shown 5x with nearest-neighbour scaling so the pixels stay square. Whatever the
decoder throws becomes the note of that frame, caught by the same net `SeriesRun` casts per
frame. `CropsPending` is the decode in flight; a selection made while it runs wins
(`LoadCropsAsync` checks the selected row before publishing).

## Drawing (`Charts.cs`)

Two `FrameworkElement`s that draw in `OnRender`, no chart library. `SeriesChart` is a line or
bar chart over the frame index with optional guide lines and a minimum; `DitherMap` is the
positions on a 10 px grid, equal scales on both axes, each point coloured by its class, joined
to the next and numbered, a `Ring` point drawn as a hollow circle in the same colour. Both
redraw when `Points` changes (the dependency properties call
`InvalidateVisual`) and draw nothing under a minimum size.

## Culture, title bar, icon

`App.ForceInvariantNumbers` sets the default and current cultures to the invariant one at
start, for the UI thread and the pool threads; a WPF project cannot use
`InvariantGlobalization` (FlatCheck's ADR 0037), and `CultureTests` keep the report identical
to the console's under `ro-RO`. `DarkTitleBar` asks DWM for the dark title bar through one
`LibraryImport`. `AppIcon.Largest` picks the 256 px frame of the one `.ico` for the About
window; `IconTests` ties both executables' `ApplicationIcon` to `Icon/lightscheck.ico`.

## The tests

`WindowSmokeTests` builds each view with a real result from `SessionFixture.Pattern`, lays it
out, renders it to a bitmap and fails on any binding error traced by
`PresentationTraceSources`; with `LIGHTSCHECK_DUMP_SCREENS=<folder>` each render is saved as a
PNG. `StaFixture` is the one STA thread with a `Dispatcher.Run` loop the view-models' awaits
need; the assembly disables test parallelization. `MainViewModelTests` cover R-029 to R-032
without a window.
