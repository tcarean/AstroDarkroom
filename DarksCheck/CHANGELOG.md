# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- A Help button in the window's top bar, a question mark on a blue disc and the word, also on
  F1 (R-125, ADR 0061): it opens the window's manual in the suite's wiki, in the browser, at the
  section of the tab on screen, at "Opening a folder" before a result or while one is read, and
  at "When something goes wrong" on the card of a folder that cannot be analysed.

- The lights of another folder, so the question "do these darks fit that session's lights?" is
  asked darks first (R-055, R-122, ADR 0039): `--lights DIR` in the console, the card
  `Lights from another folder` on the window's Lights tab. DIR is a session, whose `lights\` is
  taken, or a folder of lights; those lights replace the open folder's own in the run, the
  session row `lights` names them as chosen, and the darks' index and label stay the open
  folder's. The window's choice holds for the open folder only: not kept in the settings,
  dropped when another folder is opened.

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

- F1 no longer opens About, which opens from its button alone; the window is at least 1180 px
  wide instead of 960, so the whole top bar shows with Help in both languages (R-125, ADR 0061).

- Check 9 says what fewer or more darks would give, and with lights judges the darks against
  the lights' stack (R-024, R-123, ADR 0051). A new row `with n frames` gives, for 5, 10, 15,
  ... frames to two rows past the frames read, the master's noise, how it compares with the
  master of now, with lights how much noisier their stack comes out than with an ideal dark
  master (counted without dithering, on the noise of a light against its neighbours) and a
  rating, `too few`, `few` or `enough`; a row `enough from` gives the frames needed. With
  lights the darks are enough when that stack is at most 10 % noisier than with an ideal dark
  master, where the verdict looked only at the file's value step, so a verdict can change both
  ways; it also says how many frames could be left out. Without lights the rule is the old
  one, and the verdict adds how many frames would do.
  In the window the table is drawn in check 9's card, with a dropdown above it for the limit,
  5 % or 10 %, which judges check 9 again at once. The row `noise when averaged` is now one
  sentence, the last measured point, and no longer a table.
- The checks are numbered in the order the Summary tab shows them, in the console too (R-025,
  ADR 0046): after the session's lines (check 1), 2 is how well the dark fits the lights, then
  3 light leaks, 4 the warming, 5 the noise, 6 hot pixels, 7 the fixed pattern, 8 dead and
  saturated pixels, 9 how many darks, then INDEX. The console prints them in that order; the
  tabs' headings and the hints that name a check say the new numbers. Old 2 .. 8 are new 3 .. 9
  and old 9 is new 2; the entries below already use the new numbers.
- On the Summary tab, the verdicts of checks 3 (light leaks) and 4 (warming) put each statement
  on a line of its own, with the console's words (R-071, ADR 0047).
- Choosing the lights on the Match with lights tab, or the partner on the Index tab, analyses the
  open folder again at once, and so does clearing the choice; the two **Analyse again** buttons
  are gone (R-073, R-122, ADR 0048).
- Check 4 reads the series against its end, not against a fitted plateau (R-023, ADR 0043):
  the end of the series is the average of its last 5 frames, the frames more than 2 degrees
  from it are the ones to discard, `REDO` when more than half are, `WARNING` when the master is
  more than 1 degree from the end or a frame leaves 0.7 .. 1.4 of the master. The `plateau` row
  is `end of series`, the frames table's column `plateau` is `end`, the window's chart has the
  end of the series as its line, and no text names a plateau any more. A series that was still
  moving at its last frame is no longer flagged for that alone: darks that moved half a degree
  in 70 minutes read `WARNING` at "1.4 degrees above the plateau" and now read `OK`, 0.4
  degrees from where they ended. This replaces the plateau of the two older entries on check 4
  further down. A series whose last frame shows no hot pixel is no longer `UNMEASURED` as a
  whole: its frames are judged like any others. An `OK` no longer names `-opt` (it read as
  advice to use it): it says that no frame is too far from the master, the hot pixels of
  every frame between 0.7 and 1.4 of the master's.
