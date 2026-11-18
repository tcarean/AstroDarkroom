# Code review of the whole project, 2026-11-16

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0017.

## How it was done

The code was written by an earlier model and had almost no review. On 2026-11-16 it measured,
blank lines included, 4,447 lines (`src/CopyWcs.Core/` 2,271, `src/CopyWcs.Cli/` 382,
`src/CopyWcs.Wpf/` 1,794) and 6,077 of tests. It was split into four areas. Each area was read
whole by a reviewer with a clean context (a subagent on the session's model), which first read
`CLAUDE.md`, the area's design note and its requirements, so that what is deliberate was not
reported, and read the area's tests for one question: can this test fail, and does it check
what its name says. A reviewer changed nothing in the repository and built nothing there. Every
finding it returned was then checked by the session that wrote this list: the cited lines were
opened and the case followed through the callers. The seams (what `WcsCopy.Run` returns against
how the console and the window use it, the paths, the write) were read by the session itself,
before the reports came in.

| # | Area | Reported | In this list as |
|---|------|---------:|-----------------|
| 1 | The FITS header engine (`FitsHeader`, `AsciiText`, `WcsMetadataKeys`) | 11 | CR-003, 004, 005, 007, 014, 018 to 022, 028 |
| 2 | The run, the console, the language (`WcsCopy`, `Language/`, `CopyWcsCli`, `Options`) | 10 | CR-001, 003, 004, 011, 013 to 015, 023, 026, 029 |
| 3 | The window (`src/CopyWcs.Wpf/`) | 10 | CR-001, 002, 006 to 010, 012, 017, 025, 027, 030; CP-1 to CP-3 |
| 4 | The diagnostic log (`Logging/`, `Program.cs`, the log calls) | 4 | CR-002, 021, 024, 031 |
| - | Seams, read by the session | 5 | CR-001, 007, 011, 016, 018 |

40 findings were reported and none was rejected at the check. The same defect seen from two or
three areas became one entry, which gives the 31 entries below, three more for the pieces
copied from the sibling projects, and a cleanup section. Each of the five seam findings was
found by a reviewer too.

**Shown, not only read.** The project is small and its tests run in seconds, so the cases a
test can build were run: one throw-away test class of 14 tests, built with `FitsTestData`, kept
outside the repository, copied into `tests/CopyWcs.Tests/` for one run on 2026-11-16 and removed
after. An entry marked *shown* quotes that run's output. No test of that class stays in the
repository; a fix writes its own (the kit's rule "Test before fix").

The reviewers also worked outside the repository, read-only on it. Area 1 ran Python scripts on
the two fixtures (the FITS checksum before and after a replicated copy; how astropy 8.0.1 reads
a cut `CONTINUE`; the `AsciiText` table against Python's NFD, no difference) and a small .NET
console (the reader's loop on a 300 MiB stream, `File.Move` over a file). Area 2 ran one F#
script on file sharing and path spellings. Area 3 measured label widths with WPF's
`FormattedText`. Area 4 read NLog's `FileTarget` source on GitHub and ran a console against
NLog 6.2.2 with the log's settings retyped. Their figures are quoted as theirs.

Out of scope: `scripts/` and `.claude/` (the kit's), `prototype/copy_wcs.py` (frozen, ADR 0001;
read only to see what the port lost), `Icon/make_icon.py`, and how strong the tests are line by
line (`/mutate` measures that). The Help button and F1, in work in another task on 2026-11-16
and not on `main`, were not read: that task has its own review.

Line numbers are those of commit `181f21e3`. A task finds its lines again by the names the
entry gives; the window's lines move when the Help task is merged.

## How to read an entry

- **Severity.** 1 = a file written wrong or lost (a wrong, missing or stale card, a damaged or
  truncated file, a file of the owner's overwritten), or an outcome said wrongly. 2 = a crash,
  a hang, a frozen window. 3 = a wrong text or a wrong display, a misleading refusal. 4 =
  robustness and cleanliness, a weak test.
- **Reach.** Who meets it: *common* (an ordinary run), *some files* (another program's FITS
  than Siril's, another shape of header), *some setups* (a slow disk, a small screen, a second
  monitor), *slip* (a mistyped argument or a wrong click), *rare*, *crafted* (a broken or
  hostile file).
- **Evidence.** *shown*: the case was run on 2026-11-16 and the entry quotes the output.
  *confirmed*: the lines were read and the case follows from them. *to measure*: the code was
  read, but the case rests on something only a run of the window, a screenshot or another
  program can show; the task starts by showing it. *owner*: the code does what is described
  and follows a requirement, a note or a test that pins it; whether that is a defect is the
  owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

The table is ordered by severity; within severity 1, a file lost or truncated comes before a
wrong card, and an unproven case last; within the others, by reach.

An entry gives the direction of a fix, never the fix. A task takes one entry (or the group one
entry names), starts from the requirement and a failing test (the kit's rules "Requirements
first" and "Test before fix"). A text an entry would change is shown to the owner first,
original and proposal side by side.

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | slip | shown | A new-file path that names the source overwrites the solved source, and the run says `Copied` | fixed d1178b79 |
| CR-002 | 1 | slip | confirmed | Closing the window during a copy cuts the write: a truncated new file, and a log that says `exit 0` | fixed c403bfc5 |
| CR-003 | 1 | rare | confirmed | A write to a new file that fails midway leaves a truncated file, and has already destroyed the one that was there | fixed f7d27425 |
| CR-004 | 1 | some files | shown | A destination that holds `CHECKSUM` is written with the checksum left stale | fixed 9453c365 |
| CR-005 | 1 | some files | shown | A long string value loses its `CONTINUE` cards, or keeps the destination's old ones | fixed 2ed7ac1b |
| CR-006 | 1 | rare | to measure | The window's relative paths follow the process's working folder | open |
| CR-007 | 2 | slip | shown | A large file that is not FITS is read whole before it is refused | open |
| CR-008 | 2 | some setups | to measure | The window reads the file facts on its own thread at every keystroke | open |
| CR-009 | 3 | common | confirmed | The outcome card stays as it was while the boxes change and during the next run | open |
| CR-010 | 3 | common | owner | A file's existence and facts are read only when the box's text changes | open |
| CR-011 | 3 | some files | shown | A header without `NAXIS1` is reported as the size -1, with the advice to force | open |
| CR-012 | 3 | some setups | to measure | The window may open taller than a small screen, and the cards may get no height | open |
| CR-013 | 3 | slip | shown | The same file as source and destination is accepted and reported as a copy | fixed d1178b79 |
| CR-014 | 3 | slip | shown | A new-file path that names the destination in another spelling ends in "used by another process" | fixed d1178b79 |
| CR-015 | 3 | slip | owner | `-o` takes a following flag as its file name: `-o --dry-run` writes a file | open |
| CR-016 | 3 | slip | owner | A read-only destination is refused only after its whole copy is written, and the copy is left | fixed f7d27425 |
| CR-017 | 3 | rare | confirmed | The window draws the warnings under the refusal; R-033 says above | open |
| CR-018 | 4 | rare | confirmed | The in-place replace is a plain move: creation time, attributes and links are lost, an existing `.tmp` is overwritten | open |
| CR-019 | 4 | some files | shown | `WCSAXES` can be written after the WCS cards it must precede | open |
| CR-020 | 4 | some files | owner | The SIP keys `A_DMAX` and `B_DMAX` are in neither list | open |
| CR-021 | 4 | crafted | shown | A keyword ending in a line feed passes for a SIP coefficient and splits a log line | open |
| CR-022 | 4 | rare | shown | Control characters reach the HISTORY card | open |
| CR-023 | 4 | common | shown | The usage block is written with LF line ends inside CRLF output | open |
| CR-024 | 4 | rare | confirmed | At `COPYWCS_LOG=warn` or higher the process's first and last lines are not written | open |
| CR-025 | 4 | slip | confirmed | `Copy` is enabled with two quote characters as the new file's path | open |
| CR-026 | 4 | slip | owner | A path that starts with a dash cannot be given; flags are case-sensitive, `--lang` is not | open |
| CR-027 | 4 | some setups | to measure | `ApplicationHighDpiMode` does nothing in a WPF project | open |
| CR-028 | 4 | - | confirmed | Weak tests of the header engine: the data offset, the padding, `Distinct` | open |
| CR-029 | 4 | - | confirmed | Weak tests of the run and the console | open |
| CR-030 | 4 | - | confirmed | Weak and missing tests of the window | open |
| CR-031 | 4 | - | confirmed | Weak tests of the log | open |

6 of severity 1, 2 of severity 2, 9 of severity 3, 14 of severity 4; 4 `to measure`, 5 `owner`.

CR-001, CR-013 and CR-014 are one group: a check of the three paths against each other, by the
file they name and not by their spelling, serves all three. CR-002, CR-003 and CR-016 share the
write through a temporary file.

## Severity 1

### CR-001 A new-file path that names the source overwrites the solved source

- **Where:** `src/CopyWcs.Core/WcsCopy.cs:247-250`; the mechanism is
  `FitsHeader.WriteWithDataFrom`, `src/CopyWcs.Core/FitsHeader.cs:399-409`. Reached from
  `-o` (`src/CopyWcs.Cli/Options.cs:69-77`) and from the window's `Write a new file` box
  (`MainViewModel.Request`, `src/CopyWcs.Wpf/ViewModels/MainViewModel.cs:240-241`;
  `CanCopy`, line 155, compares no paths).
- **What happens:** `inPlace` compares the destination with the target only. A target that is
  the source is "a new file": `File.Create` truncates it (the source's header was read and its
  handle closed at the first step) and the destination's data is written under the new header.
- **The case:** `copywcs solved.fit stack.fit -o solved.fit`, a slip of tab completion; or the
  source's path pasted in the window's third box. Shown: `kind=Copied`, the note `Copied 15
  keys into ...\source.fit.`, `source unchanged=False`, the source's last data byte 7 (the
  destination's) where it was 63. The solved stack's pixels are gone, with no warning. The
  Browse dialog asks before overwriting (`OverwritePrompt`); a typed path and `-o` do not.
- **Should:** a refusal before anything is written, a dry run included: the output is the
  source.
- **Direction:** in the core, where both applications pass; one check of source, destination
  and output by the file each names (see CR-013 and CR-014).
- **Test:** none gives the source as the output.
- **Evidence:** shown.
- **Status:** fixed d1178b79 (a new file that is the source, in any spelling, is refused before anything is written, a dry run too: one check of the three paths by the file each opens (`FileIdentity`), taken with CR-013 and CR-014 at the owner's word of 2026-11-18; R-019, ADR 0018).

### CR-002 Closing the window during a copy cuts the write

- **Where:** `src/CopyWcs.Wpf/App.xaml:26` (`ShutdownMode="OnMainWindowClose"`),
  `MainViewModel.RunAsync` (`MainViewModel.cs:221-235`, the core on a thread-pool thread),
  `src/CopyWcs.Wpf/MainWindow.xaml.cs` (no `Closing` handler), `App.OnExit`
  (`src/CopyWcs.Wpf/App.xaml.cs:113-120`), `LogSetup.Close`
  (`src/CopyWcs.Core/Logging/LogSetup.cs:175-185`).
- **What happens:** the copy runs on a background thread and nothing waits for it or refuses
  the close. Closing the window ends the process and kills the thread inside
  `input.CopyTo(output)`. The log is shut down first with `exit 0`; the run's `wrote` and
  `copy done` lines are never written.
- **The case:** Copy on a large stack (seconds on a hard disk; the only sign of a run is the
  two dimmed buttons), then the window's X. With `Write a new file`, the chosen file is left
  with a header and part of the data, an existing file the user agreed to overwrite included.
  In place, the destination stays whole and a partial `<dest>.tmp` is left. Nothing says so
  anywhere, and the log ends in a way `docs/design/logging.md` ("Reading a log") does not
  describe.
- **Should:** the close waits for the run, or is refused while it is in flight, and the log
  says how the copy ended.
- **Direction:** a `Closing` handler over the view-model's running state; CR-003's temporary
  file makes a killed write harmless as well.
- **Test:** none runs `App`, `OnExit` or a close during a run.
- **Evidence:** confirmed by reading (a thread-pool thread is a background thread; area 4's
  probe showed a line logged after `LogManager.Shutdown` is dropped). Not run; how wide the
  moment is on a real stack is for the task to measure.
- **Status:** fixed c403bfc5 (a close during a Preview or a Copy is held until the run ends, then the window closes by itself, with no new text, the owner's answer of 2026-11-18; the log has the window's two lines around the run's done line; R-050, ADR 0019. Measured that day with the Release console: a copy of the 290 MB Veil stack wrote in 1327 ms cold, 315 ms and 307 ms warm).

### CR-003 A write to a new file that fails midway leaves a truncated file

- **Where:** `FitsHeader.WriteWithDataFrom`, `src/CopyWcs.Core/FitsHeader.cs:399-431`; the
  catch in `WcsCopy.Run`, `src/CopyWcs.Core/WcsCopy.cs:248-258`, removes nothing.
- **What happens:** only the in-place write goes through a temporary file. For a new file,
  `File.Create(target)` truncates whatever is there at once, and nothing deletes the file when
  `CopyTo` throws.
- **The case:** `copywcs solved.fit stack.fit -o E:\out\final_wcs.fit` with less free space
  than the image, or a drive that drops: exit 1 and `Write error: ...`, but `final_wcs.fit`
  exists with a complete new header and part of the data; a good `final_wcs.fit` of an earlier
  run is gone. R-014 says a failed write leaves "no target file"; the README says a refused
  copy writes nothing.
- **Should:** no target file after a failed write, and an existing one untouched.
- **Direction:** the new file written beside its final name and moved at the end, as in place;
  the partial file deleted on failure.
- **Test:** `R014_Run_WriteFailure_IsRefusedWithTheWriteLine` and
  `R016_Run_AWriteError_DropsNothingAndHasNoLine` use a target in a missing folder, where
  `File.Create` fails before any byte. No test fails a write after it began.
- **Evidence:** confirmed by reading (no cleanup exists on any path); a full disk was not run.
- **Status:** fixed f7d27425 (every write, new file and in place, goes to `<name>.copywcs-<8 hex digits>.tmp` beside the target, created only when no file holds that name, renamed over the target at the end and deleted when anything fails; shown before the fix by a byte-range lock on the middle of the destination's data, which left the new file cut short and an existing one changed; R-012, R-014, ADR 0020).

### CR-004 A destination that holds `CHECKSUM` is written with the checksum left stale

- **Where:** `WcsCopy.Run`, `src/CopyWcs.Core/WcsCopy.cs:231-250`;
  `FitsHeader.WriteWithDataFrom`, `FitsHeader.cs:411-414`. No code names `CHECKSUM` or
  `DATASUM`.
- **What happens:** the header is changed and every other card written back verbatim, the
  `CHECKSUM` card included, which no longer matches the header.
- **The case:** the project's own V-002 destination, the SyQon CLI's output
  (`tests/testdata/fixtures/veil-2026-10-06-syqon-cli-1.1.8-denoised.fit`), holds
  `CHECKSUM= '6jF39j936jE36j93'` and `DATASUM = '2647693551'`. Shown, with the copy forced as
  V-002 does: before, the header's sum plus `DATASUM` is `0xFFFFFFFF` (valid); after `Copied 72
  keys` and 4 keys dropped, the card is unchanged and the sum is `0x2452C8C8`. A reader that
  verifies (astropy with `checksum=True`, `fitsverify`, CFITSIO's `fits_verify_chksum`) reports
  the HDU as corrupted. `DATASUM` stays right, the data being untouched.
- **Should:** `CHECKSUM` recomputed, or the card dropped; the checksum convention asks one of
  the two of a program that changes an HDU.
- **Direction:** a behaviour the port lost: area 1 measured that the prototype's write through
  astropy 8.0.1 keeps the checksum valid. Recomputing is 30 lines of arithmetic over the header
  bytes; dropping is one more key in the drop. The choice is the owner's.
- **Test:** none; `R016_ACopyOfTheRealPair_CopiesTheRecordedCountAndLeavesNoDroppedKey` writes
  this very file and does not look at the card.
- **Evidence:** shown. What Siril, ASTAP and PixInsight do with a stale `CHECKSUM` was not
  measured: the task starts there, since it decides how much this matters to the owner's files.
- **Status:** fixed 9453c365 (the destination's `CHECKSUM` is dropped and `DATASUM` kept, nothing said on screen, the owner's answer of 2026-11-18 over recomputing it; measured that day: Siril 1.4.4 reads a stale `CHECKSUM` without a word, astropy warns only when opened with `checksum=True`, ASTAP and PixInsight are not installed; R-018, ADR 0021).

### CR-005 A long string value loses its `CONTINUE` cards

- **Where:** `FitsHeader.SetCard`, `src/CopyWcs.Core/FitsHeader.cs:321-333` (one card);
  `WcsMetadataKeys.SelectKeysToCopy`, `src/CopyWcs.Core/WcsMetadataKeys.cs:65-77` (one key is
  one card); the caller, `WcsCopy.cs:238-241`.
- **What happens:** the first card of a keyword is copied. The `CONTINUE` cards that follow it
  in the source are not, and the ones that follow the replaced card in the destination stay.
- **The case:** a string of more than 68 characters in a copied key (`OBJECT`, `TELESCOP`,
  `INSTRUME`). Shown: (a) the source's long `TELESCOP` arrives as `'xxx...x&'` with no
  `CONTINUE` card, a value cut with a stray `&`; (b) a destination holding a long `TELESCOP`
  and a source with a short one gives `TELESCOP= 'My Scope'` followed by the destination's old
  `CONTINUE  'old tail'`, which astropy reads as the new value with the old tail glued on
  (area 1's reading).
- **Should:** the whole value copied, and the old continuation removed with the card it
  belonged to.
- **Direction:** a card and the `CONTINUE` cards right under it travel as one, in `GetCard`'s
  and `SetCard`'s place, still verbatim (R-010). The prototype copied the whole string through
  astropy.
- **Test:** none.
- **Evidence:** shown. Siril's own headers hold no such string in the two fixtures.
- **Status:** fixed 2ed7ac1b (a card travels with every `CONTINUE` card right under it, in the copy, the replace and the drop; the outcome lists the `CONTINUE` cards and still counts keys; R-009, R-010, ADR 0022).

### CR-006 The window's relative paths follow the process's working folder

- **Where:** `App.Parse`, `src/CopyWcs.Wpf/App.xaml.cs:130-155` (an argument kept "as given");
  `MainViewModel.Clean`, `MainViewModel.cs:274-278` (never makes a path absolute); the
  dialogs, `MainWindow.xaml.cs:115-122` and `163-173` (no `RestoreDirectory`).
- **What happens:** a relative path is resolved against the working folder each time it is
  used: when the facts are read, and again when the copy runs.
- **The case, certain by reading:** `Write a new file` with `out.fit` typed writes into the
  working folder of the process (the launcher's, or a shortcut's "Start in"), and the window
  says `Copied 15 keys into out.fit.` with no folder.
- **The case to measure:** `CopyWcs.Wpf.exe solved.fit stack.fit` started in folder X, as
  R-034 shows; then Browse for the new file in folder Y. If the dialog moves the working folder
  to Y, Copy is refused with `Cannot read the source 'solved.fit'`, or, when Y holds files of
  the same names (another night's folder), copies between Y's files while the facts under the
  boxes are X's.
- **Should:** each path made absolute once, when it is set, and shown whole.
- **Direction:** in `App.Parse` and the setters; the core takes what it is given.
- **Test:** none; `AppTests` and `R034_StartFillsTheBoxesWithTheFilesGiven` use absolute paths.
- **Evidence:** to measure: whether the file dialog of .NET 8 moves the working folder decides
  between severity 1 and 3. The fix is the same either way.
- **Note (2026-11-18):** a worse case, shown by the second review of the branch of CR-001 to
  CR-005 with a scratch console over the core: the working folder moved after the destination's
  header was read and before the write began (at the `read dest.fit` log line) makes the copy
  write the first folder's header, with the source's WCS, over the other folder's `dest.fit`,
  whose own bytes from the first header's data offset become its data; the run says `Copied`.
  The write itself now makes its two paths whole as it begins (ADR 0020), so a folder that moves
  during the write moves nothing; one that moves before it is this entry's. The owner chose to
  leave it to this entry's own task.

## Severity 2

### CR-007 A large file that is not FITS is read whole before it is refused

- **Where:** `FitsHeader.ReadFrom`, `src/CopyWcs.Core/FitsHeader.cs:137-164`. Reached from
  `WcsCopy.Run` and, on the window's thread, from `MainViewModel.Facts`
  (`MainViewModel.cs:68`, `83`, `210`, `292`).
- **What happens:** the loop ends only at an `END` card or the end of the stream, keeping every
  80 bytes as a string; the `SIMPLE` test comes after the loop.
- **The case:** the TIFF, XISF, SER or video of the same night picked by mistake through "All
  files", pasted, or passed on the command line. Shown on a 200 MB file of random bytes: 4,588
  ms, the process's peak working set from 65 MB to 611 MB, then `Truncated file, or not a valid
  FITS (the END card is missing)` and never `does not begin with SIMPLE`. Area 1 measured 2.41
  times the file's size on the managed heap. A file of several GB ends in
  `OutOfMemoryException`, which neither `WcsCopy.Run`'s filters nor `Facts`'s catch: the
  console crashes, and the window, which reads the facts in a property setter, freezes and
  then dies.
- **Should:** refused at the first card when it is not `SIMPLE`, with that message, without
  reading further.
- **Direction:** the check moved to the first card read; a ceiling on the number of header
  blocks is the owner's call.
- **Test:** `R015_ReadFrom_MissingSimpleAsFirstCard_Throws` builds a block with `END`, so it
  does not reach this path.
- **Evidence:** shown (the time and the memory; the crash at a given size was not run).

### CR-008 The window reads the file facts on its own thread at every keystroke

- **Where:** the `Source` and `Dest` setters, `MainViewModel.cs:60-87`, bound with
  `UpdateSourceTrigger=PropertyChanged` (`src/CopyWcs.Wpf/MainWindow.xaml:91`, `105`);
  `Copy`, line 210.
- **What happens:** every change of a box's text runs `File.Exists` and, for an existing file,
  `FitsHeader.ReadPrimary`, on the window's thread.
- **The case:** a path typed letter by letter on a share, or on a hard disk that has gone to
  sleep: each `File.Exists` waits for the disk, and the window does not answer meanwhile.
  With CR-007 open, a large file that is not FITS freezes it for seconds.
- **Should:** the window stays live while a file is looked at.
- **Direction:** the facts read off the window's thread, the latest text winning; after
  CR-007's fix the read is one block.
- **Test:** `R030_TheFactsUnderEachBoxComeFromTheHeaderAndVanishWhenTheFileCannotBeRead`
  covers a 60-byte file.
- **Evidence:** to measure, on a sleeping disk or a share; the read on the window's thread is
  certain by reading.

## Severity 3

### CR-009 The outcome card stays as it was while the boxes change and during the next run

- **Where:** `MainViewModel.cs:60-135` (no setter touches `_outcome`), `RunAsync`, lines
  221-229 (the outcome is shown only at the end).
- **The case:** (a) Preview on A into B lists A's 15 cards; change the source to C, or untick
  the optical keys: the card still lists A's cards under boxes that name C. (b) A refused copy
  shows `Refused / Different dimensions`; tick the force box and click Copy on a large stack:
  for the seconds of the write the window still shows `Refused`, and nothing says a copy is
  running.
- **Should:** the outcome cleared or dimmed when a path or an option changes and when a run
  starts, and a run in flight said.
- **Test:** none.
- **Evidence:** confirmed.

### CR-010 A file's existence and facts are read only when the box's text changes

- **Where:** `MainViewModel.cs:65-70`, `80-85` (`if (Set(ref _source, value))`);
  `ObservableObject.Set`, `src/CopyWcs.Wpf/Presentation/Mvvm.cs:45-55`, returns false for an
  equal value.
- **The case:** (a) paste the path of a file Siril has not saved yet, then save it: Preview and
  Copy stay disabled, and picking the same file in Browse changes nothing, the text being
  equal; the user must edit the text. (b) the box names a source that reads `not
  plate-solved`; solve and save it in Siril: the line still says `not plate-solved` while
  Preview already works on the new header.
- **Should:** the file looked at again on a Browse of the same path, when the window is
  activated, and before a run.
- **Test:** none for a file that changes after its path is set.
- **Evidence:** owner: `docs/design/window.md` makes "checked once, when its path is set"
  deliberate, against a check in the getters; these two cases follow from it and are not
  named there.

### CR-011 A header without `NAXIS1` is reported as the size -1

- **Where:** `WcsCopy.cs:138-153`; `FitsHeader.GetInt` returns -1 (`FitsHeader.cs:237-241`).
- **The case:** a file whose primary HDU has no image (`NAXIS = 0`, the image in an extension,
  as astropy and fpack write it) against a normal one. Shown: `Different dimensions: source
  (-1, -1) vs destination (100, 80). The WCS would be wrong. Use --force if you know what you
  are doing.` Forcing writes the WCS into a header no program reads the image from. Two files
  that both lack the keys pass as equal sizes (`kind=Previewed`); `docs/design/fits-header.md`
  calls that deliberate.
- **Should:** a line that says the file has no `NAXIS1` and `NAXIS2` in its primary header; -1
  is not a size.
- **Test:** none with a missing `NAXIS1`.
- **Evidence:** shown. The new text is the owner's to approve.

### CR-012 The window may open taller than a small screen

- **Where:** `src/CopyWcs.Wpf/MainWindow.xaml:31-34` (`Height="700"`, `CenterScreen`,
  `MinHeight="620"`), and the outcome card's rows, lines 147-153 (the cards list is the `*`
  row, with no `MinHeight`).
- **The case:** a 1920 x 1080 laptop at 150 % has a work area of about 680 DIP: the window's
  top and bottom would sit some 10 DIP off the screen. At the minimum height, two forced
  warnings and a drop line may leave the cards list no height.
- **Should:** the window capped to its screen, as AstroDarkroom's (its CR-005), and the list a
  minimum height or the card a scroll.
- **Evidence:** to measure, by a screenshot at 150 % and one at the minimum height.

### CR-013 The same file as source and destination is accepted

- **Where:** `WcsCopy.cs:105-135` (no comparison of the two paths).
- **The case:** `copywcs stack.fit stack.fit`, or the same file in both boxes. Shown:
  `kind=Copied`, `Copied 15 keys into ...\stack.fit.`, and the file gains `HISTORY WCS copied
  from stack.fit`, a card that says it was copied from itself; each further run adds another.
- **Should:** a refusal: the source and the destination are the same file.
- **Test:** none.
- **Evidence:** shown. One check with CR-001 and CR-014.
- **Status:** fixed d1178b79 (with CR-001: the same file as source and destination, in any spelling, is refused, a dry run too; R-019, ADR 0018).

### CR-014 A new-file path that names the destination in another spelling

- **Where:** `FitsHeader.cs:399` (`Path.GetFullPath(dataSource) == Path.GetFullPath(target)`,
  ordinal, on a file system that ignores case).
- **The case:** the destination is `Stack.fit`; `copywcs solved.fit Stack.fit -o stack.fit`.
  Shown: `Write error: The process cannot access the file '...\stack.fit' because it is being
  used by another process.`, the destination unchanged. No other process exists: the tool
  holds the file itself. The same miss follows for a `\\?\` prefix (area 2's probe), an 8.3
  name, a `subst` drive, a junction or a link (not run).
- **Should:** taken as the in-place write it is, or refused in words that say so.
- **Note:** the destination survives only because the input is opened before the output and
  shares for reading alone. Swap those two lines and this case empties the destination. Worth
  a test that pins it.
- **Test:** none; the in-place test passes the identical string twice.
- **Evidence:** shown.
- **Status:** fixed d1178b79 (with CR-001: a new file that is the destination is refused in every spelling, the destination's own included, rather than taken as the in-place write; the destination keeps every byte; R-019, ADR 0018).

### CR-015 `-o` takes a following flag as its file name

- **Where:** `Options.Parse`, `src/CopyWcs.Cli/Options.cs:69-77`.
- **The case:** `copywcs solved.fit stack.fit -o --dry-run`, the file name forgotten. Shown:
  `Output=--dry-run DryRun=False`: no dry run, a real copy into a file named `--dry-run` in
  the working folder. With `-o --force` the force is not applied.
- **Should:** R-002's `--output needs a file name.` with exit 2; a name that really starts
  with a dash can be written `.\-x.fit`.
- **Test:** `OptionsTests.Parse_OutputValueLooksLikeAFlag_ConsumedVerbatimAsFilename` pins the
  present behaviour, with the comment "Existing behaviour".
- **Evidence:** owner: a test pins it and no requirement asks for it; changing it changes that
  test (the kit's rule "No test edited to pass").

### CR-016 A read-only destination is refused only after its whole copy is written

- **Where:** `FitsHeader.cs:399-436`; the catch in `WcsCopy.cs:252-258`.
- **The case:** a destination with the read-only attribute, in place. Shown: `Write error:
  Access to the path is denied.`, and the folder holds `dest.fit` and a `dest.fit.tmp` of the
  same size. On a real stack that is hundreds of MB written first and left behind, and the
  line does not say a file was left.
- **Should:** refused before the data is copied, or the temporary file removed, or the line
  names it.
- **Test:** none with a destination that cannot be replaced.
- **Evidence:** owner: `docs/design/fits-header.md` says "the `.tmp` is left behind" on a
  failure before the move; whether to keep that is the owner's call. Shown.
- **Status:** fixed f7d27425 (with CR-003: a failed write's temporary file is deleted in place too, the owner's answer of 2026-11-18; the destination keeps every byte and nothing is left beside it; R-014, ADR 0020).

### CR-017 The window draws the warnings under the refusal

- **Where:** `src/CopyWcs.Wpf/MainWindow.xaml:156-168` (the refusal in `Grid.Row="0"`, the
  warnings in `Grid.Row="1"`).
- **The case:** a source without `CTYPE1`, the optical keys off: the core gives the warning
  `the source does not look plate-solved` and then the refusal `No relevant key found`. The
  window shows the red block first and the amber line under it; R-033 says "the warnings
  raised before it above it", and the console prints them in that order. A forced copy that
  ends in a write error does the same.
- **Should:** the requirement and the window agree; `docs/design/window.md:30-34` lists the
  blocks in the code's order, so the note and the requirement disagree too.
- **Test:** none checks the order.
- **Evidence:** confirmed.

## Severity 4

### CR-018 The in-place replace is a plain move

- **Where:** `FitsHeader.cs:400`, `433-436` (`target + ".tmp"`, `File.Move(temp, target,
  overwrite: true)`).
- **The case:** shown: a file the owner already has named `dest.fit.tmp` is overwritten and
  moved away without a word (`tmp exists=False`). Area 1 measured that a destination created
  in 2020 and marked Hidden comes out with the run's creation time and `Archive` alone. By
  reading, not run: the file's permissions become the folder's, a destination that is a
  symbolic link is replaced by a regular file while the linked file stays as it was, two runs
  on one destination (the window and the console) write the same temporary file, and nothing
  is flushed before the move.
- **Direction:** `File.Replace` keeps the times, the attributes and the permissions; a
  temporary name that cannot collide.
- **Evidence:** confirmed; the links are for the task to run.
- **Note (2026-11-18):** still open, but its temporary file changed with CR-003's fix (f7d27425, ADR 0020): it is `<name>.copywcs-<8 hex digits>.tmp`, created only when no file holds that name, so a `dest.fit.tmp` of the owner's is left alone and two runs on one destination no longer share a file. The rest stands: the replaced file's times, attributes and permissions, a destination that is a link, no flush before the rename. With the same fix a new file (`-o`) that replaces an existing one goes the same way, a rename over it, so all of these reach it too, with two more for both writes: the replace needs room for both files, and a folder where the user may change the file but not create one refuses it (the review of the branch, 2026-11-18; ADR 0020).

### CR-019 `WCSAXES` can be written after the WCS cards it must precede

- **Where:** `FitsHeader.SetCard` (replace where it is, else append).
- **The case:** a source that holds `WCSAXES` (astrometry.net writes it; Siril does not) into
  a destination that has `CTYPE1` and the rest but no `WCSAXES`. Shown: `CTYPE1` at index 6,
  `WCSAXES` at 14 of 23. The FITS standard (4.0, section 8.2) says `WCSAXES`, if present, must
  precede all other WCS keywords. The prototype wrote the same order.
- **Evidence:** shown. Whether a reader minds was not measured; if one does, this is a
  severity 1.

### CR-020 The SIP keys `A_DMAX` and `B_DMAX` are in neither list

- **Where:** `WcsMetadataKeys.cs:30-40`, `54-55`.
- **The case:** a destination whose old solve wrote them keeps them beside the source's SIP
  coefficients, and a source that has them does not pass them on.
- **Evidence:** owner: R-016 and ADR 0011 leave "a plate-solve card outside both lists" alone
  on purpose, naming `RADECSYS` and `PVi_m`; these two belong to the convention the lists
  cover. Neither fixture holds them.

### CR-021 A keyword ending in a line feed passes for a SIP coefficient

- **Where:** `WcsMetadataKeys.cs:54-55` (`^(A|B|AP|BP)_\d+_\d+$`: in .NET `$` also matches
  before a final `\n`), against `FitsHeader.KeywordOf` (`FitsHeader.cs:201-202`), which keeps
  control characters in a keyword so that no such card passes for another (R-010). The key
  then goes raw into the log's `card` and `drop` lines (`WcsCopy.cs:353`, `363`) and into the
  drop line on stdout.
- **The case:** a card whose keyword field is `A_1_2` and the byte 0x0A. Shown: `keys with a
  line feed selected=1`. The Debug line is written as two physical lines, the second without
  time or level (area 4's probe), where R-042 says a control character is logged as `\xNN`.
- **Direction:** `\z` and `[0-9]` in the pattern.
- **Evidence:** shown.

### CR-022 Control characters reach the HISTORY card

- **Where:** `AsciiText.Of`, `src/CopyWcs.Core/AsciiText.cs:115-126` (everything below 0x80 is
  kept); `FitsHeader.AddHistory`, `FitsHeader.cs:364-373`.
- **The case:** a source file named with the character 0x7F, which Windows allows. Shown, for
  `a<tab>b<0x7F>c`: the card's bytes are `... 61 09 62 7F 63`. A FITS header may hold 0x20 to
  0x7E only, and R-011 and ADR 0010 mean the one card the tool composes to be a standard one.
- **Evidence:** shown.

### CR-023 The usage block is written with LF line ends

- **Where:** `Options.PrintUsage`, `Options.cs:123-135`, `142-154`: one raw string literal,
  which keeps the source file's line ends, and the repository forces LF.
- **The case:** `copywcs --help > help.txt` on Windows. Shown: 9 lines end in CRLF (the
  banner, the copyright, the warranty) and 10 in a bare LF (the usage).
- **Direction:** the usage written line by line, as the warranty is.
- **Test:** none can see it; the tests' `Lines()` normalise the line ends first.
- **Evidence:** shown.

### CR-024 At `COPYWCS_LOG=warn` the process's first and last lines are not written

- **Where:** `LogSetup.cs:142`, `183` (both at Info), `243` (one rule from the minimum level).
- **The case:** `COPYWCS_LOG=warn` and a refused copy: the log holds a lone `WARN` line, with
  no version, no arguments, no folder and no `exit` line, though R-041 says each process
  begins and ends with them. `RunClock`'s `ended without an outcome` is at Info too
  (`RunClock.cs:100`).
- **Should:** the two lines written at every level but `off`, or R-041 and the README say
  they are Info lines.
- **Evidence:** confirmed.

### CR-025 `Copy` is enabled with two quote characters as the new file's path

- **Where:** `MainViewModel.cs:155` tests the raw text, line 241 sends the cleaned one.
- **The case:** `""` in the third box: Copy is enabled, and the run is refused with a write
  error from an empty path. Nothing is written.
- **Evidence:** confirmed.

### CR-026 A path that starts with a dash cannot be given

- **Where:** `Options.cs:63-94`.
- **The case:** `copywcs -stack.fit out.fit` gives `Unknown option: -stack.fit`; there is no
  `--` to end the options. `--DRY-RUN` and `--output=x.fit` are unknown, while `--LANG RO` and
  `--lang=ro` are accepted. All fail safely with exit 2.
- **Evidence:** owner.

### CR-027 `ApplicationHighDpiMode` does nothing in a WPF project

- **Where:** `src/CopyWcs.Wpf/CopyWcs.Wpf.csproj:16`; there is no `app.manifest`.
- **The case:** the property is read by the Windows Forms targets alone (area 3 searched the
  installed SDK), so the window is not per-monitor aware as the line suggests: dragged to a
  second monitor with another scale, Windows stretches it as a picture.
- **Evidence:** to measure, on two monitors of different scale.

### CR-028 Weak tests of the header engine

- **The data offset.** No test reads a destination whose header spans more than one block and
  has data after it: every test file with data has a one-block header, and the two real
  fixtures hold no data. By area 1's reading, `h._dataOffset = consumed` (`FitsHeader.cs:166`)
  replaced by the constant 2880 passes the whole suite, while every real file (3 and 4 header
  blocks in the fixtures) would be written with the tail of its old header in front of its
  data. This is the line the tool's safety rests on; no mutation was run.
- **The padding.** R-013 says spaces between `END` and the data; the tests read the file back
  through the same reader, which ignores what follows `END`. A writer that fills with zeros
  passes.
- **The length.** `R012_WriteWithDataFrom_InPlaceOverwrite_NoLeftoverTempFileAndDataPreserved`
  (`FitsHeaderTests.cs:409-428`) compares the last bytes with the data and not the length.
- **`Distinct`.** `R007_SelectKeysToCopy_ResultHasNoDuplicates`
  (`WcsMetadataKeysTests.cs:108-118`) cannot fail: the lists and the pattern never overlap,
  and a SIP keyword twice in the source is in no test.
- **Evidence:** confirmed by reading.

### CR-029 Weak tests of the run and the console

- R-005 says `NAXIS1` or `NAXIS2`; every mismatch test differs in `NAXIS1`. A run that
  compared `NAXIS1` alone passes them all.
- `LicenseNoticeTests.TheCopyrightComesFromTheBuildProps` cannot tell the attribute from the
  fallback: `LicenseInfo.cs:78` holds the same text as `<Copyright>`.
- `LanguageTests.R021_WithoutLang_TheTextsAreEnglish` runs inside `Ui.Use(English)`; the
  initialiser `Ui.Default = StartupDefault` is overwritten by `RomanianByDefault` before any
  test, so no test would see it wrong.
- The text `--output needs a file name.` is asserted nowhere.
- No test covers R-003's "an unknown option before it is still a usage error".
- `VersionTests.TheBannerIsTheNameAndTheVersion` restates the implementation's expression.
- **Evidence:** confirmed by reading; the copyright test and the `-o` test were opened, the
  others taken from the reviewer.

### CR-030 Weak and missing tests of the window

- R-037's dark title bar: no test runs `DarkTitleBar.Apply`.
- R-035's "`Close`, Enter and Esc close it": nothing asserts `IsCancel` or `IsDefault`.
- `WindowSmokeTests.BindingErrors` has no canary: five tests assert its text is empty, and
  none proves a broken binding puts text there.
- No check that every `{l:T}` has both `En` and `Ro`; a missing `Ro` gives an empty label.
- `Render` uses 720 x 560, the window's outer minimum; the real client area is about 16 px
  narrower.
- Not covered: the two `async void` handlers, the three Browse handlers, `App.OnStartup`.
- **Evidence:** confirmed by the reviewer's reading; spot-checked, not opened one by one.

### CR-031 Weak tests of the log

- `R041_TheLastLineGivesTheSecondsWithOneDecimal` (`LoggingTests.cs:284-290`) asserts
  `LogText.Seconds` and `LogText.Ms` only: `LogSetup.Close` and `LogSetup.Apply` are run by no
  test.
- `NoLogLineGoesThroughTheTranslation` (`LoggingTests.cs:914-949`) says it sees every call; its
  pattern matches `Log.Info(`-style calls and not the texts handed to `clock.Done(...)` and
  `RunClock.Start(...)`, which become log lines too.
- `R046_TheLogIsTheSameInBothLanguages` has no scenario of R-016 or R-017.
- **Evidence:** confirmed; the first two were opened.

## The copied pieces

Fixed in the sibling first (ADR 0007), then copied here. None of the three is in a sibling's
list by that name, as far as this review looked.

- **CP-1** `src/CopyWcs.Wpf/Theme/Dark.xaml:184-186` (from LightsCheck): the CheckBox
  template's comment says a long label breaks into two lines; a string `Content`, which this
  window passes, gets a `TextBlock` that does not wrap and would be cut. Area 3 measured 17 px
  to spare at the minimum width at 100 %, so nothing is cut today.
- **CP-2** `src/CopyWcs.Wpf/Theme/Dark.xaml:83-84` (from LightsCheck): the Hyperlink's hover
  trigger sets `Foreground` to the value it already has.
- **CP-3** `src/CopyWcs.Wpf/Presentation/DarkTitleBar.cs:69-71` (from AstroDarkroom): an empty
  catch with no comment, and neither catch writes a log line, where ADR 0009 says the window's
  quiet catches got one.

## Cleanup

Short entries, taken from the reviewers and spot-checked, for a task of their own or the task
that next touches the file.

- `FitsHeader.cs:237-241`: `GetInt`'s comment says "trimmed of spaces only", but
  `int.TryParse` accepts 0x09 to 0x0D around the digits, and it parses with the current
  culture where `GetDouble` names the invariant one.
- `FitsHeader.cs:201-202`: `Trim(' ')` trims both ends, so a keyword with spaces in front
  passes for the left-justified one; the comment describes padding, which is `TrimEnd`.
- `FitsHeader.cs:341-350`: `RemoveCards` walks the list twice, and only tests read what it
  returns.
- `FitsHeader.cs:413`: `Pad(card)` in the write loop is redundant; for the same reason the
  `card.Length < 8` branch of `KeywordOf` cannot be reached.
- `WcsMetadataKeys.cs:98`: the `cd` array is made on every call, and `Determinant` runs four
  times per copy.
- `WcsCopy.cs:55`, `66`: the comments of `Refused` and `Cards` say "before writing" and
  "before the selection"; a write error comes after both.
- `WcsCopy.cs:75`: `WcsCopyOutcome.Target` is read by no application.
- `WcsCopy.cs:160`, `218`: `source.Contains("CTYPE1")` twice.
- `LanguageArgument.cs:31`: `Given` has no reader in `src/`.
- `Options.cs:137-139`, `156-157`: the same two closing lines in both language branches.
- `Options.cs:129`, `102`: Romanian wording, the owner's: "in loc sa modifice destinatia in
  loc" uses "in loc" for both "instead" and "in place"; "sursa si destinatie" against "the
  source and the destination".
- `LicenseInfo.cs:20-22`: "Hardcode nothing here" above a file whose line 78 holds the
  copyright as a fallback: a year roll has that second place to change.
- `VersionInfo.cs:41`: the `<returns>` says `or "0.0.0"`; with the attribute missing the first
  fallback gives the four-part `1.0.0.0`.
- `VersionInfo.cs:24`, `tests/CopyWcs.Tests/VersionTests.cs:24`: "bumps it by hand" is stale
  beside ADR 0003.
- `Program.cs:44-45`: the comment promises that "a broken NLog on this machine" is survived;
  only a failure inside `Apply` is. An NLog that cannot load fails the type initialisers of
  the six classes with a static `Logger`. Not reachable in the single-file publish.
- `LogSetup.cs:394`, `LogText.cs:167-169`: arguments and paths are logged unescaped; an
  argument that holds a line break splits the start line.
- `LogText.cs:178-204`: `Escape` does not escape the backslash, so a card holding the text
  `\x00` reads as an escaped NUL.
- `LogText.cs:127-130`: a card with `=` in column 9 and no space after it is logged as
  `NAXIS1==100`.
- `LogSetup.cs:316`: `NewestLog` is `internal`, used by `CanWrite` alone.
- `RunClock.cs:30-31`: "its caller logs the exception"; no caller of `WcsCopy.Run` does.
- `tests/CopyWcs.Tests/LoggingTests.cs:900`: the configurer check does not see
  `LogManager.Shutdown()` or `LogManager.ThrowExceptions =` outside `LogSetup`.
- `tests/CopyWcs.Tests/FitsHeaderTests.cs:152-159`:
  `GetInt_CardWithNoEqualsSign_ReturnsNegativeOne` gives -1 with or without the branch it
  names.
- `docs/design/logging.md:158`: "the two warnings and the two refusals"; since R-017 there are
  three of each. Lines 171-184 describe the console's crash order alone.
- `MainViewModel.cs:237`: `Request`'s summary says "without their surrounding whitespace";
  `Clean` takes the quotes off too. Lines 51 and 167: two pragma blocks for one reason.
- `MainWindow.xaml.cs:123`, `166`: `Path.GetDirectoryName` gets the raw box text, so a quoted
  path proposes no folder; the Save dialog ignores the output box's own path, against
  `docs/design/window.md:41-42`.
- `App.xaml.cs:138`: `string.IsNullOrWhiteSpace(a) ||` is redundant before `File.Exists(a)`.
- `Views/AboutWindow.xaml.cs:31-34`: the summary lists the About window's parts without the
  third-party card.
- `README.md:98-99`: "after a copy the destination reads `already plate-solved`" holds for an
  in-place copy alone (R-032 says so).
