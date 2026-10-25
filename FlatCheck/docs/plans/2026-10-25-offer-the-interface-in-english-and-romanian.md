# Offer the interface in English and Romanian, chosen by `--lang`

## Context

Every text FlatCheck shows today is Romanian, written without diacritics, inline where it is
composed (ADR 0006). The owner asked for two interface languages, English and Romanian, chosen by
a `lang` command-line parameter (`--lang=ro` and `--lang ro`), English when it is missing, in both
the console and the window. The translations must keep their meaning and context, not be made
literal by literal. The existing tests that assert Romanian keep asserting the Romanian wording,
with as few test changes as possible: texts may change, figures may not.

Measured (three surveys, 2026-10-25):
- Core and console: about 600 distinct messages. Report/ holds about 470, Runs/ 46 (a whole
  `AgainstReport` inside `AgainstRun.cs`), LensProfiles/ 19, Analysis/ 8, the root (exceptions,
  ThirdParty, verdict words) about 40, the console 67 (`--help`, `Options` errors). There is no
  central string table.
- Window: about 150 XAML literals, `SessionMessages.cs` (79 members, the long guides), about 60
  literals in the view-models and dialogs. The report panel, verdicts and advice come from Core.
- Tests: 73 goldens (43 parity, 30 main), all through `GoldenOutputTests.RunCli`, which calls
  `FlatCheckCli.Run`. About 415 Romanian assertion literals in 37 files. `FlatCheck.Tests` runs
  test classes in parallel; `FlatCheck.Wpf.Tests` runs serially.
- Numbers already ignore the language. `InvariantGlobalization` covers the console, Core and
  tests, and the window forces `InvariantCulture`. Switching the language therefore cannot
  change a figure, as long as no culture is ever set.
- Text used as a key: the verdict words (`Stats`, `ExitFor`, `Severity`, `VerdictPalette`,
  `StructureReport.Advice`), the corner names (`"stanga-sus"`), the series reason keys
  (`"sem"`, `"corelatie"`), `"Mozaic"`, `SensorFormats.Unusual`, `_cropNote.StartsWith("crop
  necunoscut")` (SyntheticTabViewModel:265), and `AgainstRun.cs:323`, which cuts a sentence at
  `" la ("`.

## Owner's decisions (2026-10-25)

1. Verdicts are **translated when written**. Internally they keep today's keys, so exit codes,
   colours and the Romanian goldens do not move. English: OK / SUSPECT / PROBLEM, PERFECT /
   GOOD / ACCEPTABLE / REDO, UNDETERMINED, NOT APPLICABLE.
2. The **files written to disk get English names, the same in both languages**:
   `Test result.txt`, `Split result.txt`, `_smoothed`, `_synthetic`, the map suffixes
   `-world` / `-after`, the folders `part-N`. Old Romanian names are **not** recognised: an
   existing `_netezit.fit` or `_sintetic.fit` is no longer shown as the smoothed or synthetic
   master.
3. The two wordings sit **in place, as en/ro pairs**, where each text is composed.
4. **An unknown or missing value:** the console stops with an English `ERROR`, exit 1; the
   window shows a message and goes on in English.
5. **Parity goldens:** only the 4 `parity_ratio_*.txt` may be recaptured, and only for the map
   path line (`-lume.png` → `-world.png`). The other 39 stay byte for byte.

## Design

**Mechanism (Core, new `FlatCheck.Core/Language/`).**
- `UiLanguage` (English, Romanian).
- `Ui`, with:
  - `Ui.T(string en, string ro)`, used as `using static`, so a literal becomes
    `T("...", "...")`. An interpolated text keeps both interpolations, with the same format
    strings.
  - `Ui.Current`: the language of the run. A scope set by `Ui.Use(lang)` is held in an
    `AsyncLocal`, so parallel tests cannot race. Otherwise `Ui.Default` applies: English, set
    once at start-up.
- `LanguageArgument.Extract(args)`:
  - takes `--lang X` / `--lang=X` out, anywhere, case-insensitive, the last one counting;
  - never takes it for a path;
  - returns the language, or the bad value, plus the remaining arguments in order.
