# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`.

## [Unreleased]

### Added

- A **Help** button with a question-mark icon in the window's band, and F1, open the wiki
  manual at the section about what the window shows: the window, the result, or the section
  on problems after a refusal or a warning; About opens from its own button. The window opens
  820 px wide, so the band's sentence keeps two lines (ADR 0016).
- A copy is refused when the destination's own plate solve is the mirror image of the
  source's, as in the SyQon CLI's top-down output: the image was flipped after the source was
  solved, so the copy would point the wrong way. `--force`, and the window's renamed option
  `Continue even when the dimensions differ or the image is mirrored`, let it through
  (ADR 0012).
- A diagnostic log beside each executable, `log\copywcs.log` and `log\CopyWcs.Wpf.log`, five
  files of 1 MiB at most, with each copy's options, the headers read, every card copied and the
  card of the destination it replaced, every card dropped, the file written, the time of each
  stage and every error
  with its technical details (ADR 0009); attach the `log` folder to a bug report;
  `COPYWCS_LOG` sets the level (`off` to `trace`).
- The About window lists the third-party components (NLog), and the About button's tooltip
  says so.
- The window, `CopyWcs.Wpf.exe` (ADR 0007): dark, one page, the source and the destination
  boxes (filled from the command line when two existing files are given), in place or a new
  file, the two options, Preview and Copy showing exactly what the console's dry run and copy
  do, the header facts under each box, an About window with the GPLv3 notice, English or
  Romanian by `--lang` as the console. At the top, FlatCheck's band: the version, the
  copyright, one sentence on what the tool does and the About button (ADR 0008). The window
  and the console share one icon, `Icon/copywcs.ico`.
- `--lang en|ro`: the console speaks English or Romanian, chosen on the command line, English
  when the option is absent (ADR 0004); `--lang` anywhere, `--lang=ro` too, a value that is
  not `en` or `ro` refused with exit 1.
- `README.md`, the user documentation, in English (ADR 0006).
- Two validation cases, on real headers kept as fixtures in the repo (ADR 0005): V-001, the keys
  the tool selects from a Siril 1.4.4 plate solve of the Veil session of 2026-10-06; V-002, that
  solve against the same stack after the SyQon CLI, a mirror image refused without `--force`,
  and the keys a forced copy drops (ADR 0011, ADR 0012).

### Changed

- The header is read and written byte for byte: a byte above 0x7F, which the FITS standard
  forbids but a file may hold, now comes out as it went in, in the copied cards and in the
  destination's own cards, where it was written as `?` before. The `HISTORY` card stays ASCII:
  a letter with a diacritic in the source's name is written as its base letter (`Nebula.fit`
  for a name with a-breve), a character with no ASCII form as `?`. The log names such a byte
  by its code, `\xC5`, instead of `?` (ADR 0010).
- The texts are English by default; the Romanian wordings are unchanged under `--lang ro`,
  the usage gaining its `--lang` line.
- The `HISTORY` card written into the destination reads `WCS copied from <file>` in both
  languages, where it read `WCS copiat din <file>` (ADR 0004).
- `-o` onto an existing file replaces it as the in-place write does, with a new file renamed
  over it: the file it replaces does not keep its creation date, its attributes or its own
  permissions, a symbolic link is replaced by the file itself, and a folder that lets you
  change files but not create them refuses the copy (ADR 0020).

### Fixed

- A copy takes the SIP distortion's largest corrections, `A_DMAX` and `B_DMAX`, with the rest of
  the plate solve, and drops the destination's old ones the source does not have, where it left
  both alone (ADR 0033; CR-020 of the review of 2026-11-16).
- A copy writes `WCSAXES` above the other cards of the plate solve, as the FITS standard asks,
  where a destination's own plate solve kept its cards above the copied `WCSAXES`; astropy reads
  either order the same (R-061, ADR 0032; CR-019 of the review of 2026-11-16).
- A file a copy replaces, the destination in place or an existing file given to `-o`, keeps its
  creation date, its attributes (Hidden, for one) and its permissions, where it got the copy's
  date and lost them; the copy is flushed to the disk before it takes the file's place, so a
  power cut right after a copy cannot leave it half written, at a cost of about 2.5 s for a
  290 MB stack on a hard disk; a replace that Windows leaves half done puts the old file back
  (ADR 0031).
- The window shows the warnings raised before a refusal above it, as the console prints them,
  where it showed them under it.
- `-o` followed by a word that starts with a dash, `-o --dry-run` with the file name forgotten,
  is a usage error (`--output needs a file name.`, exit 2), where it copied into a file named
  `--dry-run` without the dry run (ADR 0030).
- The window fits into its screen when it opens: on a small screen, a 1080p laptop at 150 %, it
  no longer runs under the taskbar. Its minimum height is 680 instead of 620, so the result
  keeps four cards in view with two warnings and the closing lines on screen, where it kept one
  (ADR 0029).
- A file with no image size in its main header (`NAXIS1` or `NAXIS2` missing, the image in an
  extension) is refused with a line that says so, even with `--force`, where it was reported as
  the size (-1, -1) with the advice to force, and two such files were copied (ADR 0028).
- In the window, a path pasted in double quotes, as File Explorer's "Copy as path" gives it,
  now names its file: the quotes are taken off, so its facts show and Preview and Copy come on.
