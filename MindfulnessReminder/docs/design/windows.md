# The windows the app shows

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "Windows the app shows". Where a
> paragraph became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.App/BreakWindow.xaml`, `src/MindfulnessReminder.App/BreakWindow.xaml.cs`,
> `src/MindfulnessReminder.App/SettingsWindow.xaml.cs`,
> `src/MindfulnessReminder.App/AboutWindow.xaml`,
> `src/MindfulnessReminder.App/WpfReminderPresenter.cs`, `src/MindfulnessReminder.App/App.xaml.cs`.
> A change to the code this describes means this note is updated in the same task, or deleted with
> the decision it explains.

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

See ADR 0026; About's own reasons are ADR 0032.
