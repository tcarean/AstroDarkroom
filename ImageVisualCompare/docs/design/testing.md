# Testing

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Testing". Where a paragraph became a
> decision record, its number is given beside it.
> **Covers:** `tests/ImageVisualCompare.Tests/TempImages.cs`,
> `tests/ImageVisualCompare.Tests/FitsFixture.cs`,
> `tests/ImageVisualCompare.Tests/FitsImageLoaderTests.cs`,
> `tests/ImageVisualCompare.Tests/WicImageLoaderTests.cs`,
> `tests/ImageVisualCompare.Tests/ImagePairLoaderTests.cs`,
> and the untested files it names, `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`,
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
without binary files in the repository. FITS files are built in memory, card by card, by
`FitsFixture` (`tests/ImageVisualCompare.Tests/FitsFixture.cs`): the Core tests read them from a
`MemoryStream`, and `FitsImageLoaderTests` writes them to disk with `TempImages.WriteBytes`,
whole or cut short. `ImagePairLoaderTests` covers valid, mismatched,
missing and corrupt inputs, the two failures WIC reports with types of its own (ADR 0031: a PNG
whose header claims 40000 x 30000, from `TempImages.WritePngWithHeaderSize`, and a WebP with a
garbage bitstream, from `WriteDamagedWebP`), progress reporting (recorded on the reporting thread: a
`Progress<T>` posts its callbacks, and an assertion after the load raced them), and — with a
recording fake — that a
mismatched pair never reaches a decode, whether it was refused outright or declined at the
prompt.

What remains untested is now genuinely untestable without UI automation: event wiring, mouse
capture, property assignment, and the interop that darkens a caption. Every number those controls put on screen comes from Core
and has a test. `MainWindow`, `MessageDialog`, `DarkTitleBar`,
`FileDialogService` and `MemoryHousekeeping` have none.

Be aware of what that has meant in practice: both real defects found in this project lived in
the untested layer, and neither would have been caught by any reasonable unit test. One was
WPF layout behaviour, the other was build configuration killing text rendering. Both were
found by launching the app and looking at a screenshot. When changing the WPF layer, run it —
a green test suite says nothing about that code. See ADR 0026.
