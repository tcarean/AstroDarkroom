# 0031. The write is flushed to the disk and replaces an existing file with Windows' replace

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

Every write goes through a temporary file beside the target (ADR 0020), which
`File.Move(temp, target, overwrite: true)` then put in the target's place. CR-018 of the review
of 2026-11-16 read what a move loses: the replaced file's creation time, its attributes and its
permissions, which become the new file's and the folder's; a destination that is a link is
replaced by a plain file; and nothing is flushed before the move. Its overwritten `.tmp` was
already gone with ADR 0020.

Measured on 2026-11-18, PowerShell 5.1 on E: (`[IO.File]::Replace` makes the same Windows call,
`ReplaceFile`, as .NET 8): a target created 2020-01-02 and marked Hidden keeps both through a
replace, and through a move gets the day's date and `Archive` alone; a read-only target refuses
the replace with `Access to the path is denied.` and is left as it was, as the move did; a
hard-linked target loses the link either way, the other name keeping the old bytes; a symbolic
link could not be made, this account needing administrator rights for it. With the Release
console of the branch, a copy into a file with an extra `Everyone:(R)` entry kept the entry.

The flush, measured the same day: copying the 290 MB Veil stack on E:, a hard disk, took 2,060
(cold), 395 and 386 ms without `Flush(true)` and 2,623, 3,307 and 2,761 ms with it, in a
PowerShell probe; the branch's Release console then wrote the stack with `-o` onto E: in 2,711,
3,243 and 2,730 ms, and into the scratchpad on C: in 1,034, 971 and 959 ms, where it took 315 and
307 ms warm before (ADR 0019's figures).

## Decision

The temporary file is flushed to the disk before it is closed, and then put in the target's
place: an existing target by `File.Replace(temp, target, null, ignoreMetadataErrors: true)`, a
target that does not exist by `File.Move` (R-012). The replaced file keeps its creation time,
attributes and permissions; a drive that cannot carry permissions over (FAT, some shares) does
not fail the copy. The owner chose the replace and the flush, with their cost, on 2026-11-18.

## Alternatives rejected

- **The replace without the flush.** No wait, but the file system journals the rename and not
  the data: a power cut in the seconds after a copy could leave the target's name on a file not
  yet written, the owner's stack lost. The owner chose the 2 to 3 s per 290 MB.
- **Copy the times and attributes by hand after a move.** More code for less: the permissions
  and the other metadata `ReplaceFile` carries would still be lost.
- **Write through a link to the file it names.** Not measurable on this PC, and none of the
  owner's files is a link; left as the entry recorded it.

## Consequences

A copy takes about 2.5 s longer per 290 MB on a hard disk, during which the window says
`Copy in progress...` (R-053) and holds a close (R-050). A target that disappears between the
existence check and the replace is a write error, its temporary file deleted (R-014). The two
limits of a write through a temporary file stay: the drive needs room for both files, and a
folder where a file may be changed but not created refuses the write (ADR 0020). A hard link is
broken, as before; a symbolic link was not measured. No test can see the flush. Guarded by
`WcsCopyTests.R012_Run_AReplacedFileKeepsItsCreationTimeAndItsAttributes` and
`R014_Run_AReadOnlyDestination_IsAWriteErrorThatLeavesTheFolderAsItWas`.
