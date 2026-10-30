# Fix the diagnostic log review findings

Branch `claude/Fix-the-diagnostic-log-review-findings`, worktree
`.claude/worktrees/Fix-the-diagnostic-log-review-findings` (the project is under `FlatCheck/` in
it). Written for an implementer who has this file, `CLAUDE.md` and the repository. Saved to
`docs/plans/2026-10-27-fix-the-diagnostic-log-review-findings.md` once approved.

## Context

The diagnostic log (ADR 0070, R-109 to R-112, `FlatCheck.Core/Logging/`) merged into `main` on
2026-10-27 (108c4ca). A code review the same day listed 16 code defects (A), 6 test weaknesses
(B), 9 documentation errors (C) and 11 clean-ups (D). Every point was verified on the worktree
before this plan: none is wrong; A13 and D2 are partly right; A15 is a false comment as stated.
The verification found more cases of the same defects, listed under their points below.

## Owner's decisions (2026-10-27)

Put to the owner with the facts, two options each and a recommendation; the owner chose every
recommendation, option **(a)**, on 2026-10-27. Implemented without asking again.

| # | Decision | Recommendation, chosen |
| --- | --- | --- |
| A2 | Exceptions out of `OpenAsync` | **(a)** a `catch` in `OpenAsync`: `Log.Error`, a problem card (new `T` pair "The folder could not be read" / "Folderul nu a putut fi citit" + `TabViewModel.Describe(e)`), `_session = null`, pending cancelled; the Synthetic prefill moves its `ReferenceRawPath` read inside its `try`. (b) await the task in `MainWindow` so the crash hooks see it: the window dies on a folder it cannot list. |
| A3 | A lens without a vignetting profile | **(a)** refuse with `SyntheticReport.NoProfile(lens)`, exit 1 (an input refusal, like a missing focal); R-071 extended; the exit table comment and `thresholds.md` updated. Text: EN `SYNTHETIC FLAT: the lens "<key>" has no vignetting profile (its file describes distortion or colour only); choose another lens.` RO `FLAT SINTETIC: obiectivul "<key>" nu are profil de vignetare (fisierul lui descrie doar distorsiunea sau culoarea); alege alt obiectiv.` (b) hide such lenses from `--lenses` and the window's list: needs parsing all 3531 files at `Locate`, today lazy. |
| A6 + D7 | Refusals in the log; one logger style | **(a)** one logger per tab, `Window.<Tab>` (an instance field in `TabViewModel`), messages lose the hand-written `X tab:` prefix; `Refuse(title, text, why)` = `Log.Info("refused: " + why)` + `Fail`, used by the four callers without a line; `RunGuardedAsync` writes `run N refused in N ms` instead of `done` when the work ended with an explanation shown. Every logger a `static readonly Logger Log` field (`using NLog`), names as constants, no inline `GetLogger`. `WindowLoggingTests`' expected strings change with the format (test-only). (b) keep one `Window` logger and a `Line()` helper that prefixes `TabName`. |
| A7 | An unreadable Adobe subfolder | **(a)** `new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }` (the `SearchOption` overload skips nothing; a fresh `EnumerationOptions` would skip Hidden and System), the `try/catch` kept for the root; `lens-profiles.md` reworded; ADR 0066 lines 53-55 get a `(Corrected 2026-10-27: …)` note, since the sentence was never true. (b) keep the code, correct the docs to say the whole root is lost. |
| A8 | `skipped=` always 0 | **(a)** remove the dead branch, the `skipped` field, `ProfileSources.AdobeFilesSkipped` and its constructor parameter, the dead `(N skipped)` branch of `SyntheticReport.SourcesSummary` (never printed, so no golden moves), the two asserts; `LensCatalog.Parse` logs at Debug a file that parses to zero vignetting profiles (the A3 lens). Stale comments at `ProfileLocator.cs:156-158, 170-171` and `LcpReader.cs:26-27` corrected. (b) count after parsing: the catalog parses lazily, so the count cannot be in `Locate`'s line. |
| A9 (optional) | `RunClock` disposable | **(a)** `IDisposable`, `using var clock = RunClock.Start(...)` at the ten sites; a `Dispose` without `Done` writes Info `<run> ended without an outcome after N ms (<stages>)`, so a run that throws leaves its stages and its time (the console's `run failed` line has neither). Info, so `R110_AFailingRunLogsTheExceptionWithItsStack`'s single Error holds. (b) leave it: the caller logs the exception. |
| A14 | The log can stop the tool at start | **(a)** `try/catch` around `LogSetup.Apply` in `Program.Main` and `App`'s constructor (the tool runs without a log, nothing on stdout or stderr); `LogSetup.Log` becomes a lazily fetched logger so the configuration is assigned before any logger exists and NLog's autoload of an `NLog.config` is prevented, which makes the `Off` comment true; measured with a scratch console beside an `NLog.config` that throws (`throwConfigExceptions="true"`), before and after. (b) only the `try/catch`, the comment and the note corrected to say the autoload is not prevented. |
| B1 | `Analysis/` and `Report/` log indirectly | **(a)** ADR 0070 gets a `(Restated 2026-10-27: …)` note: those folders write no log line themselves; a catalog they call may warn about an Adobe file it cannot read. `NothingThatComputesOrWordsTheReportLogs` greps `NLog`, `Logger`, `LogManager`. (b) parse every candidate profile in the run before calling into `Analysis/` and `Report/`: the runs would have to know which lenses the comparison will touch. |
| B3 (second half) | The two older Unix-unsafe tests (`LensProfileTests.cs:250`, `:1211`) | **(a)** fix them too, two lines, forward slashes. (b) leave them. |
| C3 | What the docs promise about the language | **(a)** README and CHANGELOG say: the log's own words are English with invariant numbers; an error's or a refusal's message appears as the user saw it, so in Romanian under `--lang ro`. The plan's deviations list gains the D8 change. `FlatCheckCli.cs:114` logs the refusal the user saw (today hard-coded English, unlike the two other refusals). (b) nothing. |
| C8 | R-110 promises every exception | **(a)** Debug lines at `SplitPlan.SizeOf`, `Options.SamePath`, `SirilInstall.Normalise` and the two cancellations (`MainViewModel.cs:276`, `SyntheticTabViewModel.cs:510`); `--lenses` gets a `RunClock` start and done; R-110 restated to except a cancellation the user caused, a race at shutdown (`ObjectDisposedException`, `Win32Exception`, `TaskCanceledException` on close) and the log's own checks; ADR 0070 `(Restated 2026-10-27: …)`; README and CHANGELOG reworded. (b) leave R-110 and log every catch, the shutdown ones included. |
| D1 | `ModeOf` is a second dispatch | **(a)** `Options.Mode`, an enum with the precedence of R-003, computed once in `Options.Parse`; the dispatch is a `switch` on it and the log line is `mode=<m> input=<path> lang=<l>`; `Describe(Options)` removed (the first line has the arguments verbatim); `process.md` says so. (b) keep both and add a test that `ModeOf` agrees with the dispatch. |
| D11 | Two hooks for one crash | **(a)** keep both hooks and the dedup: dropping the dispatcher hook changes a bullet of ADR 0070's decision, which needs a superseding ADR for one field and four lines; a test of the dedup is added instead. (b) keep only the `AppDomain` hook, remove `lastFatal`, new ADR superseding that bullet. |

## The work, in the brief's order

Every code defect starts with a failing test; every requirement change precedes the code; every
user-visible text is a `T("english", "romana")` pair, no diacritics. Build and test only in the
worktree. Commit after each group with the gate green.

### A1 to A6

- **A1** `MainViewModel.OpenAsync`: `Describe` moves after the card and `Preprocess.Attach` in
  the problem branch, and after the five `Attach` calls in the usable branch; each count goes
  through a local `Count(Func<int>)` that catches `IOException` and `UnauthorizedAccessException`
  and writes `unreadable (<Type>)`; the whole call sits in a `try` that logs at Debug with the
  exception. Test (`MainViewModelTests`, Windows guard, the ACL technique of
  `SirilInstallTests.cs:89-115`): `flats/` present, `process/` without list rights, no master:
  the card "flat master is missing" shows. `logging.md`'s `MainViewModel` row updated.
- **A2** as decided. Test: master present, `flats/` without list rights: the new card shows,
  the log has an Error line, `HasProblem` is true, no tab is busy.
- **A3** as decided: `all` computed before the exit block of `SyntheticRun.Execute`, `refusal`
  and `why` set when `all.Count == 0`. Requirement first: R-071 extended with the lens without a
  profile (exit 1). Tests: `LensProfileTests`, a catalog with a stub whose file has no
  `VignetteModel` (the console refuses with the text and exit 1; nothing written);
  `SyntheticTabTests`, the tab shows the refusal. Comment at `SyntheticTabViewModel.cs:364`
  corrected (C9); the exit table comment in `FlatCheckCli.cs:31-33`; `docs/design/thresholds.md:89-91`.
  New ADR 0071 (the number checked against `main` before the merge): a lens whose file carries
  no vignetting model is refused, not hidden; why (listing needs no parse; 1275 of 3531 files
  on the author's machine).
- **A4** `LogText.Exposure` writes `c.Label` (`mono`, `R`, `CFA[0,1]`) instead of `c.Colour`.
  Test in `LoggingTests`: `--expo` through `FlatCheckCli` on a CFA FITS without `BAYERPAT`
  under `LogCapture`; no captured message contains `'\0'`.
- **A5** `SessionMessages.AboutThirdPartyLead(int components)` takes
  `ThirdParty.Components.Count(c => !c.IsData)` from `AboutViewModel`, written as a word in both
  languages (`two`..`nine` / `doua`..`noua`, the digit beyond), and mentions the database the way
  the README does. Test in `AboutTests`: the lead holds the word for the current count.
- **A6** as decided, with D7.

### The rest of A

- **A7** as decided. Test (`LensProfileTests`, Windows guard): a root with two subfolders, one
  without list rights: the other's profiles are found, a Warn is not required. Docs: C9.
- **A8** as decided.
- **A9** `SmoothRun` and `SyntheticRun` path overloads: `RunClock.Start` before `Load`,
  `Lap("load")`, the refusal through `clock.Done("refused: " + why)`; a private body taking the
  clock, as `StructureRun.AnalyzeTimed` does, so the frame overloads (kept: the console's
  `--scale auto` and the tests use them) write one start line. The start text of Synthetic in
  one helper for both overloads. The window's Smooth tab (`SmoothTabViewModel.cs:325`) takes the
  same path. Tests in `LoggingTests`: `--smooth` on a raw-like refusal has start and done lines
  with `load`; `--synthetic` on a non-FITS has a start line. Then, if A9's option (a): `RunClock :
  IDisposable`, `using var` at the ten sites, test: a run that throws leaves the "ended without
  an outcome" line with its stages; `RunClock`'s summary and `logging.md` updated.
- **A10** one `wrote <file> (<KiB>)` line per file in the Structure and Split tabs
  (`LogText.FileKiB` for the report, `KiB(png.Length)` for the PNG); `LogText.Seconds(ms)` →
  `12.9 s` and `LogText.Ms(ms)`; Split's two lines, `SirilRunViewModel.Seconds` and
  `LogSetup.Close` use them (D8). Test: `WindowLoggingTests` or `SplitTabTests` on the format.
- **A11** `LightSeriesRun`: `Lap("load")` after the first frame is loaded, and frame 1's time
  includes that load (its load time is measured and added). Test: the first frame's `in N ms` is
  at least its load time (a capture on a two-frame series; assert the `load` lap exists).
- **A12** `SirilRunViewModel`: `_currentCommand` set in `Apply`, `CurrentCommand` uses it,
  `of N` only when the script lists commands. Test in `SirilRunTests`: an unreadable script,
  the log's `run ended` line names the verb.
- **A13** `double black0 = c.Black;` before the lambda in `RawReader.Decode`; the test
  `HoldsNoLibrawTypedFields` walks nested types recursively and generic arguments, so it fails
  on the display class first.
- **A14** as decided. Test: `LogSetup.Apply` with a `Plan` that throws cannot be made in-process;
  the measurement with the scratch console is pasted at wrap-up.
- **A15** the comment: `// what the log's last line reports when the run throws past RunIn's
  catch; the process itself exits with the runtime's crash code`.
- **A16** `CanWrite` also opens, for append, the file NLog would write next (`<name>.log`, or
  the highest `<name>_NN.log`), `FileShare.ReadWrite | Delete`, when it exists. Tests: a
  read-only newest file falls back; a file another `LogFactory` holds open does not.

### B

- **B1** `NoLogLineGoesThroughTheTranslation`: after D7 every logger is a field named `Log`;
  the test matches a whole call (`Log.<Level>(` up to its `;`, multiline) against `\bT\(`, and
  asserts `GetLogger(` appears only in field initialisers. `NothingThatComputesOrWordsTheReportLogs`
  greps the three tokens. `OnlyTheTwoEntryPointsConfigureTheLog` also greps `LogManager.Configuration`
  and `LogManager.Setup(` outside `LogSetup.cs` and `LogCapture.cs`. `R110_TheLogIsTheSameInBothLanguages`
  becomes a theory over structure, expo, light, synthetic (a refusal), against, smooth (a
  refusal), comparing the messages after the same normalisation, and excluding the refusal
  message itself (as the user saw it, per R-110).
- **B2** the second half asserts the same five names before and after and that the newest grew.
- **B3** forward slashes in the three Windows rows; the two older tests if decided.
- **B4** `RepoInvariantTests.CsSources` made internal with a folder filter; `CodeFiles` uses it.
- **B5** two dedicated threads released by a `Barrier`; assert the two writers' lines interleave
  in the file (an A line after a B line and the reverse).
- **B6** one file-level `StubTabViewModel` (the name feeds `TabName`) with `Enter`, used by both.

### C

- **C1** ADR 0070 line 45: `(Corrected 2026-10-27: 48, 33 and 42; the first run lost 48, as
  R-112 and the package notice say.)`
- **C2** `LogSetup.cs:21-22`, `FlatCheck.Core.csproj:53-54`: the tests configure a capture at
  Trace; ADR 0070 line 30-31 `(Restated 2026-10-27: …)`; `R-109 to R-112` in the seven places.
- **C3** as decided.
- **C4** README 1374-1397 and CHANGELOG 17-18: `fatal` in the list; 1 MiB per file, each ending
  one line past it, about 5 MiB per program; `setx FLATCHECK_LOG trace` (or the user's
  environment variables) for a window started from Start or AstroDarkroom, `set` only for the
  programs of that cmd window.
- **C5** `logging.md` keeps how the parts fit, the measured file sequence and the seam table,
  and points to ADR 0070 for the decisions and the figures (the target, the counts, the Fatal
  dedup, `LogCapture`'s reason, the line's fields). Step 2 says `AtomicFileTarget`.
- **C6** `dependencies.md:49-50`: the alternatives the ADR names; `docs/design/README.md:35`
  names ADR 0070 and NLog.
- **C7** `process.md`: `Options.Mode` is where a new mode is added (D1), the log line takes it.
- **C8** as decided.
- **C9** with A3 and A7.

### D

- **D2** `SmoothRun.Refusal(f, path, out string why)` replaces `RefusalKind`; `SyntheticRun`'s
  `ChooseLens` and `Complete` return the English `why` with the text.
- **D3** one `StructureRun.Analyze(path, query = null, clock = null)`.
- **D4** `LogCapture`: nested captures and `Captured.Logger` removed; `logging.md:102` with it.
- **D5** `Apply` always assigns `Build(...)` and returns after it for `Off`; `applied` replaced
  by the clock (`Interlocked.CompareExchange`).
- **D6** `LogText.FileSize(path, Func<long,string>)` behind `FileKiB`/`FileMiB`; `Lens(id, mount,
  name)` and `Range(min, max, aperture)` shared by `Frame` and `Shot`; `StructureReport` exposes
  `EnglishCorner(key)` and `CornerName` becomes `T(EnglishCorner(key), key)`, `LogText` uses the
  English one.
- **D7** with A6. **D8** with A10.
- **D9** `SirilScriptRun` logs `psi.FileName` and `psi.ArgumentList` joined and quoted.
- **D10** `LensCatalog.Lenses` memoised (`Lazy`); a light series logs the FITS keys of its first
  frame only (`ImageLoader.LoadImage(path, quiet: true)` passes a flag to `FitsReader`, which
  logs the keys at Trace then); the Siril progress line behind `IsTraceEnabled`.
- **D11** as decided.

## Files

Code: `FlatCheck.Core/Logging/{LogSetup,LogText,RunClock}.cs`, `Runs/{SmoothRun,SyntheticRun,
StructureRun,LightSeriesRun}.cs`, `LensProfiles/{ProfileLocator,LensProfile,LcpReader}.cs`,
`Report/{SyntheticReport,StructureReport}.cs`, `RawReader.cs`, `FlatCheck/{Program,FlatCheckCli,
Options}.cs`, `FlatCheck.Wpf/{App.xaml.cs,MainWindow.xaml.cs}`, `ViewModels/{MainViewModel,
TabViewModel,*TabViewModel,SirilRunViewModel,AboutViewModel}.cs`, `Session/{SessionMessages,
SirilScriptRun,SplitPlan,SirilInstall}.cs`, `DarkTitleBar.cs`, `AboutWindow.xaml.cs`.
Tests: `FlatCheck.Tests/{LoggingTests,LogCapture,LensProfileTests,RawReaderTests,RepoInvariantTests,
OptionsTests}.cs`, `FlatCheck.Wpf.Tests/{WindowLoggingTests,TabViewModelTests,MainViewModelTests,
AboutTests,SirilRunTests,SyntheticTabTests}.cs`.
Docs: `docs/requirements.md` (R-071, R-110), `docs/decisions/0070` (dated notes), `0066` (note),
new `0071`, `docs/design/{logging,lens-profiles,dependencies,process,thresholds,README}.md`,
`README.md`, `CHANGELOG.md`, `docs/plans/2026-10-27-add-a-diagnostic-log-with-nlog.md`
(deviation D8), this plan saved under `docs/plans/`.

## Verification

- `scripts/verify.ps1` green in the worktree after each group; `dotnet build --no-incremental
  -warnaserror --no-restore` before the last verify; `scripts/check-design.ps1 -Since main`.
- Golden files: no diff expected (the `(N skipped)` branch was dead; `--lenses` output unchanged).
- By hand, pasted at wrap-up: the console on a lens without a profile (the refusal, exit 1); the
  window on a session whose `flats/` is unlistable (the card, the Error line); `--expo` on a CFA
  without `BAYERPAT` and `grep` reading the log as text; the A14 measurement with the scratch
  console; a `--smooth` refusal's start and done lines.
- No formula moved: `/validate` not needed.

## Outcome and deviations (2026-10-27)

Implemented as planned, on `claude/Fix-the-diagnostic-log-review-findings`, in four commits
(A1 to A6; A7 to A16; B; C and D together, since D1's `Options.Mode` and C7's note of it, and
C8's lines and D's helpers, share files) and a wrap-up commit (the design notes, R-080, the
README, ADR 0072, this section). Every finding was verified on the code first: none was wrong; A13 and D2
were partly right; A15 was a false comment as stated. What differs from the plan, and why:

- **ADR 0072 written at wrap-up.** The plan named only ADR 0071 (A3); the other choices the
  owner made in chat with a "why" (A2, A6 with D7, A9, A14 with A16, D1, C8, D11) are a
  decision under the kit's rule "Decisions", so they have their ADR, with the rejected options.
- **R-080 extended at wrap-up.** The card for a folder that cannot be read is a window session
  rule, so R-080 now names it and the two window tests of A1 and A2 carry its ID; the plan had
  put the behaviour under R-110 only.
- **By hand, in the real window** (2026-10-27, the worktree's Release build, a scratch session
  with its master in `process/` and `flats/` denied to the user): the card "The folder could not
  be read" with the folder, the system's message and the hint, and in `FlatCheck.Wpf.log`
  `Window session ...: usable`, then `ERROR ... the opening failed; the card shows the message`
  with `System.UnauthorizedAccessException` and its stack through `SirilSession.RawFlats`,
  `ExposureTabViewModel.Attach` and `MainViewModel.OpenAsync`, then `Process exit 0`.
- **By hand, the console** (same build): `--synthetic --lens "Canon RF 100-400mm F5.6-8 IS USM +
  EXTENDER RF2x"` on the author's Adobe folder printed the new refusal and exited 1, with
  `LensCatalog ... holds no vignetting profile` and `synthetic done refused: the lens has no
  vignetting profile` in the log; `--expo` on a CFA without `BAYERPAT` logged
  `channels=[0:CFA[0,0]=...` and the file held no NUL byte; `--smooth` on a cube logged
  `smooth start` and `smooth done refused: a cube of 3 planes in 27 ms (load 26 ms)`.

- **D4 kept `Captured.Logger`.** The plan said it was never read; the per-tab loggers of A6
  made it the thing the window's tests assert on, so only the nested captures went.
- **A11 times the first frame's load twice over**: as its own `load` stage, and inside the
  first frame's line, so that line reads like the others'.
- **D10's three enumerations of `flats/`** became one: `Describe` reads the raw flats and the
  reference raw off the one list, as the session itself does.
- **The dated ADR notes open `## Alternatives rejected`** of ADR 0066 and ADR 0070, each quoting
  the Decision's sentence it corrects: the kit's ADR 0024 puts a note on Decision text right
  after that section (precedents: the kit's ADR 0016, APA's ADR 0005), and
  `check-decisions.ps1` refuses one inside it.
- **`R-109 to R-111` became `R-109 to R-112`** in the code, the tests, the CHANGELOG and the
  package notice; the previous plan keeps its own text (history).
- **A clean build at wrap-up found CS0419** (`dotnet build --no-incremental -warnaserror`):
  eight crefs to `ImageLoader.Load` had become ambiguous when D10 gave it a second overload,
  and the gate's incremental build had not shown it. They name `Load(string)` since the
  wrap-up commit; 97808e8 alone does not build clean.
- **A subagent review of the branch (2026-10-30)** found seven points, none grave, all taken:
  the window's `Close` was not guarded like `Apply`; `App`'s logger was a static field on the
  type whose constructor calls `Apply`, now fetched on use like `LogSetup`'s; a doubled doc
  comment in `SessionMessages`; an Adobe root the user may not list was skipped by
  `IgnoreInaccessible` in silence (measured by `R107_AnUnlistableRootIsLeftOutWithAWarnLine`,
  which failed before the fix: the root is opened once without the option first); the tab's
  `refused` line tested `HasError`, which `Fail` sets too, now a flag `Refuse` sets; the
  planned window test of the no-profile refusal was missing, added; and `logging.md`
  contradicted itself on the logger declaration. Noticed, pre-existing and left alone:
  `OpenAsync`'s `finally` clears `IsRunning` for a superseded chain too.
- **Two CHANGELOG lines were left out at wrap-up**: the About window's "three components" and
  the card lost to the Debug line were never in a release (both came with the unreleased log),
  so they are not fixes a reader of the version history can meet.
- **Measured (A14)**, 2026-10-27, a scratch console beside an `NLog.config` naming an unknown
  target with `throwConfigExceptions="true"`: with the logger in a static field,
  `TypeInitializationException` / `NLogConfigurationException: Failed loading NLog
  configuration ... unknown type-alias: 'NoSuchTargetType'` before `Main`'s body; with the
  logger fetched on use after the configuration is set, `ok, the tool would run`.
- **Found on the way, not fixed (outside the task):** `ExposureTabTests.PickingAnotherFileRecomputes`
  failed once in the commit gate and passed in every other run. Setting `Selected` starts a run
  nobody awaits and `RecomputeAsync` starts a second one on the same file; a superseded run
  still writes its figures (`Populate` runs inside the guarded work, which the generation check
  does not cover), so under load the bright file's late run can overwrite the dim file's
  `Centre`. A race in the view-model as much as in the test; proposed as a task of its own.
