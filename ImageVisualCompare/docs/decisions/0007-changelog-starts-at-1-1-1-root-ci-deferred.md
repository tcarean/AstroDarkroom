# 0007. The changelog starts at 1.1.1; the git hooks are not copied; root CI is deferred; nothing is deleted

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Four small choices made while adopting the kit, each with an alternative someone might ask
about later. The project had ten commits, of 2026-09-08 and 2026-09-09, no tag and no
changelog; its version went 1.0.0 (c6b676d, licence and versioning), 1.1.0 (861f5c1, padding of
a mismatched pair) and 1.1.1 (f4b91ee, dark title bars and the application's own message
dialog), none of them released. The `photo` repository's remote is on Bitbucket with no
pipeline configured, and the kit's `monorepo/` folder holds the root CI templates. The git
hooks need nothing: the root `.githooks/` holds the kit's, installed with an absolute
`core.hooksPath` (the kit's ADR 0016), and they find this project's `scripts/verify.ps1` on their
own. Unlike AstroPhotoAnalyzer, the project carried no stray file (no `.github/` folder, no
editor boilerplate); `.vs/` is ignored by the root `.gitignore`.

## Decision

- `CHANGELOG.md` opens with `[Unreleased]` and one section, `[1.1.1] - 2026-09-09`, summarising
  what the ten commits built, with the two version steps named; the commits themselves are in
  `git log`.
- No git hook is copied into the project.
- CI at the repository root (the kit's `monorepo/bitbucket-pipelines.yml`, with a step for this
  project) is a change to the `photo` repository, not to this project, and is left for a task of
  its own, as FlatCheck (its ADR 0007) and AstroPhotoAnalyzer (its ADR 0006) left it.
- Nothing is deleted: no file of the project is made obsolete by the kit.

## Alternatives rejected

- **A changelog reconstructed commit by commit.** Subjects such as "bug fix", "cleanup" and
  "changed zoom button" would give entries that look like history and are not; `git log` itself
  is the honest record.
- **Three dated sections, one per version.** None of the three was tagged or released; a dated
  heading per version would claim releases that did not happen.
- **Add CI now.** A pipeline file at the root affects every project in the repository and needs
  the owner's Bitbucket setup; nothing here depends on it.

## Consequences

`scripts/release.ps1` closes `[Unreleased]` into a dated section from now on, so the changelog is
complete from 1.1.1 onward; its floor is the newest released heading, 1.1.1, so the next release
is at least 1.1.2 (the kit's ADR 0029).
