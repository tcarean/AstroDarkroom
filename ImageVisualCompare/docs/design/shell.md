# Shell: the culture trap and the dark chrome

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Invariants" (two of its ten). Where
> a paragraph became a decision record, its number is given beside it.
> **Covers:** `Directory.Build.props`,
> `src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs`,
> `src/ImageVisualCompare.Wpf/AboutWindow.xaml`,
> `src/ImageVisualCompare.Wpf/AboutWindow.xaml.cs`,
> `src/ImageVisualCompare.Wpf/AboutViewModel.cs`,
> `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`,
> `src/ImageVisualCompare.Wpf/Interop/DarkTitleBar.cs`,
> `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`,
> `src/ImageVisualCompare.Wpf/Theme.xaml`,
> `src/ImageVisualCompare.Wpf/Assets/make-icon.ps1`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## Invariants

These are two of the ten invariants of the former `CLAUDE.md`; the list's introduction and
seven others are in `rendering.md`, one in `imaging.md`. What each file of the shell is for
(`MainWindow`, `MessageDialog`, `App.xaml.cs`, `FileDialogService`, `DarkTitleBar`,
`Theme.xaml`) is in `code-map.md`.

**Never set `InvariantGlobalization`.** WPF's font cache constructs `CultureInfo("en")` while
computing typography availability; in invariant mode that throws `CultureNotFoundException`
and takes the process down during text layout. It was set in `Directory.Build.props` for two
phases and only crashed once a Consolas run appeared in the status bar. No unit test catches
this — the app has to be launched. See ADR 0017.

**No `MessageBox`, and every window opts into a dark caption.** `MessageBox.Show` opens a
Win32 window that cannot be themed at all — white background, black text — and on a tool for
judging dark images that is a bright rectangle over the picture being compared, not a
cosmetic complaint. `MessageDialog` replaces it. The caption is a separate problem: it belongs
to the desktop window manager, WPF cannot reach it and does not follow the system theme
either, so each window asks DWM for it through `DarkTitleBar.Apply` — in the constructor, so
the attribute is set before the first frame and the caption is never white even briefly. On
Windows 11 the caption is set to the exact chrome colour; Windows 10 has no such attribute and
gets the generic dark caption, which is close but not `#1B1F26`. See ADR 0018.

## One open at a time

`MainWindow.OpenPairAsync` runs a whole open: the file dialogs, the load behind the overlay,
then the pair on screen or the message, and the dialogs again after a failure. The loading
overlay is a `Border` over the window and stops only the mouse; the keyboard still reaches the
window, so Ctrl+O, or Space on the focused `Open…` button, would start a second
`OpenPairAsync` while the first awaits its decode, and two pairs would decode at once. The
field `_opening`, set for the length of a call, makes a second call return at once (R-006,
ADR 0036).
Both ways in, `OnPreviewKeyDown` and the button's `Click`, go through it. `MainWindowTests`
clicks the button twice on the WPF test thread, with dialogs and a loader of its own given
through the internal constructor (ADR 0032).

A switch of display mode (ADR 0040) is the same kind of load and goes through the same field:
`ToggleModeAsync`, reached from the two `ToggleButton`s of the toolbar and from `H` and `F` in
`OnPreviewKeyDown`, returns at once while `_opening` is set, with no pair shown, or on a pair
whose files are not both shown as stored (`ImagePair.AllowsDisplayModes`). Otherwise it loads
the files of the pair on screen again (`_selection`, kept from the last open) through
`LoadWithOverlayAsync` with the mode asked for and the padding answered yes, and
`ShowPair(pair, keepView: true)` hands the new bitmaps to `CompareView.ReplacePair` instead of
`SetPair`; for a switch as for an open, the outgoing pair is released after one render pass
has put the incoming one on screen (`rendering.md`). `ToggleModeAsync` keeps only the canvas's
size across its await, never the pair: a local holding the pair is hoisted into the state machine
and keeps the outgoing bitmaps alive through that release, which then frees nothing (measured on
2026-10-30, ADR 0046). `_mode` is always what the pair says it was
rendered in, so a pair the loader fell back to linear on leaves both toggles unchecked and
disabled; the pair line of the status bar ends with the mode's name, and the readout gives, in
false colours, the level `RainbowTable.LevelOf` finds for the pixel's colour. The two toggles
have their own style, `ToolToggleButton` in `Theme.xaml`, a copy of `ToolButton`'s template
with an `IsChecked` trigger, since a `ToggleButton` is not a `Button` and cannot be based on
one.

