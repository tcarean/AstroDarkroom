# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- A diagnostic log, for bug reports (R-070 to R-073, ADR 0029): each application writes
  `log\lightscheck.log` or `log\LightsCheck.Wpf.log` beside its executable (or under
  `%LOCALAPPDATA%\LightsCheck\log` when that folder cannot be written), at most 5 files of 1 MiB
  each, rolled by NLog, two windows writing into one file without losing a line. It holds the
  arguments, the machine and the runtime, the session's folders, every file read, each run's
  options, stages and times, each frame's figures, the figures and thresholds behind every
  verdict, every file written, the window's runs and buttons, and every error with its stack,
  those the program works around included (not a stop you asked for, nor what the window drops
  while it closes). The log's own words are English whatever `--lang` says; an error's or a
  refusal's message appears as it was shown. `LIGHTSCHECK_LOG=off|fatal|error|warn|info|debug|trace`
  sets the level, Debug by default. Nothing changes on stdout, on stderr or on screen.

- The interface speaks English or Romanian: `--lang en` or `--lang ro` (also `--lang=ro`), in the
  console and in the window, English when it is missing; a value it does not know stops the
  console with an English `ERROR:` and exit 1, and the window says it and goes on in English
  (R-060 to R-063, ADR 0022).
- `--east left|right` and `--north up|down`, the English forms of `--est stanga|dreapta` and
  `--nord sus|jos`; both forms work in both languages (R-064).
- `.pef` (Pentax) is a raw extension, and the lens of a raw of any make is read as FlatCheck
  reads it since its ADR 0068 (R-065, ADR 0025): the maker's lens ID by mount (Nikon's own field
  on a Nikon body, the MakerNote's on Canon, Sony, Pentax and the rest), the mount's name, the
  lens's name as the camera wrote it and its range. The inventory's `lens` line names it:
  `"EF50mm f/1.4 USM" (ID 198, Canon EF)`, `ID 2263 (Pentax K)`.

### Added

- The frame table shows each frame's exposure, after `time`, in the console and in the window
  (R-085, ADR 0040). A frame exposed under half the session's usual exposure is bad and leaves
  the stack, with the reason `exposure 0.2 s, under half the usual 120.2 s: too short to be
  worth stacking`; its few stars and dark sky no longer read as clouds or as a jump. A frame
  with another exposure stays in with a note, and a background jump its exposure predicts says
  so. On the Veil session of 2026-11-06, where the intervalometer misfired, the two 0.2 s frames
  and the 34.9 s one leave and the 137.5 s one stays.
- The session lines open with the total exposure to the second, in the console and in the
  window, where the Summary tab's SESSION card shows it first, in bold and a little larger
  (R-086, ADR 0042): `total exposure : 2 h 54 min 30 s` on the Veil session of 2026-10-06, whose
  interval line gives the same total in minutes.

### Changed

- The default SynScan arrows are those of a Star Adventurer GTi driven by the SynScan Pro app,
  as tested on that pair with a target west of the meridian: `→` east and `↓` north, so `←` west
  and `↑` south (R-040, ADR 0041). East of the meridian the DEC arrows may swap; not tested yet.
  Until now `↑` was north, so a DEC move named with the default arrows now shows the other
  arrow; the sense on the sky (N or S) and every figure stay. `--help` and the EAST and NORTH
  tips say whose defaults they are. A window that has stored its arrows keeps both, since it
  saves the two together whenever either changes: check NORTH once after the update.
- The walking-noise verdict reads what reaches the stack, not how spread out the dither map
  looks (R-084, ADR 0039): `INSUFFICIENT` when the frames inside runs at one spot whose streak
  reaches 3 px are 25 % or more of the aligned frames, `NO walking noise` when they are fewer
  but the stack keeps over one and a half times the ideal share of one frame's pattern, `OK`
  otherwise. Until now it wanted half the frames in different spots, 30 % alone and a quarter of
  the moves on each axis, which a random dither inside a small area cannot give: a session of
  85 frames dithered after 77 of them read `INSUFFICIENT` with a nearly ideal pattern figure.
  The verdict's text now says in how many frames the streak would show and what the stack
  keeps of the pattern. The different spots, frames alone and two-axis counts stay as
  information.
