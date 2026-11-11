# LightsCheck

A quality check for the light frames of an astrophotography night, made for a DSLR on a star
tracker and a Siril workflow (tested with a Nikon D3300 and a Samyang 135 mm on a Star Adventurer
GTi). It reads a Siril session folder, measures every frame and tells you which frames to leave
out of the stack and why, how often and how far you dithered, how the mount drifted, how good the
polar alignment was, and what to do differently next time. The frames are only read: nothing in
`lights/` is modified, moved or written.

There are two programs over one core, and they give the same figures: the console,
`lightscheck.exe`, and the window, `LightsCheck.Wpf.exe`. Both speak English, or Romanian with
`--lang ro`.

The figures are checked against two test nights (`docs/validation.md`): the Veil
Nebula on 2026-10-06, 87 frames at 135 mm, against the hand analysis the tool grew out of, and the
Milky Way at Smida on 2026-08-12, 32 frames at 20 mm.

## Getting it

The window is one of the tools of the Astro Darkroom suite, described on the front page of this
repository ([../README.md](../README.md)); opened from the suite's launcher, it starts in the
launcher's language. The console is not part of the suite: build it from this folder, as
"Building" below says.

To read camera raw files, both programs need **Microsoft's Visual C++ v14 runtime (x64)**, which
most computers already have. Windows lists it as "Microsoft Visual C++ v14 Redistributable (x64)",
or, when it is older, "Microsoft Visual C++ 2015-2022 Redistributable (x64)", "2015-2019", "2017"
or "2015"; the last two are too old, from before Visual Studio 2019 added one of the DLLs libraw
needs. Neither the suite nor the programs built here include it. Without it FITS frames are still
read, and every raw file gives this error, whose link downloads the latest version:

```
cannot load libraw: Microsoft's Visual C++ runtime is likely missing or too old, so camera raw files cannot be read until its latest version is installed (FITS frames are not affected): https://aka.ms/vc14/vc_redist.x64.exe
```

The log then names the runtime's DLLs that Windows could not load ("Logs" below).

## The session folder

Give it the folder of one night, laid out the way Siril and its scripts lay it out:

```
2026-10-06-3h\
    lights\      DSC_0001.NEF ... DSC_0087.NEF   <- required (or the frames directly in the folder)
    biases\      DSC_0128.NEF ...                <- optional
    darks\       DSC_0160.NEF ...                <- optional, only counted, never read
    flats\       ...                             <- not touched
    process\     ...                             <- not touched
    result.fit                                   <- optional, once plate-solved
```

