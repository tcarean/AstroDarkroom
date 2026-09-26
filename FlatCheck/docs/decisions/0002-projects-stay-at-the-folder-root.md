# 0002. The projects stay at the folder root; the kit scripts take a list of code folders

Date: 2026-09-25
Status: superseded by 0048

## Context

The kit lays a project out as `src/<Name>.Core`, `src/<Name>.Cli` and `tests/<Name>.Tests`, and
three of its scripts read that layout: `check-staged.ps1` (a change under `src/` needs a
CHANGELOG line), `claude-stop-gate.ps1` (a turn that edited `src/` or `tests/` cannot end
unverified) and the `docs-auditor` agent (never opens `src/`). FlatCheck has five projects at the
folder root (`FlatCheck.Core`, `FlatCheck`, `FlatCheck.Wpf`, `FlatCheck.Tests`,
`FlatCheck.Wpf.Tests`), referenced by path from the solution file, from `<Compile Link>` items
in the WPF test project, from `<None>` items copying `lensfun/`, from the README, from the
design notes and from `LicenseHeaderTests`, which lists the project folders explicitly.

## Decision

The layout stays. The three scripts and the agent carry the list of FlatCheck's code folders
(`FlatCheck.Core/`, `FlatCheck/`, `FlatCheck.Wpf/`, plus `flatcheck.py`, the numeric twin)
and test folders (`FlatCheck.Tests/`, `FlatCheck.Wpf.Tests/`) instead of the `src/` and `tests/`
prefixes. `flatcheck.py` counts as code: a formula changed there is a behaviour change.

## Alternatives rejected

- **Move the projects under `src/` and `tests/`.** `git mv` keeps history, but the move touches
  the solution, four csproj files, the README, twenty design notes and one test, for a hook that
  gains nothing from it; and the guide's migration chapter is explicit that an existing project
  keeps its layout ("fara rescrieri").
- **A single prefix `FlatCheck` in the scripts.** `FlatCheck/` matches only the console folder
  (the character after the name is `/`, not `.`), so a list is needed anyway.

## Consequences

The three scripts differ from the kit's copies in one variable each; a future kit change to
those scripts is merged by hand. A new project folder in FlatCheck must be added to the lists,
as it must be added to `LicenseHeaderTests.Projects`. Test-only changes do not require a
CHANGELOG line, as in the kit.
