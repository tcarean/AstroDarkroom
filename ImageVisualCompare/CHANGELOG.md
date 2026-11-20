# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The `M` key hides the minimap and the zoom badge, and shows them again: from 100 % up the
  bottom-left corner of the image could only be brought under the minimap, and the top-right
  corner under the badge, with no way to see what was there. The choice holds for the next pair;
  the status bar's key reminder names the key, `M minimap` (R-067, ADR 0059).

- `Help` (`Ajutor`) in the toolbar, between `Open…` and `About`, and the `F1` key: they open the
  window's manual in the wiki at the section of the view on screen, the window, Histogram or
  False colours, or opening a pair while one loads. The status bar's key reminder ends with
  `F1 help` (R-066, ADR 0056).

- `Change A…` and `Change B…`, after each file name in the toolbar: one dialog, and the file
  chosen takes that image's place while the other image stays as it is, not read again; the
  zoom, the position and the line stay where they were when the canvas keeps its size, and the
  padding question is asked when the sizes differ. `Open…` and `Ctrl+O` still open a whole pair
  (R-009, R-065, ADR 0047).

- A diagnostic log, for the day a problem is reported: `log\ImageVisualCompare.Wpf.log` beside
  the program (or `%LOCALAPPDATA%\ImageVisualCompare\log` when that folder cannot be written),
  in plain text, 1 MiB a file and the five most recent files kept, two windows writing into the
  same file. It holds the start's facts, every pair opened and how, what each file turned out
  to be and what its decoder made of it, the time each step of a load took, the padding
  question, the mode switches, the zoom, the memory, and every error with its technical details
  beside the message shown, in English whatever the interface's language. The environment
  variable `IMAGEVISUALCOMPARE_LOG` sets how much, from `off` to `trace`, which adds every FITS
  header card, a refused file's too. Written with NLog, the first package the program references,
  which the `About` window names with its licence, its copyright line and its address, the
  licence's whole text in `THIRD-PARTY-NOTICES.md` beside the source (R-120 to R-123, R-075, ADR
  0043).
- Two display modes for a pair of FITS or float TIFF files, the files shown as stored:
  `Histogram` equalises each channel on its own histogram as Siril 1.4.4's "Histogram" display
  does, and `False colours` shows one layer, the mean of the planes, through Siril's rainbow
  colour map. The toolbar buttons `Histogram` and `False colours`, or the keys `H` and `F`,
  switch, a key held down switching once; the pair is decoded again in the new mode with the
  zoom, the pan and the divider kept, the status bar names the mode, and in false colours the
  readout shows the level a colour stands for. A pair with a PNG or an integer TIFF stays linear
  and the buttons are disabled (R-110 to R-114, ADR 0040, ADR 0044).

- The interface is in English or in Romanian, chosen on the command line with `--lang=ro` (or
  `--lang ro`); without it the interface is English, and a language it does not have is said in
  a message before the interface comes up in English. Numbers read the same in both
  (R-100 to R-102, ADR 0038).
- FITS files (`.fit`, `.fits`, `.fts`) open as image A or B, next to or against a PNG or a
  TIFF: 8, 16 and 32-bit integers, 32 and 64-bit floats, one plane (gray) or three (RGB). The
  values are shown as stored, with no stretch, at the level the same value has in a 16-bit
  PNG; the first row of the file is at the bottom whatever `ROWORDER` says, as Siril shows it;
  a float file that counts to 65 535 instead of 1 is recognised and the status bar says so; a
  damaged header or an image too large to show is a message (R-014 to R-019, ADR 0029, ADR
  0039).
- The application has its own icon, in the window's caption, the taskbar and Explorer: a dim
  star field and a brighter one either side of the turquoise divider (R-064, ADR 0030).

### Changed

- A FITS that Windows does not have in its file cache, as at the first open after a restart,
  loads faster from a hard disk: each plane is read in bands of 4 MiB, not a row at a time from
  three places in turn. Measured on a 277 MB file of three float planes: 2.3 to 2.4 s where it
  took 4 to 5 s; the image decoded is the same, byte for byte.
- Every toolbar and dialog button has the 10-pixel padding its style always declared; the
  border hugged the text before.
