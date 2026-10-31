# 0046. The WPF test thread runs a plain Application with the theme, never the application's App

Date: 2026-10-30
Status: accepted 2026-10-30
Supersedes: 0032 in part (the real `App` created on the test thread)

## Context

ADR 0032 gave the tests of a window or a control one STA thread, `WpfThread`, and had it create
the real `App` and call its `InitializeComponent`, so that `Theme.xaml`'s resources resolve, in
the belief that nothing of the application starts without `Run`. It does: WPF's `Application`
constructor queues the application's start, which runs at the thread's first dispatcher pass.
Measured on 2026-10-30 (ADR 0032's dated correction, ADR 0043): `App.OnStartup` ran there with
the test host's arguments and set the interface language back to English, so
`UiTextTests.R101_TheMainWindow_InRomanian` failed whenever it was the first test to use the
thread; and `StartupUri` opened a real `MainWindow`, shown and maximised, whose `Loaded` opened
the real Open dialog, on the owner's screen for as long as the test host lived, at every gate
run. The owner chose on 2026-10-30 to have it fixed in a task of its own right after the
diagnostic log's merge. The tests use `App` only through its static methods
(`ApplyCommandLine`, `TakeStartupSelection`, `TakeUnknownLanguage`), which need no instance.

## Decision

- `WpfThread` creates a plain `Application` and merges `Theme.xaml` into its resources by the
  pack URI that names the application's assembly, since a plain application's own assembly is
  the test host's. Nothing of `App`'s start runs in the tests: no `OnStartup`, no `StartupUri`.
- Its `ShutdownMode` is `OnExplicitShutdown`. `AboutWindowTests` close the windows they build,
  and under the default, `OnLastWindowClose`, closing the thread's last window ends the
  application: measured on 2026-10-30, the next window test then failed with `The Application
  object is being shut down`. The dispatcher runs on either way, since `Application.Run` is never
  called.
- `App.xaml`'s resources are that merge alone; a resource meant for the windows and the controls
  goes in `Theme.xaml`, where the tests find it too.
- `WpfThreadTests` pins it: after the thread's first passes its application is exactly a plain
  `Application` in that mode, and no top-level window of the thread is visible, Win32 ones
  included; and `App.xaml`'s resources are the theme's merge alone. On the old fixture its type
  check failed; a WPF window and a Win32 window shown off-screen on purpose, the second one absent
  from `Application.Windows`, each made its window check fail (2026-10-30).

## Alternatives rejected

- **Keep `App` and clear its `StartupUri` before the first pass.** No window, but `OnStartup`
  would still read the test host's arguments and set the interface language.
- **Make `App` skip its start when it runs under a test.** The application would ask whether it
  runs under a test, which none of its code does; ADR 0043 rejected the same for the log.
- **A test-only subclass of `App`.** It would inherit `StartupUri` and `OnStartup` only to
  override both, and keep nothing but the merge a plain application does in a few lines.

## Consequences

- A test run shows nothing on the screen: watched from outside through UI Automation during a
  full run of the suite on 2026-10-30, 464 tests, the test host showed no top-level window; and
  `UiTextTests.R101_TheMainWindow_InRomanian` passes when run alone.
- `App`'s start is tested through `App.ApplyCommandLine` and the two `Take` methods, as before;
  what `OnStartup` and `StartupUri` do together stays checked by launching the application.
- The defect that ADR 0032's dated correction and ADR 0043's consequences record is fixed here.
- ADR 0043 rejected this plain `Application` as a way to let `App` start the log, and some of
  its reasons for starting the log in `Program.Main` rested on `App` running in every test run;
  they get a dated restatement there. The log does not move: the owner kept it in `Program.Main`
  on 2026-10-30, since a process's first line belongs before the application object.
