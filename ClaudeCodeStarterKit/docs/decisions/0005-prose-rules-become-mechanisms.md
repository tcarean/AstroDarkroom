# 0005. Rules the model must remember become mechanisms the model cannot skip

Date: 2026-09-25
Status: accepted 2026-09-25; item 1 and the CHANGELOG half of item 3 superseded by 0023

## Context

CLAUDE.md is loaded every session, so the rules are always in front of the model. Adherence to a
rule written in prose is still probabilistic, and drops in long sessions. The owner asked whether
the documentation structure would be maintained automatically in a future conversation. The
honest answer was "mostly", and "mostly" is not a gate.

## Decision

Three prose rules get a deterministic backstop:

1. **CHANGELOG at commit.** `scripts/check-staged.ps1` refuses a commit whose staged files touch
   `src/` without touching `CHANGELOG.md`. Run by the git pre-commit hook and by the Claude Code
   commit gate. A pure refactor gets one line under `Internal`; there is no bypass flag.
2. **Requirements and tests in step.** `RepoInvariantTests` reads `docs/requirements.md` and the
   test method names: every `implemented` requirement has a test whose name starts with its ID,
   and every such test names a requirement that exists.
3. **Turn cannot end dirty.** A PostToolUse hook records which files the session edited; a Stop
   hook refuses to end a turn that edited `src/` or `tests/` until `scripts/verify.ps1` ran
   green after the last edit (it writes `.verify-stamp`) and `CHANGELOG.md` is modified. The
   refusal message points to `/wrap-up`. `stop_hook_active` lets the second attempt through, so
   a session is never trapped.

## Alternatives rejected

- **Trust CLAUDE.md.** It is what was in place; the question was asked because it is not enough.
- **A Stop hook that inspects the whole working tree.** Would nag about the owner's own
  uncommitted work during a discussion; the per-session edit list scopes it to what the model did.
- **A bypass environment variable for the CHANGELOG rule.** The model would learn to use it. A
  human who really needs to bypass has `--no-verify` and knows what it means.

## Consequences

What still relies on judgement: whether a change deserves an ADR, whether a requirement is well
written, whether a validation figure is right. Those stay with the owner, through the diff read
and the accepted requirement. The Stop hook adds one verify run per code-editing turn.
