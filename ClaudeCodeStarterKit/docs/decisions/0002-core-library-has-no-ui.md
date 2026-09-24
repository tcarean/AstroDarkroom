# 0002. All logic lives in a library that cannot reach Console or a UI

Date: 2026-09-24
Status: accepted

## Context

An assistant can only verify what it can run and read. Logic tangled with a window or a terminal
is verified by screenshots and guesswork; logic in a library is verified by a test in under a
second. FlatCheck learned this at version 2.0.0, when the WPF window appeared and the core had to
be extracted; the extraction was cheap only because the console tool had already been kept thin.

## Decision

`Starter.Core` holds every computation and every formatting rule. It has no entry point and no
reference to `System.Console`, WPF, WinForms or dialogs. `Starter.Cli` (and any later UI project)
is a thin adapter over it. The rule is in CLAUDE.md, and the project reference graph enforces it.

## Alternatives rejected

- **One project with a "keep it clean" convention.** Held by goodwill; a compile boundary is
  held by the compiler.
- **Interfaces plus dependency injection for I/O.** Heavier than needed for a tool this size;
  can be added inside Core later without moving the boundary.

## Consequences

Acceptance tests drive `Program.Run(args, stdout, stderr)` with `StringWriter`s, so the CLI is
tested black-box without a process launch. A future UI gets the same treatment: a view-model in
Core or a Core-adjacent project, a thin view.
