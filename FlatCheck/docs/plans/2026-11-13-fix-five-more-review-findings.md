# Fix five more review findings (CR-011 to CR-015)

## Context

`docs/review/2026-11-13-code-review.md` holds 60 findings (ADR 0100). CR-001 to CR-010 are
fixed and merged. The owner asked for the next five open ones and for the list to be marked so
later sessions know what is done: CR-011 to CR-015, all of severity 2 (a crash, a hang, or a
file overwritten or lost). As before: one branch (`claude/Fix-FlatCheck-CR-011-to-CR-015`, its
worktree made already), one commit per entry with its requirement, failing test, fix, docs and
gate, then a commit that closes the five entries in the list.

Four choices were put to the owner on 2026-11-13 with the facts; the answers are in the plan.

## What was measured (2026-11-13)

**CR-015** was "to measure". A throw-away .NET 8 program started a `.cmd` whose child
(PowerShell) denies `PROCESS_TERMINATE` on itself, as a process the user may not end, then ran
what `SirilScriptRun` runs:

```
[0.9s] child: READY pid=22848 convert=True set=True
[1.0s] Cancel() threw AggregateException: One or more errors occurred. (Not all processes in process tree could be terminated. (Access is denied.))
[1.0s] root (cmd) HasExited=True
[9.0s] WaitForExitAsync finished within 8 s of the kill: False
[20.9s] child: CHILD-EXITS-BY-ITSELF
[20.9s] WaitForExitAsync finished
```

So: the exception does leave `Cancel()`; the window's handler for it only logs
(`App.xaml.cs`, `LogSetup.Fatal`), so the window closes. And once the exception is caught, the
run still would not end: `siril-cli` is dead, but `WaitForExitAsync` waits for its output to
close, which the surviving child holds open (20 s here, until it ended by itself).

The other four are `confirmed` by reading; their lines were read again today and each case
becomes the failing test.

## The fixes

### 1. CR-011: an output never replaces an input (new R-137, ADR 0108)

Owner's choice: **only the files the run reads** are protected, not every name ending in a
FITS or raw extension.

- `FlatCheck/Options.cs`, after the existing `--synthetic` guard, with the existing `SamePath`:
  `-o` / `--out` against the input file, the `--against` file and the `--raw` file;
  `--synthetic` also against the `--raw` file. Checked whatever the mode, before anything runs.
- `FlatCheck/FlatCheckCli.cs`: for `--light` on a folder, `-o` against the frames
  `SeriesFinder.Find` lists (the files exist only on disk, so not in `Options`), before the
  series runs.
- Texts (the existing `--synthetic cannot write over the reference frame; give another path`
  stays):
  - `-o cannot write the picture over an input file (<path>); give another path` /
    `-o nu poate scrie poza peste un fisier de intrare (<cale>); da alta cale`
  - `-o cannot write the picture over a frame of the series (<path>); give another path` /
    `-o nu poate scrie poza peste un cadru din serie (<cale>); da alta cale`
  - `--synthetic cannot write over the raw given with --raw; give another path` /
    `--synthetic nu poate scrie peste raw-ul dat cu --raw; da alta cale`
- Tests (`OptionsTests`, `CliTests`): each of the four pairs exits 1 with its text and leaves the
  file's bytes as they were; a different `-o` still writes.

### 2. CR-012: an output file is the old one whole or the new one whole (new R-138, ADR 0109)

Owner's choice: **every file FlatCheck writes**, not only the three the entry names.

- New `FlatCheck.Core/OutputFile.cs`: `Write(path, Action<Stream>)`, `WriteAllBytes`,
  `WriteAllText`. The bytes go to `<path>.tmp` beside the target, which then replaces the target
  in one move (`File.Move(..., overwrite: true)`), the way `SessionSettings.Write` already does
  for `flatcheck.json`. A failure deletes the temporary file and throws what it threw; a
  leftover from a killed process is overwritten by the next write.
- Used by the seven writes: `FlatCheckCli.SaveMap` and `RunSynthetic`;
  `SyntheticTabViewModel.RunAsync`; the report and map saves of `StructureTabViewModel` and
  `SplitTabViewModel` (the call alone changes there; CR-040, the wrong file named on a save
  failure, stays open).
- Tests: `OutputFileTests` (a writer that throws half way leaves the old bytes and no `.tmp`;
  a good write replaces the file and leaves no `.tmp`; a stale `.tmp` is overwritten); one per
  application showing the old file is never cut: a reader that holds the old file open keeps
  reading the old bytes while the path gets the new file (console `-o` and `--synthetic`, the
  Synthetic tab).

### 3. CR-013: a FITS header that cannot be right is refused before it is used (new R-139)