- Romanian wordings stay **byte-identical** to today's, without diacritics. English is ASCII
  only (the split mosaic's `BitmapFont` has capitals only, and the ASCII tests).

**Console.** `FlatCheckCli.Run` extracts `--lang` first. That way `--help`, a bare
`flatcheck --lang ro` (help) and every parse error come out in the chosen language. It then
runs the rest inside `Ui.Use(language)`. `Options.Parse` is unchanged apart from its messages.
`WriteUsage` gains the `--lang` line. Map suffixes become `-world` / `-after`.

**Core texts.** Every literal in Report/, Runs/, Analysis/, LensProfiles/, the exceptions,
ThirdParty and the split-mosaic labels becomes a pair:
- **Verdicts:** `Stats.Word(key)` in Report/ writes the word in the current language. Every
  place that prints a verdict goes through it; keys and comparisons stay as they are.
- **The other keys that are text** (corners, reason keys, `Mozaic`, `Unusual`, the crop note,
  the `" la ("` cut) become keys or flags, compared without language. Each change starts with a
  failing test (the rule "Test before fix" applies to the cut, a language bug).
- **FITS HISTORY cards** are written in English in both languages, like the file names
  (assumption: they are file metadata, not interface).

**Window.**
- `App.ApplyCommandLine` sets `Ui.Default` before the first window. `FolderFromArgs` skips
  `--lang` and its value.
- XAML literals become `{l:T En='...', Ro='...'}`, a small markup extension that picks once at
  load. The text stays in the XAML, in its context.
- `SessionMessages`, the view-models, the dialogs and file filters become pairs. The constants
  in `SirilSession` / `SplitPlan` get the English names.

**Translation method.**
- Translated area by area, reading the whole report or guide and the README section of that
  mode first, never literal by literal.
- English uses Siril's and the usual astrophotography vocabulary (master flat, light, stack,
  vignetting, residual).
- A glossary goes in the design note so terms stay consistent across Core and window.

**Tests (minimal changes to existing tests).**
- A `[ModuleInitializer]` in each test assembly sets `Ui.Default = Romanian`, so the roughly
  415 direct assertions run unchanged.
- `RunCli` prepends `--lang ro` (one line). The no-argument help test still prints help,
  because extraction comes before the help check. The two `FlatCheckCli.Run` calls in
  `VersionTests` get `--lang ro`.
- Expected file names change: the map paths in about 10 main goldens plus the 4 parity lines,
  `SirilSessionTests`, `out-lume.png`, and `parte-` → `part-`. `help.txt` gains the `--lang`
  line. No figure changes anywhere.
- New tests:
  - R-099: console `--lang`, both forms, anywhere, last wins, help and errors in the language.
  - R-100: window `--lang` and `FolderFromArgs`.
  - R-101: an unknown or missing value.
  - R-102: English goldens `en_*.txt` for the 30 main scenarios. These are where the owner
    reads the English.
  - `R102_TheNumbersAreTheSameInBothLanguages`: the multiset of numbers in each scenario's
    Romanian and English output is equal.
  - `R102_TheEnglishOutputHasNoRomanian`: a marker-word scan of the English console output, and
    of the window's English render, walking every text in the visual tree.

**Docs.**
- ADR 0062: two interface languages, inline pairs, `AsyncLocal` with a start-up default,
  verdicts as keys, English file names. It supersedes 0006 in part.
- `CLAUDE.md` rule "Languages" updated (stays ≤ 200 lines).
- `docs/design/language.md` (Covers, glossary) plus the index; `requirements.md` R-099..R-102
  (renumbered if taken by then).
- `README.md` (still Romanian): `--lang` in "Opțiuni" and in the window section, and the new
  file names.
- CHANGELOG: Added `--lang`; Changed file names.
- A version bump seems warranted (new option, renamed files): the owner's call.

## Stages: two branches, in order

1. `claude/Offer-the-console-in-English-and-Romanian`, worktree `.claude/worktrees/Console-in-English`:
   - the mechanism, the console and every Core text, plus the goldens;
   - the window calls `Ui.Default = Romanian` at start-up, so it stays entirely Romanian until
     stage 2 (no mixed screens);
   - merge proposed at the end.
2. `claude/Offer-the-window-in-English-and-Romanian`, from main after stage 1 merges:
   - window `--lang`, XAML, SessionMessages, the view-models, the window's file names;
   - the README's window section.

## Verification

