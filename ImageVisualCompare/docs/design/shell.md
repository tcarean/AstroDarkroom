# Shell: the culture trap and the dark chrome

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Invariants" (two of its ten). Where
> a paragraph became a decision record, its number is given beside it.
> **Covers:** `Directory.Build.props`,
> `src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs`,
> `src/ImageVisualCompare.Wpf/Interop/DarkTitleBar.cs`,
> `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`,
> `src/ImageVisualCompare.Wpf/Theme.xaml`.
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
