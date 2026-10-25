# Offer the interface in English and Romanian, chosen by `--lang`

## Context

Every text LightsCheck shows is Romanian, inline where it is composed (ADR 0005). The owner
asked (2026-10-25) for two interface languages, English and Romanian, chosen by a `lang`
command-line parameter (`--lang=ro` and `--lang ro`), English when it is missing, in the
console and in the window. Translations keep their meaning and context (never literal by
literal). Existing tests that assert Romanian keep asserting the Romanian wording, with as few
test changes as possible: texts may change, figures may not.

FlatCheck (its ADR 0062, merged 71530a4 + 5f8bc6b) and AstroPhotoAnalyzer (ADR 0048) did the
same request today; LightsCheck takes FlatCheck's design, which the owner chose there: both
wordings in place as `T(en, ro)` pairs, `Ui.Current` an `AsyncLocal` scope over a start-up
default, an unknown `--lang` value = console `ERROR:` exit 1 / window message then English,
`README.md` stays Romanian.

Measured (survey 2026-10-25):
- Core: about 400 texts. `Report/SeriesReport.cs` holds most (~240), then `InventoryReport`,
  `FrameReport`, `RunReport`, `SeriesOutputs`; the runs (`SeriesRun`, `FrameRun`), a few in
  `Analysis/` (`SeriesAnalyzer` axis source, `StarCrops` position words, `NoiseAnalyzer`),
  `Session/`, `ThirdParty.cs`, and the readers (`FitsReader` 3, `RawReader` ~8).
- Verdicts and classes are enums in `Analysis/`; the words are made in `Report/` alone. The
  window colours a verdict by its word (`VerdictPalette.Severity`, prefix match on Romanian).
- Console: `Options.cs` (errors, `--est stanga|dreapta`, `--nord sus|jos`), `Program.cs`
  (usage, progress `cadrul 12 / 87`, `-> scris:`).
