# Testing

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Testing". Where a paragraph became a
> decision record, its number is given beside it.
> **Covers:** `tests/ImageVisualCompare.Tests/TempImages.cs`,
> `tests/ImageVisualCompare.Tests/FitsFixture.cs`,
> `tests/ImageVisualCompare.Tests/FitsImageLoaderTests.cs`,
> `tests/ImageVisualCompare.Tests/WicImageLoaderTests.cs`,
> `tests/ImageVisualCompare.Tests/ImagePairLoaderTests.cs`,
> `tests/ImageVisualCompare.Tests/WpfThread.cs`,
> `tests/ImageVisualCompare.Tests/MainWindowTests.cs`,
> `tests/ImageVisualCompare.Tests/DisplayRendererTests.cs`,
> `tests/ImageVisualCompare.Tests/LevelLookupTests.cs`,
> `tests/ImageVisualCompare.Tests/RainbowTableTests.cs`,
> `tests/ImageVisualCompare.Tests/MinimapViewTests.cs`,
> `tests/ImageVisualCompare.Tests/AboutWindowTests.cs`,
> `tests/ImageVisualCompare.Tests/Win32Resources.cs`,
> and the WPF files it names, `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`,
> `src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs`,
> `src/ImageVisualCompare.Wpf/Interop/DarkTitleBar.cs`,
> `src/ImageVisualCompare.Wpf/Services/FileDialogService.cs`,
> `src/ImageVisualCompare.Wpf/Imaging/MemoryHousekeeping.cs`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## Testing

142 cases (on 2026-10-02; `RepoInvariantTests` and `ValidationTests` were added the same day). `dotnet test ImageVisualCompare.sln`.

Covered: everything in Core, and the imaging layer. `WicImageLoaderTests` generates fixtures
at run time in six pixel formats plus TIFF via `TempImages`, so the real codecs are exercised
without binary files in the repository; the float32 TIFFs of R-090 and R-091 are written byte
by byte (`TempImages.WriteFloatTiff`), laid out as libtiff writes the 32-bit exports of Siril
and GraXpert, since WPF's own encoder writes float sources as 16-bit integers. FITS files are
built in memory, card by card, by `FitsFixture` (`tests/ImageVisualCompare.Tests/FitsFixture.cs`):
the Core tests read them from a `MemoryStream`, and `FitsImageLoaderTests` writes them to disk
with `TempImages.WriteBytes`, whole or cut short. `ImagePairLoaderTests` covers valid,
mismatched, missing and corrupt inputs, the two failures WIC reports with types of its own (ADR
0031: a PNG whose header claims 40000 x 30000, from `TempImages.WritePngWithHeaderSize`, and a
WebP with a garbage bitstream, from `WriteDamagedWebP`), progress reporting (recorded on the
reporting thread: a
`Progress<T>` posts its callbacks, and an assertion after the load raced them), and — with a
recording fake — that a
mismatched pair never reaches a decode, whether it was refused outright or declined at the
prompt.

The WPF layer is mostly untested: event wiring, mouse capture, property assignment, and the
interop that darkens a caption. Every number those controls put on screen comes from Core and
has a test. Since 2026-10-22 a defect a window or a control shows without being shown has a
test of its own (ADR 0032). `WpfThread` starts one STA thread for the run, creates the real
`App` there and calls its `InitializeComponent`, so `Theme.xaml`'s resources resolve, never
calls `Run`, and posts each test body to that thread's dispatcher; an exception in the body
fails the test. `MainWindowTests` builds the main window there with dialogs that count the
asking and a loader whose decode never ends, and clicks Open twice while the body still holds
the dispatcher; the same dialogs pick `c.png` for one image, so the change of one image
(R-065) is driven through `ChangeImageAsync` and read back from the names, the title, the
divider and the other image's `Source`, which has to be the same bitmap object. For the display modes (R-113, R-114) it uses a loader that decodes a 2 × 2
bitmap at once and records the mode of every load: it clicks Open, waits between pumps of the
dispatcher until the pair line of the status bar is filled, moves the divider, calls
`ToggleModeAsync` and reads the buttons, the status bar and the divider back; the keys `H` and
`F` themselves are checked by launching the application, since a `KeyEventArgs` needs a
presentation source a window never shown does not have. `WindowLoggingTests` drives the main
window the same way and reads its lines, and the surface's, in the diagnostic log
(`logging.md`).

`WpfThread` never calls `Run`, but WPF's `Application` constructor queues the application's start,
which runs once the thread's dispatcher pumps (the note of 2026-10-30 on ADR 0032): `App.OnStartup`
reads the test host's arguments, and `StartupUri` opens a real `MainWindow`, shown and maximised,
whose start opens the real Open dialog; the tests' bodies run on inside that dialog's message
loop, for as long as the test host lives.

The display modes' arithmetic is Core's: `LevelLookupTests` pins Siril's equalisation on
hand-computed histograms (R-111's two examples, the rounding, a uniform histogram, the share
per level), `RainbowTableTests` the ten colours R-112 names and the inverse the readout uses,
`DisplayRendererTests` the three modes over 16-bit rows, including the mean layer of an RGB
image. `FitsImageLoaderTests` and `WicImageLoaderTests` then load fixtures in each mode and
compare bytes, and `ImagePairLoaderTests` checks the fallback to linear for a pair one file of
which is not shown as stored. `MinimapViewTests` lays the minimap out with `Measure`, `Arrange` and
`UpdateLayout` at the size it gives itself, then checks a click on the thumbnail's corner and
the rectangle at the far edge against the thumbnail inside the border. `UiTextTests` builds the
main window in Romanian there and reads its toolbar and hint; it and the other Romanian tests run
alone, in a collection with parallelisation off (`language.md`). `MessageDialog`, `DarkTitleBar`,
`FileDialogService` and `MemoryHousekeeping.ReleaseImageBuffers` have no test, nor has the main
window's message about an unknown `--lang` (a modal dialog, checked by launching); the start-up
that sets the language and keeps that value, `App.ApplyCommandLine`, has one. Of the diagnostic
log, `Program.Main`, `LogSetup.Apply` and `LogSetup.Close` with their real file and their hooks,
the lines a window writes around a modal dialog (a failed open, a declined padding, About), and the
repeats of `Ctrl+O` and of the zoom keys are checked by launching (`logging.md`); a test cannot make
a `KeyEventArgs` a repeat, so `MainWindowTests` gives a held `H` to `OnModeKeyAsync`, which takes
the repeat as an argument (R-114, ADR 0044). `AboutWindowTests` reads the About window's view
model against `VersionInfo`, `UiText` and `ThirdParty`, and builds the window on the WPF test
thread, never shown, then lays its content out at the client width, which builds the card's rows
from their template: the body that builds it returns before its bindings take their values, in
the dispatcher's data-binding pass, and so do the rows' after the layout, so later bodies read
the title, the logo, Close's Enter and Esc, the links and the row's text, as `MainWindowTests`
waits for a pair (ADR 0045). `Win32Resources`
reads one resource of a type out of a built file, loaded as data, for `AppIconTests` (the icon
group) and `AppManifestTests` (the manifest).

Be aware of what that has meant in practice: both real defects found in this project before
2026-10-21 lived in the untested layer, and neither would have been caught by any reasonable
unit test. One was WPF layout behaviour, the other was build configuration killing text
rendering. Both were found by launching the app and looking at a screenshot. When changing the
WPF layer, run it — a green test suite says little about that code. See ADR 0026.