- `FlatCheck.Core/FitsReader.cs`, `LoadFull`, for every HDU that is not a table, before any
  size is computed or any array made:
  - `NAXIS` outside 0 to 999, or a `NAXISn` below 0, is refused with `InvalidDataException`:
    `FITS: invalid header: NAXIS1 = -2880; an axis cannot be shorter than 0.` /
    `FITS: header invalid: NAXIS1 = -2880; o axa nu poate fi mai scurta de 0.` and
    `FITS: invalid header: NAXIS = 1000; the standard allows 0 to 999 axes.` /
    `FITS: header invalid: NAXIS = 1000; standardul permite intre 0 si 999 de axe.`
  - an HDU with data whose `BITPIX` is missing or not a number:
    `FITS: invalid header: BITPIX is missing or not a number.` /
    `FITS: header invalid: BITPIX lipseste sau nu e un numar.`; a `BITPIX` that is none of the
    five read keeps today's `unsupported BITPIX: <n>` (same exception type, the existing test
    pins it), thrown now before the pixels are allocated.
  - the byte count in checked arithmetic, an overflow ending in the cut-data refusal (R-009), as
    the table branch already does; the step to the next HDU never moves backwards.
  - an image with a zero-length further axis (`NAXIS3 = 0`) is an HDU without an image and is
    skipped, instead of an `ArgumentException` out of the copy.
- Tests (`FitsReaderTests`): the entry's header (`BITPIX 8, NAXIS1 -2880, NAXIS2 2`) is refused
  by name instead of looping (the test runs the load under a time limit); no `BITPIX` with a
  large `NAXIS1 x NAXIS2` is refused by name; `NAXIS3 = 0`; the console exits 1 on each.
- CR-029 and CR-048 (the header-only read) stay open.

### 4. CR-014: a lens name is read with the digits 0 to 9 only (R-018 extended, ADR 0110)

Owner's choice: **a name with other digits says nothing**; it is not converted.

- `FlatCheck.Core/LensProfiles/LensProfile.cs`, `LensSpec`: `[0-9]` instead of `\d` in
  `FocalRx`, `ApertureRx` and `FocalTailRx`, and `double.TryParse` with a finite check instead
  of `double.Parse`, so no name can throw out of `Parse`.
- Tests (`LensProfileTests`, `R018_...`): names with full-width and Arabic-Indic digits give no
  focal length and no aperture and do not throw; a mixed name (`135mm` in full-width digits,
  then an ASCII `F2.8`) gives the aperture alone; one such lens in a catalogue no longer stops
  `LensIdentification.Identify` for a raw that goes through the range.

### 5. CR-015: Stop ends a Siril run even when a child cannot be killed (new R-140, ADR 0111)

Owner's choice: **the run ends within 2 seconds of the kill**; the screen's text stays.

- `FlatCheck.Wpf/Session/SirilScriptRun.cs`:
  - `Kill` also catches the `AggregateException` of `Process.Kill(entireProcessTree: true)` and
    logs it at Warn (some process Siril started could not be ended), so it never throws, as its
    summary says.
  - `ExecuteAsync`: after the kill, the wait for the output to close is bounded by a constant
    of 2 s. When `siril-cli` has exited and its output is still open then, the run is reported
    as stopped (the same cancellation as today) with a Warn line saying a process it started
    still runs. When `siril-cli` itself is still alive, the wait goes on as today: a run is
    never called stopped while Siril runs. A normal stop is unchanged: the output closes at
    once.
- Test (`SirilRunTests`, Windows-only like the whole project): the fake `siril-cli` starts the
  measurement's self-protecting child; the cancel does not throw and the run reports a
  cancellation within the limit while the child is still alive; the test then waits for the
  child's own end (a few seconds) before it removes its folder. About 7 s in the gate.

### 6. Close the entries

The Status column of the five rows and a `Status:` line per entry (commit, requirement, ADR,
what was measured, what stays open), as for CR-001 to CR-010.

## Docs in the same commits

`docs/requirements.md` (R-137 to R-140 new, R-018 and R-009's cross-references);
`docs/decisions/0108` to `0111`; `CHANGELOG.md` (five `Fixed` lines); the design notes that
cover the touched files (`process.md` for the console's arguments and writes, the FITS
reader's note, `lens-profiles.md`, `two-applications.md` for the Siril run, `code-map.md` for
the new file); `README.md` where it speaks of `-o`. This plan saved as
`docs/plans/2026-11-13-fix-five-more-review-findings.md`.

## Verification

- Each new test seen failing before its fix, then green; the gate (`scripts/verify.ps1`) at
  every commit; a `--no-incremental -warnaserror` build before the last one.
- Goldens: no line expected to change (no report text moves); any changed line is read and
  reported, and a parity golden line stops the task.
- No formula changes (`Metrics.cs`, `Stats.cs`, `Analysis/`, `Report/` are not touched), so
  `/validate` is not due.
- By hand in the worktree: `flatcheck A.fit -o A.fit` refused with A intact; a normal run leaves
  no `.tmp` beside its map.
- Then a subagent review of the branch, its findings applied minimally, and the merge proposed
  and asked.