- The lights are the frames of `lights\`. A folder without `lights\` but with frames of its own
  is taken as the lights; an empty `lights\` is an error, never a fall-back to the root of the
  session, where `result.fit` and the other results of the stacking sit.
- The frames are the camera raws (`.nef`, `.cr2`, `.cr3`, `.arw`, `.dng`, `.raf`, `.pef`) when the folder
  has any, otherwise its FITS files (`.fit`, `.fits`, `.fts`); the two kinds are never mixed. They
  are taken in the order of their file names.
- `biases\`, two frames or more, gives the read noise, and with it the verdict on the exposure.
- `darks\` is only counted, to say whether the session has darks to take the sensor's pattern
  out (DITHERING, `fixed pattern`).
- A FITS at the root of the folder that Siril has plate-solved, such as the stack `result.fit`,
  tells which image axis is RA, which way is east and which north, and the declination. Without
  one the RA axis is taken from the direction of the trails when at least two bad frames agree on
  it, assuming the tracking made them; the dither moves get no sense on the sky, and the polar
  alignment gets its lower bound at most.

## The console

```
lightscheck <session folder> [--out DIR] [--threads N] [--pixel UM] [--east left|right] [--north up|down] [--lang en|ro]
lightscheck <session folder> --inventory
lightscheck <session folder> --frame N
lightscheck --help
```

- No option: measures every frame and prints the report of the night (below).
- `--out DIR`: also writes the report, a CSV with every figure and the list of the frames to
  reject into DIR.
- `--threads N`: how many frames are measured in parallel; half the cores by default.
- `--pixel UM`: the pixel size in microns. Without it, the files' `XPIXSZ` when a FITS carries
  it; else the size the raw implies, from the 35 mm-equivalent focal length the camera writes
  and the mosaic's size (4.00 on a D3300 at 135 mm); else 3.92, the D3300's spec. The `scale`
  line of the report says which was used. With the focal length of the files it gives the scale
  in "/px.
- `--east left|right` and `--north up|down`: the SynScan arrows that move the mount east and
  north, for the `dithering` column (below); right and down by default. Their Romanian forms,
  `--est stanga|dreapta` and `--nord sus|jos`, work too, in either language.
- `--inventory`: the metadata of the frames only, without decoding them.
- `--frame N`: measures one frame, the N-th of `lights\` (1 = the first).
- `--lang en|ro`: the language of the texts, English by default.
- `-h`, `--help`: the usage and the licence notice.

Every run first prints `lightscheck <version>` and the copyright line. The exit code is `0` when
every frame was read and `1` on an error: a missing folder, a folder without frames, a wrong
option, or a frame that cannot be read, which is listed with its error while the others are still
analysed.

`--lang ro` (or `--lang=ro`), anywhere on the command line, gives every text in Romanian: the
usage, the report, the text in the files of `--out`, and in the window every label. `--lang en`,
or no `--lang`, gives English. An unknown value (`--lang fr`) stops the console with an English
`ERROR:` line and exit code `1`; the window says it in a message and goes on in English. The
figures are the same in both languages.

### The whole night

`lightscheck <session folder>` measures every frame, registers them all on the first and prints
the report; the progress goes to stderr. On the test computer, an Intel i7-4790K with the
default 4 threads, the 87 raws of 24 MP of the Veil night take about two minutes (123 s, measured
2026-10-26). The report's sections:

- **SUMMARY**: one row per question, each opening with its verdict: the focus, the exposure
  against the read noise of the biases, dew, the trailed frames, the dithering, the polar
  alignment, and how many frames to leave out of the stack and how much exposure stays.
- **FOCUS**: the stars' width (FWHM) on the good frames, at the start and at the end of the night
  and in three zones (centre, middle, edge), and the verdict.
- **EXPOSURE AND NOISE**: the sky background, the read noise of each channel (the noise the sensor
  adds to every frame), and on the first, the middle and the last frame the shot/read ratio of each
  channel: from 10 up the sky covers the read noise; under 10 the channel suffers from the read
  noise, and longer exposures would help.
- **DEW**: the light around the bright stars (the halo) and the star count at the start and at the
  end, and the verdict.
- **TRAILED FRAMES**: the bad frames (an elongation of 1.60 or more) and the marginal ones (1.35
  to 1.60), the direction of the trails and which image axis is RA. When the RA axis is known and
  a frame's stars agree on their direction (above 0.8, where 1 is all the same way), the reason of
  a trailed frame names the mount axis its trail falls on:
  - `trail on RA (-90 degrees)`: the tracking (periodic error, rate, backlash on RA);
  - `trail on DEC`: the polar alignment, wind or a touch on the mount;
  - `trail oblique`: both, or a knock.

  Without the axis it gives the angle alone: `trail at -90 degrees`.
- **DRIFT AND ROTATION**: the drift between frames in a row without dithering, the drift over
  one exposure and the walking-noise streak it draws, the field rotation, the polar alignment and
  the drift along RA (explained below).
- **DITHERING**: the pauses and the moves (a step of 5 px or more), how often (every how many
  frames), how far, on which axes and which way, and how close the frames are to each other (the
  nearest other frame, the different spots, the runs of frames in a row in the same spot); then:
  - `scale`: how far on the sky the mount must move the frame for 5 px and for 10 px, at the
    session's focal length;
  - `pattern in the stack`: how much of one frame's pattern stays after stacking, against 1
    divided by the number of frames, the ideal when no two frames share a spot;
  - `fixed pattern`: the sensor's fixed pattern, read from the frames laid one on another without
    aligning them, so that the stars move with the dithers and only the pattern stays: its
    strength on each channel, in ADU and as a share of one frame's noise, the hot pixels per
    million pixels, and whether the session has darks to take it out;
  - the verdict, for walking noise and for hot pixels, with its figures (below).
- **REJECTED**: each bad frame with its reason, its threshold and the axis of its trail, and each
  frame that could not be read. The marginal frames stay in, your choice; their list is in
  TRAILED FRAMES.
- **FRAME BY FRAME**: one row per frame: time, exposure, class, elongation, FWHM, stars,
  background, pause, step, the position against the first frame, `dithering`, `same spot` and
  the reasons (below).

### Drift, field rotation and polar alignment

The drift is measured on the pairs of consecutive frames with no pause and no move between them,
where nothing was pressed and the mount did not jump. With fewer than 3 such pairs, as when you
dither after every frame, every pair without a move of 5 px counts, and the row says
`approximate`, since the small dither presses enter the figure. From the drift come the drift
over one exposure, with the mount axis it runs along, and the walking-noise streak it draws over
the longest run of consecutive frames, 2 or more, that stayed within 10 px of the run's first.

The polar alignment is read from the drift along DEC and from the field rotation: a polar axis
off by an angle e makes the stars slide along DEC and the field rotate, in proportions that depend
on the declination alone. The DEC drift alone gives a lower bound of the error
(`the mount's axis is off the pole by at least 5.7'`). The field rotation, put down entirely to the alignment, gives the
upper bound (`by 4.5' to 6.7'`), once it is cleared of the dithers' slews: each move
in RA turns the frame a little, by its change of right ascension times the sine of the
declination, and on the Veil night that was most of the rotation the registration saw. The upper bound needs the declination,
from the plate solve at the root, and a session of an hour or more. The row also says what the
error costs: the drift the alignment can cause over one exposure, in pixels, and the elongation it
adds to a star of the session's FWHM. The verdict in SUMMARY weighs that drift against a third of
the FWHM: `OK` when the upper bound's drift stays under it, `WARNING` when even the lower bound's
reaches it, `MARGINAL` between; with the lower bound alone and under the third, no verdict.

`RA drift` says how much of the drift along RA the alignment can explain (the rotation times the
tangent of the declination); the rest is the tracking (the rate, the periodic error, flexure), not
the alignment. On the Veil night the RA drift of 4.4"/min owed at most 0.7"/min to the alignment,
and the alignment, between 4.5' and 6.7', cost at most 0.46 px over a 120 s exposure: it is not
what moved the frames.

The alignment cannot be measured with fewer than 3 pairs of frames without a pause and without a
move; a run of 10 or more frames without dithering would measure it. Without a plate solve, or
under an hour, it gets only its lower bound. Splitting the error into altitude and azimuth would
need the exact place and time, which the raws do not carry.

### Dithering and walking noise

Walking noise means fine streaks, all in the same direction, in the stacked image. The sensor's
fixed pattern makes them: the hot pixels and the faint marks the sensor always has in the same
place, what a dark takes out. When the stack aligns the frames on the stars, the pattern moves
with the drift from one frame to the next, and its copies line up into a streak, in the runs of
frames in a row that stayed in the same spot. The longer the focal length, the more pixels the
same drift and the same dither move make. On the Veil night, at 135 mm, the drift was 1.52 px
per exposure and drew a streak of 13.7 px over frames 61 to 70; at Smida, at 20 mm, it was
0.17 px per exposure, and a run of 12 frames in one spot drew 1.9 px. A short focal length
forgives; a long one asks for a dither after every frame, on both axes, each time in another
direction, at random. How often you move matters more than how far: 5 px are enough as long as
two neighbouring frames do not stay in the same spot, while a big jump every 2 frames leaves
pairs of frames in the same spot, in which the drift draws short streaks.

Darks take the pattern out before it gets to make streaks. You do not need to take the filter off
the lens: take the lens off with the filter on it, put the body cap on, cover the viewfinder's
eyepiece and shoot 20 frames at the same ISO, with the same exposure time and at the same
temperature, at the end of the session. The flats stay good. The window says the same under its
DITHERING card.

The verdict of DITHERING reads what reaches the stack, not how spread out the map looks: a
random dither inside a small area has to come back near earlier spots, and a return to within
10 px is not a return to the same pixels. For every run of frames in a row that stayed in the
same spot (within 10 px) it works out the streak the drift draws across it, and counts the
frames of the runs whose streak reaches 3 px, the ones whose streak would show:

- `INSUFFICIENT for walking noise` when those frames are 25 % or more of the aligned frames,
  too many for the rejection at stacking to drop their streak, with the longest streak in
  pixels, its axis and the share of frames;
- `NO walking noise` when they are fewer but the pattern adds up in place: the stack keeps more
  than one and a half times the ideal share of one frame's pattern (the `pattern in the stack`
  row), as it does without dithering at a short focal length;
- `OK for walking noise` otherwise. The text still lists the different spots, the frames alone
  and the moves on both axes, as information;
- `OK for hot pixels` while no more than 3 frames in a row sit within 3 px of one spot, else
  `WARNING on hot pixels`.

### The frame table

- `exposure` is how long the frame was exposed, in seconds. The session's usual exposure is
  the one most frames carry. A frame exposed under half of it is bad and leaves the stack: it
  brings almost no signal, and Siril would have to stretch its noise up to the others' level
  when it evens the frames out before stacking; its reason reads `exposure 0.2 s, under half
  the usual 120.2 s: too short to be worth stacking`. A frame with another exposure, longer or a
  little shorter, stays in with a note, `exposure 137.5 s instead of the usual 120.2 s (Siril
  evens it out when stacking)`, and when its sky jumps by as much as the exposure predicts, the
  jump's reason says so: `, which its exposure explains`.
- `dithering` is the mount's move behind each dither: on which axis, how many pixels, which way on
  the sky and which SynScan arrow, such as `RA 17 px ← W` or `RA 5 px ← W, DEC 29 px ↑ S`: the
  arrow pressed, then the sense on the sky (E or W, N or S). The sense comes from the plate-solved
  FITS at the root. Siril stores a FITS with its rows bottom-up against the raw, and the program
  allows for it; this was checked on the Veil night against plate solves of raw frames. A session
  of FITS lights, such as the `pp_light_*.fit` of Siril's `process\`, is read in the same order as
  the solve and gives the same senses. Without a solve the cell is `RA 17 px`.
- Which arrow moves the mount east and which north depends on the mount and the app. The
  defaults are those of a Star Adventurer GTi driven by the SynScan Pro app, tested on that
  pair with a target west of the meridian: `→` moves the mount east and `←` west, `↓` moves it
  north and `↑` south. East of the meridian the camera sits on the other side of the mount, and
  the DEC arrows may swap north and south; that side is not tested yet. To check a side, or
  another mount or app, press one arrow alone between two frames of a session with a
  plate-solved FITS at the root: the next frame's cell gives the sense on the sky, which comes
  from the solve, not from the arrows. Change them with `--east` and `--north`, or with the EAST
  and NORTH selectors of the window.
- `same spot` says which consecutive frame within 10 px of the run's first it is, out of how many,
  from which frame: `7/10 from 61` is the seventh of ten frames that stayed at the spot of frame
  61. Such runs make walking noise, but the frame stays good and in the stack: the cure is to
  dither more often next time, not to stack fewer frames.

The arrows are Unicode characters. The console window shows them, since the program switches it
to UTF-8 while it runs; output redirected to a file with `>` from Windows PowerShell 5.1 garbles
them, so for a file use `--out`, whose files are UTF-8.

### The files of `--out`

`--out DIR` writes three files into DIR, creating it when needed:

- `lightscheck-report.txt`: the report, as printed;
- `lightscheck-frames.csv`: every figure of every frame, among them the trail's axis
  (`trail_on`), the `same spot` run (`spot_run_index`, `spot_run_frames`, `spot_run_first`) and the
  dither move (`move_ra_px` and `move_dec_px`, in pixels, positive east and north, and
  `move_arrows`);
- `lightscheck-rejected.txt`: the names of the frames to reject, one per line, to take out of the
  sequence in Siril.

The names are the same in both languages; version 1.0.0 named them `lightscheck-raport.txt`,
`lightscheck-cadre.csv` and `lightscheck-respinse.txt`. The text in them follows the language of
the run, and the CSV's header row stays English. Give a folder outside `lights\` and `biases\`,
such as the session folder itself.

### Only the metadata: `--inventory`

It reads the metadata of the frames without decoding their pixels, in a second or two for the 87
raws of the Veil night:

```
=== SESSION: E:\Photografie\Astro\Veil\2026-10-06-3h ===
    total exposure     : 2 h 54 min 30 s
    lights             : lights\ (87 frames .NEF)
    biases             : biases\ (30 frames .NEF)
    darks              : (missing)
    camera             : Nikon D3300
    lens               : 135 mm f/2.8 (when shot), 135 mm f/2.0 lens, ID 74 (Nikon F mount)
    exposure           : 120.2 s at ISO 1600; 133.2 s at ISO 1600 (different exposures)
    interval           : from 2026-10-06 23:35:03 to 2026-10-07 02:57:49 (3 h 22 min); total exposure 2 h 54 min, from 87 frames (the camera was exposing 86 % of the time)

  FRAMES
     no  file                  time      exposure    ISO    f/    focal   pause
      1  DSC_0001.NEF          23:35:03     120.2   1600   2.8   135 mm       -
      2  DSC_0002.NEF          23:37:51     120.2   1600   2.8   135 mm    48 s
      3  DSC_0003.NEF          23:40:14     120.2   1600   2.8   135 mm    23 s
