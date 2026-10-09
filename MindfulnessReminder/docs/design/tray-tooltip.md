# The tray tooltip

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "The tray tooltip". Where a paragraph
> became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.Core/TrayTooltip.cs`, `src/MindfulnessReminder.App/TrayIconHost.cs`,
> `src/MindfulnessReminder.App/App.xaml.cs`. A change to the code this describes means this note is
> updated in the same task, or deleted with the decision it explains.

## The tray tooltip

`NotifyIcon.Text` is capped at 63 characters and **throws** above it. That is the only
reason `TrayTooltip` is a separate pure type: the one thing worth testing is that the longest
string the app can produce still fits, and
`R062_The_longest_tooltip_the_app_can_produce_fits_the_tray_limit` pins it.

Minutes are rounded **up**, never below 1, so a 30-minute countdown reads "30 min" the
instant it restarts and never reads "0 min" through its last minute.

While reminders are paused the tooltip shows the reason **instead of** the countdown. Two
things make that the right way round. A held-back reminder is counting the retry gap, seconds
rather than minutes, and rounding that up would leave the tooltip reading "1 min" for the
whole of an hour-long call. And between reminders the countdown would be a promise the app
cannot keep -- the number would run down and nothing would appear.

`App.UpdateTooltip` compares the rendered string before writing it. The countdown ticks every
second but changes once a minute, and every write is a `Shell_NotifyIcon` call.

See ADR 0025; that a held-back reminder shows its reason is also ADR 0018.
