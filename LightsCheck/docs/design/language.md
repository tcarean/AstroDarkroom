# The interface language

**Covers:** `src/LightsCheck.Core/Language/Ui.cs`, `src/LightsCheck.Core/Language/LanguageArgument.cs`, `src/LightsCheck.Wpf/Presentation/TExtension.cs`, `src/LightsCheck.Wpf/Presentation/VerdictPalette.cs`, `src/LightsCheck.Wpf/App.xaml.cs`, `tests/LightsCheck.Tests/RomanianByDefault.cs`, `tests/LightsCheck.Tests/LanguageTests.cs`, `tests/LightsCheck.Tests/EnglishGoldenOutputTests.cs`, `tests/LightsCheck.Tests/PlainWords.cs`, `tests/LightsCheck.Wpf.Tests/WindowLanguageTests.cs`.

The behaviour is R-060 to R-064, the reasons ADR 0022 (the design is FlatCheck's, its ADR
0062). This note is how the code does it.

## How the language is chosen

`Program.Run` writes the banner, then hands the whole command line to
`LanguageArgument.Extract`, which takes every `--lang <code>` and `--lang=<code>` out (the last
one counting; the spaced form takes the next argument whatever it is) and returns the language,
the value it did not understand (`BadValue`, an empty string for a `--lang` with nothing after
it) and the other arguments in their order. A bad value ends the run there, with
`ERROR: ` + `LanguageArgument.BadValueMessage` on stderr and exit 1, in English whatever the
default. Otherwise the rest runs in `RunIn`, inside `Ui.Use(language)` when a `--lang` was given
and in `Ui.Default` when none was. `Options.Parse` never sees `--lang`, so `lightscheck --lang ro`
is an empty command line and prints the usage, in Romanian.

`Ui.Current` is the language of the innermost open `Ui.Use` scope on the current flow, held in
an `AsyncLocal` (the series run's `Parallel` work inherits it), else `Ui.Default`. `Ui.Default`
is `Ui.StartupDefault`, English, until a start-up sets it: the window in `App`'s constructor,
the test assemblies to Romanian in `RomanianByDefault`'s module initializer (linked into the
window's test project too). `App`'s constructor reads `Environment.GetCommandLineArgs()` and
first starts the diagnostic log with them, which needs no language, its words being no text of
the interface (`logging.md`); then `App.ApplyCommandLine`, before App.xaml's resources and the
first view load, since both carry texts, sets `Ui.Default`
to the language named or to English, and keeps the other arguments and the bad value;
`OnStartup` shows the bad value's message in a box once the main window is up and looks for the
session folder (`FolderFromArgs`) among the other arguments only, so a `--lang` value is never
the folder.

## How a text is written

`Ui.T(english, romanian)` returns one of the two; files call it as `T(...)` through
`using static LightsCheck.Core.Ui;`. An interpolated text is written twice with the same holes
and the same format strings; where a sentence holds many figures, they are formatted once into
locals and both wordings use the locals, so the two cannot differ in a number. Three shapes:

- a literal or a sentence: `T("...", "...")` where it was;
- a text aligned by hand across many lines: written whole per language, chosen by
  `Ui.IsRomanian` (the usage, `WriteUsageEnglish` / `WriteUsageRomanian`); a table header is
  built in English with the same `{x,n}` widths as its rows, the Romanian literal kept as it was;
- a list once built in a `static readonly` field (`RunReport.WarrantyLines`,
  `ThirdParty.Components`, `SeriesReport.RejectedExplanation`, `SeriesReport.DitherExplanation`),
  or a `const` that held a text (`SeriesReport.ScaleLabel`): now a property, built in the language
  of the moment it is read. A `const` that is a key or a file name stays a `const`.

A label that another text quotes (the `drift over one exposure`, `pattern in the stack` and
`fixed pattern` rows the Dithering explanation names; the DITHERING rows that are also table
titles) is a private property of `SeriesReport`, used in both places. The session card of the
window splits the header lines on `"    "` and `" : "`, so the English labels keep that shape.

A few texts are made when the run is, not when the report is printed: the RA axis's source
(`SeriesAnalyzer`), the pixel size's source (`SeriesRun`), the ADU scale's source (`FrameRun`),
the star cut-outs' position words (`StarCrops`) and every error message (the readers,
`SessionFolder`, `FrameInfo`). They are worded in the language current on the flow that makes
them, which is the run's; a result reported in the other language keeps them in the language it
was analysed in. Neither application does that (the language is fixed for the process), only a
test that builds both languages' rows from one result.

