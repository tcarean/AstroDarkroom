# The interface language

> Written on 2026-10-25 with the English interface (ADR 0048, which says why it is built this
> way). The behaviour is stated with IDs in `../requirements.md` (R-150 to R-154). **Covers:**
> `AstroPhotoAnalyzer.Core/Language.cs`, `AstroPhotoAnalyzer.Wpf/Presentation/TExtension.cs`,
> `AstroPhotoAnalyzer.Tests/RunsInRomanian.cs`, `AstroPhotoAnalyzer.Wpf.Tests/RunsInRomanian.cs`,
> `AstroPhotoAnalyzer.Tests/RomanianWords.cs`, `AstroPhotoAnalyzer.Tests/LanguageTests.cs`,
> `AstroPhotoAnalyzer.Wpf.Tests/WindowEnglishTests.cs` and
> `AstroPhotoAnalyzer.Wpf.Tests/AppLanguageTests.cs`. A change to the code this describes means
> this note is updated in the same task, or deleted with the decision it explains.

## How a text picks its language

`Tr.Pick(english, romanian)` returns one of its two arguments by `Tr.Current`. `Tr.Current` is
the value of an `AsyncLocal` when a `Tr.Use` scope is open on the current flow of execution,
otherwise the process default (`Tr.Default`, English until `Tr.SetDefault` changes it). Both
arguments are built before the call, interpolations included; the cost is nothing beside an
analysis, and it keeps each text one expression.

The language is read when the text is built, so a text must be built when it is shown:

- A text that was a `const` or a `static readonly` field is now a property (`=>`), for example
  `ReportWriter.SimulationHeader`, `RunReport.WarrantyLines`, `ThirdParty.Components`,
  `CurveTexts.Computing`, `CurveAdvisor.Names`, `DustAnalyzer`'s class names,
  `TonesTabViewModel.NoWcsSentence`. A field initialised once would keep the language of the
  first reader.
- A result is not fixed in one language. Its stored texts (the report body, a crop's `Axis`, a
  region's name) keep the language they were built in, while others are built each time they
  are read (`DustMap.ClassNames`, the simulation's `DustDescription`, `SkyDescription`,
  `MethodNote` and `ClassFractions`), and `ReportWriter.CropCutText` puts the stored axis into a
  sentence picked when it is called. A result read in another language than it was built in
  would mix the two; neither application can do that, since the language is fixed for the run.
- Code compares a text in one place only: `VerdictPalette.For` matches a verdict's label
  against `VerdictText.Label` in the language in use, so a label built in one language and
  coloured in the other would turn neutral grey; again impossible within one run. Where code
  used to compare a text that is now translated (the crop's axis, `"latime"`), a flag stands
  beside the label (`CropResult.CutsWidth`).

The XAML texts go through `{local:T En='...', Ro='...'}` (`TExtension`), which calls `Tr.Pick`
when the XAML is loaded. A markup extension in a `DataTemplate` (the "because:" of
`Theme/Dark.xaml`) is evaluated each time the template is instantiated, so it too follows the
language in use, although the dictionary itself is loaded once. Inside a single-quoted value an
apostrophe would end the value: the English avoids it, or writes `\'`.

## Who sets the language

- **The console**: `AnalyzerCli.Run` calls `UiLanguageArgs.Find` before anything else and runs
  the rest inside `Tr.Use(language ?? English)`. `Options.Parse` strips `--lang` and its value
  (`UiLanguageArgs.Strip`) and parses the rest as before, so `--lang=ro` alone is the help.
  A wrong `--lang` is reported after the banner, in English, because the scope is English when
  no language was found.
- **The window**: `App.OnStartup` calls `App.LanguageFromArgs`, sets the process default, then
  builds the main window; a wrong value is shown in a message box after the window is shown.
  `App.FileFromArgs` looks for the image among the arguments without `--lang` and its value.
  The analysis runs on a pool thread and reads the process default, which is why the window
  sets the default rather than a scope.

## Tables whose columns are aligned

The report's tables (the simulation, the clouds, the JPEG table, the size table) and the
window's mono tables (the level - L* ladder, the dpi ladder) print right-aligned figures under a
header. The row formats are shared by both languages; each English column title is written so
that it ends in the same column as the Romanian one. A title longer than its column starts
earlier and eats into the spaces before it ("pure black" over a nine-wide column has two spaces
before it instead of three).

## The tests

- **Romanian by default.** Each test assembly sets the process default to Romanian in a
  `[ModuleInitializer]` (`RunsInRomanian.cs`); every test written before ADR 0048 asserts
  Romanian and runs unchanged. A console run sets its own scope, so the console tests that
  assert Romanian pass `--lang ro`: the `Run` helpers of `CliTests`, `IccPaperBlackTests`,
  `GoldenOutputTests` and `VersionTests`, and the calls in `ThirdPartyTests` and
  `WcsBorrowTests`.
- **English where it is the subject.** `LanguageTests` (the parser, the scope, the console's
  English default, the error, the numbers in both languages), `AppLanguageTests` (the window's
  start-up), the English goldens (`GoldenOutputTests.CheckBoth` writes `name.txt` with
  `--lang ro` and `name.en.txt` without), and `WindowEnglishTests`, which builds every tab and
  window of a real result inside `Tr.Use(English)` on the STA thread and collects the text of
  every `TextBlock`, content, header and tooltip, each window's title (after the dispatcher has
  run its queue, which is when a bound title resolves), every busy message the analysis reports,
  and a parameter field's error.
- **The guard.** `RomanianWords.In` lists the words of a text that are on a fixed list of
  Romanian words (function words and the domain's nouns), after scrubbing file paths, since the
  fixtures' names are Romanian. The English goldens and the window's texts must give none. A new
  text left Romanian shows there; a Romanian word that is also English must stay off the list.
