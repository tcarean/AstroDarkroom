# 0006. The changelog starts at 1.0.0; root CI is deferred; the Copilot file is deleted

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Three small choices made while adopting the kit, each with an alternative someone might ask
about later. The project had eleven commits, of 2026-09-20 and 2026-09-21, no tag and no
changelog; its version has been 1.0.0 since the first commit. The `photo` repository's remote is
on Bitbucket with no pipeline configured, and the kit's `monorepo/` folder holds the root CI
templates. The project carried a `.github/copilot-instructions.md` with three Azure rules that
Visual Studio had put there. The git hooks need nothing: the root `.githooks/` holds the kit's,
installed with an absolute `core.hooksPath` (the kit's ADR 0016), and they find this project's
`scripts/verify.ps1` on their own.

## Decision

- `CHANGELOG.md` opens with `[Unreleased]` and one section, `[1.0.0] - 2026-09-21`, summarising
  what 1.0.0 holds in the order it was built, and says that the eleven commits are in `git log`.
  Nothing is reconstructed commit by commit.
- CI at the repository root (the kit's `monorepo/bitbucket-pipelines.yml`, with a step for this
  project) is a change to the `photo` repository, not to this project, and is left for a task of
  its own, as FlatCheck left it (its ADR 0007).
- `.github/copilot-instructions.md` is deleted, and with it the `.github/` folder.

## Alternatives rejected

- **A changelog reconstructed from the git log.** Eleven commits of one release, with subjects
  such as "docs and small fixes" and "UI changes to make things more clear", would give entries
  that look like history and are not; `git log` itself is the honest record.
- **Add CI now.** A pipeline file at the root affects every project in the repository and needs
  the owner's Bitbucket setup; nothing here depends on it.
- **Keep the Copilot file.** It gives instructions about Azure tools to an assistant that is not
  used here, for a project that has nothing to do with Azure.

## Consequences

`scripts/release.ps1` closes `[Unreleased]` into a dated section from now on, so the changelog is
complete from 1.0.0 onward; its floor is the newest released heading, 1.0.0, so the next release
is at least 1.0.1 (the kit's ADR 0029).