- Each stage: `scripts/verify.ps1` green (with `--no-incremental -warnaserror` first).
- The whole golden diff is read; every changed line is tied to a cause, listed at wrap-up.
- The 39 untouched parity goldens are byte-identical; the 4 changed ones differ only on the
  path line.
- `/validate` on the real masters, with the drift table pasted (Report/ moved; zero drift
  expected).
- Run by hand:
  - `dotnet run --project FlatCheck -- <master> --lang=en` and `--lang ro` on a real master;
  - `flatcheck --lang fr` (exit 1);
  - stage 2: window renders via `FLATCHECK_DUMP_SCREENS` in both languages.

## Deviations (stage 1, recorded at the end of the stage)

- **Requirement IDs.** R-100 to R-103 instead of R-099 to R-102: the requirements file groups
  IDs in blocks of ten, one area each, and R-095..R-098 is the licence area.
- **`RunCli` is unchanged.** The plan prepended `--lang ro` in the harness. A console run
  without `--lang` keeps `Ui.Default` instead of forcing English, and `Ui.Default` is English
  in the program and Romanian in the test assemblies, so neither `RunCli` nor `VersionTests`
  needed an edit. "No `--lang` means English" is pinned by `Ui.StartupDefault`.
- **FITS HISTORY cards follow the run's language** instead of English in both: an existing
  test (`LensProfileTests`) pins the Romanian card, and the cards are read by a person in
  Siril's header view, like the report.
- **Test changes, all texts:** `GoldenOutputTests` gained `internal` on four fixture helpers
  (reused by `EnglishOutputTests`) and `out-lume.png` became `out-world.png` in R020.
- **Found on the way:** `--against`'s spot cell cut `StructureReport.LocalSpotValue` at its
  Romanian `" la ("`; it now uses `StructureReport.LocalSpotSize`. The English golden of
  `against_radial` is its test.
- **Review follow-up (subagent review of 55e959c):** one message left Romanian in English runs
  (`RawReader`, invalid raw dimensions) now paired; R-006, R-020 and R-061 name `-world` and
  `-after`; two doc comments cited a non-existent R-099; `StartupLanguageTests` runs the console
  with the program's own default (English), which no test did; one English sentence reworded.
  For stage 2: `LensChoice.Calibration` is built at catalog load with `SensorFormats.LabelOf`,
  so "unusual format" is fixed in the language of the load; `SplitTabViewModel` compares a
  cell's value with its verdict key, and the Structure tab shows `DepthScaleRows().Band` keys.

## Stage 2 (the window), recorded at the end of the stage

- **Done as planned:** `{l:T En, Ro}` in the XAML (`TExtension`, 111 labels), `SessionMessages`
  and the view-models as `T(en, ro)` pairs, the window's files renamed, `App` reading `--lang`.
- **Deviation:** the language is read in `App`'s constructor, not in `OnStartup` as planned:
  App.xaml's resources (the theme, which carries two texts) load between the two.
- **Added:** `VerdictToWordConverter`, because the pills bound the verdict key both to their
  colour and to their text; the Histogram views got a `Key` beside their label.
- **Test changes, all expected file names:** `_netezit` -> `_smoothed`, `_sintetic` ->
  `_synthetic`, `Rezultat testare.txt` -> `Test result.txt`, `parte-` -> `part-` in five window
  test files. The `NETEZIT` chip stays asserted: the tests run in Romanian.
- **Not re-validated:** stage 2 touched no numeric code (`Metrics.cs`, `Stats.cs`, `Analysis/`,
  `Report/`); stage 1's `/validate` (40/40) stands.
- **Review follow-up (subagent review of fad6a17):** no code defect; the README's tab table and
  the CHANGELOG's Split entry still named `Rezultat split.txt`, now `Split result.txt`; the
  English window test was widened (more Romanian words, and results, guides, an error card
  and the About window rendered in English); a test pins that `_netezit` / `_sintetic` files
  are not recognised; two English wordings aligned with the labels they quote.
- **Found by the widened test:** the theme's two `{l:T}` texts (in templates of `Dark.xaml`)
  were picked once, when the dictionary loaded, so a theme loaded before the language was set
  kept that language. In the application the language is set first; in the tests it is not.
  `TExtension` now returns a one-time binding on dependency properties and setters, so the
  wording is picked when each element is made.
