# The copy run, the console and the language

**Covers:** `src/CopyWcs.Core/WcsCopy.cs`, `src/CopyWcs.Core/Language/Ui.cs`,
`src/CopyWcs.Core/Language/LanguageArgument.cs`, `src/CopyWcs.Core/VersionInfo.cs`,
`src/CopyWcs.Core/LicenseInfo.cs`, `src/CopyWcs.Core/ThirdParty.cs`, `src/CopyWcs.Cli/Program.cs`,
`src/CopyWcs.Cli/CopyWcsCli.cs` and `src/CopyWcs.Cli/Options.cs`.

How a copy runs in the core, how the console drives it and prints it, and how the texts pick
their language. The header engine the run calls is in `fits-header.md`; the window, which
calls the same run and shows the same outcome, is in `window.md`.

## The run (`WcsCopy`)

`WcsCopy.Run(WcsCopyRequest)` is the one sequence both applications call (ADR 0001). The
request is the six things the console's options say: source, destination, output or in
place, optical keys or not, force, dry run. The outcome is a record of lines already worded:

| Field | What it holds | Who prints it |
| --- | --- | --- |
| `Kind` | `Copied`, `Previewed` or `Refused` | decides the exit code: 0, 0, 1 |
| `Error` | the one line of a refusal, else null | stderr |
| `Warnings` | the `WARNING:` lines, in order; forced mismatch, missing `CTYPE1` | stdout, before anything else |
| `Cards` | the raw 80-character cards selected, in the source's order | the preview, indented, trailing spaces trimmed (a 0xA0 stays); a byte above 0x7F shows as the character of its Latin-1 code, which the log alone escapes (ADR 0010) |
| `Target` | the file written, else null | nothing; the window reads the destination's facts again after a copy |
| `Notes` | the preview's heading, or the count copied and the flip reminder | stdout: a preview's heading first, above its cards; a copy's lines last |
| `Dropped` | the one line naming the destination's old plate-solve keys dropped, or a preview would drop (R-016), else null | stdout: a preview's after its cards, a copy's before its notes, so the count and the reminder stay the last two lines (R-012) |

The sequence: read the source, read the destination (each read failure is a refusal naming
which file and why, R-004; the reader's own messages of R-015 travel in it); compare
`NAXIS1`/`NAXIS2` (a refusal, or a warning under force, R-005); warn on a missing `CTYPE1`
(R-006); when both files have one, compare their plate solves' `WcsMetadataKeys.Orientation`
(opposite signs are a refusal, or a warning under force, R-017, ADR 0012; force passes both
checks); select the keys (R-007; none is a refusal, R-008) and, when the source has `CTYPE1`,
the destination's keys to drop (`WcsMetadataKeys.SelectKeysToDrop`, R-016, ADR 0011); on a dry
run return the cards, the heading and the drop line (R-009); else remove the dropped keys' cards,
set each selected card verbatim, append the HISTORY card, write through the header engine (a
write failure is a refusal, R-014) and return the count, the reminder and the drop line (R-012).
The keys dropped and the keys set never overlap: a dropped key is one the source lacks. A
refusal drops nothing and carries no drop line, the write error's included, since the header in
memory is thrown away. The warnings raised before a refusal stay in the outcome, so the console
prints them before the error, as it did before the split. The core composes every sentence
about the copy; an application adds nothing to them. Beside the outcome, the run writes its own lines
to the diagnostic log, in English (`logging.md`, "A copy's lines").

## The console (`CopyWcsCli`, `Options`, `Program`)

`Program.Main` is a shim: it opens the diagnostic log, calls `CopyWcsCli.Run(args, stdout,
stderr)` once, which the tests drive with string writers, and closes the log (`logging.md`). `Run` prints the banner and the copyright first (R-001), then takes
`--lang` out of the arguments (below), then parses `Options`: two positional paths, the flags
in any position, `-h` stopping the parse at once; a parse error is one line on stderr, the
usage on stdout and exit 2 (R-002); `--help` is the usage and exit 0 (R-003). The usage is
written whole per language because its columns are aligned by hand; the warranty paragraph
of GPLv3 section 5(d) follows it, there and not under the banner so that it does not weigh on
every run. Then the run, and the printing of the outcome as the table above says. Exit codes:
0 success or preview, 1 refused or a bad `--lang`, 2 usage.

## The language (`Ui`, `LanguageArgument`)

FlatCheck's mechanism (ADR 0004): `Ui.T(en, ro)` returns the wording of `Ui.Current`, which is
the innermost open `Ui.Use(language)` scope on the current flow (an `AsyncLocal`, so parallel
tests each keep their own) or `Ui.Default`, English at start-up. `LanguageArgument.Extract`
removes every `--lang x` and `--lang=x` from the arguments, case-insensitive, the last one
counting, and returns the language, a bad value if any, and the rest; the console refuses a
bad value in English with exit 1 (R-022) and runs the rest inside `Ui.Use(chosen)` when one
was given (R-020, R-021). The test assembly sets `Ui.Default` to Romanian once
(`RomanianByDefault`), so the tests written before the English interface assert the Romanian
wordings unchanged, and `LanguageTests` opens English scopes or passes `--lang en`.

## The banner, the copyright and the warranty (`VersionInfo`, `LicenseInfo`)

`<Version>` and `<Copyright>` in `Directory.Build.props` (ADR 0003) become
`AssemblyInformationalVersion` and `AssemblyCopyright`; `VersionInfo.Value` reads the first,
cuts any `+commit` suffix and yields `1.0.0`, and `Banner` is `copywcs ` plus it;
`LicenseInfo.Copyright` reads the second, with a literal fallback for an assembly built
without the attribute. `VersionTests` and `LicenseNoticeTests` bind the file and the
assembly, and check lines 1 and 2 of every run, failed runs included.

The warranty paragraph of GPLv3 section 5(d) is `LicenseInfo.WarrantyLines`: the five lines
of each language, wrapped by hand, picked by `Ui.Current`; `Options.PrintUsage` writes them
one per line under the usage, and `LicenseInfo.Warranty` joins them with a space for the
About window (ADR 0007), so the two applications cannot disagree about the licence.
`LicenseInfo.LicenseUrl` is the one place the licence's address is written.

`ThirdParty.Components` is the list of what the core links, as values (ADR 0009, DarksCheck's
shape): today one component, NLog, which claims the two packages `NLog` and
`NLog.Targets.AtomicFile`, with its role as a pair, its licence, its copyright line and its link.
The About window shows it; `THIRD-PARTY-NOTICES.md` beside the source holds the licence's whole
text. `ThirdPartyTests` binds the list to the `PackageReference`s of `CopyWcs.Core.csproj` in
both directions and each name to a heading of the notices file, so a package cannot be added
without its notice.
