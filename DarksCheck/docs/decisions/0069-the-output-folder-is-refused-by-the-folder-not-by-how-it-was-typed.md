# 0069. The output folder is refused by the folder, not by how it was typed

Date: 2026-11-19
Status: accepted 2026-11-19

## Context

Nothing is written into the session, the partner's or the chosen lights' folder (the rule
"Read-only input", R-026, R-055, R-074). `DarkOutputs.CheckOutDir` enforced it by comparing
full paths: the output folder was refused when its path was the path of the folder given, or
began with it. The code review of 2026-11-13 (CR-019, `confirmed`) showed that the answer then
depends on the spelling:

- `darkscheck E:\lib\X\darks --out E:\lib\X\report` wrote into `X`, while `darkscheck E:\lib\X
  --out E:\lib\X\report` was refused. R-061 already treats `X\darks` as the library `X`.
- `--lights E:\S\lights --out E:\S\report` wrote into the session `S`; `--lights E:\S` refused
  it.
- Another name of the same folder passed: a `subst` drive, a junction, a short 8.3 name, and
  `\\?\E:\S\out`, which `Path.GetFullPath` leaves as it is.

No frame is touched in any of these and the files do not land among frames, which is why the
entry is of severity 2 and not 1.

## Decision

1. **A folder given as a session's subfolder stands for the folder around it.** When the darks
   are listed in the folder opened and that folder is named `darks`, the folder around it is
   kept clear as well; the same for a partner given as `<name>\darks`, and for lights listed in
   a chosen folder named `lights`. Not when the folder around it is a drive's root, which is no
   session. The refusal names the folder around it, in today's words.
2. **Two paths that the system says are one folder are one folder.** On Windows a folder is
   opened for its attributes alone and asked for its volume serial number and file index
   (`FolderIdentity`, after CopyWcs's `FileIdentity`, its ADR 0018). The output folder is taken
   by the path the system resolves it to, every link followed, from its deepest folder that
   exists, and that folder and every folder above it are held against the protected folder's
   pair. The folders of the path as typed are not enough: a junction beside the session that
   leads to a folder inside it, its `darks\` included, has neither the session's identity nor
   the session above it (found by the review of the branch, which wrote the files among the
   frames that way). The folder around a `darks` or `lights` folder (point 1) is taken from the
   name as given and from the resolved path, both: a library opened through another name of its
   `darks` folder keeps the library clear, and a link named `darks` keeps the folder around the
   link clear (the second review of the branch: the resolved name had taken the given name's
   place, and the folder around the link was written into again). The full paths are compared first, without case and without a `\\?\` prefix
   before a drive letter, which also answers for an output folder that does not exist yet.
3. **One list of folders, used twice.** `DarkOutputs.ReadOnlyFolders` gives the folders,
   `CheckOut` refuses the output folder in any of them; the console calls it before the run and
   `WriteAll` after it, so the window's export refuses exactly what `--out` refuses.

Three calls into Windows' `kernel32` (`CreateFileW`, `GetFileInformationByHandle`,
`GetFinalPathNameByHandleW`) are source-generated as unsafe code, so the core project allows
unsafe blocks; no package is added.

## Alternatives rejected

- **Resolving the paths in managed code** (link targets, component by component). It covers
  junctions and links and neither a `subst` drive nor an 8.3 name, and it re-implements what
  the file system answers in one call.
- **Refusing only the two spellings the entry's "should" names.** The title of the entry is the
  defect: one folder must get one answer, whatever name it was reached by.
- **The session subfolders `biases` and `flats` treated like `darks` and `lights`.** No option
  takes such a folder, so none can be given.

## Consequences

`--out` beside a library's `darks` folder, or beside lights given as a `lights` folder, is
refused where it was accepted: the files go to a folder outside. A folder named `darks` or
`lights` that is no part of a session (`Desktop\darks`) keeps the folder around it clear too,
which is the same answer the folder around it gives when opened. On a file system that gives no
file index, and anywhere but Windows, the paths alone are compared, as before. A `subst` drive
was not tried: making one changes the machine. A short 8.3 name was run by the second review
on a folder that already had one, and refused both ways. Two holes are left, both run by that
review and entered in the review list as CR-080: the links are followed on the output's side
only, so a session whose own `darks\` is a link to a folder elsewhere does not keep that folder
clear; and a link that sits past the 260th character of the output path is not followed where
Windows' long paths are off, since the folder is opened by its path as typed. Guarded by
`R026_OutBesideTheDarksOfALibraryKeptAsNameDarksIsRefused_Exit1`,
`R026_OutThroughAnotherNameOfTheSessionIsRefused_Exit1`,
`R026_OutBesideALinkNamedDarksOrLightsIsRefused_Exit1`,
`R026_AFolderIsHeldAgainstAnotherByItsFullPathWhateverItsSpelling`,
`R026_AJunctionToAFolderIsThatFolder`, `R026_AJunctionToAFolderInsideIsInsideToo` and
`R026_AFolderNamedDarksOrLightsStandsForTheFolderAroundIt`.
