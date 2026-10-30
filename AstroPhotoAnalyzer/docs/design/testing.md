# Testing

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §11; the rows for
> invariants and validation and the last section were added the same day, with the kit's
> process. **Covers:** the
> shared fixtures `AstroPhotoAnalyzer.Tests/SyntheticRasters.cs`,
> `AstroPhotoAnalyzer.Tests/Scenes.cs`, `AstroPhotoAnalyzer.Tests/WcsFixtures.cs`,
> `AstroPhotoAnalyzer.Tests/FitsTestData.cs`,
> `AstroPhotoAnalyzer.Tests/TiffTestData.cs`, `AstroPhotoAnalyzer.Tests/PngTestData.cs`,
> `AstroPhotoAnalyzer.Tests/IccTestData.cs`, `AstroPhotoAnalyzer.Tests/ImageFiles.cs`,
> `AstroPhotoAnalyzer.Tests/TempWorkspace.cs`, the project file
> `AstroPhotoAnalyzer.Wpf.Tests/AstroPhotoAnalyzer.Wpf.Tests.csproj` that borrows six of
> them, and the window's harness `AstroPhotoAnalyzer.Wpf.Tests/StaFixture.cs` and
> `AstroPhotoAnalyzer.Wpf.Tests/WindowSmokeTests.cs`, and the tests of the process
> `AstroPhotoAnalyzer.Tests/RepoInvariantTests.cs`, `AstroPhotoAnalyzer.Tests/ValidationTests.cs`,
> `AstroPhotoAnalyzer.Tests/StarPairTests.cs` and `AstroPhotoAnalyzer.Tests/TestData.cs`. A
> change to the code this describes
> means this note is updated in the same task, or deleted with the decision it explains.

## Testing

