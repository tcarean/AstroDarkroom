# Mindfulness Reminder

A tray-resident break reminder for Windows. It shows an always-on-top "Ground yourself"
window on every workstation unlock, and at a configurable interval while the user works --
unless the user is on a call or in a full-screen application, in which case it waits.

.NET 8, **zero external dependencies** — WPF for the windows, Windows Forms purely for
`NotifyIcon`, both from the WindowsDesktop shared framework. See "Conventions" before adding
a package.

## Structure

- `src/MindfulnessReminder.Core/` — `net8.0`, **no UI dependencies**. Every rule the app has
  lives here, which is what makes it unit-testable; the test project references this and
  nothing else.
  - `ReminderCoordinator.cs` — the single place that decides when a reminder appears. Both
    sources of one (an unlock, and the periodic schedule) go through it, so the rules about
    not stacking windows are written once. Owns the debounce, the already-showing check, and
    the `IntervalSchedule`.
  - `IntervalSchedule.cs` — the deadline arithmetic for the periodic reminder. No timer, no
    events, no UI. Read "The periodic reminder" below before touching it.
  - `ReminderSettings.cs` — `record(bool PeriodicEnabled, int IntervalMinutes)` plus three
    init-only properties for the two pauses and their retry gap, the bounds, and
    `Normalize()`. Everything that builds settings goes through `Normalize`. The pause
    settings are init-only rather than more positional parameters so that
    `new ReminderSettings(true, 30)` goes on meaning what it always meant.
  - `ISettingsStore.cs` / `RegistrySettingsStore.cs` — settings persistence over
    `IRegistryStore`, under the key the auto-start marker already uses.
  - `TrayTooltip.cs` — the hover text, as a pure function. It is a separate type because of
    the 63-character limit; see "The tray tooltip".
  - `SuppressionPolicy.cs` — `ReminderSuppression` and the pure rule behind the two pause
    settings: which switch governs which signal, and which wins when both apply.
  - `ForegroundState.cs` — the enum, plus `UserNotificationState.FromShellState`, the
    QUERY_USER_NOTIFICATION_STATE table. In Core, taking the raw integer, so the table is
    testable and the adapter has nothing left to get wrong.
  - `ActivitySnapshot.cs` — what the adapter gathers and the policy reads; the line between
    a Windows question and a rule.
  - `DetectionLog.cs` — the timeline `--detect <seconds>` renders. See "Pausing for what the
    user is doing".
  - `IPeriodicTimer.cs`, `IClock.cs`, `ISessionMonitor.cs`, `IReminderPresenter.cs`,
    `IRegistryStore.cs`, `IAutoStartManager.cs`, `IActivityMonitor.cs` — the seams. Each
    exists so a rule can be tested off a real machine; none has a second implementation in
    production.
  - `RegistryAutoStartManager.cs` / `StartupRegistrar.cs` — auto-start over `HKCU\...\Run`,
    plus the once-only registration on first run.
  - `WindowPlacement.cs`, `Version.cs`, `LicenseInfo.cs`, `AutoStartStatus.cs`.
- `src/MindfulnessReminder.App/` — `net8.0-windows`, `WinExe`, `AssemblyName` is
  `MindfulnessReminder`, so the binary is `MindfulnessReminder.exe`. A **thin adapter layer**:
  every file here is either a window or a Windows API behind a Core interface. Nothing here
  decides anything, which is why none of it is unit-tested.
  - `App.xaml.cs` — composition root. `ShutdownMode` is `OnExplicitShutdown`; there is no
    main window. Also holds the single-instance mutex, the crash dialog, the lifetimes of the
    Settings and About windows (both modeless and single-instance; `TrayIconHost` only raises
    the request), and `--preview` / `--detect` — both of which run BEFORE the mutex is
    claimed, so they work alongside the copy already in the tray.
  - `TrayIconHost.cs` — the icon and its four-item menu.
  - `BreakWindow.xaml(.cs)` — the reminder. `SettingsWindow.xaml(.cs)` — the settings.
    `AboutWindow.xaml(.cs)` — the licence notice, a window rather than a `MessageBox` since
    the dark theme; it displays `LicenseInfo.AboutText` and composes nothing itself.
  - `SystemEventsSessionMonitor.cs`, `DispatcherPeriodicTimer.cs`,
    `WpfReminderPresenter.cs`, `WindowsRegistryStore.cs`, `WindowsActivityMonitor.cs` —
    the adapters. The last one reads the `ConsentStore` capture-usage keys and calls
    `SHQueryUserNotificationState`, and interprets neither.
  - `Theme/DarkTheme.xaml` — the dark palette and the control templates that go with it.
    Merged into `SettingsWindow` **alone**. See "The dark theme".
  - `ImmersiveDarkMode.cs` — the one P/Invoke that darkens a window's title bar.
  - `DarkMenuRenderer.cs` — the same palette again, for the Windows Forms tray menu.
