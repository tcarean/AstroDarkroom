# Design notes

One note per area whose code does not explain itself. Each says what the code does, why the
obvious simpler version is wrong, and what was measured. CLAUDE.md points here under "Read
before touching"; nothing in this folder is loaded into a session unless the task needs it.

Most of these notes were CLAUDE.md itself until 2026-09-25 (ADR 0001): 2709 lines that loaded
into every session. They were moved here unchanged, so the prose still says "see X below" where
X is now a sibling file; this table maps the areas. The four evidence files that already lived
in `docs/` moved into this folder with them. **Every note is maintained in one precise sense**:
it is the evidence behind a decision still in force, it says at the top which code it covers,
and a change to that code means the note is updated in the same task, or deleted with the
decision it explains. A note left standing next to code it no longer describes is worse than
no note.

| Area | Note | Read before editing |
| --- | --- | --- |
| Any file: what it is for and why it is shaped that way | `code-map.md` | any file you have not opened before; it is long, search it for the file name |
| The core idea, input contract, mode dispatch, shared algorithms (`Prepare`, `FitCenter`, `RadialModel`, `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots`, verdict ladder) | `pipeline.md` | `Img.cs`, `Stats.cs`, `Metrics.cs`, anything in `Analysis/` |
| The radial model on partial rings: the even 2θ + 4θ correction, every design measured and rejected | `radial-model-even-term.md` | `Metrics.RadialModel`, `Metrics.RingFit`, `radial_model` in `flatcheck.py` |
| Mode 1, structure: the five deviation tests, the local spot, the sixth test against the lens profiles | `modes-structure.md` | `StructureAnalyzer`, `VignettingComparison`, `StructureReport`, `StructureRun`, `LensIdentification` |
| Mode 2, `--ratio`: why the old verdict was wrong, what `(A+B)/2` and `A/B` mean, the derived error bar | `modes-ratio.md` | `RatioAnalyzer`, `RatioReport`, `RatioRun` |
| Mode 3, `--light`: the plane/radial fit, the validity limit (field of view), series mode | `modes-light.md` | `LightAnalyzer`, `LightSuitability`, `LightSeriesAnalyzer`, `LightRun`, `LightSeriesRun`, `SeriesFinder` |
| Mode 4, `--expo`: the central window, the CFA order (`BAYERPAT`, `ROWORDER`, libraw's index-3 trap), the histograms, the camera preview | `modes-expo.md` | `ExposureAnalyzer`, `ExposureBands`, `CfaPattern`, `Mosaic`, `RawReader`, `PreviewReader`, `ExposureReport` |
| Mode 5, `--histeq`: what Siril's Histogram display does and how it is mirrored | `modes-histeq.md`, `siril-histeq-source.md` | `HistEqAnalyzer`, `HistEqLut`, `HistEqReport`, `HistEqRun` |
| Mode 6, `--smooth`: the method, the two rejected designs, the clip floor, the scale advisor | `modes-smooth.md`, `smooth-validation.md` | `SmoothAnalyzer`, `SmoothScaleAdvisor`, `SmoothReport`, `SmoothRun`, `FitsWriter`, `Img.TileMedians`, `Mosaic.Merge` |
| Mode 7, `--synthetic` / `--lenses` / `--against`: the two profile conventions, the catalog rules, lens identification, the depth verdict | `modes-synthetic.md`, `lens-profiles.md` | `LensProfiles/`, `SyntheticFlatBuilder`, `SyntheticReport`, `SyntheticRun`, `AgainstRun`, the Sintetic tab |
| What was learned about flat sources on the real sessions (sky, monitor, tablet, paper) | `flat-sources.md` | the README's advice on shooting flats; any threshold on vignetting depth |
| The thresholds, the residual scale, why each test reads a defect differently, the exit codes | `thresholds.md` | `Thresholds.cs`, `Stats.Verdict`, `FlatCheckCli.ExitFor`, any wording of a verdict |
| The residual map: colour scale, fixed caps, why files stay on the automatic scale | `png-output.md` | `PngWriter`, `MapScaleViewModel`, `RunReport.MapLegend` |
| The two applications: what the window adds and what it must not, session folder rules, the Siril run button, the WPF lessons | `two-applications.md` | anything in `FlatCheck.Wpf/` or `FlatCheck/`, `RunOutcome.cs` |
| The dependencies: the rule, libraw (LGPL choice, lazy loading), ImageSharp (Split License, 3.1.x pin) | `dependencies.md` | `FlatCheck.Core.csproj`, `Directory.Packages.props`, `RawReader.cs`, `PreviewReader.cs`, `ThirdParty.cs` |
| Parity with `flatcheck.py`: the cross-check, the intentional divergences | `python-parity.md` | any formula, `flatcheck.py`, `crosscheck.py`, `CrossCheckFixtures` |
| Known issues and gotchas, deliberate or not worth changing | `known-issues.md` | before "fixing" a CRC race, a card parser, a corner floor |
| Conventions, versioning, licensing, build, publish | `process.md` | `Directory.Build.props`, `Version.cs`, `LicenseHeaderTests`, `VersionTests`, `THIRD-PARTY-NOTICES.md` |

The decisions themselves, with the alternatives rejected, are in `../decisions/`. The
behaviour, with IDs, is in `../requirements.md`. The figures measured on the real masters are in
`../validation.md`.
