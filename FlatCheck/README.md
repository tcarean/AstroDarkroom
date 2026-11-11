# FlatCheck

Quantitative diagnostic for astrophotography flat frames.

FlatCheck measures the flats of a photographic lens on a camera, stacked with
[Siril](https://siril.org), and answers three questions, each with a figure and the threshold
behind it:

- Is the master flat radially symmetric, and does its vignetting match the lens's published
  profile? (structure, the default mode)
- Does the flat correct a real light? (`--light`)
- Is a raw flat exposed right? (`--expo`)

Two more modes give no verdict. `--histeq` shows a frame the way Siril's "Histogram" display
does, where any unevenness comes out at maximum contrast. `--synthetic` writes a radial flat from the lens's published
vignetting profile, and `--against` compares two flats, for example the real one with that
synthetic one.

There are two applications over one core library:

- `flatcheck.exe`, a console: one file, one mode, one report per run;
- `FlatCheck.Wpf.exe`, a window: it opens a Siril session folder and runs the same five modes in
  six tabs.

The window is not a second implementation. Both applications call the same code, so their
figures are the same by construction, and the report the window saves is byte for byte the
console's. The window's manual, tab by tab, is in the suite's wiki:
<https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck>.

Both speak English by default, or Romanian with `--lang ro` (also `--lang=ro`), anywhere on the
command line. The language changes the words only: the figures, the verdicts behind them, the
exit codes and the names of the files written are the same in both. The output quoted in this
file is the English one.

FlatCheck measures and reports. It never changes a file you give it, and it never decides what a
shape on a map is. The limits come from tests with one APS-C camera and three lenses, from
wide-angle to telephoto. Raws of other cameras are read too, through LibRaw.

## Getting it

The window is one of the tools of Astro Darkroom: the repository's front page (the `README.md` at
the root of the repository) says how to get the suite, prebuilt or built from a checkout. The
console is not part of the suite; build it as below.