```

`total exposure` is the exposures of the frames that carry a time of capture added up, to the
second; `interval` gives the same total in minutes. `exposure` lists each exposure and ISO the frames use; that night frame 75
was exposed for 133.2 s. `pause` is the time between the end of the previous exposure and the start of this one:
with manual dithering, the pause in which you moved the mount. The time comes from the EXIF of a
raw (the camera's clock) and from the `DATE-OBS` of a FITS (UTC, as Siril writes it).

### One frame: `--frame N`

`lightscheck <session folder> --frame 4` decodes the 4th frame of `lights\` and measures it:

```
=== FRAME 4: DSC_0004.NEF ===
    size               : 6016 x 4016 px; scale 150 .. 4095 ADU (libraw (exact))
    CFA levels         : R 25 (saturated 63, min -3)  G1 120 (saturated 414, min 1)  G2 120 (saturated 416, min 13)  B 138 (saturated 404, min 11)  [ADU above black]
    green background   : median 120, centre 165, corners 76 .. 86 (tiles 71 .. 171); noise sigma 13.1
    stars              : 15132 detected, 2104 bright, 357 saturated; 398 measured
    FWHM               : 3.06 px median (p90 3.64); centre 3.15 (95), middle 3.08 (202), edge 2.90 (101)
    elongation         : 1.15 median (p90 1.39); 14 % above 1.3; angle 86 degrees, coherence 0.36; centre 1.11 (95), edge 1.14 (101)
    halo               : 0.019 (ring 5-10 px / core 3 px, over the 40 brightest stars)
    trails             : 0
    registration       : 600 stars kept
