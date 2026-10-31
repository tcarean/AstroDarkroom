# The session, the inventory and the console

**Covers:** `src/LightsCheck.Core/Session/SessionFolder.cs`, `src/LightsCheck.Core/Session/FrameInfo.cs`, `src/LightsCheck.Core/Analysis/CadenceAnalyzer.cs`, `src/LightsCheck.Core/Runs/InventoryRun.cs`, `src/LightsCheck.Core/Report/InventoryReport.cs`, `src/LightsCheck.Core/Report/RunReport.cs`, `src/LightsCheck.Cli/Options.cs`, `src/LightsCheck.Cli/Program.cs`.

The part every run goes through before a pixel is decoded: finding the frames, reading what
their files say about the shot, the rhythm of the night, and the console that dispatches the
three runs. The behaviour is R-001 to R-006 and the folder rules are ADR 0004; this note is
how the code does it.

## The session folder (`SessionFolder`)

`SessionFolder.Open(path)` resolves the full path and throws `DirectoryNotFoundException`
when it is missing. When `lights/` exists it is the lights folder, empty or not (an empty one
throws, never falls back to the root, which holds `result.fit` and the other stacked
results); otherwise the folder itself is. `FramesIn(dir)` lists one folder's frames: the
camera raws (`RawReader.IsRawPath`) when there are any, else the FITS files
(`FitsReader.IsFitsPath`), sorted by file name with an ordinal case-insensitive compare, so
`DSC_0010` follows `DSC_0009` and a lower-case extension changes nothing. No frames at all
throws `InvalidOperationException` naming the folder and the extensions looked for.
`biases/` and `darks/` go through the same `FramesIn`; `BiasesDir` and `DarksDir` are null
when the folder is absent or empty, and the report prints `(missing)`. The darks are only
counted (R-042): the `fixed pattern` row of the series report says whether Siril will subtract
the sensor's pattern, and no dark is ever opened. The record exposes `Root`, `LightsDir`,
`BiasesDir`, `DarksDir`, `Lights`, `Biases` and `Darks` as full paths, and `LightsAreRaws`,
true when the lights are camera raws rather than FITS files (one file decides, since
`FramesIn` never mixes the two; the series uses it to flip the solve, ADR 0023); nothing
here writes.

## The metadata of a frame (`FrameInfo`)

One record per light, with its 1-based `Index`, its path, the shot fields (camera, focal
length, f-number, exposure, ISO, timestamp, pixel size, the raw's 35 mm-equivalent focal
length and visible size (R-066), the lens's ID, mount and name as the raw gives them (R-065,
`imaging.md`) and the lens's own focal range and maximum aperture, all nullable) and an
`Error`. `FrameInfo.Read(index, path)`
never throws: it calls `ImageLoader.LoadInfo` (the metadata without the pixels,
`imaging.md`) and turns any exception into the record's `Error`: libraw's own type no longer
reaches it (`RawReader` wraps it into `InvalidDataException` naming the file, ADR 0019), and a
`.fit` that is not a FITS is refused by the reader itself with `FITS: the file does not begin
with SIMPLE.` (R-008, ADR 0027), so the record guesses nothing from an empty header any more.
`FrameInfo.From(index, path,
loaded)` builds the same record from a frame that was decoded for measuring, so `FrameRun`
and `SeriesRun` read the file once.

## The cadence (`CadenceAnalyzer`)

`Analyze(frames)` walks the list in order and keeps a pause per position of the list, not
per frame index, so a filtered list (the good frames only) works too. A frame with a
timestamp and an exposure is timed: the first timed start is `Start`, the latest end
(start plus exposure) is `End`, the exposures sum to `IntegrationSeconds`, and the pause
before a timed frame is its start minus the previous timed frame's end. A frame without a
time or an exposure gets no pause, breaks the chain (the next timed frame gets none either)
and stays out of the span and the integration. `DutyPct` is integration over span; 0 when
nothing is timed.

## The inventory (`InventoryRun`, `InventoryReport`)

`InventoryRun.Run(folder)` opens the session, reads every light's metadata in order and
returns `Inventory(Session, Lights, Cadence)`; `HasErrors` is true when any light carries an
error, which is what makes the console exit 1. `InventoryReport.Write` prints the header and
the table. `WriteHeader` is shared: the series report opens with the same session lines
(folders with their frame counts and extension, camera, lens, exposure, interval, and a
`WARNING` line when frames could not be read). The lens line puts the shot's focal length
and f-number first, then the lens as its chip declares it (`lens 135 mm f/2.0`, a range
for a zoom) and the lens as the raw names it: the name in quotes, then the ID and the mount in
one parenthesis (`"EF50mm f/1.4 USM" (ID 198, Canon EF)`), or the ID with the mount when the
raw gives no name (`ID 74 (Nikon F)`), nothing for a mount alone (R-065); the frame read is the
first readable one with a focal length, an ID or a name; the exposure line lists each distinct exposure and ISO
pair and says `(different exposures)` when there is more than one; the interval line is start,
end, span, integration, timed frame count and duty. The table prints one row per light with
`-` for a missing field and `error: <message>` in place of the fields for a frame that
could not be read. File names are cut at 20 characters.

