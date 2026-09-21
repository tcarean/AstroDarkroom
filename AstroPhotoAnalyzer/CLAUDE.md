# AstroPhotoAnalyzer

Print-readiness diagnostic for a *finished* astrophoto: a stretched TIFF, PNG or FITS as
it comes out of Siril. It measures the image and reports, in decision order, how to send
it to print - file format, whether the background survives paper, colour cast and
gradient, the largest size the real detail supports, how much denoise the print version
needs. It **exports nothing**: recommendations only, each with the number and the
threshold behind it. Exports are made cleanly from Siril.

Two applications - a console tool and a WPF window - over one core library. The core
hand-parses FITS and takes ImageSharp for TIFF/PNG/JPEG. .NET 8.

**`docs/specification.md` is the requirements document**: every measurement, rule,
threshold, option, tab and format, at the level needed to maintain and extend the tool.
Read it before changing behaviour. `docs/implementation-plan.md` records how it was built
and what each phase taught; `docs/acceptance-de-printat.md` records the run on the
reference image against the hand-made numbers.

This tool is a sibling of FlatCheck (`../FlatCheck`) and follows its conventions to the
letter. Where the two differ, this file says so.

## Structure

- `Directory.Build.props` - `<Version>` and `<Copyright>`, shared by every project. The
  **single** place to edit either. See "Versioning".
- `AstroPhotoAnalyzer.Core/` - everything that computes and everything that formats. Knows
  nothing about consoles or windows and has no entry point, so it *cannot* reach
  `Console`. `Input/` (readers, `Raster`), `Analysis/` (one class per measurement family),
  `Report/` (recommendations and text), `Runs/` (the chain both applications call),
  `Thresholds.cs`, `Parameters.cs`.
- `AstroPhotoAnalyzer/` - the console: `Program.cs`, `Options.cs`, `AnalyzerCli.cs`.
  Nothing else.
- `AstroPhotoAnalyzer.Wpf/` - the window. `net8.0-windows`, dark theme. Appears in phase 5.
- `AstroPhotoAnalyzer.Tests/` - core + console, portable.
- `AstroPhotoAnalyzer.Wpf.Tests/` - view-models, converters, `WindowSmokeTests`. Phase 5.
- `Icon/` - `make_icon.py` generates `AstroPhotoAnalyzer.ico`, shared by both exes.
- `docs/` - `specification.md` (requirements: what and how), `implementation-plan.md`
  (why, per phase), `acceptance-de-printat.md` (the reference run).

## Conventions

- **Every change to behaviour updates `docs/specification.md` in the same change.** A
  new or changed measurement, threshold, recommendation rule, parameter, option, report
  line, tab, input format or dependency is not done until its section in the
  specification says what the code now does. The spec's §15 lists, per kind of change,
  the places to touch - the code, the tests, the goldens, and the spec section. Do not
  leave it for "later"; that is how FlatCheck's README once described a tool that did
  not exist.

- **User-facing text (CLI messages, `--help`, window labels, code comments) is in
  Romanian**, without diacritics in code and messages, with diacritics in Markdown.
  Documentation *about* the code (this file, `docs/`) is in English.
- **Nothing is computed outside `AstroPhotoAnalyzer.Core`.** A new number, a new
  threshold or a new sentence of diagnosis belongs in `Analysis/`, `Thresholds.cs` or
  `Report/` - never in a view-model or in the console. A view-model formats; it does not
  decide. The window's explanatory texts - a row's `Description`, a table's
  `GuideParagraph`s, a preview tile's `Check`, a parameter field's `*Help` tooltip - are
  the one kind of sentence a view-model writes, and they are explanation, not diagnosis:
  every number in them is interpolated from `Thresholds` or from the result, never
  typed. A literal a text needs is lifted into `Thresholds` first, with its comment -
  even a range that no rule reads (`ScreenBackground*`, `PrintBackground*` exist only
  for a tooltip).
- **"Fundal" names one thing, the sky, and two measurements of it.** Tonuri measures its
  *level* (the median of the reference channel, what the target background is compared
  to; "Nivelul fundalului"); Culoare measures its *colour* (per-channel medians of the
  darkest quarter, for cast and gradients; "Culoarea fundalului"). They are different
  numbers on different scales. Every label, row, appendix and `Because` that shows one
  says which, and no tab is called just "Fundal".
- **Every recommendation carries its evidence.** A `Recommendation` has a `Because`: the
  metric and the threshold. "Do X" without a number is not a recommendation here.
- **Thresholds are empirical rules of thumb, not physical constants.** Each one in
  `Thresholds.cs` has a comment saying where it came from. Most came from measuring one
  image, `de_printat.png`, in the conversation that started the tool; they are a starting
  point.
- **The image is display-referred and final.** The tool assumes a stretched image. It does
  not stretch linear data; a linear FITS is refused with a message, not silently
  mis-measured. The print-background simulation is a curve applied to stretched data and
  is labelled as a simulation, not as what Siril will produce.
- **Never bump the version and never commit** - both are the user's call. See
  "Versioning" and "Committing".
- **Every source file starts with the GPL notice.** New files included, XAML included.
  `LicenseHeaderTests` enforces it; its project list is explicit, so adding a project is
  a decision.
- **User-facing documentation lives in `README.md`, and only there.** Code comments explain
  *why the code is the way it is*; the README explains *how to use the tool*; the
  specification explains *what the tool does and how*, for whoever maintains it.

## The dependencies