- The window's four top-bar inputs, PIXEL, THREADS, EAST and NORTH, explain themselves in plain
  words (R-083, ADR 0038): a tooltip on the label and the field, shown also during a run, says
  what to type and what is used when the field is left as it is, the threads count of this
  computer included. The pixel tip now says that a size written in the files wins over one typed.
  The threads field starts with that count written in it (it runs as the blank field did), and its
  Romanian label reads `THREADURI` instead of `FIRE`.
- The Rejected tab and the console's REJECTED line speak in plain words (R-081, ADR 0036): the
  line under the heading counts each kind of frame in the singular for one (`1 cadru slab si 1
  cadru necitit` in Romanian, which read `1 cadre slabe, 1 necitite`) and says that the marginal
  frames stay in the stack; the four paragraphs say what the elongation is (how many times
  longer than wide the stars are), where the marginal frames are listed, what a trail and its
  angle are and what a trail on each axis comes from, and the button note says the file holds
  these frames' names. Every figure is unchanged.
- The Quality tab speaks in plain words (R-080, ADR 0035): its four charts are headed `the stars'
  width (FWHM)`, `how elongated the stars are`, `stars found` and `the sky background, on green
  (ADU above the black level)`, each note says what the figure is in each frame and what moves
  it, and a new line above the charts says that each point is a frame, in the order they were
  shot, and what the colours of the first two charts mean.
- The Frames tab and the reasons a frame is flagged speak in plain words (R-079, ADR 0034),
  wherever the reasons are printed: on the Frames and Rejected tabs and in the console's REJECTED
  section and frame table. A reason names the class its figure gives
  (`elongation 1.76 (from 1.60 the frame is bad)`), compares the stars' width and count with the
  session's usual ones (`wide stars: ...`, `too few stars: ...`), says the sky background jumps
  against the neighbouring frames, that Siril takes a satellite or plane trail out when stacking,
  and that a frame could not be aligned. The column tooltips, the cut-outs' caption and wait note,
  and the line under each cut-out (`width (FWHM)`, the angle in degrees) are rewritten the same
  way; the Romanian plural of trails reads `3 dare`. The Frames table's reasons column widens
  from 280 to 560 px, so the reason of a trailed frame, bad or marginal, shows whole in either
  language; a longer one is still whole in its tooltip and on the Rejected tab. Every figure is
  unchanged.
- The Summary tab, the console's session lines (`--inventory` too) and its sections SUMMARY,
  FOCUS, EXPOSURE AND NOISE, DEW, TRAILED FRAMES and DRIFT AND ROTATION speak in plain words
  (R-078, ADR 0033): `the stars' width (FWHM)` and `typically` for the median, `total exposure`
  for the integration, `extra noise` for the penalty, `the light around the stars (halo)`, the
  polar error as how far the mount's axis is off the pole and its cost as how far it moves the
  stars, angles in `'`, `(when shot)` and the lens's mount named as one, and a legend under the
  noise rows labelled `what it means`. Every figure is unchanged.
- The Dithering tab and the console's DITHERING section speak in plain words (R-077, ADR 0032):
  `frames aligned` for registered, `how close the frames are` for coverage, `different spots`,
  `frames alone`, `area of the positions`, `hot pixels per million pixels`, `typically` for the
  median move, and no sigma or Mpx; the captions under the map and the charts and the five
  paragraphs under the card are rewritten the same way, and the Summary rows that share a piece
  (`trailed frames`, `trail direction`, `drift over one exposure`, `dithering`) follow. The
  explanation no longer says a 3"/min drift stays under a pixel for a whole session at 20 mm: it is
  0.15 px per 2-minute exposure there, against about 1 px at 135 mm. Every figure is unchanged;
  the validation case V-010 records the tables' new words, its figures kept.
- When libraw cannot be loaded, the error names Microsoft's Visual C++ runtime, missing or too
  old, as the likely cause, with Microsoft's link to its latest version,
  `https://aka.ms/vc14/vc_redist.x64.exe`, instead of advice on where the DLLs sit and how to
  publish, which held in neither the single-file programs nor the suite folder; the log adds,
  once, which of the runtime's four DLLs Windows cannot load (R-074, ADR 0030).
