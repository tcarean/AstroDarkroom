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
`biases/` goes through the same `FramesIn`; `BiasesDir` is null when the folder is absent or
empty, and the report prints `(lipsa)`. The record exposes `Root`, `LightsDir`, `BiasesDir`,
`Lights` and `Biases` as full paths; nothing here writes.

## The metadata of a frame (`FrameInfo`)

One record per light, with its 1-based `Index`, its path, the shot fields (camera, focal
length, f-number, exposure, ISO, timestamp, pixel size, the Nikon lens ID and the lens's own
focal range and maximum aperture, all nullable) and an `Error`. `FrameInfo.Read(index, path)`
never throws: it calls `ImageLoader.LoadInfo` (the metadata without the pixels,
`imaging.md`), turns any exception, libraw's own type included, into the record's `Error`,
and treats a `.fit` whose header came back empty (the reader's answer to a file that does
not start with `SIMPLE`) as the error `nu e un fisier FITS`. `FrameInfo.From(index, path,
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
(folders with their frame counts and extension, camera, lens, exposure, interval, and an
`ATENTIE` line when frames could not be read). The lens line puts the shot's focal length
and f-number first, then the lens as its chip declares it (`obiectiv 135 mm f/2.0`, a range
for a zoom) and the Nikon lens ID; the exposure line lists each distinct exposure and ISO
pair and says `(expuneri diferite)` when there is more than one; the interval line is start,
end, span, integration, timed frame count and duty. The table prints one row per light with
`-` for a missing field and `eroare: <message>` in place of the fields for a frame that
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
`--out DIR`, `--threads N`, `--pixel UM`, `-h`/`--help`; no arguments at all means help.
The first error wins and is a sentence in Romanian naming the argument: an option without
its value, a frame or thread count under 1, a pixel size that is not a positive number, an
unknown option (anything else starting with `-`), a second folder, a missing folder,
`--inventory` with `--frame`. The pixel size defaults to `SeriesOptions.D3300PixelUm` (3.92
um) and the thread count to 0, which `SeriesRun` reads as half the cores.

`Program.Run(args, stdout, stderr)` is the whole console, called by `Main` with the real
streams and by the tests with `StringWriter`s: it writes the banner, parses, and dispatches
to `FrameRun` + `FrameReport` for `--frame`, to `InventoryRun` + `InventoryReport` for
`--inventory`, and otherwise to `SeriesRun.Run(folder, SeriesOptions(pixel, threads),
progress)` + `SeriesReport.Write`, then `SeriesOutputs.WriteAll` when `--out` was given,
printing `-> scris: <path>` per file. The series progress goes to stderr as one line
rewritten in place (`\r    cadrul 12 / 87`), written synchronously on the reporting thread
under a lock, because a `Progress<T>` would post to the thread pool and could print after
the report; stdout stays the report alone, which is what the goldens compare. Exit codes:
0 for help and for a clean run; 1 for a parse error, for any `IOException`,
`InvalidDataException`, `InvalidOperationException`, `ArgumentException` or
`UnauthorizedAccessException` the runs throw (printed as `EROARE: <message>` on stderr), and
for an inventory or a series in which a frame could not be read, the report having been
printed in full. Any other exception is a bug and crashes. `WriteUsage` is the `--help`
text; the golden `help.txt` holds it.
