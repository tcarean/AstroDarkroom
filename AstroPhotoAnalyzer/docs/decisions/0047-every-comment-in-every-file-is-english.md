# 0047. Every comment in every file is English

Date: 2026-10-24
Status: accepted 2026-10-24
Supersedes: 0005 in part (the Romanian comments of the tests and of the XAML and project files)

## Context

ADR 0005 (2026-10-02) made the code comments Romanian, the csproj files and
`Directory.Build.props` included, and the process files English. The kit's ADR 0035 then made
the comments of the code's C# files English, and the documentation backfill of 2026-10-22..24
(`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`)
translated them. Three places were left Romanian on purpose, and the rule "Languages" in
`CLAUDE.md` named them: the tests' comments, until a task of their own; the XAML comments; the
comments of the csproj files and `Directory.Build.props`. Measured on `main` at d99d53f with a
function-word scan of comment lines (the GPL headers and quoted output skipped): 371 Romanian
comment lines in 54 files, about 200 in the tests, about 90 in the XAML, about 80 in the
csproj and props files, 4 in `Icon/make_icon.py`.

On 2026-10-24 the owner asked for the comments to be translated and for a rule that comments
are English from now on ("fixeaza comentariile sa fie in engleza si pune o regula ca pe viitor
sa fie in engleza"), and, asked whether the XAML and project files should follow the tests,
chose all of them, so that the rule has no exception.

## Decision

Every comment in every file of the project is English: C# (`//`, `///`, `/* */`), XAML and
MSBuild (`<!-- -->`), Python and configuration files (`#`), a comment added to any file
included. Romanian stays only in what the user sees (CLI text, `--help`, window labels and
texts, the report, `README.md`, `THIRD-PARTY-NOTICES.md`) and, inside a comment or a test, in
quoted output, written as the user sees it. A test's own failure messages (the text an
assertion prints when it fails) are not output of the tool and are English, like its
comments; the expected output a test compares against stays as the user sees it. The comments
and failure messages that were Romanian are translated in the same task
(`docs/plans/2026-10-24-translate-the-last-comments.md`), with the backfill's glossary. The
rule "Languages" in `CLAUDE.md` says so.

## Alternatives rejected

- **Keep the three places Romanian, as ADR 0005 and the rule had it.** A rule with three
  exceptions, one of them temporary, and a reader who adds a comment has to know which file
  is in which language; the owner chose one language.
- **Translate the tests only.** The owner's choice, put to them with the counts: the XAML and
  project comments are a third of the lines, and leaving them would keep two of the
  exceptions.

## Consequences

- ADR 0005 keeps its decision on what the user sees and on the process files; its part on the
  Romanian comments is superseded, by the kit's ADR 0035 for the code's C# files and by this
  ADR for the rest. Its dated note of 2026-10-24 is history.
- The tool's code and output do not change: a token comparison with `main` shows the code
  folders' files identical with the comments stripped, and no golden file moves. In the tests,
  besides the comments, 30 failure messages in 13 files are translated, and two scans that
  skipped a missing project (`LicenseHeaderTests`, `VersionTests`) assert that it exists, so
  that a renamed project fails instead of leaving the check (the code review of this task).