- `About` opens a window laid out as the About windows of the suite's other tools, in this
  tool's colours: the icon, the name, the version and the copyright, a card with the licence
  notice and a link to the GPL's text, a card with the components written by others, NLog with
  its licence, copyright line and link, and `Close`, which Enter and Esc press too. It was a
  message of plain text (R-072, R-075, ADR 0045).
- The projects target .NET 8 instead of .NET 9, so the Astro Darkroom suite ships one runtime
  (ADR 0037).

- A TIFF of 32-bit float samples, as Siril and GraXpert export, is reported `32-bit float RGB`
  (or gray, RGBA) and shown as stored, as a FITS of the same samples is, with the same 0..1 or
  0..65535 rule. WIC's own conversion showed it about 73 levels brighter, and reported the RGB
  ones as `96-bit` (R-090, R-091, ADR 0035).
- `README.md`, the user documentation, is in English, the language the interface starts in, and
  was checked against the current behaviour: it no longer says the two images must have the
  same size, gives the memory at fit and after zooming, and now covers cancelling an open
  dialog, the scrollbars, the zoom stops, the 8-bit readout and where the keyboard focus goes;
  its section for developers is cut to pointers into `CLAUDE.md` and `docs/` (ADR 0041).

### Fixed

- At `Fit`, on some window sizes, a scrollbar with a thumb as long as its track and the minimap's
  rectangle around the whole thumbnail were shown, though the whole image was on screen: a
  rounding residue of a millionth of a millionth of a pixel counted as an image larger than the
  view. With a 5938 × 3888 image it happened at 85 of the 900 view heights from 600 to 1499
  (R-043, R-041).
- A FITS whose `BZERO` or `BSCALE` is not a number (`3.2768d4`, `32768,0`) was shown with the
  default, 0 or 1, in silence: with a `BZERO` of 32768 lost, 32,768 too low and its dark half
  black. Such a file now gives `FITS: the header is not valid.` (R-019, ADR 0058).
- A FITS card written with no space after its `=` (`NAXIS1  =5938`) lost the first character of
  its value: a width of 938, or `BITPIX` 32 for -32, shown with no message. The value is now read
  from right after the `=`, as Siril reads it (R-014, ADR 0057).
- A switch of display mode gave the outgoing pair's memory back only at the next load, not after
  its first render as intended, so the process carried two pairs in between: on the real FITS pair
  the release left 665.9 MiB where it now leaves 403.8 MiB (R-081, R-113, ADR 0047).
- `LICENSE` now sits next to the program, in the build folder and in the publish folder, where
  the About window says the full licence text is (R-074, ADR 0027).
- A file WIC fails with an error of its own, an image too large to decode or a damaged WebP,
  HEIC or AVIF, gives the usual message instead of closing the application (R-005, ADR 0031).
- `Ctrl+O` and `Open…` do nothing while a pair is being opened; a second open could start on
  top of the first, and two pairs decode at once (R-006).
- A FITS whose `BZERO` or `BSCALE` is NaN or infinite is refused as an invalid header instead of
  showing all black or all white, and an empty primary with a `PCOUNT` no longer hides the image
  in the extension behind it (R-014, R-019).
- `−` never zooms in: in a window so small that the image fits under 2 %, it went up to 2 %, and
  now goes to the fit (R-024, ADR 0033).
- A click in the minimap centres the view on the point under the pointer; it was off by the
  minimap's 1-DIP border, 31 image pixels of a 5938-pixel-wide image. The viewport rectangle
  stays on the thumbnail at the far edge (R-041, R-042).
- `100%` is one image pixel per physical pixel on every monitor, also on one scaled differently
  from the main display: the application declares per-monitor DPI awareness (R-022, ADR 0034).
- A click on a toolbar button (`−`, `+`, `Fit`, `100%`, `Histogram`, `False colours`, `Open…`,
  `About`) no longer keeps the keyboard focus: `+`, `−`, the arrows, `Ctrl+0` and `Ctrl+1` work
  right after it, without a click on the image (R-063, ADR 0042).
- The pixel readout names the pixel that is drawn under the pointer. It could name, and read
  the values of, the pixel one row or one column before it: at `100%` right after `Fit` on some
  window sizes (593 of 976 screen rows for a 5938 × 3888 pair on a 1920 × 976 surface), and from
  400 % up on the first screen row or column of a pixel's square, one in four at 400 % (R-028,
  ADR 0052).
