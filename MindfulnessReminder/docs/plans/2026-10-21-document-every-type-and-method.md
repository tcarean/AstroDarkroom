# Document every type and method in the code

## Context

On 2026-10-21 the kit's rule "Documentation comments" (the kit's ADR 0035, merged as 2b2235f)
started asking for an XML documentation comment on every type and method in the code: a
`<summary>`, a `<param>` for each parameter, a `<returns>` for a value. Code written before the
rule gets its comments in a task of its own per project. The owner asked for Mindfulness
Reminder's: "Backfill the XML documentation comments in MindfulnessReminder's code folders ...
about 8 types and 150 methods to touch. Plan first; one or two sessions."

The outcome: every type and method under `src/` documented, no code changed, the gate green.

## Facts checked (2026-10-21, `main` at 2b2235f)

- **The gaps, measured** with the kit task's scratch counter (`doccount`, a Roslyn syntax walk
  over the folders `kit.json` lists, skipping `bin/`, `obj/` and `*.g.cs`). It counts as
  documented a `<summary>` or `<inheritdoc/>`, and as incomplete a comment without every
  `<param>` or, for a value, the `<returns>`. In `src/` (35 files, 3,875 lines):
  - types: 42, of which 4 have no comment and 4 lack the `<param>` tags of a positional record;
  - methods: 158, of which 99 have no comment and 51 lack tags (41 a `<param>`, 29 the
    `<returns>`); 58 return a value, none a plain `Task`;
  - properties, indexers and events: 57, of which 33 have no comment (not required).
  - That is 8 types and 150 methods to touch: 7 and 71 in Core (23 files), 1 and 79 in App (12).
- **The existing comments are `<summary>` only.** There is no `<param>`, `<returns>`,
  `<inheritdoc/>`, `<exception>` or `<remarks>` in `src/`. No code comment cites an ADR or a
  requirement. `<c>true</c>` appears twice; `langword` never.
- **The compiler already checks a wrong comment.** `GenerateDocumentationFile` is on for all three
  projects and warnings are errors. So CS1570 (malformed XML), CS1572 (a `<param>` naming no
  parameter), CS1573 (a parameter untagged while another is tagged), CS1574 (a `cref` that does
  not resolve) and CS0419 (an ambiguous `cref`) fail the build. CS1591 stays muted
  (`Directory.Build.props`; ADR 0003's Status already records the kit's ADR 0035).
- **Whitespace:** LF, UTF-8, `trim_trailing_whitespace` (`.editorconfig`, checked by
  `dotnet format`). A blank line inside a comment is `///` with nothing after it.
- **Six existing doc comments describe an earlier state of the code.** The adoption plan
  (`docs/plans/2026-10-09-adopt-the-starter-kit.md`, "What deviated", findings) lists them, and
  each ADR below records the truth in its Context. No design note, README line, `CLAUDE.md` line
  or requirement quotes them.
- **Branches:** `git worktree list` shows no branch for this task.

## What each file needs (U: no comment; T: tags missing)

**Core (`src/MindfulnessReminder.Core/`)**
- `ActivitySnapshot.cs`: `ActivitySnapshot` T (3 record parameters)
- `AutoStartStatus.cs`: `AutoStartStatus` U
- `DetectionLog.cs`: `DetectionSample` T (3); `Render`, `SameReading`, `Line`, `Describe` U; `Clock` T
- `ForegroundState.cs`: `FromShellState` T
- `IActivityMonitor.cs`: `IActivityMonitor.Read` T; `IdleActivityMonitor.Read` U
- `IAutoStartManager.cs`: the interface, `GetStatus`, `Enable`, `Disable` U; `RepairPathIfNeeded` T
- `IClock.cs`: `SystemClock` U
- `IPeriodicTimer.cs`: `Stop` U
- `IRegistryStore.cs`: `GetString`, `GetBinary`, `SetString` U; `DeleteValue` T
- `IReminderPresenter.cs`: `Show` T; `BringToFront` U
- `ISessionMonitor.cs`: `Start` U
- `ISettingsStore.cs`: `Load` T; `Save` U
- `IntervalSchedule.cs`: constructor U; `Restart`, `Postpone`, `Resume`, `Apply`, `TryConsumeDue`,
  `Remaining` T
