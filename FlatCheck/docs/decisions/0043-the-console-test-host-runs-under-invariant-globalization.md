# 0043. The console's test host runs under InvariantGlobalization, like the console

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

`FlatCheck/FlatCheck.csproj` sets `<InvariantGlobalization>`, so `flatcheck.exe` formats `0.24`
whatever the Windows culture. The switch is read from the *executable's* `runtimeconfig.json`;
a test project is its own executable (the test host reads `FlatCheck.Tests.runtimeconfig.json`),
and the switch of a referenced console project does not reach it. `FlatCheck.Tests` did not set
it, and the report code mostly formats with the ambient culture, so the golden files and the
report tests compared against the test machine's culture. They passed on the author's `en-GB`
machine. `docs/design/process.md` already named the gap and the remedy ("pin the switch in the
test project rather than editing a golden file").

Measured on 2026-09-25: a module initializer setting `DefaultThreadCurrentCulture` and
`CurrentCulture` to `ro-RO` in `FlatCheck.Tests` made 63 of 894 tests fail: 26 in
`GoldenOutputTests`, 12 in `LightReportTests`, 8 in `StructureReportTests`, and the rest in
`RunReportTests`, `SyntheticRunTests`, `SyntheticFlatTests`, `SmoothReportTests`,
`LightSuitabilityTests`, `LightSeriesReportTests`, `ExposureReportTests`,
`LensProfileReaderTests` and `FitsReaderTests`. Most compare `,` against `.`; the last few are
fixtures (`FitsTestData.OpticsCards`, the LCP fixture) that write their numbers with the ambient
culture, which the core then parses invariantly (`3,92` read as `392`). The core's parsers name
the invariant culture and were not at fault. The probe was removed after the run.

## Decision

`FlatCheck.Tests.csproj` sets `<InvariantGlobalization>true</InvariantGlobalization>`, the same
switch as the console, so the test host has the console's runtime culture behaviour rather than
an imitation of it. `InvariantGlobalizationTests` (R-007) pins both halves: the console's
`flatcheck.runtimeconfig.json` sets the switch, and the test host runs with it (invariant
`CurrentCulture`, `0.24`).

## Alternatives rejected

- **A module initializer pinning `CultureInfo.InvariantCulture` in the test assembly.** It
  imitates the console instead of running under its switch, and would still let a test
  depend on culture data the console does not have (a `ro-RO` `CultureInfo` loads in the
  test and throws in `flatcheck.exe`).
- **Naming `CultureInfo.InvariantCulture` in every format call in `FlatCheck.Core`.** Right
  for new code (`process.md` asks for it, since the window hosts the same core without the
  switch), but a sweep of the report code is a task of its own and would not stop the tests
  from reading the machine's culture in the meantime.
- **Leaving it.** The gate would fail on any machine with a comma culture, and the tempting
  "fix" there is to re-approve the goldens with commas.

## Consequences

- A test in `FlatCheck.Tests` cannot construct a named culture such as `ro-RO` (it throws under
  the switch). A test that needs one belongs in `FlatCheck.Wpf.Tests`, which has culture data
  (ADR 0037); `CultureTests` there is where the window's half (R-090) is proved.
- `FlatCheck.Wpf.Tests` stays without the switch: WPF cannot load XAML without culture data.
- Guards: `InvariantGlobalizationTests`.