**Both applications from a checkout** need Windows 10 or 11 (x64), Git and the .NET 9 SDK,
version 9.0.318 or a later 9.0.3xx patch (`global.json` asks for it; it builds the projects,
which target .NET 8). From the `FlatCheck` folder:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version X.Y.Z -PublishOnly
```

`-PublishOnly` publishes and does nothing else: no checks, no change to any tracked file. Give as
`X.Y.Z` the `<Version>` in `Directory.Build.props`; it names the output folder:

- `artifacts/X.Y.Z/console/flatcheck.exe`, self-contained, a single file;
- `artifacts/X.Y.Z/wpf/FlatCheck.Wpf.exe`, with its files.

Each has a `lensfun/` folder beside it: copy it along with the executable. Without it everything
works, but the list of lens profiles holds only Adobe's, and says which folder it looked in. A
folder that is there but cannot be read (a permission) counts the same, and the list says so:
"the folder could not be read"; the diagnostic log has the reason, and the window looks for the
profiles only once while it runs, so fix the permission and restart it. The two `dotnet publish`
commands the script runs are under "Development".

**What FlatCheck works with:**

- **Siril 1.4.4 or newer.** FlatCheck reads what Siril's preprocessing scripts write, and
  `--histeq` mirrors Siril 1.4.4's display. The window's **Run in Siril** button starts your own
  `siril-cli.exe`.
- **Adobe's lens profiles, optional.** The `.lcp` files that Adobe DNG Converter (free), Camera
  Raw or Lightroom install. Without them, the lens list holds the lensfun profiles that ship with
  FlatCheck. See mode 5.
- **Microsoft's Visual C++ v14 runtime (x64), to read camera raw files.** Most computers have it;
  Windows lists it as "Microsoft Visual C++ v14 Redistributable (x64)", or "2015-2022" on older
  installations. LibRaw, which reads the raw files, needs it, and neither FlatCheck nor Astro
  Darkroom ships it. Without it, or with one too old (the 2015 and 2017 packages lack a file
  LibRaw needs), FITS files are read as usual and a raw file is refused with a message that
  gives Microsoft's link to its latest version,
  <https://aka.ms/vc14/vc_redist.x64.exe>.

## Quick start

The window, on a Siril session folder (the one with `flats/`, `lights/` and `process/` or
`masters/`):

```bash
FlatCheck.Wpf.exe "D:\Astro\MySessionFolder"
```

It runs the structure test on the master flat and the end-to-end test on the calibrated lights by
itself. The console answers one question per run:

```bash
flatcheck process/pp_flat_stacked.fit                           # the structure of the master
flatcheck process/pp_flat_stacked.fit --raw flats/DSC_0001.NEF  # ... and its vignetting against the lens profile
flatcheck process/ --light                                      # does the flat correct the lights
flatcheck flats/DSC_0060.NEF --expo                             # is a raw flat exposed right
```

Every run prints a report and exits with a code that follows its verdict (see "Exit codes"); most
modes also write a map, `flatcheck.png` unless `-o` names another file. The map, the pictures and
every position the report prints, the `(x, y)` pairs and the corner names, show the frame the way
Siril shows it, which is also the way the camera shows a raw: `(0, 0)` is the top-left corner of
that view. A FITS is read with the file's first row at the bottom whatever `ROWORDER` says,
exactly as Siril reads it; a raw as it comes from the camera.

## Which file to give it

The most likely cause of a wrong result is not in the code; it is the file chosen.

| mode | wants |
|---|---|
| structure (default) | the **calibrated** master flat: `pp_flat_stacked.fit`, in `process/` or in `masters/` depending on the Siril script |
| `--light` | a light **calibrated with the flat**, **without background extraction**, **not registered**; better, the folder of them |
| `--expo` | a **single raw, uncalibrated** flat, preferably the camera's own raw file (`.NEF`, `.CR2`, `.ARW`, ...) |

Siril 1.4 writes the master as **float**, divided by 65535, so the `median / max ADU` line reads
like `0.0143 / 0.0190 (normalised float 0..1)`. The file is not empty: every metric is a ratio,
so the scale does not matter.

Siril's `pp_` prefix means "preprocessed", that is calibrated. An uncalibrated stack still holds a
pedestal of ~150-600 ADU, which flattens the profile and under-reports the vignetting by ~1 %,
and **every test will still say all is well**, because the profile stays radially symmetric; only
its amplitude is wrong. It cannot be detected, only avoided.

Accepted formats: FITS (`.fit`, `.fits`, `.fts`; BITPIX 8, 16, 32, -32 and -64; mono, or a
colour cube, which is averaged over its planes) and camera raws (`.nef`, `.cr2`, `.cr3`, `.arw`,
`.dng`, `.raf`, `.pef`), read undebayered through LibRaw, with the black and white levels the
camera wrote. On a raw, `--expo` uses those exact levels instead of guessing them from the
frame's maximum.

**ISO does not matter.** Change nothing for ISO. Every metric is a ratio
(`log2(centre / corner)`, `(max - min) / mean`, `image / model`) and ISO is a scale factor, so it
cancels out of all of them.

## The idea

For a photographic lens with no optical train behind it, a *correct* flat is by construction a
**purely radial** function centred on the optical axis. Vignetting is rotationally symmetric, and
dust at f/2 is below the threshold (measured: the visible dust shadow on a test master at f/2.8
is 0.31 %, against a threshold of 2 %).

So any deviation from radial symmetry **is not vignetting; it is error**: an uneven light source,
a light leak, a tilted camera, ambient light, a diffusing sheet too close to the lens.

A good flat does not mean "it looks flat". It means "its deviation from radial symmetry is below
the noise". That is a figure, and it can be measured.

The radial model and the asymmetry are computed around **the centre of the sensor** (the frame's
geometric centre, so a frame and its mirror image give the same figures), not around
the fitted peak of the illumination: fitting the centre would absorb exactly the linear gradient
being looked for, and the test would cancel itself. The radial profile is drawn after a gradient
and the even non-radial part (such as a saddle of the sky) are taken out, so neither can leave
rings in the model that the flat does not have.

## The five modes

### 1. Structure (default)

```bash
flatcheck process/pp_flat_stacked.fit     # or masters/pp_flat_stacked.fit, depending on the Siril script
```

Runs on the final master. It reports three figures as information, with no verdict (under
`INFORMATIVE (not tests)`), then five tests of deviation from radial symmetry, and writes a
residual map.

The information:

- **vignetting to corner**: the depth of the vignetting, in stops, `log2(centre / corner)`;
- **illum. centre offset**: how far the illumination peak sits from the sensor's centre. It is
  not a test, and not on the scale below: see "The residual scale";
- **local spots (dust etc.)**: the deepest compact spot; see below.

The five tests:

| test | what it measures |
|---|---|
| spread of the 4 corners | how much the four corners (5 % boxes) differ from each other; on a correct flat they would be equally bright |
| max azimuthal asymmetry | how much the 12 slices of the same ring around the centre differ from each other, on the most uneven ring; measured only in the circle that fits whole in the frame |
| residual (radial model) | how much of the frame does not look like a usual vignetting, symmetric about the centre, over the whole frame, corners included: **the most important** |
| tilt (gradient) | how much brighter one edge of the frame is than the opposite one; it also catches a lens whose axis is off-centre |
| worst spot (one tile) | the small square that stands out most from the others: a small compact defect, which the residual sets aside as an extreme |

All five share one scale, and the verdict is the worst of them (see "The residual scale").

#### Dust: for the session or for the library

The informative figure **local spots** is the deepest compact spot left after the large-scale
structure is subtracted, with its position in the analysis raster. A gradient does not enter it;
a dust shadow, a ring of condensation or a small leak enters whole.

**The sign is the diagnosis**, and the report writes it in words beside the figure:

| sign | means | typical causes |
|---|---|---|
| negative, `(darker)` | the spot is darker than what surrounds it | dust on the sensor or on a filter, condensation, an obstruction |
| positive, `(brighter)` | the spot is brighter | a light leak, a reflection |

Above the 2 % threshold the report prints a **warning**, not a verdict, because the answer
depends on what the flat is for: a spot fixed to the camera is *right* for the session whose
lights have the same spot, and *wrong* for a library flat, since by its next use the dust will
have moved. Below the threshold the figure is information only. Look at its position on the map
before you decide.

Worth knowing before you act on it: the figure finds what is *compact*, but it also catches the
edge of a large structure that is steeper than the window the local background is taken from.
On two test masters the extreme was not a round spot: a vertical stripe along the left edge
(-0.44 %) on one, the top-right corner (+0.36 %) on the other. Both stay at a fifth of the
threshold, so nothing lit up; but if you ever see the warning, check first whether the shape on
the map is round. Only round is dust.

#### What structure cannot see

A spot that is **round and centred exactly on the axis** (a panel too close, the backlight of a
monitor the lens was centred on) is absorbed whole by the radial model, exactly like vignetting:
measured, a 10 % spot in the centre leaves every figure under 0.1 % and the map empty. Its only
trace is the vignetting figure, which grows. It shows only when it is slightly off-centre, and
then as a crescent, not a disc. No rotation removes it; the counter-test is distance: one set of
flats at 30 cm, one at 60.

What this mode gives is an **upper bound** of the flat's error, not the error (see
"The residual scale"). It is still the test to start with, because it is the only one that needs
nothing more to be photographed.

#### The sixth test: the vignetting against the lens's profile

The five tests measure deviations from radial symmetry, so they are **blind to a radial
excess**. In the tests, a master shot on a tablet (20 mm f/2.8, four orientations 90 degrees
apart) came out PERFECT on all five with 1.26 stops of vignetting, where the two profile
databases give 0.66 to 0.91 for that lens. The excess was the panel's angular fall-off, made
radial by the rotation; more rotations do not help. The tool's only outside reference is **the
lens's published profile** (Adobe's on the machine, lensfun's beside the program; see mode 5),
and this is the comparison:

```bash
flatcheck process/pp_flat_stacked.fit --raw flats/DSC_0001.NEF            # the lens and the aperture from the raw
flatcheck process/pp_flat_stacked.fit --lens "Tokina 11-20" --fstop 2.8   # or by name
```

The candidate lenses come from what the raw says about its lens: its name as the camera wrote it,
matched letter for letter against the profiles' names, and the lens ID (Nikon's on a Nikon, the
maker's on Canon, Sony and Pentax). The name's matches are the candidates when there are any, the
ID's otherwise, narrowed by the focal range and the aperture the raw gives and by the camera's
mount. With `--lens`, the candidates are the lenses that match every word of the text, as in
mode 5. For each candidate, the profile nearest in focal length and aperture is built as a raster
at the analysis size, and its vignetting is measured with **exactly the master's operator**: the
median of the 5 % corner boxes against the centre.

The difference in stops, master minus profile, goes through the same arithmetic as `--against`:
`2^|D| - 1` is the error it leaves in the corner of a light calibrated with the master, read on the
residual scale, so under 0.03 stops PERFECT, under 0.14 GOOD, under 0.26 ACCEPTABLE, above that
REDO. **When the raw's lens ID or name found the lens and Adobe has a file for it, the Adobe
profile gives the verdict**; the lensfun rows stay in the list, for comparison. Adobe measures each
lens itself; lensfun's profiles come from volunteers, each with their own camera and diffuser: on
one test lens both lensfun entries came out deeper than any panel flat taken with it allows, so
taking the nearest profile let a panel's own falloff pass as the lens's. When Adobe has the same
lens under two names with the same figures (a lens and its re-release in another housing, for
example), the file whose own lens ID is the raw's gives the verdict. When
the lenses came by the focal range alone (a manual lens, below), with `--lens` (a search, which may
match lenses of several makers), when you chose your lens, or when no Adobe file is among them,
**the nearest profile gives the verdict**, not the worst one: the two databases differ from each
other by ~0.2 stops on the same lens, and a master that matches one of them is not wrong. The run's
verdict is the worse of the shape (the five tests) and the depth, and the exit code follows it.

**Two scales for one quantity.** The error in the corner of the calibrated light is exactly what
`--light` measures too, directly in the light, with its thresholds of 2 % OK and 5 % PROBLEM.
Here the same error sits on the residual scale, where 10 % is still GOOD, because the reference
is another one: the published profile, and the two databases differ by 0.2 stops, that is 15 %
in the corner, three times `--light`'s PROBLEM line. A verdict finer than its reference would be
invented precision. So the report shows both: under the depth scale it prints the corner error,
what `--light` would read from it, and the reference's own uncertainty. At an excess of 0.10
stops, the case of paper sheets on a tablet, that is 7 % in the corner: GOOD on this scale and
PROBLEM on `--light`'s. Read both before you decide to reshoot, and remember that `--light` itself
refuses a field wider than 40 degrees.

**The sign is the diagnosis.** Plus means the master falls toward the corner more than the
model: the source falls off with the angle, a panel too close or without a real diffuser (matte
sheets do not diffuse). Minus means the source fills the corners: the dome of the sky, ambient
light, a reflection in the lens hood. More rotations do not help, because the excess turns with
the camera or is symmetrised by the rotation.

Measured on test masters, with `--raw` and the first raw of `flats/`:

| master | vignetting (stops) | profile that decides | difference | depth verdict |
|---|---|---|---|---|
| 20 mm f/2.8, tablet + 7 matte covers | 1.26 | Adobe 0.66 | +0.59 | REDO |
| 20 mm f/2.8, tablet + 15 covers | 1.24 | Adobe 0.66 | +0.58 | REDO |
| 20 mm f/2.8, tablet + 3 sheets of paper | 0.81 | Adobe 0.66 | +0.15 | ACCEPTABLE |
| 20 mm f/2.8, tablet + 4 sheets of paper | 0.80 | Adobe 0.66 | +0.14 | ACCEPTABLE |
| 20 mm f/2.8, 3 sheets, rotated in place | 0.76 | Adobe 0.66 | +0.10 | GOOD |
| 20 mm f/2.8, sky, no diffuser | 0.27 | Adobe 0.66 | -0.39 | REDO |
| 20 mm f/2.8, monitor, diffuser close | 0.85 | Adobe 0.66 | +0.19 | ACCEPTABLE |
| 20 mm f/2.8, on the monitor | 0.78 | Adobe 0.66 | +0.11 | GOOD |
| 35 mm f/2.8, sky (ISO 400 / 800) | 0.63 / 0.62 | Adobe 0.52 | +0.11 / +0.10 | GOOD |
| 135 mm f/2, sky (ISO 400 / 1600) | 0.61 / 0.60 | nearest: another maker's 135 mm f/2, 0.50 | +0.11 / +0.10 | GOOD |
| 135 mm f/2.8, sky | 0.15 | nearest: lensfun, 0.14 | +0.01 | PERFECT |

The two lensfun entries of the lens at 20 mm read 0.91 and 0.85 stops: every panel flat above
(monitor, tablet and paper) is deeper than Adobe's 0.66 and the sky flat, which fills the
corners, shallower, as the light sources make them; against lensfun four of the five panel flats
with a real diffuser would read shallower than the lens, which a panel cannot do. The 135 mm is a
manual lens, compared by range, so its rows keep the nearest profile.

What it cannot know: the profile is the model of the lens, not your copy of it, and it is
achromatic; the master is the 2x2 mean of the mosaic. **A manual lens** carries no lens ID that
any profile knows; then the candidates are the lenses on your camera's mount (for a lensfun
lens, the mount lensfun's camera table gives the body or one it says fits it; for an Adobe file,
the camera's maker in its name) whose name states exactly the range in the raw (for example
135 mm f/2.0), and the report says the list may hold another lens of the same range: read your
lens's row. With no raw beside the master and no `--lens` there is no
comparison: the block says why, and the verdict stays the five tests'. A raw that says nothing
about its lens (a camera on a telescope, or a lens without contacts: no lens ID, no name, no
focal range) is named as such, and the report says what to give: `--lens <text>`, plus `--focal`
and `--fstop` for whichever is unknown too.

**A filter on the lens voids the comparison.** The profile describes the bare lens; any filter
changes the vignetting (a mount shades the corners, a glass darkens the edges), so the master's
depth is no longer the profile plus the flat's error and the difference means nothing. In the
console, leave `--raw` out. In the window, the card opens with the box **I didn't use any filters
on my lens**, ticked by default: untick it and the run is the plain one, the verdict is the five
tests' alone, the card keeps only the master's depth, for information, with a line saying why,
and `Test result.txt` is rewritten without the table. Tick it again and the table comes back and
counts. The box is remembered per session folder (see "The window").

### 2. End to end: `--light`

```bash
flatcheck process/pp_light_00001.fit --light    # one frame
flatcheck process/ --light                      # the whole series (recommended)
```

The only mode that closes the loop; the structure tests are proxies for it. **But it does not work
on all data**: see "When `--light` works and when it does not", which matters.

It estimates the local background (the 15th percentile of each tile, robust to stars and
nebulosity) and fits, all at once:

```
background = a + b·X + c·Y + d·R² + e·R⁴
```

The plane absorbs the light-pollution gradient; the radial terms catch the residual vignetting,
that is the flat's error. Fitting both at once is what lets the test work under a polluted sky:
the two have different shapes, so they separate. Four rounds of one-sided sigma clipping reject
the tiles with nebulosity (a contaminant only adds signal).

**The sign is the diagnosis:**

- **positive**: the background *falls* toward the edges, **under-correction**. Another aperture
  for the flats than for the lights, or a contaminated flat.
- **negative**: the background *rises* toward the edges, **over-correction**. Almost certainly
  flat-darks missing or wrong: you divide by a flat that still holds an offset.

> **Without darks**, the lights *must* get a bias; otherwise their offset is divided by the flat
> and amplified radially into the corners. In Siril: `-bias=...` at `calibrate`. The biases in
> `biases/` must be at the **same ISO** as the lights.

#### Run it on the folder, not on one frame

The radial component of a frame is the sum of two things: the flat's error, which is **the same on
every frame by construction** (the same master was divided out of each), and a radial term of the
sky, which changes during the session.

On a real session of 32 frames over 81 minutes, the figure swept smoothly from +3.13 %
through -19.20 % to +35.73 %, with only 1.36 points between consecutive frames. Whatever moves
like that is sky, not flat.

Even on a narrow field the sky leaves its mark on single frames. On a session at 135 mm (87
frames, verdict OK at a mean of +1.13 %), the figure sat near -0.3 % for the first nine frames, rose
smoothly to +4.31 % over the next 27 and settled near +0.4 % for the last 51, 0.30 points between
consecutive frames. One frame from the middle of that hump would have read SUSPECT on a flat the
series proves good.

Given a folder, it runs on every frame that matches the pattern (`pp_light_*.fit` by default),
prints a table row per frame as it goes, and averages. **But it gives a verdict only when the
mean means something**, under three conditions:

| condition | threshold | why |
|---|---|---|
| number of frames | at least 5 | below that, a mean means nothing |
| error of the mean (`σ/√n`) | under 2.00 points | above even the OK threshold, the series does not resolve the flat to the precision the thresholds speak at |
| correlation with the sky gradient, in absolute value | under 0.70 | if the "flat error" follows the sky, what is measured is the sky |

The correlation is checked only when the sky gradient really varies over the series, by 10
points or more. On a clean session where it stays constant, the correlation is noise and would
reject good data.

If a condition fails, the verdict is **UNDETERMINED** and the report says which and why. A
reasoned "I don't know" is more useful than a number that looks confident.

The default pattern leaves out `r_pp_light_*`: registered frames are transformed geometrically,
so the flat's error, fixed to the sensor, is moved off the centre the radial model is fitted
around. Use `--pattern` for another naming convention.

### 3. Exposure: `--expo`

```bash
flatcheck DSC_0060.NEF --expo                            # the recommended way
flatcheck DSC_0060.NEF --expo --sky                      # a sky flat
flatcheck raw_flat.fit --expo                            # a raw FITS from Siril
flatcheck raw_flat.fit --expo --sat 4095 --black 150     # FITS only, when the scale is guessed wrong
```

Runs on a **single raw flat**, not on the master: stacking normalises and erases the level. It
splits the frame into its 4 Bayer positions and takes the median of each; if `max/min > 1.15` it
treats the file as CFA and uses the brightest channel (it clips first, so it is the limiting one).

**The channels are named by colour, but only when the file says which is which.** On a raw the
order comes from LibRaw; on a FITS, from the `BAYERPAT` card Siril writes at Conversion. The
report then writes `R  (CFA[0,0])`, `G1 (CFA[0,1])` and so on. `BAYERPAT` describes the sensor
read from the top down, and the tool reads a FITS the way Siril shows it, the file's first row at
the bottom, so on the file Siril writes (`ROWORDER = 'BOTTOM-UP'`) the pattern applies as written:
`RGGB` reads as `RGGB`, `R` at `CFA[0,0]`, the same as on the camera's raw. A file written
top-down, or one without the card, is read upside down against its pattern, and on an even height
that shifts the phase by one row: there `RGGB` reads as `GBRG`. With no source, the positions stay
anonymous, `CFA[y,x]`. Nothing is guessed: the order differs from camera to camera (RGGB is the
most common, but GBRG, GRBG and BGGR exist), and a label on the wrong channel would look measured.
For the verdict it does not matter which is which anyway: the limiting channel is the brightest,
whatever its colour.

**The figure that decides is the median of the central 20 % window, not the global median.** A
flat has a wide distribution by definition (that *is* the vignetting), so the global median is
pulled down by the corners. Expose by it and you overexpose until the centre clips, exactly where
the vignetting signal is highest.

> The camera's histogram, or ViewNX's, shows the **processed** image (debayered, white-balanced,
> gamma-encoded), which pushes the peaks to the right: that is why a frame looks "at the limit"
> when its raw data sit at ~40 %. For a histogram of the real raw data: Siril, or the window's
> Exposure tab.

**On a raw nothing is guessed:** the camera writes both the saturation and the black level into
the file, the mode uses them, and the report says `source: libraw (exact)`. On the test camera, at
12 bits: white 4095, black 150.

**On a FITS**, where the scale is gone, the saturation is detected from the frame's maximum, and
`--sat` / `--black` are there for when it guesses wrong. To learn the exact white, shoot one frame
overexposed on purpose: its maximum *is* the saturation. (On a raw you do not need to.)

`--sat` and `--black` beat the file, so they stay a way out on a raw too. One real reason: on the
test camera LibRaw reports `maximum = 4095`, but the sensor is no longer linear above ~3827. The
difference is not cosmetic: on the same frame, 50.3 % against 53.9 %.

**Sanity check:** the mode refuses the file if the central level is below 1.0 (already calibrated
or normalised) or if the four CFA positions are almost identical (the file is debayered). It
catches exactly the mistake "I opened the raw in Siril and pressed Save".

#### If you really need a raw FITS

`--expo` does not need one: give it the camera's raw file. If you need the FITS for another
reason, **do not** open the raw in Siril and Save, which debayers and normalises it. Use Siril's
**Conversion** tab: add the raw files, Convert. The result keeps the raw CFA mosaic at the
original ADU scale. The mode works on FITS for mono astro cameras too, which write FITS natively
and have no raw file at all.

#### `--sky`, for sky flats

For flats on the blue-hour sky a modest level is **right**: the sky darkens continuously and is
full of faint stars, and the master's SNR comes from the *number* of frames, not from pushing one
frame to 55 %. Without `--sky`, a sky flat at 22 % would be reported, wrongly, as underexposed.
The bands of both are under "Thresholds".

### 4. Histogram equalisation: `--histeq`

```bash
flatcheck flats/DSC_0060.NEF --histeq -o picture.png
```

Not a test, and no verdict. It is **Siril 1.4.4's "Histogram" display**, redone: the image passed
through its own distribution function, so that every grey level gets the same number of pixels.
On a flat, whose distribution is narrow against the file's scale, the curve is steep exactly over
the vignetting's range, and everything that is not uniform comes out at maximum contrast,
including what does not show at all on the linear image. Measured on a synthetic frame with 5 %
vignetting: a ripple of 0.3 % is a third of a grey level on the linear display and about fifteen
levels equalised.

What it does, as Siril does:

- a histogram of 65536 bins **per channel**, leaving out the pixels equal to zero;
- the transfer table is the cumulative distribution times 255, rounded as Siril rounds it;
- on a CFA frame the four Bayer positions are equalised **each on its own distribution**, like the
  layers of a debayered image in Siril. The console writes the RGB composite: R from the red
  position, G the mean of the two greens, B from the blue one, chosen by colour, not by position,
  so the order comes from the file and is never guessed; with no order, the mean of the four, in
  grey.

What differs: the bins span the channel's own `[min, max]`, not 0..65535, because a raster can be
at any scale (0..4095, 0..65535, 0..1); on integer data the mapping is the same, on float it is
finer. The equalisation runs at full resolution, then the 8-bit result is shrunk to ~1500 px on
the long side, by the same rule as the analysis raster.

**What the picture cannot tell: the amplitude.** Every grey level gets the same number of pixels
however close their values were, so a ripple of 0.5 % and a gradient of 10 % with the same shape
come out the same, and a perfect flat shows its own noise at maximum contrast. The picture says
**what shape** the thing the structure mode measures has; the figures are there. It exits with 0
once the picture is written; the report is information only.

It takes any file the tool reads: a camera raw or a FITS, a single frame or a master. On a master
(float, 0..1, with the CFA mosaic intact as Siril writes it) it works the same, and what shows is
what was left after stacking.

**False colours: `--rainbow`.** Beside its "Histogram" display Siril has a *colour map* button: the
equalised layer, already 8-bit, goes through a table of 256 hues: **magenta is the lowest level**,
then blue, cyan, green, yellow, and **red the highest**; level 0 is not black. Bands of one colour
are contours of equal rank, and the shape of the vignetting (round, off-centre, square) reads at a
glance where the grey picture shows a diffuse blur. The colours are ranks, not values: the
amplitude is still gone.

```bash
flatcheck flats/DSC_0060.NEF --histeq --rainbow -o picture.png
```

The table applies to **one layer**: the console writes the mosaic on a CFA frame and the channel on
mono, shrunk first and coloured after, so every pixel of the file is one level of the table, and
the report prints the legend. The RGB composite is never coloured: in Siril the RGB view takes one
byte from the colour of each layer, and where the channels have different shapes that makes colours
outside the palette, which say nothing. `--rainbow`
without `--histeq` is a command-line error, exit 1. In the window, the **False colours** chip beside
the views does the same on Mosaic, on a channel and on mono, and is greyed out on RGB.

### 5. Synthetic flat: `--synthetic`, `--lenses`, `--against`

```bash
flatcheck --lenses --lens "Tokina 11-20"                         # which lenses have a profile
flatcheck process/pp_flat_stacked.fit --synthetic process/pp_flat_stacked_synthetic.fit --lens "Tokina AT-X 11-20mm F2.8 PRO DX" --fstop 2.8
flatcheck process/pp_flat_stacked.fit --against process/pp_flat_stacked_synthetic.fit   # how far the master is from the model
```

Not a test, and no verdict. It writes **a radial flat from the lens's published vignetting
profile**: the model of the lens, not a measurement of your copy. It is there for when no light
source gives the real depth of the vignetting. Measured on one test lens at 20 mm f/2.8: the sky
gives 0.27 stops in the corner (the dome brightens toward the horizon all around the zenith and
fills the corners), a monitor up close 0.78-0.85 (the panel's angular fall-off), and the two public
databases 0.66 (Adobe) and 0.85 to 0.91 (lensfun's two entries). A synthetic flat has no source in
it, so neither the sky's curvature nor the panel's fall-off.

**Two sources of profiles, one list.**

- **Adobe (`.lcp`)**, read from the disk where Lightroom, Camera Raw or the free DNG Converter
  installed them: `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0` and
  `%APPDATA%\Adobe\CameraRaw\LensProfiles` (where Camera Raw and the DNG Converter put them), then
  `C:\Program Files\Adobe\<application>\Resources\LensProfiles\1.0` (the copy Lightroom Classic and
  Lightroom keep inside their own program folder). A file present in two folders is read once, from
  the first. They are neither copied nor redistributed; without Adobe, this half of the list is
  empty. There is one file per lens and mount, with a profile per focal length, aperture and
  distance. The model, from Adobe's "Adobe Camera Model" document: `V = 1 + a1·r² + a2·r⁴ + a3·r⁶`,
  with r in units of the focal length (the distance in mm divided by the focal length in mm), the
  same for every channel.
- **lensfun**, the database (CC BY-SA 3.0) that ships with the tool, in the `lensfun/` folder beside
  the executable. Its `pa` model (Hugin's) is the same polynomial, with r normalised to the
  half-diagonal of a 36x24 frame divided by the crop of the camera it was calibrated on. That is why
  lensfun needs your camera's crop, which it takes from its own camera database by `INSTRUME`, or
  from `--crop`. The Adobe model uses your crop too, for the frame's physical width (36 / crop);
  without it, it takes the sensor Adobe calibrated the profile on, and says so.

A profile calibrated on another format than yours is recomputed through millimetres and holds up
to the corner of its calibration sensor; beyond it the polynomial extrapolates, and above 10 % the
report says your corner is FAR beyond what was measured. Then take a profile calibrated on your format: Adobe keeps
files per body, such as "NIKON D750 (lens)", which the program prefers when the reference frame
names that body. The lensfun camera database knows over a thousand bodies; for one that is
missing, give `--crop` one of the six nominal crops (full frame 1.0, APS-C 1.5, APS-C Canon 1.6,
APS-H 1.3, Micro 4/3 2.0, 1" 2.7), or 36 divided by the sensor's width in mm.

**Updating lensfun:** download the XML files from
<https://github.com/lensfun/lensfun/tree/master/data/db>, replace those in the `lensfun/` folder and
restart. `lensfun/LENSFUN-VERSION.txt` says which commit the copy is from and when;
`lensfun/update-lensfun.py`, in the repository, does the same in one command.

**Choosing the lens and the profile.** The lens is chosen by text: `--lens` looks for every word in
the name, the maker, the mount and, for lensfun, the calibration format; `--source adobe|lensfun`
keeps one source. The profile is the **nearest**, not interpolated: the nearest focal length, then
the nearest aperture in stops, then the farthest distance; the report says what was asked and what
was used. A lensfun entry written at 10 m and at 1000 m with the same coefficients is a single
measurement at a distance nobody wrote down (that is how lensfun marks "unknown"), and the report
says `distance unspecified`, not `infinity`. On one test lens the entry calibrated on APS-C has the
shape of Adobe's profile at 0.36 m, not of the one at 1.5 m (a wall up close, most likely), and
that lens's vignetting drops a lot at close focus.

The focal length comes from `FOCALLEN` or from the raw; the aperture only from the raw (a Siril
FITS carries none) or from `--fstop`. The same lens on two mounts (Adobe has a file per mount) is
chosen by the camera in `INSTRUME`; when several lenses still remain, they are listed and the exit
code is 2.

**A focal length or an aperture the lens has no profile for is not refused.** The nearest profile
is taken (500 mm f/1.0 on a 35 mm f/1.8 lens ends at 35 mm f/1.8), and the report says so under
`profile used`, with a `WARNING`: what was asked, what was taken, what the lens has, and that the
depth written is the profile's, not that of the values asked. The threshold is 1 mm or 5 % on the
focal length and half a stop on the aperture; Adobe's grid is in whole stops, so any request inside
the lens's range passes without a word, and only those outside it are flagged. The same sentence
appears in the structure mode's vignetting table when the raw's focal length or aperture falls far
from a lens's profiles.

**A lensfun lens can be in the list twice**, with the same name and mount: the database holds it
once for every body someone calibrated it on, and the calibration format stands beside the mount
(`Nikon F AF, calibrated on APS-C 1.528` and `..., calibrated on full frame 1`). They are two
different profiles, not a duplicate: on one test lens, a 35 mm f/1.8, the entry calibrated on
APS-C gives 0.19 stops in the corner at f/2.8, the one calibrated on full frame 0.51, and Adobe
0.61; both enter the structure mode's vignetting table. The name alone matches both (exit 2, with
the list); add the format's word (`--lens "Nikkor 35mm f/1.8G full"`) to choose one.

**What it reports:** the depth written, in per cent of the axis and in stops, at four points: the
corner (the corner test's 5 % box), the long edge, the short edge, and half the half-diagonal. For
one test lens at 20 mm f/2.8 on the test camera: Adobe 0.67 / 0.51 / 0.31 / 0.28 stops, one lensfun
entry 0.85 / 0.56 / 0.26 / 0.24 (there the corner falls at r = 1.06, beyond the calibration sensor,
and the report says so).

**What it does not hold**, as the report says too: the chromatic part of the fall-off (on a sky
master of the tests R falls ~25 % more than B), the shading specific to the body, the dust, any
non-radial structure, and your copy of the lens. The two databases differ from each other by
~0.2 stops in the corner on the same lens; ask no more precision of it. The file is float, at the
reference frame's size, with its header copied (`BAYERPAT` included, so Siril takes it as a CFA
master) and `HISTORY` lines naming the profile used; 1.0 on the axis, nothing renormalised.

**`--against`** is the test that closes the loop: the structure battery on each of the two flats,
each normalised to a median of 1, **and** on their ratio, in a table of three columns, A, B and
A / B, with the vignetting in stops, the illumination offset, the local spot and the five tests. In the
ratio's column the figures are about the difference: the vignetting is the **depth difference in
stops** (positive: A falls more toward the corner), the five tests say whether the difference is
radial or has a shape, and the map shows it. **The difference's verdict has two parts**, the shape
(the five tests on the ratio) and the depth, and is the worse of them. The depth gets no new
threshold: a difference of D stops leaves in the corner of a light calibrated with the wrong flat an
error of `2^|D| - 1`, which goes through the same residual scale: under 0.03 stops PERFECT, under
0.14 GOOD, under 0.26 ACCEPTABLE, above that REDO. Up to ACCEPTABLE, under 0.26 stops, the
difference cannot be told apart from the model's own uncertainty, since the two profile databases
differ by ~0.2 stops on the same lens.
The vignetting row is the only one with a verdict in the difference's column; on the five tests the
verdict stays on each flat, the shape of the difference being the one above. Two correct flats of
the same lens give PERFECT and 0.00 stops.

It works between any two flats (real against synthetic, two diffuser thicknesses, two sources), so
it is also the convergence test for a diffuser: when the sky and the monitor, through the same
diffuser, give the same figure against the model, the source has left the measurement. Measured on
one test lens: the monitor up close against the Adobe synthetic +0.19 stops, the sky without a
diffuser -0.39, the Adobe synthetic against the lensfun one -0.18, with the shape PERFECT.

**When to use it.** The synthetic flat beats a contaminated sky flat by half a stop and is at least
even with a monitor's; it lacks the chromatic part and the body's shading. A thick diffuser,
converged in thickness and between two sources, stays better, and the synthetic flat is the
reference you validate it against.

## When `--light` works and when it does not

The mode splits the background into a light-pollution plane plus radial terms, the flat's error.
The split holds **as long as the light-pollution dome is roughly linear across the frame**. On a
wide field the dome *curves inside the frame*, and the radially symmetric part of that curvature
has exactly the shape of vignetting. Two things with the same shape cannot be separated by a fit,
however well written. **It is not a limitation of the tool, it is a geometric degeneracy.**

That is why the tool **refuses** to answer beyond a ~40-degree diagonal, instead of giving a bad
answer.

The conditions, most important first:

1. **A narrow field.** Under ~20 degrees of diagonal it is safe; above ~40 it cannot be done. On
   APS-C that means from ~100 mm up. A telescope is practically always fine.
2. **The target high in the sky.** The gradient grows steeply and non-linearly toward the horizon.
3. **A decent sky**, preferably with the town outside the frame.
4. **A target that does not fill the frame.** On the core of the Milky Way, the "background" the
   tool measures is not background, it is galaxy.

The field is deduced from the FITS header, in layers, the most trusted first: the `--fov` you give,
then the scale of a plate solve (`CDELT1` / `CD1_1`), then `XPIXSZ` with `FOCALLEN`, then `FOCALLEN`
alone. The last layer is the case of a DSLR through Siril, which writes no `XPIXSZ`: there the real
field cannot be computed, so the largest plausible sensor (full frame) is assumed and the focal
length decides (from 120 mm fine, under 60 mm refused, a warning in between), conservative by
construction. On a smaller sensor, give the real field with `--fov <degrees>`.

**If the header says nothing, the test runs**, and prints a line saying it could not check. Unknown
means unchecked, not suspect.

**A concrete counter-example.** A 20 mm lens on an APS-C camera covers ~70 degrees of diagonal. On
a real Milky Way session with that setup, the correlation between the radial component reported and
the sky gradient came out **-0.94** (R² = 0.88): 88 % of what the tool reported as "flat" was
explained by the sky. On data like these the flat is judged on its own, by the master's structure
tests, which do not involve the sky at all.

**A concrete positive example.** A 135 mm lens on the same camera covers ~12 degrees. On a session
of 87 frames, the sky gradient doubled from 7 % to 13 % over the last 51 frames while the radial
component stayed near +0.4 %: at that field the plane and the radial terms separate, and the
series came out OK with a mean of +1.13 %. When the structure run finds the depth wrong against
the lens profile while `--light` finds the lights corrected, the profile is wrong for the setup,
and `--light` is the measurement to trust.

## The residual scale

The five deviation tests of the structure mode (the corner spread, the azimuthal asymmetry, the
residual after the radial model, the tilt and the worst spot) measure *the same thing* from
different angles: how much of the frame a purely radial function does not explain. All five are
amplitudes in per cent, so they share one scale, not one threshold:

| band | verdict | means |
|---|---|---|
| under 2 % | `PERFECT` | perfect quality |
| 2-10 % | `GOOD` | usable; background extraction removes what is left |
| 10-20 % | `ACCEPTABLE` | usable, but confirm with the End-to-end test (`--light`) before a hard stretch |
| over 20 % | `REDO` | redo the flats |

The most important is **the residual after the radial model**: it measures directly how much of the
frame a purely radial model does not explain, over the whole frame, corners included. The corners,
the azimuthal asymmetry and the tilt are partial views of the same deviation: the corners catch it
only where the four boxes sit, the azimuthal test only inside the inscribed circle, the tilt only
the part that is a plane. **The worst spot** complements the residual, which drops 5 % of the tiles
at each end so as not to measure noise: a *compact* defect (a small leak, a condensation spot)
therefore vanishes from it, however deep. Measured: a finger-sized +10 % spot in a corner gave a
residual of 0.04 % and the verdict PERFECT, with the spot plain to see on the map; the worst spot
reads 9.3 % on it.

**It is an upper bound of the flat's error, not the error.** It measures the sum of two things with
opposite consequences: a part fixed to the **camera** (the optics, which divide out of the lights
correctly, so harmless) and a part fixed in the **world** (the illumination, which does not repeat,
so harmful). On one master the two cannot be separated; the information is simply not there. Only a
second session with the same lens separates them: what comes back identical is the camera's.

That is why a lens with genuinely non-radial vignetting shows up here as an "error" without being
one. Measured on the test masters: one master had 4.00 % of corner spread, of which
only ~1.3 % was the sky; the rest survived a 180-degree rotation, so it is fixed to the camera.

The illumination centre's offset is **not** on this scale, and not a test: it appears under
`INFORMATIVE (not tests)`, with no verdict. It measures a position, and that position is the ratio
of the gradient to the curvature, so it is neither a fraction nor scale-invariant. With the same
11 % gradient held fixed, the offset rises from 2.4 % on a lens with 2 stops of vignetting to 57 %
on one with 0.08 stops, and below that the illumination peak leaves the frame. A peak outside the
frame reads `undetermined`. The tilt measures the same defect as an amplitude, which is what the
scale asks for, and still catches a real decentring of the optical axis, weighted by what it
actually costs.

## Thresholds

They are **empirical rules of thumb, not physical constants**: the limits come from tests with one
camera. A few tolerances are not in the table: whether a frame is CFA (1.15), the lens and
sensor-format matching (a quarter stop on the aperture, 5 % on a crop), and when an extrapolation
is large (10 %).

| metric | mode | threshold |
|---|---|---|
| the five deviation tests | structure | the residual scale: 2 / 10 / 20 % |
| local spots (dust) | structure | 2 %: a warning, never a vote |
| vignetting against the lens profile | structure with `--raw` / `--lens`, `--against` | 0.03 / 0.14 / 0.26 stops: the residual scale, through the corner error |
| a request far from the profile used | `--synthetic`, structure with a profile | 1 mm or 5 % on the focal length, half a stop on the aperture: a warning |
| radial component, in absolute value | `--light` | under 2 % OK, under 5 % SUSPECT, above PROBLEM |
| diagonal field | `--light` | under 20 degrees fine, up to 40 a warning, above refused |
| focal length alone, as on full frame | `--light` | from 120 mm fine, under 60 mm refused, a warning between |
| sky gradient | `--light` | above 40 % flagged as large; never a condition |
| frames in the series | `--light` on a folder | at least 5 |
| error of the mean | `--light` on a folder | under 2.00 points |
| correlation with the sky gradient | `--light` on a folder | under 0.70, checked when the gradient spans 10 points or more |
| centre level, panel | `--expo` | OK 50-65 %; ACCEPTABLE, but rather low 30-50 %, rather high 65-80 %; TOO LOW under 30 %, TOO HIGH above 80 % |
| centre level, sky | `--expo --sky` | OK 15-50 %; rather high, but acceptable 50-60 %; TOO HIGH above 60 %, TOO LOW under 15 % |
| clipping in the centre | `--expo` | more than 0.05 % of the window at 98 % of white or above: TOO HIGH, whatever the level |

The thresholds of the series are the least calibrated of all: they come from a single session.

## Exit codes

| code | means |
|---|---|
| 0 | `OK` / `PERFECT` / `GOOD`: usable as it is |
| 1 | an error, or an input that does not suit the mode (a field too wide for `--light`, a file that is not a raw flat for `--expo`) |
| 2 | `SUSPECT` / `ACCEPTABLE` / `UNDETERMINED`, and `--expo`'s "acceptable, but" bands |
| 3 | `PROBLEM` / `REDO`, and `--expo`'s TOO LOW and TOO HIGH |

The severity is monotonic, so "anything but 0" means "not clean, look". The modes without a verdict:

- `--histeq`: 0 when the picture was written, 1 when it could not be.
- `--synthetic`: 0 when it wrote the flat, 1 when it refused the reference, could not learn the
  focal length, the aperture or the crop, or the lens chosen has no vignetting profile (Adobe ships
  files for some lenses with only their distortion or colour measured; they are in the list, and
  are refused when chosen), 2 when the lens could not be chosen (none, or too many: the list is
  printed).
- `--lenses`: always 0.

`--against` exits like the structure mode, on the verdict of the *difference*. Structure with
`--raw` or `--lens` exits on the combined verdict: the worse of the five tests and the depth against
the profile.

## Options

```
flatcheck <flat.fit>                       # structure
flatcheck <flat.fit> --raw <flat.NEF>      # structure + the vignetting against the profile of the lens named in the raw
flatcheck <light.fit> --light              # end to end, one frame
flatcheck <folder/> --light                # end to end on the whole series
flatcheck <raw_flat.fit> --expo            # exposure
flatcheck <raw_flat.NEF> --expo            # the same, directly on a camera raw
flatcheck <sky_flat.NEF> --expo --sky      # exposure for sky flats
flatcheck <flat.NEF> --histeq              # Siril's "Histogram" view, per channel
flatcheck <flat.NEF> --histeq --rainbow    # the same in Siril's false colours (one layer: the mosaic or the channel)
flatcheck <reference.fit> --synthetic <synthetic.fit> --lens <text>   # a synthetic flat from the lens profile
flatcheck --lenses [--lens <text>]         # the lenses with a profile (Adobe on the disk, lensfun/ beside the exe);
                                           # their focal lengths, apertures, distances and number of profiles, on a list of at most 40
