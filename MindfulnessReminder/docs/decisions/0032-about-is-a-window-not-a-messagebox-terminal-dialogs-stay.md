# 0032. About is a window, not a MessageBox, showing AboutText in one TextBlock and the icon's largest frame; the crash and --detect dialogs stay MessageBox

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The dark theme", the last four
paragraphs; "Structure", the `AboutWindow.xaml(.cs)` line), `README.md` ("Design decisions", row
"About") and the comments of `App.ShowAbout`, `AboutWindow.xaml(.cs)`, `TrayIconHost.cs` and
`LicenseInfo.cs`; the decision itself dates from 432edfd (the dark theme, 1.3.0, 2026-09-08).
About came with d3ccd6c (licensing and versioning, 1.0.0, 2026-09-01) as the place where the
copyright and the warranty disclaimer are displayed (ADR 0010): a Windows Forms `MessageBox`
showing `LicenseInfo.AboutText`, opened by `TrayIconHost` itself. 432edfd made the tray menu dark
(ADR 0030), and "A Win32 dialog cannot be themed, and a white About box opening out of a dark
tray menu reads as a different program."

## Decision

- About is `AboutWindow`, a WPF window with the dark theme and a dark title bar (ADR 0030),
  modeless and single-instance like Settings (ADR 0026).
- It shows `LicenseInfo.AboutText` in **one** `TextBlock`, set in its constructor, rather than
  the banner, the copyright and the disclaimer as three elements; the window "composes nothing
  itself", the text is composed in Core.
- `TrayIconHost` raises `AboutRequested` exactly as it raises `SettingsRequested`, and
  `App.ShowAbout` opens the window: "Window lifetimes belong to `App`, and the tray host knows
  about neither window."
- The icon, `Assets/app.ico`, is decoded with `BitmapDecoder` and the frame with the largest
  `PixelWidth` is drawn at 48 px. `TrayIconHost.LoadTrayIcon` does the same in the other
  direction, asking for `SystemInformation.SmallIconSize`, as it has since 61a0121.
- The two other `MessageBox` dialogs stay `MessageBox`, and light: the crash dialog (ADR 0033)
  and the `--detect` report (ADR 0020). "Both are terminal, in the sense that the app is either
  dying or exiting straight after, so neither is worth a window."

## Alternatives rejected

- **The `MessageBox`, as from d3ccd6c to 432edfd.** It cannot be themed, and being a window "is
  also the only way it could carry the app icon" (`AboutWindow`).
- **The banner, the copyright and the disclaimer as three elements.** That "would read a shade
  better and would leave AboutText displayed nowhere, which is the property two tests describe as
  everything About shows, in order. The notice is the point of this window; its typography is
  not." (`AboutWindow.xaml`)
- **About opened by `TrayIconHost`, as before.** The tray host would own a window's lifetime,
  which is `App`'s for Settings already.
- **The `.ico`'s first frame.** "frame order is not a promise", and "scaling a 16 px frame up is
  what makes an About icon look like a mistake".
- **Windows for the crash dialog and the `--detect` report.** "the app is either dying or exiting
  straight after".

## Consequences

- What About says changes in `LicenseInfo`, in Core, where tests read it; the window has nothing
  of its own to keep in step.
- Two dialogs stay light beside the dark windows: the crash report and the `--detect` timeline.
- Guards: `VersionTests` and `LicenseHeaderTests` pin `LicenseInfo.AboutText` (the banner on the
  first line, the copyright on the second, the warranty disclaimer, the licence's name, `LICENSE`
  and the GPL's address). The window, the event and the icon frame: none by test; launch the
  application from the tray and open About.

Source: CLAUDE.md ("The dark theme"; "Structure"); README.md ("Design decisions", row "About");
src/MindfulnessReminder.App/AboutWindow.xaml; src/MindfulnessReminder.App/AboutWindow.xaml.cs;
src/MindfulnessReminder.App/App.xaml.cs; src/MindfulnessReminder.App/TrayIconHost.cs;
src/MindfulnessReminder.Core/LicenseInfo.cs; tests/MindfulnessReminder.Tests/VersionTests.cs;
tests/MindfulnessReminder.Tests/LicenseHeaderTests.cs.
