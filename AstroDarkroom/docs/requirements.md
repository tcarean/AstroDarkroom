# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status `dropped`.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`.
- Describe observable behaviour, with one concrete example. No adjectives.
- Tests carry the ID in their name: `R001_Catalogue_ListsTheFiveAppsInWorkflowOrder`.
- Error behaviour is a requirement too.

The suite folder is the folder the launcher runs from (its own base directory): the flat folder
`scripts/release.ps1` publishes, with every component's executables beside `AstroDarkroom.exe`.

| ID | Status | Requirement |
| --- | --- | --- |
| R-001 | implemented | `AppCatalogue.Apps` lists the five apps in workflow order, each with its folder name, a Romanian title, a one-line Romanian description and the file name of its window exe: DarksCheck, "DarksCheck", "Verifica dark-urile unei sesiuni sau ale unei biblioteci", `DarksCheck.Wpf.exe`; FlatCheck, "FlatCheck", "Verifica flat-urile unui obiectiv", `FlatCheck.Wpf.exe`; LightsCheck, "LightsCheck", "Verifica light-urile unei sesiuni: focus, trailing, dithering, drift", `LightsCheck.Wpf.exe`; ImageVisualCompare, "ImageVisualCompare", "Compara doua variante ale aceleiasi procesari, pixel cu pixel", `ImageVisualCompare.Wpf.exe`; AstroPhotoAnalyzer, "AstroPhotoAnalyzer", "Verifica o imagine finala inainte de print", `AstroPhotoAnalyzer.Wpf.exe`. The suite ships the windows only; the consoles stay in each component's own release (ADR 0002). The exe names are those the components' project files build (`SuiteTests`). |
| R-002 | implemented | `SuiteFolder.Locate(app)` says an app is available when a file named like its window exe exists in the suite folder, and gives its full path; otherwise the app is unavailable and the reason is `Lipseste <window exe>`. Example: a suite folder with `DarksCheck.Wpf.exe` and nothing else: DarksCheck available at `<folder>\DarksCheck.Wpf.exe`, FlatCheck unavailable with the reason `Lipseste FlatCheck.Wpf.exe`. A console exe of a component's own release, such as `flatcheck.exe`, plays no part. |
| R-003 | implemented | `Launcher.Plan(app, folder)` gives, for an available app, the file to start (the window exe's full path) and the working directory (the suite folder); for an unavailable app it throws `InvalidOperationException` carrying the reason of R-002. The core only plans the start; the window executes it with `Process.Start`. |
| R-004 | implemented | `ReleaseManifest.TryRead(path)` reads the `release.json` `scripts/release.ps1` writes: the suite's name, version, date and commit, and each component's name, version, commit, `unreleased` and `clean`. Example: a file with version `0.1.0` and one component `FlatCheck` at `2.3.0` with `unreleased: true` gives those values. A missing file, a file that is not JSON, or one without a `components` array gives null, and a missing or mistyped key in a component gives `""` or `false` for that key; the launcher runs the same with or without the file, showing the versions as unknown. |
| R-005 | implemented | Every source file (`.cs`, `.xaml`, `.py`) under the code, test and `Icon/` folders starts with the GPL notice: the SPDX line, the copyright line and the three standard paragraphs, as `LicenseHeaderTests` spells them; a `.py` keeps its shebang and a `.xaml` its XML declaration on the first line. `LICENSE` is the full GPL v3 text. Example: `src/AstroDarkroom.Core/Version.cs` begins with `// SPDX-License-Identifier: GPL-3.0-or-later`. |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