A change of one image (R-065, ADR 0046) is the third kind of load through the same field.
`ChangeImageAsync(side, from)`, reached from `ChangeBeforeButton` and `ChangeAfterButton`, the
two buttons after the file names in `PairPanel`, the left column of the toolbar's grid, returns at
once while `_opening` is set or with no pair shown. Otherwise it asks `IFileDialogService.PickImage`
for one file, in the folder of the file it replaces, and hands the pair on screen, the side and the
file to `ImagePairLoader.ReplaceAsync` through `LoadWithOverlayAsync`, with
`ConfirmPaddingAsync` for the question and `_mode` for the mode, as an open does; the loader
keeps the other image's `LoadedImage` and decodes the new file alone (`imaging.md`). The pair is
read inside `ReplaceShownAsync`, a method of its own, and `ChangeImageAsync` keeps only the
canvas's size: a local holding the pair would be hoisted into the async method's state machine,
live through `ShowPair`, and the release there would find the outgoing bitmap referenced and free
nothing (measured on 2026-10-30, ADR 0046). On success
`_selection` takes the new path, so a later switch of mode decodes the pair now on screen, and
`ShowPair` keeps the view when the canvas has the same size as before and fits otherwise; `ShowPair`
also writes the two names into `BeforeName` and `AfterName` and enables the two buttons, disabled
in the XAML until the first pair. The buttons use `ChangeButton` of `Theme.xaml`, `ToolButton` with
a `ContentTemplate` that puts the open-file glyph of Segoe MDL2 Assets before the content string,
so the content stays the string the tests read and UI Automation names the button by; the
template's `ContentPresenter` takes the style's `Padding` as its margin, in `ToolToggleButton`
too, which no template did before. A cancelled dialog, a declined padding or a failure (the R-005
dialog, no second file dialog) leave the pair as it was. A change of A moves `_lastDirectory`, so
`Open…` follows the image A chosen last (R-001). `LoadWithOverlayAsync` takes the load as a
function of the overlay's progress, the one place the overlay goes up and down for an open, a
switch and a change alike. `MainWindowTests` changes B and A with dialogs that pick `c.png` for one
image, and reads the names, the title, the divider and the other image's `Source`, the same bitmap
object, back; it also cancels the one dialog and clicks the buttons before a pair and while one
loads.

## The focus after a toolbar click

Every toolbar button, the two toggles and the two change buttons included, is a `ButtonBase` and
focusable, so a click takes the keyboard focus from the comparison surface, and the surface's
shortcuts (R-063) would reach the button instead. `MainWindow` adds one handler for
`ButtonBase.ClickEvent` on each of the toolbar's two panels, `PairPanel` and `Toolbar`; the event
bubbles, so the handler runs after the clicked button's own, and it calls `Compare.Focus()`. By then `About`'s window has closed and
`Open…`'s file dialogs have been answered; a load that follows, an open or a mode switch,
focuses the surface once more when the pair is shown (`ShowPair`). The buttons stay focusable:
`Tab` still reaches them, and the dialog buttons share the style (ADR 0042). `MainWindowTests`
clicks each button but `About` on the WPF test thread and reads the window's logical focus, the
part of `Focus()` that works without a window handle (ADR 0032).

## In the diagnostic log

