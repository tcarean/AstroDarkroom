# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- A diagnostic log, for bug reports (R-100 to R-103, ADR 0025): each application writes
  `log\darkscheck.log` or `log\DarksCheck.Wpf.log` beside its executable (or under
  `%LOCALAPPDATA%\DarksCheck\log` when that folder cannot be written), at most 5 files of 1 MiB
  each, rolled by NLog, two windows writing into one file without losing a line. It holds the
  start (version, command line, machine), each folder opened, each file read with what its header
  or the camera says, each frame decoded with its time and the memory held after it, each run's
  options, stages and outcome, the figures behind every verdict, every output file written, the
  window's runs, dialogs and buttons, every error with its stack, those worked around included
  (not a stop the user asked for), and the exit code. The log's own words are English whatever
  `--lang` says; an error's or a refusal's message appears as it was shown.
  `DARKSCHECK_LOG=off|fatal|error|warn|info|debug|trace` sets the level, Debug by default;
  stdout, stderr and the reports are unchanged.

- `.pef` (Pentax) is a raw extension, and the lens of a raw of any make is read as FlatCheck
  reads it since its ADR 0068 (R-002, R-008, ADR 0023): the maker's lens ID by mount (Nikon's
  own field on a Nikon body, the MakerNote's on Canon, Sony, Pentax and the rest), the mount's
  name, the lens's name as the camera wrote it and its range. The frames' metadata carry
  `LensId`, `LensMount` and `LensName` where they carried the Nikon ID alone; nothing prints
  them yet.

- The first paragraph of `ADU SCALE EXPLAINED` says when the camera is not in the application's
  table, so no figure of the section is in electrons and the source's full well and read noise
  are missing (R-080, ADR 0023); until now only the `electrons` row said so.

### Changed

- Check 3 says which way the camera moved and when the series is to be taken again (R-023, ADR
  0038): the `plateau` row opens with "the camera cooled the whole series" (or warmed), with the
  hot pixels of the first and the last frame, and the verdict is a red `REDO` (`REFA`) when more
  than half the frames are more than 2 degrees from the plateau, with the advice to let the
  camera sit for three time constants before a cooling series is taken again, or to take a
  warming one longer; no distance to the plateau is printed with a sign any more, a frame is
  "above" or "below" it, and a cooling series says it started warmer than the place it was in.
  The `hot pixels` row of check 3 is gone, its figures in the `plateau` row; the window's chart
  note says what a falling curve means.
- The texts around the report speak plain words, in English and Romanian (R-088, ADR 0037): the
  start screen says the program reads and measures the frames, the progress lines read `Reading
  the darks: 3 / 20` (`Se citesc dark-urile`) in the window and on the console's stderr, and the
  top bar's hints say what each setting does, the second one named `SIGNAL / ISO STOP` (`SEMNAL /
  TREAPTA ISO`, was `GAIN / ISO STOP`); `--help` says what each option does in the same words, and
  a blank folder path reads `the folder's path is blank`.