- `tests/MindfulnessReminder.Tests/` — xUnit, `net8.0`. References **Core only**.
- `README.md` — **the** user-facing documentation, and the single source for it.
- `Directory.Build.props` — `<Version>` and `<Copyright>` for the whole solution.
- `LICENSE` — the verbatim GNU GPL v3 text.

## Conventions

- **All text is in English** — UI strings, comments, documentation. (The sibling `FlatCheck`
  tool in this repo is Romanian; that convention is its own, do not carry it over.)
- **Zero external dependencies.** The point of the hybrid WPF/WinForms design is that
  `NotifyIcon` and everything else come from the shared framework. WPF having no numeric
  spinner is not a reason to add a package — the settings dialog hand-validates a `TextBox`.
- **Logic in Core, adapters in App.** If a new rule cannot be tested because it sits in the
  App project, it is in the wrong project. The test project deliberately does not reference
  App, so this is enforced by the build rather than by discipline.
- **Never bump the version and never commit** — both are the user's call. See "Versioning"
  and "Committing".
- **Every source file starts with the GPL notice**, `.cs` and `.xaml` alike. New files
  included. `LicenseHeaderTests` fails otherwise. Note that XML comments **cannot contain
  `--`**, so the `--` used as an em-dash in C# comments has to be reworded in XAML.

## The periodic reminder

Added in 1.1.0. The shape of it is more considered than the feature looks, and the four
decisions below are load-bearing.

**One timer, ticking every second, driving a deadline.** `IPeriodicTimer` is *not* armed for
the reminder interval. It ticks at `ReminderCoordinator.DefaultTickInterval` (1 s) and
`IntervalSchedule` holds a deadline that each tick is compared against. Two things need that
tick — deciding whether the interval has elapsed, and redrawing the tooltip countdown — and
a one-shot timer armed for the interval would have needed a second timer beside it. The
requirement was explicitly for one timer.

**The deadline is on the wall clock, never a count of elapsed ticks.** A `DispatcherTimer`
does not tick while the machine sleeps, so accumulating ticks would silently stretch a
30-minute interval by however long the lid was shut. Comparing against `IClock.UtcNow`
instead means a reminder that came due during sleep fires on the first tick after waking,
which is the wanted behaviour. Do not "fix" this into a stopwatch.

**A lock suspends the schedule; an unlock restarts it in full.** `ISessionMonitor` raises
`Locked` for this and nothing else — no reminder is ever shown on lock. Without the suspend,
the deadline would pass behind the lock screen, the window would be created where nobody can
see it, and it would then already be open when the unlock reminder tried to appear, so the
coordinator would raise it instead of showing one. `Resume` grants the whole interval afresh
regardless of how long the lock lasted, which is the requirement, and is why no time is
carried across a lock.

**The clock can move backwards.** An NTP correction or a hand-set clock would otherwise
postpone the reminder by however far it jumped. `TryConsumeDue` notices a deadline further
out than a whole interval could put it and starts over. The other direction needs no guard
and must not get one — see the sleep case above.

Two smaller rules that are easy to break:

- `HandleUnlock` restarts the schedule **before** the debounce and the already-showing
  check. Those two decide whether a *window* appears; the schedule is not theirs to skip.
- `TryConsumeDue` re-arms as it returns true, so a due reminder that cannot be shown (one is
  already on screen) still leaves a sane schedule instead of a deadline stuck in the past
  that fires on every subsequent tick.

Settings are applied by `ApplySettings`, which **restarts the countdown from zero**. Keeping
the elapsed time would mean changing 30 minutes to 10 with 25 gone either fires immediately
or leaves the tooltip showing a number the new setting cannot explain.

## Pausing for what the user is doing

Added in 1.2.0. Two switches, both on by default, that hold a reminder back rather than put
it over the top of something. They are **independent of each other and of the periodic
reminder** -- the unlock reminder obeys them too, which is why they sit at the top level of
the Settings window rather than inside the periodic panel.

