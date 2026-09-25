# 0012. English in everything the model reads or writes; Romanian only in the human guide

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The kit mixes two languages without saying where each one goes. Code, comments, CLAUDE.md,
ADRs, requirements, CHANGELOG, skills and commit messages are in English; `GHID.md` and the
READMEs are in Romanian. The only written hint was one sentence in the guide, saying that
comments for people "may stay in Romanian". When every script got a header comment, the
language had to be chosen again, in chat, and the choice contradicted that sentence. A choice
that has to be made twice needs a decision.

## Decision

Everything the model reads or writes in the working loop is in English: source code,
comments in code and scripts, CLAUDE.md, requirements, ADRs, plans, design notes, CHANGELOG,
skills, agents, test names, commit messages. What only people read is in Romanian: `GHID.md`,
`README.md` and `monorepo/README.md`. (`tests/testdata/README.md` stays English: the model
reads it when it adds a fixture.) Text shown to the end user of
the tool (CLI output, UI) is a per-project choice, recorded in that project's requirements.
The model translates when it moves content across that line; it never leaves a file in two
languages.

## Alternatives rejected

- **Comments in Romanian, as the guide allowed.** The model reads comments as instructions
  and context; its adherence is better in English, and the code around them is English. Two
  languages in one file are the worst reading experience for both audiences.
- **Everything in English, guide included.** The guide exists so the owner can read the
  reasoning quickly; a 370-line translation makes it slower to read and harder to maintain.
- **Leave it to judgement.** That is what forced the question at the first header comment.

## Consequences

CLAUDE.md rule 14 states the split; the model no longer asks. The sentence in the guide that
allowed Romanian comments is corrected. A project made from the kit that wants another
language for its users records it in `docs/requirements.md`, not here.
