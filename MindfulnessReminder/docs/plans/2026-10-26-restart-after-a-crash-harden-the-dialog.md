# Restart after a crash, harden the crash dialog

## Context

On 2026-10-26 the tray copy died: a Visual Studio security update uninstalled the .NET 8.0.32
runtime it was running on, the first reminder window needed an assembly that was gone
(`UIAutomationTypes`), the crash dialog of R-076 appeared with an empty message, and the `OK`
ran `Application.Shutdown`, whose WPF path needed another missing assembly
(`System.Diagnostics.Tracing`) and killed the process inside its own handler. The owner chose
option A (register for automatic restart, harden the dialog); the requirements R-076, R-100 to
R-102 are accepted as drafted (2026-10-26), ADR 0036 is accepted with `Environment.FailFast` as
the exit path. Measured with a probe: Windows restarts a registered process 0.2 s after a
fail-fast when it has run 65 s, and not after 20 s; the fail-fast writes `.NET Runtime` event 1025
with the message and the stack.

The failing acceptance tests exist and are parked in the scratchpad
(`...\scratchpad\parked-tests\ApplicationRestartTests.cs`, `CrashReportTests.cs`), because the
Stop hook needed a green gate while waiting for the ADR answer. Branch
`claude/Restart-after-a-crash-harden-the-dialog`, worktree
`.claude/worktrees/Restart-after-a-crash-harden-the-dialog`.

## Steps

1. **Restore the tests.** Move the two parked files back into `tests/MindfulnessReminder.Tests/`.
   Verified by: the build fails on `ApplicationRestart` and `CrashReport` (step 2 of /new-feature).

2. **Core: `src/MindfulnessReminder.Core/CommandLineSwitches.cs`** (new). The three switches
   the executable knows, in one place so Core can decide and App can parse the same strings:
   `Preview = "--preview"`, `Detect = "--detect"`, `Restarted = "--restarted"`, and
   `Has(IReadOnlyList<string> args, string name)`: true when an argument equals the switch or
   starts with the switch and `=`, compared `OrdinalIgnoreCase` (the shape `--detect=30` already
   has in `App.DetectDuration`). Null arguments throw `ArgumentNullException`.

3. **Core: `src/MindfulnessReminder.Core/ApplicationRestart.cs`** (new, static). `CommandLine`
   (= `CommandLineSwitches.Restarted`), `Flags` (8, `RESTART_NO_REBOOT`, with the three other
   flag values named in a comment), `ShouldRegister(args)`: true unless `Has(Preview)` or
   `Has(Detect)`; `IsRestart(args)`: `Has(Restarted)` exactly, so `--restarted=1` is not one
   (the test says so; the switch takes no value). Doc comments on everything (the kit's rule
   "Documentation comments").

4. **Core: `src/MindfulnessReminder.Core/CrashReport.cs`** (new, static). `Format(exception,
   banner)`: for the exception and then each `InnerException` in turn, one line: the type's
   `FullName`, then `: ` and `Message` when `Message` is not white space; a line `File: <name>`
   after it when the exception is `FileNotFoundException`, `FileLoadException` or
   `BadImageFormatException` with a non-blank `FileName`; inner lines are prefixed `Caused by: `.
   Lines joined with `\n` (what `MessageBox` draws and what the old dialog used), then `\n\n`
   and the banner. Both arguments null-checked.
   Verified by: `dotnet test --filter "FullyQualifiedName~CrashReportTests|FullyQualifiedName~ApplicationRestartTests"`
   green; `RepoInvariantTests` still red until step 8 flips the statuses.

5. **App: `src/MindfulnessReminder.App/RestartRegistration.cs`** (new). One Windows API per
   file: `[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int
   RegisterApplicationRestart(string commandLine, int flags)` behind
   `static void Register()` that passes `ApplicationRestart.CommandLine` and
   `ApplicationRestart.Flags` and ignores the HRESULT (R-100: a refused registration changes
   nothing). GPL header, doc comments.

6. **App: `src/MindfulnessReminder.App/App.xaml.cs`.**
   - `PreviewSwitch` and `DetectSwitch` become `CommandLineSwitches.Preview` / `.Detect`
     (the two private constants go; `PreviewMessage` and `DetectDuration` read the same).
   - In `OnStartup`, right after the mutex is claimed and before the handlers:
     `if (ApplicationRestart.ShouldRegister(e.Args)) RestartRegistration.Register();`.
   - After `EnsureRegisteredOnFirstRun` (so the first-run balloon, when due, comes first):
     `if (ApplicationRestart.IsRestart(e.Args)) _tray.ShowBalloon(VersionInfo.Name,
     "Restarted after an error. The details are in the Windows Application log.");`.
   - `ReportCrash(exception)`: `var text = CrashReport.Format(exception, VersionInfo.Banner);`
     `MessageBox.Show(text, VersionInfo.Name + " stopped", OK, Error)`; then
     `try { _tray?.Dispose(); } catch (Exception) { /* the icon is cosmetic here */ }`; then
     `Environment.FailFast(text, exception)`. `Shutdown()` is gone from this path.
     `OnDispatcherUnhandledException` keeps `e.Handled = true` before `ReportCrash` (the
     fail-fast never returns, the flag is for the WinForms-side reader).
   - Comments say why FailFast (ADR 0036) in one line each.
   Verified by: `scripts/verify.ps1 -SkipTests` green; `--preview` still shows the window
   (unchanged, a smoke check that the switch parsing moved intact). The crash path and the
   registration run only in a tray start, which a worktree build must not do (R-006): listed
   for the owner.

