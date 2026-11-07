# The session folder, the inventory and the console

**Covers:** `src/DarksCheck.Core/Session/SessionFolder.cs`, `src/DarksCheck.Core/Session/FrameInfo.cs`, `src/DarksCheck.Core/Analysis/CadenceAnalyzer.cs`, `src/DarksCheck.Core/Analysis/MatchAnalyzer.cs`, `src/DarksCheck.Core/Thresholds.cs`, `src/DarksCheck.Core/Runs/InventoryRun.cs`, `src/DarksCheck.Core/Report/InventoryReport.cs`, `src/DarksCheck.Core/Report/RunReport.cs`, `src/DarksCheck.Core/Version.cs`, `src/DarksCheck.Cli/Options.cs`, `src/DarksCheck.Cli/Program.cs`, `src/DarksCheck.Core/Language/Ui.cs`, `src/DarksCheck.Core/Language/LanguageArgument.cs`.

The behaviour is R-001 to R-007; the shapes of the input are ADR 0002. This note says how the
pieces fit.

## The folder

`SessionFolder.Open` decides the layout once. `darks/` present means a session: its frames
are the darks, `biases/` and `lights/` are read when present and non-empty, and an empty
`darks/` is an error rather than a fallback, because a session root holds `result.fit` and
the other stacked FITS, which would otherwise pass for darks. `darks/` absent but `lights/`,
`biases/` or `flats/` present is a session without darks, refused for the same reason. Neither
present means a bare folder of darks, a library: the frames are at the root and no biases or
lights are looked for, even if such subfolders existed somewhere else. `FramesIn` lists one
folder: raws when there are any, else FITS, sorted by file name ignoring case.

