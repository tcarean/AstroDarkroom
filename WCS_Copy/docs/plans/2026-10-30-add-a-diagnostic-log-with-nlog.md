# Add a diagnostic log with NLog

Plan for the task branch `claude/Log-CopyWcs-with-NLog`, worktree
`.claude/worktrees/Log-CopyWcs-with-NLog`, made from `E:\Photografie\Code\WCS_Copy`. The
repository root is `E:\Photografie\Code`, so the project is `WCS_Copy\` inside the worktree
(`E:\Photografie\Code\WCS_Copy\.claude\worktrees\Log-CopyWcs-with-NLog\WCS_Copy`); a session
that implements it opens there, not at the worktree's root, which loads no `CLAUDE.md`. The
branch name follows the siblings' of the same day (`claude/Log-LightsCheck-with-NLog`,
`claude/Log-ImageVisualCompare-with-NLog`): `claude/Add-a-diagnostic-log-with-NLog` was
FlatCheck's and is named by DarksCheck's plan, and branch names are shared by the whole
repository. Written for an implementer
who has this file, `CLAUDE.md`, the repository and the FlatCheck checkout beside it
(`E:\Photografie\Code\FlatCheck`), and nothing else from the planning session. Approved by the
owner on 2026-10-30, with the answers to Q1 and Q2 below.

## Context

A bug report about CopyWcs carries only what was on the screen. A refusal is one line built from
an exception's message alone: `WcsCopy.Run` catches the reader's and the writer's exceptions and
keeps `ex.Message`, so the type, the stack, the inner exception and the Windows error code are
lost. The window's header facts swallow the reader's exception and show nothing
(`MainViewModel.Facts`), the About window swallows a link that would not open, and an exception
nobody catches ends the console with the runtime's dump and the window with no word. Nothing
records what was read (the sizes, the header's blocks, `NAXIS`, `ROWORDER`, the solve's `CTYPE`),
which cards were copied and which card of the destination each one replaced, what was written,
or how long the write took. The report a user is most likely to bring, "the WCS is wrong after
the copy", can only be answered from the cards that went in and the ones they replaced. Nothing
in the three projects logs today.

The owner's ask (2026-10-30, in Romanian): a log with NLog, as text, in `log/` beside the
executable, the file names starting with the executable's name, at most 5 files per executable,
rolling, at most 1 MB each, done the way FlatCheck does it, with one criterion: a bug report with
the log attached can be diagnosed from the log.

FlatCheck built exactly this on 2026-10-27 (its ADRs 0070 and 0072, its `docs/design/logging.md`,
`FlatCheck.Core/Logging/`, `FlatCheck.Tests/LogCapture.cs` and `LoggingTests.cs`) and measured what
mattered: NLog's plain file target lost lines between two processes (33 to 48 of 4000 closed
between lines, 67 to 72 kept open) where `NLog.Targets.AtomicFile` lost none at 0.09 ms a line;
NLog 6.2.2's `MaxArchiveFiles` counts the file being written; a logger held in a static field of
the type that configures NLog makes NLog load a stray `NLog.config` before the configuration is
set. LightsCheck and ImageVisualCompare took the same design on 2026-10-30 on their own branches.
This plan takes FlatCheck's measurements as measured and repeats the tests that pin them, on
CopyWcs's configuration.

Two facts of this project shape the plan:

- **CopyWcs has no package today.** `Directory.Packages.props` says "The tool itself has no
  package", `CLAUDE.md`'s first paragraph says "no package", and ADR 0007 and 0008 dropped the
  siblings' third-party card from the About window and "components" from its tooltip for that
  reason. NLog is the first package.
- **AstroDarkroom bundles every window into one flat folder** (its `kit.json`, `"output": ""` for
  each component, `CopyWcs.Wpf` among them) and refuses a file of other bytes at a path another
  component already published (the kit's ADR 0036, `scripts/merge-publish.ps1`). FlatCheck's
  `NLog.dll` and `NLog.Targets.AtomicFile.dll` are already in it
  (`E:\Photografie\Code\AstroDarkroom\artifacts\1.0.0\`). So CopyWcs takes FlatCheck's exact
  versions, and it ships no `THIRD-PARTY-NOTICES.md` beside its executable (in the shared folder
  one tool's notices would stand for the others' or collide with them).

## Decisions

D1 to D9 are FlatCheck's decisions (its ADRs 0070 and 0072), taken as they are. C1 to C6 are
CopyWcs's own. Q1 and Q2 were put to the owner on 2026-10-30, two options each with a
recommendation; the owner chose both recommendations. Approving the plan settles every row.

| # | Decision | Where it comes from |
| --- | --- | --- |
| D1 | **Where NLog lives.** The core references the two packages and logs at its seams; each application configures NLog **in code**, `LogSetup.Apply` first and `LogSetup.Close` last. No `NLog.config`. Without a configuration (a library use) every call writes nothing. | FlatCheck ADR 0070 |
| D2 | **The files.** `<executable>.log` in `log` beside the executable; NLog 6's own roll-forward past 1 MiB to `<executable>_01.log`, `_02`, ...; `MaxArchiveFiles = 5` keeps five files, the one being written included; the next process goes on in the newest file; UTF-8 without BOM. | the owner's ask; FlatCheck ADR 0070 |
| D3 | **Two processes on one file** (two windows, two consoles in a script): `AtomicFileTarget`, kept open, `ConcurrentWrites`. | FlatCheck ADR 0070, the owner's choice of 2026-10-27 |
| D4 | **The level.** Debug by default; an environment variable sets `off`, `fatal`, `error`, `warn`, `info`, `debug` or `trace`, in any case; any other value is Debug. No console option: the console's contract, `--help` and the tests of its output do not move. | FlatCheck ADR 0070 |
| D5 | **A folder that cannot be written.** Fall back under `%LOCALAPPDATA%` and say so in the first line. The probe creates the folder, a probe file deleted on close, and opens for append the file NLog will write next, with the share mode another process of the same executable leaves. | FlatCheck ADR 0070, 0072 |
| D6 | **The words.** English, invariant numbers, never through `T(english, romanian)`; an exception's message (under the line, with its stack) and a usage error's line appear as the user saw them, so in Romanian under `--lang ro`. | FlatCheck ADR 0070 |
| D7 | **Crashes.** `AppDomain.UnhandledException` and the window's `DispatcherUnhandledException` at Fatal, `TaskScheduler.UnobservedTaskException` at Error, flushed; the same exception seen by two hooks is written once; what a crash does is unchanged. | FlatCheck ADR 0070, 0072 |
| D8 | **The log never stops the tool.** `Apply` and `Close` run in a `try` that swallows anything; `LogSetup`'s and `App`'s loggers are properties fetched on use, never static fields; a run that throws leaves its stages through a disposable `RunClock`. | FlatCheck ADR 0072 |
| D9 | **The tests.** A capture at Trace configured once per test assembly by a module initializer (`LogCapture`), so every test runs every log line on its path; a test reads its own lines through an `AsyncLocal` scope; the file tests build the applications' configuration with `LogSetup.Build` into a private `LogFactory`. | FlatCheck |
| C1 | **Whose code `Logging/` is.** CopyWcs's own, adapted from FlatCheck's in this project's style (nullable enabled, file-scoped namespace, braces, implicit usings, `_camelCase` private fields), each file's comment saying "adapted from FlatCheck's"; the names, the constants and the method shapes stay FlatCheck's, so a fix found in one tool carries to the others. Not a copy under ADR 0007's rule for the window's copied pieces. | LightsCheck's plan, its L1 |
| C2 | **The versions.** `NLog` and `NLog.Targets.AtomicFile` **6.2.2**, equal to each other and to FlatCheck's, and they move with FlatCheck's: the suite's flat folder holds one `NLog.dll`. | the bundle (Context) |
| C3 | **The seams.** `FitsHeader.ReadPrimary`, `AddHistory` and `WriteWithDataFrom`; `WcsCopy.Run`; `CopyWcsCli`; `App`, `MainWindow`, `MainViewModel.Facts`, `AboutWindow.OpenLink`. `WcsMetadataKeys`, `Language/`, `VersionInfo`, `LicenseInfo` and `ThirdParty` hold no logger. The table under "The lines" is the specification. | this plan |
| C4 | **Every card copied, with the destination's card it replaces, at Debug** (the default level): one line per key, 72 for the Siril solve of the Veil fixture (V-001). It is the one record a wrong WCS can be diagnosed from; at about 110 bytes a line a copy writes some 9 KB, so the 5 MiB hold hundreds of runs. Trace adds every card of every header read and written. | this plan |
| C5 | **The variable and the fallback.** `COPYWCS_LOG`; `%LOCALAPPDATA%\CopyWcs\log`. | the siblings' naming |
| C6 | **Not in this task:** a `--log` option, an "open the log folder" button, zipping or sending the log. The README says where the log is and asks for the whole folder. | FlatCheck's out of scope |
| Q1 | **NLog's notice.** The siblings' third-party card comes back to the About window (DarksCheck's markup and `ThirdParty.cs` shape, FlatCheck's NLog entry: role, licence, copyright line, link), the About tooltip goes back to the siblings' `Version, licence, components (F1)`, and `THIRD-PARTY-NOTICES.md` holds NLog's whole licence in the repository; no notices file is shipped beside the executables. Rejected: the notices file and a README row only, About unchanged (the executables, alone or in the suite's folder, would carry no NLog notice, which BSD-3-Clause asks of a binary). | the owner, 2026-10-30 |
| Q2 | **`Main`.** It does what FlatCheck's does: `LogSetup.Apply` in a `try`, `CopyWcsCli.Run` once, `LogSetup.Close` in a `finally`, guarded the same way. `CLAUDE.md`'s rule "`Main` is a one-line shim" is amended to say so; its purpose stays (console behaviour lives in `CopyWcsCli` or `Options`, testable in process). Rejected: a one-line `Main` through a wrapper in the core, which only the console could use. | the owner, 2026-10-30 |

## Design

### The packages and the notice (C2, Q1)

- `Directory.Packages.props`: the header comment's last sentence becomes "The FITS header is read
  and written by hand (ADR 0001); the tool's one package is NLog, for the diagnostic log (ADR
  0009)." A new `ItemGroup` above the test packages:

  ```xml
  <!-- The diagnostic log beside each executable (R-040 to R-046, ADR 0009): configured in code,
       a size-rolled text file, BSD-3-Clause, purely managed. The owner asked for NLog by name on
       2026-10-30, the way FlatCheck has it (FlatCheck's ADR 0070). -->
  <PackageVersion Include="NLog" Version="6.2.2" />
  <!-- NLog's own file target with the operating system's atomic append, so two processes of one
       executable writing one log lose no line (R-045); FlatCheck measured NLog's plain target
       losing 33 to 48 lines of 4000 (its ADR 0070). Same project, commit and licence as NLog.
       Keep both versions equal to each other and to FlatCheck's: AstroDarkroom bundles every
       window into one folder, where an NLog.dll of other bytes is a collision that stops the
       suite's release (the kit's ADR 0036). -->
  <PackageVersion Include="NLog.Targets.AtomicFile" Version="6.2.2" />
  ```

- `src/CopyWcs.Core/CopyWcs.Core.csproj`: the header comment gains "the diagnostic log's
  configuration and its words (`Logging/`), the third-party list the About window shows"; a new
  `ItemGroup` with `<PackageReference Include="NLog" />` and
  `<PackageReference Include="NLog.Targets.AtomicFile" />`, under a comment: the core logs at its
  seams (`FitsHeader`, `WcsCopy`), the two applications configure NLog through
  `Logging/LogSetup.cs`, a library use logs into nothing, the tests configure a capture (ADR 0009).
  The console, the window and the two test projects get the packages transitively.
- Lock files: `dotnet restore <worktree>\WCS_Copy\CopyWcs.sln --force-evaluate` regenerates the
  five `packages.lock.json` (Core, Cli, Wpf, both test projects); they go into the same commit as
  the package. Both packages are in the local cache (`%USERPROFILE%\.nuget\packages\nlog\6.2.2`,
  `nlog.targets.atomicfile\6.2.2`). The window resolves AtomicFile's `net8.0-windows7.0`
  assembly, the console and `CopyWcs.Tests` its `net8.0` one; FlatCheck's two-writer test passes
  on `net8.0`.
- `src/CopyWcs.Core/ThirdParty.cs` (new): DarksCheck's `src/DarksCheck.Core/ThirdParty.cs` shape
  (the record `ThirdPartyComponent(string Name, string Role, string Licence, string Copyright,
  string Url, IReadOnlyList<string> Packages)` and `internal static class ThirdParty` with
  `Components`), namespace `CopyWcs`, `using static CopyWcs.Ui;`. Its comment says why the list
  is shown and not only shipped: `THIRD-PARTY-NOTICES.md` stays beside the source, and the
  executable, alone or in the suite's folder, carries the About window; a new package means an
  entry here and a heading in the notices file, which `ThirdPartyTests` checks. One component,
  FlatCheck's NLog entry with its comment (one component, two packages, the same commit
  `d9ebf82`):

  ```csharp
  new ThirdPartyComponent(
      Name: "NLog",
      Role: T("writing the diagnostic log beside the program (the log folder)",
              "scrierea jurnalului de diagnostic langa program (folderul log)"),
      Licence: "BSD-3-Clause",
      Copyright: "Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen",
      Url: "https://nlog-project.org/",
      Packages: ["NLog", "NLog.Targets.AtomicFile"]),
  ```

- `THIRD-PARTY-NOTICES.md` (new, at the project root, English like every file here but the
  Romanian wordings): a top paragraph (the components CopyWcs links, the licence each is taken
  under and why it is compatible with GPLv3; the About window shows each one's notice; this file
  stays beside the source), then FlatCheck's `## NLog` section (`FlatCheck/THIRD-PARTY-NOTICES.md`)
  adapted: Role, Licence, Compatibility, the licence text **copied byte for byte from FlatCheck's
  section, never retyped**, the package's second notice line, the alternatives, the two links. The
  references become CopyWcs's (R-045, ADR 0009; "the window's About shows the notice, the
  licence's name and its link"; drop FlatCheck's "as it does for the MIT wrapper (ADR 0040)").
