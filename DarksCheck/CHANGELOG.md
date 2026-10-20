# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Changed

- The label of a folder already renamed to a label (its name ends in `-idx` and digits) loses
  that suffix before the new one is appended, so `...-15.8celsius-idx67` reads `...-idx67`
  again, not `...-idx67-idx67`, and a changed index shows as a changed number (R-061, ADR 0013).

### Added

- Check 2 tells a light leak from the sensor's glow by the warming of the series (R-028,
  R-029, ADR 0012). The row `lumina pe cadre` fits every frame's mean on the hot pixels'
  amplitude ratio (Theil-Sen, three clipping passes) and names the frames that carry light
  over it (5 scales and 0.1 ADU-eq), which make the verdict `ATENTIE` with the advice to take
  them out of `darks\`; the row `glow / constant` splits the verdict region's excess into the
  part that grows with the warming and the constant part, and with biases into the readout
  offset the biases hold and what remains; the verdict's words follow the cause (glow, leak,
  readout offset, or undecidable without biases; the split needs the series to have warmed by
  0.1 of amplitude ratio, and shows the raw ADU beside the equivalent); check 6's `glow` row
  shows the glow part;
  `TABEL PE CADRE`, the Cadre tab and the darks CSV carry every frame's excess light, and the
  CSV the four corners' offsets. Measured on the owner's deliberate leak set of 2026-10-20,
  which the old check read as `OK`.
- Check 2 also names the light that is in every frame (R-029, ADR 0014): with biases, the row
  `lumina constanta` reads the four sides (an edge with its two corners) after the bias, with
  the error of each, and a side over 0.15 ADU-eq and three times its error makes the verdict
  `ATENTIE: lumina constanta in toate cadrele: ... scurgere de lumina (ocular, montura)`; the
  lit-frame sentence says `in plus fata de restul seriei`; `TABEL PE CADRE` and the Cadre tab
  gain the column `latura`, the worst side frame by frame; every column header of the Cadre
  and Lights tables has a hover hint. On the leak set the eyepiece band of frames 1 to 25
  (+0.20 ADU-eq on the top side) is now named beside the five frames lit from the front.
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
  biases' median as black. V-003 checks the Smida darks against the Python prototype's figures;
  V-004 pins the 2026-10-10 library run (the prototype has only its master, regions and warming).
- Check 9, the index and the matching (R-050 to R-065, ADR 0008): the lights are decoded one
  by one and searched for isolated hot pixels (6 sigma over the median of the eight same-colour
  neighbours, no immediate neighbour over 3 sigma), the pixels persistent in 80 % of the lights
  are compared with the master's hot pixels both ways, and the lights' amplitude at the
  master's hot pixels gives the lights-over-dark ratio per light and over the night, the
  degrees and the verdict (direct subtraction, `-opt`, cosmetic only, unmatched). `INDEX`
  prints the rank index of the darks' master (`idx25` on Smida, `idx67` on the library), the
  label to put in the folder's name, the lights' equivalent index, and with `--against DIR` a
  partner folder's index and label, the session's darks over it on the pixels hot in both
  masters, the overlap of the two hot-pixel sets and the lights against the partner. `--out`
  also writes `darkscheck-lights.csv`; the report gets `TABEL PE LIGHTS`. V-005 checks the Smida
  lights and the library as partner against the prototype's section 9, V-006 the library's index.
- The window `DarksCheck.Wpf.exe` (R-070 to R-076, ADR 0009): the same analysis as the
  console, started on a folder from the command line or chosen at start, with progress and a
  stop button; seven tabs: Rezumat (every check as a card of the report's rows with the
  verdict as a coloured chip), Cadre (the frames table with the warming, residual sigma and
  banding charts), Harti (the three binned maps), Pixeli fierbinti (check 5 and the noise
  curve over its expected line), Lights (check 9, the ratio per light with the verdict bounds,
  the lights table), Index (the label with a copy button, the INDEX rows, the partner folder
  of `--against`), Raport (the console's text, identical whatever the Windows culture); the
  export writes the same files as `--out`. Four settings in the top bar, threads, frame cap,
  gain per ISO stop and degrees per doubling, plus the partner, remembered in
  `%LOCALAPPDATA%\DarksCheck\settings.json`; the gain and the degrees reach the core as the
  normalisation's settings (the console keeps the defaults, 2 and 6). Dark theme, dark title
  bar, "Despre" window and its own icon, drawn by `Icon/make_icon.py`.

- A session with lights and no darks (R-002 refined, R-054, R-077, ADR 0011): opened with
  `--against DIR` or the window's partner, the partner's darks stand in for the session's,
  taking checks 2 to 8, check 9 against the session's lights and INDEX; the report says whose
  darks they are and drops the rows that would compare the partner with itself. Without a
  partner the folder is refused with a sentence naming what to give, and the window's problem
  card offers `Alege partener...`; `--inventory` lists such a folder with `darks: (lipsa)`.
  V-007 runs the Veil lights of 2026-10-06 against the 2026-10-10 library.

### Internal

- `prototype/`: the Python scripts of the 2026-10-11 analysis of the Smida session of
  2026-08-12, frozen, and their report, figures and CSV files under
  `docs/validation/2026-08-12-smida/`; V-001 checks the inventory of that session and V-002
  the library of darks of 2026-10-10.
- The report is built as rows (`DarkReport.Sections`, `HeaderRows`, `FrameRows`, `LightRows`)
  that `Write` prints and the window binds; the console's text is unchanged. The normalisation
  is a record of the core (`Normalisation`), built by the run from the two settings of
  `DarkOptions`.
