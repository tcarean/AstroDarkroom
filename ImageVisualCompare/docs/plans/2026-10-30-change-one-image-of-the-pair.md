# Change one image of the pair, the other kept

On `claude/Change-one-image-of-the-pair`, approved 2026-10-30; the approval is also the owner's
yes to ADR 0047 (`docs/decisions/0047-one-image-of-the-pair-is-changed-in-place.md`).

## Context

The owner asked (2026-10-30) for a button on each side that changes only that image, the other
staying as it is; `Open…` keeps opening a whole pair. The requirement is accepted as R-009 (the
loader: only the new file probed and decoded, the kept image never read again, the padding
question with the kept image on its side, one display mode for both, the kept file decoded again
only when the mode falls to `Linear`) and R-065 (the buttons `Change A…` / `Change B…` in the
toolbar beside each name, one dialog in the folder of the file replaced, the view kept on an
unchanged canvas, cancel / declined / unreadable leave the pair as it was). The failing tests are
written: `ImagePairLoaderTests.R009_*` (8), `MainWindowTests.R065_*` (4) and the two new buttons
in the `R063_` theory, the Romanian wordings in `UiTextTests`; they do not compile yet
(`ImageSide`, `ReplaceAsync`, `PickImage`, the buttons).

## Steps

1. **Core.** `src/ImageVisualCompare.Core/ImageSide.cs`: `enum ImageSide { A, B }`.
   `UiText.cs`: `ChangeA` / `ChangeB` (`Change A…` / `Schimbă A…`, `Change B…` / `Schimbă B…`),
   `ChangeImage(ImageSide)` and `SelectImage(ImageSide)` returning the existing members.
   Verified by `UiTextTests` (R-101: every property worded in both languages).
2. **Dialogs.** `Services/FileDialogService.cs`: `string? PickImage(ImageSide side, string?
   initialDirectory = null)` on `IFileDialogService`, implemented with the existing private
   `Pick` and `UiText.Current.SelectImage(side)`; `PickImagePair` calls it for its two dialogs.
3. **Loader.** `Imaging/ImagePairLoader.cs`: `ReplaceAsync(ImagePair current, ImageSide side,
   string path, progress, confirmPadding, mode, cancellationToken)`. Probe the new file; pad plan
   from the kept image's `Info` and the probe, in A/B order; the padding question / refusal /
   declined as `LoadAsync` does (the three branches pulled into a private helper shared by both
   methods, so the wording and the log lines stay one); mode fallback as `LoadAsync` does (also a
   shared helper); decode the new file (progress 0 with its name); when `kept.Mode != mode`
   decode `kept.Info.Path` again in `mode` (progress 0.5 with its name); progress 1; build the
   pair in the right order. `RunClock` named `replace`, start line `A=… B=… changes=A|B mode=…`,
   done line as the load's. Same `catch` blocks (cancelled, expected failure).
   Verified by `R009_*` in `ImagePairLoaderTests`.
4. **Window.** `MainWindow.xaml`: the toolbar's `Grid` gets two columns (`*`, `Auto`); the left
   column holds `PairPanel`, a horizontal `StackPanel`: `BeforeName` (TextBlock, `ToolLabel`
   style, `TextBrush`, `MaxWidth` 360, `CharacterEllipsis`), `ChangeBeforeButton` (`ToolButton`,
   `IsEnabled=False`), `VsLabel` (`vs`, `ToolLabel`), `AfterName`, `ChangeAfterButton`; the right
   `Toolbar` panel in column 1 unchanged. `ToolbarTitle` goes. `MainWindow.xaml.cs`:
   - constructor: the two buttons' `Content` from `UiText`, `VsLabel.Text = "vs"`, clicks to
     `ChangeImageAsync(ImageSide.A, "the Change A button")`; the focus-back handler also on
     `PairPanel` (R-063).
   - `internal async Task ChangeImageAsync(ImageSide side, string from)`: refused with a Debug
     line while `_opening`, or with no pair; else `_opening = true`; `Log.Info("change of <side>
     asked by …")`; `_dialogs.PickImage(side, folder of the file replaced)`; cancelled → Info line,
     return; for A set `_lastDirectory`; `ReplaceWithOverlayAsync` (the overlay code of
     `LoadWithOverlayAsync` generalised over the load call); success → `_selection` with the new
     path, `ShowPair(pair, keepView: same canvas size)`; failure → the R-005 dialog; declined →
     Info line. `finally _opening = false`.
   - `ShowPair`: `BeforeName.Text` / `AfterName.Text` instead of `ToolbarTitle`, the two buttons
     enabled; the `keepView` doc says a change on an unchanged canvas keeps the view too.
   Verified by `R065_*` and the `R063_` theory in `MainWindowTests`, `R101_TheMainWindow_InRomanian`.
5. **Gate and the application.** `scripts/verify.ps1` green; then the application launched on the
   real pair (`E:\Photografie\Claude\de_printat.png` and the PRINT preview), `Change B…` to
   `de_printat_50x35_TIFF8.tif` (the padding question, the view kept), `Change A…`, cancel, a
   screenshot looked at, the log read (`change of B asked by the Change B button`, `replace …`,
   `shown … view=kept`, `exit 0`, no `ERROR`). Only when the owner is not using the PC.
6. **Record.** R-009 and R-065 `implemented`; R-008 (toolbar: the names with a button after
   each), R-060 (the toolbar list), R-063 (the two buttons hand the focus back) amended; ADR 0047
   `accepted 2026-10-30`; `docs/design/shell.md` (a section on the change beside "One open at a
   time", the log lines), `imaging.md` (`ReplaceAsync` in the loader's section), `logging.md`
   (the new lines in the table), `code-map.md` (the two files' lines), `testing.md`
   (`MainWindowTests`' new tests); `README.md`: the controls table row `change one image`, "What
   the window shows" (the toolbar's left part), "Starting it" (cancelling the one dialog);
   `CHANGELOG.md` Added; this plan saved; the memory note. `/wrap-up`: commit on the branch,
   merge proposed.

## Not done

- No keyboard shortcut for a change (ADR 0047); no drag and drop.
- No new validation case: the change decodes one file through the same loaders the cases of
  `docs/validation.json` already measure.
