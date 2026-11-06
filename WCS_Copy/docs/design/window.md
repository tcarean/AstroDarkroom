# The window

**Covers:** `src/CopyWcs.Wpf/`, `tests/CopyWcs.Wpf.Tests/`, `Icon/make_icon.py`.

The behaviour is R-030 to R-038; the reasons are ADR 0007, ADR 0008 and, for the About window's
third-party card and the window's log lines, ADR 0009. The pieces copied from
the siblings (`Presentation/DarkTitleBar.cs`, `Presentation/TExtension.cs`,
`Presentation/Mvvm.cs`, `Presentation/AppIcon.cs`, `Theme/Dark.xaml`, `Views/AboutWindow.*`,
the band at the top of `MainWindow.xaml`, the test fixture `StaFixture`) are marked as such in
their headers and are the siblings' to improve first; `App`, `MainViewModel`, the rest of
`MainWindow` and `AboutViewModel` are this project's own.

## The shape

`MainWindow.xaml` is a grid of two rows. The first is the band, FlatCheck's top bar markup: a
`Border` on `PanelBg` with a `BorderBr` line under it (`BorderThickness="0,0,0,1"`, padding
`20,12`) over three columns, `Banner` in semi-bold over `Copyright` in `Faint`, the explanation
of the tool in `Muted` (wrapped, the window's own pair), and the About button, whose
`MinWidth` of 88 is the Browse buttons', so the two right-hand columns line up. The band is 57
px high in both languages, the explanation in two lines (measured on 2026-10-28 in the tests'
renders, 720 px wide, and in screenshots of the running window at its minimum size), so the
window opens 700 px high instead of 640, with a minimum height of 620 instead of 560 (the
widths stay 800 and 720): the outcome card keeps its room.

The second row is the form, one column with the margin `20,16`: the three file rows (a label,
a `TextBox` bound to the path with `UpdateSourceTrigger=PropertyChanged`, a Browse button, and
under the two inputs the fact line read from the header), the two radio buttons for in place
or a new file with the output row enabled only by the second, the two check boxes, the row of
buttons (`Preview`, `Copy`), and the outcome card. The card is one `Border` of the `Card`
style whose visibility follows `HasOutcome`, holding four blocks that each follow their own
flag: the refusal (the heading `RefusedHeading` in the `BadBr` colour over the core's
`Error`), the warnings (an `ItemsControl` in `WarnBr`), the preview's heading, the cards (a
`ListBox` in the monospace font with a horizontal scroll bar, since a card is 80 characters)
and the closing notes. The built-in `BooleanToVisibilityConverter` does every visibility;
there is no converter of the project's own. Every label is `{l:T En='...', Ro='...'}`,
resolved by `TExtension` when the element is made (its header says why a binding and not a
string). The band's About button runs `ApplicationCommands.Help`, the command F1 runs, and the
window's one `CommandBinding` for it opens `AboutWindow` modally, made anew at every open.

`MainWindow.xaml.cs` owns what needs a window: the two `OpenFileDialog`s and the
`SaveFileDialog` (the FITS filter first, every file second, the folder of the current path
proposed), the About window (`ShowAbout`, the Help command's handler), and
`Start(source, dest)`, which `App` calls with the files from the command line. The handlers of
`Preview` and `Copy` call the view-model and nothing else.

## One view-model, three things

`MainViewModel` (an `ObservableObject` of the copied `Mvvm.cs`) may do three things (ADR 0007):

- **Build a request.** `Source`, `Dest`, `Output`, `WriteInPlace` (its mirror `WritesNewFile`
  serves the second radio button and the output row), `IncludeOptical` and `Force` become a
  `WcsCopyRequest` in `Request(dryRun)`, the three paths through `Clean` (trimmed: a box is
  typed into, and a name with a space in front is not the file meant); `Preview()` runs it
  with the dry run, `Copy()` without. Both go through `RunAsync`: the request is built on the
  caller's thread, the core runs in a `Task.Run` (an in-place copy streams the whole data
  area through a temp file, seconds on a large stack, and the window must stay live), and the
  outcome is shown back on the caller's thread; the window's two handlers are `async void`
  over them. `CanPreview` is "both paths name existing files and no run is in flight",
  `CanCopy` adds "the output path is not blank when a new file is chosen"; both are raised
  whenever a path, the choice or the running state changes, and the buttons bind them, so a
  second click during a run finds the buttons off. The existence of each file is checked once,
  when its path is set (`_sourceExists`, `_destExists`), not in the getters: the boxes update
  on every keystroke. The view-model never guards the commands itself: a run on a missing
  file is the core's refusal, in the core's words.
