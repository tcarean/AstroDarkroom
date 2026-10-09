# 0016. Being on a call is read from the device, the capture record Windows keeps for its own privacy indicator, never from window titles

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Pausing for what the user is doing", the
paragraph "Reading the device, not the window title, is the whole point"), `README.md` ("How it
detects them", its first two paragraphs; "What it will and will not catch"; the "Design
decisions" row "In a call") and the comments of `WindowsActivityMonitor.cs`; the decision itself
dates from 89de6c8 (pausing for a call or full screen, 1.2.0, 2026-09-08). Since 1.2.0 a
reminder waits rather than interrupting a call, behind the switch "Pause while the microphone or
camera is in use", on by default. The calls come from any number of applications: "Zoom, Teams
(desktop and browser), a Slack huddle, Meet, Webex, Discord, and whatever ships next"
(`CLAUDE.md`). A Slack huddle "lives inside the main Slack window and has no title of its own",
and a user on mute is still on the call. `CLAUDE.md` and the README name the record under `HKCU`
only; the adapter has read the same path under `HKLM` as well since 89de6c8.

## Decision

- Whether the microphone or the camera is in use is read from the per-application capture usage
  Windows records under
  `SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone`
  and `...\webcam`, "the same record its own tray privacy indicator reads" (`CLAUDE.md`). A key
  counts as open when its `LastUsedTimeStart` is above zero and its `LastUsedTimeStop` is present
  and zero (`IsOpenNow`).
- `WindowsActivityMonitor` reads that path under `HKEY_CURRENT_USER`, which "holds what the
  signed-in user runs, which is every meeting application", and under `HKEY_LOCAL_MACHINE`,
  which "holds services, which can hold the microphone open on their behalf" (the comment on
  `Hives`). It walks two levels down (`ScanDepth`), which reaches classic applications under
  `NonPackaged`, Store applications, and the further key per app id some of those keep.
- A hive or a key it cannot read adds nothing: the adapter fails open, and `Read` never throws.
  It interprets nothing either: it hands `SuppressionPolicy` an `ActivitySnapshot`, in which the
  microphone and the camera count the same (`CaptureInUse`).
- No window title and no process name is matched, and a table of window titles is not to be
  reintroduced: "Do not reintroduce one." (`CLAUDE.md`).

## Alternatives rejected

- **A table of window titles.** It "needs an entry per application, breaks on each new release,
  and misses a Slack huddle outright, because a huddle lives inside the main Slack window and
  has no title of its own" (`CLAUDE.md`).
- **Matching process names.** A list of known software again; reading the device "is what makes
  this work for software nobody thought of" (the `WindowsActivityMonitor` summary).
- **A key without a stop time read as still open.** Requiring the stop time "to be PRESENT and
  zero" is what stops "a half-written key, or one left behind by an application that crashed
  holding the microphone, from suppressing every reminder from then on" (the `IsOpenNow`
  comment).
- **Failing closed on a hive that cannot be read.** Such a hive "is a reason to show the
  reminder anyway; the alternative is a reminder that silently never appears again, which is the
  worse of the two failures by a long way" (the comment in `Holders`).

## Consequences

- Anything holding the microphone or the camera open counts, meeting or not: "a voice channel
  left open, dictation, screen-recording software", which the README calls "the intended reading
  (if the microphone is live, do not interrupt)". A reminder can then be held back for a long
  time, with no cap (ADR 0018).
- It stays right on mute, "because those applications keep the stream open" (README). What it
  misses is "a meeting you only listen to, with the microphone never opened" (README).
- A reading walks registry keys, which is why it is never taken on the one-second tick (ADR
  0019).
- Guards: none by test for the reading, which lives in `src/MindfulnessReminder.App/` (ADR
  0008); launch the application with `--detect` during a call, which names the application
  holding the device (ADR 0020). `SuppressionPolicyTests` pins what the reading means in Core: a
  live microphone holds a reminder back, a live camera counts the same, and the switch turns off
  only its own check.

Source: CLAUDE.md ("Pausing for what the user is doing"); README.md ("How it detects them", "What
it will and will not catch", "Design decisions" row "In a call");
src/MindfulnessReminder.App/WindowsActivityMonitor.cs;
src/MindfulnessReminder.Core/ActivitySnapshot.cs; src/MindfulnessReminder.Core/IActivityMonitor.cs.
