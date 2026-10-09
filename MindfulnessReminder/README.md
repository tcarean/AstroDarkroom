# Mindfulness Reminder

A tray-resident reminder for Windows. It shows a small always-on-top window that says
**Ground yourself** every time you unlock the workstation, and — since 1.1.0 — at a set
interval while you work. Since 1.2.0 it waits instead of interrupting a call or a full-screen
application. Each of the two reminders has its own switch and its own message in
**Settings...**.

## Behaviour

- Lives in the notification area. The tray menu has four items: **Settings...**, a **Run at
  Windows startup** checkbox, **About**, and **Exit**.
- Registers itself for Windows startup on the very first run only, so unticking the checkbox
  sticks across restarts.
- The reminder always appears on the primary monitor, on top of everything, with a
  single **Close** button. Esc closes it; **Enter deliberately does not** — see
  [Periodic reminders](#periodic-reminders).
- There are two reminders, the one on unlock and the periodic one, and they are independent:
  each has its own switch and its own message — see
  [Two reminders, two messages](#two-reminders-two-messages).
- The tray tooltip counts down to the next reminder:
  `Mindfulness Reminder 1.4.0 - next in 12 min`, or says why one is being held back:
  `Mindfulness Reminder 1.4.0 - paused while your mic is on`.
- Since 1.2.0 a reminder waits rather than interrupting a call or a full-screen application —
  see [Pausing](#pausing-for-what-you-are-doing).
- Since 1.3.0 the **Settings**, **About** and tray-menu surfaces are dark. The reminder itself
  is not, and that is deliberate: a dark rectangle appearing over dark work is easy to miss,
  and being seen is the whole point of it.

## Two reminders, two messages

Added in 1.4.0. **Remind me when I unlock the computer** and **Remind me while I work** are
two switches in **Settings...**, both **on** by default, and under each is the **Message**
that reminder shows. Both say *Ground yourself* until you change them; up to 50 characters
each. A message box is greyed out while its reminder is off, and a reminder that is switched
on will not save with an empty message.

The two switches are independent. Turning the unlock reminder off does not stop the
countdown restarting on unlock, so the periodic reminder still comes a whole interval after
you come back; turning the periodic one off does not stop the unlock reminder. The two
pauses below them hold back whichever reminder is about to appear, so they sit outside both.

A long message is scaled down to fit the window rather than growing it, so the reminder keeps
the same size and place on screen whatever it says. To see what a message will look like
before saving it:

```
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --preview "Take a slow breath"
```

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

## Pausing for what you are doing

Added in 1.2.0. Two independent switches in **Settings...**, both **on** by default, that
hold a reminder back instead of putting it over the top of something:

- **Pause while the microphone or camera is in use** — a call, a meeting, a recording.
- **Pause during full screen and presentations** — a game, a film, a shared screen, or
  Windows presentation mode.

A held-back periodic reminder is **postponed**, and the conditions are looked at again after
**Check again every N seconds** (default 20, anything from 5 to 300) — for as long as it
takes. A held-back *unlock* reminder is simply **skipped**: the unlock restarted the schedule
on its way past, so one is coming anyway.

**The tray tooltip says so while it is happening**, not only after a reminder has been held
back. Join a call and within ten seconds hovering the icon reads
`Mindfulness Reminder 1.4.0 - paused while your mic is on` instead of a countdown — which is
honest, because that countdown would otherwise run down to a reminder that is not going to
arrive. Leave the call and the countdown comes back just as quickly.

The retry gap is shared by both switches, and stays editable while either is ticked.

### How it detects them

**The microphone and camera come from the usage record Windows keeps for its own privacy
indicator** — the microphone icon in the tray. Every application that opens a capture device
gets a key under
`HKCU\...\CapabilityAccessManager\ConsentStore\{microphone,webcam}` with a
`LastUsedTimeStart` and a `LastUsedTimeStop`, and while the stream is open the stop time is
zero.

That, and not window titles, is what makes this robust. Reading the device works for software
nobody thought of — Zoom, Teams on the desktop or in a browser tab, a Slack huddle, Meet,
Webex, Discord — and it is still right when you are on mute, because those applications keep
the stream open. Matching window titles instead would have needed a table per application,
would have broken on the next release of each, and would have missed a Slack huddle
altogether: a huddle lives inside the main Slack window and has no title of its own to match.

**Full screen comes from `SHQueryUserNotificationState`**, which is the same question Windows
asks itself before showing a notification. `QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`,
`QUNS_APP` and `QUNS_PRESENTATION_MODE` hold a reminder back.

`QUNS_NOT_PRESENT` deliberately does **not**. It is what Windows reports behind a lock screen,
and it can still be the answer for a moment after an unlock — long enough to swallow the
unlock reminder, which is the oldest thing this app does. Nothing is lost by ignoring it: the
schedule is suspended for the whole of a lock anyway. `QUNS_QUIET_TIME` (Focus Assist) is
mapped but unused, and any state a future Windows adds reads as unknown and holds nothing
back.

### What it will and will not catch

Anything holding the microphone open counts, whether or not it is a meeting — a voice channel
left open, dictation, screen-recording software. That is the intended reading (if the
microphone is live, do not interrupt), but it does mean a reminder can be held back for a long
time. There is no cap on purpose; the tooltip says what is happening instead, so the reason is
one hover away.

The one thing it misses is a meeting you only listen to, with the microphone never opened.

### Checking it on your machine

The feature cannot be exercised without a real call or a real game, so there is a switch that
shows the raw readings:

```
MindfulnessReminder.exe --detect
```

It reports which application is holding the microphone or camera, the raw number
`SHQueryUserNotificationState` returned and what it was read as, and the verdict under your
current settings — then exits.

**For the full-screen check, give it a number of seconds instead:**

```
MindfulnessReminder.exe --detect 60
```

A reading taken the instant the switch runs is no use there — getting to a terminal to run it
means leaving full screen, which destroys the state being measured. With a duration it takes
one reading a second and shows a timeline afterwards, so you start it, launch the game, play
for a while, quit, and read what it saw:

```
0:00 - 0:04   mic off, camera off, Normal       ->  None
0:05 - 0:48   mic off, camera off, FullScreen   ->  FullScreen
0:49 - 1:00   mic off, camera off, Normal       ->  None

A reminder would have been held back for 44 of the 61 readings.
```

The message box waits until the run is over, so it cannot disturb what is being measured.
Neither `--detect` nor `--preview` claims the single-instance mutex, so both run happily
alongside the copy already in the tray. Anything over ten minutes is clamped to ten.

## Design decisions

| Concern | Choice | Why |
|---|---|---|
| UI | WPF window + Windows Forms `NotifyIcon` in one process | `NotifyIcon` has no WPF equivalent in the BCL, and both live in the WindowsDesktop shared framework, so the hybrid needs no external package |
| Auto-start | `HKCU\...\CurrentVersion\Run` | No elevation, and the entry is one value that maps directly onto a checkbox |
| Auto-start truth | also reads `Explorer\StartupApproved\Run` | Task Manager's Startup tab vetoes entries without deleting them; a checkbox reading only the Run key would report "on" for an entry Windows ignores |
| Unlock detection | `SystemEvents.SessionSwitch` | A few lines behind `ISessionMonitor`; a `WTSRegisterSessionNotification` implementation can replace it without touching anything else |
| Periodic schedule | one `DispatcherTimer` at 1 s + a deadline in `IntervalSchedule` | Survives sleep, feeds the tooltip off the same tick, and makes "reset on unlock" a single line |
| Settings storage | strings under `HKCU\Software\MindfulnessReminder` | The key the auto-start marker already uses, and strings keep `IRegistryStore` at the four methods it had |
| In a call | per-app usage under `CapabilityAccessManager\ConsentStore` | Windows' own privacy indicator reads the same record, so it covers every conferencing app including browser tabs; window titles would need a table per app and would miss a Slack huddle entirely |
| Full screen | `SHQueryUserNotificationState` | One P/Invoke, no dependency, and it is the question Windows asks itself before it shows a notification |
| Held-back reminder | postponed by a retry gap, re-checked | The alternative is asking on every one-second tick, which would walk the registry 1800 times per reminder |
| Tooltip freshness | the reading refreshes every 10 s when idle | Reading only when a reminder falls due makes the feature invisible for up to a whole interval; 10 s is faster than anyone can hover the icon after joining a call, and 200× cheaper than every tick |
| Settings window | modeless, hand-validated `TextBox` | A modal `ShowDialog` would disable every other window, so a reminder falling due would appear with a dead Close button; and WPF has no numeric spinner in the BCL, which is not worth a package |
| Reminder text | fixed-size window, text scaled down only when it must | A 50-character message can wrap to one more line than fits above the Close button; shrinking it keeps the window the same size and in the same place, and a `Viewbox` that only scales down leaves every shorter message, the default included, exactly as it was |
| Dark theme | the settings window, About and the tray menu, not the reminder | The three you go to on purpose are dark; the reminder stays a pale card, because a dark rectangle appearing over dark work is easy to miss and the point of it is to be seen. Written by hand -- control templates for WPF, a `ToolStripRenderer` for the menu -- so it costs no package |
| About | a window, not a `MessageBox` | A Win32 dialog cannot be themed, and a white About box opening out of a dark tray menu reads as a different program. Being a window is also what lets it carry the app icon |

## Layout

    src/MindfulnessReminder.Core   net8.0, no UI dependencies -- auto-start rules, unlock and interval policy, when to pause, placement math
    src/MindfulnessReminder.App    net8.0-windows, WinExe -- tray icon, reminder/settings/about windows, the dark theme, Windows adapters (registry, session, timer, microphone and full-screen readings)
    tests/MindfulnessReminder.Tests xunit

## Build and run

```
dotnet build -c Release
dotnet test -c Release
```

The full gate, the one the `pre-commit` hook runs, is `scripts/verify.ps1` (see
"Development" below).

To see the reminder window without locking the machine:

```
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --preview
```

`--preview` shows the window once and exits: no tray icon, no registry access, no session hook.
With a text after it, that text is the message; without one it shows the default.

To see what the pause checks can currently detect:

```
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --detect
src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --detect 60
```

Without a number `--detect` reports one reading and the verdict; with one it watches for that
many seconds and reports a timeline — which is the only way to test the full-screen check, as
running the switch at all means leaving full screen. There is no equivalent switch for the
settings or About windows; both open from the tray menu.

## Deployment note

The Run entry records the path of the executable that was running when auto-start was switched
on. Moving the app is handled -- each launch repairs a stale path -- but the first launch after
a move has to happen manually. Publish somewhere stable, e.g.

```
dotnet publish src\MindfulnessReminder.App -c Release -o %LOCALAPPDATA%\MindfulnessReminder
```

## Development: the gate, the documentation and Claude Code

Since 2026-10-09 the project follows the process of `ClaudeCodeStarterKit` (the folder beside
this one in the `photo` repository; the guide with the reasoning behind each piece is `GHID.md`
there, in Romanian), like FlatCheck, AstroPhotoAnalyzer and ImageVisualCompare. In short, what
is enforced mechanically:

- **`scripts/verify.ps1` is the gate**: the scripts identical with the kit's, the test data
  manifest, the decision records' headers, the design notes (each in the index, with its files
  named in its `Covers:` sentence), restore, `dotnet format` (check only), the build with
  warnings as errors, all the tests. About 20 seconds. The git `pre-commit` hook at the
  repository root runs it on every commit that touches the project, the `pre-merge-commit` hook
  on every clean merge, the Claude Code hook before any `git commit` of the model, and
  `scripts/release.ps1`. Without it green no commit goes in.

  ```
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
  ```

- **The commit message** is one line, in English, of at most 100 characters.
- **`scripts/`, `.claude/skills/`, `.claude/agents/` and `.claude/rules/kit/` are the kit's**,
  byte for byte, and are not edited here; what is specific to the project as values is in
  `kit.json`, the steps it adds to the skills are in `docs/skills.md`.
- **The behaviour is in `docs/requirements.md`**, with `R-NNN` IDs carried in the test names;
  `RepoInvariantTests` requires every `implemented` requirement to have a test with its ID, and
  the reverse. The old `CLAUDE.md` was split on 2026-10-09 into requirements, decisions and
  design notes; nothing was lost, it was moved.
- **The decisions are in `docs/decisions/`**, one record per decision, with the alternatives
  rejected; those taken before 2026-10-09 (the periodic reminder's timer and deadline, reading
  the device instead of window titles, the dark theme merged per window, among others) were
  written down after the fact. The "Design decisions" table above is the short form of the same
  choices.
- **There are no validation figures**: the application has no numeric output and no input
  data, and `docs/validation.json` lists no case; `docs/validation.md` says what would become
  one.
- **The explanations behind the code** are in `docs/design/`, with the index in
  `docs/design/README.md`; `CLAUDE.md` holds only the project's rules, and the process rules are
  the kit's, in `.claude/rules/kit/process.md`.
- **The version** is bumped by the owner, in `Directory.Build.props`. A release is made on
  `main`, with `scripts/release.ps1 -Version X.Y.Z`, which runs the gate, closes the CHANGELOG
  section and publishes the application, framework-dependent, into `artifacts/X.Y.Z/`. The
  commit and the tag stay the owner's.
- In Claude Code: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`.

## Versioning

There is no console to print a banner on, so the app identifies itself in the three places a
user can actually see it:

| where | what it shows |
|---|---|
| tray tooltip | `Mindfulness Reminder 1.4.0 - next in 12 min` -- hovering the icon says which build is running, and when the next reminder is due |
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
