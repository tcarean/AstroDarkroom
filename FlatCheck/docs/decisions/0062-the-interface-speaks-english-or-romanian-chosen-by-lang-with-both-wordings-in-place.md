# 0062. The interface speaks English or Romanian, chosen by `--lang`, with both wordings in place

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0006 in part (Romanian as the only language of what the user sees)

## Context

Until 2026-10-25 every text FlatCheck shows was Romanian, written without diacritics, inline
where it is composed: about 600 messages in `FlatCheck.Core` and the console (the reports, the
refusals, the exceptions the user reads, `--help`, the parse errors) and about 300 in the
window (XAML, `SessionMessages`, the view-models). The owner asked for two interface languages,
English and Romanian, chosen by a command-line parameter `lang` (`--lang=ro` and `--lang ro`),
English when it is missing, in the console and in the window; translations that keep their
meaning and context; the existing tests that assert Romanian kept on the Romanian wording, with
as few test changes as possible, texts allowed to change and figures not.

Facts that shaped the design: the verdict words (`REFA`, `PROBLEMA`, `INAPLICABIL`...) are keys
the exit code, the severity, the window's colours and the advice compare; a few other texts are
keys too (the corner names, the series' reason keys, `Mozaic`, `SensorFormats.Unusual`), and
`--against` cut a sentence at its Romanian `" la ("`; the console's tests run in parallel; the
console, Core and tests run under `InvariantGlobalization`, so no culture can change a number.
ImageVisualCompare made the same request into one `UiText` table (its ADR 0038) for sixty texts.

## Decision

- **Both wordings sit in place**, as `T("english", "romana")` (`Ui.T`, `using static`) where the
  text is composed, in `Report/` and wherever else a text is written. The sentence keeps its
  context, the compiler asks for both wordings, and the code keeps its shape. A text whose
  columns are aligned by hand across many lines (the usage, two paragraphs) is written whole in
  each language, chosen by `Ui.Current`.
- **The language is a scope, not a culture.** `Ui.Current` is the innermost `Ui.Use(lang)` scope
  on the current flow (an `AsyncLocal`, which a task started inside inherits), else
  `Ui.Default`, English. No `CultureInfo` is set for the language, so a number reads the same
  in both.
- **`--lang` is read first, by `LanguageArgument.Extract`**: `--lang <code>` or `--lang=<code>`,
  anywhere, case-insensitive, the last one counting, the spaced form taking the next argument
  whatever it is; `en` and `ro`. The console takes it out before the usage and the parse, so
  both are worded in the language, and runs the rest in `Ui.Use`. A value it does not know, or
  none, stops the console with an English `ERROR:` and exit 1 (R-102).
- **Verdicts stay keys.** The analysis hands out the Romanian words it always did; only the
  printing goes through `VerdictWords.Of` (English OK, SUSPECT, PROBLEM, PERFECT, GOOD,
  ACCEPTABLE, REDO, UNDETERMINED, NOT APPLICABLE). The corners keep their keys, printed through
  `StructureReport.CornerName`; `HistEqReport.ViewCaption` takes the view's key. The `--against`
  spot cell no longer cuts a sentence: it uses `StructureReport.LocalSpotSize`.
- **The names of the files written are English, the same in both languages**: the console's
  second maps are `-world` and `-after` (they were `-lume` and `-dupa`); the window's files
  follow in its stage. Old Romanian names are not recognised (the owner's decision).
- **The FITS HISTORY cards follow the run's language**, like the report: they are text a person
  reads in Siril's header view, and an existing test pins the Romanian card.
- **The Romanian wordings are the ones the user read until now, byte for byte**, without
  diacritics; English is ASCII. The test assemblies set `Ui.Default` to Romanian once, in a
  module initializer, so every test written before keeps asserting Romanian unchanged.
- **Two stages.** The console and Core first; the window keeps `Ui.Default = Romanian` until
  its own texts are paired (R-101), so no screen mixes the two.

## Alternatives rejected

- **One table of texts (ImageVisualCompare's `UiText`).** Fine for sixty labels; for ~900
  sentences, most of them composed around figures, it would tear each sentence from the code
  that builds it, which is where its meaning is read.
- **`.resx` files and `CurrentUICulture`.** A missing key falls back silently, format
  placeholders are checked by nothing, and Core runs under `InvariantGlobalization`, where a
  Romanian culture cannot even be created.
- **Passing the language as a parameter.** It would reach every report and analyzer signature
  and most existing tests, for a value fixed once per run.
- **An enum for the verdicts.** Cleaner, but it moves `Analysis/`, the exit code mapping and
  many tests, for no change in behaviour.
- **File names per language.** A session written in one language would hide its files from the
  other; the owner chose English names for both.

## Consequences

(Restated 2026-10-25: the window's stage is done. `App`'s constructor reads `--lang` before the
resources and the first view load; a value it does not understand is said in an English message
once the window is up, and the window goes on in English (the owner's decision 4). A XAML label
is a `{l:T En='...', Ro='...'}` pair (`TExtension`); a verdict pill's text goes through
`VerdictToWordConverter` while its colour keeps the key. The window's files are named
`Test result.txt`, `Split result.txt`, `pp_flat_stacked_smoothed.fit`, `pp_flat_stacked_synthetic.fit`
and `part-<i>` in both languages.)

- A new text is a `T(english, romanian)` pair, never a single literal; a printed verdict goes
  through `VerdictWords.Of`.
- The English output is pinned by `Golden/en_*.txt`, one per main scenario; for those and for
  the 43 parity scenarios, `EnglishOutputTests` checks that the English run has the Romanian
  run's exit code and the same numbers, the same number of times, and no Romanian word (R-103).
- The four parity goldens of `--ratio` were recaptured, with the owner's yes, for the map's
  file name alone (`-lume.png` to `-world.png`); see `docs/design/python-parity.md`.
- Guards: `LanguageTests` (R-100, R-102), `EnglishOutputTests` (R-103), the Romanian goldens.
