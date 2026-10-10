# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

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

### Changed

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
