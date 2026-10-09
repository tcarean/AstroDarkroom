# 0006. The changelog starts at 1.4.0; the git hooks are not copied; root CI is deferred; nothing is deleted

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Four small choices made while adopting the kit, each with an alternative someone might ask
about later. The project had eight commits, from 2026-08-28 to 2026-09-14, no tag and no
changelog; its version went 1.0.0 (d3ccd6c, licensing and versioning), 1.1.0 (b6554d5, the
periodic reminder), 1.2.0 (89de6c8, pausing for a call or full screen), 1.3.0 (432edfd, the dark
theme) and 1.4.0 (2be5361, a switch and a message for each reminder), none of them released.
The `photo` repository's remote is on Bitbucket with no pipeline configured, and the kit's
`monorepo/` folder holds the root CI templates. The git hooks need nothing: the root
`.githooks/` holds the kit's, installed with an absolute `core.hooksPath` (the kit's ADR 0016),
and they find this project's `scripts/verify.ps1` on their own. The project carried no stray
file; its own `.gitignore` un-ignores `src/MindfulnessReminder.App/`, which the root's `*.app`
rule would otherwise hide from git.

## Decision

- `CHANGELOG.md` opens with `[Unreleased]` and one section, `[1.4.0] - 2026-09-14`,
  summarising what the eight commits built, with the five version steps named; the commits
  themselves are in `git log`.
- No git hook is copied into the project.
- CI at the repository root (the kit's `monorepo/bitbucket-pipelines.yml`, with a step for this
  project) is a change to the `photo` repository, not to this project, and is left for a task of
  its own, as FlatCheck (its ADR 0007), AstroPhotoAnalyzer (its ADR 0006) and ImageVisualCompare
  (its ADR 0007) left it.
- Nothing is deleted. The project's `.gitignore` keeps its lines, the `*.app` exception
  included, and gains the kit's.

## Alternatives rejected

- **A changelog reconstructed commit by commit.** Subjects such as "changed message" would give
  entries that look like history and are not; `git log` itself is the honest record.
- **Five dated sections, one per version.** None of the five was tagged or released; a dated
  heading per version would claim releases that did not happen.
- **Add CI now.** A pipeline file at the root affects every project in the repository and needs
  the owner's Bitbucket setup; nothing here depends on it.

## Consequences

`scripts/release.ps1` closes `[Unreleased]` into a dated section from now on, so the changelog
is complete from 1.4.0 onward; its floor is the newest released heading, 1.4.0, so the next
release is at least 1.4.1 (the kit's ADR 0029).
