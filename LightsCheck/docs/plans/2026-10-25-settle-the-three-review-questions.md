# Settle the three review questions of the defects task

## Context

The defects task (`2026-10-25-fix-the-defects-the-backfill-found.md`, merged into `main` as
6fb3076) left three review questions for the owner, each with a recommendation, and the FITS
half of its item 7 open on a FlatCheck task. The owner answered on 2026-10-25: fix all three
now, and the FITS half when FlatCheck's fix is ready, on the owner's word. The owner's word is
the plan; no plan mode. Branch `claude/Settle-the-three-review-questions`, cut from `main`.

## Steps, one commit

1. **The `EROARE: ` line is the core's.** `RunReport.Error(message)` replaces the two literals
   of `Program.cs` and `WindowTexts.Error`, which is deleted. No behaviour change: the tests
   that assert the line keep passing; `VerdictPalette`'s `StartsWith("EROARE")` stays as it is.
2. **The reader's message is Romanian only.** Requirement R-007 first, then the R-007 reader
   test pins `<file>: libraw nu poate citi fisierul` on `Load` and `LoadInfo` and the inner
   exception naming the file (it failed on the bracketed message, then passed), then
   `RawReader.Unreadable` drops the brackets. ADR 0019's bracket clause is superseded in part
   by ADR 0020; ADR 0019's Status says so.
3. **The redundant test goes.** `RawReaderTests.AFileThatIsNotActuallyARawIsRefused` is
   deleted on the owner's word; the R-007 test is its stronger form.

Then the notes (`imaging.md`, `session-and-console.md`, `window.md`), the CHANGELOG's Fixed
line for the raw error, and the validation cases, since `Report/` is touched (`RunReport`).

## Not in this task

The truncated-FITS crash of `--frame` (`FitsReader.LoadFull`, a FlatCheck copy): the FlatCheck
prompt is in the defects task's plan; the owner says when FlatCheck's fix has landed, then
`FitsReader.cs` is recopied here with its tests, a task of its own (ADR 0018 keeps the
FITS-magic check of FlatCheck's ADR 0057 for that task too).

## Verification

`scripts/verify.ps1` green at the commit; the validation cases on the real sessions unchanged.

## What deviated

Nothing in the steps. The validation cases ran on the real sessions under
`E:\Photografie\Astro` (2026-10-25): `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13,
Duration: 15 m 23 s` (slower than the usual 8 min because the unit tests built in parallel);
no figure of `docs/validation.json` moved, as expected of a message and a prefix.
