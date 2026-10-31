# Show no window during the tests

Branch `claude/Show-no-window-during-the-tests`, worktree
`.claude/worktrees/Show-no-window-during-the-tests` (made from `main` at 9fc7cd92). Once approved,
this file is saved as `docs/plans/2026-10-30-show-no-window-during-the-tests.md` and committed first.

## Context

`WpfThread` (ADR 0032) runs the tests of a window or a control on one STA thread, and creates the
real `App` there for `Theme.xaml`'s resources. WPF's `Application` constructor queues the
application's start, which runs at the thread's first dispatcher pass even though `Run` is never
called: `App.OnStartup` reads the test host's arguments and sets the interface language, and
`StartupUri` opens a real `MainWindow`, shown and maximised, whose `Loaded` opens the real Open
dialog, for as long as the test host lives (measured 2026-10-30; ADR 0032's dated correction, ADR
0043). So every gate run puts the application on the owner's screen, and
`UiTextTests.R101_TheMainWindow_InRomanian` fails when it runs alone (the start sets English back
after the test set Romanian). The owner chose on 2026-10-30 to fix it in a task of its own, in this
session, after the log branch's merge. `App` is used by the tests only through its static
methods (`ApplyCommandLine`, `TakeStartupSelection`, `TakeUnknownLanguage`), which need no instance.

## The change

`tests/ImageVisualCompare.Tests/WpfThread.cs`, in place of `new App().InitializeComponent()`:

```csharp
// A plain Application with the application's theme, never the application's own App: WPF's
// Application constructor queues the application's start, which runs at the dispatcher's first
// pass, and App's would read the test host's arguments and open its StartupUri window (ADR 0046).
// The tests close the windows they build, so the application shuts down only when told to.
var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
application.Resources.MergedDictionaries.Add(new ResourceDictionary
{
    Source = new Uri("pack://application:,,,/ImageVisualCompare.Wpf;component/Theme.xaml"),
});
```

The URI names the assembly, since a plain application's own assembly is the test host's.
`App.xaml` holds nothing but that merge today; a resource meant for the windows belongs in
`Theme.xaml`, which `testing.md` will say. Nothing in `src/` changes.

`WindowLoggingTests.StartOutsideTheCapture` stays (the thread still takes the execution context of
the test that first uses it); its comment loses "the App's own start, which builds a window of its
own".

## Steps

1. **Test first** (red on today's fixture): new `tests/ImageVisualCompare.Tests/WpfThreadTests.cs`,
   `TheTestThreadRunsNoApplicationStart_AndShowsNoWindow`: five empty bodies 100 ms apart (the
   start runs in the first passes), then on the thread `Assert.IsNotType<App>(Application.Current)`
   and no window of `Application.Current.Windows` visible. Today it fails on both. No R-ID: it pins
   the fixture, not the application.
2. **The fix** in `WpfThread.cs`, its summary and comment corrected; `StartOutsideTheCapture`'s
   comment.
3. **Decision** `docs/decisions/0046-the-wpf-test-thread-runs-a-plain-application-with-the-theme.md`
   (Supersedes: 0032 in part, the real `App` on the thread; ADR 0032's Status says so): the
   context above; rejected: keep `App` and clear `StartupUri` (its `OnStartup` would still set
   the language from the test host's arguments), make `App` ask whether it runs under a test (ADR
   0043 rejected the same), a test-only subclass of `App` (the XAML class gives nothing to reuse).
   Check `main`'s last ADR number before the merge (0045 now).
4. **Documents**: `docs/design/testing.md` (`WpfThread`'s paragraph, the paragraph on the start
   rewritten to say what was and why it no longer is; `WpfThreadTests.cs` in its `Covers:`; shared
   resources in `Theme.xaml`), `CLAUDE.md` (Layout's and the rule "Run the application"'s
   parentheses on the `App` start; the log rule's "the WPF test thread builds an `App`" becomes
   the reason as ADR 0043 gives it), `docs/design/code-map.md` (`WpfThread`, ADR 0046),
   `CHANGELOG.md` (Internal: a test run no longer shows the application's window and Open dialog).
5. **Gate**: strict build, `dotnet format --verify-no-changes`, `check-design.ps1 -Since main`,
   `verify.ps1`; commit on the branch; propose the merge.

## Verification

- The new test fails before the fix (both asserts) and passes after.
- `dotnet test --filter "FullyQualifiedName~R101_TheMainWindow_InRomanian"` alone: failed before
  ("Navigation" for "Navigare", also on c494e48), passes after.
- A full test run watched from outside: a PowerShell script polls every 200 ms, while `dotnet
  test` runs, for visible top-level windows of the `testhost` processes (UI Automation, the root's
  children by process id) and prints what it sees; after the fix, none. Before it, a maximised
  `MainWindow` (already measured on 2026-10-30, not repeated).
- The gate green, the count of tests one more than `main`'s 463.

## Outcome (2026-10-30)

Implemented as planned on `claude/Show-no-window-during-the-tests`, the gate green. The branch's
commits were squashed into one before the merge, at the owner's word (2026-10-31), so the hash
named below is the branch's, not `main`'s. Measured:

- **Red first:** `WpfThreadTests.TheTestThreadRunsNoApplicationStart_AndShowsNoWindow` on the old
  fixture failed on its first assert, the thread's application being `App`.
- **Green after:** it passes, and `UiTextTests.R101_TheMainWindow_InRomanian` run alone passes too
  (it failed alone before: "Navigation" for "Navigare").
- **Watched from outside:** a full run of the suite, 464 tests, polled 31 times through UI
  Automation for the visible top-level windows of the test host (one process seen): none.
- **No CHANGELOG line**, unlike step 4 said: only tests and documents change, and the kit's rule
  "CHANGELOG" gives a test-only change none.
- **The review of the branch** (a subagent, 2026-10-30, at the owner's word): twelve findings,
  all verified and all taken at the owner's word. The reason given for `OnExplicitShutdown` was
  wrong: measured without it, the About tests' closing of their last window ended the application
  and the next window test failed with `The Application object is being shut down`, the
  dispatcher running on. The test now requires exactly a plain `Application` in that mode and no
  visible top-level window of the thread, Win32 ones included: a window shown off-screen on
  purpose made it fail with `["probe off-screen"]`. A second test pins `App.xaml` to the theme's
  merge. Stale reasons were reworded in `logging.md`, `Program.cs`, the csproj, `docs/skills.md`,
  `LoggingTests` and `CLAUDE.md`; ADR 0043 got a dated restatement and a dated correction. The
  owner kept the log in `Program.Main` and had the launch skipped, `src/` changing by comments
  alone.
- **The second review, of ffd57f9 alone** (a subagent, 2026-10-30, at the owner's word): nine
  findings, all verified and all taken at the owner's word. ADR 0043's dated notes now sit next
  to each reason they take back, and the "(Corrected ...)" note on its consequence is gone, the
  kit's ADR 0024 keeping a stale sentence of a Consequences section as history. The claims on
  `App.xaml` and on `App` were narrowed to what the tests bear out; the `App.xaml` test compares
  the whole resources element; `testing.md` covers `App.xaml`; `Application.Run` is named where
  it is meant; the sentence on the thread's first context agrees with the one before it. What
  failed where is stated, measured: an off-screen Win32 window, absent from
  `Application.Windows`, made the window check fail with `["probe win32"]`.