- The About window (`src/CopyWcs.Wpf/ViewModels/AboutViewModel.cs`, `Views/AboutWindow.xaml`),
  after DarksCheck's (`src/DarksCheck.Wpf/ViewModels/AboutViewModel.cs`, `Views/AboutWindow.xaml`):
  - `AboutViewModel`: DarksCheck's `public sealed record ComponentRow(string Name, string Role,
    string Licence, string Copyright, string Url)`; a constructor filling
    `public IReadOnlyList<ComponentRow> Components { get; }` from `ThirdParty.Components`;
    `ThirdPartyHeading => T("THIRD-PARTY COMPONENTS", "COMPONENTE TERTE")`; `ThirdPartyLead =>
    T("The tool links one component written by others, compatible with GPLv3: NLog, which writes
    the diagnostic log. The FITS header is read and written by hand. The licence's full text, and
    why it is compatible, are in THIRD-PARTY-NOTICES.md, beside the source.", "Unealta leaga o
    componenta scrisa de altii, compatibila cu GPLv3: NLog, care scrie jurnalul de diagnostic.
    Header-ul FITS e citit si scris de mana. Textul complet al licentei si motivul pentru care e
    compatibila sunt in THIRD-PARTY-NOTICES.md, langa sursa.")`. The file comment's "without the
    third-party list: this project has no package (ADR 0007)" becomes "with the third-party list
    since ADR 0009".
  - `AboutWindow.xaml`: DarksCheck's second card under the licence card (`Heading`
    `ThirdPartyHeading`, `Muted` `ThirdPartyLead`, `ItemsControl` over `Components` with
    `ComponentRowTemplate`); the header comment loses "without its third-party card (ADR 0007)".
  - `Theme/Dark.xaml`: DarksCheck's `ComponentRowTemplate` (its `Theme/Dark.xaml`, the
    `DataTemplate` at the end, with its comment on the routed `Hyperlink.RequestNavigate` that
    `AboutWindow.OpenLink` catches). Width stays 560: give the template's licence and copyright
    lines `TextWrapping="Wrap"` (NLog's copyright line is about 95 characters) and say so in the
    copied template's comment.
- The band's About button in `MainWindow.xaml`: `ToolTip="{l:T En='Version, licence, components
  (F1)', Ro='Versiune, licenta, componente (F1)'}"`, FlatCheck's and DarksCheck's words.

### `Logging/` (C1)

**`src/CopyWcs.Core/Logging/LogSetup.cs`** (new). Read `FlatCheck/FlatCheck.Core/Logging/LogSetup.cs`
whole first; this one is the same class line for line, in this project's style:

```csharp
internal sealed record LogPlace(string ExecutableName, string? Folder, bool FellBack, LogLevel Level);

internal static class LogSetup
{
    internal const long FileSizeLimit = 1024 * 1024;          // 1 MiB (R-040)
    internal const int FilesPerExecutable = 5;                // the file being written included
    internal const string LevelVariable = "COPYWCS_LOG";      // R-044
    internal const string FolderName = "log";
    internal const string FallbackFolder = "CopyWcs";         // under %LOCALAPPDATA%
    internal static readonly LogLevel DefaultLevel = LogLevel.Debug;
    internal const string LineLayout =
        "${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}"
        + "${onexception:inner=${newline}${exception:format=ToString}}";

    private static Logger Log => LogManager.GetLogger("Process");   // fetched on use (D8)
    private static Stopwatch? _clock;
    private static Exception? _lastFatal;

    internal static void Apply(string entryName, IReadOnlyList<string> args);
    internal static LogPlace Plan(string entryName, string? processPath, string baseDirectory, string fallbackRoot, string? levelValue);
    internal static void Close(int exitCode);
    internal static void Fatal(Exception? e, string where);
    internal static LoggingConfiguration Build(string executableName, string? folder, LogLevel minimum);
    internal static string ResolveFolder(string baseDirectory, string fallbackRoot, string executableName, out bool fellBack);
    private static bool CanWrite(string folder, string executableName);
    internal static string? NewestLog(string folder, string executableName);
    internal static LogLevel LevelFrom(string? value);
    internal static string ExecutableName(string? processPath, string entryName);
    internal static string StartupLine(string executableName, IReadOnlyList<string> args, string folder, bool fellBack, LogLevel level);
    private static string CurrentDirectory();
    private static string CultureName(CultureInfo c);
}
```

The behaviour is FlatCheck's; the only differences in the text:

- `namespace CopyWcs;`; the usings the SDK implies are dropped (`System`, `System.Collections.Generic`,
  `System.IO`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`; IDE0005 fails the
  build on a leftover); kept: `System.Diagnostics`, `System.Globalization`,
  `System.Runtime.InteropServices`, `System.Text`, `NLog`, `NLog.Config`, `NLog.Layouts`,
  `NLog.Targets`.
- Braces on every `if`, `foreach` and `else` (`csharp_prefer_braces` is a warning, so an error).
- `Build` returns the empty configuration when `minimum` is `Off` **or `folder` is null**, so
  `Apply` passes `place.Folder` as it is; `Apply` returns after setting the configuration when
  `place.Folder` is null, as FlatCheck's does.
- The lambdas' unused parameters are discards: `(_, e) => ...`, as `MainWindow` writes them.
- The `catch` filters in CopyWcs's pattern form: `when (e is IOException or
  UnauthorizedAccessException or NotSupportedException or ArgumentException)`.
- The file comment: what the class is (the one place that knows how the log is configured), that
  it is adapted from FlatCheck's (its ADRs 0070 and 0072) with the names kept so a fix carries
  between the tools, and that `docs/design/logging.md` holds the mechanics.
- Every type and method keeps a documentation comment (the kit's rule "Documentation comments"),
  FlatCheck's wording with CopyWcs's requirement numbers (R-040 for R-109, R-041 and R-042 for
  R-110, R-044 for R-111, R-045 for R-112).

**`src/CopyWcs.Core/Logging/RunClock.cs`** (new): FlatCheck's `RunClock` (a run's start line, its
stages through `Lap`, its done line through `Done`, and `Dispose` writing `<run> ended without an
outcome after N ms (<stages>)`), styled the same way, fields `_log`, `_run`, `_total`, `_laps`,
`_stageStart`, `_ended`.

**`src/CopyWcs.Core/Logging/LogText.cs`** (new): how a value is written in the log. FlatCheck's
general pieces, and CopyWcs's own:

| Member | Writes |
| --- | --- |
| `Int(long)`, `Ms(long)`, `Seconds(TimeSpan)`, `Str(string?)`, `Yes(bool)`, `KiB(long)`, `MiB(long)` | as FlatCheck's: invariant integers, `1141 ms`, `12.9 s` (one decimal, never whole seconds), `none` for null or empty, `yes`/`no`, `3.2 KiB`, `276.5 MiB` |
| `Size(long bytes)` | `KiB` below 1 MiB, `MiB` from there: `3.2 KiB`, `276.5 MiB` |
| `Card(string card)` | the card without its trailing spaces (`TrimEnd(' ')` only), each character below 0x20, and 0x7F, written as `\xNN` (two upper-case hex digits), so a malformed header never puts a NUL byte in the file (FlatCheck's review A4); the reader decodes ASCII, so nothing above 0x7F reaches it |
| `Value(string? card)` | a card's value as written, for the log only: `none` for null; when columns 9 and 10 hold `= `, the text from column 11 with its leading spaces trimmed, a quoted string up to and including its closing quote (a doubled `''` inside is part of it), anything else up to the first `/`, trimmed; otherwise the text from column 9, trimmed; escaped as `Card` does. It never feeds the copy, which stays `SetCard(key, GetCard(key)!)` (R-010). |
| `Keys(FitsHeader header)` | `KEY=value` for `BITPIX`, `NAXIS`, `NAXIS1`, `NAXIS2`, `NAXIS3`, `EXTEND`, `CTYPE1`, `CTYPE2`, `ROWORDER`, `A_ORDER`, `PLTSOLVD`, through `Value`, joined by spaces: the keys that say what the image is, whether the data sits in the primary HDU, whether it was flipped, and what solve it holds |
| `Request(WcsCopyRequest r)` | `source=<path> dest=<path> output=<path, or "in place"> optical=yes force=no dryRun=no` |

### The lines (C3, C4)

Every message is a plain string composed from `LogText` pieces (NLog never parses it as a
template, so a `{` in a path is harmless); one whose building costs a loop is guarded by
`Log.IsDebugEnabled` or `Log.IsTraceEnabled`. Every logger is a `Logger` named `Log`, a
`private static readonly` field taken from `LogManager.GetLogger("<name>")`, except `LogSetup`'s
and `App`'s, properties fetched on use (D8). The table is the specification the tests assert;
`<card>` is `LogText.Card`, `<n> ms` an elapsed time.

| Logger | Level | Message |
| --- | --- | --- |
| `Process` | Info | `<exe> <version> starting; exe=<path>; args=["a", "b"]; cwd=<folder>; log=<folder>[ (fallback: the folder beside the executable cannot be written)]; level=<Level>; os=<OS> <arch>; cpus=<n>; memory=<x.x> GiB; runtime=<.NET> <arch>; culture=<culture>/<ui culture>; tz=<id> (UTC+hh:mm); pid=<n>` (FlatCheck's `StartupLine`) |
| `Process` | Info | `exit <code> after <s> s` |
| `Process` | Fatal | `unhandled exception, the process ends` (with the exception), `unhandled exception on the window's thread, the window closes`, or `<where>: the exception logged above` for a crash already written |
| `Process` | Error | `a task failed and nobody awaited it` (with the exception) |
| `WcsCopy` | Info | `copy start <LogText.Request>` (the `RunClock` start line, before the source is read) |
| `FitsHeader` | Info | `read <path> (<Size>) bytes=<n> cards=<n> blocks=<n> dataOffset=<n> data=<n> <LogText.Keys>`, from `ReadPrimary`, for the copy's two reads and the window's facts alike; `data` is the bytes after the header (the file's length minus the data offset) |
| `FitsHeader` | Trace | `<path> card <n>: <card>`, every card read, numbered from 1, after the read line |
| `WcsCopy` | Warn | `cannot read the source <path>` or `cannot read the destination <path>`, with the exception |
| `WcsCopy` | Warn | `dimensions differ: source <W>x<H>, destination <W>x<H>; not forced, nothing is written`, or `...; forced, the copy goes on` (`-1` is the reader's "no such key") |
| `WcsCopy` | Warn | `the source has no CTYPE1, it does not look plate-solved; the copy goes on` |
| `WcsCopy` | Warn | `no relevant key in the source; nothing is written` |
| `WcsCopy` | Debug | `selected <n> keys: <w> WCS, <s> SIP, <o> optical (optical keys asked: yes)` (counted against `WcsMetadataKeys.WcsKeys` and `OpticalKeys`; the rest are SIP coefficients) |
| `WcsCopy` | Debug | `card <KEY>: <source card> \| dest: none`, `\| dest: same`, or `\| dest: <destination card>`, one per selected key in the copy's order, in a dry run too, computed before any `SetCard` |
| `FitsHeader` | Debug | `appended <card>` (`AddHistory`, the card exactly as stored) |
| `FitsHeader` | Debug | `write <target> in place through <target>.tmp, the data from byte <offset> of <source>`, or `write <target>, the data from byte <offset> of <source>` (`WriteWithDataFrom`, before the file is created) |
| `FitsHeader` | Trace | `<target> card <n>: <card>`, every card to be written, before the file is created, so a failed write still shows the header it meant to write |
| `FitsHeader` | Info | `wrote <target> (<Size>) bytes=<n> cards=<n> blocks=<n> data=<n>` (after the move, for an in-place write) |
| `WcsCopy` | Error | `the write of <target> failed`, with the exception |
| `WcsCopy` | Info | `copy done copied <n> keys into <target> (new <a>, replaced <r>, identical <i>) in <n> ms (source <n> ms, dest <n> ms, select <n> ms, write <n> ms)`; `copy done previewed <n> keys (new <a>, replaced <r>, identical <i>) in ...`; `copy done refused: <reason> in ...`, the reason one of `cannot read the source`, `cannot read the destination`, `dimensions differ (source 100x80, destination 200x80)`, `no relevant key in the source`, `the write failed`; the stages are those reached |
| `WcsCopy` | Info | `copy ended without an outcome after <n> ms (<stages>)` (`RunClock.Dispose`, a run that threw) |
| `Cli` | Warn | `usage error (exit 2): <the line on stderr>`; `refused (exit 1): <LanguageArgument.BadValueMessage>` |
| `Cli` | Info | `help shown (exit 0)` |
| `App` | Info | `language=<en or ro> source from the command line: <path or none> dest: <path or none>` (`App.StartLine`, after the window is shown) |
| `App` | Warn | `refused: <LanguageArgument.BadValueMessage>; the window goes on in English` |
| `Window` | Info | `source picked in the dialog: <path>`, `destination picked in the dialog: <path>`, `new file picked in the dialog: <path>`, `About opened` |
| `Window` | Debug | `<source, destination or new file> dialog closed without a file` |
| `Window` | Debug | `no facts under the <source or destination> box: the header of <path> could not be read` (with the exception); `no facts under the <...> box: <path> has no NAXIS1 or NAXIS2` |
| `Window` | Debug | `the link <uri> could not be opened` (with the exception; `AboutWindow.OpenLink`'s two catches become one filtered catch) |

The window's Preview and Copy add no line of their own: the core's start line holds the request
and its done line the outcome. A copy's stages: `source` after the source is read, `dest` after
the destination, `select` after the size check, the `CTYPE1` check and the selection, `write`
after the cards are set, the `HISTORY` card appended and the file written; a refusal ends the
stage it happens in, then `Done`.

A copy at Debug reads like this, the source the Veil fixture (its first cards are `BITPIX = -32`,
`NAXIS = 3`, 5601 x 3692 x 3, `EXTEND = T`, `ROWORDER = 'BOTTOM-UP'`, a SIP solve of degree 3)
and the destination an export of the same stack; the destination's values, the counts left as
`...` and every time are illustrative, and the implementer pastes a real one into `logging.md`:

```
2026-10-30 14:02:11.1234 INFO  [4312:1] Process copywcs 1.0.0 starting; exe=C:\Tools\copywcs.exe; args=["veil-solve.fit", "stack.fit", "-o", "out.fit"]; cwd=D:\Astro\Veil; log=C:\Tools\log; level=Debug; os=Microsoft Windows 10.0.19045 X64; cpus=8; memory=31.9 GiB; runtime=.NET 8.0.21 X64; culture=invariant/invariant; tz=GTB Standard Time (UTC+02:00); pid=4312
2026-10-30 14:02:11.1300 INFO  [4312:1] WcsCopy copy start source=veil-solve.fit dest=stack.fit output=out.fit optical=yes force=no dryRun=no
2026-10-30 14:02:11.1310 INFO  [4312:1] FitsHeader read veil-solve.fit (8.4 KiB) bytes=8640 cards=... blocks=3 dataOffset=8640 data=0 BITPIX=-32 NAXIS=3 NAXIS1=5601 NAXIS2=3692 NAXIS3=3 EXTEND=T CTYPE1='RA---TAN-SIP' CTYPE2='DEC--TAN-SIP' ROWORDER='BOTTOM-UP' A_ORDER=3 PLTSOLVD=T
2026-10-30 14:02:11.1320 INFO  [4312:1] FitsHeader read stack.fit (236.5 MiB) bytes=... cards=12 blocks=1 dataOffset=2880 data=... BITPIX=16 NAXIS=3 NAXIS1=5601 NAXIS2=3692 NAXIS3=3 EXTEND=none CTYPE1=none CTYPE2=none ROWORDER=none A_ORDER=none PLTSOLVD=none
2026-10-30 14:02:11.1330 DEBUG [4312:1] WcsCopy selected 72 keys: 21 WCS, 40 SIP, 11 optical (optical keys asked: yes)
2026-10-30 14:02:11.1331 DEBUG [4312:1] WcsCopy card CTYPE1: CTYPE1  = 'RA---TAN-SIP'       / TAN (gnomic) projection + SIP distortions | dest: none
2026-10-30 14:02:11.1340 DEBUG [4312:1] WcsCopy card INSTRUME: INSTRUME= 'Nikon D3300'        / Instrument name | dest: INSTRUME= 'NIKON D3300'
2026-10-30 14:02:11.1350 DEBUG [4312:1] FitsHeader appended HISTORY WCS copied from veil-solve.fit
2026-10-30 14:02:11.1351 DEBUG [4312:1] FitsHeader write out.fit, the data from byte 2880 of stack.fit
2026-10-30 14:02:12.3100 INFO  [4312:1] FitsHeader wrote out.fit (236.5 MiB) bytes=... cards=... blocks=... data=...
2026-10-30 14:02:12.3110 INFO  [4312:1] WcsCopy copy done copied 72 keys into out.fit (new 71, replaced 1, identical 0) in 1180 ms (source 1 ms, dest 1 ms, select 1 ms, write 1177 ms)
2026-10-30 14:02:12.3200 INFO  [4312:1] Process exit 0 after 1.2 s
```

### The applications

**`src/CopyWcs.Cli/Program.cs`** (Q2): `Main` becomes FlatCheck's, its XML comment and the file's
comment saying what it is now (a shim: the log's first line, the one call to `CopyWcsCli.Run`,
the log's last line; the log is configured here and not in `CopyWcsCli.Run`, which the tests call
in process):

```csharp
private static int Main(string[] args)
{
    // The log never stops the tool: a folder that cannot be probed, a broken NLog on this
    // machine, and the run goes on without a log, stdout and stderr untouched (R-041).
    try { LogSetup.Apply("copywcs", args); }
    catch (Exception) { }

    // What the log's last line reports when the run throws; the process itself then exits
    // with the runtime's crash code, not with this.
    int code = 1;
    try
    {
        code = CopyWcsCli.Run(args, Console.Out, Console.Error);
        return code;
    }
    finally
    {
        try { LogSetup.Close(code); }
        catch (Exception) { }
    }
}
```

**`src/CopyWcs.Cli/CopyWcsCli.cs`**: `private static readonly Logger Log = LogManager.GetLogger("Cli");`
and the three lines of the table: the bad `--lang` before `return 1`, the parse error before
`return 2` (`ex.Message`, the line written on stderr), `--help` before `return 0`. Nothing else:
the run logs itself. `CopyWcsCli.Run` configures nothing.

**`src/CopyWcs.Wpf/Loggers.cs`** (new): FlatCheck's `Loggers` with two names, `App` (the start,
the arguments, the exit) and `Window` (the main window, the view-model, the About window); the
window has one view-model, so FlatCheck's `Window.<Tab>`, `Siril` and `Split` have nothing to name
here (LightsCheck's L5).

**`src/CopyWcs.Wpf/App.xaml.cs`**:

- The constructor: the arguments once (`all.Length > 1 ? all[1..] : []`), then
  `try { LogSetup.Apply("CopyWcs.Wpf", args); } catch (Exception) { }`, then
  `DispatcherUnhandledException += (_, e) => LogSetup.Fatal(e.Exception, "unhandled exception on
  the window's thread, the window closes");`, then `Parse(args)` and `Ui.Default` as today. The
  handler does not set `e.Handled` (D7).
- `private static Logger Log => LogManager.GetLogger(Loggers.App);`, with FlatCheck's comment on
  why it is fetched on use (ADR 0009, FlatCheck's ADR 0072).
- `OnStartup`: after `window.Show()`, the Warn line before the existing `MessageBox` for a bad
  `--lang`, then `Log.Info(StartLine(_start))`, then `window.Start(...)`.
- `internal static string StartLine(StartArgs start)`: the `App` Info line of the table, so a
  test reads it without an `App`.
- `protected override void OnExit(ExitEventArgs e)`: `base.OnExit(e);` then
  `try { LogSetup.Close(e.ApplicationExitCode); } catch (Exception) { }`.
- The file comment gains the log (Apply first, so a failure while the resources load is in it).

**`src/CopyWcs.Wpf/MainWindow.xaml.cs`**: a `Window` logger; after each dialog the picked line or
the closed line; `ShowAbout` logs `About opened` before `ShowDialog`.

**`src/CopyWcs.Wpf/ViewModels/MainViewModel.cs`**: a `Window` logger; `Facts` logs its two
Debug lines (the catch, with the exception, and the header without a size). Nothing else: the
view-model still builds a request, reads two facts and shows the outcome (ADR 0007).

**`src/CopyWcs.Wpf/Views/AboutWindow.xaml.cs`**: `OpenLink`'s two empty catches become one
`catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)` with the Debug
line (LightsCheck's deviation, the same fix).

**`src/CopyWcs.Core/FitsHeader.cs`** and **`src/CopyWcs.Core/WcsCopy.cs`**: the lines of the
table. In `WcsCopy.Run`: `using RunClock clock = RunClock.Start(Log, "copy",
LogText.Request(request));` at the top (after the null check); the card loop's status
(`new`/`replaced`/`identical`) is computed in a loop of its own before the existing
`foreach (var k in present) { dest.SetCard(k, source.GetCard(k)!); }`, which stays exactly as it
is (R-010, `CLAUDE.md`). `FitsHeader`'s class comment "no FITS library, no package (ADR 0001)"
becomes "no FITS library (ADR 0001)".

### Requirements

In `docs/requirements.md`: the preamble's list of plans gains "the diagnostic log (R-040 on) the
plan `docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`"; a new section **The diagnostic
log (R-040 to R-049)** before `## Dropped`, with the file's three-column header (`| ID | Status |
Requirement |`), each row `draft` until its tests pass, then `implemented` (the texts below, the
status column left out here):

| ID | Requirement |
| --- | --- |
| R-040 | Each application writes a plain-text log, UTF-8 without a BOM, in a folder `log` beside its executable, or in `%LOCALAPPDATA%\CopyWcs\log` when that folder cannot be created, a file cannot be created in it, or the file to be written next cannot be opened for append (the process's first line says which). The file names begin with the executable's name, `copywcs` for the console and `CopyWcs.Wpf` for the window: the first file is `copywcs.log`, and when the file being written passes 1 MiB (1,048,576 bytes) the next is started, `copywcs_01.log`, `copywcs_02.log`, ...; at most 5 files per executable are kept, the oldest deleted first, and the next process goes on in the newest. One executable's rolling never deletes the other's files when both share a folder, and a file another process of the same executable holds open is written beside it. Example: 8 MiB of lines through the console's configuration leave `copywcs_03.log` to `copywcs_07.log`, none above 1 MiB plus one line, the last line in `copywcs_07.log`, where the next run writes too. |
| R-041 | The log adds nothing to stdout or stderr and never stops a run: a log that cannot be set up leaves the tool working without one. Every line is the local time to 0.1 ms, the level padded to five, `[<process id>:<thread id>]`, the logger's name and the message, with an exception written whole (type, message, stack, inner exceptions) on the lines under it. Each process begins with one line naming the executable and its version, its path, the arguments verbatim, the working folder, the log folder (and the fallback when taken), the level, the OS, the processors, the memory, the .NET runtime, the culture, the time zone and the process id, and ends with `exit <code> after <seconds, one decimal> s`; an exception nobody handled is written at `FATAL` once, even when two hooks see it, and the process ends as it did before. Example: `copywcs 1.0.0 starting; exe=C:\Tools\copywcs.exe; args=["solved.fit", "stack.fit", "--dry-run"]; ...; pid=4312`, then `exit 0 after 0.1 s`. |
| R-042 | A copy run, from the console or the window, writes: a start line with the request; one line per header read with the file's size, its bytes, cards, blocks, data offset and the bytes after it, and `BITPIX`, `NAXIS`, `NAXIS1`, `NAXIS2`, `NAXIS3`, `EXTEND`, `CTYPE1`, `CTYPE2`, `ROWORDER`, `A_ORDER` and `PLTSOLVD` as written (`none` when absent); at Debug the count of keys selected per list and one line per key with the source's card and the destination's card it replaces (`none`, `same`, or that card), and the `HISTORY` card appended; the file written with its size, cards and blocks, and for an in-place write the temporary file; a line per warning of R-005 and R-006; a refusal's reason in English, with the exception that caused it, at `WARN` for a file that cannot be read and at `ERROR` for a write that failed; and a done line with the outcome, the counts and the milliseconds of each stage reached (`source`, `dest`, `select`, `write`); a run that throws ends with `copy ended without an outcome after N ms` and its stages. A card is written without its trailing spaces, a control character as `\xNN`. Example: the `FitsTestData` pair whose destination has `OBJECT = 'M43'` writes `card OBJECT: OBJECT  = 'M42' \| dest: OBJECT  = 'M43'` and `copy done copied 15 keys into <dest> (new 14, replaced 1, identical 0) in 3 ms (source 0 ms, dest 0 ms, select 0 ms, write 2 ms)`. |
| R-043 | The applications log their own events: the console a usage error with its line and exit 2, `--help`, and a `--lang` refused with exit 1; the window the language and the two files taken from the command line, a `--lang` it did not understand, the file picked in each dialog, the About window opened, a file whose header facts could not be read (at Debug, with the reader's exception), and a link the About window could not open. A copy from the window writes the same lines as the console's copy of the same request. Example: `CopyWcs.Wpf.exe --lang ro solved.fit stack.fit` writes `language=ro source from the command line: solved.fit dest: stack.fit`. |
| R-044 | The level is Debug unless the environment variable `COPYWCS_LOG` names another, in any letter case, spaces around ignored: `off`, `fatal`, `error`, `warn`, `info`, `debug` or `trace`; any other value means Debug. Trace adds every card of every header read and of every header written, numbered from 1; `off` creates neither the folder nor a file. Example: with `COPYWCS_LOG=off` a run leaves the folder beside the executable as it was; with `COPYWCS_LOG=trace` the log of a copy holds `card 1: SIMPLE  = T` for the source, the destination and the file written. |
| R-045 | Two processes of the same executable writing the same file at full speed both keep every line, each whole and once, across the 1 MiB roll too: the log is written through `NLog.Targets.AtomicFile`, which leaves the operating system to place every write at the end of the file (FlatCheck's ADR 0070 measured NLog's plain target losing lines). Example: two writers of 4000 lines each into `CopyWcs.Wpf.log` leave 8000 whole lines in two files, the two writers' lines interleaved. |
| R-046 | The log's own words are English with invariant numbers whatever `--lang` says and never pass through `T(english, romanian)`: the same run in English and in Romanian writes the same messages, the times aside; an exception's message (under its line) and a usage error's line appear as the user was shown them. Example: under `--lang ro` a copy refused for different dimensions writes `copy done refused: dimensions differ (source 100x80, destination 200x80) in 1 ms (source 0 ms, dest 0 ms, select 0 ms)`. |

Two rows change with Q1, both staying `implemented`, their tests changed with them:

- **R-035**: after "the link `https://www.gnu.org/licenses/gpl-3.0.html`" insert "a card
  `THIRD-PARTY COMPONENTS` (`COMPONENTE TERTE`) with one sentence on the components and one row
  per component of `ThirdParty.Components` (its name, its role in the run's language, its
  licence, its copyright line and its link), which claims every package of
  `src/CopyWcs.Core/CopyWcs.Core.csproj` exactly once, each with a heading in
  `THIRD-PARTY-NOTICES.md`,"; and at the end "Example: the one row today is `NLog`,
  `BSD-3-Clause`, `Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim
  Christensen, Julian Verdurmen`, `https://nlog-project.org/`."
- **R-038**: the tooltip becomes `Version, licence, components (F1)` (`Versiune, licenta,
  componente (F1)`).

### The decision and the notes

- **`docs/decisions/0009-a-diagnostic-log-with-nlog-as-flatchecks-and-nlog-in-the-about-window.md`**,
  title "0009. A diagnostic log with NLog, as FlatCheck's, written at the copy's seams, and NLog's
  notice in the About window", `Date: 2026-10-30`, `Status: accepted 2026-10-30`,
  `Supersedes: 0007 in part (the About window without the third-party card); 0008 in part (the
  About tooltip without the components)`. Context: what a report carries today (this plan's
  Context, shorter), the owner's ask, FlatCheck's ADRs 0070 and 0072 as the model, the two facts
  (no package; the flat bundle), the two questions and the answers. Decision: the rows D1 to D9
  as one decision with CopyWcs's values (cite FlatCheck's measurements, do not copy its table),
  C1 to C6, Q1, Q2. Alternatives rejected: `Microsoft.Extensions.Logging` with a provider (two
  packages for one sink; NLog asked for by name); an `NLog.config` (a file to ship, keep in step
  and break); logging inside `WcsMetadataKeys` or `Language/` (the run logs what they return); a `--log` option (moves the console's contract, R-002's usage and its tests); NLog's plain
  target either way (FlatCheck's table); one file per process (five runs would fill the five
  files); the cards only at Trace (the first log a user sends would not hold the one record a
  wrong WCS needs); a notices file beside each executable (the suite's flat folder); a one-line
  `Main` through a wrapper in the core (Q2); a logging library shared with FlatCheck (ADR 0007's
  reason: copies of three small files cost less than a package with a version). Consequences:
  the first package, `THIRD-PARTY-NOTICES.md`, `ThirdParty.cs` and `ThirdPartyTests`; the log is
  the first thing to ask a reporter for; every test runs every log line; the invariant tests;
  the versions move with FlatCheck's; the tests named below.
- **ADR 0007 and 0008**, merged, change only their Status line (the kit's ADR 0024):
  `Status: accepted 2026-10-26; the About window without the third-party card superseded by 0009`
  and `Status: accepted 2026-10-28; the About tooltip without the components superseded by 0009`.
  Nothing else in them is edited. Before the merge, check that 0009 is still CopyWcs's next free
  number on `main`.
- **`docs/design/logging.md`** (new). `**Covers:**` `src/CopyWcs.Core/Logging/LogSetup.cs`,
  `src/CopyWcs.Core/Logging/LogText.cs`, `src/CopyWcs.Core/Logging/RunClock.cs`,
  `src/CopyWcs.Cli/Program.cs`, `src/CopyWcs.Wpf/App.xaml.cs`, `src/CopyWcs.Wpf/Loggers.cs`,
  `tests/CopyWcs.Tests/LogCapture.cs`, `tests/CopyWcs.Tests/LoggingTests.cs`,
  `tests/CopyWcs.Wpf.Tests/WindowLoggingTests.cs`, and the log lines of the seams the table lists.
  Sections after FlatCheck's note: how the parts fit (`Apply`'s four steps, `Plan`, the probe,
  `Build` before any logger, the hooks; `CopyWcsCli.Run` configures nothing, the window's test
  fixture builds a plain `Application`); the files, measured (the rolling test's file list as
  this project's run prints it, pasted; the window's files surviving the console's rolls); the
  line; a copy's lines (the stages, the card statuses, the counts, a refusal, `ended without an
  outcome`); what is logged where (this plan's table, kept in step with the code); reading a log
  (a process from `starting` to `exit` or `FATAL`, the pid separating two processes, a run from
  `copy start` to `copy done`); in the tests (`LogCapture`, the private factories).
- **`docs/design/README.md`**: a row "The diagnostic log: the configuration the two applications
  apply, the files as NLog rolls them, the line, what each seam logs, the tests' capture |
  `logging.md` | `Logging/`, `Program.cs`, `App.xaml.cs`, any log line, `LogCapture`,
  `LoggingTests`, R-040 to R-046"; the run row gains `ThirdParty.cs` and the window row the About
  window's third-party card.
- **`docs/design/run-and-console.md`**: `src/CopyWcs.Core/ThirdParty.cs` in its Covers sentence
  and a paragraph beside `LicenseInfo` (the list the About window shows, bound by
  `ThirdPartyTests` to the core's csproj and to the notices file's headings); "`Program.Main` is a
  one-line shim" becomes what it is now, pointing at `logging.md`.
- **`docs/design/window.md`**: the About window's third-party card and the template; the tooltip;
  `App`'s log calls (one sentence pointing at `logging.md`); `Loggers.cs`; `WindowLoggingTests`
  in the tests paragraph.
- **`docs/design/fits-header.md`**: one sentence: the reader and the writer write the read, write
  and wrote lines and the Trace cards of `logging.md`'s table, and decide nothing from them.
- **`CLAUDE.md`**: the first paragraph's "no FITS library, no package" becomes "no FITS library;
  its one package, NLog, writes the diagnostic log (ADR 0009)"; Commands gains "The log of a run:
  `log\copywcs.log` or `log\CopyWcs.Wpf.log` beside the executable (under `bin\Release\...` for a
  build); `COPYWCS_LOG=trace` adds every card, `off` writes nothing"; Layout names `Logging/` and
  `ThirdParty.cs` in the core's line, `LoggingTests`, `ThirdPartyTests` and `LogCapture` in the
  tests' line, `WindowLoggingTests` in the window tests' line, and `THIRD-PARTY-NOTICES.md` at the
  root; the rule on `Main`
  becomes "**`Main` is a shim**: it opens the diagnostic log, calls `CopyWcsCli.Run(args, stdout,
  stderr)` once and closes the log, each log call guarded so the log never stops the tool (ADR
  0009); new console behaviour goes in `CopyWcsCli` or `Options`, testable in process, without a
  subprocess."; the language rule's "the HISTORY card is English in both" becomes "the HISTORY
  card and the diagnostic log are English in both"; a new rule "**The log is configured in two
  places and written in English.** Only `Program.Main` and `App` call `LogSetup.Apply` and
  `Close`, and nothing but `LogSetup` and the tests' `LogCapture` sets NLog's configuration; no
  log line goes through `T(...)`; a log line never changes what the console prints or the window
  shows (ADR 0009)."; "Read before touching" gains "`logging.md` before `Logging/`, `Program.cs`,
  `App.xaml.cs` or any log line".
- **`docs/skills.md`**, `/new-feature`: "A new log line is English, composed from `LogText`, never
  through `T(...)` (`NoLogLineGoesThroughTheTranslation`); a new seam's lines join the table in
  `docs/design/logging.md`, and a line of the copy run R-042 and its test."
- **`README.md`**: a section `## Logs` after "The two languages", adapted from FlatCheck's README
  section: attach the whole `log` folder to a report; where it is (`log\copywcs.log` beside
  `copywcs.exe`, `log\CopyWcs.Wpf.log` beside `CopyWcs.Wpf.exe`, the fallback
  `%LOCALAPPDATA%\CopyWcs\log` said in the first line, the files past 1 MiB, five kept, about
  5 MiB per program, two windows into one file); what is in it (the version, the command line,
  the Windows and .NET versions and the folders; each file read with its size and the header keys
  that matter; the run's options; every card copied and the card it replaced; the file written;
  the warnings, the refusals with their reasons and every error with its technical details; in
  the window the files picked and the About window; the exit code and the time); that it holds
  the paths of your files and the header cards copied, which include the site's latitude and
  longitude when the source has them, so read it before you send it; how much (`COPYWCS_LOG`,
  `set COPYWCS_LOG=trace` for one command window, `setx COPYWCS_LOG trace` for the account, the
  levels). A section `## Third-party components` before "Licence": a table with NLog's row (the
  diagnostic log, BSD-3-Clause, the link) and a sentence pointing at `THIRD-PARTY-NOTICES.md`.
  "The window" paragraph: About opens the version, the copyright, the GPLv3 notice and the
  third-party components.
- **`CHANGELOG.md`** under `[Unreleased]`: Added, "A diagnostic log beside each executable,
  `log\copywcs.log` and `log\CopyWcs.Wpf.log`, five files of 1 MiB at most, with the run's
  options, the headers read, every card copied and the card it replaced, the file written and
  every error with its details (ADR 0009); attach the `log` folder to a bug report;
  `COPYWCS_LOG` sets the level." and "The About window lists the third-party components (NLog),
  and the About button's tooltip says so."; Internal, "NLog 6.2.2 and NLog.Targets.AtomicFile
  6.2.2, the tool's first package, at FlatCheck's versions; `Logging/` adapted from FlatCheck's."

## Tests

Tests first, red, then the code (the kit's rule "Requirements first"). Every new file starts with
the GPL notice (`LicenseHeaderTests`); test names `R0NN_...` for a requirement, plain names for a
repository invariant.

- **`tests/CopyWcs.Tests/LogCapture.cs`** (new): FlatCheck's `FlatCheck.Tests/LogCapture.cs`
  (already file-scoped and nullable), namespace `CopyWcs.Tests`. Linked into the window's tests:
  `tests/CopyWcs.Wpf.Tests/CopyWcs.Wpf.Tests.csproj` gains
  `<Compile Include="..\CopyWcs.Tests\LogCapture.cs" Link="Shared\LogCapture.cs" />` beside the
  other links, with a comment. One capture at a time per call tree: a test that captures twice
  closes the first (`using (...) { }`) before it opens the second.
- **`tests/CopyWcs.Tests/LoggingTests.cs`** (new), after FlatCheck's `LoggingTests` (`LinePrefix`,
  `FilesOf`, `Factory`, `WriteThrough`, `TwoWritersLines` copied; `TempWorkspace.FilePath` and
  `WriteFile` where FlatCheck has `PathTo`; `TestData.FindRepoRoot()` for the repository):
  - R-040: `R040_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName` (the folder
    created, the probe gone, the one line in `copywcs.log` matching `LinePrefix`, no BOM);
    `R040_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback` (a file named `log`;
    `<root>\CopyWcs\log`; the first line says `(fallback`);
    `R040_AReadOnlyNewestLogFileSendsTheLogToTheFallback`;
    `R040_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe`;
    `R040_TheLogRollsAt1MiBAndKeepsAtMost5Files` (8 MiB: exactly `copywcs_03.log` to
    `copywcs_07.log`, each at most `FileSizeLimit + 4200` bytes, the last line in the newest; a
    second writer grows the newest and keeps the five names; the file list printed through
    `ITestOutputHelper` for the design note);
    `R040_OneExecutablesRollingLeavesTheOthersFilesAlone` (`CopyWcs.Wpf` rolled once, then
    `copywcs` rolled to five: the window's two files intact; the two names share their letters
    in another case); `R040_TheFilesBeginWithTheExecutablesName` (a theory:
    `C:/Tools/copywcs.exe` gives `copywcs`, `C:/Tools/copywcs-1.1.exe` `copywcs-1.1`, the
    `dotnet` host on Windows and Unix the entry name, a null path the entry name).
  - R-041: `R041_TheFirstLineCarriesTheFacts` (FlatCheck's `R110_TheFirstLineCarriesTheFacts`
    with `copywcs` and `VersionInfo.Value`); `R041_TheConfigurationHasOneFileTargetAndNoConsole`
    (one `AtomicFileTarget`, `ArchiveAboveSize`, `MaxArchiveFiles`, `KeepFileOpen`,
    `ConcurrentWrites`, one rule from Debug); `R041_TheSameCrashSeenByTwoHooksIsWrittenOnce`;
    `R041_TheLastLineGivesTheSecondsWithOneDecimal` (`LogText.Seconds` and `Ms`).
  - R-042, through `CopyWcsCli.Run` on files from `FitsTestData` (`PlateSolvedCards`,
    `ProcessedCards`, `DeterministicData(400)`):
    `R042_ACopyLogsTheRequestBothHeadersEveryCardAndTheStages`: the source `source.fit`, the
    destination `dest.fit` with its `OBJECT` made `'M43'`, in place. It asserts the start line
    with `output=in place`; both read lines field by field, the source's ending in

    ```
    (3.2 KiB) bytes=3280 cards=20 blocks=1 dataOffset=2880 data=400 BITPIX=16 NAXIS=2 NAXIS1=100 NAXIS2=80 NAXIS3=none EXTEND=none CTYPE1='RA---TAN-SIP' CTYPE2='DEC--TAN-SIP' ROWORDER=none A_ORDER=none PLTSOLVD=T
    ```

    and the destination's holding `cards=6` and `CTYPE1=none`; then these messages, whole:

    ```
    selected 15 keys: 9 WCS, 2 SIP, 4 optical (optical keys asked: yes)
    card CTYPE1: CTYPE1  = 'RA---TAN-SIP' / TAN + SIP | dest: none
    card OBJECT: OBJECT  = 'M42' | dest: OBJECT  = 'M43'
    appended HISTORY WCS copied from source.fit
    wrote <dest> (3.2 KiB) bytes=3280 cards=21 blocks=1 data=400
    ```

    the `write <dest> in place through <dest>.tmp, the data from byte 2880 of <dest>` line, and
    the done line by a regex, `copy done copied 15 keys into <dest> (new 14, replaced 1,
    identical 0) in \d+ ms` with its four stages; every event matches `LinePrefix`.
    `R042_ADryRunLogsTheCardsItWouldCopyAndWritesNoFile`: the default pair with `--dry-run`,
    `previewed 15 keys (new 14, replaced 0, identical 1)`, the message
    `card OBJECT: OBJECT  = 'M42' | dest: same`, no `write` or `wrote` line.
    `R042_AFileThatCannotBeReadIsAWarningWithTheExceptionAndItsStack` (a text file as source:
    stderr still its one line; one Warn event with an `InvalidDataException`, its rendered line
    holding `System.IO.InvalidDataException:` and `   at CopyWcs.FitsHeader.ReadFrom`; the done
    line `refused: cannot read the source` with the `source` stage only);
    `R042_EveryRefusalSaysWhyInTheDoneLine` (a theory: a missing destination, different
    dimensions without `--force`, `ProcessedCards` as source with `--no-optical`);
    `R042_AWriteThatFailsIsAnErrorWithTheException` (`-o` into a missing folder: one Error event
    with a `DirectoryNotFoundException`, `refused: the write failed` with the `write` stage);
    `R042_TheTwoWarningsAreLogged` (`--force` on 100 against 200 columns; a source without
    `CTYPE1`); `R042_AControlCharacterInACardIsEscaped_NoNulByteInTheLog` (a source whose
    `OBJECT` comment holds `'\0'`: no `'\0'` in the captured text, `\x00` in the card line);
    `R042_ARunThatEndsWithoutAnOutcomeSaysSoWithItsStages` (`RunClock` directly: a lap, no
    `Done`, then `copy ended without an outcome after \d+ ms \(source \d+ ms\)`).
  - R-043, the console's part: `R043_TheConsoleLogsAUsageErrorHelpAndARefusedLang` (`--lang en`
    and one path gives `usage error (exit 2): Exactly two files are needed: the source and the
    destination.`; `--help` gives `help shown (exit 0)`; `--lang xx` gives `refused (exit 1):
    --lang expects en or ro, got 'xx'`).
  - R-044: `R044_TheLevelComesFromTheEnvironmentVariable` (FlatCheck's theory);
    `R044_OffCreatesNeitherTheFolderNorAFile` (`Plan` with `off`, then with `trace`);
    `R044_TraceHoldsEveryCardOfEveryHeaderReadAndWritten` (a copy: `<source> card 1: SIMPLE  = T`,
    the destination's and the target's card 1 at Trace, the count of the target's Trace cards
    21).
  - R-045: `R045_TwoProcessesWritingAtFullSpeedKeepEveryLine` (FlatCheck's, `CopyWcs.Wpf`).
  - R-046: `R046_TheLogIsTheSameInBothLanguages` (a theory over `copy`, `preview`, `newfile`,
    `forced`, `nosolve`, `dims`, `nokey`, `unreadable`, `writeerror`: the same scenario under
    `--lang en` and `--lang ro`, each in its own `TempWorkspace`; the messages compared after the
    workspace's path becomes `<ws>` and `\d+ ms` becomes `# ms`; not empty).
  - Invariants: `OnlyTheTwoEntryPointsConfigureTheLog` (over every `.cs` under `src/` and `tests/`
    without `bin/` and `obj/`, the walk `LicenseHeaderTests.SourceFiles` does: `LogSetup.(Apply|Close)(`
    only in `src/CopyWcs.Cli/Program.cs` and `src/CopyWcs.Wpf/App.xaml.cs`;
    `LogManager.(Configuration =|Setup()` only in `src/CopyWcs.Core/Logging/LogSetup.cs` and
    `tests/CopyWcs.Tests/LogCapture.cs`); `NoLogLineGoesThroughTheTranslation` (FlatCheck's, over
    `src/`: no log call holds `T(`, every logger declared the one way).
- **`tests/CopyWcs.Tests/ThirdPartyTests.cs`** (new), DarksCheck's tests adapted:
  `R035_EveryPackageTheCoreLinksIsListedExactlyOnce` (reads `src/CopyWcs.Core/CopyWcs.Core.csproj`),
  `R035_EveryComponentHasAHeadingInTheNoticesFile`, `EveryComponentNamesItsLicenceAndUrl` (one
  component), `R035_NLogIsTakenUnderBsd3AndCarriesItsCopyright`.
- **`tests/CopyWcs.Wpf.Tests/WindowLoggingTests.cs`** (new), like `MainViewModelTests` (no
  window, no collection): `R043_TheStartLineNamesTheLanguageAndTheFilesFromTheCommandLine`
  (`App.StartLine(App.Parse(["--lang", "ro", f.Source, f.Dest]))`, and `none` for an empty command
  line); `R043_AFileWhoseFactsCannotBeReadIsADebugLineWithTheException` (a text file as `Source`:
  `SourceFacts` empty, one Debug event of `Window` with an `InvalidDataException`);
  `R043_ACopyFromTheWindowWritesTheLinesOfTheConsolesCopy` (`vm.Copy()` on one `WindowFixture`,
  `CopyWcsCli.Run` on a second; the `WcsCopy` and `FitsHeader` messages from `copy start` to
  `copy done` equal once each folder becomes `<dir>` and `\d+ ms` becomes `# ms`; the facts read
  again after the copy is outside the range).
- **`tests/CopyWcs.Wpf.Tests/AboutTests.cs`**: `R035_TheThirdPartyCardListsTheCoresComponents` (a
  theory in both languages: `Components` equal to `ThirdParty.Components` field by field, the
  heading, a lead naming `NLog` and `THIRD-PARTY-NOTICES.md`).
- **`tests/CopyWcs.Wpf.Tests/WindowSmokeTests.cs`**: the R-038 theory's two tooltips change with
  R-038 (the intended change of that test, with the requirement); `RomanianWord()` gains
  `COMPONENTE|TERTE|componente|Unealta|jurnalului|leaga`; the About render, which now holds the
  card, still shows no binding error.

Every other test stays as it is and stays green: the console's output, the copy's bytes, the
window's renders and the validation case do not move. No golden file changes.

## Steps, in order

1. **Worktree.** From `E:\Photografie\Code\WCS_Copy` on `main`:
   `git worktree add .claude/worktrees/Log-CopyWcs-with-NLog -b claude/Log-CopyWcs-with-NLog main`.
   Work, build and test only there (the project is `<worktree>\WCS_Copy`); never in the main
   checkout. Save this plan as `docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md` there,
   commit it alone.
2. **Requirements.** R-040 to R-046 as `draft` in their new section; R-035 and R-038 amended;
   the preamble's plan list.
3. **The package and the notice.** `Directory.Packages.props`, the core csproj, the forced
   restore and the five lock files; `ThirdParty.cs`, `THIRD-PARTY-NOTICES.md`; then the About
   card and the tooltip, tests first (`ThirdPartyTests`, `AboutTests`, `WindowSmokeTests`). Gate
   green, commit.
4. **`Logging/`, the tests' capture and the two entry points.** `LogCapture.cs` and its link; the
   R-040, R-041, R-044 and R-045 tests and the two invariants, red; `LogSetup.cs`, `RunClock.cs`,
   `LogText.cs`, then `Program.Main` and `App`'s constructor, dispatcher hook and `OnExit` (the
   invariant `OnlyTheTwoEntryPointsConfigureTheLog` asserts those two callers, so they land in
   this commit), until green. Commit.
5. **The seams.** The R-042, R-043 and R-046 tests, red; then `FitsHeader`, `WcsCopy`,
   `CopyWcsCli`, `Loggers`, `App`'s start lines, `MainWindow`, `MainViewModel`, `AboutWindow`.
   Every `catch` keeps what it does; only the log line is added (`AboutWindow.OpenLink` excepted,
   one filtered catch). R-040 to R-046 `implemented` once their tests pass. Gate green, commit.
6. **Docs.** ADR 0009, the Status lines of 0007 and 0008, `logging.md` and its index row,
   `run-and-console.md`, `window.md`, `fits-header.md`, `CLAUDE.md`, `docs/skills.md`, the
   README, the CHANGELOG, the comments named above (`Directory.Packages.props`, the core csproj,
   `FitsHeader`, `AboutViewModel`, `AboutWindow.xaml`, `App`, `Program`, the console's csproj
   comment on `Main`). Gate green, commit.
7. **By hand** (Verification below), the outputs pasted into this plan's Outcome and the rolling
   test's file list into `logging.md`. Commit.
8. **Close.** `dotnet build <worktree>\WCS_Copy\CopyWcs.sln -c Release --no-incremental
   -warnaserror --no-restore`, then `scripts/verify.ps1`, then `scripts/check-design.ps1 -Since
   main` and read every note it lists again; read the whole diff and summarise it in plain
   language. Offer the owner a subagent code review of the branch, and after its fixes a second
   one on the fix commit only. Then `/wrap-up`: CHANGELOG, requirements, an Outcome section here,
   the merge proposed and asked, with main's latest CopyWcs ADR number checked first. Say that
   the change seems to warrant a version bump (the window, the band and now the log are
   unreleased since 1.0.0); the bump is the owner's. No figure moved, so `/validate` is not needed.

## Verification

- The gate, `scripts/verify.ps1`, green in the worktree, after the no-incremental build above.
- The console, the Release build of the worktree, the log read from
  `src\CopyWcs.Cli\bin\Release\net8.0\log\copywcs.log`, each run's lines pasted. The source is
  the fixture `tests\testdata\fixtures\veil-2026-10-06-siril-1.4.4-solve.fit`; the destination a
  header-only file of 5601 x 3692 in the scratchpad, as V-001 is measured (`docs/validation.md`),
  written by PowerShell:

  ```powershell
  $cards = 'SIMPLE  =                    T', 'BITPIX  =                  -32', 'NAXIS   =                    3',
           'NAXIS1  =                 5601', 'NAXIS2  =                 3692', 'NAXIS3  =                    3', 'END'
  [IO.File]::WriteAllText("$scratch\dest-5601x3692.fit", (($cards | ForEach-Object { $_.PadRight(80) }) -join '').PadRight(2880), [Text.Encoding]::ASCII)
  ```

  - `--dry-run`, then a copy with `-o <scratch>\out.fit`: the first line, both read lines
    (the fixture's `ROWORDER='BOTTOM-UP'`, `A_ORDER`), the selected line (`72 keys: 21 WCS, 40
    SIP, 11 optical`, V-001's counts), three card lines, the done line, the exit line;
  - a text file as source: stderr's one line, and the Warn with its stack in the log;
  - `COPYWCS_LOG=off` after deleting `log\`: no folder appears; `COPYWCS_LOG=trace`: the cards.
- The cost, on real data (the kit's rule "Measured numbers"): the fixture as source and
  `E:\Photografie\Astro\Veil\2026-10-06-3h\procesate\result.fit` (290 MB, 6016 x 4016, so with
  `--force`: this measures the log, not the WCS) as destination, always with `-o` into the
  scratchpad, never into the owner's folders; three runs with `COPYWCS_LOG=off` and three at
  Debug, alternated; paste the wall times, the done lines (the `write` stage) and the log's
  growth, then delete the outputs.
- The published console, as `kit.json` publishes it: `dotnet publish
  src\CopyWcs.Cli\CopyWcs.Cli.csproj -c Release -r win-x64 --self-contained
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o <scratch>\console`;
  `copywcs.exe` copied alone into another scratch folder and run with `--help` from a third
  working folder: `log\copywcs.log` appears beside the executable; `git status` shows the lock
  files unmoved.
- The suite's folder: `dotnet publish src\CopyWcs.Wpf\CopyWcs.Wpf.csproj -c Release -r win-x64
  --self-contained -o <scratch>\wpf`, then `Get-FileHash` of its `NLog.dll` and
  `NLog.Targets.AtomicFile.dll` against `E:\Photografie\Code\AstroDarkroom\artifacts\1.0.0\`'s:
  equal, so the suite's release does not collide.
- The renders: `COPYWCS_DUMP_SCREENS=<scratch>\screens` with `dotnet test
  tests\CopyWcs.Wpf.Tests`; look at the About window in both languages (the card, NLog's
  copyright wrapped, not cut) and send the two PNGs to the owner.
- The window by hand only when the owner is not using the PC, and only after asking: started on
  the fixture pair with `--lang ro`, one Browse, Preview, Copy, About, closed: its start,
  language, dialog, run, About and exit lines pasted; two windows at once leave both pids in one
  `CopyWcs.Wpf.log`. If it cannot be done, say so: `App`'s constructor and `OnExit` are then the
  only lines no test runs.
- Delete every scratch publish and output folder before ending the turn (a copy of the project
  in the scratchpad with `kit.json` makes the Stop hook ask for a verify there).

## Traps

- Never build or test in the main checkout `E:\Photografie\Code\WCS_Copy`. A `cd` into the
  worktree switches the desktop session's working folder: use absolute paths and `git -C`.
- Write files with Write and Edit, not shell heredocs (`%...%`, backslash paths and `\u` get
  mangled). An Edit whose `old_string` ends mid-line leaves the line's tail glued on: reflow it.
- The incremental build hides analyzer warnings: build `--no-incremental -warnaserror
  --no-restore` before the final verify.
- A commit message is one English line of at most 100 characters, no body, no trailers; count it
  first (`${#msg}`): the hook refuses a longer one silently in a filtered pipe. Never
  `--no-verify`, never `-c core.hooksPath`; the hook runs the gate and prints `== OK`.
- A static `Logger` field in `LogSetup` or `App` makes NLog load an `NLog.config` beside the
  executable before `Apply`: keep both as properties fetched on use. `MaxArchiveFiles` is 5,
  not 4. Never `KeepFileOpen = true` on NLog's plain `FileTarget` for a file two processes share.
- A `{l:T ...}` text in XAML cannot hold an apostrophe.
- The analyzers are errors (`TreatWarningsAsErrors`, `latest-recommended`): fix what they say;
  a suppression carries a comment with its reason.
- The ADR number: a parallel CopyWcs branch could take 0009; check `main` before the merge.

## Out of scope, and noticed on the way

- **A failed in-place write leaves `<destination>.tmp` beside the destination.**
  `FitsHeader.WriteWithDataFrom` creates the temp file and moves it over the target at the end,
  with no clean-up when the copy or the move throws (a full disk, the destination held open by
  another program). The original stays intact, as the README says, but the temp file stays too;
  R-012's "gone at the end" holds on success only. A fix of its own, test first, after this task;
  the log's `write ... in place through <dest>.tmp` and Error lines will show it meanwhile.
- The console's build and publish output carry no `LICENSE`; the window's do.
- The version bump is the owner's.
- DarksCheck has a plan of the same day; AstroDarkroom and AstroPhotoAnalyzer have no log yet.
  When FlatCheck moves to another NLog version, CopyWcs, LightsCheck and ImageVisualCompare move
  the same day, or the suite's release stops on the collision.

## Files

New: `src/CopyWcs.Core/Logging/LogSetup.cs`, `src/CopyWcs.Core/Logging/LogText.cs`,
`src/CopyWcs.Core/Logging/RunClock.cs`, `src/CopyWcs.Core/ThirdParty.cs`,
`src/CopyWcs.Wpf/Loggers.cs`, `THIRD-PARTY-NOTICES.md`, `tests/CopyWcs.Tests/LogCapture.cs`,
`tests/CopyWcs.Tests/LoggingTests.cs`, `tests/CopyWcs.Tests/ThirdPartyTests.cs`,
`tests/CopyWcs.Wpf.Tests/WindowLoggingTests.cs`, `docs/decisions/0009-...md`,
`docs/design/logging.md`, `docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`.

Changed: `Directory.Packages.props`, the five `packages.lock.json`,
`src/CopyWcs.Core/CopyWcs.Core.csproj`, `src/CopyWcs.Core/FitsHeader.cs`,
`src/CopyWcs.Core/WcsCopy.cs`, `src/CopyWcs.Cli/Program.cs`, `src/CopyWcs.Cli/CopyWcsCli.cs`,
`src/CopyWcs.Cli/CopyWcs.Cli.csproj` (its comment on `Main`), `src/CopyWcs.Wpf/App.xaml.cs`,
`src/CopyWcs.Wpf/MainWindow.xaml`, `src/CopyWcs.Wpf/MainWindow.xaml.cs`,
`src/CopyWcs.Wpf/ViewModels/MainViewModel.cs`, `src/CopyWcs.Wpf/ViewModels/AboutViewModel.cs`,
`src/CopyWcs.Wpf/Views/AboutWindow.xaml`, `src/CopyWcs.Wpf/Views/AboutWindow.xaml.cs`,
`src/CopyWcs.Wpf/Theme/Dark.xaml`, `tests/CopyWcs.Wpf.Tests/CopyWcs.Wpf.Tests.csproj`,
`tests/CopyWcs.Wpf.Tests/AboutTests.cs`, `tests/CopyWcs.Wpf.Tests/WindowSmokeTests.cs`,
`docs/requirements.md`, `docs/decisions/0007-...md` and `0008-...md` (Status lines only),
`docs/design/README.md`, `docs/design/run-and-console.md`, `docs/design/window.md`,
`docs/design/fits-header.md`, `docs/skills.md`, `CLAUDE.md`, `README.md`, `CHANGELOG.md`.

Untouched: `WcsMetadataKeys.cs`, `Language/`, `VersionInfo.cs`, `LicenseInfo.cs`, `Options.cs`,
the fixture, `docs/validation.*`, `kit.json`, the kit's folders.
