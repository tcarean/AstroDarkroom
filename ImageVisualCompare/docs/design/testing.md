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
> `tests/ImageVisualCompare.Tests/MinimapViewTests.cs`,
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
the dispatcher. `MinimapViewTests` lays the minimap out with `Measure`, `Arrange` and
`UpdateLayout` at the size it gives itself, then checks a click on the thumbnail's corner and
the rectangle at the far edge against the thumbnail inside the border. `MessageDialog`,
`DarkTitleBar`, `FileDialogService` and `MemoryHousekeeping` have no test.

Be aware of what that has meant in practice: both real defects found in this project before
2026-10-21 lived in the untested layer, and neither would have been caught by any reasonable
unit test. One was WPF layout behaviour, the other was build configuration killing text
rendering. Both were found by launching the app and looking at a screenshot. When changing the
WPF layer, run it — a green test suite says little about that code. See ADR 0026.