- The Index tab's own texts speak plain words, in English and Romanian (R-087, ADR 0036): the text
  under the label says the index in the INDEX row's words (`the average value of the master's hot
  pixels at places 5-20 per megapixel`) and that a warmer sensor gives a higher index, the
  partner's explanation says what is compared with what, the lights of a session without darks
  directly with the partner's darks, and **Analyse again**'s hint says it analyses the open folder
  again.

- A typical value is `average` (`medie`, `mediu`, `in medie`) wherever a text called it `usual`
  or `typical` (`obisnuit`) (ADR 0035), the figures unchanged: the `dark - bias` row (`the
  average pixel`), check 2's frame light row (`the average spread of the frames`), the index
  (`the average value of the hot pixels`), the dark current (`what a pixel gathers on average`),
  the darks' header for a FITS without a black (`the average value of frame 1`) and the Cadre
  tab's guide line (`1.5 times the average noise`).

- The Lights tab and the console's lights table speak plain words, in English and Romanian (R-086,
  ADR 0034), the figures unchanged: the columns `sigma`, `candidates`, `isolated`, `ampl.`, `q25`
  and `q75` are `noise`, `bright`, `single`, `hot`, `low` and `high` (`zgomot`, `luminosi`,
  `singuri`, `fierb.`, `jos`, `sus`), the hover hints and the table's note say what each column
  means, the chart is titled `EACH LIGHT AGAINST THE DARK`, its sentence says what each colour
  means to do with the dark, and its lines read `0.9 as it is`, `0.7 with -opt` and `0.4 cosmetic
  only` in either language.

- The Pixeli fierbinti tab's own texts speak plain words, in English and Romanian (R-085, ADR
  0033): the text under the rows says what they count and that check 5 never warns, the noise
  chart is titled `HOW THE NOISE FALLS WITH MORE FRAMES`, its sentence explains the dotted line
  (`4 times the frames, half the noise`), and a point's hint reads `1 frame: measured 1.426,
  expected 1.514`.

- The Harti tab's own texts speak plain words, in English and Romanian (R-084, ADR 0032): the lead
  says what the three maps show and how each map's contrast is set, the third map is titled
  `DARK MINUS BIAS`, and a missing map says why (`the frames are smaller than 32 px`; for the bias
  maps, `the session has no biases that can be used`).

- The Cadre tab and the console's frames table speak plain words, in English and Romanian (R-083,
  ADR 0031), the figures unchanged: the columns `sigma`, `rezid.` and `amplit.` are `zgomot`,
  `dif.` and `fierb.` (`noise`, `diff.`, `hot`), the note and the hover hints say what each column
  means, a frame to discard reads `de aruncat: pixelii fierbinti sunt la 0.826 din master, sub
  0.87, adica la peste 2.0 grade sub platou (1.10)`, and the charts' titles, sentences and lines
  lose the method's words. When no dark, bias, light or partner dark can be read, the message
  says `nu s-a putut citi` (`could not be read`). A left or right side reads `latura din stanga` /
  `latura din dreapta` in every text, and a frame whose hot pixels do not show gets its reason to
  discard in the table.

- The Rezumat tab and the console's report speak plain words, in English and Romanian (R-082,
  ADR 0030), the figures unchanged: the session card, the `=== DARK-URI` header (`frames read 20
  of 20`, `values from 150 (black) to 4095 ADU (white)`) and its rows, now `1 ADU-eq` and
  `value step` (`pasul valorilor`), checks 2 to 9 and INDEX. Checks 2 and 3 are now titled `2. LIGHT
  LEAKS AND DIFFERENCES ACROSS THE FRAME` and `3. THE CAMERA'S WARMING DURING THE SERIES`, whose
  row `amplitude` is `hot pixels`; checks 4 and 6 `4. THE NOISE OF EACH FRAME` and `6. FIXED
  PATTERN: WHAT IS THE SAME IN EVERY FRAME`; the rows of checks 4 to 8 say what a figure shows
  (`against the master`, `above the threshold`, `how hot`, `fixed banding`, `noise when
  averaged`), without the formulas; check 9, `9. HOW WELL THE DARK FITS THE LIGHTS` (the Lights
  tab's heading too), and INDEX say what a ratio means for the hot pixels (`in the lights the hot
  pixels are 1.30 times what they are in the dark`), what to do in Siril and the degrees in words
  (`7.5 degrees colder than the partner`). An error reads `+/-` instead of `±`.

- The ADU scale section, the window's `ADU scale explained` tab and the console's last block, is
  in plain words, in English and Romanian: each row and paragraph says what a figure means
  without the method's terms (no sigma, square root, quadrature, raw ADU or `e-`), the figures
  unchanged (R-080, R-081, ADR 0028). Three rows are renamed: `value step` (`pasul valorilor`),
  `noise on one dark` (`zgomot pe un dark`) and `what was found` (`ce s-a gasit`). Check 2's
  ratio to the lights stack reads the same way on the Summary tab.

- When LibRaw cannot be loaded, every raw's error names Microsoft's Visual C++ runtime, missing
  or too old, as the likely cause and gives Microsoft's link to its latest version, in English or
  Romanian, instead of saying where the DLLs must sit and how to publish them; the log says once
  which of the runtime's four DLLs Windows cannot load. The words and the code are LightsCheck's
  (R-110, ADR 0026).

- About lists the six components the program ships, the three libraries inside LibRaw's DLLs
  (libjpeg-turbo, Little CMS, zlib) apart from it, under LightsCheck's lead, which says how many
  there are and where their licence texts are: in the Astro Darkroom folder and in the source
  (R-120, ADR 0027). It is no taller than its screen: on a screen too low for it, such as a
  1080-line screen at 125 % or more, its cards scroll and the Close button stays in view (R-121).

- `README.md`, the user guide, and `THIRD-PARTY-NOTICES.md` are in English (ADR 0022). The
  README is rewritten against the current behaviour: the English labels and verdict words,
  examples cut from English runs on the real sessions, the latest measured timings, where to
  get the programs and what they need.

- The files `--out` and the window's `Scrie fisierele...` write are named in English in both
  languages: `darkscheck-report.txt` and `darkscheck-map-dark.png`, `-map-bias.png`,
  `-map-dark-minus-bias.png` (they were `darkscheck-raport.txt` and `darkscheck-harta-*.png`)
  (R-026, ADR 0021).

- The projects target .NET 8 instead of .NET 9, so the Astro Darkroom suite ships one runtime
  (ADR 0018).

- Check 3 judges the plateau of the series instead of the first frame against the last
  (R-023, ADR 0017). The hot-pixel amplitude of every frame is fitted on the minutes as an
  approach to a plateau; the rows `platou` and `de aruncat` give the plateau, the time
  constant, how far under the plateau the master sits and which frames are more than two
  degrees from it (to move to `aruncate` by hand, with the noise the master would gain), and the verdict is `ATENTIE` only when the series
  has not settled at its end or a frame leaves the `-opt` band of 0.7 .. 1.4 of the master; the
  warm-up at the start is stated, not flagged. The frames table gains the column `platou`
  (degrees from the plateau) and a note on every frame to discard, and the window's Cadre tab
  shows those rows, the note and the chart's plateau and bounds in the attention colour
  (R-025, R-071). The five real series now read `OK` where they read `ATENTIE` before.

- The label of a folder already renamed to a label (its name ends in `-idx` and digits) loses
  that suffix before the new one is appended, so `...-15.8celsius-idx67` reads `...-idx67`
  again, not `...-idx67-idx67`, and a changed index shows as a changed number (R-061, ADR 0013).

### Added

- The console and the window speak English or Romanian, chosen by `--lang en` or `--lang ro`
  (also `--lang=ro`), **English when `--lang` is missing**; a value other than `en` or `ro` is
  refused with `ERROR:` and exit 1 in the console and said in a message by the window. The
  figures are the same in both languages (R-090 to R-094, ADR 0021).

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
- The masters' noise beside every light found (R-024, R-029, R-080, ADR 0016): the scale
  section's row `zgomot in master` gives the dark master's per-pixel noise and the lights
  stack's (mean and median); check 2's `lumina constanta`, `lumina pe cadre`, its verdict and
  the scale's `gasit` state each light in raw ADU as a fraction of those noises; the window
  shows the noises and the light found in bold, through a per-row list of emphasised figures
  the console ignores.
- A help on the ADU scale, computed on the open set (R-078, R-080, ADR 0015): the tab
  `Explicatii scara ADU` and the section `EXPLICATII SCARA ADU` at the end of the report show
  the file's scale, the normalisation, the quantisation step, the read noise, the dark
  current, the checks' thresholds in raw ADU on this set, what check 2 found, the lights' sky
  and the stack's noise, the saturation, in ADU, ADU-eq and electrons (the D3300's published
  gain from photonstophotos.net, with its provenance), and four paragraphs on why a fraction
  of an ADU in the master matters.
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

### Fixed

- The console's lights table: from `sky` on, each column name ended one column before its figures;
  each now ends above them (R-086).

- The Romanian texts write `de` after a count of 20 and up, as Romanian grammar wants:
  `20 de cadre`, `443 de pixeli`, `32 de lights`, but `19 cadre` and `101 cadre`; figures with
  decimals and units are as before, and the English is unchanged (R-095, ADR 0029).

- About's lead said the program links two components written by others while it listed three;
  a test now holds the lead's count to the list (R-120).

- The content of the window's tabs is in the UI Automation tree: a screen reader such as
  Narrator, or a UI Automation tool, now reaches the checks and the buttons inside a tab, where
  it found only the eight tab headers before (R-079; ADR 0024, as AstroPhotoAnalyzer's
  ADR 0065).
- A FITS frame whose data is shorter than its header says (a copy cut mid-transfer) was
  listed with `Index was outside the bounds of the array.`; the reader now refuses it with
  `FITS: date trunchiate: ...` and both byte counts. Taken from FlatCheck's fix (R-012,
  ADR 0020).

### Internal

- NLog 6.2.2 is the third third-party component, with its `NLog.Targets.AtomicFile` package so
  two windows writing one log lose no line (BSD-3-Clause, R-103, ADR 0025), as FlatCheck and
  LightsCheck have it. The core logs at its seams: the two readers adapted from LightsCheck take
  LightsCheck's log hunk (its ADR 0029), the other copies of `Imaging/` none; of `Analysis/` only
  the two decoding loops log, and `Report/` never does, `DarkOutputs.WriteAll` telling its caller
  each file instead. `LogText.Figures` writes every figure of a result record by one rule, and
  every test runs every log line on its path.

- Every type and method of the code carries an XML documentation comment (the kit's ADR 0035),
  and every comment is English, the copied readers', `Dark.xaml`'s and two tests' included: the
  copies' comments are DarksCheck's own and describe what the files do here (ADR 0019). No code
  changed.
- `prototype/`: the Python scripts of the 2026-10-11 analysis of the Smida session of
  2026-08-12, frozen, and their report, figures and CSV files under
  `docs/validation/2026-08-12-smida/`; V-001 checks the inventory of that session and V-002
  the library of darks of 2026-10-10.
- The report is built as rows (`DarkReport.Sections`, `HeaderRows`, `FrameRows`, `LightRows`)
  that `Write` prints and the window binds; the console's text is unchanged. The normalisation
  is a record of the core (`Normalisation`), built by the run from the two settings of
  `DarkOptions`.
