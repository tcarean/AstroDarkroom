# CopyWcs

Copies the plate solve (WCS) and the optical metadata from a Siril-solved FITS into another
FITS of the same pixel grid, by reading and writing the FITS header cards by hand: no FITS
library, no package. A console, `copywcs.exe`, over a core library; a WPF window over the same
core is the second task of `docs/plans/2026-10-26-adopt-the-kit-and-build-the-window.md`. It
copies each selected card's 80 bytes verbatim and the destination's data unchanged; it never
reads a pixel. It is for the owner's own stacks and exports.

## Commands

- Gate (what pre-commit and the Claude commit hook run):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build CopyWcs.sln -c Release --nologo`
- Test: `dotnet test CopyWcs.sln -c Release --nologo`
- One test: `dotnet test CopyWcs.sln --filter "FullyQualifiedName~R005"`
- Format: `dotnet format CopyWcs.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project src/CopyWcs.Cli -- <solved.fit> <destination.fit> [--dry-run] [--lang ro]`
- Test data hashes after an intentional fixture change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `src/CopyWcs.Core/` - the FITS header engine (`FitsHeader`), the key lists
  (`WcsMetadataKeys`), the copy run and its texts (`WcsCopy`), the language of the texts
  (`Language/`), the version and the copyright read from the assembly. No Console, no UI, no
  entry point: a compile boundary.
- `src/CopyWcs.Cli/` - the console, `copywcs.exe`: options, dispatch, exit codes, printing.
- `tests/CopyWcs.Tests/` - unit, acceptance on `CopyWcsCli.Run`, the language
  (`LanguageTests`), the one validation case (`ValidationTests`), repo rules
  (`RepoInvariantTests`, `LicenseHeaderTests`, `LicenseNoticeTests`, `VersionTests`). Every
  FITS the tests build comes from `FitsTestData`; `tests/testdata/fixtures/` holds the one
  real header, with the manifest.
- `prototype/copy_wcs.py` - the Python/astropy prototype the tool was ported from, frozen:
  prior art, not maintained, not run by the gate; it keeps its Romanian docstrings (ADR 0001).
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs.
  `docs/validation.md` + `.json` - the figures on the real header. `docs/design/` - the
  design notes, read on demand. `docs/plans/` - approved plans. `README.md` - the user
  documentation, English (ADR 0006).
- `kit.json` - what the kit's scripts, skills and agent need to know about CopyWcs.
  `docs/skills.md` - CopyWcs's additions to the kit's skills. `scripts/`, `.claude/skills/`,
  `.claude/agents/`, `.claude/rules/kit/` and the process keys of `.claude/settings.json` are
  the kit's, the same here (the kit's ADRs 0017, 0018 and 0028).
- The process rules are the kit's, in `.claude/rules/kit/process.md`; CopyWcs's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are CopyWcs's own; one that extends or
varies a kit rule names it.

- **Core has no UI.** `CopyWcs.Core` never references Console, WPF, a dialog or a `TextWriter`;
  `WcsCopy.Run` returns lines, the applications print or show them (ADR 0001).
- **A card is copied verbatim.** `dest.SetCard(key, source.GetCard(key)!)` copies the raw
  80-byte card, never a reparsed or reformatted value: the source's formatting and inline
  comment survive (R-010). Do not "clean up" this into a reparse.
- **`Main` is a one-line shim** to `CopyWcsCli.Run(args, stdout, stderr)`; new console
  behaviour goes in `CopyWcsCli` or `Options`, testable in process, without a subprocess.
- **Two interface languages, English everywhere else** (varies the kit's rule "Languages",
  ADR 0004). Every text the user sees is `T("english", "romana")` where it is composed, the
  Romanian without diacritics, never a single literal; the HISTORY card is English in both.
  Code, comments, docs, tests and commits are English; the Romanian wordings and the tests
  that quote them are the one exception. Never leave a file in two languages otherwise.
- **Every source file starts with the GPL notice**, `.py` too, above the file's own header
  comment; `LicenseHeaderTests` walks `src/`, `tests/` and `prototype/`. `LICENSE` is the
  verbatim GPL v3 text: do not reword or reflow it.
- **The banner and the copyright are lines 1 and 2 of every run** (R-001), in every mode, at
  `--help` and on a failed run, read from `Directory.Build.props` through the assembly (ADR
  0003); `--help` carries the warranty paragraph of GPLv3 section 5(d). There is no
  `--version` flag; `VersionTests` pins that it stays gone.

## Read before touching

`docs/design/README.md` maps the areas: `fits-header.md` before `FitsHeader` or the key lists,
`run-and-console.md` before the run, the console or the language.

## Style

- `.editorconfig` and the analyzers enforce style at build time; do not restate style here.
- Nullable enabled, warnings are errors, `AnalysisLevel` latest-recommended. Fix, do not
  suppress, unless the suppression carries a comment with the reason.