- About lists the six components the tool ships: libjpeg-turbo, Little CMS and zlib, the
  libraries inside libraw's DLLs, joined Sdcb.LibRaw, LibRaw and NLog. The line above the list
  counts them right (it said two, with three listed) and points to the licence texts in the
  suite's `THIRD-PARTY-NOTICES.txt`, in the source `AstroDarkroom/THIRD-PARTY-NOTICES.md`
  (R-075).
- Without `--lang`, the console and the window now speak English; `--lang ro` gives the
  Romanian output as it was.
- The inventory's `lens` line reads `ID 74 (Nikon F)` where it read `Nikon ID 74`: the ID is
  no longer Nikon's alone, so the mount says whose number it is (R-065).
- The pixel size, when no file carries `XPIXSZ` and none is typed, comes from the raw itself:
  the 35 mm-equivalent focal length the camera writes, over the shot's focal length, over the
  mosaic's diagonal (4.00 um on the D3300 at 135 mm, 3.99 at 20 mm); the D3300's 3.92 is used
  only when the raw gives no such figure. `--pixel` and the window's field, blank by default,
  still override it; the `scale` line names the source (R-066, ADR 0026). On the Veil session the
  scale reads 6.11 "/px instead of 5.99, so every arcsecond figure grows by 1.9 %.
- The files of `--out` and of "Write the files..." are named in English in both languages:
  `lightscheck-report.txt`, `lightscheck-frames.csv`, `lightscheck-rejected.txt` (they were
  `lightscheck-raport.txt`, `lightscheck-cadre.csv`, `lightscheck-respinse.txt`); the CSV's text
  cells follow the run's language (R-028, ADR 0022).
- The Frames tab's `dithering` tooltip named the old default arrows (left = east); it now points
  at the EAST and NORTH selectors.
- `README.md`, the user guide, is in English: it quotes the English output, the default, and is
  brought up to date with the program, its examples run again on the Veil session (ADR 0024).

### Fixed

- Romanian counts of 20 and up take `de` before the word they count, as Romanian writes them
  (R-082, ADR 0037): `lights\ (87 de cadre .NEF)`, `din 87 de cadre`, `22 de cadre slabe`, `din 39
  de perechi de cadre`, `22 de ori` in the dithering table, `dara pe RA (-90 de grade)`, and in
  `--frame` `40 de stele retinute`. Counts under 20 or ending in 01 to 19, figures with decimals
  and units are written as before, and so is the English; the validation case V-010 records
  `22 de ori`, its figure unchanged.
- About fits the screen it opens on: never taller than the screen's work area, its cards
  scrolling with the wheel and the keys when they are taller, the Close button always in view.
  It overflowed a 1366x768 laptop and a 1080p screen at 150 %, and with this version's six
  components a 1080p screen at 125 % too (R-076, ADR 0031).
- The content of the window's tabs is in the UI Automation tree: a screen reader such as
  Narrator, or a UI Automation tool, now reaches what each tab shows, where it found only the six
  tab headers before (R-067, ADR 0028).
