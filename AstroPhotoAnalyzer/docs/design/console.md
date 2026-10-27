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
| `DmaxOverride` | null | `--dmax n` (0 < n <= 4) | "Dmax", filled with the chosen medium's | paper black |
| `Dmax` (derived) | baryta 2.4 / canvas 1.5 / acrylic 2.2 (ADR 0028) | | | Tonal, Simulation |
| `DmaxProfile` | null | `--icc file` (sets it with `DmaxOverride`; excludes `--dmax`) | `Din profil ICC...` | Anexa F: where the Dmax came from (R-028) |
| `DmaxProfilePath` | null | set with `DmaxProfile`, the same file | set with `DmaxProfile` | the Editare Curbe tab's gamut figure re-opens the profile (R-144); the report does not print it |
| `CandidateWidthsCm` | 50, 60, 70, 80, 90, 100 | `--sizes a,b,c` | "Latimi candidate" | PrintSize |
| `ViewingDistanceM` | null = 1.5 x diagonal per row (then no row is recommended) | `--distance m` | "Distanta de privire" | PrintSize |
| `PassepartoutCm` | 7 | `--passepartout cm` | field | PrintSize (outer size) |
| `FrameCm` | 3 | `--frame cm` | field | PrintSize (outer size) |
| `TargetAspect` | null = native | `--aspect w:h` (also `wxh`) | "Raport de aspect" | Crop, PrintSize |
| `TargetBackground` | null = none | `--target-bg`, strictly inside (0, 1) once rounded to 3 decimals (the core's `TargetBackgroundError`, one message for both applications; ADR 0040) | "Fundal-tinta in plus" | Simulation (one more row, marked `(dat)`, the primary one; the Preview tile) |
| `SimulationTargets` | 0.05 .. 0.20 by 0.01 | - | - | Simulation (the grid) |
| `WcsPath` | null = the file's own WCS | `--wcs file.fit` | "WCS din FITS" | the dust and the sky (R-027: a FITS header lent to a TIFF/PNG of the same size) |
| `Seed` | 0 | - | - | Noise tiles |

The console (`Options.cs`) and the window (`ParameterFields.cs`) validate syntax only,
with the invariant culture (a comma decimal is an error that says to use the point).
Neither holds a copy of the defaults: an unset field means "the core's default". What the
syntax lets through but no analysis can use, NaN and infinity (`double.TryParse` reads the
words, and a range check is false on NaN), is the core's to refuse: `Parameters.Validate()`
names the parameter and quotes the value in one message both applications show (R-024,
ADR 0045), the same words NaN and infinity get in the target background's message.

The media are the values of `PrintMedium`, and their names one list, `Parameters.MediumNames`
(the enum's order, through `NameOf`). `Options.Media` is that list, so the `--medium` line of
`--help` and the unknown-medium error are built from it, and `ParameterFields.Media`, the
window's combo, is the same object. The level-L\* ladder of the Tonuri tab walks the enum too.
Two places still name the media by hand, because they are prose: the "Negrul hartiei" paragraph
of the Tonuri guide (`ResultTabs.cs`), which calls the third one "C-print sub acril", and the
README. The window's Dmax field starts filled, like the widths, the passepartout and the frame,
and is the one field another field rewrites: `ParameterFields` formats `Parameters.DmaxOf` of
the chosen medium into it (in the field's initialiser and in the `Medium` setter, so code that
sets both sets the medium first) and decides nothing. A field that still shows that text is no
override: `TryBuild` passes null and the core's own value is used, not a copy rounded for
display. Whether a Dmax that was typed is explicit is `Parameters.DmaxIsExplicit`'s answer,
which the report asks. The level-L\* ladder asks the same question: it marks the chosen medium's
row when the Dmax in use is that medium's, and otherwise adds and marks an explicit row, even
when another medium has that Dmax (R-042). A Dmax read from
an ICC profile (`input.md`, "The Dmax of a printer profile") arrives as `DmaxOverride` with
`DmaxProfile`, the profile's name: in the console through `Parameters.WithDmaxFromProfile`,
called by `AnalyzerCli.Run` before `Validate` (a problem is `ERROR: --icc: <reason>`, exit 1);
in the window through `ParameterFields.UseProfile`, which writes the Dmax into the field and
remembers the name with the text it wrote, so that `TryBuild` passes the name only while the
field still shows that text. `ReportWriter.DmaxOrigin` turns the three cases into Anexa F's
words.

Rules between parameters are the core's: `Parameters.Validate()` returns a message or
null, and both the console (exit 1, before any reading) and the window (the error text)
call it. It checks, in this order, the target background after rounding (R-023), NaN and
infinity in each number parameter in the parameters' order (`FiniteError`; the aspect ratio
through `PositiveError`, finite and positive, the backstop for a `Parameters` built in code:
the two aspect parsers refuse a side that reads as NaN or infinity themselves, with their
syntax message, so that the user reads the text typed and not the 0, infinity or NaN ratio
it would give),
and currently one rule between two parameters: **the passepartout must be at least
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
AstroPhotoAnalyzer <image> [--medium baryta|canvas|acrilic] [--dmax n | --icc profile.icc] [--wcs file.fit] [--distance m]
                   [--sizes cm,cm,...] [--aspect w:h] [--target-bg 0..1]
                   [--passepartout cm] [--frame cm] [--lang en|ro] [-o|--out file] [-h|--help]
```

Flags may come before or after the image. `Options.Parse` never throws; a bad value is
an `Error` printed as `ERROR: ...` (`EROARE: ...` with `--lang ro`). Every run prints the banner and copyright on lines
1-2, including `--help` and failures; `--help` ends with the GPLv3 warranty lines.

Exit codes: **0** no recommendation above Ok, **1** error (bad argument, invalid
parameter combination, missing file, a folder, unreadable format, refused linear image,
cannot write `-o`), **2** at least one Attention, **3** at least one Problem. `-o` writes `WriteFull` through
`ReportWriter.SaveFull` (whole or not at all, `recommendations-and-report.md`) and prints
`    -> report saved: <file>` (`    -> raport salvat: <file>` with `--lang ro`). `--lang` and its
value are read first and stripped before the options are parsed (`language.md`).

## Golden scenes

- **Golden scenes are synthetic and deterministic** (`Scenes.cs`): a dark sky with every
  defect the reference image had (low background, green cast, warm horizon corner,
  over-denoised, a nebula and a dust lane for continuous tones), a clean sky, a mono
  8-bit PNG, a linear FITS. Version banner and temp path are scrubbed. Each case is written
  twice, `name.txt` with `--lang ro` and `name.en.txt` in English (`CheckBoth`). Re-approve with
  `APA_UPDATE_GOLDEN=1 dotnet test AstroPhotoAnalyzer.sln` and read the diff.
