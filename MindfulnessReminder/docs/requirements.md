# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`), and the design
note of its area changes in the same task (the kit's rules "Requirements first" and "Design
notes"). Until 2026-10-09 the behaviour was described by the code, its comments, `CLAUDE.md` and
the user documentation; that day `CLAUDE.md` was split into this file, `docs/decisions/` and
`docs/design/` (ADR 0001).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status
  `dropped`. Gaps in the numbering are deliberate: each block of ten is one area.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`. `implemented` means a test
  named `R###_...` exists in `tests/MindfulnessReminder.Tests` (`RepoInvariantTests` checks it
  both ways). `accepted` means the behaviour is agreed and in the code, and no test carries the
  ID. On 2026-10-09 that is the App layer, `src/MindfulnessReminder.App/`, which has no unit
  tests by design (ADR 0008) and is checked by launching the application, with `--preview` or
  from the tray.
- Describe observable behaviour, with one concrete example that includes numbers. No adjectives.
- Error behaviour is a requirement too.
- Constants are quoted by their name in the code and their value on 2026-10-09; the code holds the
  value, this file the meaning.
- There are no validation figures (ADR 0005): every number below is a constant a unit test pins.

The user documentation of the same behaviour is `README.md`; the explanations behind each row are
in `docs/design/` and `docs/decisions/`.

## The tray, one instance, auto-start

| ID | Status | Requirement |
| --- | --- | --- |
| R-001 | accepted | The application has no main window and no taskbar entry: it lives in the notification area, with the icon `Assets/app.ico`, embedded in the executable and drawn at `SystemInformation.SmallIconSize`. Right-clicking it opens a menu of four items, in this order: `Settings...`, the checkbox `Run at Windows startup`, a separator, `About`, `Exit`. `Exit` ends the process. Example: hovering the icon right after a start shows `Mindfulness Reminder 1.4.0 - next in 30 min` (R-060). |
| R-002 | accepted | One copy runs per Windows session: the first claims the session's named mutex `Local\MindfulnessReminder.SingleInstance`, and a second copy started while it is held exits at once, with no message and no tray icon. `--preview` and `--detect` (R-045, R-080) do not claim the mutex and run beside the copy in the tray. Example: the Run entry starts a copy at logon; a double-click on the executable afterwards starts nothing. |
| R-003 | accepted | On the very first run, when `HKCU\Software\MindfulnessReminder` has no value `AutoStartInitialized`, the application registers itself for Windows startup: it writes the quoted path of the running executable (`RegistryAutoStartManager.Command`) as the value `MindfulnessReminder` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, writes `AutoStartInitialized` as `1`, and shows the balloon `Running in the tray. It now starts with Windows -- untick that in the tray menu to stop it.`, titled `Mindfulness Reminder`. Every later run leaves the registration alone, so an entry the user removed stays removed. A blank executable path is rejected. Example: an executable at `C:\Tools\MindfulnessReminder.exe` is registered as `"C:\Tools\MindfulnessReminder.exe"`, quotes included. |
| R-004 | accepted | The startup status (`RegistryAutoStartManager.GetStatus`) is `Disabled` when the Run value is missing or blank; `BlockedByWindows` when the Run value is there and the binary value of the same name under `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run`, where Task Manager records a veto, has a first byte whose lowest bit is set; `Enabled` otherwise. A veto without a Run value is `Disabled`. The checkbox is ticked only for `Enabled`, and reads `Run at Windows startup (disabled in Task Manager)` for `BlockedByWindows`. Example: a first byte of `0x02` is enabled, `0x03` is blocked. |
| R-005 | accepted | Clicking the checkbox while the status is `Enabled` deletes the Run value; in any other status it writes the Run value with the current path and deletes the Task Manager veto. When Windows refuses the change (`UnauthorizedAccessException`, `SecurityException`), a balloon says `Windows refused the startup setting change. Group policy may be managing it.`; either way the checkbox is then redrawn from the registry. Deleting a value that is not there is not an error. |
| R-006 | accepted | On every run after the first, a Run value that holds another path than the running executable's, compared without regard to case, is rewritten with the current path; a missing Run value is not created, and a Task Manager veto is left as it is. Example: after the executable moves from `C:\Tools` to `D:\Apps`, the first start by hand rewrites the entry to `"D:\Apps\MindfulnessReminder.exe"`, and Windows starts the moved copy from then on. |

## The unlock reminder