- A `.fit` whose header begins with `SIMPLE` but ends before `END` was read as a FITS with the
  cards found so far; it is now refused with `FITS: incomplete header.`, by the inventory's
  metadata read and the full load alike, and a file that does not begin with `SIMPLE` is
  refused by both with `FITS: the file does not begin with SIMPLE.` (FlatCheck's check, R-008,
  ADR 0027). The inventory's row for such a file reads that message where it read `not a FITS
  file (it does not begin with SIMPLE)`.
- A session read from FITS lights (a folder with no raws, for example Siril's `pp_light_*.fit`)
  had the sense its y axis feeds the wrong way round in the dithering cells, the CSV's
  `move_ra_px` / `move_dec_px`, the star drift and the polar handedness: the solved FITS was
  flipped against the lights as it is against the raws, while FITS lights are stored as the
  solve is. The flip now applies to raw lights only; the raw sessions read as before
  (R-038, R-050; ADR 0023).
- The CSV of a session whose lights are a mosaic of unknown Bayer order (Siril's calibrated
  `pp_light_*.fit`, for one) had four cells more than its header on every row: the channel
  names `CFA[0,0]` to `CFA[1,1]` were written bare, comma and all. A channel name with a
  comma is now quoted (R-028).
- A FITS light whose data is shorter than its header says (a copy cut mid-transfer) crashed
  `lightscheck --frame N` and was listed with `Index was outside the bounds of the array.`;
  the reader now refuses it with `FITS: data cut short: ...` (`FITS: date trunchiate: ...` with
  `--lang ro`) and both byte counts, so the console exits 1 with the `ERROR:` line. Taken from FlatCheck's fix (R-008, R-017, ADR 0021).
- `--pixel NaN` and `--pixel Infinity` were accepted by the console, `Infinity` by the window.
  Both now refuse anything that is not a finite number greater than 0, with the same message as
  before, by one check in the core (`SeriesOptions.PixelSizeIsValid`; R-004, R-029).
- In the window, a star cut-out decode that failed after another frame had been selected wrote
  its `EROARE` note under the newer frame; a stale decode now writes nothing, success or
  failure alike (R-031).
- `lightscheck --frame N` on a raw libraw cannot open (a `.NEF` that is not a raw) crashed with
  libraw's own exception; the reader now reports it as an unreadable file, `<file>: libraw nu
  poate citi fisierul`, so the console prints the `EROARE:` line and exits 1 (R-007, ADRs 0019
  and 0020).
- In the window, the progress reports of a run that was stopped or replaced could still land
  after the next run had started and show the old run's counts on the bar and the wait line;
  a report of a run that is no longer the current one is now dropped (R-029).

### Internal

- NLog 6.2.2 is the third third-party component, with its `NLog.Targets.AtomicFile` package so
  two windows writing one log lose no line (BSD-3-Clause, R-073, ADR 0029), as FlatCheck has it;
  the core logs at its seams and never in `Analysis/`, `Report/` or the readers copied from
  FlatCheck, and every test runs every log line on its path. The console dispatches on one mode,
  `Options.Mode`, which the log's `mode=` line reads too.
- Every type and method in the code has its XML documentation comment (the kit's rule
  "Documentation comments"), and every comment is English: the files copied from FlatCheck take
  FlatCheck's English text, their code unchanged (ADR 0018). No behaviour changed.
- The validation case V-014 follows the Veil session of 2026-11-06 as it now is: 82 lights, the
  three short frames deleted, no darks, and the stack's plate solve at the root, which gives the
  RA axis; its drift and walking-noise figures move with the frames (1.19 px per exposure instead
  of 1.97), its verdicts stay. No code changed.

## [1.0.0] - 2026-10-25

### Added

- The project, started from `ClaudeCodeStarterKit` on 2026-10-09 (ADR 0001): the kit's gate,
  skills, hooks and documentation set; the raw and FITS readers, the PNG encoder and the
  bitmap font copied from FlatCheck 2.3.0 (ADR 0003), the raw reader extended with the shot
  metadata (exposure, ISO, time of capture); libraw under LGPL-2.1 as the only package.
- The console `lightscheck <folder sesiune>` (R-001 to R-006, ADR 0004): opens a Siril session
  folder (`lights/` or the folder itself, `biases/` optional), reads the metadata of every
  light without decoding it, and prints the inventory: camera, lens, exposure, the interval
  with the integration and the duty, and one row per frame with its time, exposure, ISO,
  f-number, focal length and the pause before it. A frame that cannot be read is listed with
  its error and the run exits 1.
- `lightscheck <folder> --frame N` and the frame analyzer behind it (R-012 to R-017, ADR
  0006): the levels of the four CFA positions, the green channel's tile background and noise,
  the stars (4-connected components of the smoothed residual over 5 sigma) with their second
  moments (FWHM, elongation, angle), the fraction elongated, the angle vote and its coherence,
  the FWHM per radial zone, the halo of the brightest stars, the satellite trails, and the
  list of the 600 brightest stars for the registration of the series. Every constant is in
  `Thresholds.cs`. V-002 to V-007 check six Veil frames against the Python prototype's figures.
- The whole series (R-018 to R-028, ADR 0007): `lightscheck <folder>` measures every light in
  parallel, registers them to the first (shift, rotation, matched stars), and prints the
  report the hand analysis had: focus and its trend, exposure against the read noise from the
  biases, dew, the trailed frames (bad at elongation 1.6, marginal at 1.35, each with its
  figures and thresholds), drift and field rotation, the RA axis from a plate-solved FITS at
  the root or from the trail direction, the dithering (moves and pauses, "every N frames",
  size classes, axes and signs, coverage, runs, the hot-pixel and walking-noise verdicts),
  the frames to drop, and a table per frame. Every REZUMAT line and every verdict line opens
  with a verdict word (`NEDETERMINAT`, `NEMASURAT` where there is none to give). `--out DIR`
  writes the report, a CSV with every figure and the plain list of rejected file names.
  `--inventory` keeps the metadata-only run. `--threads`, `--pixel` for the pixel size when
  the files do not say it. V-008 checks the whole Veil session against the hand report.
- The window, `LightsCheck.Wpf.exe` (R-029 to R-032, ADR 0008): open a session folder (or pass
  it on the command line), the series runs by itself with a progress bar and a stop button;
  the tabs Rezumat (the report's rows with verdict chips), Cadre (one row per frame, the star
  cut-outs of the selected frame), Dithering (the map of positions, the step per frame, the
  nearest-frame distance, the dithering rows), Calitate (FWHM, elongation, stars, background
  over the night), Respinse, Raport (the console's text); the pixel size and the thread count
  as inputs with the D3300's defaults; `Scrie fisierele...` writes the three files of `--out`
  into a chosen folder. The dark theme, the About window and the icon follow FlatCheck; the
  icon is LightsCheck's own, drawn per size by `Icon/make_icon.py`.
- `prototype/`: the Python scripts of the 2026-10-09 analysis of the Veil session, frozen, and
  their report and figures under `docs/validation/2026-10-06-veil/`; V-001 checks the inventory
  on that session.
- The axis of each trail (R-033, ADR 0011): a trailed or marginal frame's reason says on which
  mount axis its stars stretched, `dara pe RA (-90 grade)`, `pe DEC` or `oblica`, when the RA
  axis is known and the frame's stars agree on a direction; the `directia darelor` row ends
  with the same word, the CSV gets `trail_on`. V-009 checks the words on the Veil session.
- The run at 10 px per frame (R-034, R-035, ADR 0011): the column `acelasi loc` of the frame
  table and of the Cadre tab (`7/10 de la 61`: the seventh of ten consecutive frames at the
  spot of frame 61), three `spot_run_*` CSV columns, and a ring instead of a dot on the
  dithering map for those frames. No class changes: a run is a property of the stack, not a
  defect of the frame.
- The Respinse tab explains its words under the heading: the elongation and its thresholds,
  the trail and its axis, what RA against DEC points to, where the marginal frames are listed.
- The Dithering tab shows the DITHERING figures as five tables (how often, how far,
  direction, coverage per radius, runs) beside the rows that are not tables; the Cadre
  headers read `dX (DEC)` / `dY (RA)` when the RA axis is known; the Respinse explanation is
  one paragraph per line with the RA-against-DEC sentence in bold (R-036, R-037, ADR 0012).
  V-010 checks the tables on the Veil session.
- The mount's move behind each dither (R-038, R-039, ADR 0013): the column `dithering` of the
  frame table and the Cadre tab says on which mount axis, how many pixels, in which sense on
  the sky and which SynScan arrow (`RA 15 px V (dreapta), DEC 12 px S (jos)`), the sense from
  the plate-solved FITS at the root with Siril's bottom-up rows accounted for, measured on the
  Veil session with two plate-solved raws; the step bars of the Dithering tab show it as a
  tooltip; the CSV gets `move_ra_px`, `move_dec_px`, `move_arrows`. V-011 checks the cells on
  the Veil session.
- The SynScan arrow keys as a setting (R-040, R-041, ADR 0014): `--est stanga|dreapta` and
  `--nord sus|jos` in the console, the selectors `EST` and `NORD` in the window's top bar,
  remembered in `%LOCALAPPDATA%\LightsCheck\settings.json`.
- The session's `darks/` folder is counted, never read, and printed after `biases` in the
  session lines (R-042).
- The walking-noise verdict reads the drift (R-043, ADR 0015): DRIFT SI ROTATIE gains
  `drift intr-o expunere`, the drift over one exposure and the dash of walking noise, the
  span of the widest stretch of consecutive frames at one spot; a dash under 3 px is `FARA walking noise` whatever the
  dithering, as on the Smida session at 20 mm, where the same drift as Veil's is a seventh of
  a pixel per frame. `INSUFICIENT` and `FARA` say the dash, the drift and the run.
- DITHERING gains `scara`, what a move of 5 and 10 px is in arcseconds at the session's focal
  length (R-044); `pattern in stiva`, how much of one frame's pattern the mean of the stack
  keeps against the ideal of one over the frames (R-048); and `pattern fix`, the sensor's
  pattern read from the lights' own median, its sigma per channel in ADU and as a share of a
  frame's noise and its hot pixels per megapixel, with whether the session's darks will take
  it out (R-046, ADR 0015).
- The Dithering tab explains walking noise, the focal length, what breaks it and how to take
  darks without touching the lens, in five paragraphs worded in the core (R-045, R-047);
  `README.md` says the same.
- V-012 and V-013 add the Smida session of 2026-08-12 (20 mm) to the validation cases.
- The polar alignment, read from the frames (R-050, ADR 0016): DRIFT SI ROTATIE gets
  `aliniere polara`, the error bracketed between the DEC drift over the sidereal rate and that
  plus the field rotation over the rate times cos DEC (the declination from the solved FITS at
  the root), with the drift the alignment can cause in one exposure and the elongation it adds,
  and `drift pe RA`, how much of the RA drift the alignment can explain and that the rest is the
  tracking; REZUMAT gets the verdict, `OK`, `LA LIMITA` or `ATENTIE` by that drift against a
  third of the FWHM. The rotation is first cleared of the dithers' slews (each turns the frame by its RA step
  times sin DEC) and read only over an hour or more. On the Veil session: between 4.5' and
  6.7', at most 0.46 px per exposure, and over 3.7 of the 4.4 "/min of RA drift are not the
  alignment.

### Changed

- The projects target .NET 8 instead of .NET 9, so the Astro Darkroom suite ships one runtime
  (ADR 0017).

- The drift is measured on the pairs of consecutive frames with no pause and no move between
  them, where nothing was pressed and the mount did not jump, and falls back to the pairs
  without a move, worded approximate, under three such pairs (R-049, ADR 0016). On Veil nine
  post-pause presses of 3 to 5 px had halved the RA drift: -0.48 px/min becomes -0.73, 1.1 px
  per exposure becomes 1.5 and the walking-noise dash over frames 61-70 9.9 px becomes
  13.7 px (V-008).

- The reason of a frame at the limit carries the trail angle too (`alungire 1.49 la limita
  (prag 1.35), dara la -90 grade`), as the bad frames' did.
- FOCUS closes with a `verdict` row, as ROUA does: the console prints it and the window's
  FOCUS card shows the chip it lacked (R-036, ADR 0012).
- The dithering verdict prints its two parts worst first (`ATENTIE la pixeli calzi, FARA
  walking noise ...`), so the window's chip takes the colour of the worse one (R-043); a chip
  opening with `OK` or `FARA` is now the good colour, where `OK pentru walking noise, OK
  pentru pixeli calzi` was the fair one.
- The arrows of the `dithering` cell are symbols after the pixels, `RA 15 px ← V, DEC 12 px ↓ S`
  instead of `RA 15 px V (dreapta), DEC 12 px S (jos)`, and the default mapping is the owner's
  SynScan, right east and up north, the reverse of the forum default of ADR 0013 (ADR 0014);
  the console window is switched to UTF-8 for the run so the symbols show, and `--out` stays
  the way to a file.
