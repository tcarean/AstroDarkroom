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
> `AstroPhotoAnalyzer.Tests/RepoInvariantTests.cs`, `AstroPhotoAnalyzer.Tests/ValidationTests.cs`
> and `AstroPhotoAnalyzer.Tests/TestData.cs`. A change to the code this describes
> means this note is updated in the same task, or deleted with the decision it explains.

## Testing

| kind | where | what |
|---|---|---|
| synthetic, known answer | `AstroPhotoAnalyzer.Tests/*AnalyzerTests.cs` | Gaussian stars of known FWHM, injected noise sigma, ramps, casts, corners, upscales; each analyzer recovers the truth within a stated tolerance |
| fixtures | `SyntheticRasters`, `Scenes` (DarkSky with every defect of the reference image, CleanSky, MonoSky), `WcsFixtures` (a synthetic TAN WCS, and the LDN 42 scene: the cloud's disc dark on a flat sky at 9"/px, narrow so that the Pipe's other clouds stay out of the frame), `FitsTestData`, `TiffTestData` (hand-built 16-bit RGB TIFF with ICC), `PngTestData` (hand-built 16-bit PNG with iCCP, own CRC-32), `IccTestData` (v2/v4 profiles), `ImageFiles` (ImageSharp-encoded 8-bit files), `TempWorkspace` | the 16-bit TIFF/PNG fixtures are hand-built because ImageSharp 3.1 cannot encode them |
| golden | `GoldenOutputTests`, `Golden/*.txt` | the console's whole output on the scenes (one of them, `dark-sky-wcs`, a float FITS with a WCS centred on LDN 42), version banner and temp path scrubbed; `APA_UPDATE_GOLDEN=1` re-approves |
| console | `CliTests`, `OptionsTests` | exit codes, `-o`, parsing, parameter mapping |
| recommendations | `RecommendationsTests` | every flag the reference image raised, on the dark scene |
| plumbing | `LicenseHeaderTests` (every source file starts with the notice; explicit project list), `VersionTests` (version/copyright from props, banner on line 1), `ThirdPartyTests` (csproj <-> `ThirdParty.Components` <-> notices headings) | |
| window | `Wpf.Tests/WindowSmokeTests` (renders the window's *content*, About, every tab with a real result, the map and preview windows, the error card; fails on any binding error; `APA_DUMP_SCREENS=<dir>` saves PNGs), `CultureTests` (report identical under ro-RO), `ParameterFieldsTests`, `ViewModelTests`, `OverlayWindowTests` (the Tonuri tab's definitions and buttons, the map and preview view-models with and without a WCS) | one STA thread with a message loop per process (`StaFixture`) |
| invariants | `RepoInvariantTests` | every `implemented` row of `docs/requirements.md` has a test named `R###_...` and every such test names a real row, read from the sources of both test projects; unique IDs; the design index exists; every validation case names a manifest file; `CLAUDE.md` at most 200 lines |
| validation | `ValidationTests`, `TestData` | the two reference images (the PNG, V-001 to V-003 and V-005; the plate-solved FITS, V-004) against `docs/validation.json`; trait `Category=Validation`, kept out of the gate by `kit.json`'s filter; `TestData.Large` finds the git-ignored copies in `testdata/large/` |

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
