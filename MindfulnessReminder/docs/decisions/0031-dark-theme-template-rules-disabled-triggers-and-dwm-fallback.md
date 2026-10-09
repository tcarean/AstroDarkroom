# 0031. The dark theme's template rules: a disabled trigger in every template, no TextBlock Foreground setter, DynamicResource on the Window tag, focus as a colour, two button styles, a DWM call allowed to fail

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The dark theme", the six "Rules that are
easy to break") and the comments of `DarkTheme.xaml`, `SettingsWindow.xaml` and
`ImmersiveDarkMode.cs`; the decision itself dates from 432edfd (the dark theme, 1.3.0,
2026-09-08). Every template in `DarkTheme.xaml` replaces a stock one, because the stock ones "are
painted for a light window: a TextBox drawn white with black text on top of this background is
unreadable, not merely off-palette". A replaced template takes with it whatever the stock one
did, and nothing fails when it does. The sources name what each rule below protects: "The
interval panel reading as unavailable while the periodic reminder is switched off", which "is a
requirement, not decoration"; a disabled checkbox that would otherwise "keep bright text beside a
greyed box"; a focused box whose text would otherwise move; and a title bar whose DWM attribute
has a different number on different Windows 10 builds.

## Decision

1. **Every template carries its own disabled trigger.** "The stock greying belongs to the stock
   template, and replacing the template throws it away silently — no test fails." The `TextBox`,
   `CheckBox`, `Button` and `PrimaryButtonStyle` templates each grey their surface, border and
   text when `IsEnabled` is false; in the `CheckBox` template the disabled trigger comes last, so
   that "it wins over checked and hover alike".
2. **`TextBlock`'s implicit style has no `Foreground` setter, only a trigger.** A setter "would
   beat the inherited value on the `TextBlock` a `ContentPresenter` generates for a `CheckBox`
   caption"; the colour comes by inheritance and the trigger greys it. The hints are the
   exception: `HintTextStyle` carries their colour and their trigger together, "because within one
   style a trigger outranks a setter".
3. **`Window.Background` and `Window.Foreground` use `DynamicResource`.** "Attributes on the
   `Window` tag are parsed before `Window.Resources` exists, so a `StaticResource` there has
   nothing to find." Everything inside the tree uses `StaticResource`.
4. **Focus is a border colour, never a border thickness.** "Growing a `TextBox` border by a pixel
   nudges the text inside it every time the box is tabbed into."
5. **Two button styles rather than one**: the implicit `Button` style, and `PrimaryButtonStyle`,
   on the accent, which the default buttons use (Save in Settings, Close in About).
6. **`DwmSetWindowAttribute` failing is not an error.** The attribute is 20 from Windows 10 build
   18985 and 19 on 17763..18984; `ImmersiveDarkMode.Apply` asks for 20, falls back to 19, and then
   gives up, throwing and reporting nothing.

## Alternatives rejected

- **The stock templates.** Painted for a light window, and unreadable on this one.
- **A `Foreground` setter in the implicit `TextBlock` style.** A disabled checkbox would keep
  bright text beside a greyed box.
- **A local `Foreground` on each hint.** "a local one on the element would block the trigger
  above" (`DarkTheme.xaml`).
- **`StaticResource` on the `Window` tag.** It has nothing to find there.
- **A thicker border for focus.** It moves the text.
- **One button template.** It "would need every hover, pressed and focus trigger paired with a
  second condition asking whether this is the default button, because the plain trigger for each
  would otherwise paint the accent back to neutral" (`DarkTheme.xaml`).
- **Reading the build number to pick the attribute.** Not done: "Rather than read the build number
  and pick, this asks for 20 and falls back to 19 when the call is refused" (`ImmersiveDarkMode`).
- **Reporting a failed call.** "The worst outcome is a light caption, which is not worth a
  dialog."

## Consequences

- A new template carries its own `IsEnabled` trigger, placed last where it has to win over the
  others; a new hint uses `HintTextStyle`; a new default button uses `PrimaryButtonStyle`.
- The rules govern `DarkTheme.xaml`. `BreakWindow`, which the theme does not reach (ADR 0030), has
  its own Close button template, and that one sets a border of 2 on keyboard focus.
- On a build that knows neither attribute the caption stays light: "failure here is a window that
  looks wrong, never a window that does not open" (`ImmersiveDarkMode`).
- Guards: none by test, which is the point of the first rule ("no test fails"). Launch the
  application from the tray and open Settings: switch each reminder and both pauses off and on,
  and tab through the boxes; then open About. `--preview` shows only the reminder, which these
  rules do not touch.

Source: CLAUDE.md ("The dark theme", "Rules that are easy to break");
src/MindfulnessReminder.App/Theme/DarkTheme.xaml; src/MindfulnessReminder.App/SettingsWindow.xaml;
src/MindfulnessReminder.App/AboutWindow.xaml; src/MindfulnessReminder.App/ImmersiveDarkMode.cs;
src/MindfulnessReminder.App/BreakWindow.xaml.
