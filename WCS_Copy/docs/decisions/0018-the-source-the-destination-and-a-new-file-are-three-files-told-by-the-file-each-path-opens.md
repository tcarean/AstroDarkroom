# 0018. The source, the destination and a new file are three files, told by the file each path opens

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The review of 2026-11-16 (ADR 0017) showed three runs that name one file twice. CR-001: `-o`, or
the window's `Write a new file` box, naming the source, wrote the destination's data under a new
header into the solved source and said `Copied 15 keys`; the solved stack was gone. CR-013: the
same file as source and destination was copied into itself and gained `HISTORY WCS copied from
stack.fit`. CR-014: `-o stack.fit` for the destination `Stack.fit` ended in a write error, "used by
another process", because `FitsHeader.WriteWithDataFrom` took the in-place write only when the
two full paths were equal as strings, and Windows ignores letter case. The list named the three
one group, one check by the file each path names. On 2026-11-18 the owner chose to fix the group
in one task.

## Decision

After both headers are read and before anything is selected or written, a dry run included,
`WcsCopy.Run` refuses a source that is the destination, then a new file that is the source, then
a new file that is the destination, each with its own line in both languages (R-019). Whether
two paths name one file is told by the file each opens: `FileIdentity.Same` compares the volume
serial number and the file index Windows gives the two open files, so a letter case, a `..`, a
`\\?\` prefix, an 8.3 name or a link counts as the file it leads to. A new file that names the
destination is refused under every spelling, the destination's own included.

## Alternatives rejected

- **Compare the full paths, ignoring case.** It sees CR-014's letter case but not a `\\?\`
  prefix, a short name, a `subst` drive or a link, which name the same file just as surely.
- **Take a new file that names the destination as the in-place write.** R-012 says a new file
  leaves the destination as it was; and for a hard link, the in-place write's rename would leave
  the other name holding the old file.
- **Check in the applications.** The window and the console reach the same run, and the window
  computes nothing (ADR 0007).

## Consequences

The core makes its one call into Windows, `GetFileInformationByHandle`, a `LibraryImport` that
needs `AllowUnsafeBlocks` in `CopyWcs.Core.csproj`, as `DarkTitleBar` does in the window. Where
the call cannot tell (off Windows, a failure, a file system that gives the index 0), the full
paths are compared. A path that cannot be opened is never the same file as another, so a
missing new file is written and an unreadable source is still R-004's refusal. A run that used
the destination as its source, as one window test did for a source without keys, is now this
refusal. Guarded by the `R019_` tests in `WcsCopyTests`, `CopyWcsCliTests` and
`MainViewModelTests`, `FileIdentityTests`, R-023's three scenarios and R-042's done lines.