- **Read two facts of each header.** `Facts(path, source)` is the one reading of a file the
  window does: `FitsHeader.ReadPrimary`, `NAXIS1 x NAXIS2`, and whether `CTYPE1` is present,
  worded as `Source: 100 x 80, plate-solved` / `not plate-solved` and `Destination: 100 x 80`
  / `, already plate-solved`; a file that is missing (not read at all), that the reader refuses,
  or whose header has no `NAXIS1` or `NAXIS2` (the reader's -1, not a size) gives an empty
  string, since the run will say why. The facts are read when a path is set and the
  destination's again after a copy, so an in-place copy shows up as `already plate-solved`.
- **Show the outcome.** `Show(outcome)` keeps the `WcsCopyOutcome` and raises every bound
  property: `Error`, `Warnings`, `Heading` (the outcome's notes when the kind is `Previewed`:
  the core's one heading line), `Cards` (each card with its trailing spaces trimmed, a 0xA0
  kept and a byte above 0x7F shown as the character of its Latin-1 code, ADR 0010, as the console
  prints it without the indent) and `Notes`, the lines under the cards in the console's order:
  the outcome's `Dropped` line when there is one (R-016), then, when the kind is `Copied`, its
  notes, the count and the reminder; a preview's `Notes` is the drop line alone, or empty. The
  `Has...` flags are derived, never stored.

Beside the three, `Banner` and `Copyright` hand the band the core's `VersionInfo.Banner` and
`LicenseInfo.Copyright`, the console's lines 1 and 2, as `AboutViewModel` does: passed
through, not composed. They are properties of the view-model because the core's members are
internal, which an `x:Static` in XAML cannot reach.

## The application

`App` reads the command line once, in its constructor, before `App.xaml`'s resources and the
first view load: `Parse` takes `--lang` out with `LanguageArgument.Extract` (the console's
reader) and then the first two existing files among the other arguments, in their order, so a
`--lang` value is never mistaken for a file and a wrong argument never stops the window from
opening; the result is a `StartArgs` record. The language becomes `Ui.Default` at once; a bad
value is kept and said in an English message box once the window is up. `OnStartup` pins the
invariant culture on the current thread and on `DefaultThreadCurrentCulture` (a WPF
application cannot use `InvariantGlobalization`, FlatCheck's ADR 0037; the facts' figures must
read as the console's), shows the window, and fills the boxes with the two files of
`StartArgs`; `e.Args` is not read again.

Before all of it the constructor opens the diagnostic log and hooks the dispatcher's unhandled
exceptions, and `OnExit` closes the log (`logging.md`); `OnStartup` logs `StartLine(StartArgs)`
and a refused `--lang`. The window's classes take their logger names from `Loggers` (`App`,
`Window`): `MainWindow` logs the file picked in each dialog and About, `MainViewModel.Facts` a
header it could not read, `AboutWindow.OpenLink` a link that would not open; none of them logs a
run, which the core logs itself.

## The theme and the pieces

`Theme/Dark.xaml` is merged once in `App.xaml`: the palette (the siblings' colours, `WarnBr`
amber and `BadBr` red for the two outcome colours), the text styles, the card, the tooltip,
the button, the text field, the check box, the radio button (this project's, drawn after the
check box), the list and the scroll bar, and DarksCheck's `ComponentRowTemplate` (a component's
name and role, its licence and copyright under them in the wrapping `Faint` style, its link),
which the About window's third-party card lists `AboutViewModel.Components` with; the link's
routed `RequestNavigate` reaches the window's `OpenLink`. The About button's tooltip reads
`Version, licence, components (F1)`, the siblings' words. `DarkTitleBar.Apply` is called on
`SourceInitialized` of each window. `AppIcon.Largest` decodes the 256 px frame of `Icon/copywcs.ico` for the About
window's logo; both windows take the same resource through `Icon=`, both executables through
`ApplicationIcon`.

`Icon/make_icon.py` draws every size on its own (the siblings' reason) on a grid of 110 units:
the family's charcoal rounded square, a plate-solved frame with a curved blue grid and six
stars (`celestial_grid`, `STARS`), an arrow to an empty frame; at 24 px and under, the grid
across the square and one bold arrow. Pillow is needed only to run it; the `.ico` is committed.

## The tests

`tests/CopyWcs.Wpf.Tests` targets `net8.0-windows` and links `FitsTestData`, `TempWorkspace`
and `TestData` from `tests/CopyWcs.Tests`; `WindowFixture` writes a solved source and a
processed destination of a given width into a temp folder; `RomanianByDefault` sets the
assembly's language as the console's tests do; `AssemblyInfo` turns parallel collections off.
`MainViewModelTests` run the console in process (`CopyWcsCli.Run`, through the console's
`InternalsVisibleTo`) and compare: the preview's cards with the dry run's lines, the copy's
bytes with the console's on a second fixture, the refusal line with the console's stderr.
`WindowSmokeTests` lay out and render both windows to a bitmap on the `StaFixture` thread
(the window's background drawn first, or the labels on transparency come out dim in a PNG; the
About window at its content's height, measured over two layout passes since the third-party
card's items exist only after the first, as the real window sizes to its content),
fail on any data-binding trace error, and in the English pass walk every `TextBlock` of the
visual tree against a list of Romanian words; `COPYWCS_DUMP_SCREENS=<folder>` saves each
render as a PNG. The R-038 theory finds the band upward from the banner's text block (the
nearest `Border` on `PanelBg`), checks that it starts at the top, spans the width and has the
line under it, and that the copyright, the explanation and the window's single About button
(run by Help, with its tooltip) sit inside it, left to right in that order with About inside
the window, and that Help can run from the button; the two lines are the first two the console
prints at `--help`, in each language. `AboutTests` bind the About view-model to `VersionInfo`
and `LicenseInfo`, check the console prints the same five warranty lines and that the card lists
`ThirdParty.Components`, in both languages; `WindowLoggingTests`, in the `window` collection on
the `StaFixture` thread, check the start line, the facts' Debug line, that a copy from the window
logs what the console's copy logs, that a link the About window cannot open (a relative one) is a
Debug line and leaves the window open, and that two writers through the window's build of
`NLog.Targets.AtomicFile` keep every line (R-045); `AppTests` cover
`Parse`, `--lang` and the files from the arguments; `IconTests` tie the sizes of the `.ico`,
the script's `SIZES` line and the two `ApplicationIcon` paths together. The tests that run the
core await `Preview()` or `Copy()`; the smoke tests that do so run through
`StaFixture.InvokeAsync`, since the outcome comes back on the dispatcher, which a blocking
`Invoke` would hold.
