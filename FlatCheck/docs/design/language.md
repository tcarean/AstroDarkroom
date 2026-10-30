# The interface language

> Added 2026-10-25 with ADR 0062. **Covers:** `FlatCheck.Core/Language/Ui.cs`,
> `FlatCheck.Core/Language/LanguageArgument.cs`, `FlatCheck.Core/Report/VerdictWords.cs`,
> `FlatCheck.Tests/RomanianByDefault.cs`, `FlatCheck.Wpf.Tests/RomanianByDefault.cs`,
> `FlatCheck.Tests/EnglishOutputTests.cs`, `FlatCheck.Wpf/Presentation/TExtension.cs`,
> `FlatCheck.Wpf/App.xaml.cs`, `FlatCheck.Wpf.Tests/WindowLanguageTests.cs`. A change to the code this describes means this note
> is updated in the same task, or deleted with the decision it explains.

## How the language is chosen

`FlatCheckCli.Run` writes the banner, then hands the whole command line to
`LanguageArgument.Extract`, which takes every `--lang <code>` and `--lang=<code>` out (the last
one counting) and returns the language, the value it did not understand, and the other
arguments in their order. A bad value ends the run there, with `ERROR:` on stderr and exit 1;
otherwise the rest runs inside `Ui.Use(language)` when a `--lang` was given, and in `Ui.Default`
when none was. `Options.Parse` never sees `--lang`, so `flatcheck --lang ro` is an empty command
line and prints the usage, in Romanian.

`Ui.Current` is the language of the innermost open `Ui.Use` scope on the current flow, held in
an `AsyncLocal` (a `Task.Run` inside the scope inherits it), else `Ui.Default`. `Ui.Default` is
`Ui.StartupDefault`, English, until a start-up sets it once: the window in `App`'s
constructor (`App.ApplyCommandLine`, from `Environment.GetCommandLineArgs`), before App.xaml's
resources and the first view load, since both carry texts; each test assembly sets Romanian in
its module initializer, `RomanianByDefault`. The window keeps a value it did not understand and
shows `LanguageArgument.BadValueMessage` in a message box once the main window is up, then goes
on in English. `App.FolderFromArgs` looks for the session folder among the arguments
`LanguageArgument.Extract` leaves, so a `--lang` value is never the folder.

## The window

A fixed label in XAML is `{l:T En='...', Ro='...'}` (`TExtension`, `xmlns:l` the
`FlatCheck.Wpf` namespace); an apostrophe in a wording is escaped `\'`. The wording is picked
when the element is made, not when the XAML is parsed: on a dependency property or in a
`Setter` the extension returns a one-time binding to a `TExtension.Pair`, which each instance of
a template resolves when it is created. A template in the theme is parsed once, when the
dictionary loads; in the tests that happens in Romanian before a test switches to English, and a
string returned there stayed Romanian on the English screens. On a plain property (a binding's
`TargetNullValue`) it returns the string at once. The language never changes while the window
runs, so nothing re-reads it. A
text built in code is a `T(...)` pair, in `SessionMessages` or where the view-model builds it.
A verdict pill binds its colour to the key (`VerdictToBrush`) and its text to the word
(`VerdictToWord`); `VerdictChip` does the same for the rows' chips and the scale tables. Three
places compared a text and now compare a key or a flag: the Histogram tab's views carry a
`Key` beside their label (`"Mozaic"` for `HistEqReport.ViewCaption`), the Synthetic tab keeps
`_cropUnknown` instead of matching "crop necunoscut", and the Split table compares a cell's
value with `VerdictWords.Of(cell.Verdict)`. `LensChoice.Calibration` is built when the catalog
loads, in the language of the run, which is fixed by then.

## How a text is written

`Ui.T(english, romanian)` returns one of the two; files call it as `T(...)` through
`using static FlatCheck.Ui;`. An interpolated text is written twice with the same holes and the
same format strings, so a number reads the same in both; no `CultureInfo` is set for the
language (the console and Core run under `InvariantGlobalization`, the window forces the
invariant culture, ADR 0043). Three shapes:

