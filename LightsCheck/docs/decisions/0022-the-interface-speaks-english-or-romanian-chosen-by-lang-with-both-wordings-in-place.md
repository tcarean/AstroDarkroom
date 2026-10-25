# 0022. The interface speaks English or Romanian, chosen by `--lang`, with both wordings in place

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0005 in part (Romanian as the only language of what the user sees)

## Context

Until 2026-10-25 every text LightsCheck shows was Romanian, written without diacritics where it
is composed (ADR 0005): about 400 texts in `LightsCheck.Core` (most in `Report/SeriesReport.cs`)
and the console, about 100 in the window (XAML and `WindowTexts`). The owner asked for two
interface languages, English and Romanian, chosen by a command-line parameter `lang`
(`--lang=ro` and `--lang ro`), English when it is missing, in the console and in the window;
translations that keep their meaning and context; the existing tests that assert Romanian kept on
the Romanian wording, with as few test changes as possible, texts allowed to change and figures
not.

FlatCheck took the same request the same day and settled its design with the owner (FlatCheck's
ADR 0062: both wordings in place, the language a scope over a start-up default, an unknown value
an English error); AstroPhotoAnalyzer took the same shape (its ADR 0048). Facts here: the
verdicts and the frame classes are enums in `Analysis/` and become words in `Report/` alone, so
no comparison reads a Romanian word, except the window's `VerdictPalette`, which colours a chip
by the start of its word; the console's tests run in parallel; the console, Core and tests run
under `InvariantGlobalization` and the window forces the invariant culture, so no culture can
move a number. Three questions were LightsCheck's own, asked on 2026-10-25.

## Decision

- **FlatCheck's design, taken as it is.** Every text is a `T("english", "romana")` pair where it
  is composed (`Ui.T`, `using static LightsCheck.Core.Ui`); a text aligned by hand across lines
  (the usage) is written whole per language. `Ui.Current` is the innermost `Ui.Use` scope on the
  flow (an `AsyncLocal`), else `Ui.Default`, English (`Ui.StartupDefault`); no culture is set.
  `LanguageArgument.Extract` takes `--lang <code>` / `--lang=<code>` out first, anywhere, in any
  case, the last one counting. A value it does not know, or none, stops the console with an
  English `ERROR:` and exit 1; the window says it in a message once it is up and goes on in
  English. XAML labels are `{local:T En='...', Ro='...'}` (`TExtension`, FlatCheck's).
- **The Romanian wordings stay byte for byte**; English is ASCII, British in spelling. The test
  assemblies set `Ui.Default` to Romanian in a module initializer (`RomanianByDefault`), so every
  test written before keeps asserting Romanian unchanged.
- **The verdict words are translated where the report makes them**: there are no Romanian keys
  to keep. `VerdictPalette.Severity` knows both languages' words, chosen so that no English word
  starts like a word of another severity (GOOD, NO, YES, MARGINAL, POSSIBLE, WARNING, BAD,
  INSUFFICIENT, ERROR, UNDETERMINED, NOT MEASURED, UNREAD; "NO walking noise" is as good as OK).
- **The `--out` files are named in English in both languages** (the owner's answer 1):
  `lightscheck-report.txt`, `lightscheck-frames.csv`, `lightscheck-rejected.txt`.
- **`--east left|right` and `--north up|down` sit beside `--est stanga|dreapta` and
  `--nord sus|jos`, in both languages**, either name taking either language's words; `--help`
  names the form of its language (the owner's answer 2).
- **The CSV's text cells follow the run's language** (class, reasons, trail axis, the arrows'
  sense letters, the error), like the report; its header stays the English it was (the owner's
  answer 3). So do the RA axis's source and the pixel size's source, worded when the run makes
  them.
- **The copied `FitsReader.cs` takes FlatCheck's paired wordings as one more hunk** (55e959c),
  the way ADR 0021 took the cut-data check: its four messages, word for word, with this
  project's `using static`; the rest of FlatCheck's later code stays out.

## Alternatives rejected

- **One table of texts (ImageVisualCompare's `UiText`).** For ~500 texts, most of them sentences
  built around figures, it would tear each sentence from the code that gives it its meaning.
- **`.resx` files and `CurrentUICulture`.** A missing key falls back silently, nothing checks the
  placeholders, and the core runs under `InvariantGlobalization`.
- **Passing the language as a parameter.** It would reach every report signature and most tests
  for a value fixed once per run.
- **Romanian or per-language file names; Romanian-only arrow options; English-only CSV cells.**
  The owner's three answers.

## Consequences

- A new text is a `T(english, romanian)` pair, never a single literal; a new verdict word gets its
  English twin in `VerdictPalette` in the same change.
- The English output is pinned by `Golden/en_*.txt`, one per golden scenario; `EnglishGoldenOutputTests`
  checks that each English run has the Romanian run's exit code and the same numbers, the same
  number of times, and no Romanian word (R-062). `WindowLanguageTests` renders every screen in
  English and checks the chips' colours in both languages (R-063).
- The mechanics, the glossary and the tests are in `docs/design/language.md`.
