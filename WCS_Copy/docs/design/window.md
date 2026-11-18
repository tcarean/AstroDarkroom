# The window

**Covers:** `src/CopyWcs.Wpf/`, `tests/CopyWcs.Wpf.Tests/`, `Icon/make_icon.py`.

The behaviour is R-030 to R-039 and R-050 to R-054; the reasons are ADR 0007, ADR 0008, for
the About window's third-party card and the window's log lines ADR 0009, for Help and F1 ADR
0016, for a close during a run ADR 0019, for the paths made whole ADR 0023, for the files
looked at off the window's thread ADR 0025, for the card emptied on a change and during a run
ADR 0026, and for the files looked at again ADR 0027. The pieces
copied from the siblings (`Presentation/DarkTitleBar.cs`, `Presentation/TExtension.cs`,
`Presentation/Mvvm.cs`, `Presentation/AppIcon.cs`, `Theme/Dark.xaml`, `Views/AboutWindow.*`,
the band at the top of `MainWindow.xaml`, the test fixture `StaFixture`) are marked as such in
their headers and are the siblings' to improve first; `App`, `MainViewModel`, the rest of
`MainWindow`, `AboutViewModel`, `Presentation/Manual.cs` and `Presentation/ExternalLink.cs`
(after ImageVisualCompare's) are this project's own.

## The shape

`MainWindow.xaml` is a grid of two rows. The first is the band, FlatCheck's top bar markup: a
`Border` on `PanelBg` with a `BorderBr` line under it (`BorderThickness="0,0,0,1"`, padding
`20,12`) over three columns, `Banner` in semi-bold over `Copyright` in `Faint`, the explanation
of the tool in `Muted` (wrapped, the window's own pair), and a horizontal `StackPanel` with
Help (`HelpButton`: the theme's `HelpIcon` at 16 px, then the word 7 px after it; its
automation name is the word, since its content is not a string) and, 8 px after it, About
(`AboutButton`), whose `MinWidth` of 88 is the Browse buttons', so the right edges line up.
The band is 57 px high in both languages while the explanation takes two lines; beside Help
and About that needs 787 px inside the window, 803 px with its 16 px frame, so the window opens
820 px wide; below 803 px, down to its minimum of 720, the explanation takes three lines and the
band 70 px (measured on 2026-11-16 in both languages, ADR 0016). The band made the window open
700 px high instead of 640, with a minimum height of 620 instead of 560 (2026-10-28): the
outcome card keeps its room. The minimum became 680 on 2026-11-18 (R-055, ADR 0029): at 620 a
forced copy with two warnings, the drop line and the two closing lines left the cards list
29 px, one card; at 680 it is 89 px, four cards, measured by
`R055_AtItsMinimumHeightTheCardsListShowsFourCards` (three cards at 675). Once the window has
its handle, `FitInto` caps its height and its minimum at the work area of its screen and keeps
its top inside it (`ScreenArea.WorkAreaOf` and `WindowFit.Top`, both copied from AstroDarkroom,
ADR 0007); the window stays resizable and its maximum is left alone.

The second row is the form, one column with the margin `20,16`: the three file rows (a label,
a `TextBox` bound to the path with `UpdateSourceTrigger=PropertyChanged`, a Browse button, and
under the two inputs the fact line read from the header), the two radio buttons for in place
or a new file with the output row enabled only by the second, the two check boxes, the row of
buttons (`Preview`, `Copy`), and the outcome card. The card is one `Border` of the `Card`
style whose visibility follows `HasCard` (an outcome, or a run in flight, R-053), holding a
first row that shows `RunningLine` in the `Muted` style while `IsRunning`, alone in the card
then, and four blocks that each follow their own
flag, in the console's order (R-033): the warnings (an `ItemsControl` in `WarnBr`), the refusal
(the heading `RefusedHeading` in the `BadBr` colour over the core's `Error`; above the warnings
until 2026-11-18, CR-017), the preview's heading, the cards (a
`ListBox` in the monospace font with a horizontal scroll bar, since a card is 80 characters)
and the closing notes. The built-in `BooleanToVisibilityConverter` does every visibility;
there is no converter of the project's own. Every label is `{l:T En='...', Ro='...'}`,
resolved by `TExtension` when the element is made (its header says why a binding and not a
string). About's click opens `AboutWindow` modally, made anew at every open. Help's click and F1
open the manual: the window reads F1 without a modifier (`IsHelpKey`) in its `PreviewKeyDown`,
before any control sees it, and takes it, a repeat of a held key included, which then does
nothing; the window binds no command, so `ApplicationCommands.Help`, whose gesture F1 is,
reaches nothing.

`MainWindow.xaml.cs` owns what needs a window: the two `OpenFileDialog`s and the
`SaveFileDialog`, made by `OpenDialog` and `SaveDialog` (the FITS filter first, every file
second, the folder of the current path proposed, and `RestoreDirectory`, so that no dialog moves
the working folder a path typed relative is taken from, R-051), the About window (`ShowAbout`, About's click, through `OpenAbout`, which a test
replaces), the manual (`ShowHelp`, behind
`HelpClick` and `OnPreviewKeyDown`: `Manual.SectionFor` over the view-model's `HasOutcome`,
`HasError` and `HasWarnings`, `Manual.Url`, the log line, then `OpenAddress`, which is
`ExternalLink.Open` unless a test replaced it), `Start(source, dest)`, which `App` calls
with the files from the command line, and a close during a run (R-050, ADR 0019): `OnClosing`
cancels a close while the view-model's `IsRunning` is true, remembers it, logs it once and
turns the window's `IsEnabled` off, so that no dialog is opened under the close, and
`CloseWhenTheRunEnds`, which follows the view-model's `PropertyChanged` from the constructor on,
logs and calls `Close()` when `IsRunning` turns false with a close remembered, so the run ends
whole, its done line logged, before the window and the process go. Every close goes through
`OnClosing` (the X, Alt+F4, the taskbar, a never-shown window's `Close()` in the tests); a
session's end makes WPF ignore the cancel, so a logoff is not held. `OnClosed` ends the window's
watch of the view-model, so that a run ending after the window has gone closes nothing, stops
the view-model from starting looks at the files (`StopLooking`, R-054), so that the run's look
again writes no read line after the window's last one, and logs at Warn a close that came while a run was still in flight, which
only Windows' forced close reaches. The handlers of
`Preview` and `Copy` call the view-model and nothing else; the two Browse handlers of the files
hand the path picked to `PickSource` and `PickDest`, and `LookAgainWhenBackInFront`, on the
window's `Activated`, calls `LookAgain` (R-054).

## One view-model, three things

`MainViewModel` (an `ObservableObject` of the copied `Mvvm.cs`) may do three things (ADR 0007):

- **Build a request.** `Source`, `Dest`, `Output`, `WriteInPlace` (its mirror `WritesNewFile`
  serves the second radio button and the output row), `IncludeOptical` and `Force` become a
  `WcsCopyRequest` in `Request(dryRun)`, the three paths through `Clean` (trimmed: a box is
  typed into, and a name with a space in front is not the file meant; then one pair of double
  quotes around the whole path taken off, the form File Explorer's "Copy as path" pastes, which
  no Windows file name holds; a quote left inside is kept, and the path then names no file,
  R-030), then made whole by `Whole` (R-051, ADR 0023): the source and the destination when
  their texts are set, against the working folder, kept in `_sourcePath` and `_destPath`, which
  the facts read too, so a working folder that moves later changes nothing they name; the new
  file in `Request`, against the folder of `_destPath`, so a bare name goes beside the
  destination. A text the system cannot make whole stays as it is, for the core to refuse; a box
  keeps its text as typed. `Preview()` runs it
  with the dry run, `Copy()` without. Both go through `RunAsync`: the request is built on the
  caller's thread, the run waits for each box's latest look (`LatestLooks`, in the next point),
  so that the read line of each box's latest look stays out of the run's in the log,
  the core runs in a `Task.Run` (an in-place copy streams the whole data area through a
  temp file, seconds on a large stack, and the window must stay live), and the outcome is shown
  back on the caller's thread; the window's two handlers are `async void` over them. The core
  is reached through `RunCore`, `WcsCopy.Run` unless a test puts a run there that waits for
  it, and `IsRunning` is raised as a run starts and again after its outcome is shown, for the
  window's close (R-050). `CanPreview` is "both paths name existing files and no run is in flight",
  `CanCopy` adds "the output path is not blank when a new file is chosen"; both are raised
  whenever a path, the choice, the running state or a look's result changes, and the buttons
  bind them, so a second click during a run finds the buttons off. The view-model never guards
  the commands itself: a run on a missing file is the core's refusal, in the core's words.
- **Read two facts of each header**, off the window's thread (R-052, ADR 0025).
  `Facts(path, source)` is the one reading of a file the window does: `FitsHeader.ReadPrimary`,
  `NAXIS1 x NAXIS2`, and whether `CTYPE1` is present, worded as `Source: 100 x 80,
  plate-solved` / `not plate-solved` and `Destination: 100 x 80` / `, already plate-solved`; a
  file that is missing (not read at all), that the reader refuses, or whose header has no
  `NAXIS1` or `NAXIS2` (the reader's -1, not a size) gives an empty string, since the run will
  say why. A box's setter makes its path whole and calls `LookAt`, which numbers the look as
  the box's latest (`_sourceTurn`, `_destTurn`), empties the box's fact line and turns its
  existence (`_sourceExists`, `_destExists`) off, since the old facts no longer apply, and runs
  `LookCore` in a `Task.Run`: `Look`, which is `File.Exists` and then `Facts`, unless a test
  puts a look there that waits for it. `ShowWhenFound` awaits it back on the caller's thread,
  the window's, and `Found` puts the result under the box only when the look's number is still
  the box's latest, so a look overtaken by a newer text is dropped. Each box's latest look is
  kept in `_sourceLook` and `_destLook`, and `LatestLooks`, both of them, is what a run awaits
  before it starts and after its look again: an earlier look of a box, overtaken by a newer
  text or by a look again of the same one, is not waited for, its result being dropped (on a
  share that does not answer it held a Preview 3 s, the review of the branch on 2026-11-18), so
  its read line, like that of a box changed during a run, may fall
  among the run's. Every look started is kept in `_looks` until it ends, and `FactsRead`, which
  completes once they all have, is what the tests await. `StopLooking`, which the window's
  `OnClosed` calls, starts no look from then on, so the run a close was held for writes no read
  line after the window's last one; a look already in flight, on a slow disk or share, still ends
  and may write its line after it. The existence is checked by the look, not in the getters, which the
  boxes call at every keystroke; a check on the window's thread stopped it on a share that does
  not answer (2.3 s for one letter, measured on 2026-11-18). `LookAgain` looks at both files
  again (R-054, ADR 0027), each line and the commands kept as they are until its look ends: at
  the end of every Preview and Copy, after the outcome is shown, so the lines say what the run
  found and an in-place copy shows up as `already plate-solved`; and when the window comes back
  to the front. A look again asked while a run is in flight is skipped by `LookAt`: the run's
  own look again at its end covers it, and started at once its read lines would fall among the
  run's. `PickSource` and `PickDest`, which the Browse handlers call, set the box's text,
  or look at its file again when the dialog gives back the path the box already holds, which
  the setter, a text equal to the old one, would leave alone.
- **Show the outcome.** `Show(outcome)` keeps the `WcsCopyOutcome` and raises every bound
  property: `Error`, `Warnings`, `Heading` (the outcome's notes when the kind is `Previewed`:
  the core's one heading line), `Cards` (each card with its trailing spaces trimmed, a 0xA0
  kept and a byte above 0x7F shown as the character of its Latin-1 code, ADR 0010, as the console
  prints it without the indent) and `Notes`, the lines under the cards in the console's order:
  the outcome's `Dropped` line when there is one (R-016), then, when the kind is `Copied`, its
  notes, the count and the reminder; a preview's `Notes` is the drop line alone, or empty. The
  `Has...` flags are derived, never stored. `Clear()` takes the outcome away (R-053, ADR 0026)
  when the value of a box's text, of `WriteInPlace` or of either option changes, since the card
  no longer says what a run would do, and as a run starts; a value set to what it already is
  clears nothing (`Set` returns false). `RunningLine` is the window's pair for the run in
  flight, `Preview in progress...` or `Copy in progress...` after `_runningCopy`, empty with no
  run; `RaiseRunning` raises it with `HasCard`, `IsRunning` and the commands as a run starts
  and ends. A box changed during a run does not hide that run's outcome when it comes: a copy's
  lines say a file was written.

Beside the three, `Banner` and `Copyright` hand the band the core's `VersionInfo.Banner` and
`LicenseInfo.Copyright`, the console's lines 1 and 2, as `AboutViewModel` does: passed
through, not composed. They are properties of the view-model because the core's members are
internal, which an `x:Static` in XAML cannot reach.

## The application

`App` reads the command line once, in its constructor, before `App.xaml`'s resources and the
first view load: `Parse` takes `--lang` out with `LanguageArgument.Extract` (the console's
reader) and then the first two existing files among the other arguments, in their order, each
made whole against the folder the window starts in (`Path.GetFullPath`, R-051), so a `--lang`
value is never mistaken for a file, a wrong argument never stops the window from opening, and
the boxes show where the files are; the result is a `StartArgs` record. The language becomes `Ui.Default` at once; a bad
value is kept and said in an English message box once the window is up. `OnStartup` pins the
invariant culture on the current thread and on `DefaultThreadCurrentCulture` (a WPF
application cannot use `InvariantGlobalization`, FlatCheck's ADR 0037; the facts' figures must
read as the console's), shows the window, and fills the boxes with the two files of
`StartArgs`; `e.Args` is not read again.

Before all of it the constructor opens the diagnostic log and hooks the dispatcher's unhandled
exceptions, and `OnExit` closes the log (`logging.md`); `OnStartup` logs `StartLine(StartArgs)`
and a refused `--lang`. The window's classes take their logger names from `Loggers` (`App`,
`Window`): `MainWindow` logs the file picked in each dialog, About and the manual's address,
`MainViewModel.Facts` a header it could not read, `AboutWindow.OpenLink` a link that would not
open, `ExternalLink` an address it refused or the shell could not open; none of them logs a
run, which the core logs itself.

## The theme and the pieces

`Theme/Dark.xaml` is merged once in `App.xaml`: the palette (the siblings' colours, `WarnBr`
amber and `BadBr` red for the two outcome colours), the text styles, the card, the tooltip,
the Help icon (`HelpIcon`, FlatCheck's drawing as AstroDarkroom's `App.xaml` holds it: a disc
of `AccentBr` and a question mark stroked and dotted in `WindowBg`, on 16 units), the button,
the text field, the check box, the radio button (this project's, drawn after the
check box), the list and the scroll bar, and DarksCheck's `ComponentRowTemplate` (a component's
name and role, its licence and copyright under them in the wrapping `Faint` style, its link),
which the About window's third-party card lists `AboutViewModel.Components` with; the link's
routed `RequestNavigate` reaches the window's `OpenLink`. The About button's tooltip reads
`Version, licence, components`, the siblings' words without F1; Help's reads `The manual, at
the section about what is on screen (F1)`, ImageVisualCompare's.
`Presentation/Manual.cs` holds the manual's page and the three anchors GitHub makes from its
headings (`the-window`, `the-result`, `when-something-goes-wrong`): a heading renamed in the
wiki is renamed there, or the browser lands at the top of the page. `SectionFor` gives the
problems' section when the card shows a refusal or a warning, the result's when it shows an
outcome, the window's otherwise: before any run, after a box or an option changed, and while a
run goes, when the card holds no outcome (R-053). `Presentation/ExternalLink.cs` hands the shell (`UseShellExecute`) only an absolute
http or https address (`IsWebAddress`), disposes the process it may get back, and logs at Debug
an address it refused or could not open; the About window keeps its own `OpenLink`, a copied
piece. `DarkTitleBar.Apply` is called on `SourceInitialized` of each window.
`AppIcon.Largest` decodes the 256 px frame of `Icon/copywcs.ico` for the About window's logo;
both windows take the same resource through `Icon=`, both executables through
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
bytes with the console's on a second fixture, the refusal line with the console's stderr. A
test that reads a fact line or `CanPreview` after setting a path awaits `FactsRead` first; the
R-052 tests put `HeldLook` in `LookCore`, which waits for the test on one path, and check that
the setter comes back at once with the line empty and the commands off, that a look overtaken
by a newer text is dropped though it ends last, that a Preview waits for a held look, whose
read line then comes before the run's start line, and that it does not wait for a held look its
box no longer names. `R030_TheCommandsAreOffWhileARunIsInFlight` waits for the looks before the
click, so the commands are off for the run alone. The R-053 tests change each box and option
after a Preview and find the card empty, set each to the value it has and find it kept, and
hold a Copy and a Preview in `RunCore` to read the in-progress line in both languages, the old
outcome gone, and the run's own outcome after it; `WindowSmokeTests` render the card during a
held run, in English with no Romanian word and with no binding error. The R-054 tests make a
file that was missing, or plate-solve one that was not, after its look, then call `PickSource`
and `PickDest` with the paths the boxes hold, `LookAgain` with the look held (the old line and
the commands stay until it is released), or run a Preview, and find the line as the file is now,
and ask a look again during a held run, which starts no look before the run ends;
`WindowLoggingTests.R054_AClosedWindowLooksAtNoFile` closes a window over a held run and finds
no read line after the window's last;
`WindowSmokeTests` raise WPF's own `OnActivated` by reflection, a test window being never shown.
`WindowSmokeTests` lay out and render both windows to a bitmap on the `StaFixture` thread
(the window's background drawn first, or the labels on transparency come out dim in a PNG; the
About window at its content's height, measured over two layout passes since the third-party
card's items exist only after the first, as the real window sizes to its content),
fail on any data-binding trace error, and in the English pass walk every `TextBlock` of the
visual tree against a list of Romanian words; `COPYWCS_DUMP_SCREENS=<folder>` saves each
render as a PNG. The R-038 theory finds the band upward from the banner's text block (the
nearest `Border` on `PanelBg`), checks that it starts at the top, spans the width and has the
line under it, and that the copyright, the explanation, Help (the `HelpIcon` image before the
word, its automation name and tooltip) and the window's single About button (with its tooltip)
sit inside it, left to right in that order with About inside the window, that neither button
runs a command and the window binds none, and that laid out at the width the window opens with
less its frame (`WindowFrame`, 16 px) the band is as tall as at 2000 px, where the explanation
takes one line; the two lines are the first two the console prints at `--help`, in each
language. `HelpTests` pin the three addresses and `SectionFor`, press F1 through the window's
`PreviewKeyDown` (a `KeyEventArgs` with a `PresentationSource` of its own, a repeat marked
through WPF's internal `SetRepeat`, as ImageVisualCompare's test does) on the window and on a
box inside it, and click Help, over a card with nothing, a result, a refusal and a warning,
and during a held run, when the card holds no outcome (R-053),
with `OpenAddress` replaced so no browser starts; a held F1 opens once, F1 with a modifier is
not the Help key, About's click opens About once while F1 does not open it, and
`ExternalLink` refuses a relative, `file:` or `mailto:` address with a Debug line.
`IsHelpKey` is tested on its own; that the key handler hands it the keyboard's modifiers is
not, since a test's key event reads the real keyboard's state.
`AboutTests` bind the About view-model to `VersionInfo` and `LicenseInfo`, check the console
prints the same five warranty lines and that the card lists `ThirdParty.Components`, in both
languages;
`WindowLoggingTests`, in the `window` collection on the `StaFixture` thread, check the start
line, the facts' Debug line, that a copy from the window logs what the console's copy logs,
that a link the About window cannot open (a relative one) is a Debug line and leaves the window
open, that About's click writes `About opened` and F1 `Help opened: <address>` at Info, that a
close held during a run writes its two lines around the run's done line, and
that two writers through the window's build of `NLog.Targets.AtomicFile` keep every line
(R-045); `WindowCloseTests` put a run that waits for the test in `RunCore`, close a window
that was never shown over a Preview and over a Copy into a new file (twice for the copy), and
check that it stays open, greyed, until the run is released, closes once, and leaves the
console's bytes, and close one as WPF does at a session's end (`Window.InternalClose(true,
true)`, reached by reflection), which closes it at once and logs the run cut short (R-050);
`AppTests` cover
`Parse`, `--lang` and the files from the arguments, relative ones made whole;
`WorkingFolder` moves the process's working folder for one test and puts it back, which the
assembly's one test at a time makes safe: the R-051 tests of `MainViewModelTests` type relative
paths and then move it, and read and write the files the paths named when typed, and
`R051_TheFileDialogsPutTheWorkingFolderBack` reads `RestoreDirectory` off the dialogs' factories; `IconTests` tie the sizes of the `.ico`,
the script's `SIZES` line and the two `ApplicationIcon` paths together. The tests that run the
core await `Preview()` or `Copy()`; the smoke tests that do so run through
`StaFixture.InvokeAsync`, since the outcome comes back on the dispatcher, which a blocking
`Invoke` would hold.