| ID | Status | Requirement |
| --- | --- | --- |
| R-010 | accepted | When the workstation is unlocked (`SessionSwitchReason.SessionUnlock`) and `ReminderSettings.UnlockEnabled` is true, the reminder window (R-070) appears with `UnlockMessage`, `WpfReminderPresenter.DefaultShowDelay` (600 ms) after the unlock. Unlocks before `ReminderCoordinator.Start` are ignored, `Start` twice subscribes once, and after `Dispose` the session events are no longer heard. Example: with the defaults, every unlock shows `Ground yourself`. |
| R-011 | accepted | An unlock within `ReminderCoordinator.DefaultDebounce` (2 s) of the last reminder shown, unlock or periodic, shows nothing; one after it shows the reminder again. Example: a reminder shown at 10:00:00 and closed at once; an unlock event at 10:00:01 shows nothing, and an unlock at 10:05:00 shows the reminder again. |
| R-012 | accepted | An unlock while a reminder window is open, or waiting out its 600 ms delay, brings that window to the front instead of opening a second, and its text stays as it was. |
| R-013 | accepted | With `UnlockEnabled` false an unlock shows nothing, brings no open window forward and does not read the activity monitor; it still restarts the periodic countdown (R-024). A save that switches it off takes effect from the next unlock. |
| R-014 | accepted | When the pauses (R-030 to R-032) would hold the unlock reminder back, it is skipped, not postponed: nothing appears for that unlock, and the countdown the unlock restarted runs on. Example: unlocking during a call with a 30-minute interval shows nothing, and the next reminder falls due 30 minutes later. |
| R-015 | accepted | `ForegroundState.UserAway` (`QUNS_NOT_PRESENT`, 1), which Windows reports behind a lock screen and for a moment after an unlock, never holds a reminder back (R-031): an unlock while it is reported shows the reminder. |
| R-016 | accepted | Locking the workstation never shows a reminder; it suspends the countdown (R-024). |

## The periodic reminder

| ID | Status | Requirement |
| --- | --- | --- |
| R-020 | accepted | `PeriodicEnabled` is true by default, and `IntervalMinutes` is `ReminderSettings.DefaultIntervalMinutes` (30), accepted from `MinIntervalMinutes` (1) to `MaxIntervalMinutes` (480). The countdown starts with the application: a new `IntervalSchedule` counts nothing until it is started, and `Start` puts the whole interval on the clock. Example: started at 09:00:00 with the defaults, the first reminder is due at 09:30:00. |
| R-021 | accepted | One timer ticks every `ReminderCoordinator.DefaultTickInterval` (1 s). On each tick the deadline is compared with `IClock.UtcNow`: before it nothing happens; at it or after it the reminder window appears with `PeriodicMessage`, and a whole interval is put on the clock again from that moment. `CountdownChanged` is raised on every tick and on every restart. `Dispose` stops and disposes the timer. Example: started at 09:00:00 with 30 minutes, the reminder appears on the tick of 09:30:00, then on the tick of 10:00:00. |
| R-022 | accepted | A deadline that passed while the machine slept fires on the first tick after it wakes, because the deadline is a time on the clock, not a count of ticks. Example: due at 09:30, asleep from 09:20 to 11:00: the reminder appears at 11:00, and the next is due at 11:30. |
| R-023 | accepted | A periodic reminder that falls due while a reminder window is open brings that window to the front with its text unchanged, and the next interval is armed as usual. |
| R-024 | accepted | Locking suspends the countdown: the timer stops and `Remaining` is null. Unlocking puts a whole interval on the clock from the unlock, whatever was left before the lock and however long or often it was locked; an unlock without a lock restarts it too; nothing restarts while the periodic reminder is off. Example: 10 minutes left, a lock, three hours away, an unlock at 14:00: the next reminder is due at 14:30. |
| R-025 | accepted | A clock moved backwards does not postpone the reminder: when the deadline is further away than the span last armed, `TryConsumeDue` restarts the countdown from now with a whole interval. A postponement longer than the interval (R-033) is not read as a jump, and a jump during a postponement still restarts. `Remaining` is never negative. Example: due at 10:00, the clock set back from 09:50 to 08:00: the reminder is due at 08:30, not 10:00. |
| R-026 | accepted | Saving the settings (`ApplySettings`) restarts the countdown from zero with the new interval. Switching the periodic reminder off stops the countdown and the timer at once; switching it back on puts a whole interval on the clock and starts the timer. Example: 25 of 30 minutes gone, the interval changed to 10: the next reminder is due 10 minutes after the save. |
| R-027 | accepted | With `PeriodicEnabled` false no periodic reminder appears and no timer runs; the unlock reminder is unaffected. With both reminders off nothing ever appears and no timer runs. |
| R-028 | accepted | An interval outside 1 to 480 minutes given to the schedule is clamped to the nearer bound, not taken literally. Example: 0 counts as 1 minute, 1000 as 480. |