```

- `size`: the frame in pixels, and its scale in ADU from black to the white level, with where the
  scale comes from.
- `CFA levels`: the median of each Bayer position above black, how many of its pixels reach the
  top of the scale (bright stars; a nebula should not show up here), and the minimum (below 0 is
  noise, not clipping to black).
- `green background`: the sky background on green, from medians over tiles of 128 px; `centre`
  against `corners` shows the vignetting and the gradient; `noise sigma` is the robust deviation
  of a pixel from the background.
- `stars`: how many star-sized objects were detected at 5 sigma, how many peak above 50 sigma and
  how many reach saturation; `measured` are those whose shape was measured, the 400 brightest that
  stay under 80 % of the scale.
- `FWHM`: the width of the stars in pixels, which is the focus, over the whole frame and in three
  radial zones, with the stars of each in brackets; edges wider than the centre point to coma or a
  curved field.
- `elongation`: the ratio of the stars' axes (1 = round); `% above 1.3` is the share visibly
  elongated; `angle` is the direction of the long axis against the horizontal, and `coherence` how
  well the stars agree on it (near 1 = all the same way, so the tracking; small = no direction, so
  the optics or the noise). Above 0.8 the report of the night ties the angle to a mount axis
  (TRAILED FRAMES).
- `halo`: how much of a bright star's light sits in a ring of 5 to 10 px against its core of
  3 px; it grows with dew or haze.
- `trails`: satellite or plane trails, each with its length and its top-left corner.
- `registration`: the stars kept to register the frame on the others.

## The window

`LightsCheck.Wpf.exe [session folder] [--lang en|ro]`. Without a folder it asks for one at start.
The analysis starts by itself, the same as `lightscheck <folder>`, with a progress bar and a
**Stop** button, and the result fills six tabs:

- **Summary**: the report's sections, row by row, each verdict a coloured chip. The SESSION card
  opens with the total exposure, in bold and a little larger.
- **Frames**: one row per frame (time, class, elongation, FWHM, stars, background, pause, step,
  the position as `dX (DEC)` and `dY (RA)` when the RA axis is known, `dithering`, `same spot`,
  reasons). Select a frame and the cut-outs of its stars appear on the right, from the centre and
  the corners, on the green channel: there you see the trail the elongation reports.
- **Dithering**: the map of the positions against the first frame (a 10 px grid; the colour is the
  frame's class; a ring instead of a dot marks a frame in a run of 3 or more in the same spot), the
  step from the previous frame (yellow = a move of 5 px or more; hover over a bar for the frame,
  the step and the mount's move), the distance to the nearest other frame, and on the right the
  DITHERING card: its rows, its tables (how often, how far, direction, how close the frames are at
  each distance, the runs) and the explanation of walking noise.
- **Quality**: the stars' width (FWHM), how elongated they are (with the yellow 1.35 and red 1.6
  lines), the stars found and the sky background, frame by frame, each chart with a line on what
  moves it; above them, a line says that each point is a frame and that on the first two charts
  its colour is the frame's class.
- **Rejected**: the bad and the unreadable frames with their reasons and thresholds, under an
  explanation of the words (the elongation, the trail and its axis, and in bold what a trail on RA
  and one on DEC point to). The marginal frames stay your choice; their list is in TRAILED FRAMES,
  on the Summary tab.
- **Report**: the console's text, the same to the byte.

A screen reader such as Narrator reaches what each tab shows, not only the tabs' names.

The top bar holds the inputs, read when a folder is opened: `PIXEL (um)`, the pixel size, blank
by default, when the files' `XPIXSZ` or the raw's own figure decide, as for `--pixel`; `THREADS`, how many frames are measured in parallel
(empty = half the cores); and `EAST` and `NORTH`, the SynScan arrows that move the mount east and
north (`→` and `↓` by default), remembered between starts in
`%LOCALAPPDATA%\LightsCheck\settings.json`, the only setting the program keeps there (its log goes
there too when the folder beside the program cannot be written, see [Logs](#logs)).
**Write the files...** writes the three files of `--out` into a folder you choose; the session
folder is proposed. **About** (F1) shows the version, the licence and the third-party components;
on a screen too low for all of it, its cards scroll (wheel, arrows, Page Up/Down, Home, End) and
the Close button stays in view.

## Logs

Both programs keep a log of what they did, for the day something goes wrong. To report a problem,
attach the whole `log` folder: it holds what is needed to see what happened.

**Where it is.** In a folder named `log` beside the program: `log\lightscheck.log` beside
`lightscheck.exe`, `log\LightsCheck.Wpf.log` beside `LightsCheck.Wpf.exe`. When that folder cannot
be written (a program under Program Files, a read-only drive), the log goes to
`%LOCALAPPDATA%\LightsCheck\log` instead, and its first line says so. Past 1 MiB (1,048,576 bytes,
so a file ends one line past that) a file is left and the next one is started
(`lightscheck_01.log`, `lightscheck_02.log`, ...); the five most recent files are kept and the
older ones deleted, so the folder holds about 5 MiB per program at most, the log of some thirty
analyses of a night of 90 frames. Two windows open
at the same time write into the same file, each line marked with its process. Nothing is written
into the session folder.

**What is in it.** One line per event, numbers with a point: at the start the version, the command
line, the Windows and .NET versions and the folders; the session's folders and frame counts; every
file read, with its size, its dimensions and what its header or the camera says (camera, lens,
focal length, exposure, ISO, time); for each run its options, its outcome and the time each stage
took; for each frame its figures, and the class, the flags and the thresholds behind every
rejection; the dithering, the drift, the sensor's pattern, the read noise and the polar alignment
as the report's verdicts read them; every file written; in the window, the folder opened, each
run's end and the buttons pressed; when libraw cannot be loaded, once, which of the Visual C++
runtime's four DLLs Windows cannot load, or that the four load, so none is missing;
every error with its technical details, those the program works
around without a word included (not a run you stopped yourself, nor what the window drops while it
closes); at the end the exit code and the time taken. The log's own words are English whatever
`--lang` says; an error's or a refusal's message appears as you saw it, so in Romanian under
`--lang ro`. It holds the paths of your files and the figures measured on them, not their pixels.

**How much.** The environment variable `LIGHTSCHECK_LOG` sets the level: `off`, `fatal`, `error`,
`warn`, `info`, `debug` (the default) or `trace`, which adds every header card of each FITS frame
the analysis decodes (`--inventory` reads headers only, so it adds none); `off` writes no log and
creates no folder. In a command window, `set LIGHTSCHECK_LOG=trace` holds for the
programs started from that window; for the window started from a shortcut or from Astro Darkroom,
set it once with `setx LIGHTSCHECK_LOG trace` (or in Windows' environment variables) and start the
program, or Astro Darkroom, again.

## Building

Windows 10 or 11 (x64) and the .NET 9 SDK, 9.0.318 or a later 9.0.3xx (`global.json`); the
projects target .NET 8, which that SDK builds. From this folder:

```
dotnet build LightsCheck.sln -c Release
```

The full gate, the one every commit runs (format check, strict build, tests):

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

The two executables, each one self-contained file with the libraw DLLs inside, which runs without
.NET installed (raw files still need the Visual C++ runtime, as "Getting it" says):

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 1.0.0 -PublishOnly
```

