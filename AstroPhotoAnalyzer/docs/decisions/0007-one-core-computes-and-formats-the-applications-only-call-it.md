# 0007. One core computes and formats; the console and the window only call it

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §1 and §2 ("Invariants that the
structure enforces"), the old `CLAUDE.md` ("Nothing is computed outside `AstroPhotoAnalyzer.Core`",
"The two applications", "The window") and `docs/implementation-plan.md` ("Decisions taken before
the first line", "Notes from phases 5-7"); the decision itself dates from phase 0, 2026-09-20.
The tool has two front ends, a console and a WPF window, which must give the same numbers and the
same sentences for the same image: the window saves a `Raport.txt` that is meant to be the
console's output. FlatCheck had split its core into a library for the same reason (its ADR 0011),
and this tool was laid out the same way from the start.

## Decision

`AstroPhotoAnalyzer.Core` holds everything that computes and everything that formats: the
readers, the analyzers, the thresholds, the recommendations, the report. It has no entry point,
so nothing in it can reach `Console` or a window: the boundary is the compiler's, not a matter of
discipline. Both applications call `Runs/AnalysisRun.Execute`, which loads the file, runs every
analyzer, builds the recommendations and writes the report body, and show what it returns. The
core's types are `internal`, visible to the console, the window and the two test projects through
`InternalsVisibleTo`; the window's view-models are `public`, because binding needs it, and
therefore cannot expose a core type: everything the window shows passes through the simple rows
of `Rows.cs`. A view-model formats; it does not decide. The one kind of sentence it writes,
explanation, is ADR 0020.

## Alternatives rejected

- **Compute in the view-models what the window alone shows.** Two places that compute drift; the
  window would show a figure the report does not carry, and the saved `Raport.txt` would no longer
  be the console's output.
- **A public core.** A public type in a view-model's signature compiles, so logic grows around it
  in the window. With an internal core and public view-models, the compiler refuses that.

## Consequences

- A new number, threshold or sentence of diagnosis starts in `Analysis/`, `Thresholds.cs` or
  `Report/`, never in the console or the window (the rule "Computed only in the core").
- The two window-only figures, the screen-equivalent width and the step-back factor of the
  Preview tab, are computed in the core (`PrintSizeAnalyzer`), not in the view-model.
- Guards: the compiler (no entry point in the core; an internal type in a public signature does
  not compile), `CultureTests` and `CliTests` (the saved report equals the console's output),
  `WindowSmokeTests`.

Source: docs/specification.md §1, §2; CLAUDE.md (2026-09-21); docs/implementation-plan.md.