## Pausing for a call or full screen

| ID | Status | Requirement |
| --- | --- | --- |
| R-030 | accepted | With `PauseWhileMicrophoneInUse` true, the default, a reminder is held back with the reason `MicrophoneInUse` while any application holds the microphone or the camera open. The reading (`WindowsActivityMonitor`) walks `SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone` and `\webcam`, in `HKEY_CURRENT_USER` and in `HKEY_LOCAL_MACHINE`, two levels deep (the packaged applications and `NonPackaged`), and counts a key whose `LastUsedTimeStart` is above 0 and whose `LastUsedTimeStop` is 0. The camera counts like the microphone. Example: Teams in a call with the microphone muted keeps the stream open, so the reminder is held back. |
| R-031 | accepted | With `PauseWhileFullScreen` true, the default, a reminder is held back with the reason `FullScreen` while `SHQueryUserNotificationState` reports full screen or a presentation. `UserNotificationState.FromShellState` maps 5 to `Normal`, 2, 3 and 7 to `FullScreen`, 4 to `Presenting`, 6 to `QuietHours`, 1 to `UserAway`, and anything else, a failed call included, to `Unknown`; only `FullScreen` and `Presenting` hold a reminder back. The constants are the values of the Windows header's `QUERY_USER_NOTIFICATION_STATE`. Example: a game in exclusive full screen (`QUNS_RUNNING_D3D_FULL_SCREEN`, 3) holds a reminder back; Focus Assist (`QUNS_QUIET_TIME`, 6) does not. |
| R-032 | accepted | The two switches are independent: each turns off only its own check, and both apply to both reminders, whether the periodic one is on or off. When both conditions hold, the reason given is `MicrophoneInUse`. With both switches off nothing is ever held back and the activity monitor is never read (`ReminderSettings.PausesForAnything` false). Both ship switched on. |
| R-033 | accepted | A periodic reminder that falls due while held back is postponed by `RetrySeconds` (`ReminderSettings.DefaultRetrySeconds`, 20, accepted from `MinRetrySeconds`, 5, to `MaxRetrySeconds`, 300) and looked at again after each gap, for as long as the condition lasts, with no cap. It appears at the first look after the condition ends, and the next one falls due a whole interval after that. Postponing does nothing while the periodic reminder is off or the workstation is locked, and a restart puts the whole interval back over a postponement. Example: due at 10:00:00 during a call that ends at 10:47:05, with the defaults: it is looked at every 20 seconds, appears at 10:47:20, and the next is due at 11:17:20. |
| R-034 | accepted | The activity monitor is read when a reminder falls due, once per retry, and otherwise every `ReminderCoordinator.DefaultActivityRefresh` (10 s) on the idle ticks, never on every tick. `Suppression` holds the reason of the last reading, so the tooltip (R-060) names a call or full screen within about ten seconds of its start, and goes back to the countdown within about ten seconds of its end, without waiting for a reminder. |
| R-035 | accepted | An unlock, a lock and a save of the settings clear the held-back state (`Suppression` back to `None`); after a lock or a save, the next idle tick reads the monitor again. |

## Messages and the Settings window

