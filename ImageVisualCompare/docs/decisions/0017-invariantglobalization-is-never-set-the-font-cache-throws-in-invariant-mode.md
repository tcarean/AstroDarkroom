# 0017. InvariantGlobalization is never set: in invariant mode WPF's font cache throws and the process dies during text layout

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "Never set
`InvariantGlobalization`"; "Testing", the two real defects) and the comment in
`Directory.Build.props`; the decision itself dates from a6e5f10 (the base version, 2026-09-08),
whose `Directory.Build.props` already carries the comment, and was written up in 591f433 (the
documentation commit, 2026-09-08). `InvariantGlobalization` was set in `Directory.Build.props`
for two phases of the work, before the first commit (no commit holds the setting), and nothing
failed until a Consolas run appeared in the status bar: WPF's font cache constructs
`CultureInfo("en")` while computing typography availability, which throws
`CultureNotFoundException` in invariant mode and takes the process down during text layout.
`CLAUDE.md` counts it as one of the two real defects found in the project, "build configuration
killing text rendering", found by launching the application and looking at a screenshot.

## Decision

`InvariantGlobalization` is not set, in `Directory.Build.props` or in any project file. The
comment in the first `PropertyGroup` of `Directory.Build.props`, where the property would go,
says not to set it, and why.

## Alternatives rejected

- **`InvariantGlobalization` on, as it was for two phases.** It builds and passes the tests, and
  takes the whole application down "the moment certain text is laid out" (the
  `Directory.Build.props` comment): here, the first Consolas run in the status bar.

## Consequences

- A launch can miss it: the setting survived two phases before a Consolas run set it off; today
  that run is the `StatusValue` style of `Theme.xaml`, which the cursor and pixel readouts use.
- Guards: none by test ("No unit test catches this"); the application has to be launched and
  looked at (ADR 0026).

Source: CLAUDE.md ("Invariants", "Testing"); Directory.Build.props;
src/ImageVisualCompare.Wpf/Theme.xaml; src/ImageVisualCompare.Wpf/MainWindow.xaml.