- The copy's closing line counts as each language does: `Copied 1 key`, `Copiata 1 cheie`, and
  `Copiate 72 de chei` from 20 keys on, where it read `Copied 1 keys` and `Copiate 72 chei`
  (ADR 0013).

- A copy drops the destination's own plate-solve keys that the source does not have, such as
  a `CD` matrix left beside Siril's `PC` and `CDELT` by an earlier plate solve of the same
  image. Siril 1.4.4 reads the `CD` matrix first, so such a leftover would have hidden the copied
  solution. The optical keys are never dropped; the preview and the copy name the keys dropped
  (ADR 0011).
- The same file given twice is refused before anything is written, a dry run too: a new file
  (`-o`, or the window's `Write a new file`) that is the solved source, which the copy used to
  overwrite while saying `Copied`; a new file that is the destination, under any spelling, which
  ended in a "used by another process" error or rewrote the destination in place; and the source
  given as the destination, which was copied into itself. The files themselves are compared, not
  how their paths are written (R-019, ADR 0018; CR-001, CR-013 and CR-014 of the review of
  2026-11-16).
- Closing the window during a Preview or a Copy waits for it to end, the window greyed
  meanwhile, then the window closes by itself; the close used to end the program inside the
  write, leaving a new file cut short and a log without the copy's last lines (R-050, ADR 0019;
  CR-002 of the review of 2026-11-16).
- A write that fails midway, a full disk or a drive that drops, no longer leaves a file cut
  short: a new file is written under a temporary name beside it and renamed at the end, as an
  in-place write was, so a file already at its name keeps every byte; and a failed write, in
  place too, removes its temporary file, which it used to leave as big as the image. The
  temporary file has a name of its own, so a `<name>.tmp` of yours is never touched (R-012,
  R-014, ADR 0020; CR-003 and CR-016 of the review of 2026-11-16, and part of CR-018).
- A copy removes the destination's `CHECKSUM` card, which the changed header no longer matched,
  so a program that checks it saw the file as damaged; `DATASUM`, the data's own sum, stays, the
  data being untouched (R-018, ADR 0021; CR-004 of the review of 2026-11-16).
- A text too long for one card, carried on in `CONTINUE` cards, is copied whole: the copy took
  its first card alone, a value cut short with a stray `&`, and left the `CONTINUE` cards of the
  destination's own long value under the new one; the dry run and the window's Preview list the
  `CONTINUE` cards too, and still count keys (R-009, R-010, ADR 0022; CR-005 of the review of
  2026-11-16).
- In the window, a file name alone in the new file's box is written beside the destination,
  where it landed in whatever folder the window was started from, and the result names the
  file with its folder; a relative path is taken once, when it is given, so nothing that moves
  the program's working folder afterwards, a file dialog included, makes a copy read or write
  another folder's files of the same names (R-051, ADR 0023; CR-006 of the review of 2026-11-16).
- A file that is not FITS, a TIFF or a video picked by mistake, is turned away after its first
  2,880 bytes with "does not begin with SIMPLE", where it was read whole and then called
  truncated: 200 MB took 2.2 s and 585 MB of memory, and a file of several GB could crash the
  program; a header that never reaches its `END` card is given up after 1,000 blocks
  (R-015, ADR 0024; CR-007 of the review of 2026-11-16).
- The window checks the file a box names away from its own thread, so typing a path on a slow
  disk or on a share no longer freezes it: one letter of a path to a computer that does not
  answer took 2.3 s; the line under the box comes when the check ends (R-052, ADR 0025; CR-008
  of the review of 2026-11-16).
- The window's result empties when a box or an option changes, where it kept listing the cards
  of the files named before, and during a run it says `Preview in progress...` or
  `Copy in progress...`, where it kept showing the last result, a refusal too (R-053, ADR 0026;
  CR-009 of the review of 2026-11-16).
- The window reads its files again when it comes back to the front, when Browse picks a file,
  the same one too, and after every Preview or Copy: a file saved or plate-solved in Siril after
  its path was typed used to stay missing or `not plate-solved` until the box was edited (R-054,
  ADR 0027; CR-010 of the review of 2026-11-16).

### Internal

- NLog 6.2.2 and NLog.Targets.AtomicFile 6.2.2, the tool's first package, at FlatCheck's
  versions (the Astro Darkroom suite's folder holds one of each); `Logging/` adapted from
  FlatCheck's, and every test runs its log lines into a capture.
- The project adopted the ClaudeCodeStarterKit process (ADR 0001): the gate `scripts/verify.ps1`,
  the kit's skills, agent and rules, `kit.json`, the layout `src/` and `tests/`, the core library
  `CopyWcs.Core` under the console `CopyWcs.Cli`, the Python prototype under `prototype/`, the
  version and the copyright in `Directory.Build.props` (ADR 0003), the kit's analyzers and
  `.editorconfig` with the test packages at the kit's versions (ADR 0002).

## [1.0.0] - 2026-09-01

### Added

- The console `copywcs`: copies the WCS cards and the optical metadata of a Siril-solved FITS
  into another FITS of the same pixel grid, verbatim, with `--dry-run`, `--force`, `--no-optical`
  and `--output`; the version banner and the GPLv3 copyright on every run (2026-08-26 and
  2026-09-01, the three first commits).
