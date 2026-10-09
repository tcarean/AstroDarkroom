# Two reminders, two switches, two messages

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "Two reminders, two switches, two
> messages". Where a paragraph became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.Core/ReminderSettings.cs`,
> `src/MindfulnessReminder.Core/ReminderCoordinator.cs`,
> `src/MindfulnessReminder.Core/IReminderPresenter.cs`,
> `src/MindfulnessReminder.App/WpfReminderPresenter.cs`,
> `src/MindfulnessReminder.Core/RegistrySettingsStore.cs`,
> `src/MindfulnessReminder.App/SettingsWindow.xaml`,
> `src/MindfulnessReminder.App/SettingsWindow.xaml.cs`,
> `src/MindfulnessReminder.App/BreakWindow.xaml`, `src/MindfulnessReminder.App/BreakWindow.xaml.cs`,
> `src/MindfulnessReminder.App/App.xaml.cs`. A change to the code this describes means this note is
> updated in the same task, or deleted with the decision it explains.

## Two reminders, two switches, two messages

Added in 1.4.0. The unlock reminder got its own switch, and each reminder got its own
message. `UnlockEnabled`, `UnlockMessage` and `PeriodicMessage` on `ReminderSettings`, all
init-only, all with the old behaviour as their default.

**The two are independent, and the tests say so from both sides.**
`Switching_the_periodic_reminder_off_leaves_the_unlock_reminder_alone` and
`Switching_the_unlock_reminder_off_shows_nothing_on_unlock`. Nothing about one switch is
allowed to read the other.

**The unlock gate sits AFTER the schedule restart and BEFORE everything else** in
`HandleUnlock`. The restart belongs to the periodic reminder -- with the unlock reminder off,
a lock of three hours is still followed by a whole interval, not by a deadline set before the
lock. Everything below the gate is the unlock reminder acting: the already-showing raise,
the debounce, the activity read and the window. With the switch off none of it runs, and in
particular the registry is not walked to decide whether a reminder that is not going to
appear would have been held back.

See ADR 0021 for this paragraph and the two above it.

**The coordinator picks the text, the presenter draws it.** `IReminderPresenter.Show` takes
the message, because the coordinator is the one thing that knows which reminder this is.
`BringToFront` takes nothing: a window already open -- or already in its 600 ms queue --
keeps the text it opened with rather than having it swapped under the user's eyes when the
other reminder falls due over it.

See ADR 0022.

**Messages are normalized like numbers are clamped.** `NormalizeMessage` turns line breaks
and tabs into spaces, trims, reads nothing at all as `DefaultMessage`, and cuts at
`MaxMessageLength` (50). The registry goes through it silently; the dialog refuses a blank
message with `IsMessageAcceptable` instead, but only for a reminder that is switched on --
the same rule the numbers follow, for the same reason. The `TextBox` `MaxLength` reads the
constant through `x:Static`, so the box and the clamp cannot drift apart.

See ADR 0023.

**The window stays a fixed size, and the text scales down to fit.** 50 characters can wrap
to three lines at 30 px, one more than fits above the Close button; a `Viewbox` with
`StretchDirection="DownOnly"` shrinks such a message and leaves every shorter one, the
default included, drawn exactly as before. Growing the window instead would have meant
placing it after layout rather than in `OnSourceInitialized`. The `TextBlock` inside the
`Viewbox` has an explicit `Width`; without it a `Viewbox` measures its child with unlimited
space and nothing wraps.

See ADR 0024.

`--preview "some text"` shows a message without saving it, through the same normalization
the saved one gets. It is the way to see what a candidate message looks like at the cap.

See ADR 0023.
