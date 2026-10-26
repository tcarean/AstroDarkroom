# 0006. English everywhere, as the kit's rule "Languages" says; three Romanian-commented files are translated

Date: 2026-10-02
Status: accepted 2026-10-02; no rule in `CLAUDE.md` varies the kit's rule "Languages" superseded by 0038; README.md stays Romanian superseded by 0041

## Context

The kit's rule "Languages" (the kit's ADR 0012): English in everything the model reads or
writes, Romanian only in the guide and the READMEs, never a file in two languages. FlatCheck
(its ADR 0006) and AstroPhotoAnalyzer (its ADR 0005) vary it, because there every code comment
and every text the user sees was Romanian. ImageVisualCompare on 2026-10-02 was the other way
round: 36 of its 39 `.cs` files, every `.xaml` file, every text the user sees (dialog titles,
messages, the status bar, About), `CLAUDE.md` and `Directory.Build.props` were English. Romanian,
without diacritics, was in the comments and two assertion messages of
`src/ImageVisualCompare.Wpf/Version.cs`, `tests/ImageVisualCompare.Tests/VersionTests.cs` and
`tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs`, and in the two comments of the Wpf csproj
that held the version. `README.md`, the user documentation, is Romanian with diacritics.

## Decision

The project follows the kit's rule as it is; no rule in `CLAUDE.md` varies it. The comments and
the two assertion messages of the three files were translated, with the same meaning and no
change to code; the two csproj comments moved into `Directory.Build.props` in English (ADR
0004). The `Notice` array of `LicenseHeaderTests` is the licence text, not a comment, and is
untouched. `README.md` stays Romanian with diacritics, which the kit's rule allows for READMEs.

## Alternatives rejected

- **Keep the three files Romanian, as the two siblings keep theirs.** Three files out of
  thirty-nine in another language is what the kit's rule forbids for a reason: the model picks a
  language per edit. Here, unlike in the siblings, translating is a small diff.
- **Translate the README.** It is the user's documentation, written for a Romanian reader, and
  the kit's rule keeps READMEs for people.

## Consequences

A new comment is English everywhere. The texts the user sees were English before and stay so;
`README.md` is the one Romanian file, and it describes the same behaviour as
`docs/requirements.md` in its own words (ADR 0025).