## The lines every run shares (`RunReport`)

`WriteBanner` prints the two lines of R-001 from `VersionInfo` (`Version.cs`: the version
and the copyright of `Directory.Build.props`, read from the core assembly's attributes),
before the arguments are parsed. `WarrantyLines` is the GPLv3 disclaimer `--help` prints, with the
licence URL. `F(value, decimals)` is the one number formatter of the reports (invariant
culture, fixed decimals) and `Duration(seconds)` prints `48 s`, `54 min` or `3 h 22 min`;
every report in `Report/` uses them, so no text is formatted by a view-model or the console.

## The console (`Options`, `Program`)

`Options.Parse(args)` takes one folder and the options `--inventory`, `--frame N`,
`--out DIR`, `--threads N`, `--pixel UM`, `--est stanga|dreapta`, `--nord sus|jos`,
`-h`/`--help`, and `--east left|right` / `--north up|down` beside the two arrow options, each
name taking either language's words (R-064); no arguments at all means help. The first error
wins and is a sentence in the run's language naming the argument: an option without its value, a frame or thread count under 1,
a pixel size that is not a finite positive number (`SeriesOptions.PixelSizeIsValid`, the check
the window shares), an arrow word that is not one of the two, an
unknown option (anything else starting with `-`), a second folder, a missing folder,
`--inventory` with `--frame`. The pixel size is null unless `--pixel` was given (the run then
reads it from the files or the raws, `series-analysis.md`, R-066), the thread count defaults to
0, which `SeriesRun` reads as half the cores, and the arrow keys to
`ArrowKeys.Default` (right east, up north), each option replacing one key of the record.

`Program.Run(args, stdout, stderr)` is the whole console, called by `Main` with the real
streams (after switching the console's output encoding to UTF-8 for the run, since the report
carries the arrow symbols of R-040, and putting the previous one back in a `finally`, the code
page outliving the process otherwise; a process without a console reads the code page but throws
`IOException` on the setter, which `SwitchToUtf8` writes to the log at Debug, handing back
nothing to put back) and by the tests with `StringWriter`s: it writes the banner, takes
`--lang` out and runs the rest in its language (`language.md`), parses, and dispatches to `FrameRun` + `FrameReport` for `--frame`, to `InventoryRun` +
`InventoryReport` for `--inventory`, and otherwise to `SeriesRun.Run(folder,
SeriesOptions(pixel, threads, arrows), progress)` + `SeriesReport.Write`, then
`SeriesOutputs.WriteAll` when `--out` was given,
printing `-> written: <path>` (`-> scris:`) per file. The series progress goes to stderr as one line
rewritten in place (`\r    frame 12 / 87`, `cadrul` in Romanian), written synchronously on the reporting thread
under a lock, because a `Progress<T>` would post to the thread pool and could print after
the report; stdout stays the report alone, which is what the goldens compare. Exit codes:
0 for help and for a clean run; 1 for a parse error, for any `IOException`,
`InvalidDataException`, `InvalidOperationException`, `ArgumentException` or
`UnauthorizedAccessException` the runs throw (printed as `RunReport.Error(message)`,
`ERROR: <message>` or `EROARE: <message>`, on stderr, the same line the window shows), and
for an inventory or a series in which a frame could not be read, the report having been
printed in full. Any other exception is a bug and crashes. `WriteUsage` is the `--help`
text, written whole per language; the goldens `help.txt` and `en_help.txt` hold it.

`Options.Mode` is the mode, decided once from what `Parse` read by the precedence above: help,
then an error, then `--frame`, then `--inventory`, else the series. `RunIn` dispatches on it and
the diagnostic log's `mode=` line reads it, so the two cannot disagree. `Main` starts the log
before anything else and closes it last, after the code page is put back; `Run` and `RunIn`
configure nothing, so the tests call them in-process. `SessionFolder.Open` writes the session's
line, `FrameInfo.Read` a frame whose metadata could not be read (unless its caller, the series,
takes the exception back through the overload with an `out` parameter and writes it itself), and
`InventoryRun` its start and done lines (`logging.md`).