- `LicenseInfo.cs`: `ReadCopyright` U
- `RegistryAutoStartManager.cs`: constructor, `GetStatus`, `Enable`, `Disable`,
  `RepairPathIfNeeded`, `Quote` U; `IsApprovalRevoked` T
- `RegistrySettingsStore.cs`: constructor, `Load`, `Read`, `Save`, `ReadNumber`, `WriteFlag`,
  `WriteNumber` U; `ReadFlag`, `ReadText` T
- `ReminderCoordinator.cs`: constructor, `Start`, `OnUnlocked`, `OnLocked`, `OnTick`,
  `HandleUnlock`, `HandleLock`, `HandleTick`, `Dispose` U; `ApplySettings`, `Evaluate`,
  `RefreshSuppression` T
- `ReminderSettings.cs`: `ReminderSettings` T (2); `Normalize`, `NormalizeMessage`,
  `IsIntervalInRange`, `IsRetryInRange`, `IsMessageAcceptable` T
- `StartupRegistrar.cs`: constructor U; `EnsureRegisteredOnFirstRun` T
- `SuppressionPolicy.cs`: `Evaluate` U; `Occupies` T
- `TrayTooltip.cs`: `Format`, `MinutesLeft` T
- `Version.cs`: `Read` U
- `WindowPlacement.cs`: `WorkArea` T (4); `CenterIn` U

**App (`src/MindfulnessReminder.App/`)**
- `AboutWindow.xaml.cs`: constructor, `OnCloseClick` U; `OnSourceInitialized`, `LoadAppIcon` T
- `App.xaml.cs`: `OnStartup`, `ShowPreview`, `Report`, `OnDispatcherUnhandledException`,
  `ReportCrash`, `OnExit` U; `PreviewMessage`, `DetectDuration`, `ShowDetection`,
  `ResolveExecutablePath` T
- `BreakWindow.xaml.cs`: `BreakWindow` U; `OnSourceInitialized`, `OnCloseClick`, `OnKeyDown` U;
  constructor, `OnPreviewKeyDown` T
- `DarkMenuRenderer.cs`: constructor, `OnRenderToolStripBackground`, `OnRenderToolStripBorder`,
  `OnRenderMenuItemBackground`, `OnRenderItemText`, `OnRenderSeparator` U; `Apply`,
  `OnRenderImageMargin`, `OnRenderItemCheck` T
- `DispatcherPeriodicTimer.cs`: constructor, `Stop`, `OnTick`, `Dispose` U
- `ImmersiveDarkMode.cs`: `DwmSetWindowAttribute` U; `Apply` T
- `SettingsWindow.xaml.cs`: constructor, `OnSaveClick`, `OnCancelClick`, `SyncRetryAvailability`,
  `TryReadInterval`, `TryReadRetry`, `ShowError`, `ClearError` U; `OnSourceInitialized`,
  `OnPauseToggled`, `TryReadMessage`, `Refuse`, `OnIntervalPreviewTextInput`, `OnAnyTextChanged` T
- `SystemEventsSessionMonitor.cs`: constructor, `Start`, `OnSessionSwitch`, `Dispose` U
- `TrayIconHost.cs`: constructor, `ShowBalloon`, `OnAutoStartClick`, `RefreshAutoStartItem`,
  `LoadTrayIcon`, `Dispose` U; `SetTooltip` T
- `WindowsActivityMonitor.cs`: `Read`, `Describe(string)`, `ShellState`, `InUse`, `Collect`,
  `SHQueryUserNotificationState` U; `Describe()`, `Holders`, `LeafName`, `IsOpenNow` T
- `WindowsRegistryStore.cs`: `GetString`, `GetBinary`, `SetString`, `DeleteValue` U
- `WpfReminderPresenter.cs`: constructor, `Show`, `OnPendingShowElapsed`, `BringToFront`,
  `OnWindowClosed`, `CancelPendingShow` U

