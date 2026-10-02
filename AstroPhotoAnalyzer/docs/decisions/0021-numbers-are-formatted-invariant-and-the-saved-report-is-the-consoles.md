# 0021. Every number is formatted with the invariant culture, and the window's saved report is the console's output

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §8 ("Every number goes through
`Fmt`") and §10 ("Culture"), the comment of `AstroPhotoAnalyzer.Wpf.csproj`, and
`docs/implementation-plan.md` ("The saved `Raport.txt` is `ReportWriter.WriteFull`"); the
decision dates from phases 4 and 5, 2026-09-20. The owner's Windows is Romanian, whose culture
writes `0,19`. The report is the same text from both applications, saved by the window and
compared by the golden tests. WPF needs culture data to bind a property: without it, the XAML
fails to load.

## Decision

- Every number of the report goes through `Fmt`, with `CultureInfo.InvariantCulture` spelled out
  in each call; NaN prints as `n/a`.
- The core and the console set `InvariantGlobalization`. The WPF project does not; instead
  `App.ForceInvariantNumbers` pins `DefaultThreadCurrentCulture` and `CurrentCulture` to the
  invariant culture at startup, and the parameter fields parse with the invariant culture.
- The window saves `ReportWriter.WriteFull`, the banner plus the body: the same bytes the console
  writes with `-o`.

## Alternatives rejected

- **`InvariantGlobalization` on the WPF project.** The window fails to load its XAML.
- **The current culture.** A Romanian Windows would print `0,19`, and the same image would give
  two reports on two machines.
- **Formatting in the view-models.** A second formatter, and two texts that could differ.

## Consequences

- `CultureTests` proves the report identical under `ro-RO` even without forcing the culture,
  because every number goes through `Fmt`; the golden files compare against the same text.
- R-006, R-101 and R-118 state the behaviour.

Source: docs/specification.md §8, §10; AstroPhotoAnalyzer.Wpf.csproj; docs/implementation-plan.md.
