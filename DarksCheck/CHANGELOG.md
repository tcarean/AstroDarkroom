# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The project, started from `ClaudeCodeStarterKit` on 2026-10-11 (ADR 0001): the kit's gate,
  skills, hooks and documentation set; the raw and FITS readers, the PNG encoder and the
  bitmap font copied from LightsCheck, which holds FlatCheck 2.3.0's copies with the shot
  metadata added (ADR 0003); libraw under LGPL-2.1 as the only package.
- The console `darkscheck <folder>` (R-001 to R-007, ADR 0002): opens a Siril session folder
  (`darks/` required, `biases/` and `lights/` optional) or a bare folder of darks, reads the
  metadata of every frame without decoding it, and prints the inventory: the three groups
  with their counts, the camera, the exposure and ISO of each group, the cadence of the darks
  and of the lights (interval, span, the pauses over 5 s), whether the darks match the lights
  in exposure and ISO and whether the bias is a bias, and one table per group with a row per
  frame (time, exposure, ISO, the pause before it). A frame that cannot be read is listed with
  its error and the run exits 1. `--inventory` names this metadata-only run.
- Checks 2 to 8 on the darks and the biases (R-020 to R-041, ADR 0007): `darkscheck <folder>`
  decodes the biases and the darks into memory, builds the median and mean masters, and
  prints a section per check with its figures and a verdict line: light leaks and gradient
  (the binned dark, bias and dark-minus-bias maps in nine regions), thermal drift (the
  warming of the series read from the hot pixels, in degrees), noise per frame (the residual
  sigma, the read noise from the biases, the random banding, the outlier frames), hot pixels
  (counts at five levels, amplitudes, stability, RTS candidates), fixed pattern (structured
  banding and glow), dead and saturated pixels, and how many darks are enough (the noise of
  the mean of n frames against sqrt(n) and the master's noise against the quantisation step);
  then a table per frame. Figures in ADU equivalent at ISO 400 and 120 s. `--out DIR` writes
  the report, a CSV per darks and per biases and the three maps of the reference plane as
  PNG; `--threads N` sets the parallel decoding, `--max-frames N` the frames held in memory
  (60 by default; above it every k-th frame is read). A FITS without a black level takes the
  biases' median as black. V-003 and V-004 check the Smida darks and the 2026-10-10 library
  against the Python prototype's figures.
- `prototype/`: the Python scripts of the 2026-10-11 analysis of the Smida session of
  2026-08-12, frozen, and their report, figures and CSV files under
  `docs/validation/2026-08-12-smida/`; V-001 checks the inventory of that session and V-002
  the library of darks of 2026-10-10.
