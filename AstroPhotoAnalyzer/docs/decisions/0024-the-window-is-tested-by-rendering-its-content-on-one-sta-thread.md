# 0024. The window is tested by rendering its content with a real result, on one STA thread

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §11 (the window row),
`docs/implementation-plan.md` ("`WindowSmokeTests` renders the window's *content*") and the old
`CLAUDE.md` ("The window", "Build & test"); the decision dates from phase 5, 2026-09-20. A WPF
binding that names a property that does not exist does not throw: it leaves the control empty and
writes a trace line, so a broken tab looks like an empty one. WPF objects need an STA thread with
a message loop, which xUnit's threads are not. A `Window` that is never shown renders transparent.

## Decision

- The window's tests are a project of their own, `AstroPhotoAnalyzer.Wpf.Tests`, on
  `net8.0-windows`, so the core's tests stay portable. It borrows five fixtures from the core's
  tests by link, so both see the same scenes.
- One STA thread with a message loop serves the whole process (`StaFixture`).
- `WindowSmokeTests` renders the main window's content, the About window, every tab with a real
  `AnalysisResult`, the error card and the help tooltip, and fails on any binding error. It renders
  a window's content, not the window, because an unshown window renders transparent.
- `APA_DUMP_SCREENS=<dir>` saves the renders as PNG files: a way to look at the window without
  running it, not a golden comparison.
- `MainViewModel` opens no dialogs, so it is tested directly.

## Alternatives rejected

- **Drive the running application.** Slow, and it needs a desktop session.
- **Render the `Window` itself.** It renders transparent.
- **Compare the renders with golden images.** Fonts and DPI differ from machine to machine; the
  binding errors are what a test can judge.

## Consequences

- A new tab needs a line in the test that renders every tab (`docs/design/extending.md`).
- The window's tests run on Windows only; the gate runs both test projects.
- Guards: the tests themselves, R-110 to R-119.

Source: docs/specification.md §11; docs/implementation-plan.md; CLAUDE.md (2026-09-21).
