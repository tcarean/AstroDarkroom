# 0006. `README.md` is the user documentation, in English

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The project had no `README.md`; `CLAUDE.md` described the tool for the model, and the usage
was the only text for a person. The kit expects the user documentation in the README, and the
kit's rule "Languages" allows a README in either language: DarksCheck's and FlatCheck's are
Romanian, AstroDarkroom's is English (its ADR 0009) because it is the front page of the
repository, whose root `README.md` is English (AstroDarkroom's ADR 0008). The owner approved
the recommendation of the plan's Q7.

## Decision

`README.md` is written in English: what the tool does and for whom, the console's options
with an example, the two languages and `--lang`, the warning about mirrored or flipped
images, the licence, and from task 2 the window. It is the one place for user documentation;
`CLAUDE.md` holds the project's rules and points at it, and `docs/requirements.md` holds the
behaviour with IDs. The docs-auditor may read it (`audit.alsoRead` in `kit.json`).

## Alternatives rejected

- **Romanian, as DarksCheck's and FlatCheck's.** The interface defaults to English since ADR
  0004 and the repository's front page is English; a Romanian README would be the one text of
  the tool a reader of that page could not read.
- **No README, the usage being enough.** The window has no usage, and the flip warning and the
  licence deserve a place a person finds without running anything.

## Consequences

- A behaviour change that a user sees updates `README.md` in the same task as
  `docs/requirements.md`.
- The Romanian wordings of the interface stay in the code, beside the English; the README
  quotes the English ones.