- a literal or a sentence: `T("...", "...")` where it was;
- a text whose columns are aligned across many lines: written whole per language, chosen by
  `Ui.Current == UiLanguage.Romanian` (the usage, `WriteUsageEnglish` / `WriteUsageRomanian`;
  two explanatory paragraphs of the structure and series reports);
- a list once built in a `static readonly` field (the warranty lines, `ThirdParty.Components`,
  `FocalOnlyLines`): now a property, so it is built in the language of the moment it is read.

A `const string` that held a text became a property for the same reason; a `const` that is a
key stays a `const`.

## Keys that are words

The analysis hands out verdicts as the Romanian words they were (`OK`, `SUSPECT`, `PROBLEMA`,
`PERFECT`, `BUN`, `ACCEPTABIL`, `REFA`, `NEDETERMINAT`, `INAPLICABIL`); `FlatCheckCli.ExitFor`,
`Stats.Severity`, `StructureReport.Advice`, the window's `VerdictPalette` and the scale tables
compare those keys. Only printing translates them: every place that writes a verdict calls
`VerdictWords.Of(key)`. `SplitCell` keeps the key in `Verdict` and the word in `Value` on the
verdict row; `SplitReport.Cell` compares the value with the word. The same split for the corner
names of `StructureResult.Corners` (`StructureReport.CornerName`; the diagnostic log writes the
English one, `StructureReport.EnglishCorner`, whatever the language) and for the views of
`HistEqReport.ViewCaption`, whose `"Mozaic"` / `"mono"` / `"RGB"` are keys the window passes.
The series' reasons `"sem"` and `"corelatie"` are keys `LightSeriesReport.ReasonLines` words;
the third reason is free text, written in the language of the run that produced it.

## Glossary

The English follows Siril's and the usual astrophotography vocabulary; a term is translated the
same way everywhere.

| Romanian | English |
| --- | --- |
| master, flat, light, raw, stack | the same |
| vignetare | vignetting |
| reziduu (dupa model radial) | residual (radial model) |
| imprastiere colturi | corner spread |
| inclinare | tilt |
| pata maxima (o dala) | worst spot (one tile) |
| pete locale | local spots |
| harta | map |
| stopuri | stops |
| colt / centru | corner / centre |
| netezire / netezit | smoothing / smoothed |
| flat sintetic | synthetic flat |
| obiectiv / profil | lens / profile |
| focala / diafragma | focal length / aperture |
| cadru / serie | frame / series |
| componenta fixa pe aparat / in lume | part fixed to the camera / in the world |
| ATENTIE / EROARE | WARNING / ERROR |
| REFA / BUN / ACCEPTABIL / PROBLEMA | REDO / GOOD / ACCEPTABLE / PROBLEM |
| NEDETERMINAT / INAPLICABIL | UNDETERMINED / NOT APPLICABLE |

English is British in spelling (`centre`, `colour`, `normalised`), as the code's comments are.

## Tests

The Romanian goldens and every test written before run in Romanian through the module
initializer and need no `--lang`. `EnglishOutputTests` runs the 31 main golden scenarios with
`--lang en` and pins each as `Golden/en_<name>.txt`, then runs it with `--lang ro` and asserts
the same exit code and the same numbers (every digit run, sorted, compared as a multiset), and
scans the English output, outside the workspace paths, for Romanian words. It runs the 39 parity
scenarios in English against their committed Romanian goldens for the numbers too. A new main
golden scenario is added to `EnglishOutputTests.Arguments` as well. `LanguageTests` pins the
parsing, the scope and the English error of a bad value. `WindowLanguageTests` pins the
window's start-up (`App.ApplyCommandLine`, `App.FolderFromArgs`), builds every tab with real
results and its guides open, plus the problem screen, in English, and collects every text of
the visual tree (a window's through its content: an unshown window builds no tree of its own);
none may hold a Romanian word, and the same main window in Romanian must still show
`Structura`. With `FLATCHECK_DUMP_SCREENS` it saves the English screens as `en-*.png`.