flatcheck <A.fit> --against <B.fit>        # how much and how two flats differ

  -o, --out <file.png>     the output map (default flatcheck.png; with --histeq the equalised picture)
  --sky                    the thresholds for sky flats (--expo)
  --sat <white>            the saturation level, when the auto-detection is wrong (--expo)
  --black <level>          the black level (--expo)
  --rainbow                the equalised picture in Siril's false colours, magenta = lowest,
                           red = highest (--histeq; alone it is an error)
  --pattern <pattern>      which files of a folder it takes (--light; default pp_light_*.fit)
  --fov <degrees>          the field's diagonal, when the header does not give it (--light)
  --lens <text>            the lens, by words of its name / maker / mount (--synthetic, --lenses;
                           for structure, compares the master's vignetting with every lens that matches)
  --raw <file>             a raw beside the master (structure): the aperture and the lens are read from it
  --source adobe|lensfun   one of the two sources only (--synthetic, --lenses, structure with --lens/--raw)
  --focal <mm>, --fstop <f>, --crop <factor>   what cannot be read from the reference
                           (--synthetic, structure with --lens/--raw)
  --profiles <folder>      where to take the profiles from instead of the machine: any .lcp under it,
                           lensfun/ below it (--synthetic, --lenses,
                           structure with --lens/--raw)
  --lang en|ro             the language of the texts: en English (default), ro Romanian; --lang=ro works too
  -h, --help               the usage and the licence notice
```

A flag may come anywhere: `flatcheck --expo f.fit` and `flatcheck f.fit --expo` are the same. The
modes exclude each other; given two, the first in the order `--lenses`, `--synthetic`, `--against`,
`--histeq`, `--expo`, `--light` runs. A `--lang` with a value other than `en` or `ro`, or
with none, stops the run with an English error and exit 1. `--pattern` takes `*` and `?`; character
classes (`[abc]`) are taken literally.

Every run prints the version on its first line and the copyright on the second, `--help` and failed
runs included, so a report copied somewhere says what produced it.

## What invalidates a master

Cleaning the sensor. Another aperture. A filter or a ring changed. A serious knock to the lens.

It is **not** invalidated by time, by the camera's rotation on the sky, by different sessions, by
temperature, or by the ISO of the lights.

## The map

A diverging blue → white → red map, with white pinned at **1.0** and the range scaled to the 1st
and 99th percentiles plus 10 % of padding. Pinning white at 1.0 holds for every mode that writes one:
the structure residual is `image / model ≈ 1`, the `--against` residual too, and the map of
`--light` is `1 + radial / a`. The figures
are in the report; the map is there so you see the *shape* of a problem: a gradient, horizontal,
vertical or diagonal, a spot, an edge.

### The colour scale

The automatic scale stretches the colours over **each image's own range**, so full red means nothing
in particular: measured on two masters of the same lens, full red was `+3 %` on one and `+10 %` on
the other. Two maps on the automatic scale compare **by shape only**, not by intensity.

That is why **in the window** a map can be put on a fixed scale, with a row of chips: `+/-2 %`,
`+/-10 %` and `+/-20 %`, the very bounds of the residual scale, plus `auto`, useful to see the
*shape* on an almost perfect master, where ±2 % is already too coarse. The default is
**`+/-10 %`**, the band a normal flat is expected to live in, so two sessions compare at a glance
without pressing anything.

The cap and the band are not the same thing, though, and it is worth knowing when you read a colour:
the cap is the deviation of one *pixel* from the model, the band is a *peak-to-peak* amplitude. A
one-sided +10 % spot reaches the 10 % cap right at the edge of the GOOD band; a 10 % gradient runs
from -5 % to +5 %, so it reaches only half of it, and saturates only at 20 %, at REDO. Measured: with
a 10 % gradient the map's ends are at -4.9 / +4.8 %. No cap is right for both.

Under every map stands a **legend** with the real ends of the scale shown, for example
`blue = -10.0 %   white = 0 (exactly on the model)   red = +10.0 %`. On `auto` it shows the two ends
computed, which are usually asymmetric.

**The files written to disk are always on the automatic scale**, whichever chip is pressed: the
console's `-o` as well as the `flatcheck.png` the window saves. So the two applications write the
same file for the same master, and the chips stay an aid to looking.

### What the shapes mean

The map is the image divided by the radial model, so everything that was radially symmetric has
been removed: **what is left cannot be vignetting**. Read the figures first, which say *how much*,
and only then the shape, which says *why*. The same list is in the window, under the map, in
"How to read the map", open when the window starts.

| shape | what it meant |
|---|---|
| blotches with no direction | Small deviations, with no particular direction: the diffuser's mark and the noise. Background extraction removes them without effort; the figures say whether they can be ignored. |
| one side blue, the other red | One side of the frame is brighter than the other: the same thing "tilt" measures. An uneven light source, light from around, or something lying askew over the lens. Whether it comes from the world (the sky) or from the camera (the diffuser, the lens hood) one master cannot say; a second session with the same lens can. |
| concentric circles or arcs | A ring centred on the sensor cannot stay in the map: it is taken out with the vignetting. So if you see one, it is centred elsewhere: something round in front of the lens is not on the axis, or the light is not diffuse. It has not been seen on a real master yet. |
| a local spot in a corner or at an edge | A light leak: through the viewfinder, at the mount, past the diffuser. It belongs to the camera, so it comes back in every session until you find its cause. |
| a round spot in the centre | Extra light right in the middle: a panel too close, the light behind a monitor. When it is exactly centred it does not appear on the map and no test sees it, because it looks exactly like vignetting; measured, a 10 % spot in the centre leaves the map empty and every figure under 0.1 %. Its only trace is the vignetting figure, which grows. It shows only when it is slightly off-centre, and then as a crescent, not as a disc. Turning the camera does not remove it; the test is distance: one set of flats at 30 cm, one at 60. |
| fine grain everywhere | Noise, not a defect: too few frames or too low an exposure. The figures stay small; there is nothing to fix at the source, only more frames to stack. |

### Who decides: the figures or the map

The figures and the map do not play the same role, although they look like two views of the same
thing.

- **How much: the figures decide, not the map.** "Residual (radial model)" says how much of the
  frame does not look like a usual vignetting, symmetric about the centre, on the residual scale.
  Under 2 % it does not matter what shape it has; between 2 and 10 % background extraction removes
  what is left, whatever the shape. **The verdict never depends on what you see on the map.**
- **What kind: here the eye comes in, but not from scratch.** The shape says what to change for the
  next flats, not whether the flat is good. The figures help before you look: if "tilt" is close to
  the residual, one side of the frame is brighter than the other, and the map says in which
  direction; if "worst spot" is far above the residual, it is something small and compact (a light
  leak, condensation), and "local spots" says where; if the dust warning appeared, the question is
  no longer whether the flat is good but what you use it for: the session whose lights have the
  same spot, or as a library flat. What is left for the eye is the table above.

The same explanation is in the window, under the Structure tab's verdict.

## The window

`FlatCheck.Wpf.exe` makes **exactly the same measurements** as `flatcheck.exe`, on a whole session
folder, with tabs instead of runs. Its six tabs, Structure, End to end, Exposure, Histogram,
Synthetic and Split, are explained in the window's manual, tab by tab:
<https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck>. What follows is how to open a folder,
the **Run in Siril** button, the files the window writes, what it remembers, and what it does not
do.

### Opening a folder

Give it **a folder**: the working directory of a Siril session, the one with the subfolders
`flats/`, `darks/`, `biases/`, `lights/`, and `process/` or `masters/`. Choose it in the window
(**Choose folder...**), or give it on the command line, with `--lang` before or after it:

```bash
FlatCheck.Wpf.exe "D:\Astro\MySessionFolder"
FlatCheck.Wpf.exe "D:\Astro\MySessionFolder" --lang ro
```

As soon as the folder is chosen, the **structure** test and the **end-to-end** test start by
themselves, and the Synthetic tab loads the lens list; the tabs appear once all three have ended.
The window runs one analysis at a time: a click made during another run waits for its turn, and
the waiting screen says what it is waiting for. On a 24-megapixel master one analysis takes up to
about 1.25 GB of memory.

The master flat, `pp_flat_stacked.fit`, is looked for in `process/` and in `masters/`, the two
places Siril's scripts leave it. If it is in both, the **newest** is taken, and the Structure tab
shows a warning with both paths. If it is in neither, the window does not open the folder and
says what to do: run the preprocessing script, in Siril or from the window (below). If one of the
folder's subfolders cannot be read (a permission, a drive that is gone), a card says so, with the
folder and the system's message, instead of empty tabs; the details are in the log (see "Logs").

#### Running the Siril script from the window

When the master is missing, the screen that says so has a **Run in Siril** button. It runs the
script without opening Siril: it starts `siril-cli.exe` from your installation on the session
folder, with a script from the list of Siril's own script menu (the `.ssf` files ticked in
*Preferences › Scripts*, plus those in the script folders set there, read from `config.1.4.ini`).
The one proposed is **`OSC_Preprocessing_WithoutDark.ssf`**, from the scripts repository Siril
downloads: the script for a camera without darks. `OSC_Preprocessing.ssf` from the installation
works as well; it leaves the master in `masters/`. The window brings no script with it and changes
none.

Your Siril settings (`config.1.4.ini`) stay untouched. `siril-cli` rewrites its settings file at
the end of every script, and a failed save can delete it. So the window gives `siril-cli`, with
`-i`, a copy of them in a temporary folder of the run (`%TEMP%\FlatCheck\siril-…`), deleted at the
end; yours are only read. Siril works with the same settings, but nothing from the run flows back
into them: Siril no longer starts in the last folder run from the window, and its log may say
`Could not save the settings in …\siril.ini`, which is about the copy and changes nothing. If the
settings cannot be read (for example while Siril is saving them), the run does not start, the
message names the file, and you press again.

A window shows Siril's output live: the current command with its whole text, the progress bar
and the log lines. At the end it says whether the script succeeded or at which command it
stopped. When it succeeded, closing it opens the folder again and the analysis starts on what the
script produced. **Stop** kills the process; what it had written stays in `process/`, and a new
run overwrites everything.

The button stays disabled, and the screen says why, when `siril-cli.exe` is not found (looked for
through the installation's entry in the registry, then in `Program Files\Siril\bin`), when Siril's
configuration has no `.ssf` script, or when a folder the chosen script enters (`biases/`, `flats/`
and `lights/` for WithoutDark) is missing or empty. If Siril itself is open, a warning shows, not
a block: two processes writing into the same `process/` spoil its files, but the window cannot
know which folder the other one is working on.

### The files it writes

The window writes these files of its own into the session folder:

- **`Test result.txt`** and **`flatcheck.png`**, from the Structure tab: the console's output,
  identical (the version banner, the copyright, the report, the map's line), and the map on the
  automatic scale, the same bytes the console would write, whichever scale chip is pressed;
- **`pp_flat_stacked_synthetic.fit`**, beside the master, when you press **Write synthetic
  flat** in the Synthetic tab, which asks before it overwrites; the file does not become the
  session's master;
- **`Split result.txt`** and **`flatcheck-split.png`**, from the Split tab: the version banner,
  the date and time, the table and what it means, and the maps of the parts and the whole side by
  side, on one colour scale;
- **`flatcheck.json`**, in the folder's root, when a remembered choice changes (see "What it
  remembers").

`Test result.txt` and the Split's two files are overwritten without a question. The frames are
only read: none is changed, moved or deleted; **Run in Siril** lets Siril write its usual files.

The Split tab and **Run in Siril** work in temporary folders under `%TEMP%\FlatCheck\`, deleted
at the end; after a failed split the folder is kept, with its path on screen, so you can see why.
A split needs room there, and the tab shows how much beside the free space of that drive: 40
flats of 24 megapixels in 4 parts meant ~3 GB copied and ~17 GB written by Siril in the
temporary `process/`.

### What it remembers

Two switches and a choice are **remembered per session folder**, in `flatcheck.json` in its root:
the Structure tab's **I didn't use any filters on my lens** (`noFilters`, `true` by default), the
Exposure tab's **Sky flat** (`skyFlat`, `false` by default) and the Structure tab's **My lens**
(`lens`, the lens's catalog key, absent until you pick one). The file is read when the folder
opens and written whenever one of them changes; a folder you never touch gets no file, and a
file that cannot be read gives the defaults.

### What the window does not do

It does not replace the console: it has no `--fov`, `--sat`, `--black` or `--pattern`, and no exit
codes. For scripting, and for when the tool must be told what the header does not say (a field the
header does not give, an ADU scale guessed wrong), `flatcheck.exe` stays the tool.

## Logs

Both programs keep a log of what they did, for the day something goes wrong. To report a
problem, attach the whole `log` folder: it holds what is needed to see what happened.

**Where it is.** In a folder named `log` beside the program: `log\flatcheck.log` beside
`flatcheck.exe`, `log\FlatCheck.Wpf.log` beside `FlatCheck.Wpf.exe`. When that folder cannot be
written (a program under Program Files, a read-only drive), the log goes to
`%LOCALAPPDATA%\FlatCheck\log` instead, and its first line says so. Past 1 MiB (1,048,576
bytes, so a file ends one line past that) a file is left and the next one is started
(`flatcheck_01.log`, `flatcheck_02.log`, ...); the five most recent files are kept and the older
ones deleted, so the folder holds about 5 MiB per program at most. Two windows open at the same
time write into the same file, each line marked with its process.

**What is in it.** One line per event, numbers with a point: at the start the version, the
command line, the Windows and .NET versions and the folders; every file read, with its size, its
dimensions and what its header or the camera says (camera, lens, focal length, aperture); for
each run its options, the figures behind the verdict, the verdict and the time each stage took;
the lens profiles found and the lenses that matched; every file written; in the window, the
session opened, each tab's run and the buttons pressed, and the whole output of a Siril run;
every error with its technical details, those the program works around without a word included
(not a run you cancelled yourself, nor what the window drops while it closes), and when camera
raw files cannot be read, where LibRaw was looked for and which DLLs of Microsoft's Visual C++
runtime did not load; at the end the exit code and the time taken. The log's own words are
English whatever `--lang` says; an error's or a refusal's message appears as you saw it, so in
Romanian under `--lang ro`. It holds the paths of your files and the figures measured on them,
not their pixels.

**How much.** The environment variable `FLATCHECK_LOG` sets the level. For a program started
from a command window, set it there first:

```
set FLATCHECK_LOG=trace
```

That reaches only the programs started from that window. For the window started from the Start
menu or from AstroDarkroom, set it for your account, once (new programs see it; the ones already
open do not):

```
setx FLATCHECK_LOG trace
```

`debug`, the default, writes everything above; `trace` adds every FITS header card and Siril's
progress lines; `info`, `warn`, `error` and `fatal` write less and less; `off` writes nothing
and creates no folder.

## Development

The solution has five projects:

| project | what it is |
|---|---|
| `FlatCheck.Core` | the core: reading, analysis, reports, maps; no interface |
| `FlatCheck` | the console, `flatcheck.exe` |
| `FlatCheck.Wpf` | the window, `FlatCheck.Wpf.exe` |
| `FlatCheck.Tests` | the tests of the core and the console |
| `FlatCheck.Wpf.Tests` | the tests of the window |

```bash
dotnet build FlatCheck.sln -c Release
dotnet test FlatCheck.sln -c Release --filter "Category!=Validation"
```

The whole solution builds on Windows only, because `FlatCheck.Wpf` targets `net8.0-windows`. The
console alone builds wherever the SDK of `global.json` runs:
`dotnet build FlatCheck/FlatCheck.csproj -c Release`. Camera raws need LibRaw's native libraries,
which come for win-x64 and linux-x64 (publish with `-r linux-x64` for Linux); on any other platform
only FITS files are read.

The two publishes `scripts/release.ps1` runs:

```bash
dotnet publish FlatCheck/FlatCheck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish FlatCheck.Wpf/FlatCheck.Wpf.csproj -c Release -r win-x64 --self-contained
```

The last two flags of the first keep `flatcheck.exe` a single file to copy: LibRaw comes as native DLLs
(`raw_r`, `jpeg8`, `lcms2`, `zlib1`, ~2.1 MB), and without `IncludeNativeLibrariesForSelfExtract` they
stay separate files beside the executable. If they get lost (only the `.exe` of a plain publish copied),
the FITS modes work unchanged and a raw is refused; there is no path by which their absence produces
wrong figures. On Windows the message names Microsoft's Visual C++ runtime, which the DLLs import and
which is what a user's computer lacks, since every release carries the DLLs: the error the loader
gives cannot tell a missing DLL from a missing runtime, and names neither. The diagnostic log says
which: the folder `raw_r.dll` was found in, or that it was in none, and the runtime's DLLs that
Windows does not load. The window is published without `PublishSingleFile`, which WPF does not
pack as cleanly. Both builds and both publishes put `lensfun/` beside the executable.

The verdict thresholds are all in `FlatCheck.Core/Thresholds.cs`; a few tolerances sit beside
their code.

How the project is worked on (the commit gate, the golden files, the figures measured on real
masters, the requirements, the decisions and the design notes) is written in `CLAUDE.md` and
under `docs/`; `docs/design/README.md` indexes the design notes, and `CHANGELOG.md` lists the
changes, version by version.

## Licence

FlatCheck, a quantitative diagnostic for astrophotography flat frames.
Copyright (C) 2026 Tudor Carean

This program is free software: you can redistribute it and/or modify it under the terms of the
**GNU General Public License v3** as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but **WITHOUT ANY WARRANTY**; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
Public License for more details.

The full text of the licence is in the [LICENSE](LICENSE) file, and at
<https://www.gnu.org/licenses/gpl-3.0.html>. The same notice, in short, is printed by `flatcheck --help`
and shown in the window's **About**; the copyright is printed on every run, under the version.

### Third-party components

The tool links seven components written by others and ships a database made by others, all
compatible with GPLv3:

| component | role | licence |
|---|---|---|
| Sdcb.LibRaw | the C# wrapper over LibRaw | MIT |
| LibRaw (`raw_r.dll`) | reading camera raw files | LGPL-2.1 (the option chosen of LibRaw's dual LGPL-2.1 / CDDL-1.0) |
| libjpeg-turbo (`jpeg8.dll`) | decoding, for LibRaw, the JPEG-compressed data some raw files hold | IJG, BSD-3-Clause and zlib |
| Little CMS (`lcms2.dll`) | the colour management inside LibRaw | MIT |
| zlib (`zlib1.dll`) | decompressing, for LibRaw, the Deflate-compressed data some raw files hold | zlib |
| SixLabors.ImageSharp 3.1.x | decoding the JPEG preview a raw carries | Apache-2.0 |
| NLog and NLog.Targets.AtomicFile 6.2.x | writing the diagnostic log (see "Logs"), two windows into one file | BSD-3-Clause |
| the lensfun database (the `lensfun/` folder) | the vignetting profiles of mode 5 and of the structure's profile comparison; data, not code, copied unchanged | CC BY-SA 3.0 |

LibRaw's DLL calls the three libraries after it, which come in the same package as DLLs of their
own. This software is based in part on the work of the Independent JPEG Group.

Adobe's profiles (`.lcp`) are not a component: they are read on the machine, where Adobe put them, and
copied nowhere.

The FITS reader, the PNG encoder, every computation and every threshold are written by hand.

Why each licence is compatible, including why LibRaw is taken under the LGPL and not the CDDL, is in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Each licence's whole text is in the notices the
Astro Darkroom folder carries as `THIRD-PARTY-NOTICES.txt`,
[AstroDarkroom/THIRD-PARTY-NOTICES.md](../AstroDarkroom/THIRD-PARTY-NOTICES.md) in the repository. The
window's **About** shows the same list, with each component's copyright notice, and points to both.
