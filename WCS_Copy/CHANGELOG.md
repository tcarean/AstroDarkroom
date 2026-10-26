# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`.

## [Unreleased]

### Internal

- The project adopted the ClaudeCodeStarterKit process (ADR 0001): the gate `scripts/verify.ps1`,
  the kit's skills, agent and rules, `kit.json`, the layout `src/` and `tests/`, the core library
  `CopyWcs.Core` under the console `CopyWcs.Cli`, the Python prototype under `prototype/`, the
  version and the copyright in `Directory.Build.props` (ADR 0003), the kit's analyzers and
  `.editorconfig` with the test packages at the kit's versions (ADR 0002).

## [1.0.0] - 2026-09-01

### Added

- The console `copywcs`: copies the WCS cards and the optical metadata of a Siril-solved FITS
  into another FITS of the same pixel grid, verbatim, with `--dry-run`, `--force`, `--no-optical`
  and `--output`; the version banner and the GPLv3 copyright on every run (2026-08-26 and
  2026-09-01, the three first commits).
