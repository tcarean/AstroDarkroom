# The console and the parameters

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §4 and §9 and the note
> on the golden scenes of phase 4 in `docs/implementation-plan.md`. The behaviour is stated
> with IDs in `../requirements.md` (R-001 to R-007, R-020 to R-026). **Covers:**
> `AstroPhotoAnalyzer/Program.cs`, `AstroPhotoAnalyzer/Options.cs`,
> `AstroPhotoAnalyzer/AnalyzerCli.cs`, `AstroPhotoAnalyzer.Core/Parameters.cs`, the help texts
> of `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs`,
> `AstroPhotoAnalyzer.Tests/GoldenOutputTests.cs` and `AstroPhotoAnalyzer.Tests/Scenes.cs`. A
> change to the code this describes means this note is updated in the same task, or deleted
> with the decision it explains.

## Parameters (`Parameters.cs`)

| parameter | default | console flag | window field | used by |
|---|---|---|---|---|
| `Medium` | `Baryta` | `--medium baryta\|canvas\|acrilic` | combo | Dmax |
| `DmaxOverride` | null | `--dmax n` (0 < n <= 4) | "Dmax explicit" | paper black |
| `Dmax` (derived) | baryta 2.0 / canvas 1.5 / acrylic 2.2 | | | Tonal, Simulation |
| `CandidateWidthsCm` | 50, 60, 70, 80, 90, 100 | `--sizes a,b,c` | "Latimi candidate" | PrintSize |
| `ViewingDistanceM` | null = 1.5 x diagonal per row (then no row is recommended) | `--distance m` | "Distanta de privire" | PrintSize |
| `PassepartoutCm` | 7 | `--passepartout cm` | field | PrintSize (outer size) |
| `FrameCm` | 3 | `--frame cm` | field | PrintSize (outer size) |
| `TargetAspect` | null = native | `--aspect w:h` (also `wxh`) | "Raport de aspect" | Crop, PrintSize |
| `TargetBackground` | null = none | `--target-bg 0..1` | "Fundal-tinta in plus" | Simulation (one more row, marked `(dat)`, the primary one; the Preview tile) |
| `SimulationTargets` | 0.10 .. 0.20 by 0.01 | - | - | Simulation (the grid) |
| `WcsPath` | null = the file's own WCS | `--wcs file.fit` | "WCS din FITS" | the dust and the sky (R-027: a FITS header lent to a TIFF/PNG of the same size) |
| `Seed` | 0 | - | - | Noise tiles |

The console (`Options.cs`) and the window (`ParameterFields.cs`) validate syntax only,
with the invariant culture (a comma decimal is an error that says to use the point).
Neither holds a copy of the defaults: an unset field means "the core's default".

Rules between parameters are the core's: `Parameters.Validate()` returns a message or
null, and both the console (exit 1, before any reading) and the window (the error text)
call it. Currently one rule: **the passepartout must be at least
`PassepartoutMinFrameRatio` (2) x the frame width**; 0 means no passepartout and is not
checked. The window's distance, aspect, target-background, framing and WCS fields carry help
text (`DistanceHelp`, `AspectHelp`, `TargetBgHelp`, `PassepartoutHelp`, `FrameHelp`, `WcsHelp`) as
tooltips on the field
and its label. Each text is two to four paragraphs separated by a blank line (`\n\n`),
which the tooltip wraps at 420 px with a 17 px line height (`Theme/Dark.xaml`); a test
checks the paragraphs. The distance one says what the size test does with it (and that without
stars it sets the pixel-grid floor), that empty means 1.5 x each size's own diagonal and
then no size is recommended (the wall chooses), and
where 1.5 comes from: the print-and-framing rule of thumb "1-2 x the diagonal", 1 x
being the lens's own perspective (a normal focal length equals the frame diagonal) and
2 x gallery practice; at 1.5 x the print subtends ~37 degrees. The aspect one says that
empty means the native ratio with no crop (what a fine-art lab needs), that a ready-made
frame does not constrain it either (the passepartout absorbs the difference), and that it
is for labs with fixed formats. The target-background one says that the level is one more row for the simulation
beside its grid (`SimulationTargets`' bounds), marked `(dat)` and used by the Preview tile,
and that empty means the grid alone; how the measured background is taken (the median of the
green channel over the whole frame) and how the simulation uses it (gamma = ln(target) /
ln(background), a simulation and not Siril's stretch); where a screen image's sky usually sits
and where a print's should (`ScreenBackgroundLo`-`Hi`, `PrintBackgroundLo`-`Hi`); and that the
report's recommendation is the minimum level that keeps the dust above paper black, which the
field does not change. The WCS one says what a plate-solved FITS of the same size gives (the
clouds by name, through the Lynds catalogue), why a TIFF or PNG needs it lent (the export
carries no header, the stack does), what empty means (the file's own WCS, else the fallback
rule, said in red in the Tonuri tab), and that the size must be identical.

## The console (`AstroPhotoAnalyzer/`)

```
AstroPhotoAnalyzer <image> [--medium baryta|canvas|acrilic] [--dmax n] [--distance m]
                   [--sizes cm,cm,...] [--aspect w:h] [--target-bg 0..1]
                   [--passepartout cm] [--frame cm] [-o|--out file] [-h|--help]
```

Flags may come before or after the image. `Options.Parse` never throws; a bad value is
an `Error` printed as `EROARE: ...`. Every run prints the banner and copyright on lines
1-2, including `--help` and failures; `--help` ends with the GPLv3 warranty lines.

Exit codes: **0** no recommendation above Ok, **1** error (bad argument, invalid
parameter combination, missing file, a folder, unreadable format, refused linear image,
cannot write `-o`), **2** at least one Attention, **3** at least one Problem. `-o` writes `WriteFull` and prints
`    -> raport salvat: <file>`.

## Golden scenes

- **Golden scenes are synthetic and deterministic** (`Scenes.cs`): a dark sky with every
  defect the reference image had (low background, green cast, warm horizon corner,
  over-denoised, a nebula and a dust lane for continuous tones), a clean sky, a mono
  8-bit PNG, a linear FITS. Version banner and temp path are scrubbed. Re-approve with
  `APA_UPDATE_GOLDEN=1 dotnet test AstroPhotoAnalyzer.sln` and read the diff.