`Open(path, allowLightsOnly)` has one more shape since ADR 0011: with the flag, a folder whose
`lights\` holds frames and that has no `darks\` opens with an empty `Darks` (`HasDarks` false,
`DarksDir` the absent `darks\` path), its biases and lights read as usual; without the flag,
or with an empty `lights\`, it is the error it always was. The inventory opens every folder
with the flag; the partner of `--against` without it (a partner must have darks).
`Inventory.DarksFromPartner` is null unless the run filled the darks from a partner, in which
case `InventoryReport.HeaderRows` prints `(lipsa; se folosesc dark-urile partenerului ...)`,
`expunere partener`, `interval darks: (lipsa)` and compares the lights with the partner's
darks in `potrivire`; a lights-only inventory without a partner prints `(lipsa)` three times
and `NEMASURAT: nu exista darks`. The sentence that refuses a lights-only run without a
partner is `RunReport.LightsOnlyNeedsPartner`, the same in the console and the window.

## The metadata

`FrameInfo.Read` opens a file through `ImageLoader.LoadInfo` (no pixels: tens of milliseconds
per raw) and never throws; the error becomes the record's `Error`, so one unreadable frame is
listed among the others and only the exit code says it (R-003). The FITS reader's current copy
answers a file that does not start with `SIMPLE` with an empty header, hence the explicit
check in `Read`; it goes when the copy is refreshed from LightsCheck (ADR 0003).

## The cadence

The constants of this section and the next (`Th.PauseSeconds`, `Th.ExposureTolerance`,
`Th.BiasMaxExposureSeconds`) are set from the two measured sessions (ADR 0010).

`CadenceAnalyzer.Analyze` walks the frames in list order and keeps a pause per position, so a
filtered list works too. A pause below zero is clamped to 0: the EXIF time has a 1 s
granularity and the camera records 120.2 s for a 120 s setting, so back-to-back frames
started 120 s apart would otherwise read -0.2 s (printed `-0 s`, seen on the library of
2026-10-10). A frame without a time or an exposure breaks the chain: it gets no
pause, and neither does the next one, and no interval is counted across it. The median
interval is over consecutive timed starts; a long pause is one over `Th.PauseSeconds`,
recorded with the index of the frame it precedes. The darks and the lights each get a
`Cadence`; the biases do not, their bursts say nothing.

## The match (check 1)

`MatchAnalyzer.Settings` clusters the readable frames of a group by ISO and by exposure, two
exposures within `Th.ExposureTolerance` of the longer joining one cluster, and returns the
largest cluster as the group's settings, named by its most frequent exact exposure (so the
odd 118.5 s frame never names a 119.2 s group, whatever its position), with whether it holds
every readable frame. The two
verdicts compare clusters: darks against lights on exposure (same tolerance) and ISO, and the
bias against `Th.BiasMaxExposureSeconds`. `Verdict.Unmeasured` stands for a missing group or
missing exposures; the report, not the analyzer, turns it into words.

## The report and the console

`InventoryReport` prints the session lines in a 19-column label field, then one table per
non-empty group; the exposure is printed with one decimal from 1 s up and up to four below,
so a 1/20 s bias reads `0.05` and a 1/4000 s one `0.0003` (0.00025 rounded). `RunReport` holds the
banner, the copyright, the warranty lines of `--help` and the shared formatters (`F`,
`Duration`, `Frames`); `VersionInfo` reads the version and the copyright from the assembly
attributes `Directory.Build.props` sets. `Program.Run(args, stdout, stderr)` is the console
behind `Main`: banner first, then `--lang` ("The interface language" below), then the options, then one run. `Options.Parse(args)` walks the
arguments once and returns an `Options` with `Folder`, `Inventory`, `Out`, `Threads`,
`MaxFrames`, `Against`, `Help` and `Error`: the first word that is not an option is the
folder, a second one is an error, an option that takes a value reads the next argument
(`<option> expects a value` when there is none) and parses it with the invariant culture
(`--threads` and `--max-frames` as positive integers, `--out` and `--against` as non-blank
paths; no option takes a decimal today), the first problem fills `Error` and parsing stops;
after the loop two more checks fill it: no folder given (`the session folder or the folder of darks
is missing`) and `--inventory` with `--out` or `--against`. `Program.Run` prints `Error` as
`ERROR: <message>` (`EROARE:` in Romanian) on stderr and exits 1, prints the usage for `Help` (the usage text is
`Program.WriteUsage`, written whole once per language since its columns are aligned by hand,
with `RunReport.WarrantyLines`), and otherwise builds
`DarkOptions(Threads, MaxFrames, Against)` with the two normalisation settings at their
defaults (`dark-analysis.md`, "The records of the run"). Without options it is the darks analysis of
`DarkSeriesRun` (`docs/design/dark-analysis.md`), with the progress written on stderr as one
rewritten line by a synchronous `IProgress` (a `Progress<T>` would post to the thread pool and
could print after the report) and wiped before the report or an error; `--inventory` is the
metadata-only run; `--out`, `--threads`, `--max-frames` and `--against` are passed through
(neither `--out` nor `--against` goes with `--inventory`); before the run the partner folder is opened and
`--out` is checked against the session's root and the partner's, so a wrong folder costs no
decoding. The exceptions
a bad folder or a frame can raise, the `AggregateException` of a parallel loop included, are
turned into `ERROR:` on stderr and exit 1. `Main` runs `Program.Run` between the diagnostic log's
first and last lines (`LogSetup.Apply` and `LogSetup.Close`, each guarded, so a log that cannot be
set up never stops the console); `Run`, which the tests call in-process, configures nothing. Its lines (the
mode and the options, a refusal as the user saw it, every file `--out` writes, a run that failed
with its exception before the one `ERROR:` line), and those of `SessionFolder.Open`,
`FrameInfo.Read` and `InventoryRun`, are in `logging.md` (ADR 0025).

## The interface language

Every text the user sees is a pair, `T(english, romanian)` (`Ui.T`, imported with
`using static DarksCheck.Core.Ui`), written where the text is composed (ADR 0021). `Ui.T` reads
`Ui.Current`: the innermost scope opened by `Ui.Use(language)` on the current flow, an
`AsyncLocal` that the tasks and `Parallel.For` workers of a run inherit, or `Ui.Default` outside
any scope, English unless a start-up sets it. A text that used to be a `const` or a
`static readonly` field is a property, so it is picked when it is shown; a type initialiser
would freeze the first language. No culture follows the language: every number is formatted
with the invariant culture in both. A Romanian wording that counts writes its figure through
`Ui.RoCount` (R-095, ADR 0029), which adds the `de` Romanian puts after a whole count of 20 and
up: `$"{RoCount(n)}cadre"`. `RunReport.Frames` and `DarkReport.Pixels` call it; the overload on a
string takes a figure already formatted, such as `F(x, 0)`, and leaves one with decimals alone.
English wordings never call it.

`LanguageArgument.Extract(args)` takes every `--lang <code>` and `--lang=<code>` out of the
arguments, the name and the code case-insensitive, the last one counting; in the spaced form
the next argument is the value whatever it is. It returns the language (null when none was
given or the value names none), whether one was given, the value it did not understand and the
other arguments. `Program.Run` writes the banner, extracts `--lang`, stops on a bad value with
the English `BadValueMessage` and exit 1, and runs the rest of the console in
`Ui.Use(language)` when one was given, else under `Ui.Default`. The window reads the same
record before its first view (`docs/design/window.md`).

The verdict words are the run's language's: `OK`, `WARNING`, `REDO`, `UNMEASURED` in English,
`OK`, `ATENTIE`, `REFA`, `NEMASURAT` in Romanian; `ReportRow.VerdictOf` and the window's
`VerdictPalette` read either. The test assemblies set `Ui.Default` to Romanian in a module initializer
(`RomanianByDefault.cs`), so the tests written before the English stand as they were; the
English is pinned by `EnglishGoldenOutputTests` and `LanguageTests`.