## Assumptions (the kit's rule "Two readings": decided, stated)

1. **Scope** is the 8 types and 150 methods above. Generated `obj/*.g.cs` and the XAML have no
   members of ours. Properties, fields, events and enum members stay as they are: the rule lets
   them go without one, and the owner's count leaves them out.
2. **Tags:**
   - `<summary>` always.
   - A `<param>` for each parameter. A positional record's go on the type: `ActivitySnapshot`,
     `DetectionSample`, `ReminderSettings`, `WorkArea`.
   - A `<returns>` for each of the 58 methods that return a value.
   - No `<typeparam>`: nothing in `src/` is generic.
   - No `<exception>` or `<remarks>`. The rule does not ask for them and the code has none. A
     throw a caller must plan for, other than a null guard, is said in the summary (`Start`
     after `Dispose`, `LoadTrayIcon` without its resource).
3. **`<inheritdoc/>` only where the interface states the whole contract.** This covers an
   implementation of one of this project's interfaces whose comment, written or completed here,
   says everything a caller needs. That is 13 members:
   - `RegistryAutoStartManager`'s four, and `RegistrySettingsStore.Load` and `Save`;
   - `WindowsRegistryStore`'s four, and `WindowsActivityMonitor.Read`;
   - `DispatcherPeriodicTimer.Stop` and `SystemEventsSessionMonitor.Start`.

   Members that get their own text:
   - **The 14 framework overrides** (WPF's `On*`, the renderer's `OnRender*`). The inherited text
     describes the base class's event, not what the override does.
   - **The four `Dispose` methods.** Each says what it releases. The coordinator also disposes
     the monitor and the timer it was handed.
   - **`IdleActivityMonitor.Read`:** always idle.
   - **`WpfReminderPresenter.Show` and `BringToFront`:** the show delay, and that a queued
     window is not raised.
4. **What a comment says:** the contract.
   - What the member does, what each parameter means (unit, range, and what null means where
     the type allows it), and what comes back.
   - A non-nullable parameter is not marked "not null": the signature says so.
   - In the voice of the file: English, `--` as the dash, `<see cref>`, `<paramref>`, and
     `<c>true</c>` / `<c>null</c>` as the code already writes them.
   - No ADR or requirement IDs: no code comment cites one.
   - Wrapped at 100 columns like the comments around it.
   - An overloaded name in a `cref` gets its parameter list (`Describe()`), or CS0419 fails the
     build.
5. **Event handlers** tag `sender` and `e` too, briefly: what raises it, and whether it is read
   ("The Close button; not read."). Each parameter is due a tag, and CS1573 fails a comment that
   tags only some.
6. **Existing comments keep their text,** why-paragraphs included.
   - Where a summary already says what a parameter means or what comes back, that sentence
     moves into its tag rather than being said twice. Examples: `Postpone`'s `<paramref>`; the
     "Returns <c>true</c> when ..." of `RepairPathIfNeeded` and `EnsureRegisteredOnFirstRun`;
     `TrayTooltip.Format`'s paragraph on `remaining`.
   - A summary that only says why gets a first sentence saying what (`TrayTooltip.Format`).
   - The rule's last sentence would put the why in ordinary comments. Moving the why out of the
     97 existing comments is a rewrite, and a task of its own if wanted.
