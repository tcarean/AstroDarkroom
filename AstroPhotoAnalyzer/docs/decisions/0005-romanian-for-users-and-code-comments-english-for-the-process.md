# 0005. Romanian for what the user sees and for the code comments; English for the process files

Date: 2026-10-02
Status: accepted 2026-10-02; Romanian comments in the C# files superseded by the kit's ADR 0035 (the code's files translated by 2026-10-24); the Romanian comments of the tests and of the XAML and project files superseded by 0047; Romanian as the only language of what the user sees superseded by 0048

## Context

The kit's rule "Languages" (the kit's ADR 0012): English in everything the model reads or writes,
Romanian only in the guide and the READMEs, never a file in two languages. AstroPhotoAnalyzer,
like FlatCheck before it (its ADR 0006), predates that rule with its own, written in its first
`CLAUDE.md`: the text the user sees (CLI messages, `--help`, window labels, the report) and the
code comments are Romanian, without diacritics in code and messages and with diacritics in
Markdown; the documentation about the code (`CLAUDE.md`, `docs/`) is English. The tool's output
is the product and its reader is the owner. The comments, including the long headers of the
csproj files and of `Directory.Build.props`, carry the reasons behind the code.

## Decision

Three zones:

- **Romanian**: everything the user sees (CLI text, `--help`, window labels and help texts, the
  report, `README.md`, `THIRD-PARTY-NOTICES.md`), without diacritics in code and messages and
  with diacritics in Markdown; and the code comments of the existing files, including the
  csproj files and `Directory.Build.props`, which stay as they are. A comment added to one of
  them is Romanian.
- **English**: everything about the process: `CLAUDE.md`, `docs/`, `CHANGELOG.md`, the ADRs,
  `kit.json`, `.editorconfig`, `.gitattributes`, `.gitignore`, `Directory.Packages.props`, the
  test data README and manifest, the tests that enforce the process (`RepoInvariantTests`,
  `ValidationTests` and their helper `TestData`), commit messages.
- A file is never left in two languages.

## Alternatives rejected

(Restated 2026-10-24: "the code comments of the existing files ... A comment added to one of
them is Romanian" in the Decision above holds no longer for the C# files of the code: the kit's
ADR 0035 made their comments English, and the backfill of 2026-10-22..24
(`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`)
translated the last of them, so a comment added to one of them is English. What stays Romanian:
the comments of the csproj files and `Directory.Build.props`, the XAML comments, and the test
files' comments until a task of their own translates them, a comment added to a test file
following that file's language. `CLAUDE.md`'s rule "Languages" says the same.)

- **Translate every comment now.** A diff over the whole code base with no test behind it, for
  no reader asking for it; the comments are the "why" of the code, where a translation loses
  nuance and nothing notices.
- **English for new comments, Romanian left in place.** Files in two languages, which the kit's
  rule forbids for a reason: the model then picks a language per edit.
- **The kit's rule as it is.** It would make the tool's own output non-compliant; the output
  being Romanian is a product decision, not a process one.

## Consequences

The docs quote Romanian identifiers and words of the output (`PROBLEMA`, `ATENTIE`, "Nivelul
fundalului", `fiindca:`) inside English prose; that is expected. Translating a file's comments is
a task of its own, per area, and would be the moment to revisit this ADR.