## The window

A fixed label in XAML is `{local:T En='...', Ro='...'}`; an apostrophe or a double quote inside
a wording is escaped with a backslash (`\'`, `\&quot;`). On a dependency property or in a
`Setter` the extension returns a one-time binding to a `TExtension.Pair`, so the wording is
picked when each element is made, not when the XAML is parsed: a template in the theme is
parsed once, when the dictionary loads, which in the tests happens before a test switches to
English. On a plain property it returns the string at once. The language never changes while
the window runs, so nothing re-reads it. A text built in code is a `T(...)` pair in
`WindowTexts`.

The chips are coloured by their word: `VerdictPalette.Severity` lists each severity's words in
both languages (exact words, then starts: `FARA` / `NO ` and `OK` good, `LA LIMITA` / `MARGINAL`,
`POSIBIL` / `POSSIBLE`, `ATENTIE` / `WARNING` attention, `SLAB` / `BAD`, `INSUFICIENT` /
`INSUFFICIENT`, `EROARE` / `ERROR` bad, the undetermined ones neutral, `NOT MEASURED` tested
before `NO `).

## Glossary

The English follows Siril's and the usual astrophotography vocabulary, British in spelling
(`centre`, `colour`, `analysed`); a term is translated the same way everywhere. The words are
plain ones (the rule "Plain words", R-077 to R-081): every tab of the window, the reasons wherever
they are printed, and the console's sections, session lines and REJECTED line they share use the
plain terms below.

