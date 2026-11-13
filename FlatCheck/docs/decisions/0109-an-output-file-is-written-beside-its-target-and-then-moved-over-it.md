# 0109. An output file is written beside its target and then moved over it

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

Every file the two applications write for the user was written into the file already there:
`FileMode.Create` and `File.WriteAllBytes` first cut the existing file to nothing and then
stream the new bytes in. The review of 2026-11-13 (CR-012) followed the largest one, the
synthetic flat of about 96 MB on a 24 MP master: a full disk, a drive that drops or the window
closed during the write (`MainWindow.OnClosing` waits only for a split) leaves a FITS with a
whole header and cut data under the real name, the earlier good file is gone, and `Compare
with the master` then fails with the cut-data message. `SessionSettings.Write` already avoided
this for `flatcheck.json`, with a temporary file and a move.

Measured on this machine (Windows 10 19045, .NET 8) while the fix was written: `File.Move(temp,
target, overwrite: true)` onto a target another handle holds open fails with
`UnauthorizedAccessException`, "Access to the path is denied.", whatever sharing that handle
allows (read and write and delete included), and the target stays as it was.

## Decision

The owner's choice of 2026-11-13, between the three writes the entry names and every file the
program writes for the user:

1. **Every output goes through one function**, `OutputFile` in the core: the console's map and
   synthetic flat; the window's synthetic flat, the Structure tab's report and map and the
   Split tab's report and mosaic. One rule for the reader of the code and of the manual: a file
   FlatCheck wrote is the earlier one whole or the new one whole.
2. **The bytes go to `<name>.tmp` beside the target**, on the same volume, and that file is
   moved over the target once the write has ended. The existing file is never opened for
   writing.
3. **The temporary name is fixed.** A file left by a process that ended mid-write is
   overwritten by the next write of that output, so nothing piles up in a session folder.
4. **A failure leaves the target as it was.** A failed write deletes the temporary file and
   throws what it threw. A failed move becomes an `IOException` that names the target and the
   two causes a user can act on (open in another program, read-only), with the system's
   message in brackets, because the system's message alone names neither the file nor the
   cause.

5. **A device is written into directly, and a folder is refused by name** (added by the
   review of the branch, which found `-o NUL` ending in "Could not replace NUL", exit 1, after
   the whole report). A device (`NUL`, `/dev/null`) holds no earlier file and cannot be
   replaced by a move; a folder named as the output got the sentence of point 4, whose two
   causes were not the cause.

## Alternatives rejected

- **Only the three writes of the entry.** The four small saves of the Structure and Split tabs
  fail the same way, only less often; two rules for seven writes would have to be explained.
- **A random temporary name.** Two writes of one output at once would not collide, but a
  process that ends mid-write leaves a 96 MB file nobody ever removes. The window's heavy-run
  gate already keeps one write of an output at a time; a second window on the same folder gets
  an error, not a mixed file.
- **Fall back to writing in place when the move is refused.** It would keep working for a
  target a viewer holds open with write sharing, and bring back the cut file in exactly that
  case.
- **`File.Replace`.** It keeps the target's attributes and security descriptor, which these
  outputs do not need, and it fails on a target that does not exist yet, so the first write
  would need a second path.
- **Flush to the disk before the move**, against a power cut. The outputs are made again by
  one run; the cost is a slower write of the 96 MB every time.

## Consequences

- A target that another program holds open is no longer written into, even when that program
  would have allowed it; the write fails with the new sentence and the earlier file stays. A
  file that was locked against writing failed before too, with another message.
- For the moment of the write the folder holds both files, so the write needs room for the new
  file beside the old one.
- A `<name>.tmp` may stay in a session folder after a process that was ended mid-write, until
  the next write of that output.
- An output folder that does not exist is reported by the system with the temporary file's
  name (`...\map.png.tmp`), not with the name typed.
- A symbolic link named as the output is replaced by a file; before, the write went through
  the link to its target.
- Off Windows the move asks the folder's permission, not the file's: a read-only target is
  replaced there, where the write into it failed before. The console builds on Linux and
  macOS, and nothing of this was run there; `/dev/` paths are taken as devices so that a run
  as root cannot replace `/dev/null` with a file.
- CR-040 (a save failure that names the wrong file on the Structure and Split tabs) is not
  changed here: the calls moved, the messages around them did not.
- R-138 holds the behaviour; `R138_...` in `OutputFileTests`, `SyntheticTabTests`,
  `StructureTabTests` and `SplitTabTests` pin it for each of the seven writes,
  the earlier file seen through a second name (a hard link) that keeps the old bytes only when
  the write did not go into it.
