# 0043. A diagnostic log with NLog, in the application only, started by its own entry point

Date: 2026-10-30
Status: accepted 2026-10-30
Supersedes: 0003 in part (the four test packages as the only versions, the application's lock file without a package); 0008 in part (the application referencing no NuGet package)

## Context

A bug report carried only what was on the screen. The `Cannot compare` dialog showed an
exception's message without its type, its stack or its inner exception (a codec's `COMException`,
ADR 0031); a crash left nothing; nothing recorded which files were opened, what WIC or the FITS
reader made of them, whether the pair was padded, which display mode was asked for, how long each
decode took or what the process held. The owner asked on 2026-10-30 for a log with NLog, as text,
in a `log` folder beside the executable, the file names starting with the executable's name, at
most 5 files of at most 1 MB, rolling, done as FlatCheck did it, with one criterion: a bug report
with the log attached can be diagnosed from it. The plan
(`docs/plans/2026-10-30-add-a-diagnostic-log-with-nlog.md`) put ten choices to the owner, each with
a recommendation; the owner approved it as written.

FlatCheck had measured the hard parts on 2026-10-27 (its ADRs 0070 and 0072): NLog's plain file
target loses lines when two processes write one file, closed between lines (33 to 48 of 4000, at
4.8 ms a line) or kept open (the second of two windows wrote over the first's whole log), while
`NLog.Targets.AtomicFile` loses none at 0.09 ms a line; NLog 6.2.2's `MaxArchiveFiles` counts the
file being written; a logger created before the configuration is set makes NLog load an
`NLog.config` it finds beside the executable, and one that throws took the process down before
its first line. They are taken over here, not measured again, but for the two-writer test, which
is ported.

The plan put the start of the log in `App`. Implementing it showed that `App` runs in every test
run: the WPF test thread builds an `App` for its resources (ADR 0032), and WPF's `Application`
constructor queues the application's start, which runs as soon as that thread's dispatcher pumps.
Measured on 2026-10-30: `App.OnStartup` ran there, `StartupUri` opened a real `MainWindow`
(`Application.Current.Windows` held one, `IsVisible` true, `WindowState` `Maximized`), and its
`Loaded` started an open whose dialogs are the real ones; with the log started in `OnStartup`, the
test host wrote `log\testhost.log` beside itself and the application's configuration took the
place of the tests' capture, so no window test saw a line. Put to the owner with the facts and a
recommendation, the owner chose on 2026-10-30 to start the log in the application's own entry
point.

The code review of the branch, the same day, found that the suite's release puts every tool in
one folder and refuses two different files at one path (the kit's ADR 0036; AstroDarkroom's ADR
0002), and that no other tool ships a notices file beside its executable. The owner first chose,
on 2026-10-30, that this one ship under a name of its own; told before the merge that CopyWcs, the
same day, lists NLog in its About window and keeps the licence's text beside its source, as
FlatCheck does, the owner chose the same here.

## Decision

- **Where NLog lives.** The application references `NLog` 6.2.2 and `NLog.Targets.AtomicFile`
  6.2.2, FlatCheck's versions, so the suite ships one NLog; the two move together. The core
  references no package and holds no logger: every fact it computes comes back to a loader of the
  application, which logs it.
- **Where the log starts.** In `Program.Main`, the project's `StartupObject` in place of the `Main`
  WPF generates for `App.xaml`: the log starts before `App`, its resources and its first window,
  and ends after the application's run; both are guarded, so the log never stops the tool. No
  test runs `Main`. The configuration is in code; there is no `NLog.config`.
- **The files** (R-120). `ImageVisualCompare.Wpf.log` in `log` beside the executable, or in
  `%LOCALAPPDATA%\ImageVisualCompare\log` when that folder, or the file NLog would write next,
  cannot be written; past 1 MiB the next file, `_01`, `_02` and so on; `MaxArchiveFiles` 5, which
  keeps 5 files; the next process goes on in the newest. UTF-8 without a byte order mark.
  Measured by `R120_TheLogRollsAt1MiBAndKeepsAtMost5Files` on 2026-10-30: 8 MiB of 4 KB lines left
  `_03` to `_07`, the full ones 1 052 317 bytes each.
- **Two processes** (R-123). `AtomicFileTarget`, kept open, with `ConcurrentWrites`. Measured by
  `R123_TwoProcessesWritingAtFullSpeedKeepEveryLine` on 2026-10-30: two writers of 4000 lines at
  once left all 8000 whole, interleaved both ways, in two files of 1 048 883 and 638 985 bytes.
- **The level** (R-122). `Debug`; the environment variable `IMAGEVISUALCOMPARE_LOG` names another,
  `off` creating nothing. `trace` adds every card of a FITS's headers, which the FITS loader reads
  from the file itself when it probes it, so that a header the reader refuses is written too, and
  the core needs nothing new.
- **The words** (R-121). English, invariant numbers whatever the culture, through `LogText`, never
  through `UiText`; a message the user was shown is logged as shown, its line breaks written as
  ` | ` so that the message stays on its line.
- **The lines** (R-121). The process's first and last lines; how WPF renders; the start's choices;
  each open, where it came from and what the dialogs answered; each load of a pair, with its start
  and done lines, the stages timed, one line per file probed and decoded and what its decoder made
  of it, a TIFF's own sample tags and the colour profiles a file carries among it; the padding and
  its answer; every exception with its stack beside the message shown; the pair on screen, the
  zoom each command left, the display scale, each mode switch and a refused one with why, the
  memory; About. Nothing per wheel notch, drag, scroll or repeat of a held key, which come many
  times a second and say nothing a screenshot does not.
- **Crashes.** The process's unhandled exceptions, the unobserved tasks and the window thread's
  dispatcher exceptions are written at `Fatal` (an unobserved task at `Error`) and flushed, the
  same exception once; what a crash does otherwise is unchanged.
- **The notice** (R-075). About lists the components written by others in a card of its own, as
  the About windows of FlatCheck and CopyWcs do (ADR 0045): a paragraph saying where their
  licences' whole text is, then each component of `ThirdParty.Components`, its name and its role,
  its licence, its copyright line, its address. `THIRD-PARTY-NOTICES.md` at the project's root
  holds NLog's BSD-3-Clause text and why it is compatible with GPLv3, and stays beside the source:
  nothing copies it beside the executable.

## Alternatives rejected

- **NLog in the core, as in FlatCheck.** FlatCheck's two applications share their `LogSetup`
  through their core; this project has one application, and the core stays without a package, as
  the rule "No imaging dependency" and the mutation runs want it.
- **The log started by `App`, in its constructor or in `OnStartup`, as the plan said.** It starts in
  every test run (the Context), in the test host's folder and over the tests' capture.
- **The log started by `App`, skipped when the entry assembly is not the application.** The
  application would ask whether it runs under a test, which none of its other code does, and its
  first line would still come after `App` and its resources.
- **A plain `Application` on the WPF test thread, so that `App` could start the log, as
  FlatCheck's window tests do.** It changes the fixture of ADR 0032, which the kit's rule "No test
  edited to pass" leaves to the owner; and a process's first line belongs before the application
  object anyway. The owner chose to have the fixture's own defect, a real window during the tests,
  fixed in a task of its own after this branch's merge (Consequences).
- **The notices beside the executable as `THIRD-PARTY-NOTICES.md`, About unchanged, as the plan
  said.** The first other tool to ship its own under that name would make the suite's release
  refuse the folder.
- **The same file under the tool's own name, `ImageVisualCompare-THIRD-PARTY-NOTICES.md`, the
  owner's first choice.** It cannot collide, and it carries the licence's conditions and
  disclaimer beside the executable, which About does not; but this tool alone in the suite would
  credit NLog that way, while the windows of FlatCheck and CopyWcs list it in About.
- **An `NLog.config` beside the executable.** One more file to ship and keep in step with the
  code, and one a user can break.
- **A `--log` option.** The arguments are the pair (R-002); an option would have to be taken out
  of the paths as `--lang` is, for what an environment variable does without touching them.
- **NLog's plain file target.** It loses lines with two windows open (the Context).
- **A line per wheel notch, drag, divider move, scroll or key repeat.** Hundreds of lines a minute
  that would push the open that failed out of the five files.
- **A friendly crash dialog.** A feature of its own, which the owner did not ask for.

## Consequences

- The application has packages of its own for the first time; `CLAUDE.md` says so, and the rule
  "No imaging dependency" names the log's two packages as the only ones.
- The log is the first thing to ask a reporter for: the README's "Logs" section says where it is
  and what it holds.
- Every test runs every log line on its path: the test assembly configures NLog once, at `Trace`,
  into a target that renders each event, with NLog throwing what it would swallow in a user's run
  (`LogCapture`). `R120_` to `R123_` in `LoggingTests`, `LanguageLoggingTests` and
  `WindowLoggingTests`, and `R075_` in `ThirdPartyTests`, pin the behaviour;
  `OnlyTheEntryPointConfiguresTheLog`, `TheEntryPointStartsTheLogBeforeTheApplication_AndEveryCrashIsHooked`,
  `NoLogLineGoesThroughTheInterfaceTexts` and `TheCoreHoldsNoLoggerAndNoPackage` the rules. What
  only a running process does, `Main`, `Apply` and `Close` with their real file and their hooks, is
  checked by launching the application (the rule "Run the application").
- Found on the way, for the owner, who chose on 2026-10-30 to have it fixed in a task of its own
  right after this branch's merge: since the WPF test thread exists (2026-10-22, ADR 0032), every
  test run that uses it shows a real, maximised `MainWindow` on the desktop, whose start opens the
  real Open dialog, for as long as the test host lives, and its `OnStartup` sets `UiText.Current`
  from the test host's arguments, which can race the tests of the language.
- A package the application references needs an entry in `ThirdParty.Components` and a heading
  in `THIRD-PARTY-NOTICES.md`, or `ThirdPartyTests` fails. The executable carries NLog's copyright
  line, its licence's name and its address, as FlatCheck's and CopyWcs's do, and not the
  licence's conditions and disclaimer, which BSD-3-Clause asks a binary distribution to carry as
  well: they are in `THIRD-PARTY-NOTICES.md`, beside the source.
- A self-contained release also carries the .NET runtime and WPF, under the MIT licence, whose
  notice is not in this project's notices: a question for the whole suite, apart from this.
- A release with the log warrants a minor version; the bump is the owner's.