| Romanian | English |
| --- | --- |
| cadru / cadre | frame / frames |
| light, bias, dark, flat, stack, dithering, walking noise, FWHM, halo | the same |
| alungire / cat de alungite sunt stelele | elongation / how elongated the stars are |
| dara (stea) / dare de walking noise | trail / walking-noise streaks |
| roua | dew |
| fond (de cer) | (sky) background |
| stele gasite | stars found |
| zgomot de citire / shot/citire / zgomot in plus | read noise / shot/read / extra noise |
| latimea stelelor (FWHM) / lumina din jurul stelelor (halo) | the stars' width (FWHM) / the light around the stars (halo) |
| expunere totala / la fotografiere / montura | total exposure / when shot / mount |
| axa monturii e deviata de la pol cu / asta muta stelele cu | the mount's axis is off the pole by / this moves the stars by |
| miscare / pas | move / step |
| apropierea cadrelor / locuri diferite / zona pozitiilor | how close the frames are / different spots / area of the positions |
| grup (de cadre la rand) / acelasi loc | run (of frames in a row) / same spot |
| cadre singure | frames alone |
| in medie (a median: the move, the stars' width and count) / tarie (of the fixed pattern) | on average / strength |
| aliniere polara / rotatie de camp | polar alignment / field rotation |
| pattern fix / pixeli calzi | fixed pattern / hot pixels |
| aliniat (DITHERING), nu s-a putut alinia (the reasons); inregistrat (elsewhere, until its rewrite) | aligned, could not be aligned; registered |
| scara | scale |
| decupaje | cut-outs |
| respinse / de scos din stiva | rejected / leave out of stack |
| cele la limita raman in stiva, la alegerea ta | the marginal ones stay in the stack, your choice |
| eroarea periodica / jocul din angrenaje (backlash) | periodic error / the play in the gears (backlash) |
| de la 1.60 cadrul e slab / de la 1.35 cadrul e la limita | from 1.60 the frame is bad / from 1.35 the frame is marginal |
| stele late / prea putine stele / latimea medie din sesiune | wide stars / too few stars / the session's average width |
| fondul cerului sare cu ... fata de cadrele vecine (se semnaleaza de la ...) | the sky background jumps by ... against the neighbouring frames (flagged from ...) |
| dara / dare de satelit sau avion (Siril o / le scoate la stivuire) | satellite or plane trail / trails (Siril takes it / them out when stacking) |
| cadrul dinainte / cadrul de referinta / nivelul de negru | the frame before / the reference frame / the black level |
| REZUMAT, EXPUNERE SI ZGOMOT, ROUA, CADRE MISCATE, DRIFT SI ROTATIE | SUMMARY, EXPOSURE AND NOISE, DEW, TRAILED FRAMES, DRIFT AND ROTATION |
| bun / la limita / SLAB / necitit (clasa) | good / marginal / BAD / unread |
| BUN(A), LA LIMITA, NU, DA, POSIBIL, ATENTIE | GOOD, MARGINAL, NO, YES, POSSIBLE, WARNING |
| NEDETERMINAT, NEMASURAT, INSUFICIENT, FARA walking noise | UNDETERMINED, NOT MEASURED, INSUFFICIENT, NO walking noise |
| EROARE | ERROR |
| fisiere raw de aparat | camera raw files |
| runtime-ul Microsoft Visual C++ (lipseste sau este prea vechi) | Microsoft's Visual C++ runtime (missing or too old) |
| E / V (sensul pe RA) | E / W |
| stanga / dreapta / sus / jos (sageti) | left / right / up / down |

A Romanian count of 20 and up takes `de` before the word it counts, unless its last two digits
are 01 to 19 (R-082, ADR 0037): `Ui.RoCount` writes the figure and a space, with `de ` when the
count takes one, for a count or a whole figure as the text shows it (`-90 de grade`). Every
Romanian count goes through it, directly or through `SeriesReport.Count`, `FramesWord`,
`TimesWord`, `PairsWord` and `SharingPairsWords`; a figure with decimals and the units (`px`,
`s`, `min`, `h`, `%`, `ADU`) never do. The English halves write the count as before.

## Tests

The Romanian goldens and every test written before run in Romanian through the module
initializer and need no `--lang`. `EnglishGoldenOutputTests` runs the five golden scenarios with
`--lang en`, pins each as `Golden/en_<name>.txt` (scrubbed as the Romanian ones, the progress
line replaced by `(progress, left out)`), runs it again with `--lang ro` and asserts the same exit
code and the same numbers (every digit run, sorted, compared as a multiset), and scans the
English output for Romanian words; it also checks that `--lang ro` prints two Romanian goldens
unchanged. A new golden scenario is added to `EnglishGoldenOutputTests.Scenarios` too. `LanguageTests`
pins the parsing, the usage and a parse error in each language, a core error in English, the
English error of a bad value, the arrow options' English forms, and the default: it starts the
real `lightscheck.exe` beside the tests, whose `Ui.Default` no test touched, and expects
`Usage:`. `WindowLanguageTests` pins `App.ApplyCommandLine` with `FolderFromArgs`, renders every
tab with a real result, the problem card, the empty window and the About window in English and
collects every text of the visual tree plus the charts' own labels, none of which may hold a
Romanian word; checks that the same window in Romanian shows `Rezumat`; and checks that every
verdict and class word the core writes for a result colours the same in both languages. It
also renders the Dithering, Summary, Frames, Quality and Rejected tabs in each language, the
Frames tab with a frame's cut-outs, and holds their texts, the tooltips and the charts' labels
included, and the top bar's four input tips with the defaults they name (R-083), against the
method's words of `PlainWords` (R-077 to R-081, R-083), the list `SeriesTests` holds the core's
rows, tables, paragraphs, reasons and cut-out lines against, linked from `LightsCheck.Tests`.
It measures every reason a trailed frame can get, in both languages, in the cell's font and the
window's whole-pixel text, against the Frames table's reasons cell as the tab lays it out (R-079).
With `LIGHTSCHECK_DUMP_SCREENS` it saves the English screens as `en-*.png`.
`R082_ARomanianCountOfTwentyAndUpTakesDe` (`SeriesTests`) pins `Ui.RoCount`'s edges and scans the
Romanian report of a 24-frame session for a count of 20 and up without `de`.
