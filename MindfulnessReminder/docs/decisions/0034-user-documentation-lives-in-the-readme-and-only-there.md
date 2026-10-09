# 0034. User documentation lives in README.md, and only there

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Structure", the line "`README.md` —
**the** user-facing documentation, and the single source for it") and `README.md` itself; the
decision itself dates from b6554d5 (the periodic reminder, 1.1.0, 2026-09-08), which wrote
`CLAUDE.md`, while the README has been the user documentation since the base version (61a0121,
2026-08-28). The plan of the migration listed no ADR for it; the session that wrote ADR 0007 to
ADR 0015 found it unplaced, and the plan says such a decision is added at the end. The README
describes the behaviour a user sees, with the exact tooltip strings, the `--detect` output and a
"Design decisions" table; from 2026-10-09 the same behaviour is also stated, with IDs, in
`docs/requirements.md`, and explained in `docs/design/` and here, for whoever maintains the code.

## Decision

`README.md` is the user documentation and the only place for it: how to use the application,
what each switch and setting does, what the tooltip and the windows show, how to build, run and
deploy it. `CLAUDE.md`, `docs/` and the code comments do not restate it; they say what the code
does and why, for whoever maintains it, and point to the README where the user's view matters.
The requirement rows are the testable statement of the same behaviour, not a second user guide.
The README is English (ADR 0004).

## Alternatives rejected

- **A user guide under `docs/`.** The README is what a reader of the repository opens first, and
  a second guide beside it would drift from it.
- **Usage restated in `CLAUDE.md` or in comments.** Two copies of one explanation, one of them
  loaded into every session, and only one of them kept up to date.

## Consequences

- A user-visible change updates the README in the same task: the kit's `/wrap-up` checks it at
  its step 9, and `docs/skills.md` says where in the README each kind of change goes.
- A requirement row and a README paragraph about the same behaviour change together; the row
  carries the constants and the example a test can check, the README the words a user reads.
- Guards: none by test; the check is a step of `/wrap-up`.

Source: CLAUDE.md ("Structure"); README.md.