writes `artifacts\1.0.0\console\lightscheck.exe` and `artifacts\1.0.0\wpf\LightsCheck.Wpf.exe`
and changes no tracked file; the `.pdb` and `.xml` files beside them are not needed to run them.
`-Version` only names the folder: the version the programs print is `<Version>` in
`Directory.Build.props`.

## Licence

LightsCheck is free software, under the GNU General Public License version 3 or, at your option,
any later version: see [LICENSE](LICENSE). Its raw and FITS readers are copied from FlatCheck.
What the programs ship from others is libraw with the three libraries its DLL calls
(libjpeg-turbo, Little CMS and zlib), libraw's C# wrapper, and NLog, which writes the log; the
About window lists them, each with its licence. The whole text of each licence is in
`THIRD-PARTY-NOTICES.txt` beside the programs in the Astro Darkroom suite folder, and in this
repository in [../AstroDarkroom/THIRD-PARTY-NOTICES.md](../AstroDarkroom/THIRD-PARTY-NOTICES.md);
why each is compatible with the GPLv3 is also in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md),
in Romanian. This software is based in part on the work of the Independent JPEG Group.

## For developers

The rules of the work are in `CLAUDE.md` and `.claude/rules/kit/process.md`; the behaviour, with
IDs, in `docs/requirements.md`; the decisions in `docs/decisions/`; how the code works, area by
area, in `docs/design/`; the figures measured on the real nights in `docs/validation.md`; the
history of the versions in `CHANGELOG.md`. The analysis the tool grew out of, the Python scripts
and the report on the Veil night of 2026-10-06 (in Romanian), is in `prototype/` and
`docs/validation/2026-10-06-veil/`. This file is the user documentation; the comments in the code
do not repeat it.