| ID | Status | Requirement |
| --- | --- | --- |
| R-040 | accepted | Each reminder has its own message, `UnlockMessage` and `PeriodicMessage`, both `ReminderSettings.DefaultMessage` (`Ground yourself`) by default and each at most `MaxMessageLength` (50) characters; each message box of the Settings window accepts at most that many characters (`MaxLength` bound to the constant). |
| R-041 | accepted | `ReminderSettings.NormalizeMessage` turns carriage returns, line feeds and tabs into spaces, trims, reads an empty or missing text as `Ground yourself`, and cuts a longer text at 50 characters and trims it again; `Normalize` applies it to both messages and clamps the interval and the retry gap to their bounds. Example: two spaces, `Breathe`, a tab, `out` and a line break become `Breathe out`; a 60-character text keeps its first 50 characters, less any spaces at the end. |
| R-042 | accepted | The Settings window, titled `Mindfulness Reminder Settings`, shows from top to bottom: the checkbox `Remind me when I unlock the computer` with its `Message` box; the checkbox `Remind me while I work`, `Every` N `minutes` with the hint `Between 1 and 480 minutes. The countdown restarts when you save.`, and its `Message` box; the checkboxes `Pause while the microphone or camera is in use` and `Pause during full screen and presentations`; `Check again every` N `seconds` with the hint `Between 5 and 300 seconds. The reminder waits, then looks again.`; `Save` and `Cancel`. It opens with the settings in force. A message box is greyed while its reminder is off, the interval while the periodic reminder is off, and the retry gap while neither pause is ticked. |
| R-043 | accepted | `Save` refuses, with the message shown in the window and the offending box focused and selected: a blank or over-long message of a reminder that is on (`Enter what the reminder should say, up to 50 characters.`); an interval that is not a whole number from 1 to 480 while the periodic reminder is on (`Enter a whole number of minutes between 1 and 480.`); a retry gap that is not a whole number from 5 to 300 while a pause is on (`Enter a whole number of seconds between 5 and 300.`). A field of a switched-off feature that does not read is saved as its default. `IsIntervalInRange`, `IsRetryInRange` and `IsMessageAcceptable` are the checks, and they agree with the clamp of `Normalize`. |
| R-044 | accepted | `Save` writes the settings to the registry (R-050), applies them at once (R-026) and closes the window; the new messages are used from the next reminder. `Cancel` closes it and changes nothing. |
| R-045 | accepted | `--preview` shows the reminder window once with `Ground yourself`, and the process exits when it is closed: no tray icon, no registry access, no session hook, no timer. `--preview` followed by a text shows that text after `NormalizeMessage` (R-041). Example: `MindfulnessReminder.exe --preview "Take a slow breath"`. |

## Settings storage

| ID | Status | Requirement |
| --- | --- | --- |
| R-050 | accepted | The settings live under `HKCU\Software\MindfulnessReminder`, beside `AutoStartInitialized`, as eight string values: `PeriodicRemindersEnabled`, `PeriodicIntervalMinutes`, `PauseWhileMicrophoneInUse`, `PauseWhileFullScreen`, `PauseRetrySeconds`, `UnlockReminderEnabled`, `UnlockMessage`, `PeriodicMessage`. Switches are `1` or `0`, numbers decimal text, messages the normalized text. Saving leaves `AutoStartInitialized` alone. Example: the defaults are written as `1`, `30`, `1`, `1`, `20`, `1`, `Ground yourself`, `Ground yourself`. |
| R-051 | accepted | Reading never fails: a missing value reads as its default (both reminders on, 30 minutes, both pauses on, 20 seconds, `Ground yourself` twice), and so does a switch that is neither `1` nor `0` and a number that does not parse; a number out of range is clamped, on reading and on saving; a blank message reads as `Ground yourself`, and an over-long one is cut at 50 characters. Example: a hand-edited `PeriodicIntervalMinutes` of `0` reads as 1, and one of `abc` as 30. |
| R-052 | accepted | Every field survives a save and a load unchanged, the periodic switch saved off included. |

## The tray tooltip

| ID | Status | Requirement |
| --- | --- | --- |
| R-060 | accepted | The tray icon's text is `TrayTooltip.Format` of the banner (R-090): `<banner> - next in <N> min` while the periodic countdown runs; the banner alone when there is none (the periodic reminder off, or the workstation locked); and, while a reminder is held back, `<banner> - paused while your mic is on` or `<banner> - paused for full screen` in place of the countdown. Example: `Mindfulness Reminder 1.4.0 - next in 12 min`. |
| R-061 | accepted | Minutes are rounded up and never shown below 1. Example: 29 minutes and 1 second left reads `30 min`; 20 seconds left reads `1 min`. |
| R-062 | accepted | The text never exceeds `TrayTooltip.MaxLength` (63) characters, the limit above which `NotifyIcon.Text` throws: a longer one is cut at 63. The longest text the application produces fits whole. |
| R-063 | accepted | The icon's text is written only when the rendered string changed: about once a minute, off a one-second tick. |

## The windows

