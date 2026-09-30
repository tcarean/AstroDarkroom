# 0010. A release is a deterministic script plus a proposed commit and tag

Date: 2026-09-25
Status: accepted 2026-09-25; who runs the release commit and tag superseded by 0021

## Context

Every project made from the kit releases the same way: gate green, version in
`Directory.Build.props`, `[Unreleased]` closed in the CHANGELOG with a date, an artifact built,
a commit and a tag. Done by hand, one of these is forgotten every few releases. Done entirely by
the assistant, it collides with rule 13: the owner decides what enters history.

## Decision

`scripts/release.ps1` does the mechanical part and only that: preconditions (clean tree,
version greater than current, non-empty `[Unreleased]`, tag not taken), `verify.ps1`, the two
file edits, `dotnet publish` into `artifacts/<version>/` (git-ignored). It commits nothing and
tags nothing. The `/release` skill wraps it: proposes the SemVer number from the CHANGELOG
sections, offers `/validate` first, runs the script with `-WhatIf` then for real, and ends by
proposing the commit line and the tag command and asking who runs them. Push is always the
owner's. Tag names: `vX.Y.Z` when the project is the repository root, `<folder>-vX.Y.Z` inside a
monorepo, so several projects can share one tag namespace.

## Alternatives rejected

- **Version from git tags (GitVersion, MinVer).** Adds a package and makes the version depend
  on repository state; the kit keeps it in one file that a test checks.
- **Release from CI on tag push.** Reasonable later; today there is no CI enabled and the owner
  wants to see the artifact before the tag exists.
- **Committed binaries under `artifacts/`.** Releases are tags plus reproducible builds
  (`Deterministic`, locked packages); binaries in git are weight without information.

## Consequences

A release is one `/release` and two answers. Signing or packaging (zip with checksum, MSIX) is
a step to add to `release.ps1` when there are users beyond the owner's machine. Measured on the
kit: 0.1.0 to 0.2.0 on a scratch copy, tag `kit-v0.2.0`, two files changed, gate green before
and after.
