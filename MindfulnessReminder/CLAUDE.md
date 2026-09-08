# Mindfulness Reminder

A tray-resident break reminder for Windows. It shows an always-on-top "Ground yourself"
window on every workstation unlock, and at a configurable interval while the user works.

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
  - `ReminderSettings.cs` — `record(bool PeriodicEnabled, int IntervalMinutes)`, the bounds,
    and `Normalize()`. Everything that builds settings goes through `Normalize`.
  - `ISettingsStore.cs` / `RegistrySettingsStore.cs` — settings persistence over
    `IRegistryStore`, under the key the auto-start marker already uses.
  - `TrayTooltip.cs` — the hover text, as a pure function. It is a separate type because of
    the 63-character limit; see "The tray tooltip".
  - `IPeriodicTimer.cs`, `IClock.cs`, `ISessionMonitor.cs`, `IReminderPresenter.cs`,
    `IRegistryStore.cs`, `IAutoStartManager.cs` — the seams. Each exists so a rule can be
    tested off a real machine; none has a second implementation in production.
  - `RegistryAutoStartManager.cs` / `StartupRegistrar.cs` — auto-start over `HKCU\...\Run`,
    plus the once-only registration on first run.
  - `WindowPlacement.cs`, `Version.cs`, `LicenseInfo.cs`, `AutoStartStatus.cs`.
- `src/MindfulnessReminder.App/` — `net8.0-windows`, `WinExe`, `AssemblyName` is
  `MindfulnessReminder`, so the binary is `MindfulnessReminder.exe`. A **thin adapter layer**:
  every file here is either a window or a Windows API behind a Core interface. Nothing here
  decides anything, which is why none of it is unit-tested.
  - `App.xaml.cs` — composition root. `ShutdownMode` is `OnExplicitShutdown`; there is no
    main window. Also holds the single-instance mutex, the crash dialog, and `--preview`.
  - `TrayIconHost.cs` — the icon and its four-item menu.
  - `BreakWindow.xaml(.cs)` — the reminder. `SettingsWindow.xaml(.cs)` — the settings.
  - `SystemEventsSessionMonitor.cs`, `DispatcherPeriodicTimer.cs`,
    `WpfReminderPresenter.cs`, `WindowsRegistryStore.cs` — the adapters.
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

## The tray tooltip

`NotifyIcon.Text` is capped at 63 characters and **throws** above it. That is the only
reason `TrayTooltip` is a separate pure type: the one thing worth testing is that the longest
string the app can produce still fits, and
`The_longest_tooltip_the_app_can_produce_fits_the_tray_limit` pins it.

Minutes are rounded **up**, never below 1, so a 30-minute countdown reads "30 min" the
instant it restarts and never reads "0 min" through its last minute.

`App.UpdateTooltip` compares the rendered string before writing it. The countdown ticks every
second but changes once a minute, and every write is a `Shell_NotifyIcon` call.

## Windows the app shows

| window | modality | focus | why |
|---|---|---|---|
| `BreakWindow` | modeless, `Topmost` | takes it | It has to be seen. Shown after a ~600 ms delay because a window created into a freshly unlocked desktop lands behind everything. |
| `SettingsWindow` | **modeless**, not topmost | ordinary | A modal `ShowDialog` disables every other window in the process, so a reminder falling due while it sat open would appear with a dead Close button. Single-instance: reopening raises the existing one. |

`BreakWindow` is deliberately **not** `IsDefault` on its Close button, and it swallows Enter
in `OnPreviewKeyDown`. Since the reminder became periodic it arrives mid-work and takes the
focus, so the next Enter already on its way would both dismiss an unread reminder and be lost
to whatever was being typed. It must be *Preview*: plain `KeyDown` bubbles up from the focused
button, which would already have handled the key. Esc and Space still close it — both far
less likely mid-sentence than Enter, but if that ever changes, the same handler is the place.

## Settings storage

`HKCU\Software\MindfulnessReminder`, the key `StartupRegistrar` already keeps
`AutoStartInitialized` under. `PeriodicRemindersEnabled` is `"1"`/`"0"` and
`PeriodicIntervalMinutes` is a decimal string — **strings, not DWORDs**, so `IRegistryStore`
stays the four methods it had and `FakeRegistryStore` needed no change.

Reading is deliberately forgiving. A missing value is a fresh machine, a corrupt one is a
hand-edited registry, and neither is a reason to fail to start; both land on the defaults
(**on**, 30 minutes). Out-of-range values are **clamped rather than rejected** — a hand-edited
`0` would otherwise mean a reminder on every tick of the timer.

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
hook, no timer. There is no equivalent switch for the settings window.

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