| kind | where | what |
|---|---|---|
| synthetic, known answer | `AstroPhotoAnalyzer.Tests/*AnalyzerTests.cs` | Gaussian stars of known FWHM, injected noise sigma, ramps, casts, corners, upscales; each analyzer recovers the truth within a stated tolerance |
| fixtures | `SyntheticRasters`, `Scenes` (DarkSky with every defect of the reference image, CleanSky, MonoSky), `WcsFixtures` (a synthetic TAN WCS; the LDN 42 scene: the cloud's disc dark on a flat sky at 9"/px, narrow so that the Pipe's other clouds stay out of the frame; the band scene across the plane with a dust blob over the 5 % floor, also drawn on a 10 degree field near the Veil (`NearTheVeil`, `BandScene(wcs, withDust)`) where only the latitude span decides the path (ADR 0037); the nebula scene without a WCS, a halo with a core on a noisy sky, with or without a dark lane, for the sky-relative classes of ADR 0033), `IccPrinterTestData` (printer profiles built by hand with a chosen colorimetric black: v2 `lut16` on RGB or CMYK, v4 `lutAToB` from a matrix; their perceptual table has another black), `FitsTestData`, `TiffTestData` (hand-built 16-bit RGB TIFF with ICC), `PngTestData` (hand-built 16-bit PNG with iCCP, own CRC-32), `IccTestData` (v2/v4 profiles), `ImageFiles` (ImageSharp-encoded 8-bit files), `TempWorkspace` | the 16-bit TIFF/PNG fixtures are hand-built because ImageSharp 3.1 cannot encode them |
| golden | `GoldenOutputTests`, `Golden/*.txt` | the console's whole output on the scenes (one of them, `dark-sky-wcs`, a float FITS with a WCS centred on LDN 42), version banner and temp path scrubbed; `APA_UPDATE_GOLDEN=1` re-approves |
| console | `CliTests`, `OptionsTests` | exit codes, `-o`, parsing, parameter mapping |
| recommendations | `RecommendationsTests` | every flag the reference image raised, on the dark scene |
| plumbing | `LicenseHeaderTests` (every source file starts with the notice; explicit project list), `VersionTests` (version/copyright from props, banner on line 1), `ThirdPartyTests` (csproj <-> `ThirdParty.Components` <-> notices headings) | |
| language | `LanguageTests`, `AppLanguageTests`, `WindowEnglishTests`, the `*.en.txt` goldens, `RomanianWords` (the guard); `RunsInRomanian` in each test assembly sets Romanian as the default, so the older tests assert Romanian (`language.md`) | a console run picks its own language, English without `--lang`: a test that asserts the console's Romanian passes `--lang ro` |
| window | `Wpf.Tests/WindowSmokeTests` (renders the window's *content*, About, every tab with a real result, the map and preview windows, the error card; fails on any binding error; `APA_DUMP_SCREENS=<dir>` saves PNGs), `CultureTests` (report identical under ro-RO), `ParameterFieldsTests`, `ViewModelTests`, `OverlayWindowTests` (the Tonuri tab's definitions and buttons, the map and preview view-models with and without a WCS, and the two windows built over them, the preview's check boxes driving its layers), `EmphasisTests` (a because's `**` marks in bold after its label), `ExportTabTests` (the q100 evidence and the 16-bit master mark), `AutomationTreeTests` (every tab's view under its item in the UI Automation tree, once, and under no other tab's item, the Tonuri tab's `Preview` buttons among them; nothing in the tabs' applied template by the stock name `PART_SelectedContentHost`, through which WPF would move the keyboard focus into a tab on a click: a lookup, since focus needs a window on screen) | one STA thread with a message loop per process (`StaFixture`); every window a test builds stays alive in `Application.Windows`, and radio buttons with a `GroupName` in windows never shown form one group across the process (no visual root), so a test that leaves a `CurvesView` (a `MainWindow`'s Curves tab included) with its Print box checked unchecks a later test's: `CurvesTabTests` asserts those boxes, and `AutomationTreeTests` clears its windows' `DataContext` in a `finally` |
| invariants | `RepoInvariantTests` | every `implemented` row of `docs/requirements.md` has a test named `R###_...` and every such test names a real row, read from the sources of both test projects; unique IDs; the design index exists; every validation case names a manifest file; `CLAUDE.md` at most 200 lines |
| validation | `ValidationTests`, `StarPairTests`, `TestData` | the reference images (the PNG, V-001 to V-003, V-005 and V-009; the plate-solved FITS, V-004 and V-008; the Rosetta FITS, V-010 and V-011; the Orion FITS, V-012; the Veil FITS, V-013 and V-014; the two profiles, V-006 and V-007) against `docs/validation.json`; `StarPairTests` runs the star rule alone on the frames with and without stars (ADR 0054: every starless frame under `StarMinCount`, every frame with stars kept, the Rosetta pair's kept stars where the two frames differ); both carry the trait `Category=Validation`, kept out of the gate by `kit.json`'s filter; `TestData.Large` finds the git-ignored copies in `testdata/large/` and a test whose frame is absent prints `NOT MEASURED` and returns |

The scenes were built for a paper black of L\* 9.0, the baryta of Dmax 2.0 from before ADR 0028
(`Scenes.BuiltForDmax`): a sky at 0.08 is under that black and above today's (2.4, L\* 3.6). A
test of "the background under paper black" therefore gives that paper itself, through
`Scenes.OnBuiltForPaper` or `--dmax 2.0` (the golden scenarios `dark-sky` and `dark-sky-wcs`),
and does not depend on the default medium; `mono-png8` and `clean-sky-canvas` run on a medium's
own Dmax.

```bash
dotnet test AstroPhotoAnalyzer.sln
```

## Requirement IDs in test names

Since 2026-10-02 a test that pins a row of `../requirements.md` carries its ID as a prefix:
`R002_TheExitCodeFollowsTheVerdict`. The first batch, 82 tests for 28 rows, was renamed in the
migration (ADR 0001): the console's, the options', the recommendations', the parameters', the
plumbing tests and the window's. The analyzer tests keep their names until their area is next
touched; their rows stay `accepted` until then. A test may carry one ID only; a test that pins two
rows carries the first, and the second row waits for a test of its own.

How a validation case runs, and how its figures are recorded, is in `../validation.md`, "How a
case runs". The gate never runs it: `scripts/verify.ps1` passes `kit.json`'s `testFilter`,
`Category!=Validation`; `/validate` runs it.

## The curves in the tests (ADR 0032)

`CurveTests` checks the spline against values computed with scipy's natural cubic spline on
the owner's earlier curve, the anchors and the recipe on the band scene of `WcsFixtures` at
0.7 of its values (its peak patch is at white, where no control point fits above it) and on a
striped frame without a WCS, and the saved FITS against hand-built FITS and PNG files.
`CurvesTabTests` drives the tab's view-model through `Compute(w, h)`, the synchronous body of
`Calculeaza`. `ValidationTests` has a third kind of case, `curves`: an analysis, a curve run
for print (with the parameter `icc`, a profile beside the image, for the gamut figure) and a
save of the last suggestion into a temporary folder, with the input hashed before and after.

## The soft proof in the tests (ADR 0066)

`SoftProofTests` runs the chain on the hand-built printer profile of `IccPrinterTestData`, which
since this feature carries the tables towards the printer (B2A0, B2A1, B2A2, the inverses of the
neutral model on L\* alone, and a `white` for a tinted media white; `withB2A: false` and
`withPerceptualB2A: false` make the refused profiles): a grey ramp comes back to itself, the
paper's white and black appear under the simulation, the third intent crushes the tones under
the black, the perceptual table lands elsewhere than the colorimetric one, the display step on a
`ProofProfile.ForTests`, the problems. `SoftProofTabTests` drives
the tab through `Compute()`, the synchronous body, and the async paths through
`ProofComputation`; `WindowSmokeTests.BindingErrors` is internal so that it can watch the tab's
and the window's renders there too; `ZoomPanViewerTests` lays the viewer out over a sized child
and drives `ZoomAt`, `PanBy` and `Reset` by hand. `ValidationTests` has a fourth kind of case, `proof` (V-015).
