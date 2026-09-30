# 0007. The changelog starts at 2.2.0; the git hooks are not copied; root CI is a separate task

Date: 2026-09-25
Status: accepted 2026-09-25; the fresh-clone hooks command superseded by 0046

## Context

Three small choices made while adopting the kit, each with an alternative someone might ask
about later. FlatCheck had 51 commits, no tags and no changelog; its versions 1.0.0 to 2.2.0 are
recoverable from the commit messages only roughly. The `photo` repository is a monorepo whose
root `.githooks/` already holds the kit's `pre-commit` and `commit-msg`, installed and active
(`core.hooksPath = .githooks`), and whose remote is on Bitbucket with no pipeline configured.
FlatCheck also carried a `.github/copilot-instructions.md` with Azure rules that Visual Studio
had put there.

## Decision

- `CHANGELOG.md` opens with an empty `[Unreleased]` and one section `[2.2.0] - 2026-09-23`
  summarising what 2.x holds, in the order it was built, and says that earlier history is in
  `git log`. Nothing is reconstructed commit by commit.
- The git hooks are not copied into `FlatCheck/scripts/hooks/`: the active copy is the root
  one, the kit is its source, and the guide says a project's copy must not be installed. Only the
  three Claude Code hooks live in `scripts/hooks/`. `install-hooks.ps1` and `rename.ps1` are not
  copied either.
- CI at the repository root (the kit's `monorepo/bitbucket-pipelines.yml`, with a FlatCheck
  step) is a change to the `photo` repository, not to FlatCheck, and is left for a task of its
  own. The Copilot file is deleted.

## Alternatives rejected

- **A full changelog from the git log.** Fifty-one messages of the form "bug fix" and "ui
  changes" would have produced entries that look like history and are not; `git log` itself is
  the honest record.
- **Copy the git hooks for completeness.** A second copy that git never runs and that drifts from
  the root one at the kit's next change.
- **Add CI now.** A pipeline file at the root affects every project in the repository and needs
  the owner's Bitbucket setup; nothing in FlatCheck depends on it.

## Consequences

`scripts/release.ps1` closes `[Unreleased]` into a dated section from now on, so the changelog is
complete from 2.2.0 onward. A fresh clone of the monorepo needs only `git config core.hooksPath
.githooks` for the gate to run at commit; nothing in FlatCheck installs anything.
