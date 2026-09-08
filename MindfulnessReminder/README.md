# Mindfulness Reminder

A tray-resident reminder for Windows. It shows a small always-on-top window that says
**Ground yourself** every time you unlock the workstation, and — since 1.1.0 — at a set
interval while you work.

## Behaviour

- Lives in the notification area. The tray menu has four items: **Settings...**, a **Run at
  Windows startup** checkbox, **About**, and **Exit**.
- Registers itself for Windows startup on the very first run only, so unticking the checkbox
  sticks across restarts.
- The reminder always appears on the primary monitor, on top of everything, with a
  single **Close** button. Esc closes it; **Enter deliberately does not** — see
  [Periodic reminders](#periodic-reminders).
- The tray tooltip counts down to the next reminder:
  `Mindfulness Reminder 1.1.0 - next in 12 min`.

## Periodic reminders

On top of the unlock reminder, one appears every *N* minutes while you work. It is on by
default, every 30 minutes, and both the switch and the interval live in **Settings...** in
the tray menu — with the interval greyed out while the feature is off. Anything from 1 to
480 minutes is accepted; saving restarts the countdown from zero.

Four things about it are deliberate:

- **One timer, ticking every second, driving a deadline.** It is not armed for the interval.
  The countdown in the tooltip needs a regular tick anyway, and a one-shot timer would have
  needed a second timer beside it just to redraw that.
- **A deadline on the wall clock, not a count of elapsed ticks.** A `DispatcherTimer` does
  not tick while the machine sleeps, so counting ticks would silently stretch a 30-minute
  interval by however long the lid was shut. With a deadline, a reminder that came due
  during sleep fires on the first tick after waking.
- **Locking suspends it; unlocking grants the whole interval again.** With 10 minutes left,
  a lock, three hours away and an unlock, the next reminder is 30 minutes after that unlock —
  for any number of lock/unlock cycles, of any length. Without the suspend the deadline would
  pass behind the lock screen and the window would be created where nobody can see it.
- **Enter no longer closes the reminder.** It used to, back when the window only appeared on
  a freshly unlocked desktop where nobody is typing. A window that takes the focus mid-work
  would otherwise be dismissed by the next Enter already on its way — and swallow that
  keypress out of whatever was being typed.

## Design decisions

| Concern | Choice | Why |
|---|---|---|
| UI | WPF window + Windows Forms `NotifyIcon` in one process | `NotifyIcon` has no WPF equivalent in the BCL, and both live in the WindowsDesktop shared framework, so the hybrid needs no external package |
| Auto-start | `HKCU\...\CurrentVersion\Run` | No elevation, and the entry is one value that maps directly onto a checkbox |
| Auto-start truth | also reads `Explorer\StartupApproved\Run` | Task Manager's Startup tab vetoes entries without deleting them; a checkbox reading only the Run key would report "on" for an entry Windows ignores |
| Unlock detection | `SystemEvents.SessionSwitch` | A few lines behind `ISessionMonitor`; a `WTSRegisterSessionNotification` implementation can replace it without touching anything else |
| Periodic schedule | one `DispatcherTimer` at 1 s + a deadline in `IntervalSchedule` | Survives sleep, feeds the tooltip off the same tick, and makes "reset on unlock" a single line |
| Settings storage | strings under `HKCU\Software\MindfulnessReminder` | The key the auto-start marker already uses, and strings keep `IRegistryStore` at the four methods it had |
| Settings window | modeless, hand-validated `TextBox` | A modal `ShowDialog` would disable every other window, so a reminder falling due would appear with a dead Close button; and WPF has no numeric spinner in the BCL, which is not worth a package |

## Layout

    src/MindfulnessReminder.Core   net8.0, no UI dependencies -- auto-start rules, unlock and interval policy, placement math
    src/MindfulnessReminder.App    net8.0-windows, WinExe -- tray icon, reminder and settings windows, Windows adapters
    tests/MindfulnessReminder.Tests xunit

## Build and run

```
dotnet build -c Release
dotnet test -c Release
```

To see the reminder window without locking the machine:

```
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --preview
```

`--preview` shows the window once and exits: no tray icon, no registry access, no session hook.

## Deployment note

The Run entry records the path of the executable that was running when auto-start was switched
on. Moving the app is handled -- each launch repairs a stale path -- but the first launch after
a move has to happen manually. Publish somewhere stable, e.g.

```
dotnet publish src\MindfulnessReminder.App -c Release -o %LOCALAPPDATA%\MindfulnessReminder
```

## Versioning

There is no console to print a banner on, so the app identifies itself in the three places a
user can actually see it:

| where | what it shows |
|---|---|
| tray tooltip | `Mindfulness Reminder 1.1.0 - next in 12 min` -- hovering the icon says which build is running, and when the next reminder is due |
| **About** | the banner on the first line, the copyright on the second, the licence notice below |
| crash dialog | the error, then the banner, so a screenshot of it says which version broke |

The version lives in exactly one place: `<Version>` in [Directory.Build.props](Directory.Build.props),
next to `<Copyright>`. `src/MindfulnessReminder.Core/Version.cs` does **not** hardcode it --
`VersionInfo.Value` reads the assembly's `AssemblyInformationalVersion`, which MSBuild derives
from `<Version>`, and trims the `+sourcerevision` suffix the build appends. Windows reads the
same property for the executable's file version. Don't reintroduce a literal, and don't add a
`<Version>` to a project file: `No_project_overrides_the_shared_version` fails if you do.

**The version is bumped manually**, per feature or fix, with the increment chosen deliberately.
**Never bump it as a side effect of another change.** If a change looks like it warrants a bump,
say so and let the decision be made separately.

**Bumping breaks nothing** -- no golden files, no snapshots. `VersionTests` asserts the shape of
the version and the plumbing behind it, never a particular number.

## Licence

Mindfulness Reminder -- a break reminder for Windows.
Copyright (C) 2026 Tudor Carean

This program is free software: you may redistribute it and/or modify it under the terms of
the **GNU General Public License v3** as published by the Free Software Foundation, either
version 3 of the licence or (at your option) any later version.

It is distributed in the hope that it will be useful, but **WITHOUT ANY WARRANTY** -- without
even the implied warranty of merchantability or fitness for a particular purpose. See the GNU
General Public License for details.

The full text is in [LICENSE](LICENSE), and at <https://www.gnu.org/licenses/gpl-3.0.html>.
The same notice, in short, is behind **About** in the tray menu, under the version banner; the
build copies `LICENSE` into the output folder, so it travels with the executable.

Four things hold this together, and all four are pinned by `LicenseHeaderTests`:

- **Every source file starts with the notice** -- the SPDX line, the copyright, and all three
  paragraphs of the standard GNU notice. `//` for C#, one `<!-- -->` block for XAML. New files
  get it too; it is the first thing to write, not an afterthought.
- **About displays the copyright and the warranty disclaimer** (GPLv3 section 5(d)). It cannot
  go on the reminder window without defeating the point of the reminder, so it lives in the one
  place a user goes looking for it.
- **One source of truth for the copyright string**: `<Copyright>` in `Directory.Build.props`,
  read back through `AssemblyCopyrightAttribute` by `LicenseInfo.Copyright`. The literal in
  `LicenseInfo` is a fallback, not a second place to edit. It is also what Windows shows under
  the executable's Properties > Details.
- **`LICENSE` ships with the build**, so About's "next to the executable" stays true after a
  `dotnet publish`.

The year and the author are a deliberate choice, not something to roll forward automatically.
