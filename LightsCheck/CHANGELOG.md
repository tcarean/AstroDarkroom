# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The project, started from `ClaudeCodeStarterKit` on 2026-10-09 (ADR 0001): the kit's gate,
  skills, hooks and documentation set; the raw and FITS readers, the PNG encoder and the
  bitmap font copied from FlatCheck 2.3.0 (ADR 0003), the raw reader extended with the shot
  metadata (exposure, ISO, time of capture); libraw under LGPL-2.1 as the only package.
- The console `lightscheck <folder sesiune>` (R-001 to R-006, ADR 0004): opens a Siril session
  folder (`lights/` or the folder itself, `biases/` optional), reads the metadata of every
  light without decoding it, and prints the inventory: camera, lens, exposure, the interval
  with the integration and the duty, and one row per frame with its time, exposure, ISO,
  f-number, focal length and the pause before it. A frame that cannot be read is listed with
  its error and the run exits 1.
- `prototype/`: the Python scripts of the 2026-10-09 analysis of the Veil session, frozen, and
  their report and figures under `docs/validation/2026-10-06-veil/`; V-001 checks the inventory
  on that session.
