# 0007. One-line English commit messages; the owner decides every commit and merge

Date: 2026-09-25
Status: superseded by 0021

## Context

The owner wants git history that reads as a list of short reasons, and wants to remain the
one who decides what enters that history and when. The CHANGELOG already carries the "what"
of every change, so a commit body repeats it. Assistant tooling tends to append attribution
trailers (`Co-Authored-By`) that the owner does not want in the repository.

## Decision

- A commit message is one line, in English, saying why, under 72 characters. No body, no
  trailers of any kind.
- The assistant never commits, merges or pushes on its own. It proposes the message (or the
  merge into `main`) and asks; the owner decides each time whether the assistant runs the
  command or does it by hand. An approval covers that one command, not the session.
- Enforced twice: a `commit-msg` git hook rejects multi-line messages and trailers for every
  committer, and the Claude Code `permissions.ask` list makes every `git commit`, `merge`,
  `push` and `rebase` prompt the owner before it runs.

## Alternatives rejected

- **Conventional Commits or a body per commit.** More structure than a one-person project
  needs; the structured record is `CHANGELOG.md`.
- **Prose rule only.** Attribution trailers are added by the tooling by default, so a rule the
  model must remember would lose to the default eventually. The hook does not forget.
- **Letting an approved commit imply approval for the next.** The owner wants the decision per
  command; the `ask` list gives exactly that without relying on the model.

## Consequences

`/wrap-up` ends with a proposed one-line message and the question "you or me?". Merging into
`main` goes through the same question. The `commit-msg` hook applies to the owner's own commits
too, by design; `install-hooks.ps1` installs it beside `pre-commit`, and in a monorepo copies
both to `<root>/.githooks/`, so the one-line rule then covers every project in the repository.
