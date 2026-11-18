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

Both paths are made whole once, as the write begins, so the rename lands beside its temporary
file and the data is read from the file the write began with, even when the working folder moves
meanwhile, as a file dialog of the window may (CR-006). The review of the branch found the
temporary file's folder fixed at the start and the target looked up again at the rename, which
could have replaced a file of the same name in another folder; shown by a test that moves the
working folder at the write's first log line.

The `Write error:` line names the file asked for where the system's message names the temporary
file: `Write error: Could not find a part of the path 'E:\out\final_wcs.fit'.`, as before this
change. The exception goes on as the system raised it, the two paths in its `Data`, and the log
writes it so; `FitsHeader.WriteErrorMessage` makes the line's message. The owner's answer of
2026-11-18, after the review of the branch found the line naming
`final_wcs.fit.copywcs-3f9a0c1e.tmp`, a name the user never typed.

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
run to run, so the two tests that compare two runs' log lines (R-046's English and Romanian logs,
R-043's window and console) mask it as they mask the times; R-023's figures, on screen, need no
mask, since the screen never shows that name. A rename the system refuses (a read-only target)
says `Access to the path is denied.` with no path, as the in-place write's always did; for `-o`
onto a read-only file, `File.Create` used to name it. A temporary file stays behind only when the
process is killed, or when its removal fails too (the drive that holds it gone), and the log's
Warn line names it then.

A new file that replaces an existing one is now a new file renamed over it, as the in-place
write was, so the rest of CR-018 reaches `-o` too: the replaced file's creation time, attributes
and permissions are not kept, its other hard-linked names keep the old file, a symbolic link is
replaced by a plain file, and nothing is flushed before the rename. The replace also needs room
for both files, a folder where the user may change the file but not create one refuses it, and
a name longer than 234 characters no longer fits NTFS's 255 once the 21-character suffix is
added (251 for the in-place write's `.tmp` before). CR-018 stays open for those. Guarded by
`WcsCopyTests`' `R014_` and `R012_` tests (a byte-range lock fails a write midway, a read-only
destination fails the rename, a missing folder's line names the new file),
`WorkingFolderTests` (the working folder moved during the write) and
`LoggingTests.R042_AFailedWriteRemovesItsTemporaryFileAndSaysSo`.
