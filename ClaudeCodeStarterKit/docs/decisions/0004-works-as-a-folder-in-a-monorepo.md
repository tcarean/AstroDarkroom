# 0004. The kit works as a folder inside a monorepo, not only as a repository root

Date: 2026-09-24
Status: accepted

## Context

The owner keeps several small tools in one repository (`photo`: FlatCheck, SirilCleanup, and
others) and starts Claude Code in the tool's folder. Claude Code resolves its project root,
`CLAUDE.md`, `.claude/settings.json` and hooks from that folder, and MSBuild resolves
`global.json`, `Directory.Build.props` and `.editorconfig` by walking up from the project. Only
git hooks and CI are repo-wide.

## Decision

The kit assumes nothing about being the git root. The pre-commit hook finds, for every staged
file, the nearest ancestor folder with `scripts/verify.ps1` and runs each such script once.
`install-hooks.ps1` detects the layout: hooksPath to `scripts/hooks` when standalone, a copy in
`<root>/.githooks/` otherwise. CI templates for the monorepo root live in `monorepo/`.

## Alternatives rejected

- **Require one repo per project.** Would force the owner to split a working repository and
  lose shared history for no gain in the gate.
- **hooksPath pointing into the kit folder.** Breaks as soon as the kit is renamed, moved or a
  second project wants its own hook; a root-level copy is visible and independent.
- **One root `verify.ps1` that builds everything.** Slow, and a red build in one tool would block
  commits to another.

## Consequences

A project folder without `scripts/verify.ps1` is not gated; adoption is per folder. The hook
copy at `.githooks/` must be refreshed by re-running `install-hooks.ps1` after the hook changes.
Version tags need a project prefix in a monorepo.
