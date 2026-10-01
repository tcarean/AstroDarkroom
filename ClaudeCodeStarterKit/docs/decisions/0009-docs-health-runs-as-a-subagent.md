# 0009. The documentation audit runs in a subagent with a clean context

Date: 2026-09-25
Status: accepted 2026-09-25; the list of inputs superseded by 0018; the list of inputs superseded by 0025; the list of inputs superseded by 0028

## Context

`/docs-health` asks whether `docs/` alone would let a fresh model understand and change the
project. Run as a skill inside the main session, the model doing the audit has usually already
read `src/`, so it cannot tell what it learned from the docs and what from the code. The test
measured the wrong thing.

## Decision

`/docs-health` stays the command the owner types, but it only launches the `docs-auditor`
subagent defined in `.claude/agents/docs-auditor.md`: fresh context, an explicit list of
allowed inputs (`CLAUDE.md`, `README.md`, `CHANGELOG.md`, `docs/`, the test data README and
manifest, test names from `dotnet test --list-tests`), a hard rule never to open `src/`, a fixed
report format that ends with the list of files it opened, and no permission to change anything.
The main session relays the report and fixes nothing in that pass.

## Alternatives rejected

- **Keep it a skill and tell the model to "forget" the code.** It cannot; context is not
  selectively forgettable.
- **A second Claude Code session opened by the owner.** Works, and stays the right tool for
  code review, but the audit's output has to come back to the working session to be acted on,
  and a session cannot return a report to another. A subagent can.
- **Restrict the subagent's tools instead of its paths.** Tool permissions cannot exclude a
  directory; the prompt does, and the report's file list makes a breach visible.

## Consequences

The first run on the kit (2026-09-25) took about three minutes, opened 19 files, none under
`src/`, and produced 17 findings, most of them minutes to fix. A subagent file added to
`.claude/agents/` is only available in the next session; the first run after adding one goes
through a general-purpose subagent with the same prompt, which is what was done here.