**Dependencies are discussed before they are added, not avoided.** Bring the candidate,
its licence, its size and its alternatives to the author, and let them decide. Do not add
one silently, and do not add one to save an afternoon on something the BCL already does.

Two constraints do not bend: **licence compatibility with GPLv3**, written down in
`THIRD-PARTY-NOTICES.md`, and **the single-copyable-binary promise** for the console
(pure-managed packages keep `PublishSingleFile` simple).

**A new package means three places, and a test enforces two of them**: the
`PackageReference`, an entry in `THIRD-PARTY-NOTICES.md` (the *why*), and an entry in
`AstroPhotoAnalyzer.Core/ThirdParty.cs` (what the window's "Despre" displays).
`ThirdPartyTests` binds the csproj and the list in both directions and each name to a
heading in the notices file. The README's table is the fourth copy, kept by hand.

### `SixLabors.ImageSharp`

Approved before the first line was written. It decodes TIFF and PNG (8/16-bit, ICC
metadata) and encodes/decodes JPEG for the compression table. Unlike in FlatCheck it is
**not** peripheral: the pixels every measurement runs on come through it. Pinned to
3.1.11 - 4.x adds a licence-key MSBuild task that errors on Release; 3.1.x has the same
licence text and no task. 3.1.x never applies the ICC profile at decode, which is the
behaviour we want. See the notices file for the licence reasoning.

Four things about 3.1.11 were measured rather than assumed, and the code depends on
them: its TIFF encoder cannot write 16-bit RGB, `Image.Identify` does not see a PNG's
ICC profile but `Image.Load` does, the TIFF decoder leaves the profile on the first
frame, and the PNG encoder does not write `iCCP`. Details and consequences in
`docs/implementation-plan.md`, "Things measured about ImageSharp". The 16-bit TIFF and
PNG fixtures in the tests are hand-built for exactly these reasons - don't replace them
with ImageSharp-encoded ones.

## The two applications

Neither reimplements a number. Both call `Runs/AnalysisRun.Execute`, which loads the
file, runs every analyzer, builds the recommendations and writes the report into a
`TextWriter`. The console writes it to stdout (and to `-o`); the window shows it as
panels and saves it as `Raport.txt`, byte-identical to the console's output, banner
included.

Exit codes: 0 clean, 1 error (missing file, bad argument, unreadable format), 2 at least
one "Atentie", 3 at least one "Problema". Monotone: anything non-zero means "look".

## Versioning

Every run prints `AstroPhotoAnalyzer <version>` as its **first line** and the copyright as
its **second** - every mode, `--help`, failed runs. `RunReport.WriteBanner` is the only
place that prints them. `VersionInfo` reads `<Version>` from the assembly; don't
reintroduce a literal. **The user bumps the version manually.** Never bump it on your own
initiative; if a change seems to warrant a bump, say so.

## Licensing

GPLv3-or-later. `LICENSE` is the verbatim FSF text. Every source file starts with the
notice (`//` for C#, `#` for Python, one `<!-- -->` block for XAML). `--help` carries the
warranty disclaimer (GPLv3 §5(d)); the window's "Despre" shows the same text plus the
third-party list from `ThirdParty.Components`. The year and the author are the user's
call - don't roll the year forward.

## Committing

**Do not commit.** Make the changes, run the tests, and hand over a commit message for the
user to use. Don't `git add`, don't stage, don't amend. A good message follows the
sibling projects' log: a short lowercase subject, then a body that explains *why*. No
`Co-Authored-By` trailer.

Note: this folder lives inside the `photo` monorepo (`E:\Photografie\Code`); the git root
is the parent. Sample images never enter the repo - the root `.gitignore` covers `*.tiff`
but not `*.tif` or `*.png`, so keep them out by hand.

## Build & test

```bash
dotnet build AstroPhotoAnalyzer.sln -c Release
```

```bash
dotnet test AstroPhotoAnalyzer.sln
```

The solution is Windows-only because of the WPF project; the console builds alone with
`dotnet build AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj`. Two test projects run:
`AstroPhotoAnalyzer.Tests` (core + console, portable) and `AstroPhotoAnalyzer.Wpf.Tests`
(window, Windows-only, one STA thread shared through `StaFixture`).

Golden-output tests snapshot the console text on deterministic synthetic scenes;
re-approve with `APA_UPDATE_GOLDEN=1 dotnet test AstroPhotoAnalyzer.sln` and read the
diff. `APA_DUMP_SCREENS=<dir> dotnet test AstroPhotoAnalyzer.Wpf.Tests` saves the
window renders as PNGs - a way to look at the window without running it, not a golden
comparison.

## Run

```bash
dotnet publish AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

```bash
dotnet publish AstroPhotoAnalyzer.Wpf/AstroPhotoAnalyzer.Wpf.csproj -c Release -r win-x64 --self-contained
```

The console is pure managed (ImageSharp has no native binaries), so `PublishSingleFile`
gives one exe. The window publishes as a folder.

## The window

`MainViewModel` owns the file, the `ParameterFields` and six `ResultTabViewModel`s. It
runs `AnalysisRun.Execute` on a pool thread and populates the tabs from the result; it
opens no dialogs (the window does), so it is testable. View-models are public and cannot
expose core types; `Rows.cs` holds what XAML binds to. Tables are shown as pre-aligned
mono strings, the same the report writes. `WindowSmokeTests` renders every view with a
real result and fails on any binding error; it renders a window's *content*, because an
unshown `Window` renders transparent.
