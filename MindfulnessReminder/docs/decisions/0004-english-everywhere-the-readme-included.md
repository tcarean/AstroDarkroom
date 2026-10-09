# 0004. English everywhere, the README included: the project's rule "All text is in English" extends the kit's rule "Languages"

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The kit's rule "Languages" puts English in everything the model reads or writes and allows
Romanian in `GHID.md` and the READMEs, which are for people (the kit's ADR 0012); FlatCheck,
AstroPhotoAnalyzer and ImageVisualCompare have Romanian READMEs. Mindfulness Reminder's
README has been English since the base version (61a0121, 2026-08-28), and its `CLAUDE.md`, since
it was first written (b6554d5, the periodic reminder, 2026-09-08), has carried the convention:
"**All text is in English** — UI strings, comments, documentation. (The sibling `FlatCheck` tool in
this repo is Romanian; that convention is its own, do not carry it over.)" On 2026-10-09 every
`.cs` and `.xaml` file, every csproj comment, `Directory.Build.props` and the 291-line
`README.md` were English; nothing needed translating, unlike the three earlier migrations.

## Decision

Everything in this project is English, the README included. The convention stays a project
rule in `CLAUDE.md`, "All text is in English", which names the kit's rule "Languages" as the one
it extends: where the kit allows a Romanian README, this project does not. The section the
migration adds to the README, on the gate and the documentation, is English too. This record is
both the migration's confirmation and the retroactive record of the convention, so it is not
repeated among the retroactive ADRs 0007 to 0033.

## Alternatives rejected

- **A Romanian README, as the kit allows and the sibling projects have.** The README is this
  project's user documentation in English since its first commit, and the convention says not to
  carry the sibling tools' language over.
- **Dropping the project rule as covered by the kit's.** The kit's rule allows what this one
  forbids; without it, a session could write the README in Romanian and break no rule.

## Consequences

- A new README section, a new UI string or a new comment is English.
- Nothing was translated in the migration.
