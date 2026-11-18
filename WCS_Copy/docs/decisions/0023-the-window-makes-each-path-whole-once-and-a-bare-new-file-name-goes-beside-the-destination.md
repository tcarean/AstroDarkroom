# 0023. The window makes each path whole once, and a bare new-file name goes beside the destination

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The window passed each box's text to the core as typed, and the core and the facts looked a
relative path up in the process's working folder each time they used it. CR-006 of the review of
2026-11-16 read two cases. `out.fit` in the new file's box wrote into the folder the window was
started from (the launcher's, or a shortcut's "Start in"), and the window said `Copied 15 keys
into out.fit.` with no folder. A working folder that moved between the facts and the copy would
copy between another folder's files of the same names; the second review of CR-001 to CR-005
showed a move after the destination's read write the first folder's header over the other
folder's file. Whether the window's own file dialogs move the working folder decided between
severity 1 and 3. Measured on 2026-11-18 with .NET 8.0.34, the dialogs run on a desktop of their
own and driven by window messages: the Save dialog never moved it, with or without
`RestoreDirectory`, and the Open dialog with `RestoreDirectory` did not; the Open dialog without
it, what the two Browse buttons used, was not measured, since the security software of the PC
refused every new build of the probe after its first run. A window has no folder of its own on
screen, as a console has its prompt.

## Decision

The window makes each path whole once, when it is given, and uses that whole path for the facts,
the run and the core's lines (R-051): a file from the command line, and a source or a
destination typed relative, against the folder the window started in, the command line's files
shown whole in their boxes; a new file typed relative against the destination's folder, so a
bare name goes beside the destination, the owner's answer of 2026-11-18. A box keeps its text as
typed. The three file dialogs set `RestoreDirectory`, so nothing in the window moves the working
folder.

## Alternatives rejected

- **A relative new file against the start folder, as the console's `-o`.** In a window that
  folder is not on screen, and is often the program's own; the owner chose the destination's.
- **Rewrite a box's text as its whole path.** The box is bound at every keystroke: rewriting it
  under the cursor would fight the typing.
- **`RestoreDirectory` alone.** It keeps the folder still for the dialogs, but anything else that
  moves it would still move every path, and `out.fit` would still say no folder.

## Consequences

The core's lines in the window name whole paths (`Copied 15 keys into E:\Night\out.fit.`, a
refusal's `'E:\Night\x.fit'`), and the window's start line in the log names the command line's
files whole. The window and the console now read a relative new file differently, each against
the folder its user sees. Guarded by `AppTests.R051_TheFilesOfTheCommandLineAreMadeWhole`, the two
R-051 tests of `MainViewModelTests` (a working folder moved after the paths were typed, a bare
new-file name) and `WindowSmokeTests.R051_TheFileDialogsPutTheWorkingFolderBack`.
