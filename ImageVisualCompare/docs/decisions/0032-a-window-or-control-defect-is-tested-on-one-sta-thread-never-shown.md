# 0032. A defect a window or control shows without being shown is tested on one STA thread of the test project

Date: 2026-10-22
Status: accepted 2026-10-22
Supersedes: 0026 in part (the WPF layer untested by design)

## Context

ADR 0026 leaves the WPF layer without unit tests, by design: what is left in it is wiring,
capture and property assignment, every number it shows has its test in Core (ADR 0009), and the
two defects it had were layout and build configuration, seen on a screenshot. The code review of
the documentation backfill (2026-10-21) found two defects there that a test can reproduce
without showing anything: `MainWindow` starts a second open while a pair is still loading,
through Ctrl+O or the focused Open button, and two pairs can decode at once (R-006); and
`MinimapView` maps a click and lays out its rectangle with its own size and position, while the
thumbnail sits inside a 1-DIP border (R-041, R-042). The kit's rule "Test before fix" wants a
failing test first. No test created a window or a control, nothing ran on an STA thread, xUnit 2
has no STA support of its own, and a package for it would need the owner's word. On 2026-10-22
the owner chose tests on an STA thread of the test project over the other way: the open flow
moved out of the window into a class tested with fakes, and the minimap's border fixed in XAML
with no test that fails first.

## Decision

- `tests/ImageVisualCompare.Tests/WpfThread.cs` starts one background STA thread for the test
  run. On it the real `App` is created and its `InitializeComponent` called, which loads
  `App.xaml` with `Theme.xaml`; `Run` is never called, so `StartupUri` opens nothing. The thread
  then runs a dispatcher. `WpfThread.RunAsync` posts a test body to it and returns its task, so
  an exception in the body fails the test, and the bodies run one at a time.
- A window or a control under test is created there and never shown: no `Loaded`, no window
  handle. The test drives it through what the user's input reaches, a button's `Click` event or
  the internal method an input handler calls, and replaces what lies outside it, the file
  dialogs and the loader, through an internal constructor.
- A defect a window or a control shows in that state gets its test there, named after the
  requirement it breaks. Launching the application stays a step of every change to the WPF
  layer (the rule "Run the application"; ADR 0026 in that part stands): layout by a real
  window, rendering and real input are still out of reach.

## Alternatives rejected

- **No unit tests in the WPF layer, as ADR 0026 had it.** The open flow would move out of
  `MainWindow` into a class of its own to be tested with fakes, a larger change than the missing
  flag; and the minimap's border would be fixed in XAML, seen at the launch, with no test.
- **An STA test package.** It would need the owner's word for a new package, and a thread of the
  project's own does the same in a few dozen lines.

## Consequences

- `MainWindow` has an internal constructor that takes the file dialogs and the pair loader; the
  public one, which `StartupUri` calls, passes the real ones.
- The application object exists in the test process from the first such test on; no other test
  may create one.
- What still needs the window shown or real input, such as Ctrl+O with Ctrl held, is read in the
  code and looked at in the running application; a test does not pretend to cover it.
- Guards: `MainWindowTests.R006_ASecondOpenWhileAPairLoads_AsksForNothing`.