- At `100%` every screen pixel shows exactly one stored pixel. The image could sit between
  screen pixels and be drawn blended, a pixel spread over two rows or two columns: always in
  the main window, whose image area ended on a fraction of a pixel, and on any window of odd
  width or height. At every whole zoom the image is now drawn on whole screen pixels (R-022,
  ADR 0053).
- A pair named on the command line by relative paths (`ImageVisualCompare.Wpf.exe a.fit b.fit`)
  is held by its full paths. `Open…`, `Change A…` and `Change B…` then open in the files' folder,
  where they opened wherever Windows chose, and a switch of display mode reads the same two
  files again whatever folder an open dialog was last in (R-002).
- A float TIFF whose transparency is stored already multiplied into its colours (associated
  alpha) is shown as stored. Its colours were divided by the alpha first: a pixel stored as 0.2
  at an alpha of 0.5 showed at level 102, where the same values in a FITS show at 51, and a
  fully transparent pixel showed black whatever it held (R-091).
- Transparency is no longer shown: every image is drawn solid, in the colours Windows decodes
  for its pixels, as a FITS and a float TIFF already were. A half-transparent pixel holding 200 was drawn at 100
  over the black background while the status bar read 200, so a PNG with transparency could
  look different from an export without it while the two readouts were equal (R-010, ADR 0054).
- The pixel readout follows what is under a pointer that does not move: after `H` or `F`, after
  `Change A…` or `Change B…`, after a zoom or a pan, and while the line is dragged. It was
  written only when the pointer reached another pixel, so it could go on showing the values of
  the images that were on screen before (R-051).
- A float FITS or float TIFF shows the levels of the 16-bit PNG or TIFF Siril exports of it,
  for every value. The 16-bit step was computed in double precision where Siril computes it in
  single: 127 of the float values between 0.5 and 1 came out one level lower than in Siril's
  export, a difference of one level that was in neither file. None of the five real float
  stacks measured had such a pixel (R-015, ADR 0055).

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0007): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and `<Version>` and `<Copyright>` moved from the
  Wpf csproj to `Directory.Build.props`, where the kit's release script reads them. `CLAUDE.md`
  was split into `docs/requirements.md` (45 rows with IDs), `docs/decisions/` (26 ADRs, 19 of
  them retroactive) and `docs/design/` (6 notes, moved unchanged); `CLAUDE.md` holds only the
  project's rules. `RepoInvariantTests` ties `implemented` rows to tests named `R###_...` (102
  tests renamed for 31 rows), and `ValidationTests` checks two cases on a real 23 MP pair outside
  the gate. The comments of three files turned English. No behaviour changed; one
  `dotnet format` pass over 4 files.
- `Directory.Build.props` declares the `win-x64` runtime the release publishes for, on every
  project but the test project, so a release's publish no longer rewrites the lock files and
  stops the release on them (ADR 0028).
- Every type and method in `src/` has an XML documentation comment, with a tag for each
  parameter and for the return, and every coordinate's pixel space named (the kit's rule
  "Documentation comments", the kit's ADR 0035); six comments already there that did not match
  the code were corrected. `Directory.Build.props` no longer mutes CS1573, so a comment that
  tags only some parameters fails the build (the kit's ADR 0035, recorded on ADR 0003). No code
  changed.
- A defect a window or a control shows without being shown is now tested: the test project runs
  them on one STA thread of its own (`WpfThread`), `MainWindow` and `MinimapView` first (ADR 0032,
  superseding ADR 0026 in part).

## [1.1.1] - 2026-09-09

The state of the tool when the changelog was started, built on 2026-09-08 and 2026-09-09 in
ten commits:

- 1.0.0 (2026-09-08): the slider over two identically sized images, one shared transform, zoom
  anchored at the cursor with a ladder of stops, the divider with its handle and arrow-key
  nudges, the minimap, floating scrollbars, the pixel readout from both images, WIC decoding of
  any PNG or TIFF with the source format reported, the memory readout and the release of the
  outgoing pair; the GPL notice on every source file, the version and copyright in the status
  bar, the About dialog.
- 1.1.0 (2026-09-09): a pair of different pixel dimensions is compared on the larger canvas,
  both anchored top-left, after the user agrees; nothing is decoded before the answer.
- 1.1.1 (2026-09-09): dark title bars through DWM on every window, and the application's own
  message dialog in place of `MessageBox`.

No earlier version was released; the ten commits are in `git log`.
