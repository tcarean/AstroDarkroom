# Code review of the whole project, 2026-11-13

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0051.

## How it was done

The code was written by an earlier model and had almost no review. On 2026-11-13 it measured
8,864 lines in 57 files, blank lines included (`src/ImageVisualCompare.Core/` 3,217,
`src/ImageVisualCompare.Wpf/` 5,647). That fits one context, but a long reading is done well at
the start and thinly at the end, so the code was split into six areas. Each area was read whole
by a reviewer with a clean context (a subagent on this session's model), which first read
`CLAUDE.md`, `docs/design/code-map.md`, the area's design notes, ADRs and requirements, so that
what is deliberate was not reported. A reviewer changed nothing and built nothing. Every finding
it returned was then checked by the session that wrote this list: the cited lines were opened
and the case followed through the callers. Last, the seams between areas were read (what
`ImagePairLoader` returns against how `MainWindow` and `CompareView` use it); they added no
entry of their own.

| # | Area | Reported | In this list as |
|---|------|---------:|-----------------|
| 1 | Zoom, pan, divider, layout records (Core) | 6 | CR-001, 002, 011, 015, 020, 026 |
| 2 | FITS and the display modes (Core) | 4 | CR-007, 008, 009, 027 |
| 3 | Loading the images (`Imaging/`) | 6 | CR-004, 005, 010, 017, 022, 025 |
| 4 | The comparison surface and the theme (`Controls/`, `Theme.xaml`, `DarkTitleBar`) | 9 | CR-001, 002, 006, 011, 012, 014, 015, 016, 018 |
| 5 | The main window and the dialogs | 7 | CR-003, 006, 013, 021, 022, 024 |
| 6 | Start-up, the log and the texts | 3 | CR-003, 019, 023 |
| - | The reviewers' cleanup lists | - | CR-028 and the cleanup section |

35 findings were reported and none was rejected at the check. The same defect seen from two
areas became one entry (seven times), which gives 27 entries; CR-028 gathers, from the cleanup
lists, the notes and comments that describe code that is gone. That no finding was rejected says
the reviewers were careful, not that every entry is proved: the check was a reading, not a run
of the application. What a reading cannot settle is marked.

Out of scope: `scripts/` and `.claude/` (the kit's), and the tests line by line (a reviewer
opened a test only to see whether a suspected defect was covered).

Some figures come from throw-away Python that mirrors a few lines of Core in IEEE doubles, the
arithmetic .NET uses: the reviewers of areas 1, 2, 4 and 5 wrote such scripts, and the session
recomputed the figures of CR-001, CR-002, CR-007 and CR-011 with its own. They are quoted where
used. Nothing in the repository was built or run.

Two areas came back clean in their riskiest parts, which is worth knowing: the FITS reader's
header walk, sizes, plane and row order, `BZERO`/`BSCALE`, every `BITPIX` and the histogram do
what R-014 to R-019 and R-110 to R-112 say; the log's start, fall-back folder, level, hooks and
lines do what `docs/design/logging.md` says, and every English and Romanian text of `UiText`
says the same thing but for CR-019.

## How to read an entry

- **Severity.** 1 = the comparison lies: a wrong pixel shown, the two images out of step, a
  wrong value read, or one file named while another is shown. 2 = a crash, a hang, a file left
  locked, or memory never given back. 3 = a wrong text or a wrong layout. 4 = robustness and
  cleanliness.
- **Reach.** Who meets it: *common* (an ordinary session), *some files* (another format, depth
  or layout than a 16-bit PNG or TIFF pair or a Siril FITS), *some setups* (display scaling, a
  narrow window, a hard disk, a start from a console), *slip* (a mistyped argument, a stray
  key), *rare*, *crafted* (a broken or hostile file).
- **Evidence.** *confirmed*: the lines were read and the case follows from them. *to measure*:
  the code was read, but the case rests on something only a run of the application or an outside
  source can show (how WPF samples, what WIC converts, what a dialog does to the process); the
  task starts by showing it. *owner*: the code does what is described, and whether that is a
  defect is the owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

An entry gives the direction of a fix, never the fix. A task takes one entry, starts from the
requirement and a failing test (the kit's rules "Requirements first" and "Test before fix"), and
runs the application when it touched the WPF layer (the rule "Run the application"). Line
numbers are those of 2026-11-13; a task finds its lines again by the names the entry gives.

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | common | confirmed | The read-out names the neighbouring pixel when the offset is an ulp under a whole number | fixed e4cb8e5d, 42796c8b |
| CR-002 | 1 | common | to measure | At 100 % the image sits between screen pixels and is drawn interpolated | fixed 4c770ae7, 4f419791, 0c1d11e5, 02e63f4d |
| CR-003 | 1 | some setups | to measure | A pair named by relative paths on the command line is read again by those relative paths | fixed 8ba6986e |
| CR-004 | 1 | some files | to measure | A float TIFF with associated alpha is un-premultiplied, not shown as stored | fixed 34e7e45e |
| CR-005 | 1 | some files | owner | A transparent pixel is drawn over black and read unblended; a float RGBA loses its alpha | fixed 115684cf, 1f6223dc |
| CR-006 | 1 | rare | to measure | The read-out is not refreshed when the pair or the view changes under a resting pointer | fixed f5cf5c51, 1f6223dc |
| CR-007 | 1 | rare | to measure | A float sample is quantised in double precision, Siril does it in single: 127 values differ by a level | fixed 5c65300a, 1f6223dc |
| CR-008 | 1 | crafted | confirmed | A FITS card with no space after `=` loses the first character of its value | fixed 1b261833 |
| CR-009 | 1 | crafted | confirmed | A `BZERO` or `BSCALE` that does not parse is read as 0 or 1 | fixed a21d59ab, a8f45631 |
| CR-010 | 2 | some setups | to measure | A three-plane FITS not in the file cache is read with a seek per plane per row | fixed 54f7807a, a8f45631; measured, severity 4 |
| CR-011 | 3 | common | confirmed | At fit, a rounding residue shows a scrollbar and the minimap's rectangle | fixed 71e02ddd, a8f45631 |
| CR-012 | 3 | common | owner | The bottom-left corner of the image is always under the minimap at 100 % and above | fixed fb42836a, a8f45631 |
| CR-013 | 3 | some setups | to measure | On a narrow window the status bar cuts B's read-out before the pair line | fixed 432bcb1a |
| CR-014 | 3 | slip | to measure | Up or Down on the surface moves the keyboard focus to the toolbar | fixed 7bce108b |
| CR-015 | 3 | slip | to measure | A right click on a scrollbar thumb opens WPF's own menu: light, English, a page of one pixel | fixed 0f23b15f |
| CR-016 | 3 | some setups | to measure | The theme has no focus visual: a black dotted line on dark buttons and over the image | fixed 54f50342 |
| CR-017 | 3 | rare | confirmed | The canvas is the probes' and is never checked against what was decoded | fixed 1c167267 |
| CR-018 | 3 | rare | confirmed | A long message gets the system's light scrollbar in the dark dialog | open |
| CR-019 | 3 | some files | owner | The Romanian status text of a padded pair does not say it was padded | open |
| CR-020 | 4 | common | confirmed | A divider drag jumps the line to the pointer on the first move | open |
| CR-021 | 4 | common | owner | The folder of image A is forgotten when B's dialog is cancelled | open |
| CR-022 | 4 | slip | to measure | An image decoded before the load failed is not released | open |
| CR-023 | 4 | slip | owner | An option typed almost right is taken for a path, in silence | open |
| CR-024 | 4 | rare | to measure | `About` is the one action not held back during a load | open |
| CR-025 | 4 | common | to measure | A FITS or float TIFF decode writes a `WriteableBitmap` on a pool thread | open |
| CR-026 | 4 | crafted | confirmed | A canvas so long that its minimap has no height: a click jumps to the top-left | open |
| CR-027 | 4 | crafted | to measure | `MaxWidth` lets through seven widths whose row is larger than .NET's largest array | open |
| CR-028 | 4 | - | confirmed | Notes, requirements and comments that describe code that is gone | open |
| CR-029 | 3 | some setups | confirmed | On a narrow window the toolbar cuts `Change B…` away | open |

9 entries of severity 1, 1 of severity 2, 9 of severity 3 and 9 of severity 4; 9 are
`confirmed`, 14 `to measure` and 5 `owner`. The cleanup list after the entries holds what is not
a defect: dead code, duplicates, small inaccuracies. CR-029 is not in these counts: it was added
on 2026-11-21 by the task that fixed CR-013 to CR-017, which saw it in a render.

## Severity 1: the comparison lies

### CR-001. The read-out names the neighbouring pixel when the offset is an ulp under a whole number

- **Severity 1, common, confirmed** (part b: to measure). Status: fixed e4cb8e5d and, after the branch's
  review, 42796c8b; part (b) was measured on screen and is as described (ADR 0052). `ContentPointInsideAt` is now
  `Viewport.PixelAt`, `UpdateCursorContentPoint` `CompareView.ReportPointer`, the event
  `CursorPixelChanged` and the field `_cursorContentPoint` `_cursorPixel`; `Same` is gone.
- **Where:** `src/ImageVisualCompare.Core/Viewport.cs:134-135` (`ScreenToContent`), `:182-194`
  (`ZoomTo`), `:246` (`ZoomToActualSize`);
  `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:596-597` (`UpdatePixelReadout`);
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:553-555`
  (`UpdateCursorContentPoint`), `:577-582` (`Same`).
- **What happens (a).** `ZoomTo` sets `OffsetX = anchor.X - anchorScreenX / _scale`, where
  `anchor` was `anchorScreenX / oldScale + OffsetX`. Nothing rounds either step, so an offset
  that should be whole can come out as `N - 2e-13`. The read-out then computes
  `screenY / _scale + OffsetY` and truncates it with `(int)`.
- **The case (a).** A 5938 × 3888 pair on a surface of 1920 × 976 physical pixels, `Fit`, then
  `100%`. Recomputed by the session from the source: `OffsetY = 1455.9999999999998`, where 1456
  is meant. Image row 1556 is drawn on screen row 100, but the pointer there gives
  1555.9999999999998, truncated to 1555: the status bar shows the coordinate and the A and B
  values of the row above. 593 of the 976 screen rows read one row up. At a width of 1920 this
  happens at 15 of the 201 heights from 900 to 1100. The `—` of a padded pair is off by the same
  pixel at the smaller image's edge.
- **What happens (b), to measure.** The pointer's position is the top-left corner of the
  physical pixel it is on, and no half pixel is added before the conversion. From 400 % up the
  images are drawn nearest-neighbour; if WPF picks the image pixel at the centre of each screen
  pixel, as is usual, the first screen row or column of a painted square reads the pixel before
  it whenever the square's edge falls in the first half of a screen pixel (by the area 4
  reviewer's script: 225 of 900 screen rows at 400 % with a fractional offset). The run: at
  400 %, put the pointer on the first screen row of a square whose upper neighbour has another
  value, and compare the read-out with the square.
- **Should:** the read-out gives the pixel that is drawn under the pointer.
- **Direction:** read the image point at the centre of the physical pixel under the pointer,
  which settles both parts; CR-002 keeps the offsets whole at a whole scale.
- **Test:** none covers it. `ViewportTests.R028_ContentPointInsideAt_ReturnsTheImagePixelUnderTheCursor`
  uses 1000 × 1000 in 400 × 400 and compares with six decimals, never truncating; the truncation
  in `MainWindow` has no test. A test: the case above, asserting row 1556 for screen row 100.

### CR-002. At 100 % the image sits between screen pixels and is drawn interpolated

- **Severity 1, common, to measure.** Status: fixed 4c770ae7 and, after the branch's three
  reviews, 4f419791, 0c1d11e5 and 02e63f4d; measured on screen as described,
  and worse: the surface of the main window always ended on a fraction of a pixel (ADR 0053).
  The pan keeps its fraction in `OffsetX`, `OffsetY`; the image is drawn with
  `Viewport.DrawnOffsetX`, `DrawnOffsetY`. Not measured on a scaled display.
- **Where:** `src/ImageVisualCompare.Core/Viewport.cs:123-124`, `:191-192`, `:279-293`,
  `:317-327` (every way of moving leaves the offsets as the arithmetic gives them);
  `src/ImageVisualCompare.Core/RenderPlan.cs:82-86` (the translation is `-OffsetX`, `-OffsetY`,
  and the mode is `Smooth` below 4.0);
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:671-680` (`Sync`);
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml:8-9`, `:33`, `:52`
  (`SnapsToDevicePixels="False"`, `UseLayoutRounding="False"`).
- **What happens.** At a scale of exactly 1 the offsets are whatever the centring or the anchor
  left, and below 400 % the bitmaps are drawn with `HighQuality` (R-025). A fractional offset at
  100 % then asks WPF to resample.
- **The case.** A 5938 × 3888 pair, `Fit`, then `100%`. On a surface of 1920 × 947 physical
  pixels `OffsetY = 1470.5` (recomputed by the session): every screen row lies half way between
  two image rows. On the maximised window of a 1920 × 1080 display the surface is about 946.37
  pixels high (the toolbar and the status bar have fractional heights), which gives 1470.815. A
  single bright pixel is then spread over two rows, and a difference of one level on one pixel
  becomes a fraction of a level, in a tool built to show exactly that; the read-out still gives
  the stored value. On a display scaled 150 % the surface's own top edge is at 64.5 physical
  pixels, so even a whole offset is half a pixel off. Both images are shifted alike, so they
  stay aligned.
- **To measure:** a pair with alternating rows of 0 and 255, at `100%` after `Fit` on a surface
  of odd height, captured in physical pixels: the rows must be 0 and 255, not greys. The same
  capture shows whether the surface's origin is on a whole physical pixel.
- **Should:** at 100 % each physical pixel shows exactly one stored pixel (R-022; the README
  calls this 100 % "the honest one" for pixel peeping).
- **Direction:** at a whole scale, keep `Offset × Scale` on whole physical pixels, and put the
  surface's origin on the device grid.
- **Test:** `RenderPlanTests.R022_OnAScaledDisplayOneHundredPercentStillMeansOneImagePixelPerScreenPixel`
  checks the scale only. A Core test can pin whole offsets at 100 %; the drawing itself needs
  the run.

### CR-003. A pair named by relative paths on the command line is read again by those relative paths

- **Severity 1, some setups, to measure** (the wrong folder of the dialogs is confirmed).
  Status: fixed 8ba6986e. `App.ApplyCommandLine` makes the two paths full once the files are
  known to exist, and the pair is held by them from then on (R-002). Not measured: whether the
  Windows open dialog moves the process's current folder; the owner was at the PC on 2026-11-14
  and no dialog was shown, and the fix does not depend on the answer. Run instead, with the real
  window off the desktop on the real FITS pair named by its two bare file names: the current
  folder moved elsewhere after the start, then `H`, `F` and `F` again; each switch read the
  same two files. Left to the owner, from the branch's review, read and not run: `Path.GetFullPath`
  spells out a folder written in its 8.3 short form, so on an account whose temporary folder is
  written so (`C:\Users\LONGNA~1\...`) the unchanged test
  `R121_TheStartLogsTheLanguageARefusedLangAndThePair`, which expects the paths as
  `Path.GetTempPath` writes them, would fail; it passes on this machine.
- **Where:** `src/ImageVisualCompare.Wpf/App.xaml.cs:112-116` (`ApplyCommandLine` keeps the
  paths as typed; nothing in `src/` calls `Path.GetFullPath`);
  `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:226-228` (a mode switch decodes
  `selection.BeforePath` and `AfterPath` again), `:313`, `:376-377` (`Path.GetDirectoryName` of
  the kept path), `:397-399`; `src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs:283-286`
  (the kept image decoded again from `kept.Info.Path`);
  `src/ImageVisualCompare.Wpf/Services/FileDialogService.cs:90-106` (no `RestoreDirectory`; an
  empty folder is skipped).
- **Confirmed part.** In a folder `X`, `ImageVisualCompare.Wpf.exe a.fit b.fit`.
  `Path.GetDirectoryName("a.fit")` is an empty string, which `Pick` skips. `Ctrl+O` therefore
  opens its first dialog wherever Windows chooses, not in A's folder (R-001), and `Change A…`
  and `Change B…` do not open in the folder of the file they replace (R-065).
- **Part to measure.** The same start, then `Change B…` and a file accepted in another folder
  `Z`. If the Windows open dialog moves the process's current folder to `Z`, as it does unless
  told not to, pressing `H` reads `a.fit` as `Z\a.fit`: `Could not read a.fit.` for a file that
  is still there, or, when `Z` holds a file of that name (the same names in sibling session
  folders are usual), another image shown as A under the unchanged name. The run: log
  `Environment.CurrentDirectory` before and after a dialog accepted in another folder.
- **Should:** the files named at start-up, whatever the dialogs did since.
- **Direction:** make both paths absolute once, where the selection is built, before
  `File.Exists` and the log line.
- **Test:** none; `StartupArgumentsTests`, `LoggingTests` and `MainWindowTests` use absolute
  paths only. A test: `ApplyCommandLine` with two relative names of existing files, asserting
  rooted paths in the selection.

### CR-004. A float TIFF with associated alpha is un-premultiplied, not shown as stored

- **Severity 1, some files, to measure.** Status: fixed 34e7e45e; measured on 2026-11-14 on a
  generated file and as described: WIC names it `Prgba128Float`, and its conversion to
  `Rgba128Float` gave 0.4 for a stored 0.2 at alpha 0.5 (level 102, not 51), 0 at alpha 0, and
  1.2 for a stored 0.3 at alpha 0.25. `LoadFloat` asks for such a frame in its own format, and
  the four floats arrive as stored (R-091). The name `Decode` in `docs/design/imaging.md`, the
  first line of CR-028, went with it.
- **Where:** `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs:454` (`FloatSamplesOf`
  accepts `Prgba128Float`), `:174-179` (`LoadFloat` asks WIC for `Rgba128Float`).
- **What happens.** The comment says that between float formats WIC copies the values. From the
  premultiplied format to the straight one it should not: that conversion divides red, green
  and blue by alpha.
- **The case.** A 32-bit float RGBA TIFF with ExtraSamples = 1, as an editor writes with
  transparency: a pixel stored as (0.2, 0.2, 0.2, alpha 0.5) should show level 51, as a FITS of
  the same samples does (R-091); it would show 102, and where alpha is 0, nothing meaningful.
- **To measure:** the fixture of `R090_AFloatTiff_IsReportedAsFloat` written with tag 338 = 1,
  RGB 0.2 and alpha 0.5; the bytes must be 51. Whether WIC's TIFF decoder reports such a file as
  `Prgba128Float` at all is part of the measurement.
- **Direction:** for a premultiplied frame ask for the premultiplied format itself, the same
  four floats, so the stored values arrive untouched.
- **Test:** none; `R090_…` writes unassociated alpha and asserts the text only, `R091_…` has
  three channels.

### CR-005. A transparent pixel is drawn over black and read unblended; a float RGBA loses its alpha

- **Severity 1, some files, owner.** Status: fixed 115684cf, by the owner's choice of 2026-11-14:
  no transparency, every image is loaded opaque with the colours WIC decodes for it, as the
  float path already was (`WicImageLoader.Opaque`, R-010, ADR 0054). Measured as described: a
  PNG pixel holding 200 at alpha 128 was drawn at 100 and read as 200. On the three real files
  the new conversion costs the same time and memory, and the three rows sampled of each are the
  same bytes. After the branch's review, 1f6223dc: a test of a 16-bit RGBA PNG of random samples
  (each colour is its high byte whatever the alpha), and the README says the colour Windows
  decodes, since an 8 or 16-bit TIFF with associated alpha reaches the program already divided
  by it (ADR 0054).
- **Where:** `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs:113` (the conversion to
  Bgra32 keeps the file's alpha); `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:623-633`
  (`Describe` prints red, green and blue and never looks at alpha);
  `WicImageLoader.cs:174-178` with `FloatSamples.ToSixteenBit` (a float RGBA's fourth sample is
  not read and the pixel is opaque, R-091).
- **What happens.** An integer RGBA image is drawn blended over the black backdrop, which is
  deliberate (ADR 0013). The read-out reports the stored, unblended values with no sign of the
  alpha. A float RGBA of the same picture is drawn opaque.
- **The case.** An 8-bit RGBA PNG pixel (200, 200, 200, alpha 128) is drawn at about 100 and
  read as `200 200 200`. Beside an RGB export of the same picture the two halves look different
  while the two read-outs are equal.
- **Owner's call:** neither R-051 nor ADR 0013 says what the read-out shows on a transparent
  pixel, nor that the two paths may treat alpha differently. One rule for both: alpha forced to
  255 at the load, as the float path does, or the alpha (or the value over black) in the
  read-out.
- **Test:** none; `bgra32.png` in `R011_ReportsWhateverFormatTheFileTurnsOutToHave` checks the
  depth and the channels.

### CR-006. The read-out is not refreshed when the pair or the view changes under a resting pointer

- **Severity 1, rare, to measure.** Status: fixed f5cf5c51. `CompareView` keeps the pointer's
  last position (`_pointer`); `Sync` reports the pixel now under it (`PixelUnderPointer`), and
  `SetPair` and `ReplacePair` report it even when it is the same pixel (R-051). Shown by tests
  that report one position and nothing after it: after a switch to `Histogram` the status bar
  still read `A  35  35  35`, the linear image's value. Run with the real window off the desktop
  on the real FITS pair, one position reported: the three items followed `H`, four zoom steps,
  `F`, `F` again, `100%` and `Fit`. Not measured: the two cases with a real mouse on screen
  (the owner was at the PC), so whether WPF's own mouse move hid the first of them is not known.
  After the branch's review, 1f6223dc: the position is kept in `OnPreviewMouseMove`, since a
  scrollbar's thumb marks the moves of its own drag handled and `OnMouseMove` never saw them
  (by reading WPF's behaviour, not run). Also by reading, not measured: during a load the
  overlay covers the surface, WPF takes the pointer off it and `OnMouseLeave` forgets the
  position, so in the window a pointer at rest gets its read-out from the move WPF sends when
  the overlay goes, as before the fix, and is empty until then, never stale; the report of
  `SetPair` and `ReplacePair` serves a pointer the surface still holds, as with a button held.
- **Where:** `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:95`, `:504-572` (`ShowPair` writes
  the title, the names and the pair line, never the three read-out items), `:586-602`;
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:143-207` (`SetPair` and
  `ReplacePair` neither reset `_cursorContentPoint` nor raise the event), `:347-360`
  (`OnMouseMove` returns before the read-out during a pan or a divider drag), `:565-583`
  (`Same` drops an unchanged whole pixel), `:667-686` (`Sync` does not recompute it).
- **What happens.** The read-out is computed only in `OnMouseMove`. A change of the bitmaps
  (`H`, `F`, a changed image) or of the view (`+`, `Ctrl+1`) under a pointer that does not move
  relies on WPF sending a mouse move of its own after the layout changes; nothing in the code
  asks for it.
- **The cases to run.** Pointer at rest on a pixel, press `H`, then `+`: the three status items
  must follow without the mouse moving. Then: hold the left button on the image (a pan, the
  mouse captured), press `H`, release without moving: the status bar may keep
  `A  35  35  36   B  35  35  35` from the linear bitmaps under a histogram image, until the
  pointer crosses into another image pixel, which at 1600 % is 16 screen pixels away.
- **Direction:** have `Sync`, `SetPair` and `ReplacePair` recompute the point from the pointer's
  position and raise the event, and `ShowPair` write the read-out for it.
- **Test:** none reads the read-out.

### CR-007. A float sample is quantised in double precision, Siril does it in single: 127 values differ by a level

- **Severity 1, rare, to measure**, then the owner's call. Status: fixed 5c65300a, by the
  owner's choice of 2026-11-14: as Siril (`FitsScale.SixteenBitOfFloat`, R-015, ADR 0055). Read
  in the source of Siril 1.4: its 16-bit PNG and TIFF exports use the same single-precision
  expression as its display. Measured: over every float below 1 the 16-bit value differs for
  32,768 (16,384 of them in [0.5, 1), as below) and the level for 127; on five real float FITS
  of 69,260,832 samples each, 18 to 2,564 16-bit values differ and no level. Not measured: a
  stretched FITS against a 16-bit PNG saved from Siril. After the branch's review, 1f6223dc:
  R-015 no longer gives `FitsScale.ToByte`, the double-precision path, as the rule of a float
  sample, and the summaries of `ToByte`, `Level`, `FitsRowDecoder.ReadPlaneRow` and
  `FloatSamples.ToBgra32` say which path is whose.
- **Where:** `src/ImageVisualCompare.Core/FitsScale.cs:133-146` (`SixteenBit`);
  `src/ImageVisualCompare.Core/FitsRowDecoder.cs:155-157`, `:165-167`;
  `src/ImageVisualCompare.Core/FloatSamples.cs` (`ToSixteenBit`).
- **What happens.** The value times 65535 and the `+ 0.5` are computed in `double`. The comment
  on `SixteenBit` says Siril quantises a float the same way; Siril's display path does the
  product in `float` (`roundf_to_WORD(f * USHRT_MAX_SINGLE)`, quoted in FlatCheck's
  `docs/design/siril-histeq-source.md`). In the top binade a single-precision product just under
  `k + 0.5` becomes exactly `k + 0.5` and rounds up.
- **The case.** The float `0.8359426259994507` (bits `0x3F560056`): here 54783 and level 213; in
  single precision 54784 and level 214. `0.9453192949295044` (`0x3F720072`): 241 here, 242 in
  single. Both recomputed by the session. By the area 2 reviewer's script over every float of
  each binade: in [0.5, 1) the 16-bit value differs for 16,384 of 8,388,608 floats and the level
  for 127; below 0.5 the level never differs.
- **To measure:** a stretched float FITS saved from Siril as a 16-bit PNG, the pair loaded, the
  pixels that differ counted (a few tens expected on a 23-megapixel image, all at level 128 and
  above, the FITS side one lower). Whether Siril's 16-bit export uses the same single-precision
  expression as its display was not read in its source.
- **Owner's call after it:** R-015 says `round(value × 65535)` and not in which precision.
- **Test:** `FitsScaleTests.R015_ToByte_GivesEverySixteenBitValueItsHighByte_OnEachScaleAFileCanHave`
  covers the 65,536 floats `k / 65535f`, which agree in both precisions.

### CR-008. A FITS card with no space after `=` loses the first character of its value

- **Severity 1, crafted, confirmed.** Status: fixed 1b261833, by the owner's choice of 2026-11-20:
  the value is read from right after the `=`, with or without the space (R-014, ADR 0057). Read
  that day in the source of cfitsio 4.7.0, through which Siril reads a FITS: `ffpsvc` takes the
  value from right after the `=` when columns 9 and 10 are not `= `, so Siril reads
  `NAXIS1  =5938` as 5938 and opens the file; so does this program now. `ReadCards` hands `Value`
  the card from its tenth character on. Left as it was: a keyword shorter than eight characters
  followed at once by `=` (`NAXIS1=5938`), which cfitsio also reads.
- **Where:** `src/ImageVisualCompare.Core/FitsHeader.cs:363-366` (`ReadCards`).
- **What happens.** A card is a value card when its ninth character is `=`, and the value is
  taken from the eleventh on; the tenth is skipped without being looked at.
- **The case.** `NAXIS1  =5938` gives a width of 938; `BITPIX  =-32` gives 32, so floats are
  decoded as integers. The file loads and shows garbage with no message.
- **Should:** `FITS: the header is not valid.` (R-019), or the value read from the tenth
  character.
- **Direction:** take the value only when the tenth character is a space, or parse from it.
- **Test:** none; `FitsFixture.Card` always writes `= `.

### CR-009. A `BZERO` or `BSCALE` that does not parse is read as 0 or 1

- **Severity 1, crafted, confirmed.** Status: fixed a21d59ab, by the owner's choice of 2026-11-20:
  `FITS: the header is not valid.`, and the default only when the header has no such keyword
  (R-019, ADR 0058). Read that day in the source of cfitsio 4.7.0: `ffc2dd` does not convert
  `3.2768d4` or `32768,0`, and `ffgphd` then keeps 1.0 or 0.0 and reads on, so Siril opens such a
  file with the default, as this program did; the owner chose the refusal over showing the same
  wrong image in silence. An empty value is refused too. The `//` comment above the `///` block
  of `Number`, of the cleanup list, went with it. After the branch's review, a8f45631: the
  comments, ADR 0058 and the note said a lower-case exponent letter is refused, where only a
  lower-case `d` is; `3.2768e4` is a number for cfitsio and for this reader, and a test pins it.
- **Where:** `src/ImageVisualCompare.Core/FitsHeader.cs:446-457` (`Number`).
- **What happens.** `Count` refuses a value that is not an integer and `Number` refuses NaN and
  the infinities, but text that does not parse is treated as a missing keyword. Only an
  upper-case `D` is replaced by `E`. The `<param>` of `fallback` says so ("missing or does not
  parse"), against the summary of `Count`, which says a value is never read as its default.
- **The case.** `BITPIX` 16 with `BZERO   =             3.2768d4` (a lower-case exponent), or
  `32768,0`: `BZERO` becomes 0, the samples are read as signed, the dark half of the image is
  black and the rest 32,768 too low.
- **Should:** `FITS: the header is not valid.`; R-019's list names only the case that is not
  finite, so the requirement is worded with the fix.
- **Direction:** the default only when the keyword is absent.
- **Test:** `R019_ABZeroOrBScaleThatIsNotFinite_IsAnInvalidHeader` covers NaN, `1D999` and
  `-Infinity`; nothing covers text that does not parse.

## Severity 2: a crash, a hang, memory not given back

### CR-010. A three-plane FITS not in the file cache is read with a seek per plane per row

- **Severity 2 if the run shows a stall that reads as a hang, otherwise 4; some setups; to
  measure.** Status: fixed 54f7807a; measured on 2026-11-20, and it is severity 4: no stall. On
  `result_3813s.fit` (277 MB, three float planes) on the hard disk, dropped from the Windows
  cache before each run by a handle opened without buffering (a read of the whole file then
  took 1.4 to 1.5 s, against 0.2 s from the cache; no restart was made): `Load` took 3.95 and
  4.87 s in `Linear`, where 1.67 s from the cache, and 4.33 s in `Histogram` (2.10 s). The reads
  alone took 3.34 s a row at a time, 2.31 s in bands of 1 MiB a plane, 1.68 s at 4 MiB, 1.64 s
  at 8 MiB. So the seeks cost about two seconds a file, never a minute: the drive's read-ahead
  hid most of them, as the entry allowed. `FitsRowDecoder` now reads each plane in bands of
  4 MiB (`DefaultBandBytes`, `FillBand`) and `OpenRead` no longer asks for `RandomAccess`: 2.33
  and 2.38 s cold in `Linear`, 3.04 s in `Histogram`, and from the cache as before; the six
  bitmaps of the two real FITS in the three modes are the same bytes as before (SHA-256), and
  the six validation cases pass. A test counts the reads: 3 for a 200-row image of three planes,
  where there were 600. Not measured: the first open after a real restart, a fragmented file, a
  USB disk, a network share. The figures are in `docs/design/imaging.md`. After the branch's
  review, a8f45631: the note says the three bands are held during each pass (`Pass` makes a
  decoder a pass), and the `stream` parameter of `Pass` no longer speaks of a row a read.
- **Where:** `src/ImageVisualCompare.Wpf/Imaging/FitsImageLoader.cs:305-318` (`Pass`),
  `:330-331` (`OpenRead`: no buffer, `FileOptions.RandomAccess`);
  `src/ImageVisualCompare.Core/FitsRowDecoder.cs:59-65`, `:98-107`.
- **What happens.** Each row is read from three places one plane apart, with no buffer, and
  `RandomAccess` tells Windows not to read ahead. The comment at `OpenRead` explains the missing
  buffer; nothing was measured with the file out of the cache (the timings of ADR 0029 and
  ADR 0040 are with it in).
- **The case.** A 5938 × 3888 FITS of three float planes, 277 MB, opened first after a restart
  from a hard disk: 11,664 reads of 23,752 bytes alternating between three offsets 92 MB apart.
  At a few milliseconds a seek that is a minute or more for one file, behind an overlay whose
  bar stands at 0 and then at 0.5, where a read from start to end takes seconds. `Histogram`
  and `False colours` pass over the file twice.
- **To measure:** after a restart, open a FITS pair from the hard disk and read the `decode A`
  lap of the log's `load done` line; the drive's own read-ahead may hide it.
- **Direction:** read each plane a band of rows at a time, as `WicImageLoader.Pass` does, and
  drop `RandomAccess`.
- **Test:** none can show a cold disk; the measurement is the evidence.

## Severity 3: a wrong text or a wrong layout

### CR-011. At fit, a rounding residue shows a scrollbar and the minimap's rectangle

- **Severity 3, common, confirmed.** Status: fixed 71e02ddd. `Viewport.Overflow` takes an
  overflow of up to `WholeTolerance` (1e-9) of a physical pixel as none, so `MaxOffsetX` and
  `MaxOffsetY` are 0 at such a fit, and `ClampOffsets` puts the axis at 0, not at half the
  residue (R-043, R-041). Recomputed on 2026-11-20: 85 heights over and 85 under, as below; over
  1.9 million random sizes of image and view the largest residue is 1.3e-12 of a physical pixel.
  Run with the real window off the desktop on the real FITS pair: `Fit` on 401 window heights,
  68 of them with a residue, showed no bar and no rectangle. The last sentence of the case, the
  top screen row reading no pixel, was already gone since CR-001: the read-out takes the centre
  of the screen pixel; the test pins it. After the branch's two reviews, a8f45631 and the commit
  of the second review: the note and a test's comment said an overflow "however small" still scrolls,
  where it is one above the slack that does.
- **Where:** `src/ImageVisualCompare.Core/Viewport.cs:57-58`, `:67-78`, `:234-243`, `:317-327`;
  `src/ImageVisualCompare.Core/ScrollBarState.cs:52-56`, `:71-75`;
  `src/ImageVisualCompare.Core/MinimapGeometry.cs:80-82`; shown by
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:705-712` and
  `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs:105`.
- **What happens.** The fit scale is `ViewportHeight / ContentHeight` and the visible extent
  `ViewportHeight / _scale`; `V / (V / C)` is not always `C` in doubles, and the tests are
  exact: `CanScrollVertically => MaxOffsetY > 0`, `if (visible >= content)`.
- **The case.** A 5938 × 3888 canvas on a surface of 1920 × 973 physical pixels, a new pair or
  `Fit`. Recomputed by the session: the visible height is 3887.9999999999995, `MaxOffsetY` is
  4.5e-13, `CanScrollVertically` is true. The vertical bar is made visible with a thumb as long
  as its track, and the minimap's rectangle is shown over the whole thumbnail. 85 of the 900
  whole heights from 600 to 1499 do this. With the residue of the other sign (another 85 of
  those heights) the offset is about -2e-13 and the top screen row at fit reads no pixel.
- **Should:** no bar and no rectangle at fit (R-043, R-041).
- **Direction:** treat an overflow of less than a small fraction of a physical pixel as fitting,
  in `Viewport`.
- **Test:** `ScrollBarStateTests.R043_AnImageThatFitsNeedsNoBars` and
  `MinimapGeometryTests.R041_AtFitTheRectangleCoversTheThumbnailAndIsHidden` use sizes that
  divide exactly. A test: the case above.

### CR-012. The bottom-left corner of the image is always under the minimap at 100 % and above

- **Severity 3, common, owner.** Status: fixed fb42836a, by the owner's choice of 2026-11-20, of
  three (a key; a pan that overshoots the edge; leaving it): the key `M` hides the minimap and
  the zoom badge and shows them again, wherever the focus is; a held key acts once, the choice
  holds through a mode switch and a new pair, and the status bar's hint names the key (R-067,
  ADR 0059). `CompareView.ToggleOverlays` and `MainWindow.OnOverlaysKey`. `MinimapView.Hide`,
  which also drops the thumbnail, is not what a toggle needs and stays on the cleanup list. Run
  with the real window off the desktop on the real FITS pair, at 100 % panned fully left and
  down: `M` took both off (the corner shows in the render), a repeat of the held key changed
  nothing, they stayed hidden through `H`, and `M` brought them back. Not done: the application
  on screen with real keys (the owner was at the PC), and the wiki manual, where the key's row
  waits for the owner's word. After the branch's review, a8f45631: the test of the held key read
  the state only after two repeats, which cancel out; it reads it after each. Left to the owner,
  from the same review: the hint is cut from its end when the status bar is short of room
  (R-054), and `M minimap` is its last item but one. In the render of a window 1920 wide it is
  whole in `Linear` and ends `M minimap · F1 h…` in `Histogram`; a padded pair or a narrower
  window cuts the key's name too (CR-013 is about that bar).
- **Where:** `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml:95-98` (the minimap, bottom
  left, above everything), `:56-59` (the zoom badge, top right);
  `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs:79-85` (`Hide` exists and is never
  called); `src/ImageVisualCompare.Core/Viewport.cs:326` (the offset stops at the image's edge).
- **What happens.** Whenever the image overflows the surface on both axes, its bottom-left
  corner can only be brought to the surface's bottom-left corner, where the thumbnail, 190 DIPs
  wide and up to 150 high, lies over it. No key, button or pan brings it out. The top-right
  corner has the same on a smaller scale under the zoom badge.
- **The case.** Any pair at 100 % or more, panned fully left and down: a corner star there
  cannot be compared.
- **Owner's call:** ADR 0019 accepts the 9-DIP strips of the scrollbars and says nothing of the
  minimap. A key or a button that hides the overlays, or a pan that may overshoot by their
  size.
- **Test:** none.

### CR-013. On a narrow window the status bar cuts B's read-out before the pair line

- **Severity 3, some setups, to measure.** Status: fixed 432bcb1a, by the owner's choice of
  2026-11-21, of two (shorten the pair line; put the values first): the order stays, and on a
  narrow window the hint goes first, then the memory, then the pair line is trimmed with an
  ellipsis; the coordinates and the two values stay whole (R-054, ADR 0060). Measured that day
  on the real window, never shown, laid out at a client width, the read-outs at their longest,
  and it was wider than the estimate: with a padded pair B's value had no pixel on screen at a
  client of 1008 (a window 1024 wide) and of 944 (960), where A's had 54 of its 128; a pair of
  one size lost B at 784 (800). After the fix the three are whole at all three widths, and the
  padded pair line has 242, 183 and 20 of its 419. It is layout only: the pair line and the
  read-outs are in a `DockPanel` of their own in `MainWindow.xaml`, the read-outs docked right
  and written first. Run with the real window off the desktop, 960 wide, on the real padded
  pair: the bar read `5938 × 3888 padded  ·  A 5938 × 38…`, then `1838, 1321`,
  `A  57  47  45`, `B  60  45  41` and the version, all whole, in the render. The cost: the
  read-outs keep their fixed boxes, so a pair of one size at a client of 944 has its pair line
  trimmed by 32 where before only blank of B's box was cut. Not done: the application on
  screen (the owner was at the PC); the wiki manual's line on the bar, which waits for the
  owner's word. Still the owner's, from CR-012: the hint has no room at all from a client of
  1264 down with a padded pair, `M minimap` with it.
- **Where:** `src/ImageVisualCompare.Wpf/MainWindow.xaml:82-105`;
  `src/ImageVisualCompare.Wpf/Theme.xaml:36-48`.
- **What happens.** The `DockPanel` gives the legal text, docked right, its full width first.
  The left `StackPanel` holds, in order, the pair line, the cursor (at least 96 wide), A's value
  (128), B's value (128) and the memory. A horizontal `StackPanel` does not shrink its children,
  so what does not fit is cut from the right: the memory, then B, then A.
- **The case.** A window about 960 DIPs wide (half of a 1920 screen, or a whole one at 200 %)
  with a padded pair, whose pair line is long: by the area 5 reviewer's estimate of the widths,
  B's value is not shown at all and A's last digit is cut. The run: snap the window to half the
  screen with a padded pair and look at `B`.
- **Should:** the two values the tool exists for are the last to go.
- **Direction:** lay the read-outs out before the pair line, or let the pair line trim with an
  ellipsis as the hint does. R-054 decides only that the hint is trimmed and the legal text
  never is.
- **Test:** none.

### CR-014. Up or Down on the surface moves the keyboard focus to the toolbar

- **Severity 3, slip, to measure.** Status: fixed 7bce108b; measured on 2026-11-21 and as
  described. A key-down of Up or Down on the surface came back unhandled, and on the real
  window with a handle, off the desktop and never activated, `PredictFocus`, the search an
  arrow key's navigation runs, answered `Change A…` for both from the surface (from `+`: `−`
  for Left, `Fit` for Right). `CompareView.OnKeyDown` now takes the two keys and does nothing
  with them, Tab is still WPF's, and the surface takes the focus in
  `OnPreviewMouseLeftButtonDown`, before the minimap or a scrollbar's thumb handles the press
  (R-063). Not done: the keys pressed in a live window, which would have taken the keyboard
  from the owner; the tests raise them, and a press on the image, the minimap and each thumb,
  on a window never shown.
- **Where:** `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:400-442` (`OnKeyDown`
  leaves Up and Down unhandled); `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs:110-118`
  (the press is handled, so the surface's `Focus()` does not run).
- **The case to run.** A pair shown, the surface focused, `↑`, then `←` and `+`. WPF's arrow-key
  navigation would move the focus to a toolbar button; `←` and `→` then walk the buttons, `+`,
  `−`, `Ctrl+0` and `Ctrl+1` do nothing, Space clicks whatever holds the focus, and a drag in
  the minimap does not give the focus back. R-063 says the surface's keys work while it has the
  focus; `↑` sits beside the documented `←` and `→`.
- **Direction:** mark Up and Down handled on the surface, and focus the surface from a press in
  the minimap or on a thumb.
- **Test:** `MainWindowTests.R063_…` covers clicks only.

### CR-015. A right click on a scrollbar thumb opens WPF's own menu: light, English, a page of one pixel

- **Severity 3, slip, to measure.** Status: fixed 0f23b15f, by the owner's choice of 2026-11-21,
  of two (no menu; a dark menu worded by `UiText`): a right click on a bar opens nothing
  (R-043, ADR 0061). Measured that day and as described: with the menu's opening raised on it,
  each bar held WPF's menu of seven items, `Scroll Here`, `Top`, `Bottom`, `Page Up`,
  `Page Down`, `Scroll Up`, `Scroll Down` on the vertical one, its background `#F5F5F5`, and
  the bars had a `SmallChange` of 0.1 and a `LargeChange` of 1 on a range of 0 to 2959 image
  pixels. The two thin styles set `ContextMenu` to null. The bars' steps are left as they are:
  nothing reaches them now. The summary of `OnScrollBarMoved`, a line of CR-028, went with it.
  Not done: a real right click on screen.
- **Where:** `src/ImageVisualCompare.Wpf/Theme.xaml:187-217` (the two thin styles set no
  `ContextMenu`); `src/ImageVisualCompare.Core/ScrollBarState.cs:29-33` and
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:705-712` (`Apply` sets the range, the
  thumb and the value, so the bars keep WPF's `LargeChange` of 1 and `SmallChange` of 0.1, in
  image pixels); `CompareView.xaml.cs:515-518` (the summary of `OnScrollBarMoved` names "its
  context menu").
- **The case to run.** Zoomed in, a right click on a thumb: WPF's stock menu (`Scroll Here`,
  `Top`, `Page Up`, …) in the system's light style over the dark image, in English under
  `--lang=ro` too; `Page Down` moves one image pixel and `Scroll Down` a tenth of one.
- **Should:** no menu, or a dark one worded by `UiText` (ADR 0018, R-101).
- **Direction:** no context menu on the two thin styles; if a menu is kept, the steps come from
  `ScrollBarState`.
- **Test:** none.

### CR-016. The theme has no focus visual: a black dotted line on dark buttons and over the image

- **Severity 3, some setups (keyboard use), to measure.** Status: fixed 54f50342: the button
  that has the keyboard focus has the accent border, a dialog's default button has it only
  while Enter answers with it (`IsDefaulted`, not `IsDefault`), a link of About with the focus
  is underlined, and WPF's dotted rectangle is drawn nowhere, neither on a button nor over the
  surface (R-068, ADR 0062). Read on 2026-11-21 from the elements' properties, as described:
  every button had WPF's focus visual, a rectangle stroked `#000000`, 1 thick, dashes `1 2`,
  the surface WPF's default one, and no template had a trigger on a focus property. Not seen:
  the rectangle itself, and the two cases of the entry run in a live dialog, since a focus
  visual is drawn only in the active window after a key; the tests set WPF's read-only
  `IsKeyboardFocused` and `IsDefaulted` through their keys and read the border back. That a
  dialog opens with the focus on the window, so its default button is lit, is from WPF's
  source. One change beside the fix: a dialog that is not the active window lights no button.
- **Where:** `src/ImageVisualCompare.Wpf/Theme.xaml:58-94`, `:99-138`, `:163-173` (no
  `FocusVisualStyle` and no keyboard-focus trigger anywhere in `src/`; `DialogButton` lights
  `IsDefault`, which does not move with the focus);
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml:1-9` (focusable, with WPF's default
  focus visual).
- **The cases to run.** In the padding question, Tab from `Yes` to `No`: the accent border stays
  on `Yes`, the focus on `No` is WPF's black dotted rectangle on a nearly black button, and
  Space answers No. With the surface focused, Alt or a key used after a mouse click: the dotted
  rectangle is drawn inside the comparison surface, over the image's edge.
- **Should:** a visible focus on the buttons, none over the image.
- **Direction:** no focus visual on the surface; an accent focus in the button templates.
- **Test:** none.

### CR-017. The canvas is the probes' and is never checked against what was decoded

- **Severity 3, rare, confirmed.** Status: fixed 1c167267, by the owner's choice of 2026-11-21,
  of two (refuse with a message; settle the canvas again and ask again): after each decode
  `ImagePairLoader.EnsureSize` compares the decoded size with the one the canvas was settled
  from, the probed header or, for the kept image decoded again, the image on screen, and on a
  difference the load fails as a file that cannot be read does, with a second line of its own:
  `The file has changed on disk: it was 5938 × 3888, it is now 4000 × 3000. Open the pair
  again.` (R-005, R-009, ADR 0063). After an open the dialogs ask again; after a change or a
  mode switch the pair on screen stays. Run with the real codec: a 64 × 48 PNG saved again as
  50 × 40 while the padding question was open gave `Could not read a.png.` and that line, and
  one `ERROR` line in the log. Only the size is compared. The image decoded before the refusal
  is dropped as after any failed load (CR-022).
- **Where:** `src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs:163`, `:190` (`LoadAsync`);
  `:263-265`, `:281-292` (`ReplaceAsync`); the effect in
  `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:159-167`.
- **What happens.** The `PadPlan` is computed from the probed headers and goes into the pair
  with the images decoded later. In `ReplaceAsync` the plan uses the kept image's `Info` from
  when it was opened, and the kept file is then read from disk again when the mode falls back.
- **The case.** A FITS pair in `Histogram`; file A is saved again from Siril under the same
  name, cropped; the user changes B to a PNG. The mode falls to `Linear`, A is decoded again at
  its new size, and the pair has a canvas of the old size and no padding question (ADR 0014):
  an image layer of another size than the backdrop, a status bar naming one size, and a larger
  image running past the backdrop where the pan cannot reach. `LoadAsync` has the same gap for
  as long as the padding question stays open.
- **Direction:** compare each decoded size with the probed one; on a difference, plan and ask
  again, or fail with a message.
- **Test:** `R009_AModeTheNewPairCannotShow_DecodesTheKeptFileAgainInLinear` uses a fake loader
  with fixed sizes. A test: the same fake returning another size on the second decode.

### CR-018. A long message gets the system's light scrollbar in the dark dialog

- **Severity 3, rare, confirmed.** Status: open.
- **Where:** `src/ImageVisualCompare.Wpf/MessageDialog.xaml:19` (a `ScrollViewer` with
  `MaxHeight="440"`); `src/ImageVisualCompare.Wpf/Theme.xaml:175-217` (the only `ScrollBar`
  styles are the two keyed thin ones).
- **The case.** A message longer than 22 lines of 520 DIPs (a codec's long error text): the
  `ScrollViewer` shows WPF's stock light scrollbar in the dark dialog, against ADR 0018.
- **Direction:** an implicit dark `ScrollBar` style, or the thin one on that `ScrollViewer`.
- **Test:** none.

### CR-019. The Romanian status text of a padded pair does not say it was padded

- **Severity 3, some files, owner.** Status: open.
- **Where:** `src/ImageVisualCompare.Core/UiText.cs:230` (`PaddedCanvas`); the caller
  `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:553-554`.
- **What happens.** English: `5938 × 3888 padded`. Romanian: `pânză 5938 × 3888`, which names
  the size as the canvas and does not say that anything was padded (R-050: "when the pair was
  padded").
- **Owner's call:** `UiTextTests` pins the Romanian wording, so it was chosen; whether it says
  enough is the owner's.
- **Test:** the one that pins it.

## Severity 4: robustness and cleanliness

### CR-020. A divider drag jumps the line to the pointer on the first move

- **Severity 4, common, confirmed.** Status: open.
- **Where:** `src/ImageVisualCompare.Core/DividerModel.cs:71-74` (`MoveTo`), `:131-142`
  (`HitTest`); `src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs:321-325`, `:347-351`.
- **The case.** The line at x = 1000 on a surface 2000 DIPs wide. A press at (1012, 500), on the
  handle, and a move of 1 DIP to the right: the line goes to 1013, a jump of 13 DIPs, where
  1001 is meant. The press is accepted up to 8 DIPs from the line and 13 on the handle, and the
  distance is not kept.
- **Direction:** keep the distance between the press and the line at the grab, and take it off
  in the move; the arithmetic belongs in `DividerModel` (ADR 0009).
- **Test:** `DividerModelTests.R030_MoveTo_ConvertsScreenPositionToAFraction` pins `MoveTo`;
  nothing tests a drag.

### CR-021. The folder of image A is forgotten when B's dialog is cancelled

- **Severity 4, common, owner.** Status: open.
- **Where:** `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:295-313`;
  `src/ImageVisualCompare.Wpf/Services/FileDialogService.cs:61-73`.
- **The case.** A pair open from folder `X`. `Ctrl+O`, image A picked in folder `Y`, B's dialog
  cancelled. `Ctrl+O` again opens its first dialog in `X`. R-001 says a later first dialog opens
  "in the folder of the image A chosen last, whether or not that pair loaded", which reads as
  `Y`.
- **Owner's call:** word R-001 for the cancelled B, or have the dialogs report A's folder.
- **Test:** none.

### CR-022. An image decoded before the load failed is not released

- **Severity 4, slip, to measure.** Status: open.
- **Where:** `src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs:179-185`, `:275-286` (the
  first image decoded, the second throws); `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:237-242`,
  `:329-334`, `:405-410` (the three failure branches), `:527-544`
  (`MemoryHousekeeping.ReleaseImageBuffers`, called only in `ShowPair` and only when a pair is
  replaced).
- **The case to run.** At start-up a good 21-megapixel file as A and one whose header reads and
  whose pixels do not as B: A is decoded, about 88 MiB outside the managed heap, B fails, the
  message is shown, the dialogs ask again. The orphaned A waits for a collection the tiny
  managed heap does not ask for; the next good pair is shown with no pair to replace, so no
  release runs then either. ADR 0016 rejects leaving it to the collector. The run: the status
  bar's process figure after such a failed open followed by a good one, against a good one
  alone.
- **Direction:** run the release after a load that failed once something had been decoded.
- **Test:** none.

### CR-023. An option typed almost right is taken for a path, in silence

- **Severity 4, slip, owner.** Status: open.
- **Where:** `src/ImageVisualCompare.Core/StartupArguments.cs:55-67`;
  `src/ImageVisualCompare.Wpf/App.xaml.cs:113-124`.
- **The case.** `ImageVisualCompare.Wpf.exe -lang=ro A.png B.png` (or `--language=ro`,
  `/lang:ro`, a typographic dash pasted from a document), both files existing. The first
  argument is taken as image A's path, which does not exist: the interface is English, the
  dialogs ask, and nothing on screen says why; only the log does.
- **Owner's call:** R-002 says a path that does not exist falls through to the dialogs, so this
  is the behaviour as specified; R-102 exists to say such a mistake, for `--lang` alone. An
  argument that begins with `-` and names no file could be reported as R-102 reports a language.
- **Test:** none for an argument that begins with `-` and is not `--lang`.

### CR-024. `About` is the one action not held back during a load

- **Severity 4, rare, to measure.** Status: open.
- **Where:** `src/ImageVisualCompare.Wpf/MainWindow.xaml.cs:106-110` (no check of `_opening`).
- **The case to run.** During a load, Tab to `About` and Space (the overlay stops only the
  mouse). The load then ends inside About's modal loop: on success the pair appears behind it;
  on failure the message is a second modal dialog of the same window, then the file dialogs, and
  cancelling those with no pair closes the main window with About still open. No crash was found
  by reading. ADR 0036 says a key pressed during a load is lost.
- **Direction:** ignore the click while a pair is being opened, as the other three flows do.
- **Test:** none.

### CR-025. A FITS or float TIFF decode writes a `WriteableBitmap` on a pool thread

- **Severity 4, common, to measure.** Status: open.
- **Where:** `src/ImageVisualCompare.Wpf/Imaging/FitsImageLoader.cs:87`, `:316`;
  `src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs:76`, `:364`;
  `src/ImageVisualCompare.Wpf/Imaging/Bitmaps.cs`.
- **What may happen.** `WritePixels` on a thread-pool thread may make WPF create that thread's
  own media context, with a hidden top-level window on a thread that never pumps messages, left
  behind when the pool retires the thread. This rests on the area 3 reviewer's memory of WPF's
  source, not on anything read here.
- **To measure:** after a FITS load, list the process's top-level windows and look for one of
  WPF's on a thread other than the UI's.
- **Direction:** if it is there, one long-lived thread owned by the loader; if not, a line in
  `docs/design/imaging.md` saying it was looked for.
- **Test:** none.

### CR-026. A canvas so long that its minimap has no height: a click jumps to the top-left

- **Severity 4, crafted, confirmed.** Status: open.
- **Where:** `src/ImageVisualCompare.Core/MinimapGeometry.cs:59-62`, `:110-113`;
  `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs:90`, `:172` (both guards look at the
  overlay's width only).
- **The case.** A 9600 × 100 canvas gives a minimap of 190 × 1.98 DIPs; inside its 1-DIP border
  the thumbnail is 188 × 0. `ContentPointAt` answers the origin for an area with no height, so a
  click on the strip jumps the view to the top-left. A 100 × 7600 canvas gives a width of 0:
  `Refresh` returns before touching the rectangle, which keeps what the previous pair left.
- **Direction:** guard both dimensions in `MinimapView`, and hide the rectangle when the
  thumbnail has no area.
- **Test:** `MinimapGeometryTests` has no case of zero height for `ContentPointAt`.

### CR-027. `MaxWidth` lets through seven widths whose row is larger than .NET's largest array

- **Severity 4, crafted, to measure.** Status: open.
- **Where:** `src/ImageVisualCompare.Core/FitsHeader.cs:99-100` (`MaxWidth = int.MaxValue / 8`,
  commented "still fit one array"); `src/ImageVisualCompare.Core/FitsRowDecoder.cs:33`.
- **The case.** `BITPIX` -64, `NAXIS1` from 268,435,449 to 268,435,455, with the data present (a
  file over 2 GB). The probe passes, since 268,435,455 × 8 = 2,147,483,640 fits an `int`; the
  decoder's row is larger than `Array.MaxLength` (2,147,483,591) and the allocation throws
  `OutOfMemoryException`, said as a read failure with .NET's text and not as R-019's "too
  large".
- **To measure:** whether `Bitmaps.Create` refuses that width first, with the right message.
- **Direction:** the limit from `Array.MaxLength`.
- **Test:** `R019_AnImageWhoseSizeOverflows_IsTooLarge…` uses 300,000,000.

### CR-028. Notes, requirements and comments that describe code that is gone

- **Severity 4, confirmed.** Status: open. One task for the group; an ADR's own text is not
  edited (the kit's ADR 0024), so where an ADR is stale the note that covers the code says what
  is true now.
- `docs/design/imaging.md:167` names a method `Decode` of `WicImageLoader`; the work is in
  `LoadFloat` and `Pass`. (Done with CR-004, 34e7e45e.)
- `docs/design/logging.md:84` gives the outcome `declined`; the code writes
  `declined: the padding was answered no` (`ImagePairLoader.cs:341`).
- `docs/design/language.md:63-66` says the window never chooses a side by hand, through
  `UiText.ChangeImage`; `MainWindow.xaml.cs:90-91` assigns `ChangeA` and `ChangeB` by hand and
  nothing in `src/` calls `ChangeImage`.
- R-015 and the rule "A FITS, and a float TIFF, is shown as stored" in `CLAUDE.md` name
  `FitsScale.ToByte` and `FitsScale` as the one place a value becomes a level; in the
  application the high byte is taken in `DisplayRenderer.RenderLinear` (`:189`, `:205-207`) and
  in `LevelLookup`, and `ToByte` and `Level` are called by tests only.
- ADR 0010 and ADR 0013 cite a `CompareView.Clear`, and ADR 0035 `FloatSamples.ToBgra32` as the
  application's path; neither is: `Clear` does not exist, `ToBgra32` is called by tests only.
  `Viewport.SetContentSize` documents "0 when nothing is shown", which nothing does any more.
- `MainWindow.xaml.cs:495-496`: the summary of `ShowPair` says the old pair's memory "is given
  back first"; the body releases it after the new pair is on screen and a render pass has run
  (ADR 0040). (This half done with CR-006's review, 1f6223dc; the `from` parameter below is not.) `:274`: the `from` parameter lists `the start` and `Ctrl+O`, not `the Open
  button`.
- `DisplayRenderer.cs:50-51`: the linear mode "looks every plane up the same way"; it shifts and
  never reads `_lookups`.
- `UiText.cs:96`: "shortest first to go when trimmed" describes nothing the code or R-054 does.
- `CompareView.xaml.cs:319-320`: "without first having to aim at a mode button"; there is no
  such button. `:515-518`: "clicking it"; the thin bars have no track to click. (This one done
  with CR-015, 0f23b15f.)
  `:688-703`: `UpdateScrollBars` says setting a bar raises no `Scroll` event, and guards against
  one with `_updatingScrollBars`; one of the two is not needed.
- `WicImageLoader.cs:395-401`: "for one read from start to end", while `LoadFloat` passes over
  the file up to three times. `:362-363` says a row a write is to be avoided for the locks;
  `FitsImageLoader.cs:316` writes a row a write.
- `LogSetup.cs:413`: the first line's "(fallback: the folder beside the executable cannot be
  written)" is also written when the folder could be written and only the newest file was
  read-only or locked.
- `PairLabelLayout.cs:57`: "A NaN name or fixed width is already refused above, since NaN
  carries the sign bit" holds for `double.NaN` only; a NaN without the sign bit passes. Not
  reachable from `PairLabelPanel`.
- `FileDialogService.cs:24`: the interface "asks for the two files of a pair"; it also asks for
  one. `ImagePairValidator.cs:61`: the summary of `Inspect` tells history ("not a refusal any
  more"). `DividerModel.cs:35-36`: `LineGrabTolerance` gives no unit.

### CR-029. On a narrow window the toolbar cuts `Change B…` away

- **Severity 3, some setups, confirmed.** Status: open. Added on 2026-11-21 by the task that
  fixed CR-013 to CR-017: seen in the render of the real window 960 wide, not found by the
  review.
- **Where:** `src/ImageVisualCompare.Wpf/MainWindow.xaml` (the toolbar's two columns: the pair
  label in `*`, clipped, the tools in `Auto`); `src/ImageVisualCompare.Core/PairLabelLayout.cs`
  (`MinName`, 48 DIPs).
- **What happens.** The tools on the right take the width they ask for, 717 DIPs in English,
  and the pair label what is left. The label trims the two names down to 48 each and no
  further (ADR 0048); what still does not fit is cut at the column's edge, and the last thing
  in the label is `Change B…`.
- **The case, measured.** A window 960 wide, half of a 1920 screen: a client of 944, of which
  the label gets 203. `Change B…` had 0 of its 101 DIPs on screen and B's name 14 of its 43;
  in the render the name runs into `Navigation`. With the names at the widths they had there,
  the label ends 342 from its start, so by these widths the button begins to be cut below a
  client of about 1083. `Ctrl+O` still opens a pair; nothing else changes B alone.
- **Should:** both buttons reachable at any width the window can have, or a smallest width for
  the window.
- **Direction:** a `MinWidth` for the window from the toolbar's needs, as the suite's other
  tools have; or let the tools give room (the `Navigation` word first).
- **Test:** `PairLabelPanelTests` cover the label inside the room it is given, not the room.

## Cleanup

Not defects; a task may take several together.

**Called by tests only, or by nothing:**
- `FitsScale.ToByte`, `FitsScale.Level`; `FitsRowDecoder.ReadFileRow(int, Span<byte>)` with its
  two fields; `FloatSamples.ToBgra32`; `LevelLookup.Level`; `RainbowTable.Colour`;
  `SampleHistogram.Count(ushort)`; `Viewport.ContentToScreen`; `RectD.Right` and `Bottom`.
- `UiText.ChangeImage(ImageSide)` (`UiText.cs:65-68`).
- `MinimapView.Hide()` (`MinimapView.xaml.cs:79-85`).
- `ColorChannels.GrayAlpha` (`ImageInfo.cs:25`, worded at `UiText.cs:321`, `:330`): no loader
  produces it.
- `LevelLookup.Linear`: a placeholder before `Prepare` and the subject of one test.

**Written twice:**
- `FitsImageLoader.cs:119-142` and `WicImageLoader.cs:190-213`: the same count, rescale, prepare
  and render sequence.
- `FitsImageLoader.cs:39` (`CardBytes`) repeats `FitsHeader.CardSize`.
- `PadPlan.Dimensions` (`ImagePairValidator.cs:37`) repeats the format of `ImageInfo.Dimensions`.
- The zoom percentage is formatted in `CompareView.xaml.cs:728` and `MainWindow.xaml.cs:575`.
- `CompareView.xaml:66`: `CornerRadius="13"` repeats half of `DividerModel.HandleWidth` by hand.
- `WicImageLoader.cs:267-269` against `:459-467`: the log's catch guards the same three tag
  queries `FloatSamplesOf` makes unguarded just before.

**Small inaccuracies and leftovers:**
- `FitsHeader.cs:379-386`: `Value` ends a string at the first quote, so a doubled quote
  (`'O''HARA'`) is cut. No keyword the reader uses is affected.
- `FitsHeader.cs:329-368`: `ReadCards` has no limit on a header's length; a file that starts
  `SIMPLE  =` and has no `END` card is read to its end before the message.
- `WicImageLoader.cs:516`: `Bgr565` is reported as 5 bits a channel; green has 6.
- `Version.cs:78-84`: the assembly-version fall-back gives four parts against R-070's three; not
  reached in an SDK build.
- `MainWindow.xaml.cs:159`: `Ctrl+O` matches any chord with Ctrl (Ctrl+Shift+O, AltGr+O), while
  `H` and `F` ask for no modifier at all.
- `Program.cs:59-61` with `LogSetup.cs:201-203`: the "ended by an unhandled exception" line is
  probably never written, since an exception nobody catches ends the process without running
  `Main`'s `finally`; the note already hedges it.
- `App.xaml.cs:113-128`: `_startupSelection` is not cleared when a later call names no pair;
  harmless in the application, and why `LoggingTests` takes it back by hand.
- `LogSetup.cs:141-147`: the hooks are installed before the first line is composed; a throw
  there leaves a hooked log without its first line.
- `LogText.cs:146`, `:155`: `Quoted` does not escape a quote inside the text, and `OneLine`
  leaves a lone carriage return; reachable only by a crafted argument.
- `MemoryHousekeeping.cs:74`: `process.Refresh()` on a `Process` made the line before does
  nothing.
- `CompareView.xaml.cs:164-165`: the two `Visibility = Visible` lines change nothing now that
  nothing collapses the backdrops. `:735`: a new `RectangleGeometry` at every `Sync`.
  `:649-657`: `OnKeyUp` sits far from `OnKeyDown`.
- `CompareView.xaml:8-9`, `:33`, `:52`: `SnapsToDevicePixels="False"` and
  `UseLayoutRounding="False"` restate the defaults with no comment saying why (it matters for
  CR-002).
- `CompareView.xaml:56-59`, `:81-86`: the vertical thumb at its top overlaps the zoom badge's
  right edge by 2 DIPs.
- `WicImageLoader.cs:380-381` (`TooLarge`) and `FitsImageLoader.cs:266-267` (`CreateBitmap`):
  wrappers of one line, each used once.
- `AssemblyInfo.cs:20-27`: the template's `ThemeInfo` with the template's comments; the project
  has no `generic.xaml`.
- `ThirdParty.cs:67`: a stray blank line before the class's closing brace.
- `FitsHeader.cs:436`, `FitsScale.cs:82`, `FitsImageLoader.cs:323-324`: a `//` comment above the
  `///` block of the member it explains. (The first went with CR-009, a21d59ab, the last with
  CR-010, 54f7807a; `FitsScale.cs` is not done.)
- `Directory.Build.props:31`: `GenerateDocumentationFile` puts the two `.xml` documentation
  files beside the executable of a publish; no ADR or note says that is accepted.
