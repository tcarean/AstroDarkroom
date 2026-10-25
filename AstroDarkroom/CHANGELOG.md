# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`
and is the version of the whole Astro Darkroom suite (ADR 0002).

## [Unreleased]

### Added

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
- The project, started from `ClaudeCodeStarterKit` on 2026-10-25 as the launcher and the
  release of the Astro Darkroom suite (ADR 0001).
