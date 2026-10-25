# Speak English or Romanian, chosen by `--lang`

## Context

Every text DarksCheck shows is Romanian today: about 1,500 lines of text in `DarksCheck.Core`
(`Report/`, the analysis' reasons and refusals, the run's phase words, the copied readers), the
console (`Options`, `Program`, `--help`) and the window (`WindowTexts`, `MainViewModel`,
`Converters`, `Charts`, the XAML). The owner asked on 2026-10-25 for two interface languages,
English and Romanian, chosen by `--lang` (`--lang=ro` or `--lang ro`, values `en` and `ro`),
**English when it is missing**, in the console and in the window. The translations must keep
their meaning and context (not text by text). The tests that assert Romanian keep asserting
Romanian, with as few test changes as possible: texts may change, figures may not.

FlatCheck (ADR 0062) and AstroPhotoAnalyzer (ADR 0048) made the same change the same day, and
the owner settled the shared questions there. This plan takes FlatCheck's shape, since DarksCheck
descends from it through LightsCheck and its reader copies will take FlatCheck's pairs.

Owner's answers in this task (2026-10-25):
- **Copied readers:** paired here. `FitsReader` takes FlatCheck's existing pairs token for token,
  as its own hunk (like ADR 0020). `RawReader`'s LightsCheck-only messages get their pairs here,
  and LightsCheck takes them back in its own language task.
- **`--out` file names:** English in both languages: `darkscheck-report.txt`,
  `darkscheck-map-dark.png`, `darkscheck-map-bias.png`, `darkscheck-map-dark-minus-bias.png`.
  The CSV names are already English.

Assumed from the owner's answers in FlatCheck and APA (stated, not asked again):
- Both wordings sit where the text is composed: `T("english", "romana")`.
- A wrong `--lang` value: in the console, an English `ERROR:` line and exit 1. In the window, an
  English message once it is up, and the window goes on in English.
- `README.md` stays Romanian only and documents `--lang`.
- Numbers never change with the language: no culture is touched.

## Design

