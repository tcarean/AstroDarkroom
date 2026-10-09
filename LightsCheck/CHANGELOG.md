# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The window, `LightsCheck.Wpf.exe` (R-029 to R-032, ADR 0008): open a session folder (or pass
  it on the command line), the series runs by itself with a progress bar and a stop button;
  the tabs Rezumat (the report's rows with verdict chips), Cadre (one row per frame, the star
  cut-outs of the selected frame), Dithering (the map of positions, the step per frame, the
  nearest-frame distance, the dithering rows), Calitate (FWHM, elongation, stars, background
  over the night), Respinse, Raport (the console's text); the pixel size and the thread count
  as inputs with the D3300's defaults; `Scrie fisierele...` writes the three files of `--out`
  into a chosen folder. The dark theme, the About window and the icon follow FlatCheck; the
  icon is LightsCheck's own, drawn per size by `Icon/make_icon.py`.

### Changed

- The series report prints every label of a section in one 19-column field; the few labels
  that had their own padding (FOCUS, ROUA, CADRE MISCATE, the first DITHERING line) line up
  with the rest, and the second coverage line has its own label, `pozitii distincte`. Every
  REZUMAT line and every verdict line now opens with a verdict word, also where there is
  none to give: `NEDETERMINAT - niciun cadru bun` where 0.1.0 printed `nedeterminat (niciun
  cadru bun)`, `NEMASURAT - ...` for the exposure without biases. The RESPINSE heading counts
  the unreadable frames too (`2 cadre slabe, 1 necitite`). No figure changed.

- The whole series (R-018 to R-028, ADR 0007): `lightscheck <folder>` measures every light in
  parallel, registers them to the first (shift, rotation, matched stars), and prints the
  report the hand analysis had: focus and its trend, exposure against the read noise from the
  biases, dew, the trailed frames (bad at elongation 1.6, marginal at 1.35, each with its
  figures and thresholds), drift and field rotation, the RA axis from a plate-solved FITS at
  the root or from the trail direction, the dithering (moves and pauses, "every N frames",
  size classes, axes and signs, coverage, runs, the hot-pixel and walking-noise verdicts),
  the frames to drop, and a table per frame. `--out DIR` writes the report, a CSV with every
  figure and the plain list of rejected file names. `--inventory` keeps the metadata-only run.
  `--threads`, `--pixel` for the pixel size when the files do not say it.

- `lightscheck <folder> --frame N` and the frame analyzer behind it (R-012 to R-017, ADR
  0006): the levels of the four CFA positions, the green channel's tile background and noise,
  the stars (4-connected components of the smoothed residual over 5 sigma) with their second
  moments (FWHM, elongation, angle), the fraction elongated, the angle vote and its coherence,
  the FWHM per radial zone, the halo of the brightest stars, the satellite trails, and the
  list of the 600 brightest stars for the registration of the next step. Every constant is in
  `Thresholds.cs`. V-002 to V-007 check six Veil frames against the Python prototype's figures.

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
- `prototype/`: the Python scripts of the 2026-10-09 analysis of the Veil session, frozen, and
  their report and figures under `docs/validation/2026-10-06-veil/`; V-001 checks the inventory
  on that session.
