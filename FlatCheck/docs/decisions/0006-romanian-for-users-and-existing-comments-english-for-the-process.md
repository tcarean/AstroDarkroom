# 0006. Romanian for what the user sees and for the existing code comments; English for the process files

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The kit's rule 14 (its ADR 0012): English in everything the model reads or writes, Romanian only
in the guide and the READMEs, never a file in two languages. FlatCheck predates that rule with
its own: user-facing text (CLI output, `--help`, window labels, `README.md`) in Romanian, because
the tool's output is the product and its reader is the owner; and code comments in Romanian too,
across 20 000 lines, including long header comments in the csproj files and in
`Directory.Build.props`. The documentation about the code (the old CLAUDE.md, now
`docs/design/`) was already English. Translating the comments would be a diff of thousands of
lines with no test behind it and no reader asking for it.

## Decision

Three zones, stated in CLAUDE.md rule 14:

- **Romanian**: everything the user sees (CLI text, `--help`, window labels, the report, the
  README), and the code comments in the existing source files, which stay as they are.
- **English**: everything about the process: `docs/`, `CHANGELOG.md`, the ADRs, the scripts and
  their headers, the tests of repository invariants, commit messages, `Directory.Build.props`
  (translated today, since the file was rewritten for central package management).
- A file is never left in two languages: a comment added to an existing Romanian file is
  Romanian; a new source file follows the language of its folder's neighbours, which today is
  Romanian for application code.

## Alternatives rejected

- **Translate every comment now.** A review-only diff over the whole code base, the kind the
  guide says to avoid (its `/simplify` chapter); and the comments are the "why" of the code, where
  a translation loses nuance without a test to notice.
- **English for new comments, Romanian left in place.** Files in two languages, which the kit's
  rule forbids for a reason: the model then picks a language per edit.
- **Apply the kit's rule 14 verbatim.** It would make the tool's own output non-compliant; the
  output being Romanian is a product decision, not a process one.

## Consequences

The design notes quote Romanian identifiers and verdict words (`PERFECT`, `REFA`, `nedeterminat`)
inside English prose; that is expected. Translating a source file's comments is a task on its own,
per area, with a CHANGELOG line under `Internal`, and would be the moment to revisit this ADR.
