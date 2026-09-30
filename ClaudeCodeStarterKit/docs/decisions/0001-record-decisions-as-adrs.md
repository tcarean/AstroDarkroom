# 0001. Record decisions as ADRs in the repository

Date: 2026-09-24
Status: accepted 2026-09-24; "not edited after acceptance" superseded by 0024

## Context

The project is developed almost entirely with Claude Code. Each session starts with no memory
of the previous ones, and a new model version reads the same files with a different eye. Any
decision that lives only in a conversation, or in the assistant's per-machine memory, is lost
or re-litigated.

## Decision

Every choice that someone could later ask "why?" about is written as one file in
`docs/decisions/`, numbered, using `0000-template.md`. ADRs are not edited after acceptance; a
new ADR supersedes the old one and links to it.

## Alternatives rejected

- **Comments in code.** They explain a local "how", not a project-level "why", and they are
  invisible when the question comes up in another file.
- **A single DECISIONS.md.** Grows into a wall; a per-decision file is linkable and diffable.
- **Claude's auto-memory.** Per machine, per user, not versioned, not visible to CI or to a
  reviewer.

## Consequences

The wrap-up skill asks for ADR candidates at the end of every task. The docs-health skill checks
that recent changes are explained by ADRs plus requirements.
