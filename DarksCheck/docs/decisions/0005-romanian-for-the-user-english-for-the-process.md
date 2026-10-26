# 0005. Romanian for what the user sees, English for the process and the code

Date: 2026-10-11
Status: accepted 2026-10-11; the copied files' Romanian comments superseded by 0019; Romanian as the only language of what the user sees superseded by 0021; README.md and THIRD-PARTY-NOTICES.md in Romanian superseded by 0022

## Context

The kit's rule "Languages" (its ADR 0012): English in everything the model reads or writes,
Romanian only in the guide and the READMEs. FlatCheck varies it (its ADR 0006) and so does
LightsCheck (its ADR 0005): the tool's output is the product and its reader is the owner, so
the console text, the window and the report are Romanian. DarksCheck is for the same reader
and sits beside those two tools. The files copied from LightsCheck under `Imaging/` carry
FlatCheck's Romanian comments (ADR 0003).

## Decision

Three zones. Romanian: everything the user sees (console text, `--help`, window labels, the
reports and exported files, `README.md`, `THIRD-PARTY-NOTICES.md`) and the comments of the
files copied unchanged under `Imaging/`. English: everything about the process and every new
source file (code and comments, tests, `docs/`, `CHANGELOG.md`, the ADRs, scripts, commits).
A file is never in two languages: a comment added to a copied file is Romanian; a new file is
English even when it sits beside a copied one. The hand report under
`docs/validation/2026-08-12-smida/` stays as delivered, English, since it was written for the
process.

## Alternatives rejected

- **The kit's rule verbatim.** It would make the tool's own output non-compliant; the output
  being Romanian is a product decision.
- **Romanian comments everywhere, as FlatCheck.** FlatCheck's are history; for new code the
  kit's reason holds: the model reads and writes English more reliably.

## Consequences

The design notes quote Romanian identifiers and verdict words inside English prose. The
golden texts under `tests/DarksCheck.Tests/Golden/` are Romanian, since they are the output.
