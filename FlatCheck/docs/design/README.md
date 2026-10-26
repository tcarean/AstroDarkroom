# Design notes

One note per area whose code does not explain itself. Each says what the code does, why the
obvious simpler version is wrong, and what was measured. CLAUDE.md points here under "Read
before touching"; nothing in this folder is loaded into a session unless the task needs it.

Most of these notes were CLAUDE.md itself until 2026-09-25 (ADR 0001): 2709 lines that loaded
into every session. They were moved here unchanged, and on 2026-09-27 cut to what neither
CLAUDE.md nor a decision record says (ADR 0051): where an ADR was written from a section, the
section is now a pointer to it. The prose still says "see X below" where X is now a sibling
file, and a heading it names may have been renamed since; this table maps the areas. The four
evidence files that already lived in `docs/` moved into this folder with them. **Every note is
maintained in one precise sense**: it is the evidence behind a decision still in force, it says
at the top which code it covers, and a change to that code means the note is updated in the
same task, or deleted with the decision it explains. A note left standing next to code it no
longer describes is worse than no note.

| Area | Note | Read before editing |
| --- | --- | --- |
| Any file: what it is for and why it is shaped that way | `code-map.md` | any file you have not opened before; it is long, search it for the file name |
| The core idea, input contract, mode dispatch, shared algorithms (`Prepare`, `FitCenter`, `RadialModel`, `LargeScalePtp`, `MaxTileDeviation`, `LocalSpots`, verdict ladder) | `pipeline.md` | `Img.cs`, `Stats.cs`, `Metrics.cs`, anything in `Analysis/` |
| The radial model on partial rings: the even 2θ + 4θ correction, every design measured and rejected | `radial-model-even-term.md` | `Metrics.RadialModel`, `Metrics.RingFit`, `radial_model` in `flatcheck.py` |
| Mode 1, structure: the five deviation tests, the local spot, the sixth test against the lens profiles | `modes-structure.md` | `StructureAnalyzer`, `VignettingComparison`, `StructureReport`, `StructureRun`, `LensIdentification` |
| Mode 3, `--light`: the plane/radial fit, the validity limit (field of view), series mode | `modes-light.md` | `LightAnalyzer`, `LightSuitability`, `LightSeriesAnalyzer`, `LightRun`, `LightSeriesRun`, `SeriesFinder` |
| Mode 4, `--expo`: the central window, the CFA order (`BAYERPAT`, `ROWORDER`, libraw's index-3 trap), the histograms, the camera preview | `modes-expo.md` | `ExposureAnalyzer`, `ExposureBands`, `CfaPattern`, `Mosaic`, `RawReader`, `PreviewReader`, `ExposureReport` |
| Mode 5, `--histeq`: what Siril's Histogram display does and how it is mirrored | `modes-histeq.md`, `siril-histeq-source.md` | `HistEqAnalyzer`, `HistEqLut`, `HistEqReport`, `HistEqRun` |
| Mode 6, `--smooth`: the method, the two rejected designs, the clip floor, the scale advisor | `modes-smooth.md`, `smooth-validation.md` | `SmoothAnalyzer`, `SmoothScaleAdvisor`, `SmoothReport`, `SmoothRun`, `FitsWriter`, `Img.TileMedians`, `Mosaic.Merge` |
| Mode 7, `--synthetic` / `--lenses` / `--against`: the two profile conventions, the catalog rules, lens identification, the depth verdict | `modes-synthetic.md`, `lens-profiles.md` | `LensProfiles/`, `SyntheticFlatBuilder`, `SyntheticReport`, `SyntheticRun`, `AgainstRun`, the Sintetic tab |
| What was learned about flat sources on the real sessions (sky, monitor, tablet, paper) | `flat-sources.md` | the README's advice on shooting flats; any threshold on vignetting depth |
| The interface language: `--lang`, the `T(english, romanian)` pairs, verdicts as keys, the glossary, the English goldens | `language.md` | `Language/`, `VerdictWords.cs`, any user-visible text, `EnglishOutputTests` |
| The thresholds, the residual scale, why each test reads a defect differently, the exit codes | `thresholds.md` | `Thresholds.cs`, `Stats.Verdict`, `FlatCheckCli.ExitFor`, any wording of a verdict |
| The residual map: colour scale, fixed caps, why files stay on the automatic scale | `png-output.md` | `PngWriter`, `MapScaleViewModel`, `RunReport.MapLegend` |
| The two applications: the console/window comparison, the tabs' mechanics; pointers to ADR 0033 to 0037 for the session folder rules, the Siril run button and the WPF lessons (the Ratio tab of ADR 0033 is gone, ADR 0067); the Split tab's mechanics (ADR 0054); the private settings copy every siril-cli run gets (ADR 0055) | `two-applications.md` | anything in `FlatCheck.Wpf/` or `FlatCheck/`, `RunOutcome.cs`, `SplitRun`, `SplitReport`, `SplitPlan` |
| The dependencies: pointers to ADR 0008 (the rule), 0009 (libraw) and 0010 (ImageSharp), plus the one lazy-loading mechanic the ADR does not state | `dependencies.md` | `FlatCheck.Core.csproj`, `Directory.Packages.props`, `RawReader.cs`, `PreviewReader.cs`, `ThirdParty.cs` |
| The frozen Python prototype: what the cross-check was, its last result, the 43 parity goldens that replaced it (39 since ADR 0067), the intentional divergences | `python-parity.md` | any formula, `ParityGoldenTests`, `ParityFixtures`, `Golden/parity_*.txt`, `flatcheck.py`, `crosscheck.py` |
| Known issues and gotchas, deliberate or not worth changing | `known-issues.md` | before "fixing" a CRC race, a card parser, a corner floor |
| Conventions (the README rule), versioning and licensing (pointers to ADR 0039, 0040, 0042 and the mechanics they do not spell out), build and publish (pointers to CLAUDE.md, `kit.json`, `README.md`); extending the console (the `Options` contract, the dispatch order, `WriteUsage`, how a golden file and a parity golden scenario are added, the `VersionTests` mode lists, the README "Options" section, `InvariantGlobalization`) | `process.md` | `Directory.Build.props`, `Version.cs`, `LicenseHeaderTests`, `VersionTests`, `THIRD-PARTY-NOTICES.md`; `Options.cs`, `FlatCheckCli.cs`, `GoldenOutputTests`, `ParityGoldenTests`, `ParityFixtures` |

The decisions themselves, with the alternatives rejected, are in `../decisions/`. The
behaviour, with IDs, is in `../requirements.md`. The figures measured on the real masters are in
`../validation.md`.
