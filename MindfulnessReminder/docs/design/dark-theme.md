# The dark theme

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "The dark theme". Where a paragraph
> became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.App/Theme/DarkTheme.xaml`,
> `src/MindfulnessReminder.App/ImmersiveDarkMode.cs`,
> `src/MindfulnessReminder.App/DarkMenuRenderer.cs`,
> `src/MindfulnessReminder.App/SettingsWindow.xaml`,
> `src/MindfulnessReminder.App/SettingsWindow.xaml.cs`,
> `src/MindfulnessReminder.App/AboutWindow.xaml`, `src/MindfulnessReminder.App/AboutWindow.xaml.cs`,
> `src/MindfulnessReminder.App/TrayIconHost.cs`, `src/MindfulnessReminder.App/App.xaml.cs`. A change
> to the code this describes means this note is updated in the same task, or deleted with the
> decision it explains.

## The dark theme

Added in 1.3.0. The Settings window, About and the tray menu are dark. **`BreakWindow` is
deliberately not**, and keeping it that way is the constraint the rest of this section is
arranged around.

**The theme is merged per window, never into `Application.Resources`.** `DarkTheme.xaml`
holds implicit styles — `TextBox`, `CheckBox`, `Button`, `TextBlock`, no `x:Key` — so an
application-wide merge would repaint the reminder with them. Merging it into each window
that wants it makes the exclusion structural instead of something to remember. `AboutWindow`
was the second to take it, and taking it cost one `MergedDictionaries` block. A third dark
window follows the same pattern; **do not promote the dictionary to the application** on the
grounds that it is now in two places.

See ADR 0030.

**Three surfaces, three mechanisms, and nothing shared between them:**

| surface | mechanism |
|---|---|
| the client area | `Theme/DarkTheme.xaml`, merged into `SettingsWindow` and `AboutWindow` |
| the title bar | `ImmersiveDarkMode.Apply`, from each window's `OnSourceInitialized` |
| the tray menu | `DarkMenuRenderer`, applied in `TrayIconHost`'s constructor |

The menu is Windows Forms, so no WPF resource reaches it and the palette is repeated in
`DarkMenuRenderer` as `Color` literals. That duplication is the price of the hybrid design.
Do not "fix" it by moving the palette into Core: a palette is presentation, and Core holds
rules.

See ADR 0030.

Rules that are easy to break:

- **Every template carries its own disabled trigger.** The stock greying belongs to the
  stock template, and replacing the template throws it away silently — no test fails. The
  interval panel reading as unavailable while the periodic reminder is switched off is a
  requirement, not decoration.
- **`TextBlock`'s implicit style has no `Foreground` setter, only a trigger.** A setter would
  beat the inherited value on the `TextBlock` a `ContentPresenter` generates for a `CheckBox`
  caption, so a disabled checkbox would keep bright text beside a greyed box. The hints are
  the exception, and they carry their colour and their trigger together in `HintTextStyle`,
  because within one style a trigger outranks a setter.
- **`Window.Background` and `Window.Foreground` use `DynamicResource`.** Attributes on the
  `Window` tag are parsed before `Window.Resources` exists, so a `StaticResource` there has
  nothing to find. Everything inside the tree uses `StaticResource` normally.
- **Focus is a border colour, never a border thickness.** Growing a `TextBox` border by a
  pixel nudges the text inside it every time the box is tabbed into.
- **Two button styles rather than one.** A single template would need every hover, pressed
  and focus trigger paired with a second condition asking whether this is the default button.
- **`DwmSetWindowAttribute` failing is not an error.** The attribute is 20 from Windows 10
  build 18985 and 19 on 17763..18984; `Apply` asks for 20, falls back to 19, and then gives
  up. The worst outcome is a light caption, which is not worth a dialog.

See ADR 0031.

**About is a window, not a `MessageBox`.** A Win32 dialog cannot be themed, and a white About
box opening out of a dark tray menu reads as a different program. It shows
`LicenseInfo.AboutText` in **one** `TextBlock` rather than laying the banner, the copyright
and the disclaimer out as three: that property is what two tests describe as everything About
shows, in order, and splitting it would leave it displayed nowhere. The notice is the point
of the window; its typography is not.

Opening it also moved out of `TrayIconHost`, which now raises `AboutRequested` exactly as it
raises `SettingsRequested`. Window lifetimes belong to `App`, and the tray host knows about
neither window.

The icon is decoded to its **largest** frame, not its first. An `.ico` holds several sizes and
frame order is not a promise; the window draws at 48 px, and scaling a 16 px frame up is what
makes an About icon look like a mistake. `TrayIconHost.LoadTrayIcon` does the same thing in
the other direction, asking for `SystemInformation.SmallIconSize`.

The remaining `MessageBox` dialogs — the crash report and the `--detect` timeline — stay
light. Both are terminal, in the sense that the app is either dying or exiting straight
after, so neither is worth a window.

See ADR 0032 for About, its opening, the icon and the two dialogs that stay `MessageBox`.