7. **The six stale doc comments are corrected, in a commit of their own**, each to what its ADR
   records:
   - the `IActivityMonitor` summary: it is also read every `DefaultActivityRefresh` to keep the
     tooltip current, so "ONLY" and "two registry reads an hour" are wrong (ADR 0019);
   - the `RegistrySettingsStore` summary: eight values, not "Both values" (ADR 0027);
   - `IntervalSchedule.Restart`: `Postpone` sets a deadline too, so this is not "the only place a
     deadline is ever set" (ADR 0014);
   - the `IPeriodicTimer` summary: the presenter's show delay and `--detect` run timers of their
     own, so it is not "the one timer the whole app owns" (ADR 0012);
   - `LicenseInfo`'s fallback: `<Copyright>` is in `Directory.Build.props`, not "the csproj"
     (ADR 0010);
   - `ReminderSettings.MaxMessageLength`: 50 characters can need three lines at 30 px. The text
     is then scaled down to fit, rather than "two lines" that "run into the button" (ADR 0024).

   The ADRs keep their Context: a stale sentence there is history (the kit's ADR 0024). Three
   stale comments that are not doc comments in the code stay for their own task: the header of
   `DarkTheme.xaml`, the App csproj's "purely for NotifyIcon", and the summary of the
   backwards-jump test. **Say no at approval and commit 4 goes;** `Restart` then keeps its
   sentence beside its new `<param>`.
8. **Nothing else changes.**
   - No ADR: the readings are the kit's ADR 0035's, and nothing here deviates.
   - No requirement, no test, no README, no version bump (no behaviour changes).
   - One `Internal` line in `CHANGELOG.md`, as the kit's plan foresaw for each backfill.
9. **The App is not looked at on screen.** `--preview` and `--detect` would show nothing new: the
   diff check below proves that only `///` lines changed. There is nothing to list for the owner's
   copy after the merge either.

## Setup

- From `E:\Photografie\Code\MindfulnessReminder`:
  `git worktree add .claude/worktrees/Document-every-type-and-method -b claude/Document-every-type-and-method main`
- Work in `<wt>` = `E:\Photografie\Code\MindfulnessReminder\.claude\worktrees\Document-every-type-and-method\MindfulnessReminder`.
  - Edits use the Edit tool on absolute paths there, never in the main checkout.
  - Commits are plain `git -C <wt> commit -q -m "..."`; expect the hook's `== OK in NNs`.
- This plan is saved as `<wt>/docs/plans/2026-10-21-document-every-type-and-method.md`.
- **If the gate goes red for a reason that predates this task, I stop and report it.**

## Commits on `claude/Document-every-type-and-method`

1. The plan.
2. **Core:** 23 files, 7 types, 71 methods. Interfaces first, so that their implementations can
   inherit.
3. **App:** 12 files, 1 type, 79 methods.
4. **The six stale doc comments** (Assumption 7).
5. **Wrap-up:** the CHANGELOG line, and the plan's "What deviated" with the measured counts.

Each message is one line, under 100 characters, saying why.

**Sessions:** one is expected. If this one runs short, it stops after a commit; I expect commit 2,
the Core/App line. A second session opened in the project folder continues from this plan and the
branch. I give you a prompt to paste, since a chip would open at the checkout root.

## Verification

- **The gate** is green at each commit: the hook's `== OK`, 228 tests, and the build's
  CS1570/1572/1573/1574/0419 checks.
- **The scratch counter** on `<wt>`: `code` shows 0 types and 0 methods undocumented or
  incomplete (now 4 + 4 and 99 + 51), and properties still 33 without one. I paste the output.
  - It is the kit task's build, in that session's scratchpad. Right after approval I copy it
    into this session's scratchpad, so the check does not hang on another session's folder.
- **Only comments changed:** in `git -C <wt> diff main -- src`, every `+` or `-` line is a `///`
  line. Filtered for the rest, the output is empty.
- **`scripts/check-design.ps1 -Since main`:** each note it lists is read against the new comments.
  None should change, since comments change no mechanics.
- **The whole diff** is read before each commit and summarised at wrap-up.

## Then

- `/wrap-up` in `<wt>`, then I suggest `/code-review main...claude/Document-every-type-and-method`.
- I propose `git merge --no-ff claude/Document-every-type-and-method` in `E:\Photografie\Code`
  and ask who runs it. After the merge: `git worktree remove`, then `git branch -d`.
- Memory: the backfill pointer in `documentation-comments-rule.md` marks Mindfulness Reminder
  done.
- **Left for later:** the three stale comments that are not doc comments, and the why-paragraphs
  of the existing summaries if you want them moved.
