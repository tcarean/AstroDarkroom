# Review the whole code and list what to fix

## Context

CopyWcs's first code was written by Opus and had almost no code review. The owner wants the
whole project read once, critically, and one documented list of problems at the end, so that
later sessions can fix them one at a time in order of severity. Measured on 2026-11-16, blank
lines included: 4,447 lines of code (`src/CopyWcs.Core/` 2,271, `src/CopyWcs.Cli/` 382,
`src/CopyWcs.Wpf/` 1,794) and 6,077 of tests.

LightsCheck, FlatCheck, DarksCheck, AstroDarkroom and ImageVisualCompare were reviewed the same
way (LightsCheck's ADR 0046); this plan follows theirs so the lists read alike. CopyWcs is a
quarter of LightsCheck, so four areas are enough.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## What is read, and from where

- The code as it is on `main` at `6a470cee`.
- Another session has the Help button of CopyWcs in work, not committed, in the worktree
  `Add-a-help-button-to-CopyWcs` (it edits `MainWindow.xaml`, `MainWindow.xaml.cs`,
  `Theme/Dark.xaml`, and adds `Presentation/ExternalLink.cs` and `Presentation/Manual.cs`).
  That work is **not** reviewed here: it is not on `main`, and it gets its own review before
  its merge. The list says so; after that merge the window's line numbers move, and an entry is
  found again by the names it gives.
- That task holds ADR 0016, so this one takes **ADR 0017** (the ADR check refuses a duplicate
  number, not a gap); the number is looked at again at commit time.

In: `src/` (the folder `kit.json` lists as code), and the tests of each area, read for one
question: can this test fail, and does it check what its name says.

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit.
- `prototype/copy_wcs.py` - frozen (ADR 0001). It is read by the area 1 reviewer only to
  compare: a behaviour the port lost or changed without a requirement or an ADR saying so is a
  finding against the C# code.
- `Icon/make_icon.py` - a drawing script, not run by the tool.
- How strong the tests are line by line: `/mutate` measures that.

The window's pieces copied from the sibling projects (ADR 0007: the theme, `Presentation/`)
are read, because CopyWcs runs them, but a defect there is fixed in the sibling first. Such a
finding goes in a section of its own, as `CP-N`, with the entry of the sibling's list that
already names it, when one does.

## The four areas

Each reviewer reads every file of its area whole, plus the design note named, the area's
requirements in `docs/requirements.md`, and the area's tests.

| # | Area | Files | Lines | Note | Tests |
|---|------|-------|------:|------|-------|
| 1 | The FITS header engine | `FitsHeader.cs`, `AsciiText.cs`, `WcsMetadataKeys.cs` | 749 | `fits-header.md` | `FitsHeaderTests`, `WcsMetadataKeysTests`, `AsciiTextTests`, `ValidationTests`, `FitsTestData` |
| 2 | The run, the console, the language | `WcsCopy.cs`, `Language/`, `CopyWcsCli.cs`, `Options.cs`, `VersionInfo.cs`, `LicenseInfo.cs`, `ThirdParty.cs` | 1,095 | `run-and-console.md` | `WcsCopyTests`, `CopyWcsCliTests`, `OptionsTests`, `LanguageTests`, `VersionTests`, `ThirdPartyTests`, `LicenseNoticeTests` |
| 3 | The window | everything in `src/CopyWcs.Wpf/` except `Loggers.cs` | 1,763 | `window.md` | `tests/CopyWcs.Wpf.Tests/` except `WindowLoggingTests` and `AsciiTextTests` |
| 4 | The diagnostic log | `Logging/` (three files), `Program.cs`, `Loggers.cs`, the log calls of `App.xaml.cs` and of the other files | 840 | `logging.md` | `LoggingTests`, `LogCapture`, `LogFiles`, `WindowLoggingTests` |

## Steps

1. **Worktree and branch**, from the `WCS_Copy` folder of the main checkout:
   `git worktree add .claude/worktrees/Review-the-whole-CopyWcs-code -b claude/Review-the-whole-CopyWcs-code main`.
   Everything below happens in that worktree; nothing is built in the main checkout. This plan
   is saved there as `docs/plans/2026-11-16-review-the-whole-code.md`.

2. **Four reviewers in parallel** (general-purpose subagents, this session's model). They edit
   nothing in the repository, build nothing and run no test there; a throw-away script to put a
   figure on a finding is allowed in the scratchpad only, and is said in the report. Each brief
   gives the area's files, its note, its requirements, its tests, and these instructions:
   - Read every file of the area whole. Read `CLAUDE.md`, the note and the requirements first;
     something a note, an ADR or a requirement says is deliberate is not a finding (name it only
     if the code does not do what the note says).
   - Look for: a destination file written wrong (a card missing, doubled, moved, reformatted or
     cut; the END card or the 2880-byte padding wrong; the data offset moved; data bytes
     touched; a header that grows or shrinks by a block handled wrongly; a file left
     half-written when the write fails), a copy said to be done that was not or the reverse, a
     copy refused or allowed wrongly (the grid check, the mirror check, the dropped keys),
     continuation and commentary cards (`CONTINUE`, `HISTORY`, `COMMENT`, blank keys,
     `HIERARCH`), quotes inside string values, keys matched by prefix where an exact match is
     meant, off-by-one and overflow on a large or a truncated file, crashes and unhandled
     errors (a locked, read-only, missing or zero-length file, the same file given twice),
     resource leaks, races and thread affinity (window), stale state after a second run or a
     changed path, culture-dependent parsing or formatting, a text that says something the code
     does not do, the two languages out of step, an exit code that does not match the outcome,
     a log call that can stop the tool or change what is printed, dead or duplicated code, code
     that contradicts its own comment or its design note, a test that cannot fail or does not
     check what its name says.
   - A finding needs a concrete case: which file or sequence of actions, what comes out, what
     should. No case, no finding.
   - Report each as: title; severity 1-4; `file:line`; what the code does; the case; whether an
     existing test covers the spot; confidence (read and certain / plausible, needs a run).
     Cleanup items in a separate short list.

3. **The seams, read by me** while the reviewers work: what `WcsCopy.Run` returns against how
   `CopyWcsCli` and `MainViewModel` use it (error outcomes, exit codes, the language argument,
   paths), the `--help` text and `README.md` against what `Options` accepts, and the repo-rule
   tests (`RepoInvariantTests`, `LicenseHeaderTests`).

4. **I verify every finding in the code** before it enters the list: open the cited lines,
   follow the callers, check the note and the requirement, and for a window finding check it
   can be reached from the screen. Outcome per finding: confirmed, rejected (with the reason,
   kept out of the list but counted), or `to measure`. Duplicates across areas are merged.

5. **The worst ones are shown, not only read.** For a finding of severity 1 or 2 that a reading
   leaves in doubt, I write a throw-away test that builds the case with `FitsTestData`, run it
   in the worktree and quote its output in the entry. The test is kept in the scratchpad,
   copied in for the run and removed after; no test stays in the repository from this task
   (the fix task writes its own, the rule "Test before fix"). The project is small and its
   tests run in seconds, which is why this step is affordable here and was not in LightsCheck.

6. **Write `docs/review/2026-11-16-code-review.md`**, in the form of LightsCheck's list:
   - How the review was done, what was in and out of scope, the commit read, the Help task
     left out, how many findings each area reported and how many were rejected.
   - A summary table: ID, severity, reach, evidence, one-line title, status (`open`).
   - One section per finding, ordered by severity then reach: ID `CR-001`..., `file:line`, what
     happens, the case, what should happen, the direction of a fix (not the fix), the test that
     would reproduce it, the state of the evidence.
   - A section for the copied window pieces (`CP-N`), fixed in the sibling first.
   - A last section, "Cleanup", short entries.
   - How the list is used: one entry (or one tight group) per task, in the table's order; the
     task follows the rules "Requirements first" and "Test before fix"; a text it would change
     is shown to the owner first, original and proposal; it sets the entry's status to
     `fixed <commit>` or `rejected: <reason>`.

   Severity, by the kind of harm, for a tool that writes into the owner's files:
   1. A destination file written wrong (a wrong, missing or extra card, a damaged structure or
      data), or an outcome said wrongly (done when it was not, refused when it should copy).
   2. A crash, a hang, or a log that stops the tool.
   3. A wrong text or a wrong display.
   4. Robustness and cleanliness (duplicated or dead code, a fragile construct, a weak test).

   Reach: *common* (an ordinary run), *some files* (another program's FITS than Siril's, another
   shape of header), *slip* (a mistyped argument or a wrong click), *rare*, *crafted* (a broken
   or hostile file).

   Evidence: *confirmed* (the lines were read and the case follows; "shown" added when step 5
   ran it), *to measure* (only a run or a screenshot settles it; the fix task starts there),
   *owner* (the code follows a requirement or a note; whether it is a defect is the owner's
   call).

7. **ADR 0017**: the whole-code review of 2026-11-16, its list in `docs/review/`, the severity
   scale, one entry per task, citing LightsCheck's ADR 0046 as the model. One line in
   `CLAUDE.md`'s Layout for `docs/review/`.

8. **Gate, commit, merge proposal**: `scripts/verify.ps1` in the worktree, read the diff,
   commit on the task's branch, propose the merge and ask. No CHANGELOG line (a document, no
   behaviour); each fix adds its own.

## Verification

- Every entry of the list cites a `file:line` that I opened myself; none is copied from a
  reviewer unread.
- Every file under `src/` belongs to exactly one area (checked by listing the files against the
  table before the reviewers start).
- The findings document says how many reports each area returned and how many were rejected.
- No throw-away test is left: `git status` in the worktree shows documents only, and
  `git diff main --stat` shows `docs/`, `CLAUDE.md` and nothing under `src/` or `tests/`.
- `scripts/verify.ps1` green before the commit.

## Cost, said plainly

Four subagents, each reading 750 to 1,800 lines of code plus its note and tests, then my
verification pass and a handful of throw-away test runs. One session, one list.

## What deviated

- `main` had moved from `6a470cee` to `181f21e3` when the worktree was made (another project's
  merge); nothing under `WCS_Copy/` differs between the two, and the list's line numbers are
  those of `181f21e3`.
- Step 5 ran more than the cases "a reading leaves in doubt": every case a test could build
  was run, 14 tests in one throw-away class, so 11 entries are `shown` and three more quote
  the run. The severity scale's second level lost "a log that stops the tool" (none was
  found) and the list has a fourth state of evidence, `shown`; ADR 0017 records both.
- No finding was rejected: 40 reported, 31 entries, three `CP-N` and a cleanup list. The test
  notes of CR-029 and CR-030 and the cleanup list were spot-checked, not opened one by one;
  the list says so.
- The reviewers ran more outside the repository than "a throw-away script": area 1 and area 4
  each built a small .NET console in the scratchpad, area 1 wrote its probe files in a folder
  of `%TEMP%` outside the scratchpad and deleted it, area 2's path probe created and deleted
  an empty `C:\x.fit`, and area 4 read NLog's source on GitHub, which its brief allowed. None
  touched the repository.
- The window was never opened: CR-002, CR-006, CR-008, CR-012 and CR-027 rest on a reading.
