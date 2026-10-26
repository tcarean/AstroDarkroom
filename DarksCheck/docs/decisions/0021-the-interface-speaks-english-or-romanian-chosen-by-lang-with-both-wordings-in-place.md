# 0021. The interface speaks English or Romanian, chosen by `--lang`, with both wordings in place

Date: 2026-10-25
Status: accepted 2026-10-25; the README in Romanian only superseded by 0022
Supersedes: 0005 in part (Romanian as the only language of what the user sees)

## Context

Until 2026-10-25 every text DarksCheck shows was Romanian, without diacritics, written where it
is composed: about 1,500 lines of text in `DarksCheck.Core` (`Report/`, the analysis' reasons and
refusals, the run's phase words, the copied readers), in the console (`Options`, `Program`,
`--help`) and in the window (`WindowTexts`, the view-model, the converters, the charts, the XAML).
The owner asked for two interface languages, English and Romanian, chosen by a command-line
parameter `lang` (`--lang=ro` and `--lang ro`), **English when it is missing**, in the console and
in the window. The translations must keep their meaning and context. The existing tests that assert
Romanian must keep asserting it, with as few test changes as possible; texts may change, figures
may not. FlatCheck (its ADR 0062) and AstroPhotoAnalyzer (its ADR 0048) made the same change the
same day; the owner settled there the shape (both wordings where the text is used), the wrong value
(an error in the console, a message then English in the window) and the README (Romanian only,
documenting `--lang`). Two questions were DarksCheck's own, and the owner answered them on
2026-10-25: the copied readers are paired here, and the `--out` file names are English in both
languages.

## Decision

- **FlatCheck's shape, FlatCheck's API.** `Language/Ui.cs` holds `UiLanguage`, `Ui.T(en, ro)` (used
  with `using static`), `Ui.Current` (the innermost `Ui.Use(lang)` scope, an `AsyncLocal` that the
  parallel decoding inherits, else `Ui.Default`, English) and `Language/LanguageArgument.cs` reads
  `--lang <code>` / `--lang=<code>` anywhere, case-insensitive, the last one counting. The names are
  FlatCheck's, so a FlatCheck hunk that uses `T(...)` compiles here unchanged. No culture is set for
  the language, so a number reads the same in both.
- **Both wordings sit where the text is composed**, `T("english", "romana")`. A text that was a
  `const` or a `static readonly` field becomes a property, picked when it is shown. The usage, whose
  columns are aligned by hand, is written whole in each language.
- **The console** reads `--lang` after the banner (R-001 keeps the banner first) and before the
  options, so the usage and every parse error are worded in the language. A wrong value is an
  English `ERROR:` and exit 1 (R-092). Without `--lang` the run keeps `Ui.Default`.
- **The window** sets `Ui.Default` from `--lang` before the first view loads; a wrong value is said
  in an English message once the window is up, and the window goes on in English. A XAML label is a
  `{l:T En='...', Ro='...'}` pair (`TExtension`, copied from FlatCheck), picked when the element is
  made.
- **Verdict words are recognised in both languages.** English prints `OK`, `WARNING`,
  `UNMEASURED`, `ERROR`; `ReportRow.VerdictOf` and the window's `VerdictPalette` read the words of
  either, so the chip and its colour follow the run's language. Two places that compared a Romanian
  phrase (the master's name in the mismatch sentence, the partner's darks in the inventory) compare
  a bool instead.
- **The `--out` names are English in both languages**: `darkscheck-report.txt`,
  `darkscheck-map-dark.png`, `darkscheck-map-bias.png`, `darkscheck-map-dark-minus-bias.png` (they
  were `darkscheck-raport.txt` and `darkscheck-harta-*.png`); the CSV names and their columns were
  English already. The old names are not recognised: nothing reads them back.
- **The copied readers are paired here, an exception to the rule "Copied files stay copies".**
  `FitsReader` takes FlatCheck's existing pairs token for token, as its own hunk (as ADR 0020 took
  the cut-FITS check). `RawReader`'s messages are LightsCheck's adaptations, which have no English
  yet; their pairs are written here, and LightsCheck takes them back in its own language task.
- **The tests run in Romanian.** Each test assembly sets `Ui.Default` to Romanian in a module
  initializer (`RomanianByDefault.cs`), so every Romanian assertion and golden written before this
  ADR stands, `help.txt` gaining its `--lang` line and the two renamed file names of its `--out`
  line. English is tested in its own scopes: the English goldens `Golden/en_*.txt` and
  `EnglishGoldenOutputTests` (same exit code, same numbers, no Romanian word, R-093),
  `LanguageTests` (R-090 to R-092) and the window's English pass (R-094).
- **The rule "Languages" in `CLAUDE.md` is varied**: what the user sees is in both languages,
  the English beside the Romanian; everything else stays as ADR 0019 says.

## Alternatives rejected

- **One table of texts (ImageVisualCompare's `UiText`).** Most texts are sentences built around
  figures; a table would tear each from the code that builds it, where its meaning is read.
- **`.resx` files and `CurrentUICulture`.** A missing key falls back silently, nothing checks the
  placeholders, and the console runs under `InvariantGlobalization`, where a Romanian culture cannot
  be made.
- **The language as a parameter.** It would reach every report and analyzer signature and most of
  the existing tests, for a value fixed once per run.
- **Leaving the copied readers Romanian until LightsCheck has its languages.** An English run would
  print a Romanian line for a frame it cannot read; the owner chose the English now.
- **File names per language.** A folder written in one language would look different from the
  same run in the other; the owner chose English names for both.

## Consequences

- A new user-visible text is a `T(english, romanian)` pair (a `{l:T ...}` pair in XAML) from its
  first line; a Romanian change changes its English in the same edit, and the reverse.
- The English is ASCII, the Romanian stays without diacritics, byte for byte what it was.
- `FitsReader` and `RawReader` now carry `Ui.T`; the next refresh from LightsCheck must keep the
  pairs (LightsCheck's own language task takes `RawReader`'s wordings from here).
- Guards: `LanguageTests`, `EnglishGoldenOutputTests`, the English goldens, the window's English pass, and
  every Romanian test written before.
