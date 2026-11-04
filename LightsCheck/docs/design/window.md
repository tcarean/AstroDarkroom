# The window

**Covers:** `src/LightsCheck.Wpf/`, `src/LightsCheck.Core/Analysis/StarCrops.cs`, `tests/LightsCheck.Wpf.Tests/`, `Icon/make_icon.py`.

The behaviour is R-029 to R-032; the reasons are ADR 0008. The pieces copied from FlatCheck
(`Theme/Dark.xaml`, `Presentation/*.cs` but one) are FlatCheck's to improve first (the same rule
as the readers, ADR 0003); `Charts.cs` and the view-models are LightsCheck's own, and the one,
`Presentation/AccessibleTabControl.cs`, is AstroPhotoAnalyzer's class in LightsCheck's style
(ADR 0028).

## The shape

`MainWindow.xaml` is the top bar (banner, folder, the four inputs, the buttons) and one content
grid holding four overlapping states: the empty state (no folder and no problem), the problem
card (`HasProblem`), the tabs (`HasResult`) and the wait screen over everything
(`IsRunning`). Each state is a visibility binding on `MainViewModel`; there is no navigation
code. Five of the six tabs are `UserControl`s under `Views/` with no code of their own beyond
`InitializeComponent`, the sixth the report's text box in a card; `AboutWindow` is the only
second window.

Each of the four inputs is a group (`PixelInput`, `ThreadsInput`, `EastInput`, `NorthInput`, its
label and its field) whose tooltip is an `x:Static` of `MainViewModel`'s static tip properties
(R-083). They are read once, when the window is built, after the start-up has set the language;
`ToolTipService.ShowOnDisabled` keeps them while a run disables the fields.

`AboutWindow` fits its screen (R-076, ADR 0031), as AstroPhotoAnalyzer's does. `MainWindow`
passes the work area of its own screen, `ScreenArea.WorkAreaOf`. That asks Windows for the screen
nearest the window (`MonitorFromWindow`, `GetMonitorInfoW`, through `LibraryImport`) and divides
the answer by the window's DPI scale on each axis (`ToDips`). It falls back to
`SystemParameters.WorkArea`: silently when the window has no handle, with a Debug line when
Windows gives no answer. The constructor caps `MaxHeight` at the area's height.
The window is a two-row grid:

- The cards, the logo block included, are in the `ScrollViewer` `Cards`, transparent so the wheel
  scrolls anywhere over it.
- `CloseButton` is in the second row, its margins giving the old gaps, 16 DIP above and 20 below.