- Window: ~70 XAML literals (MainWindow, six views, About, two in `Dark.xaml`), `WindowTexts`
  (the window's own words), a few in `MainViewModel`/`Charts`. The session card parses the
  header lines on `"    "` and `" : "`, so both languages keep that shape.
- Tests: 5 goldens, all via `Cli.Run` -> `Program.Run`; `LightsCheck.Tests` has no module
  initializer, `LightsCheck.Wpf.Tests` has one (`TestSettings`). Console and Core run under
  `InvariantGlobalization`, the window forces the invariant culture: a language cannot move a
  number as long as no culture is set.

## Owner's decisions (2026-10-25, this task)

1. The `--out` files get English names in both languages: `lightscheck-report.txt`,
   `lightscheck-frames.csv`, `lightscheck-rejected.txt`.
2. `--east left|right` and `--north up|down` are accepted beside `--est stanga|dreapta` and
   `--nord sus|jos`, in both languages; `--help` shows the form of the chosen language.
3. The CSV's text cells (class, reasons, trail axis `oblic`, error) follow the run's language;
   the header stays the English it already is.

## Design

**Mechanism (new `src/LightsCheck.Core/Language/`, LightsCheck's own, modelled on FlatCheck's):**
`UiLanguage`, `Ui` (`T(en, ro)` via `using static`, `Current`, `Default`, `StartupDefault =
English`, `Use(lang)` scope in an `AsyncLocal`), `LanguageArgument.Extract(args)` (both forms,
anywhere, case-insensitive, last wins, spaced form takes the next argument whatever it is;
`BadValueMessage` English only).

**Console.** `Program.Run` writes the banner, extracts `--lang`, stops on a bad value with
`ERROR: --lang expects en or ro, got 'fr'` and exit 1, else runs the rest inside
`Ui.Use(lang)` (or `Ui.Default` when no `--lang`). `--help`, the parse errors, the progress and
the reports come out in that language. `WriteUsage` written whole per language (aligned
columns), with a `--lang` line. `Options` accepts both arrow-option forms (decision 2).

**Core texts.** Every user text becomes a `T("...", "...")` pair where it is composed; the
Romanian wording stays byte for byte (no diacritics), English is ASCII. A `static readonly`
list of texts (warranty lines, `ThirdParty`) becomes a property, a `const` text a property; a
`const` that is a key or a file name stays. `RunReport.Error` gives `ERROR: ` / `EROARE: `.
File names per decision 1. Readers: `RawReader.cs` (adapted, ours) gets pairs; `FitsReader.cs`
(a copy) takes FlatCheck's already paired lines as hunks, the way ADR 0021 took the cut-data
check (FlatCheck's `using static` line becomes ours), its header noting the hunk.

**Verdict colour.** `VerdictPalette.Severity` learns the English words beside the Romanian
ones (GOOD, OK, NO, YES, NO walking noise, BORDERLINE, POSSIBLE, WARNING, POOR, INSUFFICIENT,
ERROR, UNDETERMINED, NOT MEASURED, UNREAD); the English words are chosen so that no prefix
collides. A test pins that every verdict word the core prints in English colours the same as
its Romanian twin.

**Window.** `App`'s constructor reads `Environment.GetCommandLineArgs()` through
`LanguageArgument.Extract` and sets `Ui.Default` before App.xaml's resources and the first
view load; a bad value is shown in a message box once the window is up, then English.
`FolderFromArgs` looks only among the arguments `Extract` leaves. XAML labels become
`{l:T En='...', Ro='...'}` (`Presentation/TExtension.cs`, FlatCheck's one-time-binding version,
so templates in `Dark.xaml` pick at element creation). `WindowTexts`, the view-model and chart
literals become pairs. The export status, dialog titles and the `EST`/`NORD` selectors' labels
(`EAST`/`NORTH`) follow.

**Translation method.** Area by area, reading the whole report section, its README section
and the Veil report's wording first; English follows Siril's and the usual astrophotography
vocabulary, British spelling as the comments. The glossary goes in the design note (frame,
trail, elongation, dew, drift, polar alignment, read noise, hot pixels, move, distinct
positions, rejected; REZUMAT -> SUMMARY, EXPUNERE SI ZGOMOT -> EXPOSURE AND NOISE, ROUA -> DEW,
CADRE MISCATE -> TRAILED FRAMES, DRIFT SI ROTATIE -> DRIFT AND ROTATION, RESPINSE -> REJECTED).

**Tests (minimal changes).**
- `LightsCheck.Tests` gets a `[ModuleInitializer]` (`RomanianByDefault.cs`) setting
  `Ui.Default = Romanian`; `Wpf.Tests` sets it in its own initializer. Every existing assertion
  and the Romanian goldens run unchanged and need no `--lang`.
- Expected changes, texts only: `help.txt` (the `--lang` line, possibly the arrow line), the
  three file names in `AcceptanceTests` and `MainViewModelTests` (`SeriesTests` uses the
  constants). No figure changes anywhere.
- New, all `R06x_...`:
  - R-060 `LanguageTests`: both forms, anywhere, last wins, case, default English
    (`Ui.StartupDefault`), help and a parse error in each language.
  - R-061: `--lang fr` / a trailing `--lang` -> English `ERROR:`, exit 1; window keeps it and
    goes on in English.
  - R-062 `EnglishOutputTests`: the 5 golden scenarios run with `--lang en`, pinned as
    `Golden/en_*.txt`; each compared with its `--lang ro` run: same exit code, same multiset of
    numbers; English scanned for Romanian marker words (outside paths).
  - R-063 window: `App` start-up and `FolderFromArgs` with `--lang`; every tab rendered with a
    real result in English, the visual tree's texts free of Romanian words; Romanian still
    shows `Rezumat`. `LIGHTSCHECK_DUMP_SCREENS` saves `en-*.png`.
  - R-064: `--east right --north down` and `--est dreapta --nord jos` give the same report.
  - Verdict palette pairs (part of R-063).

**Docs.** `docs/requirements.md` R-060..R-064 plus R-028/R-032 file names and R-004 usage;
ADR 0022 (two languages, pairs in place, AsyncLocal, English file names, arrow aliases, CSV
cells follow the language; supersedes 0005 in part; cites FlatCheck ADR 0062 for the shared
design); `CLAUDE.md` rule "Languages" updated; `docs/design/language.md` (Covers, glossary,
tests) + index row; `session-and-console.md`, `window.md`, `series-analysis.md` where they quote
names; `README.md` (Romanian): `--lang`, the arrow aliases, the new file names; CHANGELOG:
Added `--lang`, the English arrow options; Changed the file names. A version bump seems
warranted (minor): the owner's call.

## Branch

One branch, `claude/Offer-the-interface-in-English-and-Romanian`, worktree
`.claude/worktrees/Interface-in-English`, from main 38e01db. Commits: mechanism + console +
Core + goldens; then the window; then docs. Merge proposed at the end.

## Verification

- `dotnet build --no-incremental -warnaserror --no-restore`, then `scripts/verify.ps1` green.
- The whole golden diff read: Romanian goldens byte-identical except `help.txt`'s new lines;
  each `en_*.txt` read in full for meaning and tone.
- `/validate` is not needed by the rule (no formula or `Thresholds.cs` constant changes); the
  numbers-multiset test covers the reports. Run it anyway if `Report/` moved a figure.
- By hand: `dotnet run --project src/LightsCheck.Cli -- <Veil session> --lang=en` and
  `--lang ro`, `--lang fr` (exit 1); the window with `--lang ro` and without, screens dumped.
- A subagent review of the diff before wrap-up.

## Outcome (recorded at the end of the task)

- **Done as planned**, on one branch. The window's verdict words needed no converter: LightsCheck
  has no Romanian keys (its verdicts are enums), so `VerdictPalette` learned the English words.
- **Deviation, R-063's example:** the English twin of `LA LIMITA` is `MARGINAL`, the same word as
  the frame class, not `BORDERLINE`.
- **Test changes, texts only:** the three `--out` file names in `AcceptanceTests` and
  `MainViewModelTests`; `GoldenOutputTests.Scrub` and `Compare` became `internal` for
  `EnglishOutputTests`; `RomanianByDefault.cs` linked into the window's test project. Golden
  `help.txt`: three lines (the `--lang` usage, the file names, the `--lang` line); the other four
  Romanian goldens are byte-identical.
- **Found on the way:** the Frames tab's `dithering` tooltip still named the forum default arrows
  of ADR 0013 (left = east), which ADR 0014 replaced; both wordings now point at the EAST / NORTH
  selectors.
- **By hand, on the Veil session (87 NEFs):** `--lang=en` and `--lang ro` both exit 0 and print
  the same 1965 numbers; `--lang fr` prints the banner and `ERROR: --lang expects en or ro, got
  'fr'`, exit 1. `/validate` was not re-run: no formula or `Thresholds.cs` constant changed.
