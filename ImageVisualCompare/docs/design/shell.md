# Shell: the culture trap and the dark chrome

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Invariants" (two of its ten). Where
> a paragraph became a decision record, its number is given beside it.
> **Covers:** `Directory.Build.props`,
> `src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs`,
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
has put the incoming one on screen (`rendering.md`). `_mode` is always what the pair says it was
rendered in, so a pair the loader fell back to linear on leaves both toggles unchecked and
disabled; the pair line of the status bar ends with the mode's name, and the readout gives, in
false colours, the level `RainbowTable.LevelOf` finds for the pixel's colour. The two toggles
have their own style, `ToolToggleButton` in `Theme.xaml`, a copy of `ToolButton`'s template
with an `IsChecked` trigger, since a `ToggleButton` is not a `Button` and cannot be based on
one.

## The focus after a toolbar click

Every toolbar button, the two toggles included, is a `ButtonBase` and focusable, so a click
takes the keyboard focus from the comparison surface, and the surface's shortcuts (R-063)
would reach the button instead. `MainWindow` adds one handler for `ButtonBase.ClickEvent` on
the toolbar's panel, `Toolbar`; the event bubbles, so the handler runs after the clicked
button's own, and it calls `Compare.Focus()`. By then `About`'s dialog has closed and
`Open…`'s file dialogs have been answered; a load that follows, an open or a mode switch,
focuses the surface once more when the pair is shown (`ShowPair`). The buttons stay focusable:
`Tab` still reaches them, and the dialog buttons share the style (ADR 0042). `MainWindowTests`
clicks each button but `About` on the WPF test thread and reads the window's logical focus, the
part of `Focus()` that works without a window handle (ADR 0032).

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
