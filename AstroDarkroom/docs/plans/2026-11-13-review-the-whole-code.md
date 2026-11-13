# Review the whole code and list what to fix

## Context

AstroDarkroom's first code was written by Opus and had almost no code review. The owner wants
the whole project read once, critically, and one documented list of problems at the end, so that
later sessions can fix them one at a time in order of severity. The owner asked whether to split
the project into areas, so the context is not filled, or to read it in one pass, so the list is
one. Measured on 2026-11-13, blank lines included: 3,061 lines of code
(`src/AstroDarkroom.Core/` 1,485, of which `Logging/` 578; `src/AstroDarkroom.Wpf/` 1,576, XAML
included) and 2,764 of tests. That fits one session, so the answer given in chat, and approved
there, is one pass and one list, with the reading itself split by area among reviewers with a
clean context.

FlatCheck, ImageVisualCompare, LightsCheck and DarksCheck were reviewed the same way (FlatCheck's
ADR 0100, LightsCheck's ADR 0046); this plan follows theirs so the lists read alike.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## Scope

In: `src/` (the folder `kit.json` lists as code); the tests, read for one question: can this
test fail, and does it check what its name and its requirement say; and what AstroDarkroom hands
the release: `kit.json`, the two project files, `Directory.Build.props`,
`Directory.Packages.props`, `THIRD-PARTY-NOTICES.md` against the checks that bind it.

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit. The owner agreed in
  chat to leave them to a review of the kit.
- The six components: each has, or gets, its own review.
- How strong the tests are line by line: `/mutate` measures that.

## The four areas

| # | Area | Files | Lines | Design note |
|---|------|-------|------:|-------------|
| 1 | Core | `AppCatalogue`, `SuiteApp`, `SuiteFolder`, `Launcher`, `ReleaseManifest`, `Version`, `LicenseInfo`, `Prerequisites`, `VcRuntime`, `ThirdParty`, `WindowFit`, `Language/`; tests `LauncherTests`, `LanguageTests`, `VcRuntimeTests`, `VersionTests`, `WindowFitTests`, `RomanianByDefault`, `TempWorkspace` | 907 + 670 | `launcher` |
| 2 | The log, start and stop | `Logging/LogSetup`, `Logging/LogText`, `App.xaml.cs`, `Loggers`, every log call; tests `LoggingTests`, `LogCapture`, `LogFiles` | 715 + 866 | `logging` |
| 3 | The window | `MainWindow`, `AboutWindow`, `App.xaml`, `AppRow`, `DarkTitleBar`, `Icons`, `NativeProbe`, `ScreenArea`, the project file | 1,439 | `launcher` |
| 4 | The suite's checks and what the release ships | tests `SuiteTests`, `ThirdPartyTests`, `Notices`, `RepoInvariantTests`, `LicenseHeaderTests`, `TestData`; `kit.json`, the project files, the props files, the notices file, `Icon/`, the requirements against their tests | 1,228 | `launcher` |

## Steps

1. **Worktree and branch**, from the AstroDarkroom folder of the main checkout:
   `git worktree add .claude/worktrees/Review-the-whole-AstroDarkroom-code -b claude/Review-the-whole-AstroDarkroom-code main`.
   Everything below happens in that worktree. This plan is saved there.

2. **Four reviewers in parallel** (general-purpose subagents, this session's model, read-only:
   no edit, no build, no test run). Each brief gives the area's files, its notes, its tests, and
   these instructions:
   - Read `CLAUDE.md`, the notes, the area's requirements and its ADRs first; something a note,
     an ADR or a requirement says is deliberate is not a finding (name it only if the code does
     not do what the note says). Then read every file of the area whole.
   - Look for what the area can get wrong: a tool started wrongly or not at all, `release.json`
     or the setting file read wrongly, the runtime check answering falsely, a log folder that
     cannot be written, an exception that escapes or is swallowed, the two languages out of
     step, P/Invoke and DPI mistakes, a stale row, a check that passes on nothing, a file the
     release ships wrongly, code that contradicts its comment, its note or its requirement.
   - A finding needs a concrete case: which input or sequence of actions, what comes out, what
     should. No case, no finding.
   - Report each as: title; severity 1-4; `file:line`; what the code does; the case; what should
     happen; whether an existing test covers the spot; confidence (read and certain / plausible,
     needs a run). Cleanup items in a separate short list.

3. **I verify every finding in the code** before it enters the list: open the cited lines,
   follow the callers, check the note and the requirement, and for a window finding check it
   can be reached from the screen. Outcome per finding: confirmed, rejected (with the reason,
   kept out of the list), or "to measure" (a claim only a run or a screenshot can settle; the
   fix session starts by showing it). Duplicates across areas are merged.

4. **The seams, read by me** while the reviewers work: what the core returns against how the
   window uses it (null results, the exceptions `OpenApp` catches, the paths, the language at
   the click), and the start-up order in `App`.

5. **Write `docs/review/2026-11-13-code-review.md`**, in the form of LightsCheck's list: how
   the review was done, scope, how to read an entry, a summary table (ID, severity, reach,
   evidence, title, status `open`), one section per finding ordered by severity then reach
   (`CR-001`...: where, what happens, the case, what should happen, the test that would show
   it, the evidence), a cleanup section.

   Severity, by the kind of harm; the launcher computes nothing, so the first level is not a
   wrong figure but a wrong statement the user acts on:
   1. The launcher or the released suite misleads the user or ships the wrong thing: a wrong
      program started, a tool or a prerequisite said present when it is not or the reverse, a
      wrong version shown, a licence text missing from the folder.
   2. A crash, a hang, or a file written or lost where it must not be.
   3. A wrong text or a wrong display.
   4. Robustness and cleanliness.

6. **ADR 0025** (number checked again at commit time): the whole-code review of 2026-11-13, its
   list in `docs/review/`, the severity scale, one finding per task. One line in `CLAUDE.md`'s
   Layout for `docs/review/`.

7. **Gate, commit, merge proposal**: `scripts/verify.ps1` in the worktree, read the diff, commit
   on the task's branch, propose the merge and ask. No CHANGELOG line (a document, no
   behaviour); each fix adds its own.

## Verification

- Every entry of the list cites a `file:line` that I opened myself; none is copied from a
  reviewer unread.
- Every file under `src/` and `tests/` belongs to exactly one area.
- The findings document says how many reports each area returned and how many were rejected, so
  the yield is visible.
- `scripts/verify.ps1` green before the commit; `git diff main --stat` shows documents only.

## What deviated

- No plan mode: the owner approved the approach in chat ("one pass, one list, without the
  kit's scripts"), and this file was written from that answer before the reviewers' reports
  came in.
- Three claims a reading could not settle were measured, in a throw-away console program in
  the session's scratch folder, on .NET 8: the exception of a half surrogate escape (CR-002),
  the message of a start Windows refuses (CR-006), the replace over a read-only file (CR-012).
  The launcher itself was not built or run.
- No finding was rejected. The check opened the lines of every entry, the notes on tests of
  CR-020 and CR-021 included; the cleanup list was spot-checked, and two points of CR-021 that
  rest on the reviewer's memory of NLog are marked so.
- The area 4 reviewer read the untracked `artifacts/` folder of the main checkout, which was
  not in its brief, and found there the zip of CR-001; the session read the same files.