- Check 2 also weighs what the mismatch leaves (R-067, ADR 0044): a new row `what is left` says
  how big a trace a plain subtraction leaves at a hot pixel against the noise of one light, and
  at how many pixels it passes the noise and 3 times the noise. A lights / dark ratio between
  0.7 and 1.4 whose average trace is under the noise of a light now reads `OK: the dark can be
  subtracted as it is` instead of `subtract the dark with -opt`; the window's chart of the
  lights moves its two green lines and colours its points by that wider band. Ratios inside
  0.9 .. 1.1, the cosmetic-only and no-match verdicts and the partner's verdicts are unchanged.

- The window's Summary tab puts check 2, how well the dark fits the lights, right under the
  session card, then checks 3 to 9 and INDEX; its hot-pixel card keeps only the stability and
  the verdict (the Hot pixels tab shows the rest); the stability's four statements and the
  shared pixels' two stand one under the other, and the overlap of check 2 and the noise curve
  of check 9 are drawn as tables (R-071, ADR 0041).
- Check 2 names both indexes on rows of their own, `index darks` and `index lights`, before its
  verdict, in the console and the window (R-056, ADR 0041).
- Every light is measured: the frame cap (`--max-frames`, `MAX FRAMES`) bounds the darks and
  the biases alone, so a session of 87 lights reads all 87 where 44 were read before (R-050,
  ADR 0041).
- The window's fifth tab is `Match with lights` (`Potrivire cu lights`), no longer `Lights`.
- The partner of the Index tab holds for the open folder only, like the lights folder: not kept
  in `settings.json` between starts, dropped when another folder is opened (R-073, R-075,
  ADR 0041).

- The label of a session with lights and no darks of its own is the session's: its folder's name
  and the lights' index, what dark they are like, read through the partner's darks (R-066, ADR
  0039). The partner's index and label now stand once, on the `darks (partner)` row, where the
  partner's label is appended; before, the big label on the Index tab and the `label` row were
  the partner's. The text under the label says so for such a session.

- Check 4 says which way the camera moved and when the series is to be taken again (R-023, ADR
  0038): the `plateau` row opens with "the camera cooled the whole series" (or warmed), with the
  hot pixels of the first and the last frame, and the verdict is a red `REDO` (`REFA`) when more
  than half the frames are more than 2 degrees from the plateau, with the advice to let the
  camera sit for three time constants before a cooling series is taken again, or to take a
  warming one longer; no distance to the plateau is printed with a sign any more, a frame is
  "above" or "below" it, and a cooling series says it started warmer than the place it was in.
  The `hot pixels` row of check 4 is gone, its figures in the `plateau` row; the window's chart
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
  directly with the partner's darks.

- A typical value is `average` (`medie`, `mediu`, `in medie`) wherever a text called it `usual`
  or `typical` (`obisnuit`) (ADR 0035), the figures unchanged: the `dark - bias` row (`the
  average pixel`), check 3's frame light row (`the average spread of the frames`), the index
  (`the average value of the hot pixels`), the dark current (`what a pixel gathers on average`),
  the darks' header for a FITS without a black (`the average value of frame 1`) and the Cadre
  tab's guide line (`1.5 times the average noise`).

