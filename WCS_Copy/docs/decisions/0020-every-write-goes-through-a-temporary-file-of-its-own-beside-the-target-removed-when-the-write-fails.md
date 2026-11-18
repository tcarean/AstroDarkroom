# 0020. Every write goes through a temporary file of its own beside the target, removed when the write fails

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

Only the in-place write went through a temporary file, `<dest>.tmp`, moved over the destination
at the end. A new file was opened with `File.Create`, which empties a file of that name at once:
the review of 2026-11-16 found (CR-003) that a write that fails midway, a full disk or a drive
that drops, left the new file with its header and part of its data, and had already destroyed a
good file of that name, though R-014 says a failed write leaves no target. A failed in-place
write left the `.tmp`, as big as the image, which `docs/design/fits-header.md` described as
intended (CR-016, an entry for the owner), and the fixed name overwrote a `<dest>.tmp` of the
owner's (CR-018). On 2026-11-18 the owner chose to delete the temporary file of a failed write in
place too. Shown before the fix by a test that locks a byte range in the middle of the
destination's data: the copy into a new file failed after its header and 80 KB and left that
file; into an existing file, it left the existing file changed.

## Decision

`FitsHeader.WriteWithDataFrom` writes every file, new or in place, to a temporary file in the
target's folder, `<name>.copywcs-<8 random hex digits>.tmp`, created only when no file holds that
name, and renames it over the target once it is whole (R-012). When anything fails before the
rename, a `finally` deletes the temporary file it created; the target and the destination keep
every byte, and the run is the usual `Write error:` refusal (R-014).

## Alternatives rejected

- **Keep `File.Create` for a new file and delete it on failure.** A file of that name the user
  had agreed to replace would already be emptied when the write began.
- **Keep the fixed `<name>.tmp`.** It overwrites a file of the user's of that name, and with
  `CreateNew` a leftover one would block every later write.
- **Leave a failed write's temporary file, as the design note said.** A partial file has no use:
  the destination is whole, and the temporary file only fills the disk.

## Consequences

The log's write line names the temporary file for both writes, and a failed write adds the
removal at Debug, or a Warn line when the removal fails. The random part of the name differs from
run to run, so the three tests that compare two runs' lines (R-023's figures, R-046's English and
Romanian logs, R-043's window and console) mask it as they mask the times. A write error raised on
the temporary file names it in its message (a missing folder: `Could not find a part of the path
'...\out.fit.copywcs-3f9a0c1e.tmp'`). Only a killed process leaves a temporary file behind. The
rest of CR-018 (the replaced file's times, attributes and permissions, a destination that is a
link, no flush before the rename) is not changed. Guarded by `WcsCopyTests`' `R014_` and `R012_`
tests (a byte-range lock fails a write midway, a read-only destination fails the rename) and
`LoggingTests.R042_AFailedWriteRemovesItsTemporaryFileAndSaysSo`.
