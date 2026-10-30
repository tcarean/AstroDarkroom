# 0009. A diagnostic log with NLog, as FlatCheck's, written at the copy's seams, and NLog's notice in the About window

Date: 2026-10-30
Status: accepted 2026-10-30
Supersedes: 0007 in part (the About window without the third-party card); 0008 in part (the About tooltip without the components)

## Context

A bug report about CopyWcs carried only what was on the screen. A refusal was one line built from
an exception's message alone (`WcsCopy.Run` kept `ex.Message`, so the type, the stack and the
Windows error code were lost); the window's header facts and the About window's link swallowed
their exceptions; an exception nobody caught ended the console with the runtime's dump and the
window with no word. Nothing recorded what was read, which cards were copied over which, what
was written or how long the write took, and "the WCS is wrong after the copy" can only be
answered from those cards. On 2026-10-30 the owner asked for a log with NLog, as text, in `log/`
beside the executable, the names starting with the executable's name, at most 5 files of 1 MB
per executable, rolling, done the way FlatCheck does it (its ADRs 0070 and 0072), so that a bug
report can be diagnosed from the log. Two facts of this project shaped it: CopyWcs had no package
(ADR 0007 and 0008 dropped the siblings' third-party card and the tooltip's "components" for that
reason), and AstroDarkroom bundles every window, this one among them, into one flat folder that
refuses a file of other bytes at a path already taken (the kit's ADR 0036), where FlatCheck's
`NLog.dll` already is. The plan `docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md` put two
questions to the owner with a recommendation each; the owner chose both recommendations.

## Decision

- **FlatCheck's design, taken as it is** (its ADRs 0070 and 0072, not written again): the core
  references `NLog` and `NLog.Targets.AtomicFile` and logs at its seams; the two applications
  configure NLog in code, `LogSetup.Apply` first and `LogSetup.Close` last, each in a `try` that
  swallows anything, so the log never stops the tool; no `NLog.config`; one `AtomicFileTarget`,
  kept open, `ConcurrentWrites`, so two processes of one executable lose no line; NLog 6's own
  roll-forward past 1 MiB, `MaxArchiveFiles = 5` keeping five files, the one being written
  included; the next process goes on in the newest; UTF-8 without BOM; a fallback under
  `%LOCALAPPDATA%` when the folder beside the executable cannot be written, said in the first
  line; Debug by default, an environment variable for the level; the log's words English with
  invariant numbers, never through `T(...)`, an exception's message and a usage error's line as
  the user saw them; the unhandled exceptions at Fatal, written once, the crash behaviour
  unchanged; a run held in a disposable `RunClock`; the tests' capture at Trace configured once per
  test assembly. FlatCheck's measurements stand for this project; the tests that pin them run
  here on CopyWcs's configuration.
- **CopyWcs's values.** The variable is `COPYWCS_LOG`, the fallback `%LOCALAPPDATA%\CopyWcs\log`,
  the files `copywcs.log` and `CopyWcs.Wpf.log`. The versions are **6.2.2** for both packages,
  equal to FlatCheck's, and they move with FlatCheck's: the suite's folder holds one `NLog.dll`.
- **`Logging/` is CopyWcs's own, adapted from FlatCheck's** in this project's style (nullable,
  file-scoped, braces), with FlatCheck's names, constants and method shapes, so a fix found in one
  tool carries to the other; it is not a copy under ADR 0007's rule for the window's pieces.
- **The seams.** `FitsHeader` writes each header read (the file's size, the header's cards,
  blocks and data offset, `BITPIX`, `NAXIS`, the sizes, `EXTEND`, `CTYPE1`, `CTYPE2`, `ROWORDER`,
  `A_ORDER`, `PLTSOLVD`), the `HISTORY` card appended, the write and the file written, and at Trace
  every card read and written. `WcsCopy.Run` writes the run between its start and done lines, with
  the time of each stage: the warnings, each refusal with its reason in English and its exception,
  the selection, and **at Debug, the default, every card copied with the destination's card it
  replaces**. The console logs a usage error, the help and a refused `--lang`; the window its
  start (the language and the files from the command line), the files picked in the dialogs,
  About, and the two quiet catches it had (the facts, the link). `WcsMetadataKeys`, `Language/`,
  `VersionInfo`, `LicenseInfo` and `ThirdParty` hold no logger.
- **`Main` is a shim around the run** (the owner's answer, 2026-10-30): FlatCheck's guarded
  `Apply`, one `CopyWcsCli.Run`, `Close` in a `finally`; `CLAUDE.md`'s rule "`Main` is a one-line
  shim" is amended to say so, its purpose kept (console behaviour lives in `CopyWcsCli` or
  `Options`, testable in process).
- **NLog's notice is in the About window** (the owner's answer, 2026-10-30): the siblings'
  third-party card comes back (DarksCheck's markup and `ThirdParty.cs` shape, FlatCheck's NLog
  entry: name, role, licence, copyright line, link), the About button's tooltip goes back to the
  siblings' `Version, licence, components (F1)`, and `THIRD-PARTY-NOTICES.md` holds the licence's
  whole text in the repository. No notices file is shipped beside the executables.

## Alternatives rejected

- **`Microsoft.Extensions.Logging` with a provider.** Two packages for one file sink, and the
  owner asked for NLog by name.
- **An `NLog.config` beside the executables.** A file to ship, keep in step with the code and
  break; the configuration is a few properties.
- **Logging inside `WcsMetadataKeys` or `Language/`.** The run logs what they return.
- **A `--log` console option.** It moves the console's contract, R-002's usage and its tests, for
  what an environment variable does without touching them.
- **NLog's plain file target, closed between lines or kept open.** FlatCheck measured both losing
  lines between two processes (its ADR 0070).
- **One file per process.** Five runs would fill the five files, and a report would come in pieces.
- **The copied cards at Trace only.** The first log a user sends would not hold the one record a
  wrong WCS can be diagnosed from; at about 110 bytes a card, a copy writes some 9 KB at Debug.
- **A notices file beside each executable, About unchanged.** In the suite's flat folder one
  tool's notices would collide with another's or stand for them all, and an executable copied
  alone would carry no NLog notice, which BSD-3-Clause asks of a binary.
- **A one-line `Main` through a wrapper in the core.** Only the console could use it; the window's
  `Apply` and `Close` live in `App`'s constructor and `OnExit`.
- **A logging library shared with FlatCheck.** ADR 0007's reason: copies of a few small files cost
  less than a package with a version.

## Consequences

- The tool's first package, with `THIRD-PARTY-NOTICES.md`, `ThirdParty.cs` and `ThirdPartyTests`
  (every package of the core claimed exactly once, each with a heading in the notices file); the
  About window shows the card in both languages.
- The log is the first thing to ask a reporter for; the README's "Logs" section says where it is,
  what it holds (the paths and the copied cards, the site's coordinates among them when the source
  has them) and how to set the level.
- Every test runs every log line on its path (`LogCapture`); R-040 to R-046 are pinned by
  `LoggingTests` and `WindowLoggingTests`, the two repository rules by
  `OnlyTheTwoEntryPointsConfigureTheLog` and `NoLogLineGoesThroughTheTranslation`; R-035 and R-038
  by `ThirdPartyTests`, `AboutTests` and `WindowSmokeTests`.
- When FlatCheck moves to another NLog version, CopyWcs moves the same day, or the suite's release
  stops on the collision. The mechanics are in `docs/design/logging.md`.