| ID | Status | Requirement |
| --- | --- | --- |
| R-070 | accepted | The reminder window is 440 x 260, borderless, not resizable, not in the taskbar and `Topmost`, centred in the working area of the primary monitor (`WindowPlacement.CenterIn`, offset by the area's position on the virtual desktop, its left and top edges kept inside the area when the window is wider or taller than it), with one `Close` button that takes the focus. Example: on a primary monitor whose working area is 1920 x 1040 at 0, 0, the window's top-left corner is at 740, 390. |
| R-071 | accepted | `Close`, Esc and Space on the focused button close the reminder; Enter does not, and is swallowed (`OnPreviewKeyDown`), so a keystroke already on its way neither dismisses an unread reminder nor reaches the button. |
| R-072 | accepted | The message is drawn at 30 px in a block 332 wide; a message that would not fit is scaled down to fit (`Viewbox`, `StretchDirection="DownOnly"`), and the window keeps its size and its place. Example: `Ground yourself` is drawn at full size; a 50-character message that wraps to three lines is drawn smaller. |
| R-073 | accepted | The Settings and About windows are modeless and not topmost; opening either while it is open brings the open one forward, restored if it was minimised, instead of opening a second; a reminder that falls due while either is open appears with a working `Close` button. |
| R-074 | accepted | About, titled `About Mindfulness Reminder`, 480 wide, not resizable and centred on the screen, shows the application icon at 48 x 48, decoded from the icon's largest frame, and `LicenseInfo.AboutText` (R-091) in one block; its `Close` button answers both Enter and Esc. |
| R-075 | accepted | The Settings window, About and the tray menu are dark, whatever the Windows theme: their client area, their title bar (`DwmSetWindowAttribute` with attribute 20, then 19, and nothing more when both fail) and the menu. The reminder window is light. |
| R-076 | accepted | An unhandled error shows a dialog titled `Mindfulness Reminder stopped`, with the error's message, a blank line and the banner, and then ends the application. |

## The command line

| ID | Status | Requirement |
| --- | --- | --- |
| R-080 | accepted | `--detect` shows one reading in a dialog titled `Mindfulness Reminder detection` and exits: `Microphone in use: ` and `Camera in use: `, each followed by `no` or by `yes -- ` and the names of the applications holding the device; a blank line; `Shell notification state: ` with the raw number and, in brackets, what it maps to (R-031); `With the current settings: ` and the reason the saved settings give (`None`, `MicrophoneInUse` or `FullScreen`); a blank line and the banner. Example: `Shell notification state: 5 (Normal)`. |
| R-081 | accepted | `--detect <seconds>`, or `--detect=<seconds>`, reads once a second for that long, at most `MaxDetectDuration` (10 minutes), and shows the dialog only when the run ends: `Watched for <seconds> seconds.`, then `DetectionLog.Render`, one line per run of identical readings, `<m:ss> - <m:ss>   mic off, camera off, <state>   ->  <reason>` (`mic ON` and `camera ON` while held, minutes not wrapped at 60, a reading that comes back after another a new line), then a blank line and `A reminder would have been held back for <held> of the <total> readings.`, or `A reminder would have been shown at any point in this run.` when none was held back; with no reading, `No readings taken.` Example: `0:05 - 0:48   mic off, camera off, FullScreen   ->  FullScreen`. |
| R-082 | accepted | A duration that is not a whole number above 0 falls back to the single reading (R-080); a duration above ten minutes is cut to ten. |

## Version and licence

| ID | Status | Requirement |
| --- | --- | --- |
| R-090 | accepted | The version is `<Version>` in `Directory.Build.props`, 1.4.0 on 2026-10-09, read back as `VersionInfo.Value` from `AssemblyInformationalVersion`: three parts, never the four-part or the `+revision` form; no csproj declares a `<Version>` of its own. `VersionInfo.Banner` is `Mindfulness Reminder 1.4.0`. |
| R-091 | accepted | `LicenseInfo.AboutText` is the banner, a line break, the copyright (`LicenseInfo.Copyright`, `Copyright (C) 2026 Tudor Carean`, read from `<Copyright>` in `Directory.Build.props`, which names the author and the year), a blank line, and `LicenseInfo.Warranty`: `This program comes with ABSOLUTELY NO WARRANTY, not even the implied warranty of merchantability or fitness for a particular purpose.`, a blank line, and `It is free software: you may redistribute it and modify it under the terms of the GNU General Public License v3 -- or, at your option, any later version. The full text is in the LICENSE file next to the executable, and at https://www.gnu.org/licenses/gpl-3.0.html`. |
| R-092 | accepted | Every `.cs` and `.xaml` file of the three projects starts with the sixteen-line GPL notice (the SPDX line, the copyright, the three GNU paragraphs), commented with `//` in C# and in one `<!-- -->` block in XAML; `LICENSE` is the GNU GPL v3 text; `<PackageLicenseExpression>` is `GPL-3.0-or-later`. |
| R-093 | accepted | `LICENSE` is copied next to `MindfulnessReminder.exe`, in the build output and in a publish. |
