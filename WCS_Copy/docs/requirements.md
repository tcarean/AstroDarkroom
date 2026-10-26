# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status
  `dropped`. Gaps in the numbering are deliberate: each block of ten is one area; a full block
  continues in the next free block of ten, which the area's heading then names.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`. `implemented` means a test named
  `R###_...` exists in a test project under `tests/` (`RepoInvariantTests` checks it both ways).
- Describe observable behaviour, with one concrete example that includes numbers. No adjectives.
- Error behaviour and the exit code are requirements too.

The explanations behind each row (why this way, what was measured) are in `docs/design/` and
`docs/decisions/`; the user documentation of the same behaviour is `README.md`.

## The console contract (R-001 to R-009)

| ID | Status | Requirement |
| --- | --- | --- |

## The copy (R-010 to R-019)

| ID | Status | Requirement |
| --- | --- | --- |

## The interface language (R-020 to R-029)

| ID | Status | Requirement |
| --- | --- | --- |

## The window (R-030 to R-039)

| ID | Status | Requirement |
| --- | --- | --- |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
