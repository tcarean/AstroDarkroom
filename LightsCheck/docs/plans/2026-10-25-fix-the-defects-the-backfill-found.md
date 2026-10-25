# Fix the code defects the documentation backfill found

## Context

The documentation backfill (`docs/plans/2026-10-25-document-every-type-and-method-and-translate-the-copies.md`,
merged into `main` as e30df12) read every member against its comment and listed what it found
under "Questions for the owner" and "New code questions from the review", changing none of it.
The owner answered on 2026-10-25: fix items 1, 2, 7 and 8 (test first), do items 3, 4 and 5 as
separate commits with no visible change, run `/code-review high` before proposing the merge,
and change nothing else. The backfill branch was squashed into a4b0af2 and is in `main`, so
this branch, `claude/Fix-the-defects-the-backfill-found`, is cut from `main`.

The owner's message is the plan; it is saved here as written, with the assumptions stated.

## Steps, one commit each

1. **Pixel size: NaN and Infinity are accepted.** `Options.Parse` refuses only `p <= 0`, so
   `--pixel NaN` and `--pixel Infinity` pass; the window's `PixelIsValid` checks `p > 0`, so
   it accepts `Infinity`. A raw carries no pixel size, so on a NEF session the value feeds the
   arcsec-per-pixel scale. Refuse anything that is not a finite number greater than 0, with the
   console's Romanian message as it is. The check goes once in the core, beside
   `SeriesOptions`, and both applications call it (the rule "Computed only in the core").
   R-004 and R-029 first.
2. **Star cut-outs: a stale decode's error overwrites the current note.**
   `MainViewModel.LoadCropsAsync` checks `ReferenceEquals(_selectedFrame, row)` after a
   successful decode but not in the `catch`: select frame A, then B, and A's decode fails,
   and `EROARE: ...` appears under B, against R-031. Test in `tests/LightsCheck.Wpf.Tests`
   with a decode that fails after a newer selection; the fix is the same check in the catch.
7. **`--frame N` crashes on a file the readers cannot decode.** `Program.Run` catches IO,
   InvalidData, InvalidOperation, Argument and UnauthorizedAccess exceptions only.
   `RawReader.Load` lets libraw's own exception through (`LibRawException`, a plain
   `Exception` in Sdcb.LibRaw); `FitsReader.LoadFull` indexes past the end of a FITS whose
   data is shorter than its header says (`IndexOutOfRangeException`). The owner's choice:
   translate the failure where it happens, not widen the console's filter. `RawReader.cs` is
   LightsCheck's own ("adapted"), so it wraps libraw's exception into one the console handles,
   with a Romanian message naming the file. `FitsReader.cs` is a FlatCheck copy (the rule
   "Copied files stay copies"): FlatCheck's `LoadFull` has the same gap (checked 2026-10-25,
   `FlatCheck.Core/FitsReader.cs`, the pixel loop calls `ReadSample(raw, p, bitpix)` with no
   check of `p` against `raw.Length`), so the copy is not edited here and a FlatCheck prompt is
   given instead (below). Test: a `.NEF` of junk bytes through `Program.Run --frame 1`, exit 1
   and an `EROARE` line; first establish what libraw throws on such a file.
8. **Window progress from a stopped run (plausible, not traced).** In `OpenAsync` the progress
   callback does not check that its run is still the current one, while `finally` does. Posts
   queued by a cancelled or replaced run may write `Progress` and `BusyMessage` over the new
   run's. First a test: start a run, cancel it, start another, deliver a queued report of the
   first. If it fails, the same check fixes it; if it cannot happen, say why with the evidence
   and change nothing.
3. **The window's own Romanian words**, no visible change: the class words `la limita` and
   `SLAB` and the `scara` label come from the core (`SeriesReport`); `miscare >= ... px`,
   `(walking noise)` and `EROARE: ` (`MainViewModel`) and the map's `grila la ... px`
   (`DitherMap.OnRender`) move into `WindowTexts`.
4. **Dead code**: delete the unused `RelayCommand` (`Presentation/Mvvm.cs`) and
   `ChartElement.AxisBrush` (`Presentation/Charts.cs`).
5. **R-021 wording**: the hot-pixel runs are consecutive frames within 3 px of the run's first
   frame, not "of each other". Docs only; the code and the validation figures stay.

Then `/code-review high` on the branch: findings about this task's changes are fixed in a
commit of their own, test first for a bug; the rest are listed below as questions for the
owner. Then the merge is proposed.

## Assumptions stated

- No plan mode: the owner's message specifies every step, test and fix, so it is the approved
  plan, saved here before the first edit.
- `/validate` is not run: no numeric code (`Imaging/Stats.cs`, `Analysis/`, `Report/`) changes.
  Item 3 reads words from `Report/` and writes none there.
- Item 7's wrapped exception is `InvalidDataException`, the type a corrupt FITS already gives
  (R-008), so the console, `FrameInfo.Read` and the window's problem card handle a raw libraw
  refuses the same way; ADR 0019 records the choice.
- Item 5 also corrects the same words where a design note repeats them, if it does; nothing
  else.

## Verification

`scripts/verify.ps1` green at every commit (the hook runs it); the new tests fail before each
fix and pass after, shown in the session; `WindowSmokeTests` unchanged for items 3 and 4.

## FlatCheck prompt (item 7, the FITS half)

> FlatCheck: `FitsReader.LoadFull` (`FlatCheck.Core/FitsReader.cs`) reads `npix` samples from
> the data area without checking that the file holds them: on a FITS whose data is shorter
> than its header says (a truncated copy, a file cut mid-transfer), `ReadSample(raw, p,
> bitpix)` indexes past the end and the reader throws `IndexOutOfRangeException` instead of
> `InvalidDataException` naming the problem, as the header checks do (`HeaderIncomplete`).
> Test first (the kit's rule "Test before fix"): a FITS with a valid header for a 4 x 4
> 16-bit image and only 8 data bytes, through `FitsReader.Load`, expects
> `InvalidDataException` with a Romanian message naming the shortfall. Fix: before the pixel
> loop, when `pos + dataBytes > raw.Length`, throw `InvalidDataException` with that message.
> Requirements first if a FITS requirement names the reader's errors. LightsCheck carries a
> copy of this file (its ADR 0003, ADR 0018) and will refresh it after your merge; its plan
> `docs/plans/2026-10-25-fix-the-defects-the-backfill-found.md` names the gap.

## Questions for the owner

(filled after the review)

## What deviated

(filled at wrap-up)
