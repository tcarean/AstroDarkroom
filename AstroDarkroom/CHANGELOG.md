# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`
and is the version of the whole Astro Darkroom suite (ADR 0002).

## [Unreleased]

### Changed

- The READMEs say that FlatCheck reads Lightroom Classic's and Lightroom's own profile folders
  too (FlatCheck ADR 0066): the root README's Lightroom row says "yes" and names
  `C:\Program Files\Adobe\<app>\Resources\LensProfiles\1.0`.

### Added

- CopyWcs is the sixth tool of the suite (R-001, ADR 0010): its window, `CopyWcs.Wpf.exe`, is
  published into the suite folder from `../WCS_Copy`, and the launcher lists it between
  ImageVisualCompare and AstroPhotoAnalyzer and opens it in its own language, like the others.
- The launcher says what the suite needs (R-009, ADR 0007): one line under the title and a
  "Cerinte" / "Requirements" card in "Despre" with the Siril session folder, Siril 1.4.4 or
  newer, and, optional for FlatCheck, Adobe's lens profiles with the programs that install
  them; the same in `README.md`.
- The language buttons carry their flag, drawn as vectors (ADR 0007).
- The launcher speaks English or Romanian (R-006..R-008, ADR 0006): two buttons in the bottom
  bar switch every text at once, English at a first start, the choice remembered in
  `%LOCALAPPDATA%\AstroDarkroom\settings.json`; a tool opened from the launcher starts in the
  launcher's language (`--lang=ro` / `--lang=en`).
- The launcher, `AstroDarkroom.exe` (R-001..R-004): one row per tool of the suite, in workflow
  order, with its Romanian title and description, its version from `release.json` and a
  "Deschide" button, disabled with the missing file's name when the tool's window is not in
  the folder; "Despre" (F1) with the suite's version, the licence and every component's
  version. Both windows have a dark title bar, like the tools' (ADR 0004).
- The suite's release (ADR 0002): `kit.json` publishes the launcher and the windows of the five
  tools (DarksCheck, FlatCheck, LightsCheck, ImageVisualCompare, AstroPhotoAnalyzer)
  self-contained for win-x64 into one flat folder, `artifacts/<version>/`, with `release.json`
  and the zip `AstroDarkroom-<version>-win-x64.zip` and its `.sha256` in `artifacts/`. The
  consoles stay in the tools' own releases: their base runtime and the windows' desktop runtime
  carry three DLLs of the same name with different bytes. `SuiteTests` keeps the catalogue,
  `kit.json` and the components' frameworks and package versions in step.
- `README.md`, in English (ADR 0009), has a "Download" section: each version's zip is shared
  from the owner's Google Drive, with its link and SHA-256 in a table, and the command that
  checks the hash (ADR 0005); the repository's root `README.md`, its Bitbucket front page,
  repeats the latest row and says how to build the suite from a checkout (ADR 0008).
- The project, started from `ClaudeCodeStarterKit` on 2026-10-25 as the launcher and the
  release of the Astro Darkroom suite (ADR 0001).

### Changed

- The "unreleased changes" note is no longer shown, in the rows or in "Despre";
  `release.json` keeps its `unreleased` key (ADR 0007).
