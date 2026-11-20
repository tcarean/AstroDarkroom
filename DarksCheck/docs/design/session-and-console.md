# The session folder, the inventory and the console

**Covers:** `src/DarksCheck.Core/Session/SessionFolder.cs`, `src/DarksCheck.Core/Session/FolderIdentity.cs`, `src/DarksCheck.Core/Session/FrameInfo.cs`, `src/DarksCheck.Core/Analysis/CadenceAnalyzer.cs`, `src/DarksCheck.Core/Analysis/MatchAnalyzer.cs`, `src/DarksCheck.Core/Thresholds.cs`, `src/DarksCheck.Core/Runs/InventoryRun.cs`, `src/DarksCheck.Core/Report/InventoryReport.cs`, `src/DarksCheck.Core/Report/RunReport.cs`, `src/DarksCheck.Core/Version.cs`, `src/DarksCheck.Cli/Options.cs`, `src/DarksCheck.Cli/Program.cs`, `src/DarksCheck.Core/Language/Ui.cs`, `src/DarksCheck.Core/Language/LanguageArgument.cs`.

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
folder: raws when there are any, else FITS, sorted by file name ignoring case. It drops, before
the extension is looked at, every file whose name starts with a dot and every hidden one: the
`._name` copies a Mac leaves on a card sort before the frames, so they would fail to decode
first and, over the frame cap, be picked in the darks' place. The listing goes through
`DirectoryInfo`, which brings each file's attributes with its name.

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

`Open(path, allowLightsOnly, lightsFrom)` takes the lights from another folder since ADR 0039
(R-055). `ChosenLights` resolves `lightsFrom` first, before the darks are even listed, so a bad
folder is refused at once and once: the full path must exist (`DirectoryNotFoundException`),
its `lights\` is taken when that holds frames, else the folder itself, which must hold frames
and have no session subfolder (`lights`, `darks`, `biases`, `flats`): a session's root holds
its stacked results, so a session whose `lights\` gives no frame is refused with that folder
named, and its root is never listed (ADR 0067)
(`InvalidOperationException`); both messages open with `lights-urile alese: ` / `the chosen
lights: `, as the partner's open with `partenerul: `. The result replaces `LightsDir` and
`Lights` whatever the folder's own `lights\` holds, and `ChosenLightsRoot` keeps the folder as
given, made full (`LightsChosen` true), for the `lights` row, which prints the full path and
`alese de tine` (`InventoryReport.ChosenLightsLine`), and for `DarkOutputs.WriteAll`, which
refuses `--out` inside it as it does inside the session and the partner
(`DarkOutputs.OutFolder`). A folder without darks and with the flag is lights-only whether its
own `lights\` holds frames or the lights were chosen. `InventoryRun.Run(folder, lightsFrom)` and
`DarkOptions.LightsFrom` carry it; the log's `opened` line adds `chosenLights=`.

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

The list is in the order of the file names, and the cadence, check 4 and the first and last
light of check 2 read it as the order the frames were taken in. `FirstBackwards` finds the
first place where the two part: the first frame with a time that was taken before the frame
with a time listed before it (frames without a time between them are passed over, equal times
are no disagreement). `Analyze` keeps the pair in `Cadence.Backwards`, and
`InventoryReport.HeaderRows` writes a `WARNING` row under the group's `interval` row from it
(`OutOfOrder` gives the two file names). For a session without darks the darks analysed are
the partner's, whose metadata the run keeps in `Inventory.PartnerDarks` and which have no
`Cadence` of their own: the row is then read by calling `FirstBackwards` on them. Nothing is
reordered (ADR 0063).

## The match (check 1)

`MatchAnalyzer.Settings` clusters the readable frames of a group by ISO and by exposure, two
exposures within `Th.ExposureTolerance` of the longer joining one cluster, and returns the
largest cluster as the group's settings, named by its most frequent exact exposure (so the
odd 118.5 s frame never names a 119.2 s group, whatever its position), with whether it holds
every readable frame. The two
verdicts compare clusters: darks against lights on exposure (same tolerance) and ISO, and the
bias against `Th.BiasMaxExposureSeconds`. For the darks against the lights each setting is
compared on its own when both clusters give it: one that differs makes the verdict
`Warning` whatever the other says, both given and equal make it `Ok`, and `Unmeasured` is
what is left, a setting not given on a side with nothing known to differ (ADR 0058).
`Verdict.Unmeasured` also stands for a missing group; the report, not the analyzer, turns
it into words, and `InventoryReport.DarksVsLights` names only the setting that differs when
the other is not given on both sides.

## The report and the console

`InventoryReport` prints the session lines in a 19-column label field, then one table per
non-empty group; the exposure is printed with one decimal from 1 s up and up to four below,
so a 1/20 s bias reads `0.05` and a 1/4000 s one `0.0003` (0.00025 rounded). `RunReport` holds the
banner, the copyright, the warranty lines of `--help` and the shared formatters (`F`,
`Duration`, `Frames`); `VersionInfo` reads the version and the copyright from the assembly
attributes `Directory.Build.props` sets. `Program.Run(args, stdout, stderr)` is the console
behind `Main`: banner first, then `--lang` ("The interface language" below), then the options, then one run. `Options.Parse(args)` walks the
arguments once and returns an `Options` with `Folder`, `Inventory`, `Out`, `Threads`,
`MaxFrames`, `Against`, `Lights`, `Help` and `Error`: the first word that is not an option is the
folder, a second one is an error, an option that takes a value reads the next argument
(`<option> expects a value` when there is none) and parses it with the invariant culture
(`--threads` and `--max-frames` as positive integers, `--out`, `--against` and `--lights` as
non-blank paths; no option takes a decimal today), the first problem fills `Error` and parsing stops;
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
metadata-only run, which takes `--lights` too; `--out`, `--threads`, `--max-frames`, `--against`
and `--lights` are passed through (`DarkOptions.LightsFrom` for the last; neither `--out` nor
`--against` goes with `--inventory`); before the run the partner folder is opened, the session
is opened with the chosen lights when `--lights` is given (a bad lights folder fails here), and
`--out` is checked against the session's root, the partner's and the chosen lights' folder
(`DarkOutputs.CheckOut`, each named by `DarkOutputs.OutFolder`), so a wrong folder costs no
decoding. Whether the output folder is one of those folders or lies inside it is
`FolderIdentity.IsOrInside`'s answer (ADR 0069). First the full paths, without case and without
a `\\?\` prefix before a drive letter, which also settles a folder that does not exist yet.
Then the folders themselves: `Id` opens a folder for nothing but its attributes
(`CreateFileW` with `FILE_FLAG_BACKUP_SEMANTICS`) and reads the volume's serial number and the
file index (`GetFileInformationByHandle`). The output folder is not walked as it was typed: the
folder above a link is not above the place the link leads to, so a junction beside the session
that leads to its `darks\` would pass. `Resolved` takes the deepest folder of the output path
that exists by the path the system resolves it to (`FinalPath`, `GetFinalPathNameByHandleW`),
puts the folders that do not exist yet back on, and that path's folders, from the last up, are
held against the protected folder's pair. So a junction, a link, a `subst` drive and a short
8.3 name of the session, or of a folder inside it, are all inside the session. `Real` is the
same resolved path without its `\\?\` prefix, which `DarkOutputs.ReadOnlyFolders` hands to
`Around` beside the path as given, so a folder reached by another name names the folder around
it both ways. A folder that
cannot be opened has no identity and is told by its path alone. The exceptions
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
