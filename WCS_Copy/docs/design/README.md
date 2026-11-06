# Design notes

One note per area whose code does not explain itself: a numeric model, a file format parser, a
protocol. How that code works and how its parts fit together are written here and nowhere else
(the kit's rule "Design notes", the kit's ADR 0031). Any other file may name the code, as an
address, and say what a test or a check enforces; the mechanics live in the note of their area.
A note may name classes and methods. It holds the mechanics of the code it covers and the reasons
that have no other home; it repeats neither CLAUDE.md nor an ADR, and points at them instead.

CLAUDE.md points here under "Read before touching". Nothing in this folder is loaded into a
session unless the task needs it.

## The Covers sentence

Every note says, above its first section, which files it describes, in a sentence that starts
with **Covers:**, naming its files as paths in backticks; the rules of the sentence and of the
index are the kit's (`docs/design/README.md` in `ClaudeCodeStarterKit`), checked by
`scripts/check-design.ps1` as a step of `scripts/verify.ps1`. The note is updated or deleted in
the same task as the code it covers.

## The index

| Area | Note | Read before editing |
| --- | --- | --- |
| The FITS header engine: cards, blocks, the data offset, the in-place write, the bytes kept as they are; the ASCII of the HISTORY card and its table; the key lists and the SIP pattern; the destination's old plate-solve keys a copy drops; which way a plate solve faces | `fits-header.md` | `FitsHeader.cs`, `AsciiText.cs`, `WcsMetadataKeys.cs`, anything about what is copied or dropped, a mirrored destination, or how a file is written (R-007, R-010 to R-017) |
| The copy run and its outcome, the console's options, printing and exit codes, the interface language and `--lang`, the banner, the copyright, the warranty paragraph and the third-party list | `run-and-console.md` | `WcsCopy.cs`, anything in `src/CopyWcs.Core/Language/` or `src/CopyWcs.Cli/`, `VersionInfo.cs`, `LicenseInfo.cs`, `ThirdParty.cs`, any console option, message or exit code (R-001 to R-009, R-020 to R-023) |
| The window: the view-model's three things, the window's shape and its band at the top, the About window and its third-party card, the command line, the theme, the icon, the tests | `window.md` | anything in `src/CopyWcs.Wpf/` or `tests/CopyWcs.Wpf.Tests/`, `Icon/make_icon.py`, any behaviour of R-030 to R-038 |
| The diagnostic log: the configuration the two applications apply, the files as NLog rolls them (measured), the line, a copy's lines, what each seam logs, the tests' capture | `logging.md` | anything in `src/CopyWcs.Core/Logging/`, `Program.cs`, `App.xaml.cs`, any log line, `LogCapture`, `LoggingTests`, `WindowLoggingTests` (R-040 to R-046) |
