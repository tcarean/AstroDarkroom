# 0025. User documentation lives in README.md, in Romanian, and only there; docs/requirements.md is for maintainers

Date: 2026-10-02
Status: accepted 2026-10-02; README.md in Romanian superseded by 0041

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` (its third paragraph, "User documentation
lives in `README.md`. Do not restate it here or in comments.") and the callout at the top of
`README.md` ("Documentația de utilizator stă aici, în acest fișier. Nu o duplica în
comentarii."); the decision itself dates from 591f433 (documentation for image compare,
2026-09-08), which wrote both files. The README is for the person using the tool, in Romanian
with diacritics: which files it takes, how to start it, the controls, what the status bar
shows, the memory a pair costs, how to build it, what it does not do yet, and the licence.
Everything else in the project is English (ADR 0006). Since today `docs/requirements.md` also
states the behaviour, with IDs, because the kit's rule "Requirements first" starts every
behaviour change there (ADR 0001).

## Decision

- `README.md` is the user documentation and the project's one Romanian file (ADR 0006; the
  kit's rule "Languages" keeps READMEs for people). How to use the tool is written there and
  nowhere else: code comments say why the code is the way it is, `CLAUDE.md` holds rules and
  pointers, and neither restates the README.
- `docs/requirements.md` states the same behaviour in English, with IDs that the tests' names
  carry, for whoever maintains the code. The README is the user's version of it, in the user's
  language: the two describe one behaviour in their own words, and a change to that behaviour
  changes both in the same task.

## Alternatives rejected

- **Restating it in `CLAUDE.md` or in comments.** A second copy of every behaviour to keep in
  step (two places drift, as ADR 0004 says of the version), and `CLAUDE.md` loads into every
  session (ADR 0001).
- **One description only: the README in English, or the requirements in its place.** The README
  is written for a Romanian reader and the kit's rule keeps READMEs for people (ADR 0006); the
  requirements are rows with IDs, in English, for whoever maintains the code.

## Consequences

- A user-visible change updates the README in the same task as its requirement row. The kit's
  `/wrap-up` checks that the README describes it (its step on `README.md`), and the
  `docs-auditor` reads the README too (`kit.json`, `audit.alsoRead`; ADR 0002).
- `CLAUDE.md` keeps this as a rule that cites this record.
- Guards: none by test; the kit's `/wrap-up` reads the README against every user-visible change.

Source: CLAUDE.md (third paragraph); README.md (the callout under the introduction);
.claude/skills/wrap-up/SKILL.md (the step on README.md); kit.json (`audit.alsoRead`); the diff of
591f433.