7. **Design notes.** `docs/design/code-map.md`: a line each for `CommandLineSwitches.cs`,
   `ApplicationRestart.cs`, `CrashReport.cs` under Core, `RestartRegistration.cs` under App, and
   the `App.xaml.cs` line says the crash dialog ends in a fail-fast (ADR 0036). No other note
   covers these files; `scripts/check-design.ps1 -Since main` confirms.

8. **Record.** `docs/requirements.md`: R-100, R-101, R-102 `implemented`; R-076 `accepted`
   (App behaviour, no test carries it). `CHANGELOG.md` `[Unreleased]`: Added (restart
   registration, the balloon), Changed (the dialog's text, the fail-fast exit), one Fixed line
   for the 2026-10-26 failure mode. `README.md`: a Behaviour bullet (restarts after an error),
   a row in "Design decisions" (restart via `RegisterApplicationRestart` + `FailFast`, why not
   `Exit`), the "Versioning" table's crash-dialog row (type, message, file, banner). Save this
   plan as `docs/plans/2026-10-26-restart-after-a-crash-harden-the-dialog.md`.

9. **Gate and wrap-up.** `dotnet build --no-incremental -warnaserror --no-restore` first (the
   gate's incremental build can hide a warning), then `scripts/verify.ps1` green, `/wrap-up`:
   commit on the task branch, propose the merge.

## Verification

- `scripts/verify.ps1` green: format, build with warnings as errors, 252 + the new tests, the
  design-note index, `RepoInvariantTests` (statuses in step with the `R###_` tests).
- `MindfulnessReminder.exe --preview` from the worktree's Release build shows the reminder
  window as before.
- Not verifiable here, listed for the owner after the merge, in their own copy: the balloon on a
  `--restarted` start, and the whole chain (dialog with the file named, event 1025 in the
  Application log, the copy back in the tray) by killing the runtime under it as the
  reproduction did, or by any crash after 60 s of running.

## Deviations (2026-10-26, at wrap-up)

- Step 6 said the two private constants `PreviewSwitch` and `DetectSwitch` go. They stayed, as
  aliases of `CommandLineSwitches.Preview` and `.Detect`: their documentation comments explain
  the two switches to a reader of `App.xaml.cs`, and the parsing code below them did not have to
  change.
- Step 2 did not foresee that `--restarted` must match exactly while `--detect=30` must match
  with a value, so `CommandLineSwitches` has two lookups, `Has` and `HasExactly`.
- `CrashReportTests.cs` carried a `using System.IO;` the implicit usings make redundant; the
  build's IDE0005 refused it, and the line went.
- The crash path and the registration were not exercised here, as planned: a tray start of a
  worktree build would repair the auto-start entry (R-006). The only check run was `--preview`
  from the worktree's Release build, which opened the window and left the Run entry untouched.

## Code review (2026-10-26)

A subagent reviewed fa5f264 read-only against the rules, the rows and ADR 0036 (275 tests
green). Seven findings, none blocking; all applied in the follow-up commit:

1. `ReportCrash` could nest: the dialog pumps messages, so the timer, the session hook and the
   tray menu kept running under it, and a second error opened a second dialog. Now the first
   call stops the coordinator before the dialog and a flag makes a second call return; R-076
   says so.
2. Core and App disagreed on `--preview=x`: App reads `--preview` exactly (the message is the
   next argument), Core matched the `=` form too. `ShouldRegister` now uses `HasExactly` for
   `--preview` and `Has` for `--detect`; a test pins `--preview=x` as a registering start;
   R-100 names the two shapes.
3. Two balloons at one start replace each other in the shell; R-102 now says the restart
   balloon replaces the first-run one instead of claiming both show.
4. R-101 said `FileName` "set", the code drops a blank one: the row says "not blank", and the
   test covers `null`, empty and white space.
5. R-100 and R-102 say which part the tests pin and which a tray start checks, so
   `implemented` is not read as "the Windows call is tested".
6. The restart "when an installer asks it to close" over-promised: Windows Update's installer
   runs as SYSTEM and could not ask on 2026-10-26 (event 10010). Row, comment and summary now
   say "running as the same user".
7. The README and R-100 say the restart needs Windows Error Reporting on, the default.

Nothing found: the P/Invoke signature, the ordering in `OnStartup`, what `ReportCrash` needs
before the fail-fast (all in assemblies already loaded), null and case handling, tests against
rows, documentation comments, GPL headers, `code-map.md`, CHANGELOG and README against the code,
the code against ADR 0036.