**Reading the device, not the window title, is the whole point.** Windows records per-app
capture usage under `HKCU\...\CapabilityAccessManager\ConsentStore\{microphone,webcam}`,
with `LastUsedTimeStop == 0` while the stream is open -- the same record its own tray privacy
indicator reads. That covers Zoom, Teams (desktop and browser), a Slack huddle, Meet, Webex,
Discord, and whatever ships next, and it is still right on mute. A table of window titles was
considered and rejected: it needs an entry per application, breaks on each new release, and
misses a Slack huddle outright, because a huddle lives inside the main Slack window and has
no title of its own. **Do not reintroduce one.**

Full screen is `SHQueryUserNotificationState`, one P/Invoke, mapped in Core by
`UserNotificationState.FromShellState` so the table is testable. If it ever proves lossy on
borderless-fullscreen windows, the fallback is `GetForegroundWindow` + `GetWindowRect` against
the monitor bounds -- `--detect` is how to find out.

Rules that are easy to break:

- **`QUNS_NOT_PRESENT` must not suppress.** It is what Windows reports behind a lock screen
  and can still be the answer for a moment after an unlock -- long enough to swallow the
  unlock reminder. Nothing is lost by ignoring it: the schedule is suspended across a lock
  anyway.
- **A periodic reminder is POSTPONED; an unlock reminder is SKIPPED.** The unlock already
  restarted the schedule, so a reminder is coming; a one-shot retry beside the periodic one
  would be a second deadline to keep in step with the first.
- **`IntervalSchedule` compares the backwards-clock guard against `_armedSpan`, not against
  the interval.** A retry gap longer than the interval -- 300 s against a 1-minute interval --
  would otherwise look like a clock jump, reset the schedule and throw the postponement away.
  `A_postponement_longer_than_the_interval_is_not_read_as_a_clock_jump` pins it.
- **The activity monitor is read when a reminder is due, once per retry, and otherwise once
  every `DefaultActivityRefresh` (10 s) to keep the tooltip current.** Never on the
  one-second tick: that would be 1800 registry walks per reminder. The idle refresh is not
  decoration -- without it `Suppression` only becomes non-None at the moment a reminder is
  held back, so hovering the tray during a call would count down to a reminder that is not
  going to arrive, and the whole feature would be invisible until the interval happened to
  elapse. That is exactly how it was first written, and it was wrong.
- **No cap on how long a reminder is held back.** That is the requirement, and the tooltip
  carries the reason so a quiet tray icon can be told from a broken one.

`--detect` shows the raw readings and exits. It exists because neither signal can be
exercised without a real call or a real game, and it is the only way to tell a signal that is
not arriving from a setting that is switched off.

**`--detect <seconds>` is the form that matters for full screen**, and the reason it exists is
not convenience: a reading taken as the switch runs is useless there, because getting to a
terminal to run it means leaving full screen and destroying the state being measured. With a
duration it samples once a second and renders a timeline through `DetectionLog` -- which is in
Core, and tested, because a report that quietly lost the one moment the reading changed would
send someone hunting a bug that is not there. The message box is shown only after the run
ends, so it cannot disturb what is being measured.

## The tray tooltip

`NotifyIcon.Text` is capped at 63 characters and **throws** above it. That is the only
reason `TrayTooltip` is a separate pure type: the one thing worth testing is that the longest
string the app can produce still fits, and
`The_longest_tooltip_the_app_can_produce_fits_the_tray_limit` pins it.

Minutes are rounded **up**, never below 1, so a 30-minute countdown reads "30 min" the
instant it restarts and never reads "0 min" through its last minute.

While reminders are paused the tooltip shows the reason **instead of** the countdown. Two
things make that the right way round. A held-back reminder is counting the retry gap, seconds
rather than minutes, and rounding that up would leave the tooltip reading "1 min" for the
whole of an hour-long call. And between reminders the countdown would be a promise the app
cannot keep -- the number would run down and nothing would appear.

`App.UpdateTooltip` compares the rendered string before writing it. The countdown ticks every
second but changes once a minute, and every write is a `Shell_NotifyIcon` call.

## Windows the app shows

| window | modality | focus | why |
|---|---|---|---|
| `BreakWindow` | modeless, `Topmost` | takes it | It has to be seen. Shown after a ~600 ms delay because a window created into a freshly unlocked desktop lands behind everything. |
| `SettingsWindow` | **modeless**, not topmost | ordinary | A modal `ShowDialog` disables every other window in the process, so a reminder falling due while it sat open would appear with a dead Close button. Single-instance: reopening raises the existing one. |
| `AboutWindow` | **modeless**, not topmost | ordinary | Same reasoning, and single-instance the same way. Its Close button *is* `IsDefault`, unlike the reminder's: this window was opened on purpose, so Enter closing it is what the user means. |