`PreviewKeyDown` passes Up, Down, Page Up, Page Down, Home and End to `Cards`, only while
`ScrollableHeight` is above 0 (`Scroll`). Elsewhere the arrows move the focus, as before.
`ContentRendered`, when the window's height is final, calls `FitInto`. That clamps `Top` with
`ScreenArea.TopInside`, the launcher's `WindowFit.Top`, and writes the Debug line `About shown
WxH at (L,T); work area WxH at (L,T); cards scroll: yes|no`. `AboutWindowFitTests` lays the
content out at a capped height without showing a window, raises the keys through a stand-in
presentation source, and calls `OnContentRendered` by reflection.

The tabs are an `AccessibleTabControl` (R-067, ADR 0028). The theme's template for it names the
presenter of the selected content `ContentHost`, and the control's automation peer adds that
presenter's children, the selected tab's view, under the tab's item, which the stock peer does
only through the stock name `PART_SelectedContentHost`; that name would also move the keyboard
focus into a tab on a click on its header. The theme's style targets the class, and the
window's own style of the tabs, the `HasResult` trigger, is based on it: an implicit style
applies to its exact type only.

## One view-model, fed by the core

`MainViewModel` has every property the XAML binds. `OpenAsync(folder)` cancels a run in
flight, clears the result, then runs `SeriesRun.Run` on `Task.Run` with a `Progress<T>` whose
callback lands on the UI thread (the `Progress<T>` is created on it) and sets `Progress` and
`BusyMessage`, only while its run is still the current one (`IsCurrent(cts)`, the same check the
`catch` and the `finally` make): the frames in flight when a run is stopped or replaced still
report, and their posts may sit in the dispatcher's queue when the next run starts.
`ConfigureAwait(true)` brings `Populate` back to the UI thread; a run whose
token was cancelled returns before `Populate` so a stopped run leaves nothing. The three
exception types the core throws for a bad folder become `ProblemText`; anything else is a
bug and propagates. Each run's start and outcome go to the diagnostic log, and a bug's exception
is written there by an exception filter that returns false, before it leaves in the task nobody
awaits; the window's log lines and the two logger names of `Loggers` are in `logging.md`.

`Populate(SeriesResult)` arranges, it does not compute:

- the section cards take `SeriesReport.SummaryRows(r)` and the other row producers as
  `RowVm(Label, Value, Verdict)`; the session card takes the lines `InventoryReport.WriteHeader`
  prints (split on `" : "`) plus `SeriesReport.ScaleLine`;
- the frames table takes `SeriesReport.FrameRows(r)`, the console's table cells already
  formatted (the run cell as `Spot`), plus the frame's path for the cut-outs, and its `dX` /
  `dY` headers from `SeriesReport.PositionHeaders` (`DxHeader`, `DyHeader`); `Rejected` is
  the frames `SeriesReport.RejectedRows` names, `RejectedHeading` is
  `SeriesReport.RejectedHeading` and `RejectedExplanation` maps
  `SeriesReport.RejectedExplanation` to `ParagraphVm`s, which `RejectedView` lists one per
  line with a `DataTrigger` on `Emphasis` for the bold one;
- the Dithering tab's card takes `SeriesReport.DitherScalarRows` as `DitherRows` and
  `SeriesReport.DitherTables` as `TableVm`s; `DitherView` lays a table out as its title, a
  header row and the rows, each row an `ItemsControl` with a horizontal panel of fixed-width
  `TextBlock` cells, no grid control and no column logic; under the tables it lists
  `DitherExplanation`, `SeriesReport.DitherExplanation` as `ParagraphVm`s with the same
  `DataTrigger` on `Emphasis` as the Rejected tab (R-047);
- the charts take `ChartPoint(X, Y, Label, Colour)` lists: the map from the registration's
  `Dx`/`Dy`, the steps and nearest distances from `DitherAnalysis`, the four quality series
  from `FrameMeasurement`; a frame without a figure is `NaN` and the chart leaves a gap. The
  guide lines quote `Th` (the move threshold, the 10 px coverage radius, the two elongation
  classes) so a change there moves the lines; a map point whose frame sits in a run at 10 px
  (`DitherAnalysis.SpotRun`) carries `Ring`, the map's axis labels are
  `SeriesReport.AxisLabels`, and each step point carries the table row's `StepTip` as its
  `Tip`; the frames table shows the row's `Move` as the `dithering` column;

The window's sentences built in code are `WindowTexts`, each a pair of English and Romanian; its
fixed labels are `{local:T}` pairs in the XAML (`language.md`, which also covers the start-up's
`--lang`). `WindowTexts` holds the empty and
problem cards, the wait line (`Busy`, `BusyRegistering`), the dialog titles, the status after
an export, the cut-out notes, the step chart's guide labels (`MoveGuide`, `WalkingNoiseGuide`,
given the core's formatted figure) and the map's grid note (`GridNote`). The About window's lead
above the third-party list is the core's `ThirdParty.Lead`, beside the list it counts (R-075),
and its rows are `ThirdParty.Components`. The class words on
the elongation guides are `SeriesReport.ClassWord`, the session card's `scale` label is
`SeriesReport.ScaleLabel`, and the `ERROR: ` / `EROARE: ` line of an export error or a failed cut-out decode
is `RunReport.Error`, the console's own, so a verdict word or the prefix changes in `Report/`
alone (ADR 0020).

`Report` is `RunReport.WriteBanner` plus `SeriesReport.Write` into a `StringWriter`: the
console's text. `Export(dir)` calls `SeriesOutputs.WriteAll` and shows the paths in `Status`;
`WriteAll` hands it each file as soon as the file is written, for the log's `wrote` line.

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
(`LoadCropsAsync` checks the selected row before publishing the cut-outs and before publishing
an error alike: a stale decode that fails writes nothing).

## Drawing (`Charts.cs`)

Two `FrameworkElement`s that draw in `OnRender`, no chart library. `SeriesChart` is a line or
bar chart over the frame index with optional guide lines and a minimum; `DitherMap` is the
positions on a 10 px grid (50 px once the span passes 400 px), equal scales on both axes, each
point coloured by its class, joined
to the next and numbered, a `Ring` point drawn as a hollow circle in the same colour. Both
redraw when `Points` changes (the dependency properties call
`InvalidateVisual`) and draw nothing under a minimum size. `SeriesChart` keeps its last
layout (`_xMin`, `_xMax`, `_plotW`, the points drawn) and owns one `ToolTip` placed at the
mouse: on `OnMouseMove` it finds the point whose slot holds the cursor (`PointAt`) and, when
that point changes, closes the tip and reopens it with the point's `Tip` (none for a point
without one), closing it on leave. It opens and closes the tip itself because a tooltip the
framework opens shows once per entry into the element and then stays shut.

## The one stored setting (`UserSettings`)

The SynScan arrow keys (R-041, ADR 0014) are the window's only setting that outlives a run:
`UserSettings.Load` reads `%LOCALAPPDATA%\LightsCheck\settings.json` (two strings, `east` and
`north`, written as the symbols themselves; nulls on a missing or unreadable file) and `Save`
writes it through `System.Text.Json`, swallowing IO errors; the class judges nothing.
`MainViewModel` loads them in its constructor into `EastArrow` / `NorthArrow`, keeping a
stored string only when `ArrowKeys.FromSymbol` says it is an arrow of that axis and taking
`ArrowKeys.Default`'s symbol otherwise, offers `EastChoices` / `NorthChoices` built from the
core's default and its derived opposite, saves on every change, and `Options` turns the
symbols back into `ArrowKeys`, so the symbols and the default live in the core alone.
`UserSettings.Path` lets the tests point at a file of their own, and `TestSettings` in the
test project does so for the whole run before any test, so no test touches the developer's
file. The pixel size and the thread count stay per run, as before.

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
without a window. `AutomationTreeTests` ask the automation peers in process, the same ones a
client in another process is answered by: every tab's content under its item, once, and under
no other tab's item; and nothing in the tabs' applied template by the stock name, a lookup,
since keyboard focus needs a window on screen.
