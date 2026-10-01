# 0029. The owner bumps the version; a release floors at the newest released heading and runs on main

Date: 2026-10-01
Status: accepted 2026-10-01
Supersedes: 0010 in part (the version precondition and where a release runs)

## Context

ADR 0010 made `release.ps1` refuse any version not greater than `<Version>` in
`Directory.Build.props`, which assumes that only a release changes `<Version>`. The first
adopter bumps it per feature, when the owner chooses, so that a build carries the version it
will ship as. On 2026-10-01 its `<Version>` was 2.3.0 while its newest released CHANGELOG heading
was `## [2.2.0] - 2026-09-23`: the script refused to release 2.3.0, the very version the owner
had set, and accepted only a higher one. No git tag exists in the repository, so a tag cannot
serve as the record of what was released. ADR 0010 also let a release run from "main (or the
branch the owner names)", while the commit and the tag it proposes are the owner's and are made
on `main` (ADR 0021); a release prepared in a task worktree leaves the owner a version commit on
the wrong branch. On 2026-09-30 the owner decided, in chat, decisions Q3 and Q4 of the
programme: the owner may bump the version, the floor is the newest released heading, and a
release is cut in a session opened in the project folder of the main checkout, on `main`.

## Decision

- The version and the copyright year change only at the owner's word: a rule of the kit,
  "Version and year are the owner's". The owner may bump `<Version>` at any time, per feature.
- `release.ps1` refuses, before it changes anything:
  - a checkout that is not on `main` (a task branch or a detached HEAD);
  - a version that is already a released heading of `CHANGELOG.md` (`## [X.Y.Z] - date`), or
    is not above the highest of them, the floor; with no released heading there is no floor;
  - a version below `<Version>`;
  - an empty `[Unreleased]`, a dirty tree or an existing tag, as before.
  A version equal to `<Version>` is released as it is and `Directory.Build.props` is left
  unchanged; a higher one is written to it, as before.
- `/release` runs in a session the owner opened in the project folder of the main checkout.
  Step 1 notes `git rev-parse HEAD`; step 2 proposes `<Version>` when it is above the newest
  released heading, and a SemVer step otherwise; step 6 checks that HEAD did not move and that
  only `CHANGELOG.md` and `Directory.Build.props` changed, then proposes
  `git commit -m "Release X.Y.Z" -- Directory.Build.props CHANGELOG.md` and the tag, which the
  owner runs.
- `git tag*` joins the `ask` list of `.claude/settings.json`, so a tag the model would make
  asks the owner first.

## Alternatives rejected

- **The floor at the newest tag.** No tag exists yet; the CHANGELOG heading is written by every
  release and is already the record the README and the auditor read.
- **The floor at `<Version>`, as before.** It refuses exactly the version the owner chose, and
  pushes every release one step past it.
- **Release from any branch the owner names.** The commit and the tag go to `main` anyway; a
  release prepared elsewhere either moves to `main` by hand or ships from a branch.
- **Let the script commit and tag.** ADR 0021 keeps commits and tags on `main` the owner's.
- **A plain `git commit -am` in the proposal.** It would sweep up anything else changed on
  `main` in the meantime; the pathspec limits the commit to the two files the release wrote.

## Consequences

`ReleaseTests` covers the refusals (off `main`, already released, not above the floor, below
`<Version>`, an existing tag), a `-WhatIf` that changes nothing, the owner's version released as
it is, and a higher version written to the props file; the cases build a repository on `main`
whose `<Version>` is above its newest heading. The first adopter's 2.3.0 can be released as set.
A `git tag --list` by the model now asks the owner too, a small cost for a list the release
script reads itself.