`BreakWindow` is deliberately **not** `IsDefault` on its Close button, and it swallows Enter
in `OnPreviewKeyDown`. Since the reminder became periodic it arrives mid-work and takes the
focus, so the next Enter already on its way would both dismiss an unread reminder and be lost
to whatever was being typed. It must be *Preview*: plain `KeyDown` bubbles up from the focused
button, which would already have handled the key. Esc and Space still close it — both far
less likely mid-sentence than Enter, but if that ever changes, the same handler is the place.

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

## Settings storage

`HKCU\Software\MindfulnessReminder`, the key `StartupRegistrar` already keeps
`AutoStartInitialized` under. Five values:

| value | contents |
|---|---|
| `PeriodicRemindersEnabled` | `"1"` / `"0"` |
| `PeriodicIntervalMinutes` | decimal string |
| `PauseWhileMicrophoneInUse` | `"1"` / `"0"` |
| `PauseWhileFullScreen` | `"1"` / `"0"` |
| `PauseRetrySeconds` | decimal string |

All of them **strings, not DWORDs**, so `IRegistryStore` stays the four methods it had and
`FakeRegistryStore` needed no change.

Reading is deliberately forgiving. A missing value is a fresh machine, a corrupt one is a
hand-edited registry, and neither is a reason to fail to start; both land on the defaults
(**on**, 30 minutes, both pauses **on**, 20 seconds) — and a value missing because it was
written by an older build is exactly the first of those. Out-of-range values are **clamped
rather than rejected** — a hand-edited `0` would otherwise mean a reminder on every tick of
the timer.

The dialog is the exception: it refuses to save an out-of-range number and says so, because
there is a user standing there to tell. Clamping silently is for the registry, where there
is not.

## Build & test

```bash
dotnet build -c Release
```

```bash
dotnet test -c Release
```

To see the reminder window without locking the machine:

```bash
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --preview
```

`--preview` shows the window once and exits: no tray icon, no registry access, no session
hook, no timer.

```bash
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --detect
```

`--detect` reports what the activity monitor can see -- which application holds the microphone
or camera, the raw shell state and what it mapped to, and the verdict under the saved settings
-- then exits.

```bash
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --detect 60
```

With a number of seconds it watches instead, one reading a second, and reports a timeline.
`--detect 60` and `--detect=60` both work; a duration over ten minutes is clamped, and an
unparseable one falls back to the single reading rather than refusing to run. Neither switch
claims the single-instance mutex, so both run alongside the copy already in the tray. There is
no equivalent switch for the settings window.

## Versioning

`<Version>` in `Directory.Build.props` is the single source of truth. `Version.cs` does
**not** hardcode it — `VersionInfo.Value` reads `AssemblyInformationalVersion` back out of
the assembly and trims any `+sourcerevision` suffix. `No_project_overrides_the_shared_version`
fails if a `<Version>` appears in any `.csproj`.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** — not as part of a change, not "while
you're in there". If a change seems to warrant a bump, say so and let them decide.

Bumping breaks nothing: there are no golden files here, and `VersionTests` asserts the shape
of the version and the plumbing behind it, never a particular number.

## Licensing

GPLv3-or-later. Four things follow, all pinned by `LicenseHeaderTests`:

- **Every `.cs` and `.xaml` file starts with the notice** — SPDX line, copyright, all three
  standard paragraphs. `//` for C#, one `<!-- -->` block for XAML.
- **About displays the copyright and the warranty disclaimer** (GPLv3 §5(d)). It cannot go on
  the reminder window without defeating the point of the reminder.
- **One source of truth for the copyright string**: `<Copyright>` in `Directory.Build.props`,
  read back through `AssemblyCopyrightAttribute` by `LicenseInfo.Copyright`.
- **`LICENSE` ships with the build**, so About's "next to the executable" stays true after a
  `dotnet publish`.

The year and the author are the user's call, exactly like the version. Don't roll the year
forward on your own initiative.

## Committing

**Do not commit.** Make the changes, run the tests, and hand over a commit message for the
user to use. They commit themselves.

This also means: don't `git add`, don't stage, don't amend. Leave the working tree as it is
so they can review the diff before deciding.

A good commit message here follows the existing log: a short lowercase subject line, then a
body that explains *why* rather than restating the diff. No `Co-Authored-By` trailer.