- **`src/DarksCheck.Core/Language/Ui.cs` and `LanguageArgument.cs`**: FlatCheck's two files,
  adapted to DarksCheck's style (file-scoped namespace, nullable): `UiLanguage { English,
  Romanian }`, `Ui.T(en, ro)`, `Ui.Current` = the innermost `Ui.Use(lang)` scope (`AsyncLocal`, which
  the parallel decoding inherits) or `Ui.Default` (English), `LanguageArgument.Extract` (`--lang x`
  / `--lang=x`, anywhere, case-insensitive, the last one counting), `BadValueMessage` (English). The
  same API as FlatCheck, so FlatCheck's `FitsReader` pairs compile unchanged.
- **Console** (`Program.Run`): the banner first, as now (R-001). Then `LanguageArgument.Extract`.
  A bad value gives `ERROR: --lang expects en or ro, got 'xx'` and exit 1. Otherwise everything
  else runs inside `Ui.Use(chosen)`, or under `Ui.Default` when no `--lang` was given. `Options.Parse`
  gets the rest. `EROARE:` becomes `T("ERROR: ", "EROARE: ")`. `--help` is written whole in each
  language (its columns are aligned by hand) and gains the `--lang` line.
- **Core texts**: every user-visible literal in `Report/`, `Analysis/`, `Runs/`, `Session/` and
  `DarkOutputs` becomes a pair at its use. A `static readonly` text or `const` text becomes a
  property, so it is picked when it is shown. Texts that are keys are fixed so they do not depend
  on the language:
  - `ReportRow.VerdictOf` recognises the words of both languages (OK, ATENTIE/WARNING,
    NEMASURAT/UNMEASURED). `VerdictPalette.Severity` in the window does the same (and
    EROARE/ERROR), so the colours follow either.
  - `master == "dark-ul"` (DarkReport) and `darks == "dark-urile"` (InventoryReport) compare
    a phrase. They become a bool (or a pair picked once), so neither compares a translated
    phrase.
  - `ScaleReport.Title` and any other `const` text the window uses as a key are checked one by one.
- **Glossary**, fixed before the translation so all files agree: dark/darks, bias/biases,
  light/lights (kept, as in Siril), master, cadru = frame, scurgere de lumina = light leak, glow,
  deriva termica = thermal drift, pixeli fierbinti = hot pixels, pixeli morti = dead pixels, model
  fix = fixed pattern, platou = plateau, incalzire = warming, partener = partner, biblioteca =
  library, eticheta = label, index, ADU-eq, verificarea N = check N, sarit = skipped, OK / WARNING /
  UNMEASURED, EROARE = ERROR. Sentences are translated with the threshold and the check they
  describe in view, not word for word.
- **Window**: `App` reads `--lang` before the first view loads (`Ui.Default = lang ?? English`).
  An unknown value is shown in an English message box once the window is up. `FolderFromArgs`
  reads `LanguageArgument.Extract(args).Rest`. XAML labels become
  `{l:T En='...', Ro='...'}` (FlatCheck's `TExtension`, copied into `Presentation/`). `WindowTexts`,
  `MainViewModel`, `Converters`, `Charts` and `MainWindow.xaml.cs` take `T(...)` pairs. The saved
  report stays byte-identical to the console's in the same language (R-072).
- **Copied readers**: `FitsReader`'s messages take FlatCheck's pairs (marker updated, ADR).
  `RawReader`'s messages are paired here.
- **Tests**: each test assembly (`DarksCheck.Tests`, `DarksCheck.Wpf.Tests`) gets a
  `RomanianByDefault.cs` module initializer, `Ui.Default = Romanian`. The console without `--lang`
  keeps `Ui.Default`, so every existing Romanian assertion and the five Romanian goldens stand.
  The one exception is `help.txt`, which gains the `--lang` line (an intended change, read at
  wrap-up). Tests that read the old Romanian `--out` file names follow the rename (text, not figures).
  New tests:
  - `LanguageTests`: R-090 `--lang` parsing (all forms, the last one counting, case), R-091
    English when missing (production default), R-092 a bad value refused with exit 1.
  - `EnglishOutputTests`: R-093 English goldens `Golden/en_*.txt` for inventory, darks, lights,
    lights-only and help. Each English run has the Romanian run's exit code and the same numbers
    the same number of times, and no Romanian word from a list (`dark-uri`, `cadre`, `fata de`,
    `NEMASURAT`, `ATENTIE`, `sesiunii`, ...).
  - The window: R-094 `App` reads `--lang`, the folder argument ignores it, and an English
    smoke pass renders every tab with no Romanian word.

## Steps

1. Worktree `.claude/worktrees/Speak-English-or-Romanian-by-lang`, branch
   `claude/Speak-English-or-Romanian-by-lang`, from `main`. Save this plan as
   `docs/plans/2026-10-25-speak-english-or-romanian-chosen-by-lang.md`.
2. Requirements R-090..R-094 (`draft`), a new block "The interface language". ADR 0021 (supersedes
   0005 in part, and records the copied readers' exception and the English file names). The rule
   "Languages" in `CLAUDE.md` is varied: user-visible texts in both languages, in place.
3. `Ui`, `LanguageArgument`, the module initializers, `LanguageTests` (red), then the console wiring
   (green).
4. Core texts, file by file, with the glossary. Run the Romanian goldens after each file: they must
   not move. Large files (`DarkReport`, `ScaleReport`, `InventoryReport`, `DarkSeriesAnalyzer`,
   `LightsAnalyzer`, `IndexAnalyzer`, ...) may go to parallel forks that each own their own files,
   do not build, and get the glossary. I build, read every pair back and fix them.
5. File names, `FitsReader`/`RawReader` pairs.
6. Window: `TExtension`, `App`, XAML, view-models, `VerdictPalette`, window tests.
7. English goldens, `EnglishOutputTests`. Read every English golden whole for meaning and context.
8. Docs: `README.md` (Romanian, `--lang`, the new file names), the design notes
   (`session-and-console.md`, `window.md`, `imaging.md`, the README map), CHANGELOG (Added
   `--lang`, Changed file names), the `kit.json`-listed docs as needed. `/wrap-up`: gate green,
   golden diff read and reported, commit, merge proposed.

## Verification

- `scripts/verify.ps1` green (build `--no-incremental -warnaserror` first, see memory).
- The five Romanian goldens unchanged except the `--lang` line of `help.txt`. Any other moved
  line is a finding, so stop.
- `dotnet run --project src/DarksCheck.Cli -- <synthetic session>` with no `--lang`, with
  `--lang ro`, `--lang=EN` and `--lang xx`: English, Romanian, English, and `ERROR` with exit 1.
- The window started with `--lang ro` and without it. The tests render every tab in both.
- No formula or threshold changes, so `/validate` is not needed. The validation tests' asserted
  texts run in Romanian through the module initializer.

## What deviated

- The English golden tests are `EnglishGoldenOutputTests`, not `EnglishOutputTests`, so the
  documented regeneration filter `--filter GoldenOutputTests` takes them too.
- Two Romanian-phrase comparisons became bools as planned (`DarkReport.MatchText(m, partner,
  set)`, `InventoryReport.DarksVsLights(m, partner)`); `MatchText`'s new signature changed the
  argument of three calls in `LightsAndIndexTests` (`"dark-ul"` / `"partenerul"` to `false` /
  `true`), with the asserted Romanian texts unchanged.
- Kept identical in both languages on purpose, since the window keys on them: the section-2 row
  labels `master dark`, `master bias`, `dark - bias`, the section prefixes `5.`, `9.`, `INDEX`.
- `ThirdParty.Components` became a property (its roles and licences are shown in the About
  window).
- `README.md`: the window section said seven tabs and lacked `Explicatii scara ADU`; fixed to
  eight while documenting `--lang`.
- The core texts were paired by four parallel forks with one glossary; every English golden and
  the pairs of `DarkReport`, `Analysis/`, the readers and the window were read back after.
