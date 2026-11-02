# The window

**Covers:** `src/DarksCheck.Wpf/`, `tests/DarksCheck.Wpf.Tests/`, `Icon/make_icon.py`.

The behaviour is R-070 to R-079, R-120 and R-121; the reasons are ADR 0009 and, for About, ADR
0027. The pieces copied from LightsCheck (`Theme/Dark.xaml`, `Presentation/*.cs` but three,
`Views/AboutWindow.*`, `ViewModels/AboutViewModel.cs`, the test fixture `StaFixture`) are marked
as such in their headers and are LightsCheck's to improve first; `Presentation/ScreenArea.cs` and
About's fit to its screen came with LightsCheck's ADR 0031 (here ADR 0027). Of the three,
`Presentation/AccessibleTabControl.cs` was taken from AstroPhotoAnalyzer with the theme's tab
style it reads, as LightsCheck took both (ADR 0024), `Presentation/TExtension.cs` from FlatCheck
(ADR 0021), and `Presentation/Emphasis.cs` is DarksCheck's own, as are `Theme/Rows.xaml`
(DarksCheck's templates, merging the theme), `MainViewModel`, the views and `WindowTexts`.

## The shape

`MainWindow.xaml` is the top bar (banner, folder, the four settings, the buttons) and one
content grid holding four overlapping states: the empty state (no folder and no problem), the
problem card (`HasProblem`), the `TabControl` (`HasResult`) and the wait screen over everything
(`IsRunning`). Each state is a visibility binding on `MainViewModel`; there is no navigation
code. The six tab views under `Views/` are `UserControl`s with no code of their own beyond
`InitializeComponent`, except `IndexView`, whose four buttons call back into `MainWindow`
(`CopyLabelFromView`, `ChoosePartnerFromView`, `ClearPartnerFromView`, `RerunFromView`),
since the folder dialogs and the clipboard belong to the window. `AboutWindow` is the only
second window.

The tab control is an `AccessibleTabControl`, whose automation peer gives each tab the stock
`TabItemAutomationPeer`'s children and, under the selected tab only, the children of the
presenter the theme's template names `ContentHost`: the selected tab's content, so a screen
reader reaches it (R-079, ADR 0024). The stock peer adds that content only through the stock
name `PART_SelectedContentHost`, which would also move the keyboard focus into a tab on a click
on its header. The theme's tab style targets the class, and the window's local style, the one
that hides the tabs until `HasResult`, is based on that type's style.

`App`'s constructor starts the diagnostic log, before the resources load, and hooks the window
thread's unhandled exceptions to it; `OnExit` closes it (ADR 0025). The view-model numbers its
runs and writes each one's options and outcome, the window its dialogs and buttons, and
`UserSettings`, `DarkTitleBar`, `AboutWindow` and `ScreenArea` the failures they swallow; the
lines and the two logger names of `Loggers` are in `logging.md`.

## About

`AboutViewModel` turns `ThirdParty.Components` into rows, in the core's order, and takes its
headings from `WindowTexts`. The lead above the rows is the core's, `ThirdParty.Lead`: it says
how many components there are as a word, which `ThirdPartyTests` holds to the list's length, and
names the two places of the licence texts (R-120).

The window is no taller than the work area of the main window's screen (R-121):
`MainWindow.ShowAbout` passes `ScreenArea.WorkAreaOf(this)` and the constructor sets it as
`MaxHeight`. `WorkAreaOf` asks Windows for the screen nearest the window (`MonitorFromWindow`,
`GetMonitorInfoW`, through `LibraryImport` as `DarkTitleBar`) and turns its work area into the
window's DIP with `ToDips`, each edge divided by the window's DPI scale on its own axis; without
a handle, or without an answer, it gives the primary screen's `SystemParameters.WorkArea`, the
latter with a Debug line. The cards are in a
`ScrollViewer` named `Cards`, transparent so the wheel scrolls over the gaps too, and not
focusable; the Close button sits in a second grid row, so it stays in view whatever the cards
do. At `ContentRendered`, when a window sized to its content has its final height (at `Loaded`
its bindings are not resolved yet, as AstroPhotoAnalyzer measured), `FitInto` keeps the top
inside the area (`ScreenArea.TopInside`) and writes its Debug line. `PreviewKeyDown` gives Up,
Down, Page Up, Page Down, Home and End to the cards only while they can scroll (`Scroll`), so
where the window fits the arrows still move the focus between the links. The cards' panel
(`24,20,24,0`) and the button (`24,16,24,20`) add up to the old margins where the window fits.
The heights measured at four work areas are in ADR 0027.

## One view-model, fed by the core

`MainViewModel` has every property the XAML binds, the window's own sentences included
(`WindowTexts`, which quote every threshold they name from `Th`, so a tooltip never says a
number the core no longer uses). Its constructor reads `UserSettings.Load` into the four
setting texts and the partner; the three numeric fields carry `*IsValid` flags that
`Theme/Rows.xaml`'s `SettingField` style turns into a red border and a tooltip while the text
is not a positive number. `OpenAsync(folder)` cancels a run in flight,
clears the result, builds `DarkOptions` from the texts (`Options`: a text that is not a
positive number gives the core's default, `ParseInt` / `ParseDouble` with the invariant
culture) and runs `DarkSeriesRun.Run` on `Task.Run` with a `Progress<RunProgress>` whose
callback lands on the UI thread and sets `Progress` and `BusyMessage` (the phase word is the
core's). `ConfigureAwait(true)` brings `Populate` back to the UI thread; a run whose token was
cancelled returns before `Populate`, so a stopped run leaves nothing and `Status` says so. The
exception types the core throws for a bad folder, and the `AggregateException` a failure
inside a parallel loop arrives wrapped in (flattened to its distinct messages, as the console
does), become `ProblemText`; anything else is a bug and propagates. A superseded run's
progress callbacks are dropped once its token is cancelled, so they never write over the new
run's bar. `RerunAsync` opens the same folder again, with the settings and the partner as
they are now.

`Populate(DarkSeriesResult)` arranges, it does not compute:

- `Report` is `RunReport.WriteBanner` plus `DarkReport.Write` into a `StringWriter`: the
  console's text;
- the SESIUNE card takes `InventoryReport.HeaderRows` and `DarkReport.HeaderRows`, with
  `DarkReport.HeaderLine` as its subtitle; every other card
  takes a `ReportSection` of `DarkReport.Sections` as a `SectionVm` of `RowVm(Label, Value,
  Rest, Verdict)`, the chip being `Verdict` and the text `Rest`, both read in the core
  (`ReportRow`); `HotRows`, `LightRows` and `IndexRows` are the rows of the sections whose
  title starts with `5.`, `9.` and `INDEX`, for the tabs that show one check beside a chart;
- the shared `ReportRow` template draws the rest of a row's text through `Emphasis`, an
  attached property in `Presentation/` that rebuilds the `TextBlock`'s inlines from `Rest` and
  `RowVm.Bold` (`Emphasis.Segments`: every occurrence of every bold substring that stands as a figure of its
  own, longest first, never overlapping; the rebuild is deferred to the end of the binding
  pass so the two properties cost one) as plain and bold runs (ADR 0016);
- the tab `Explicatii scara ADU` (`ScaleView`) binds `ScaleRows` (`ScaleReport.Rows` as
  `RowVm`, the `ReportRow` template) and `ScaleParagraphs` (`ScaleReport.Paragraphs`, one
  wrapped `TextBlock` each) under `ScaleTitle`, the core's title;
- the two tables take `DarkReport.FrameRows` and `LightRows` as `FrameRowVm` / `LightRowVm`,
  the console's cells as they are, with the unit note of the table header (`[pixelii verzi G1; ...]`, `[G1; ...]` for the lights) as
  the last column's header; a dark row's `Off` is set when the core gave it a `Note` (a frame
  off the plateau, R-023), and `FramesView` then tints the row (`FrameRowGrid`, the note as
  its tooltip), colours the `platou` cell bold (`PlateauCell`) and shows the note on a second
  row of the row's grid, under the cells and spanning them, all in the attention colour of
  `VerdictPalette.Warn` as local brushes;
- the Harti tab takes the three `BinnedMap`s of the reference plane through
  `DarkOutputs.MapRaster` and `RasterBitmaps.Grey`, each with the section-2 row of the same
  name as its note; a map without blocks, or an absent group, is a `MapVm` without image and
  the card says so;
- the charts take `ChartPoint(X, Y, Label, Colour, Tip)` lists: the warming (every dark's
  `AmpRatioMedian`, the frames of `Warming.OffPlateauFrames` in the attention colour, guide
  at 1 and, with a fit, at `Plateau`, `OffPlateauLow` and `OffPlateauHigh`; the card's
  sentence is `DarkReport.WarmingChartNote`, as `WarmingNote`), the residual sigma (`Residuals[rp].Sigma`, the outlier
  frames of `NoiseVerdict.OutlierFrames` in the attention colour, guide at
  `NoiseVerdict.OutlierLimitAdu`), the random banding (`ResidualFigures.RowBandingRatio`,
  guide at 1), the noise curve (`NoiseVsN.Curve` measured as points, expected as the dashed
  `Reference`, each point's tip the two figures) and the lights' ratio (`AmpRatioMedian` per
  light, coloured by `IndexAnalyzer.Judge` through `VerdictPalette.For(MatchVerdict)`, the
  five bounds of `Th.Match*` as guides); a frame without a figure is `NaN` and the chart
  leaves a gap;
- `Label` and `Tag` are the result's; `Partner` is the folder of `--against`.

The problem card has a second button, `Alege partener...` (R-077): `MainWindow.ChoosePartnerAndRerun`
runs the partner dialog and, when a folder was chosen (the same one again included), `RerunAsync`;
a cancelled dialog does nothing. So a session with lights and no darks goes from the refusal
to its run without choosing the folder again. `Export(dir)` calls `DarkOutputs.WriteAll`, which refuses a folder inside the session or the
partner as `--out` does, and shows the paths or the error in `Status`. `LabelCopied` sets the
status line after the window put the label in the clipboard.

## The settings (`UserSettings`)

`StoredSettings(Threads, MaxFrames, IsoFactor, Doubling, Partner)`, five strings as typed or
chosen, in `%LOCALAPPDATA%\DarksCheck\settings.json`, written on every change through
`System.Text.Json` with the path unescaped; a missing or unreadable file gives nulls, a failed
write is swallowed, and both leave a Debug line with their exception in the diagnostic log; the
class judges nothing. `UserSettings.Path` lets the tests point at a
file of their own, and `TestSettings` does so for the whole run before any test
(`ModuleInitializer`); a test that reads the file back takes `TestSettings.Own`, a fresh file
restored on dispose.

## Drawing (`Charts.cs`)

`SeriesChart` is LightsCheck's: a `FrameworkElement` that draws in `OnRender`, a line with
dots or bars over x, five y ticks, guide lines with labels on a dark backing, a `Minimum`, and
one owned `ToolTip` opened on hover over the point whose slot holds the cursor (`PointAt`).
DarksCheck adds `Reference`, a second series drawn first as a dashed line without dots; the
axes span both series.

## Language

`App`'s constructor sets `Ui.Default` from the command line (`App.LanguageFromArgs`, over
`LanguageArgument.Extract`; `docs/design/session-and-console.md`, "The interface language")
before `App.xaml`'s resources and the first view load, since both carry texts; a value it does
not understand is kept and said in an English `MessageBox` once the main window is shown, and
the window goes on in English. `FolderFromArgs` looks only at the arguments left once `--lang`
and its value are out. A XAML label is a `{l:T En='...', Ro='...'}` pair (`TExtension`, taken
from FlatCheck): on a dependency property or a `Setter` it returns a one-time binding, so a
template parsed once picks its wording when each element is made; elsewhere it returns the
wording at once. The view-model's sentences (`WindowTexts`, the chart captions) are `T(...)`
pairs read as properties. The verdict chips colour either language's words
(`VerdictPalette.Severity`). The window still finds the maps' rows by the labels `master dark`,
`master bias` and `dark - bias` and the sections by their `5.`, `9.` and `INDEX` prefixes,
which the report keeps the same in both languages. `LanguageWindowTests` render every tab in an
English scope and look for Romanian words, and the Cadre tab in both languages for the method's
words of R-083, the list `PlainWords.cs` that the core's tests share, linked like the fixtures.

## Culture, title bar, icon

`App.ForceInvariantNumbers` sets the default and current cultures to the invariant one at
start, for the UI thread and the pool threads; a WPF project cannot use
`InvariantGlobalization` (FlatCheck's ADR 0037), and `CultureTests` keep the report identical
to the console's under `ro-RO`. `DarkTitleBar` asks DWM for the dark title bar through one
`LibraryImport`. `AppIcon.Largest` picks the 256 px frame of the one `.ico` for the About
window; `IconTests` tie both executables' `ApplicationIcon` to `Icon/darkscheck.ico`.
The window project carries its own `runtimeconfig.template.json` with the same
`System.GC.ConserveMemory=9` knob as the console's (ADR 0007), so the decoded rasters are
collected as eagerly under the window as under `darkscheck.exe`. `Icon/make_icon.py` draws the
icon per size with Pillow (a charcoal frame with a faint pixel
grid and amber hot pixels, the strongest with a halo; the small sizes drop the grid and draw
fewer, larger dots) and saves the 256 px frame as the base image, since Pillow drops every
size above it.

## The tests

`WindowSmokeTests` builds each view with a real result from `WindowFixture` (six darks with
the lattice hot pixels, three biases, four lights at 1.3 of the darks, a partner library at
2), lays it out, renders it to a bitmap and fails on any binding error traced by
`PresentationTraceSources`; with `DARKSCHECK_DUMP_SCREENS=<folder>` each render is saved as a
PNG. `StaFixture` is the one STA thread with a `Dispatcher.Run` loop the view-model's awaits
need; the assembly disables test parallelization. `MainViewModelTests` cover R-070 to R-076
without a window, the report tab against `Program.Run`'s text. `AutomationTreeTests` (R-079)
select each tab of a window over the same session and ask the automation peers in process: the
tab's content under its item, once, and under no other tab's item. Keyboard focus needs a
window on screen, which no test shows, so the second test makes WPF's own lookup of the stock
presenter name in the applied template and finds nothing. `WindowLoggingTests` cover the
window's log lines over the view-model, and the two-writer check on the window's own build of
the atomic file target (`logging.md`, "In the tests"). `AboutTests` hold About's rows to the
core's list and its lead to the core's; `AboutWindowFitTests` (R-121, LightsCheck's) lay About's
content out at the height a capped window leaves it, raise the keys through a stand-in
presentation source, call the window's `OnContentRendered` for the fit, and test `TopInside`,
`ToDips` at 125 % and 150 % and Windows' answer apart.
