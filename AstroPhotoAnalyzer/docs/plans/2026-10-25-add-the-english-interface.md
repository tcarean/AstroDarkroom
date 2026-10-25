# Add the English interface, chosen by --lang

## Context

Every text AstroPhotoAnalyzer shows is Romanian today: the report and its recommendations, the
curves' texts, the errors, `--help`, the window's XAML and view-models, about 1,000 texts in ~40
files. The owner wants two interface languages, English and Romanian, chosen by `--lang en|ro`
(`--lang=ro` and `--lang ro`), **English when the flag is missing**, in the console and in the
window alike. The translation is made in context, not text by text. The tests that assert Romanian
keep asserting Romanian; existing tests change as little as possible, and texts may change in them,
figures never.

Owner's answers (2026-10-25): the English goes **beside each Romanian text where it is used**
(not a central table as in IVC's ADR 0038); an unknown `--lang` value is an **error** in the
console (exit 1) and a **message, then English** in the window; `README.md` **stays Romanian
only**, documenting `--lang`.

Branch `claude/Add-the-English-interface`, worktree `.claude/worktrees/Add-the-English-interface`
from `main`; the plan is saved as `docs/plans/2026-10-25-add-the-english-interface.md`.

## Design

**Core, `AstroPhotoAnalyzer.Core/Language.cs`** (new):
- `enum UiLanguage { English, Romanian }`.
- `static class Tr`: `Tr.Pick(string en, string ro)` returns the one in use. `Tr.Current` =
  an `AsyncLocal` override, else the process default (`Tr.Default`, English).
  `Tr.SetDefault(lang)` (the window at start-up, the test assemblies) and
  `using (Tr.Use(lang)) {...}` (one console run, restored at the end, so parallel tests never see
  each other's language).
- `UiLanguageArgs.Find(string[] args, out UiLanguage? lang, out string error)`: `--lang=x` or
  `--lang x`, anywhere, case-insensitive, the last one counting; a missing or unknown value gives
  an English error, `--lang asteapta en sau ro`-style but in English. One parser for both
  applications, in the core.
- Numbers do not change with the language: `Fmt` and the invariant culture stay as they are.

**Pattern for the texts**: every user-visible literal becomes
`Tr.Pick($"<English>", $"<Romanian as today>")`, the Romanian byte-identical to today's.
Pitfalls to clear while doing it:
- a `static readonly`/`const` text (e.g. `DustAnalyzer.BandNames`/`SkyNames`, `RunReport.WarrantyLines`,
  `ThirdParty` entries) would freeze the language at type load: it becomes a property or method;
- any code that compares or keys on a text (a class name, a suggestion's name) keeps an
  identifier apart from its label;
- suggested file names (`CurveTexts.ProposedFileName`, `de_printat_Medie.fit`) and the FITS
  HISTORY cards follow the language too;
- XML doc comments that say "Romanian" are reworded ("in the interface language").

**XAML**: a markup extension `Presentation/TExtension.cs`,
`Text="{t:T En='Open image...', Ro='Deschide imagine...'}"`, which calls `Tr.Pick` once when the
XAML loads (the language is set before the first window). View-models use `Tr.Pick` directly;
the rule "Explanation is not diagnosis" is unchanged (every number still interpolated).

**Console**: `Options.Parse` reads `--lang` first (so a parse error, `--help` and everything
after are already in the chosen language); `Options.Language`. `AnalyzerCli.Run` wraps the run in
`Tr.Use(o.Language ?? English)`; an unknown value prints `ERROR: ...` and exits 1. The
"EROARE: "/"ERROR: " prefix, `--help`, "-> raport salvat" all through `Tr.Pick`. Flag names and
the medium values (`baryta|canvas|acrilic`) are not translated.

**Window**: `App.OnStartup` calls `UiLanguageArgs.Find` on `e.Args`, `Tr.SetDefault`, and
`FileFromArgs` skips `--lang` and its value. An unknown value: the window opens in English and
shows a message (the window's existing message mechanism, checked at implementation).

**Tests, kept Romanian**: each test assembly gets one `[ModuleInitializer]` file setting
`Tr.SetDefault(Romanian)` (CA2255 suppressed with its reason), so the ~330 Romanian assertions
of `AstroPhotoAnalyzer.Tests` and `AstroPhotoAnalyzer.Wpf.Tests` run unchanged. The console tests
are the one exception, since a run without `--lang` is now English, as asked: their `Run`
helpers (`CliTests`, `GoldenOutputTests`, `IccPaperBlackTests`) and the four direct calls
(`ThirdPartyTests`, `VersionTests`, `WcsBorrowTests`) get `--lang ro` added, and nothing else.
**The eight Romanian goldens stay byte-identical**: that is the proof the Romanian side did not
move.

**English terminology (British spelling, as the docs use)**: fundal = background; nivelul /
culoarea fundalului = background level / colour; praf = dust; cer = sky; banda = band; nori
luminosi = bright clouds; nebuloasa (stralucitoare) = (bright) nebula; varfuri = peaks; negrul
hartiei = paper black; negru pur = pure black; pragul de vizibilitate = visibility threshold;
palier = plateau; fiindca = because; OK / ATENTIE / PROBLEMA = OK / ATTENTION / PROBLEM;
EROARE = ERROR; tabs Raport, Tonuri, Culoare, Rezolutie, Curbe, Export, Despre = Report, Tones,
Colour, Resolution, Curves, Export, About; dala = patch.

## Steps (small commits on the branch)

1. **Requirements, ADR, failing tests.** `docs/requirements.md`: R-148 `--lang` syntax (both
   forms, case, last wins), R-149 English by default in both applications, R-150 an unknown or
   missing value (console error exit 1, window message + English), R-151 every text exists in
   both languages (the English goldens and the Romanian-word guard below), R-152 numbers identical
   in both languages. R-003/R-004 reworded for the English default. ADR 0048 (pairs at the use,
   the ambient language with `AsyncLocal`, tests kept Romanian by a module initializer, README
   Romanian only, no culture change); `CLAUDE.md`: the rule "Languages" varied for the English
   and Romanian pairs, `--lang` in Commands. Tests: `LanguageTests` (parser, `Use` scope restore),
   `OptionsTests`/`CliTests` cases for `--lang`.
2. **Infrastructure**: `Language.cs`, the two module initializers, the `--lang ro` additions,
   `Options`/`AnalyzerCli`/`App` wiring. Gate green, Romanian goldens unchanged.
3. **Core texts**, area by area: `Input/` errors (`FitsReader`, `ImageSharpReader`, `ImageLoader`,
   `FitsWriter`, `IccPaperBlack`, `IccProfileInfo`, `Wcs`, `RasterOps`...), `Parameters.cs`,
   `Analysis/` descriptions (`DustAnalyzer`, `PrintStretchSimulator`, `TonalAnalyzer`,
   `BackgroundAnalyzer`, `PrintSizeAnalyzer`, `ToneCurve`...), `Report/` (`ReportWriter`,
   `Recommendations`, `Recommendation`/`VerdictText`, `CurveTexts`, `RunReport`), `ThirdParty.cs`,
   `Runs/`. Each read in full before translating, so a sentence is translated with the ones
   around it and the threshold it names.
4. **Console texts**: `Options.cs`, `AnalyzerCli.cs` (help in English).
5. **Window texts**: `TExtension`, the XAML (`MainWindow`, the views and windows, `Theme/Dark.xaml`
   where it has texts), the view-models (`ResultTabs`, `ParameterFields`, `CurvesTab`,
   `MapViewModel`, `MainViewModel`, ...), `MainWindow.xaml.cs` and the dialogs.
6. **English guards**: an English twin for each of the 8 golden cases (`Golden/*.en.txt`, new
   `R151_...` tests, read in full and reported at wrap-up); a test that the English goldens and the
   window's texts rendered in English (`WindowSmokeTests`, walking the visual tree) contain no
   Romanian word from a short list (fiindca, pentru, si, sau, nu, cu, la, de, din, mai, fara...),
   which catches a text left unpaired; the reverse holds through the unchanged Romanian goldens.
7. **Docs**: new `docs/design/language.md` (Covers `Language.cs`, `TExtension.cs`), updates to
   `console.md`, `window.md`, `extending.md` (a new text is a pair), `testing.md`, the design
   index; `README.md` documents `--lang`; CHANGELOG `Added`.
8. **/wrap-up**: gate, `/validate` (Report/ and Analysis/ change; the drift must be zero), the
   golden diffs reported, commit, merge proposal.

## Verification

- `scripts/verify.ps1` green; `git diff main -- AstroPhotoAnalyzer.Tests/Golden/*.txt` (the
  Romanian ones) empty.
- `/validate` on the reference image: no figure moves (the reference image copied into the
  worktree's `testdata/large/`).
- By hand, on `de_printat.png`: `dotnet run --project AstroPhotoAnalyzer -- <image>` (English),
  `... --lang ro` (identical to today's output), `--lang=xx` (ERROR, exit 1), `--help --lang ro`.
- Window: `APA_DUMP_SCREENS=<dir>` renders in both languages (the smoke test gets an English
  pass), the PNGs looked at; then `dotnet run --project AstroPhotoAnalyzer.Wpf -- <image>` and
  `-- --lang ro <image>` once, by eye.

## Deviations (recorded at wrap-up, 2026-10-25)

- **Requirement IDs**: R-150 to R-154, a new block of ten, not R-148 to R-152 as planned (the
  blocks of ten are areas in `docs/requirements.md`).
- **`help.txt` changed by one line**: the Romanian `--help` gained the `--lang` option it
  documents; the other seven Romanian goldens are byte for byte as on `main`.
- **The English goldens** come from `GoldenOutputTests.CheckBoth`, which runs each case in both
  languages; each test's call changed from `Check(name, RunCli(ws, ...))` to
  `CheckBoth(name, ws, ...)`, text only. `VersionTests`' `Lines` helper also got `--lang ro`,
  beside the helpers the plan named.
- **The Romanian-word guard** lives in `AstroPhotoAnalyzer.Tests/RomanianWords.cs`, linked into
  the window's test project, so the goldens and the window share one list.
- **The window's English pass** is a test of its own, `WindowEnglishTests`, rather than a second
  pass inside `WindowSmokeTests`, which stays unchanged.
- **The XAML** was translated by a subagent with the glossary of the plan, then reviewed here;
  three wordings were changed after the review (the report's file name `Report.txt`, the
  ladder's heading, an apostrophe that broke a markup extension).
- **Region names** in English are "upper-left corner" ... "lower-right corner" and "top edge" ...
  "right edge", so that the longest fits the 18-character column of Appendix E; the Romanian
  ones are unchanged.
- **The window's message** for a wrong `--lang` is the console's sentence followed by
  ". The interface is in English." (R-152 says so).
- **Found while translating**: "1 stele" in the Romanian Rezolutie tab (a plural for one star)
  stays as it was; only the English has "1 star".
- **Review follow-up** (a subagent's code review of the branch, the same day): the first
  progress stage, "citire", was left unpaired in `AnalysisRun.Execute`, so the English window
  showed it while the image loaded. The English window test now also reads every busy message,
  each window's title and a parameter error, and the word list grew by about forty words; the
  test failed on "citire" before the fix. `language.md` and ADR 0048 corrected where they said
  more than the code does (no text compared, a result fixed in one language, the markup
  extension's prefix, the goldens' one changed line).
- **Owner's choices after the review** (2026-10-25): the English verdict label is CAUTION, not
  the glossary's ATTENTION ("2 caution" in `--help`); the edges are "top / bottom / left / right
  edge"; "Denoise is set too strong for print"; "Note from reading the file"; the English
  spelling is -ize throughout the user-visible texts (minimizes, recognized), "analyse" kept.
  The medium name `acrilic` stays as the `--medium` value it is, in both languages.
