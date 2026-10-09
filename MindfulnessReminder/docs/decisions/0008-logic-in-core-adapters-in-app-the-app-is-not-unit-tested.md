# 0008. Logic in Core, adapters in App: every rule in a library without UI that the tests reference; the App is a thin layer with no unit tests

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Structure", the paragraphs on
`src/MindfulnessReminder.Core/` and `src/MindfulnessReminder.App/` and the line on the seams;
"Conventions", "Logic in Core, adapters in App") and `README.md` ("Layout"); the decision itself
dates from 61a0121 (the base version, 2026-08-28), which already had a `net8.0` Core with "no UI
dependencies" (README, "Layout"), a test project that references Core alone, and five of the
seven seams, and was written into `CLAUDE.md` in b6554d5 (the periodic reminder, 1.1.0,
2026-09-08), which added `IPeriodicTimer`; 89de6c8 (pausing for a call or full screen, 1.2.0,
2026-09-08) added `IActivityMonitor`. What the application reacts to is the machine itself: a lock
and an unlock, half an hour going by, a live microphone, a full-screen game, the per-user
registry. The interesting cases of the schedule are "the ones a manual test cannot reach in
reasonable time" (the `IntervalScheduleTests` summary). `CLAUDE.md` says of the App
"Nothing here decides anything"; on 2026-10-09 a few small rules sit there, listed under
Consequences.

## Decision

- `MindfulnessReminder.Core` targets `net8.0`, without `UseWPF` or `UseWindowsForms`. "Every rule
  the app has lives here, which is what makes it unit-testable" (`CLAUDE.md`, "Structure"): on
  2026-10-09 the coordinator, the schedule, the settings and their normalization, the
  suppression policy, the shell-state table, the tooltip text, the `--detect` timeline,
  auto-start, the settings store, window placement, the version and the licence text.
- `MindfulnessReminder.App` targets `net8.0-windows` (`WinExe`, `MindfulnessReminder.exe`) and is
  "a **thin adapter layer**": the three windows, the tray icon and its menu, the dark theme, the
  composition root, and five adapters behind Core interfaces, `SystemEventsSessionMonitor`,
  `WpfReminderPresenter`, `DispatcherPeriodicTimer`, `WindowsRegistryStore` and
  `WindowsActivityMonitor`. None of it is unit-tested.
- The test project, `net8.0`, references Core and nothing else, so the rule "is enforced by the
  build rather than by discipline" (`CLAUDE.md`, "Conventions").
- The seams are `IPeriodicTimer`, `IClock`, `ISessionMonitor`, `IReminderPresenter`,
  `IRegistryStore`, `IAutoStartManager` and `IActivityMonitor`, in Core. "Each exists so a rule
  can be tested off a real machine" (`CLAUDE.md`, "Structure"), and each has one implementation
  in the running application: the five adapters above, and `SystemClock` and
  `RegistryAutoStartManager` (which works over `IRegistryStore`), both in Core.
- The kit's ADR 0002 has made a library without UI the rule for every project made from the kit
  since 2026-09-24; this project had its own form of it from its first commit, and this ADR
  records that form.

## Alternatives rejected

- **A rule in the App.** "If a new rule cannot be tested because it sits in the App project, it
  is in the wrong project" (`CLAUDE.md`, "Conventions"): no test can reach it.
- **A test project that references the App too.** The rule would then hold by discipline; with
  Core as its only reference it is "enforced by the build".
- **Rules checked on a real machine.** A lock longer than the interval or a clock that moves
  backwards are "the ones a manual test cannot reach in reasonable time" (the
  `IntervalScheduleTests` summary); behind a seam the same rule runs on a fake, "in microseconds
  instead of in half an hour" (the `IntervalSchedule` summary).

## Consequences

- A new rule goes into Core with its test; a new Windows API goes into the App behind a Core
  interface, with a fake in the tests. A seam also makes an implementation replaceable: behind
  `ISessionMonitor`, "a `WTSRegisterSessionNotification` implementation can replace it without
  touching anything else" (README, "Design decisions", row "Unlock detection").
- The tests run on fakes, with no window, timer, session or registry: `FakeSessionMonitor`,
  `FakePresenter`, `FakeTimer`, `FakeActivityMonitor` and `MutableClock` inside
  `ReminderCoordinatorTests`, and `FakeRegistryStore`. `IAutoStartManager` has no fake:
  `RegistryAutoStartManagerTests` and `StartupRegistrarTests` run the real
  `RegistryAutoStartManager` over `FakeRegistryStore`. `IdleActivityMonitor`, in Core, is what
  the coordinator uses when it is given no monitor; the App always gives it
  `WindowsActivityMonitor`.
- A few small rules are still in the App, with no test: which `SessionSwitch` reasons count as a
  lock or an unlock (`SystemEventsSessionMonitor`); that Settings refuses a bad number or message
  only for what is switched on (`SettingsWindow.OnSaveClick`; the checks themselves are Core's
  `IsIntervalInRange`, `IsRetryInRange` and `IsMessageAcceptable`); the parsing of `--detect` and
  its ten-minute clamp (`App.DetectDuration`); and writing the tooltip only when its text changed
  (`App.UpdateTooltip`).
- A change under `src/MindfulnessReminder.App/` is checked by launching the application: a green
  suite runs none of that code.
- Guards: the compile boundary (Core targets `net8.0`, so a WPF or Windows Forms type does not
  compile there, and the test project references Core alone) and the classes that run Core's
  rules on fakes: `ReminderCoordinatorTests`, `RegistryAutoStartManagerTests`,
  `StartupRegistrarTests` and `ReminderSettingsStoreTests`. The App: none by test; launch the
  application with `--preview`, or from the tray.

Source: CLAUDE.md ("Structure"; "Conventions"); README.md ("Layout"; "Design decisions", row
"Unlock detection"); src/MindfulnessReminder.Core/MindfulnessReminder.Core.csproj;
src/MindfulnessReminder.App/MindfulnessReminder.App.csproj;
tests/MindfulnessReminder.Tests/MindfulnessReminder.Tests.csproj; src/MindfulnessReminder.Core/
(IPeriodicTimer.cs, IClock.cs, ISessionMonitor.cs, IReminderPresenter.cs, IRegistryStore.cs,
IAutoStartManager.cs, IActivityMonitor.cs, IntervalSchedule.cs);
src/MindfulnessReminder.App/App.xaml.cs; src/MindfulnessReminder.App/SettingsWindow.xaml.cs;
src/MindfulnessReminder.App/SystemEventsSessionMonitor.cs; the kit's ADR 0002.
