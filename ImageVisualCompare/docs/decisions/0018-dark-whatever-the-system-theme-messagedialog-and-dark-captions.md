# 0018. Dark whatever the system theme: MessageDialog, never MessageBox, and a dark caption on every window; file dialogs aside

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "No `MessageBox`, and every
window opts into a dark caption"; "Structure", the bullets for `Services/FileDialogService.cs`,
`Interop/DarkTitleBar.cs` and `MessageDialog.xaml(.cs)`), the summaries and constants of
`MessageDialog.xaml.cs` and `DarkTitleBar.cs`, and `README.md` (the third paragraph, "Totul e
întunecat, indiferent de tema sistemului"); the decision itself dates from f4b91ee (dark title
bars and the application's own message box, 2026-09-09), and the dark palette of `Theme.xaml`
from a6e5f10 (the base version, 2026-09-08). The palette is dark because a bright chrome around
an astro image destroys the eye's sense of what is actually dark in the picture (`Theme.xaml`).
Two things escaped it until f4b91ee. The error messages, the padding question and About went
through `MessageBox.Show`, a Win32 window that cannot be themed (white background, black text,
native buttons). And the caption belongs to the desktop window manager: WPF cannot reach it and
does not follow the system theme, so a dark chrome sat under a white title bar.

## Decision

- No `MessageBox`. `MessageDialog`, a plain WPF window on `ChromeBrush`, takes its place:
  `Show(owner, title, message)` with one `OK` button, and `Confirm(owner, title, message)` with
  `Yes` and `No`, where Escape and the close button count as no.
- Every `Window` calls `DarkTitleBar.Apply(this)` in its constructor (today `MainWindow` and
  `MessageDialog`), which sets the DWM attribute as soon as the window has a handle, or on
  `SourceInitialized` when it has none yet, so before the first frame. From build
  `FirstBuildUsingAttribute20` (19041) the attribute is `UseImmersiveDarkMode` (20), from
  `FirstDarkModeBuild` (17763) `UseImmersiveDarkModeBefore20H1` (19), and below that nothing is
  set. From `FirstWindows11Build` (22000), `CaptionColor` (35) and `BorderColor` (34) are set to
  `ChromeColorRef` (`0x00261F1B`), the chrome colour `#1B1F26` as a COLORREF; Windows 10 gets
  the generic dark caption.
- Dark is not "follow the system": the windows are dark whatever theme the desktop runs.
- The one exception: the two open dialogs of `FileDialogService` are the Windows common dialogs
  and follow the system theme.

## Alternatives rejected

- **`MessageBox.Show`, as before f4b91ee.** It cannot be themed at all; on a tool for judging
  dark images it is "a bright rectangle over the picture being compared", not a cosmetic
  complaint.
- **Follow the system theme.** On a light desktop that brings back "the bright edge around the
  image the palette exists to avoid"; the caption is dark "for the same reason the rest of the
  palette is" (the `DarkTitleBar` summary).
- **Apply the caption once the window is shown.** The title bar is then white for one frame
  (the `DarkTitleBar.Apply` summary).

## Consequences

- A new window calls `DarkTitleBar.Apply` in its constructor, and a new message or question
  goes through `MessageDialog`.
- The chrome colour is written twice: `ChromeBrush` in `Theme.xaml` and `ChromeColorRef` in
  `DarkTitleBar.cs`, whose comment names the `Theme.xaml` colour; one changes with the other.
- On Windows 10 the caption is close to the chrome but not `#1B1F26`: the colour attributes are
  set only from build 22000. No result of `DwmSetWindowAttribute` is checked.
- On a light desktop the two open dialogs are light: they are the one place the palette is not
  the application's to choose.
- Guards: none by test; the application has to be launched and looked at (ADR 0026).
  `CLAUDE.md` ("Testing") counts the interop that darkens a caption among what cannot be tested
  without UI automation; `MessageDialog` and `DarkTitleBar` have no test.

Source: CLAUDE.md ("Invariants", "Structure", "Testing"); README.md (third paragraph);
src/ImageVisualCompare.Wpf/MessageDialog.xaml.cs; src/ImageVisualCompare.Wpf/MessageDialog.xaml;
src/ImageVisualCompare.Wpf/Interop/DarkTitleBar.cs; src/ImageVisualCompare.Wpf/Theme.xaml;
src/ImageVisualCompare.Wpf/Services/FileDialogService.cs;
src/ImageVisualCompare.Wpf/MainWindow.xaml.cs.
