# 0037. WPF rules learned the hard way: no InvariantGlobalization, the dispatcher captured once per run, XAML rendered by smoke tests, unique radio GroupNames, a dark ToolTip style

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/two-applications.md ("Three things about
the WPF project that were learned the hard way") and docs/design/code-map.md (`Views/`,
`Theme/Dark.xaml`); the decisions date from the window's first versions (2.0.0 and after). Each
was a failure first: `<InvariantGlobalization>`, tempting for symmetry with the console, made
the window throw `Cannot find non-neutral culture related to 'en-us'` while loading XAML, on
every start, after passing 100 unit tests. Reading `Application.Current` per frame in
`LightSeriesRun`'s callback let the answer change mid-series, so half the rows landed on one
thread and half on another - six frames analysed, five rows in the table, measured in a test run
where the WPF `Application` appeared partway through. A shared radio `GroupName` made one tab's
chips uncheck another's. WPF's default light tooltip met the implicit light `TextBlock` style as
white on white on the Sintetic tab's sensor chips. The Structure tab said "SCARA CELOR PATRU
TESTE" for a whole version after the tests became five, caught by a `FLATCHECK_DUMP_SCREENS`
render, not by the suite.

## Decision

No `<InvariantGlobalization>` in the WPF project; the invariance actually needed is done in code,
`App.ForceInvariantNumbers` pinning `DefaultThreadCurrentCulture` so a Romanian Windows does not
write `0,24` into a file whose point is being identical to the console's. The dispatcher is
captured once per run, not asked per frame. `WindowSmokeTests` renders every tab with real
results into a `RenderTargetBitmap` and asserts that no binding error was traced; the About
window's *content* is rendered, not the window, since an unshown `Window` renders transparent.
Every set of radio chips has its own `GroupName` (`StructureMapScale`, `RatioMapScale`,
`ExposureHistogramSource`, `HistEqView`). `Dark.xaml` styles `ToolTip`, so any new tooltip is
dark for free; the converters live there because a `StaticResource` inside a dictionary resolves
in that dictionary.

## Alternatives rejected

- **`<InvariantGlobalization>` for symmetry with the console.** WPF needs culture data to bind
  anything at all.
- **Reading `Application.Current` on every callback.** Rows lost silently.
- **Trusting unit tests for XAML.** A wrong resource key, a missing converter, a binding to a
  property that is not there - all compile, and some fail at load; the crash above passed every
  unit test and never started.
- **A golden comparison of the rendered screens.** `FLATCHECK_DUMP_SCREENS=<dir>` saves PNGs for
  eyes; how a window looks is not a job for a test.

## Consequences

- `CultureTests` pins both halves: no invariant globalization, invariant numbers.
- Headings that count something are literals in XAML; `TheScaleHasFourBandsAndMarksTheCurrentOne`
  now also asserts the test count, which is the number that heading quotes.
- A public view-model cannot expose an internal core type, so the compiler stops a
  `StructureResult` from leaking into XAML; `MapScaleOption.CapPct` is `internal` for that reason.
- Hyperlinks in the About window are handled once, on the window, through the routed
  `Hyperlink.RequestNavigate`, because a resource dictionary has no code-behind.
- Guards: `CultureTests`, `WindowSmokeTests`, `TheScaleHasFourBandsAndMarksTheCurrentOne`.
- Source notes: docs/design/two-applications.md, docs/design/code-map.md.
