# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`.

## [Unreleased]

### Added

- A diagnostic log beside each executable, `log\copywcs.log` and `log\CopyWcs.Wpf.log`, five
  files of 1 MiB at most, with each copy's options, the headers read, every card copied and the
  card of the destination it replaced, the file written, the time of each stage and every error
  with its technical details (ADR 0009); attach the `log` folder to a bug report;
  `COPYWCS_LOG` sets the level (`off` to `trace`).
- The About window lists the third-party components (NLog), and the About button's tooltip
  says so.
- The window, `CopyWcs.Wpf.exe` (ADR 0007): dark, one page, the source and the destination
  boxes (filled from the command line when two existing files are given), in place or a new
  file, the two options, Preview and Copy showing exactly what the console's dry run and copy
  do, the header facts under each box, an About window with the GPLv3 notice, English or
  Romanian by `--lang` as the console. At the top, FlatCheck's band: the version, the
  copyright, one sentence on what the tool does and the About button (ADR 0008). The window
  and the console share one icon, `Icon/copywcs.ico`.
- `--lang en|ro`: the console speaks English or Romanian, chosen on the command line, English
  when the option is absent (ADR 0004); `--lang` anywhere, `--lang=ro` too, a value that is
  not `en` or `ro` refused with exit 1.
- `README.md`, the user documentation, in English (ADR 0006).
- One validation case, V-001: the keys the tool selects from the header of a real Siril 1.4.4
  plate solve of the Veil session of 2026-10-06, kept as a fixture in the repo (ADR 0005).

### Changed

- The texts are English by default; the Romanian wordings are unchanged under `--lang ro`,
  the usage gaining its `--lang` line.
- The `HISTORY` card written into the destination reads `WCS copied from <file>` in both
  languages, where it read `WCS copiat din <file>` (ADR 0004).

### Internal

- NLog 6.2.2 and NLog.Targets.AtomicFile 6.2.2, the tool's first package, at FlatCheck's
  versions (the Astro Darkroom suite's folder holds one of each); `Logging/` adapted from
  FlatCheck's, and every test runs its log lines into a capture.
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
