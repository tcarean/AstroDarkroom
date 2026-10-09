# 0007. Zero external dependencies: WPF for the windows, Windows Forms for NotifyIcon, both from the shared framework; a hand-validated TextBox, not a spinner package

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` (the opening lines, ".NET 8, **zero
external dependencies**"; "Conventions", "Zero external dependencies"), `README.md` ("Design
decisions", rows "UI" and "Settings window") and the comment on `UseWPF` and `UseWindowsForms` in
`src/MindfulnessReminder.App/MindfulnessReminder.App.csproj`; the decision itself dates from
61a0121 (the base version, 2026-08-28), whose App csproj and README already carry the hybrid and
its reason, and its half about the spinner from b6554d5 (the periodic reminder, 1.1.0,
2026-09-08), which added the Settings window and wrote the rule into the new `CLAUDE.md`. The
application is a tray icon that opens windows. WPF draws the windows, but "`NotifyIcon` has no WPF
equivalent in the BCL" (README, row "UI"); Windows Forms has one, and "both live in the
WindowsDesktop shared framework, so the hybrid needs no external package" (the same row). The
Settings window of 1.1.0 takes a number, the interval in minutes, and since 89de6c8 (pausing for a
call or full screen, 1.2.0, 2026-09-08) a second one, the retry gap in seconds; "WPF has no
numeric spinner in the BCL, which is not worth a package" (row "Settings window"). The csproj
comment has Windows Forms there "purely for NotifyIcon"; the code also takes `Screen` from it,
which `BreakWindow` asks for a monitor's work area, as it has since the base version.

## Decision

- `MindfulnessReminder.Core` and `MindfulnessReminder.App` reference no NuGet package. The App
  sets `UseWPF` and `UseWindowsForms`, both of the WindowsDesktop shared framework of .NET 8: WPF
  for the three windows, Windows Forms for the tray icon and its menu, and for `Screen`.
- The Settings window takes its two numbers in plain `TextBox`es. Typing lets only digits in,
  which is "a convenience, not the validation: a paste goes around it" (the comment on
  `OnIntervalPreviewTextInput`); Save parses the text again, checks the range with
  `ReminderSettings.IsIntervalInRange` or `IsRetryInRange`, in Core, and refuses a number it
  needs with a message in the window.

## Alternatives rejected

- **A package for a WPF tray icon.** Windows Forms supplies `NotifyIcon` from the same shared
  framework, "so the hybrid needs no external package" (README, row "UI").
- **A package for a numeric spinner.** "WPF has no numeric spinner in the BCL, which is not worth
  a package" (README, row "Settings window"); in the words of `CLAUDE.md` it "is not a reason to
  add a package". A `TextBox` checked on Save does the job, and its range check is in Core.

## Consequences

- Two UI frameworks share one process. `App.OnStartup` sets the one process-wide DPI mode,
  `PerMonitorV2`, before any window exists, and calls `EnableVisualStyles`, without which the
  Windows Forms menu "renders unthemed". No WPF resource reaches that menu, so the dark palette
  is repeated in `DarkMenuRenderer` (ADR 0030): "That duplication is the price of the hybrid
  design" (`CLAUDE.md`, "The dark theme").
- The features after the base version kept to the rule: the full-screen check is "One P/Invoke,
  no dependency" (README, row "Full screen"), and the dark theme was "Written by hand ... so it
  costs no package" (row "Dark theme").
- The solution's only packages are the test project's three, `Microsoft.NET.Test.Sdk`, `xunit`
  and `xunit.runner.visualstudio`, the same since 61a0121; their versions are in
  `Directory.Packages.props` (ADR 0003), whose comment names this rule.
- The rule stays in `CLAUDE.md` as "Zero external dependencies", which extends
  the kit's rule "Packages": where the kit asks before a new package, this project takes none in
  Core or the App. A package there would reverse this decision, with an ADR of its own.
- Guards: no test checks it. `Directory.Packages.props` lists only the three test packages, and
  the `packages.lock.json` of Core and of the App list none (ADR 0003). The `TextBox` and its
  refusal are App: none by test; launch the application from the tray and open Settings. Of the
  two range checks behind them, `ReminderSettingsStoreTests` pins `IsIntervalInRange`;
  `IsRetryInRange` has no test.

Source: CLAUDE.md (opening lines; "Conventions"; "The dark theme"); README.md ("Design
decisions", rows "UI", "Settings window", "Full screen", "Dark theme");
src/MindfulnessReminder.App/MindfulnessReminder.App.csproj;
src/MindfulnessReminder.App/SettingsWindow.xaml.cs; src/MindfulnessReminder.App/App.xaml.cs;
src/MindfulnessReminder.App/BreakWindow.xaml.cs; Directory.Packages.props;
src/MindfulnessReminder.Core/packages.lock.json; src/MindfulnessReminder.App/packages.lock.json.
