# Mindfulness Reminder

A tray-resident reminder for Windows: every time you unlock the workstation it shows a small
always-on-top window that says **Take a quick break**.

## Behaviour

- Lives in the notification area. The tray menu has exactly two items: a **Run at Windows
  startup** checkbox and **Exit**.
- Registers itself for Windows startup on the very first run only, so unticking the checkbox
  sticks across restarts.
- The reminder appears on the monitor holding the mouse pointer, on top of everything, with a
  single **Close** button (Esc and Enter also close it).

## Design decisions

| Concern | Choice | Why |
|---|---|---|
| UI | WPF window + Windows Forms `NotifyIcon` in one process | `NotifyIcon` has no WPF equivalent in the BCL, and both live in the WindowsDesktop shared framework, so the hybrid needs no external package |
| Auto-start | `HKCU\...\CurrentVersion\Run` | No elevation, and the entry is one value that maps directly onto a checkbox |
| Auto-start truth | also reads `Explorer\StartupApproved\Run` | Task Manager's Startup tab vetoes entries without deleting them; a checkbox reading only the Run key would report "on" for an entry Windows ignores |
| Unlock detection | `SystemEvents.SessionSwitch` | A few lines behind `ISessionMonitor`; a `WTSRegisterSessionNotification` implementation can replace it without touching anything else |

## Layout

    src/MindfulnessReminder.Core   net8.0, no UI dependencies -- auto-start rules, unlock policy, placement math
    src/MindfulnessReminder.App    net8.0-windows, WinExe -- tray icon, reminder window, Windows adapters
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
