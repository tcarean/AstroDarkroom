# The session folder, the inventory and the console

**Covers:** `src/DarksCheck.Core/Session/SessionFolder.cs`, `src/DarksCheck.Core/Session/FrameInfo.cs`, `src/DarksCheck.Core/Analysis/CadenceAnalyzer.cs`, `src/DarksCheck.Core/Analysis/MatchAnalyzer.cs`, `src/DarksCheck.Core/Thresholds.cs`, `src/DarksCheck.Core/Runs/InventoryRun.cs`, `src/DarksCheck.Core/Report/InventoryReport.cs`, `src/DarksCheck.Core/Report/RunReport.cs`, `src/DarksCheck.Core/Version.cs`, `src/DarksCheck.Cli/Options.cs`, `src/DarksCheck.Cli/Program.cs`.

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

## The metadata

`FrameInfo.Read` opens a file through `ImageLoader.LoadInfo` (no pixels: tens of milliseconds
per raw) and never throws; the error becomes the record's `Error`, so one unreadable frame is
listed among the others and only the exit code says it (R-003). The FITS reader's current copy
answers a file that does not start with `SIMPLE` with an empty header, hence the explicit
check in `Read`; it goes when the copy is refreshed from LightsCheck (ADR 0003).

## The cadence

`CadenceAnalyzer.Analyze` walks the frames in list order and keeps a pause per position, so a
filtered list works too. A frame without a time or an exposure breaks the chain: it gets no
pause, and neither does the next one, and no interval is counted across it. The median
interval is over consecutive timed starts; a long pause is one over `Th.PauseSeconds`,
recorded with the index of the frame it precedes. The darks and the lights each get a
`Cadence`; the biases do not, their bursts say nothing.

## The match (check 1)

`MatchAnalyzer.Settings` clusters the readable frames of a group by ISO and by exposure, two
exposures within `Th.ExposureTolerance` of the longer joining one cluster, and returns the
largest cluster as the group's settings with whether it holds every readable frame. The two
verdicts compare clusters: darks against lights on exposure (same tolerance) and ISO, and the
bias against `Th.BiasMaxExposureSeconds`. `Verdict.Unmeasured` stands for a missing group or
missing exposures; the report, not the analyzer, turns it into words.

## The report and the console

`InventoryReport` prints the session lines in a 19-column label field, then one table per
non-empty group; the exposure is printed with one decimal from 1 s up and up to four below,
so a 1/20 s bias reads `0.05` and the D3300's 1/4000 s reads `0.0002`. `RunReport` holds the
banner, the copyright and the warranty lines of `--help`; `VersionInfo` reads the version and
the copyright from the assembly attributes `Directory.Build.props` sets. `Program.Run` is the
console behind `Main`: banner first, then the options, then one run, with the exceptions a
bad folder can raise turned into `EROARE:` on stderr and exit 1. Until the analysis modes
exist, a run without options is the inventory.
