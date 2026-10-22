# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- FITS files (`.fit`, `.fits`, `.fts`) open as image A or B, next to or against a PNG or a
  TIFF: 8, 16 and 32-bit integers, 32 and 64-bit floats, one plane (gray) or three (RGB). The
  values are shown as stored, with no stretch, at the level the same value has in a 16-bit
  PNG; rows follow `ROWORDER`, bottom-up without it, as in Siril; a float file that counts to
  65 535 instead of 1 is recognised and the status bar says so; a damaged header or an image
  too large to show is a message (R-014 to R-019, ADR 0029).
- The application has its own icon, in the window's caption, the taskbar and Explorer: a dim
  star field and a brighter one either side of the turquoise divider (R-064, ADR 0030).

### Fixed

- `LICENSE` now sits next to the program, in the build folder and in the publish folder, where
  the About dialog says the full licence text is (R-074, ADR 0027).

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0007): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and `<Version>` and `<Copyright>` moved from the
  Wpf csproj to `Directory.Build.props`, where the kit's release script reads them. `CLAUDE.md`
  was split into `docs/requirements.md` (45 rows with IDs), `docs/decisions/` (26 ADRs, 19 of
  them retroactive) and `docs/design/` (6 notes, moved unchanged); `CLAUDE.md` holds only the
  project's rules. `RepoInvariantTests` ties `implemented` rows to tests named `R###_...` (102
  tests renamed for 31 rows), and `ValidationTests` checks two cases on a real 23 MP pair outside
  the gate. The comments of three files turned English. No behaviour changed; one
  `dotnet format` pass over 4 files.
- `Directory.Build.props` declares the `win-x64` runtime the release publishes for, on every
  project but the test project, so a release's publish no longer rewrites the lock files and
  stops the release on them (ADR 0028).
- Every type and method in `src/` has an XML documentation comment, with a tag for each
  parameter and for the return, and every coordinate's pixel space named (the kit's rule
  "Documentation comments", the kit's ADR 0035); six comments already there that did not match
  the code were corrected. `Directory.Build.props` no longer mutes CS1573, so a comment that
  tags only some parameters fails the build (the kit's ADR 0035, recorded on ADR 0003). No code
  changed.

## [1.1.1] - 2026-09-09

The state of the tool when the changelog was started, built on 2026-09-08 and 2026-09-09 in
ten commits:

- 1.0.0 (2026-09-08): the slider over two identically sized images, one shared transform, zoom
  anchored at the cursor with a ladder of stops, the divider with its handle and arrow-key
  nudges, the minimap, floating scrollbars, the pixel readout from both images, WIC decoding of
  any PNG or TIFF with the source format reported, the memory readout and the release of the
  outgoing pair; the GPL notice on every source file, the version and copyright in the status
  bar, the About dialog.
- 1.1.0 (2026-09-09): a pair of different pixel dimensions is compared on the larger canvas,
  both anchored top-left, after the user agrees; nothing is decoded before the answer.
- 1.1.1 (2026-09-09): dark title bars through DWM on every window, and the application's own
  message dialog in place of `MessageBox`.

No earlier version was released; the ten commits are in `git log`.
