# 0004. Two interface languages chosen by `--lang`, English by default; English in the code, the comments, the docs and the HISTORY card; the tests run in Romanian

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

Every text the tool printed was Romanian without diacritics, and so was every comment, by
the old `CLAUDE.md` convention: "User-facing text (CLI messages, `--help`, code comments) is
in Romanian - keep new text consistent with this." The owner asked on 2026-10-26 for the
window in English and Romanian chosen by `--lang` as FlatCheck does, and approved (Q2, Q3,
Q10 of the plan) that the console take the same mechanism with English as its default, that
the HISTORY card be English in both languages, and that every comment be translated in this
task. FlatCheck (its ADR 0062), DarksCheck (ADR 0021), LightsCheck (ADR 0022) and
AstroPhotoAnalyzer (ADR 0048) made the same change on 2026-10-25, and the owner settled the
shared questions there. The kit's rule "Languages" asks for English in everything the model
reads or writes.

## Decision

- **FlatCheck's shape, FlatCheck's API.** `src/CopyWcs.Core/Language/Ui.cs` holds
  `UiLanguage`, `Ui.T(en, ro)` (used with `using static`), `Ui.Current` (the innermost
  `Ui.Use(lang)` scope, an `AsyncLocal`, else `Ui.Default`, English) and
  `LanguageArgument.cs` reads `--lang <code>` / `--lang=<code>` anywhere, case-insensitive,
  the last one counting. No culture is set for the language, so a number reads the same in
  both.
- **Both wordings sit where the text is composed**, `T("english", "romana")`, the Romanian
  without diacritics. The usage, whose columns are aligned by hand, is written whole in each
  language and gains the `--lang` line. The messages of the core (`WcsCopy`, `FitsHeader`)
  are pairs too, so the window shows the same words as the console.
- **The console** reads `--lang` after the banner (R-001 keeps the banner first) and before
  the options, so the usage and every parse error are worded in the language. A wrong value
  is an English `ERROR:` line and exit 1 (R-022). Without `--lang` the run keeps `Ui.Default`,
  English in production (R-021).
- **The HISTORY card is `WCS copied from <file>` in both languages.** A header card is data
  other programs read, not interface text; DarksCheck's `--out` file names are English in both
  languages for the same reason. The old `WCS copiat din` is not recognised: nothing reads it
  back.
- **English everywhere else**: every comment of the 16 files was translated in this task, one
  commit per project, with the XML documentation comments the rule "Documentation comments"
  asks for added in the same pass; the tests' assertion messages and two Romanian test file
  names too. The prototype keeps its Romanian docstrings (ADR 0001).
- **The tests run in Romanian.** `tests/CopyWcs.Tests/RomanianByDefault.cs` sets
  `Ui.Default` to Romanian in a module initializer, so every Romanian assertion written before
  this ADR stands; English is tested in its own scopes: `LanguageTests` (R-020 to R-023), whose
  R-023 runs every scenario of the console in both languages and compares exit codes and
  figures.
- **The rule "Languages" in `CLAUDE.md` is varied**: what the user sees is in both languages,
  the English beside the Romanian; everything else is English.

## Alternatives rejected

- **The console stays Romanian and only the window takes `--lang`.** The one tool in the
  folder whose console would differ from its window; and the window shows the core's lines,
  which must then exist in both languages anyway.
- **One table of texts, `.resx` files, the language as a parameter.** Rejected in DarksCheck's
  ADR 0021 for reasons that hold here: the texts are sentences built around figures and
  paths, a missing resource falls back silently, and a parameter would reach every signature
  for a value fixed once per run.
- **A HISTORY card per language, or the Romanian card kept.** A file would carry a different
  card depending on who copied; the owner chose English.
- **Translating only the comments the split touched.** The rule allows it, but the code base
  is 16 files and the split touched most of them; leaving a Romanian remainder would leave
  files in two languages.

## Consequences

- A new user-visible text is a `T(english, romanian)` pair from its first line; a new comment
  is English. `R023_` catches an English run that still prints a Romanian word from its list.
- The window (task 2) sets `Ui.Default` from `--lang` before its first view and says a bad
  value in an English message box, FlatCheck's way.
- `README.md` documents `--lang` (ADR 0006).