- `Usually` (`de obicei`) is `on average` (`in medie`) too wherever it stands for a typical value
  (ADR 0040), the figures unchanged: check 5's `against the master` row and its verdict (`1.5
  times the average noise`), check 6's `how hot` row, check 2's rows `not in the master` and
  `lights against dark`, two sentences of the ADU scale tab and the hint of the Lights tab's
  `noise` column. The Lights chart's `The first lights of the night usually sit lower` stays: it
  means most nights, not an average.

- The Lights tab and the console's lights table speak plain words, in English and Romanian (R-086,
  ADR 0034), the figures unchanged: the columns `sigma`, `candidates`, `isolated`, `ampl.`, `q25`
  and `q75` are `noise`, `bright`, `single`, `hot`, `low` and `high` (`zgomot`, `luminosi`,
  `singuri`, `fierb.`, `jos`, `sus`), the hover hints and the table's note say what each column
  means, the chart is titled `EACH LIGHT AGAINST THE DARK`, its sentence says what each colour
  means to do with the dark, and its lines read `0.9 as it is`, `0.7 with -opt` and `0.4 cosmetic
  only` in either language.

- The Pixeli fierbinti tab's own texts speak plain words, in English and Romanian (R-085, ADR
  0033): the text under the rows says what they count and that check 6 never warns, the noise
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
  `value step` (`pasul valorilor`), checks 2 to 9 and INDEX. Checks 3 and 4 are now titled `3. LIGHT
  LEAKS AND DIFFERENCES ACROSS THE FRAME` and `4. THE CAMERA'S WARMING DURING THE SERIES`, whose
  row `amplitude` is `hot pixels`; checks 5 and 7 `5. THE NOISE OF EACH FRAME` and `7. FIXED
  PATTERN: WHAT IS THE SAME IN EVERY FRAME`; the rows of checks 5 to 9 say what a figure shows
  (`against the master`, `above the threshold`, `how hot`, `fixed banding`, `noise when
  averaged`), without the formulas; check 2, `2. HOW WELL THE DARK FITS THE LIGHTS` (the Lights
  tab's heading too), and INDEX say what a ratio means for the hot pixels (`in the lights the hot
  pixels are 1.30 times what they are in the dark`), what to do in Siril and the degrees in words
  (`7.5 degrees colder than the partner`). An error reads `+/-` instead of `±`.

- The ADU scale section, the window's `ADU scale explained` tab and the console's last block, is
  in plain words, in English and Romanian: each row and paragraph says what a figure means
  without the method's terms (no sigma, square root, quadrature, raw ADU or `e-`), the figures
  unchanged (R-080, R-081, ADR 0028). Three rows are renamed: `value step` (`pasul valorilor`),
  `noise on one dark` (`zgomot pe un dark`) and `what was found` (`ce s-a gasit`). Check 3's
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

- Check 4 judges the plateau of the series instead of the first frame against the last
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

- Check 3 tells a light leak from the sensor's glow by the warming of the series (R-028,
  R-029, ADR 0012). The row `lumina pe cadre` fits every frame's mean on the hot pixels'
  amplitude ratio (Theil-Sen, three clipping passes) and names the frames that carry light
  over it (5 scales and 0.1 ADU-eq), which make the verdict `ATENTIE` with the advice to take
  them out of `darks\`; the row `glow / constant` splits the verdict region's excess into the
  part that grows with the warming and the constant part, and with biases into the readout
  offset the biases hold and what remains; the verdict's words follow the cause (glow, leak,
  readout offset, or undecidable without biases; the split needs the series to have warmed by
  0.1 of amplitude ratio, and shows the raw ADU beside the equivalent); check 7's `glow` row
  shows the glow part;
  `TABEL PE CADRE`, the Cadre tab and the darks CSV carry every frame's excess light, and the
  CSV the four corners' offsets. Measured on the owner's deliberate leak set of 2026-10-20,
  which the old check read as `OK`.
- The masters' noise beside every light found (R-024, R-029, R-080, ADR 0016): the scale
  section's row `zgomot in master` gives the dark master's per-pixel noise and the lights
  stack's (mean and median); check 3's `lumina constanta`, `lumina pe cadre`, its verdict and
  the scale's `gasit` state each light in raw ADU as a fraction of those noises; the window
  shows the noises and the light found in bold, through a per-row list of emphasised figures
  the console ignores.
- A help on the ADU scale, computed on the open set (R-078, R-080, ADR 0015): the tab
  `Explicatii scara ADU` and the section `EXPLICATII SCARA ADU` at the end of the report show
  the file's scale, the normalisation, the quantisation step, the read noise, the dark
  current, the checks' thresholds in raw ADU on this set, what check 3 found, the lights' sky
  and the stack's noise, the saturation, in ADU, ADU-eq and electrons (the D3300's published
  gain from photonstophotos.net, with its provenance), and four paragraphs on why a fraction
  of an ADU in the master matters.
- Check 3 also names the light that is in every frame (R-029, ADR 0014): with biases, the row
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
- Checks 3 to 9 on the darks and the biases (R-020 to R-041, ADR 0007): `darkscheck <folder>`
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
- Check 2, the index and the matching (R-050 to R-065, ADR 0008): the lights are decoded one
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
  banding charts), Harti (the three binned maps), Pixeli fierbinti (check 6 and the noise
  curve over its expected line), Lights (check 2, the ratio per light with the verdict bounds,
  the lights table), Index (the label with a copy button, the INDEX rows, the partner folder
  of `--against`), Raport (the console's text, identical whatever the Windows culture); the
  export writes the same files as `--out`. Four settings in the top bar, threads, frame cap,
  gain per ISO stop and degrees per doubling, plus the partner, remembered in
  `%LOCALAPPDATA%\DarksCheck\settings.json`; the gain and the degrees reach the core as the
  normalisation's settings (the console keeps the defaults, 2 and 6). Dark theme, dark title
  bar, "Despre" window and its own icon, drawn by `Icon/make_icon.py`.

- A session with lights and no darks (R-002 refined, R-054, R-077, ADR 0011): opened with
  `--against DIR` or the window's partner, the partner's darks stand in for the session's,
  taking checks 3 to 9, check 2 against the session's lights and INDEX; the report says whose
  darks they are and drops the rows that would compare the partner with itself. Without a
  partner the folder is refused with a sentence naming what to give, and the window's problem
  card offers `Alege partener...`; `--inventory` lists such a folder with `darks: (lipsa)`.
  V-007 runs the Veil lights of 2026-10-06 against the 2026-10-10 library.

### Removed

- The `skipped lights` row of check 2 and the `skipped` column of `darkscheck-lights.csv`: no
  light is skipped any more (ADR 0041).

### Fixed

- `--out`, and the window's `Write the files...`, are refused by the folder, not by how it was
  typed. A library opened as `<name>\darks` could be given `--out <name>\reports`, and lights
  given as `<session>\lights` allowed `--out <session>\reports`, though both are refused when
  `<name>` or `<session>` itself is given; a partner given as `<name>\darks` likewise. And
  another name of the session's folder passed: a `\\?\` prefix, a junction or a link, a
  `subst` drive, a short 8.3 name. Now the folder around a `darks` or `lights` folder is kept
  clear too, and two paths that Windows says are one folder are one folder (R-026, ADR 0069;
  CR-019 of the code review).

- The master's noise of check 9 is worked out from the noise between every two neighbouring
  darks, the middle value taken, no longer from the first two darks alone. One odd first frame
  moved the figure whole, and on a series in which the camera warms the first two frames are
  the quietest: on five of the six test series the figure was 3 to 12 % too small. The master's
  noise, the table `with n frames`, the frames needed and check 5's noise added during the
  exposure follow; a series that warms now needs a few more darks (39 became 49 on a warm
  library against a long night of lights). The row `noise after averaging` keeps its points
  (R-024, ADR 0068; CR-018 of the code review).

- `--lights DIR`, and the lights folder chosen in the window, no longer take the files at the
  root of a session: when DIR has a session subfolder (`lights`, `darks`, `biases` or `flats`)
  its lights come from `DIR\lights` alone, and a session whose `lights\` holds no frame is
  refused with `the chosen lights: no light in DIR\lights`. Until now the stacked `result.fit`
  at the root was measured as the one light. A folder with the lights directly in it works as
  before (R-055, ADR 0067; CR-017 of the code review).

- A frame whose black or white is not the first frame's is left out and listed, like a frame of
  another size (`different black and white: 150 and 4095 instead of 600 and 16383`; `different
  white: 255 instead of 65535` for a FITS). Until now 12-bit files among 14-bit ones of the same
  size, ISO and exposure were measured against the first frame's black and white. A black that
  the camera measures in each frame, and that moves by a few ADU, stays within the limit of 1 %
  of the white. A FITS with decimals on 0 .. 1 that holds a value just over 1 is no longer read
  as a file in ADU, which turned the whole frame to zeros: such a file is in ADU only when more
  than 0.1 % of its values are over 1 (R-020, ADR 0066; CR-016 of the code review).

- The `+/-` beside each side's constant light in check 3 is worked out from the line the value
  itself comes from. When the warming of the series rested on one cold frame the error shown
  was several times too small (about 0.04 where the value really scattered by 0.28), and a
  side without light passed for light in about one series in six. The values of the sides do
  not change; on an ordinary series the error grows a little (R-029, ADR 0065; CR-015 of the
  code review).

- Check 3 names a block of lit frames that takes up to nearly half of the series: when about a
  third of the darks, at one end of the warming, carried light (the lamp came on for the last
  eleven of thirty), the fit tilted through them, no frame was named and the check could read
  `OK`. A second fit, from a line that holds while fewer than half of the frames are lit, is
  now taken when it finds such a block; on every other series the figures are the same as
  before (R-028, ADR 0064; CR-014 of the code review).

- The session lines say when the file names are not in the order the frames were taken: a
  `WARNING` under the darks' interval, and one under the lights', names the first two files out
  of order. The frames are read in the order of their names, so on a series whose counter
  passed from `DSC_9999` to `DSC_0001`, or whose names have no leading zeros, the pauses, the
  warming of check 4 and its frames to discard were read on a scrambled series with no sign of
  it. The order itself is not changed (R-126, ADR 0063; CR-013 of the code review).

- A stray frame listed first no longer leaves the whole series out: with one test frame of 1 s
  before twenty darks of 120 s, check 1 read `OK` on the twenty while the analysis kept the one
  and dropped the twenty as `settings differ from the first frame`. The analysis now takes the
  setting most frames have, the one check 1 names, for the darks, the biases and a partner; the
  frame left out reads `other settings than most frames: ISO 400, 1 s` (R-020, ADR 0062;
  CR-012 of the code review).

- FITS darks beside biases of another size (another camera or crop) are no longer measured
  against those biases' black level: the biases were reported as left out, yet with darks at a
  level of 200 and biases at 1000 no hot pixel was counted and every pixel read as dead, under
  a header that said `the black taken from the biases`. The black is now the darks' own, as
  without biases (R-020, R-041; CR-011 of the code review).

- A file whose name starts with a dot, or that is hidden, is no longer taken for a frame: the
  `._DSC_0034.NEF` a Mac leaves beside each file of a card were listed as darks that could not
  be read and, over the frame cap, were read in the place of real darks (R-002; CR-001 of the
  code review).

- A set that does not say its ISO or its exposure (a FITS without `ISOSPEED`) was compared at
  factor 1 with a set that says its own: lights at ISO 1600 read a quarter of such darks at the
  same temperature, and so did darks against such a partner. The two are now compared as made
  at the same settings, as a light without an ISO already was, and an `ATENTIE` line says so in
  check 2 or under `INDEX`; the `INDEX` row of such a set no longer says it was worked out for
  ISO 400 and 120 s and gives its figure in `ADU`, as the partner's row does for a partner with
  unknown settings, and the window's text under the label says the index is then in the file's
  ADU and compares only with sets made at the same settings (R-052, R-060, R-063, R-087,
  ADR 0053; CR-002 of the code review).

- The noise of the lights' stack in the scale section, and the rows of check 3 that weigh a
  light found in the darks against it, added the lights' noise in the lights' ADU to the dark
  master's in the darks' ADU: with lights at twice the darks' ISO the lights' share came out
  double. The lights' sky and noise are now brought to the darks' settings first, as check 9
  brings them, and the `sky` row and the paragraph under the table say so when the lights are
  at other settings than the darks (R-080; CR-003 of the code review). With lights at the darks' settings nothing changes.

- Lights that do not show the dark's hot pixels (in-camera noise reduction, another camera,
  rotated frames) got an index all the same, `idx-0` or `idx0`, beside a verdict that says the
  hot pixels do not show, and a session with lights and no darks was labelled with it. Such
  lights now have no index: the `index lights` and `lights` rows are left out and the label
  reads `-idx-` (R-064, R-066; CR-004 of the code review).

- Darks whose level sits above the hot-pixel threshold in every pixel (a warm sensor, or
  biases with a lower pedestal) read a master noise of 0.00 ADU, and check 9 said `OK` on it:
  the noise was read only on the pixels under the threshold above the black, and there were
  none. The quiet pixels are now chosen against the master's own level in the central crop, so
  the noise is measured on such a set too, and is no longer skewed when only part of the pixels
  pass the threshold (R-024, ADR 0054; CR-005 of the code review).

- Lights at another ISO or exposure than the darks no longer read `OK: the dark can be
  subtracted as it is`: the ratio compares the two with both brought to one setting, so it said
  1.00 at the same temperature while in the files each hot pixel was, say, twice as high in the
  light. The verdict now says not to subtract the dark as it is, names both settings and the
  ratio in the files, and advises new darks at the lights' settings first, then `-opt` or the
  cosmetic correction; the row `what is left` is worked out on the files; a partner made at
  other settings gets the same verdict. In the window check 1's `match` and these verdicts are
  drawn large and in red (R-052, R-063, R-067, R-124, ADR 0055; CR-006 of the code review). With
  lights at the darks' settings nothing changes.

- Without lights, check 9 no longer asks for a master noise under one step of the file's
  values: that made the answer turn on the camera, ten darks on one whose values go 2 by 2 and
  over a hundred on one whose values go 1 by 1, for the same noise. A folder opened without
  lights is now judged on the minimum of ten darks alone; from ten on the verdict is `OK` and
  says that whether they are enough shows only against the lights (`--lights`), and the table's
  rows from ten frames up carry no rating (R-024, ADR 0056; CR-007 of the code review). With
  lights nothing changes.

- A folder with more darks or biases than the frame cap is read up to the cap: one file in k
  was read, so 61 darks under the cap of 60 gave a master of 31, and 119 one of 60. Exactly as
  many files as the cap are now read, from the first to the last at even distances, and a
  skipped frame says so (R-027, ADR 0057; CR-008 of the code review). Up to the cap nothing
  changes.

- Check 1's `match` no longer reads `UNMEASURED` when the darks and the lights are known to
  differ: frames that give the exposure and no ISO (a capture program that writes the gain),
  darks of 300 s against lights of 120 s, now read `WARNING`, naming the two exposures and
  saying that the frames do not state the ISO; the same for ISOs that differ without an
  exposure. `UNMEASURED` stays for a setting not given with nothing known to differ (R-007,
  ADR 0058; CR-009 of the code review).

- Biases at another ISO than the darks are no longer used: nothing compared the two, so their
  read noise stood under the darks' noise in check 5, their map was subtracted from the darks'
  in check 3, and FITS darks took their black from them. Such biases are now left out of every
  figure, with a `WARNING` line that names the two ISOs, and the run exits 1, as for biases of
  another size (R-041, ADR 0059; CR-010 of the code review).

- Frames that state the exposure and no ISO (a capture program that writes the gain) are no
  longer compared as if made at the same settings: darks of 300 s against lights of 120 s were
  judged on the files' ratio as it stood, which could read `OK: the dark can be subtracted as
  it is` or a temperature difference that was the exposure's. Two sets are now compared on the
  settings both state; a setting one does not state is taken from the other, and one neither
  states is taken as the same, each with a `WARNING` line. Such a pair is judged as any pair at
  other settings, lights against darks and darks against a partner, and the partner's row
  shows the settings the partner states (R-052, R-063, ADR 0060; CR-076 of the code review).
  Files that state both settings read as before.

- With darks that state no ISO, lights at one ISO and a partner at another were read as at the
  same settings, so lights as high in the files as the partner read 1.00 and `OK: the partner
  can be subtracted as it is`. The lights are now compared with the partner on the settings
  those two state, a missing one taken from the darks: at twice the partner's ISO the lights
  read 0.50 of it (R-064, ADR 0060; CR-077 of the code review). Files that state both settings
  read as before.

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