The main window's constructor writes how WPF renders, its tier and the process's render mode.
`OpenPairAsync` takes what asked for the open, `the start`, `the Open button` or `Ctrl+O`, for its
first line, and writes one asked for while another runs as ignored; it writes what the dialogs
chose or that they were cancelled, and, after a load, that a message is shown or that the padding
was declined. `ToggleModeAsync` writes the switch, or the reason it refuses one.
`ChangeImageAsync` writes `change of A|B asked by the Change A|B button`, one asked while a pair is
being opened or with no pair shown as ignored (Debug), what the dialog chose or that it was
cancelled, and, after the load, that a message is shown or that the padding was declined, the pair
on screen staying.
`OnPreviewKeyDown` hands `H` and `F` to `OnModeKeyAsync` with `KeyEventArgs.IsRepeat`, and a repeat
does nothing there, so a held key switches once (R-114, ADR 0044); it does not pass on a repeat of
`Ctrl+O` while a pair is being opened, which the open would ignore and log for each. `ShowPair`
writes the pair on screen and the memory, reading the process's memory once more before the outgoing
pair is released.
`DarkTitleBar` writes the HRESULTs of the DWM calls it does not act on, for a report of a white
caption. `MessageDialog` writes nothing: each caller says what it shows. The lines are listed in
`logging.md` (ADR 0043).

## The About window

`AboutWindow` is FlatCheck's About window ported, in this tool's colours (ADR 0045): the same
layout, sizes and styles, under FlatCheck's names, on this palette. A `StackPanel` holds the header,
a `Grid` of the `Logo` image (72 DIPs) and the name, the tagline (`Muted`), the banner in Consolas
and the copyright (`Faint`); the `LICENCE` card (`Card`, `Heading`, the paragraph, a `Hyperlink`);
the card of the components written by others, whose `ItemsControl`, `ComponentList`, draws each
`ComponentRow` through `ComponentRowTemplate`; and `Close`, a `DialogButton` that is both the
default and the cancel button, so Enter and Esc close the window too. `MessageDialog` keeps the two
apart because its buttons are two answers that would race to close it; About has no answer: a click,
Enter or Esc runs `CloseClick` first, and the cancel command WPF sends after it finds the window
closed, as in the About windows of FlatCheck and CopyWcs and in the live run of 2026-10-30.
`FontSize` 13 on the window is what FlatCheck's implicit `TextBlock` style gives every text block
without a style of its own; this theme has no implicit style, so the window sets it. The styles live
in `Theme.xaml`, with two brushes of their own: `PanelBrush`, the card, as much lighter than
`ChromeBrush` as FlatCheck's card is than its window (8 a channel), and `FaintTextBrush`, the muted
grey darkened by FlatCheck's ratio. The implicit `Hyperlink` style draws the links in the accent,
underlined under the pointer; they are the only links.

The window binds `AboutViewModel`, which copies every text from `UiText.Current`, and the name,
the banner, the copyright and the licence page from `VersionInfo`, when the window is built. The
logo is the 256-pixel frame of `Assets/ImageVisualCompare.ico`, which the csproj also makes a WPF
`Resource` for that: an `Image` given the file takes its first frame, maybe the 16-pixel one.
A `Hyperlink` outside a navigation window only raises `RequestNavigate`, so the window catches it
on its root and opens the address with `UseShellExecute`, disposing the process the shell may
return; a refusal writes one `Debug` line with the address as written, since `AbsoluteUri`
throws on a relative one. The
constructor applies the dark caption, as every window does. `MainWindow` writes `About opened` and
shows it modal over itself.

## The icon

`Assets/make-icon.ps1` draws the curtain of ADR 0030 with WPF's own drawing classes, from
Windows PowerShell, and writes `Assets/ImageVisualCompare.ico`; the file is committed, and the
script runs only when the drawing changes. The drawing lives on a 64-unit grid, but each of the
nine sizes is drawn at its own pixel size rather than scaled down from 256, because a scaled
divider lands between pixels and blurs into a grey band at 16 px. So the divider is always an
even number of whole pixels centred on the middle; below 32 px the stars are fewer and one
pixel each, and the ringed handle becomes a solid dot, since a ring with a hole does not
survive four pixels. A one-pixel outline in the chrome border colour keeps the tile's
edge visible on a black taskbar, where the dark half alone disappears. Entries below 256 px are
32-bit DIBs, the 256 px entry is a PNG, as Windows expects.

No window sets `Icon`: a WPF window without one takes the executable's, which the
`ApplicationIcon` of the csproj puts there, so the caption, the taskbar and the dialogs owned
by the main window all show it. `AppIconTests` checks the sizes of the file and that the built
application carries one icon group with all nine.
