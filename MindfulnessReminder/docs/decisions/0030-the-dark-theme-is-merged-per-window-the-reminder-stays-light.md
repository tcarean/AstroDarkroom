# 0030. The dark theme is merged per window, never into Application.Resources, so the reminder stays light; three surfaces, three mechanisms, the palette outside Core

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The dark theme", the first three
paragraphs and the table of surfaces; "Structure", the `Theme/DarkTheme.xaml`,
`ImmersiveDarkMode.cs` and `DarkMenuRenderer.cs` lines), `README.md` ("Behaviour", the last
bullet; "Design decisions", row "Dark theme") and the comments of `DarkTheme.xaml`,
`DarkMenuRenderer.cs`, `ImmersiveDarkMode.cs`, `SettingsWindow.xaml` and `AboutWindow.xaml`; the
decision itself dates from 432edfd (the dark theme, 1.3.0, 2026-09-08). Before it the Settings
window was drawn on the reminder's pale card colour (`#FFFDFCFA`), the tray menu was the stock
Windows Forms menu, and About was a `MessageBox` (ADR 0032). 1.3.0 made dark the three surfaces
"you go to on purpose" (README), and three facts shaped how. The theme's styles for `TextBox`,
`CheckBox`, `Button` and `TextBlock` are implicit, with no `x:Key`, so they apply in every window
the dictionary is merged into. The tray menu is Windows Forms, so "no WPF resource reaches it".
And the title bar "is not WPF's to paint -- it belongs to the non-client area", while "a settings
window themed to the caption and no further looks like a bug rather than a theme"
(`ImmersiveDarkMode`). The reminder was to stay light: "a dark rectangle appearing over dark work
is easy to miss, and being seen is the whole point of it" (README). The "Structure" line for
`Theme/DarkTheme.xaml` and the dictionary's own header comment say it is merged into
`SettingsWindow` alone; since 432edfd itself `AboutWindow` merges it too, as the section "The dark
theme" says.

## Decision

- `BreakWindow` is not dark, and the theme cannot reach it: `Theme/DarkTheme.xaml` is merged into
  the `Window.Resources` of each window that wants it, today `SettingsWindow` and `AboutWindow`,
  and never into `Application.Resources`. A third dark window takes one `MergedDictionaries`
  block the same way; the dictionary is not promoted to the application "on the grounds that it
  is now in two places".
- Three surfaces, three mechanisms, and nothing shared between them:

| surface | mechanism |
| --- | --- |
| the client area | `Theme/DarkTheme.xaml`, merged into `SettingsWindow` and `AboutWindow` |
| the title bar | `ImmersiveDarkMode.Apply`, from each window's `OnSourceInitialized` |
| the tray menu | `DarkMenuRenderer`, applied in `TrayIconHost`'s constructor |

- The palette is repeated in `DarkMenuRenderer` as `Color` literals, seven fields each equal to a
  brush of `DarkTheme.xaml`, and is not moved into Core: "a palette is presentation, and Core
  holds rules". The renderer overrides what the professional renderer paints light (the
  background, the border, the check-mark gutter, the item background and text, the check, the
  separator) over a dark colour table, so that "a missed spot lands on a dark colour rather than
  on the stock light one".
- All of it is written by hand, "control templates for WPF, a `ToolStripRenderer` for the menu",
  so it costs no package (README; ADR 0007).

## Alternatives rejected

- **Merging the theme into `Application.Resources`.** Its implicit styles would repaint the
  reminder; merging per window "makes the exclusion structural instead of something to
  remember".
- **A dark reminder.** Easy to miss over dark work, and being seen is its point (README).
- **The palette in a shared table in Core.** It "would be a palette -- presentation, not a rule
  -- living in the project that is meant to hold only rules" (`DarkMenuRenderer`; ADR 0008).
- **A caption drawn by hand, `WindowStyle="None"`.** It "means reimplementing dragging, the close
  button, snap and the system menu for a cosmetic gain" (`ImmersiveDarkMode`).
- **`BackColor` and `ForeColor` on the menu alone.** The professional renderer paints the
  drop-down background, the border, the gutter and the hover fill "from its own colour table, and
  every one of those is light" (`DarkMenuRenderer`).

## Consequences

- A colour of the theme changes in two files, `DarkTheme.xaml` and `DarkMenuRenderer.cs`: "That
  duplication is the price of the hybrid design." The accent is also the reminder's own (`#2A7A6E`
  on its Close button), "carried over from the reminder window so the two agree".
- A new dark window merges the dictionary in its own XAML and calls `ImmersiveDarkMode.Apply`
  from `OnSourceInitialized`; its templates follow ADR 0031.
- The crash dialog and the `--detect` report are `MessageBox` dialogs and stay light (ADR 0032).
- Guards: none by test; launch the application with `--preview` (the reminder must stay light),
  or from the tray (the menu, Settings and About dark, title bars included).

Source: CLAUDE.md ("The dark theme"; "Structure"); README.md ("Behaviour"; "Design decisions",
row "Dark theme"); src/MindfulnessReminder.App/Theme/DarkTheme.xaml;
src/MindfulnessReminder.App/DarkMenuRenderer.cs; src/MindfulnessReminder.App/ImmersiveDarkMode.cs;
src/MindfulnessReminder.App/SettingsWindow.xaml; src/MindfulnessReminder.App/AboutWindow.xaml;
src/MindfulnessReminder.App/TrayIconHost.cs.
