# FlatCheck

Quantitative diagnostic for astrophotography flat frames.

FlatCheck measures the flats of a photographic lens on a DSLR, stacked with
[Siril](https://siril.org), and answers three questions, each with a figure and the threshold
behind it:

- Is the master flat radially symmetric, and does its vignetting match the lens's published
  profile? (structure, the default mode)
- Does the flat correct a real light? (`--light`)
- Is a raw flat exposed right? (`--expo`)

Three more modes give no verdict. `--histeq` shows a frame the way Siril's "Histogram" display
does, where any unevenness comes out at maximum contrast. `--smooth` writes a second master with
the diffuser's texture removed. `--synthetic` writes a radial flat from the lens's published
vignetting profile, and `--against` compares two flats, for example the real one with that
synthetic one.

There are two applications over one core library, `FlatCheck.Core`:

- `flatcheck.exe`, a console: one file, one mode, one report per run;
- `FlatCheck.Wpf.exe`, a window: it opens a Siril session folder and runs the same six modes in
  seven tabs.

The window is not a second implementation. Both applications call the same code, so their
figures are the same by construction, and the report the window saves is byte for byte the
console's.

Both speak English by default, or Romanian with `--lang ro` (also `--lang=ro`), anywhere on the
command line. The language changes the words only: the figures, the verdicts behind them, the
exit codes and the names of the files written are the same in both. The output quoted in this
file is the English one. Files that earlier versions wrote under Romanian names (`_netezit`,
`_sintetic`, `Rezultat testare.txt`) are no longer recognised: rename them to keep using them.

FlatCheck measures and reports. It never changes a file you give it, and it never decides what a
shape on a map is. It was built and validated on the author's equipment: a Nikon D3300 with a
Samyang 135 mm f/2, a Nikkor 35 mm f/1.8 DX and a Tokina 11-20 mm f/2.8. Raws of other cameras
are read too, through LibRaw.

[TOC]

## Getting it

The window is one of the five tools of Astro Darkroom: the repository's front page (the
`README.md` at the root of the `photo` repository) says how to get the suite, prebuilt or built
from a checkout. The console is not part of the suite; build it as below.

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
  FlatCheck. See mode 6.

## Quick start

The window, on a Siril session folder (the one with `flats/`, `lights/` and `process/` or
`masters/`):

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"
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
modes also write a map, `flatcheck.png` unless `-o` names another file.

## Which file to give it

The most likely cause of a wrong result is not in the code; it is the file chosen.

| mode | wants |
|---|---|
| structure (default) | the **calibrated** master flat: `pp_flat_stacked.fit`, in `process/` or in `masters/` depending on the Siril script |
| `--light` | a light **calibrated with the flat**, **without background extraction**, **not registered**; better, the folder of them |
| `--expo` | a **single raw, uncalibrated** flat, preferably the camera's own raw file (`.NEF`) |

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
dust at f/2 is below the threshold (measured: the visible dust shadow on a Nikkor 35 master at
f/2.8 is 0.31 %, against a threshold of 2 %).

So any deviation from radial symmetry **is not vignetting; it is error**: an uneven light source,
a light leak, a tilted camera, ambient light, a diffusing sheet too close to the lens.

A good flat does not mean "it looks flat". It means "its deviation from radial symmetry is below
the noise". That is a figure, and it can be measured.

The radial model and the asymmetry are computed around **the centre of the sensor**, not around
the fitted peak of the illumination: fitting the centre would absorb exactly the linear gradient
being looked for, and the test would cancel itself. The radial profile is taken from the frame
averaged with its own 180-degree rotation, which removes a gradient before the profile is drawn;
and beyond the inscribed circle, where the rings are only arcs, the even non-radial part (such as
a saddle of the sky, measured from three quarters of that circle's radius outward) is divided out
before a ring's median is taken. Neither can then leave rings in the model that the flat does not
have (`docs/design/radial-model-even-term.md` tells how this was found).

## The six modes

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
| spread of the 4 corners | the four 5 % corner boxes against each other |
| max azimuthal asymmetry | the 12 sectors of each ring against each other, inside the inscribed circle |
| residual (radial model) | how much of the frame a purely radial model does not explain, over the whole frame, corners included: **the most important** |
| tilt (gradient) | the peak-to-peak amplitude of the linear gradient across the frame |
| worst spot (one tile) | the most deviant tile of the whole frame: a compact defect that the residual clips as an extreme |

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
Measured on the author's two Nikkor 35 masters at f/2.8: on the ISO 400 one the extreme is a
vertical stripe along the left edge (-0.44 %), not a round spot, and on the ISO 800 one it sits in
the top-right corner (+0.36 %), the same kind of thing. Both stay at a fifth of the threshold, so
nothing lit up; but if you ever see the warning, check first whether the shape on the map is
round. Only round is dust.

#### What structure cannot see

A spot that is **round and centred exactly on the axis** (a panel too close, the backlight of a
monitor the lens was centred on) is absorbed whole by the radial model, exactly like vignetting:
measured, a 10 % spot in the centre leaves every figure under 0.1 % and the map empty. Its only
trace is the vignetting figure, which grows. It shows only when it is slightly off-centre, and
then as a crescent, not a disc. No rotation removes it; the counter-test is distance: one set of
flats at 30 cm, one at 60.

What this mode gives is an **upper bound** of the flat's error, not the error: it measures the
sum of a part fixed to the camera (harmless) and a part fixed in the world (harmful), and one
master does not hold the information that separates them (see "The residual scale"). It is still
the test to start with, because it is the only one that needs nothing more to be photographed.

#### The sixth test: the vignetting against the lens's profile

The five tests measure deviations from radial symmetry, so they are **blind to a radial
excess**. A tablet master (Tokina at 20 mm f/2.8, four orientations 90 degrees apart) came out
PERFECT on all five with 1.26 stops of vignetting, where the two profile databases give 0.66 to
0.92 for that lens. The excess was the panel's angular fall-off, made radial by the rotation;
more rotations do not help. The tool's only outside reference is **the lens's published
profile** (Adobe's on the machine, lensfun's beside the program; see mode 6), and this is the
comparison:

```bash
flatcheck process/pp_flat_stacked.fit --raw flats/DSC_0001.NEF            # the lens and the aperture from the raw
flatcheck process/pp_flat_stacked.fit --lens "Tokina 11-20" --fstop 2.8   # or by name
```

The candidate lenses come from what the raw says about its lens: its name as the camera wrote it,
matched letter for letter against the profiles' names, and the lens ID (Nikon's on a Nikon, the
maker's on Canon, Sony and Pentax). The name's matches are the candidates when there are any, the
ID's otherwise, narrowed by the focal range and the aperture the raw gives and by the camera's
mount. With `--lens`, the candidates are the lenses that match every word of the text, as in
mode 6. For each candidate, the profile nearest in focal length and aperture is built as a raster
at the analysis size, and its vignetting is measured with **exactly the master's operator**: the
median of the 5 % corner boxes against the centre.

The difference in stops, master minus profile, goes through the same arithmetic as `--against`:
`2^|D| - 1` is the error it leaves in the corner of a light calibrated with the master, read on
the residual scale, so under 0.03 stops PERFECT, under 0.14 GOOD, under 0.26 ACCEPTABLE, above
that REDO. **The comparison's verdict is the nearest profile's**, not the worst one's: the two
databases differ from each other by ~0.2 stops on the same lens, and a master that matches one of
them is not wrong. The run's verdict is the worse of the shape (the five tests) and the depth, and
the exit code follows it.

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

Measured on the author's library (D3300, `--raw` with the first raw of `flats/`; recorded on
2026-09-18, re-measured unchanged on 2026-10-26, `docs/validation.md`):

| master | vignetting (stops) | nearest profile | difference | depth verdict |
|---|---|---|---|---|
| Tokina 20 mm f/2.8, tablet + 7 matte covers | 1.26 | lensfun 0.91 | +0.34 | REDO |
| Tokina 20 mm f/2.8, tablet + 15 covers | 1.24 | lensfun 0.91 | +0.33 | REDO |
| Tokina 20 mm f/2.8, sky, no diffuser | 0.27 | Adobe 0.66 | -0.39 | REDO |
| Tokina 20 mm f/2.8, monitor, diffuser close | 0.85 | lensfun 0.85 | 0.00 | PERFECT |
| Tokina 20 mm f/2.8, on the monitor | 0.78 | lensfun 0.85 | -0.07 | GOOD |
| Nikkor 35 DX f/2.8, sky (ISO 400 / 800) | 0.63 / 0.62 | Adobe 0.52 | +0.11 / +0.10 | GOOD |
| Samyang 135 f/2, sky (ISO 400 / 1600) | 0.61 / 0.60 | Nikkor 135 f/2 DC 0.50 | +0.11 / +0.10 | GOOD |
| Samyang 135 f/2.8, sky | 0.15 | lensfun Samyang 0.14 | +0.01 | PERFECT |

What it cannot know: the profile is the model of the lens, not your copy of it, and it is
achromatic; the master is the 2x2 mean of the mosaic. **A manual lens** (the Samyang) carries no
lens ID that any profile knows; then the candidates are the lenses on your camera's mount (for
a lensfun lens, the mount lensfun's camera table gives the body or one it says fits it; for an
Adobe file, the camera's maker in its name) whose name states exactly the range in the raw
(135 mm f/2.0), and the report says the list may hold another lens of the same range: read your
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
read from the top down, but Siril writes the rows bottom-up (`ROWORDER = 'BOTTOM-UP'`), and on an
even height that shifts the mosaic's phase by one row: on such a file `RGGB` reads as `GBRG`, so
`R` lands at `CFA[1,0]`. The tool makes the shift itself, as Siril does when it debayers. With no
source, the positions stay anonymous, `CFA[y,x]`. Nothing is guessed: the order differs from camera
to camera (RGGB is the most common, but GBRG, GRBG and BGGR exist), and a label on the wrong
channel would look measured. A D3300 reads `RGGB`. For the verdict it does not matter which is
which anyway: the limiting channel is the brightest, whatever its colour.

**The figure that decides is the median of the central 20 % window, not the global median.** A
flat has a wide distribution by definition (that *is* the vignetting), so the global median is
pulled down by the corners. Expose by it and you overexpose until the centre clips, exactly where
the vignetting signal is highest.

> The camera's histogram, or ViewNX's, shows the **processed** image (debayered, white-balanced,
> gamma-encoded), which pushes the peaks to the right: that is why a frame looks "at the limit"
> when its raw data sit at ~40 %. For a histogram of the real raw data: Siril, or the window's
> Exposure tab.

**On a raw nothing is guessed:** the camera writes both the saturation and the black level into
the file, the mode uses them, and the report says `source: libraw (exact)`. On a D3300 at 12 bits:
white 4095, black 150.

**On a FITS**, where the scale is gone, the saturation is detected from the frame's maximum, and
`--sat` / `--black` are there for when it guesses wrong. To learn the exact white, shoot one frame
overexposed on purpose: its maximum *is* the saturation. (On a raw you do not need to.)

`--sat` and `--black` beat the file, so they stay a way out on a raw too. One real reason: LibRaw
reports `maximum = 4095` for the D3300, but the sensor is no longer linear above ~3827. The
difference is not cosmetic: on the same frame, 50.3 % against 53.9 %.

**Sanity check:** the mode refuses the file if the central level is below 1.0 (already calibrated
or normalised) or if the four CFA positions are almost identical (the file is debayered). It
catches exactly the mistake "I opened the NEF in Siril and pressed Save".

#### If you really need a raw FITS

`--expo` no longer needs one: give it the NEF. If you need the FITS for another reason, **do not**
open the NEF and Save, which debayers and normalises it. Use Siril's **Conversion** tab: add the
NEFs, Convert. The result keeps the raw CFA mosaic at the original ADU scale. The mode works on
FITS for mono astro cameras too, which write FITS natively and have no raw file at all.

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

What it does, as Siril does (read from its source, `image_display.c` and `histogram.c`):

- a histogram of 65536 bins **per channel**, leaving out the pixels equal to zero;
- the transfer table is the cumulative distribution times 255, rounded as `round_to_BYTE` does;
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
equalised layer, already 8-bit, goes through a table of 256 hues (`make_index_for_rainbow` in its
source: the hue runs from 300 degrees at level 0 to 0 degrees at level 255, saturation and value 1).
So **magenta is the lowest level**, then blue, cyan, green, yellow, and **red the highest**; level 0
is not black. Bands of one colour are contours of equal rank, and the shape of the vignetting
(round, off-centre, square) reads at a glance where the grey picture shows a diffuse blur. The
colours are ranks, not values: the amplitude is still gone.

```bash
flatcheck flats/DSC_0060.NEF --histeq --rainbow -o picture.png
```

The table applies to **one layer**: the console writes the mosaic on a CFA frame and the channel on
mono, shrunk first and coloured after, so every pixel of the file is one level of the table, and
the report prints the legend. The RGB composite is never coloured: in Siril the RGB view takes one
byte from the colour of each layer, and where the channels have different shapes that makes colours
outside the palette, which say nothing (measured on the Samyang's NEF, 2026-10-26). `--rainbow`
without `--histeq` is a command-line error, exit 1. In the window, the **False colours** chip beside
the views does the same on Mosaic, on a channel and on mono, and is greyed out on RGB.

### 5. Smoothing: `--smooth`

```bash
flatcheck process/pp_flat_stacked.fit --smooth process/pp_flat_stacked_smoothed.fit
flatcheck process/pp_flat_stacked.fit --smooth smoothed.fit --scale 150
```

Not a test, and no verdict. It writes **a second master**: the same flat, with everything smooth
kept and everything else removed: the diffuser's texture, the dust shadows, the compact spots, the
noise. It is meant for a flat shot through a material laid on a monitor, whose texture reaches the
lights as a blanket of cells a few per cent deep.

**Why not a synthetic radial flat.** A purely radial model would remove the texture, but also
everything the lens does besides vignetting: on a Tokina at 20 mm the four corners differ from each
other by 6-16 %, exactly the case where a radial model corrects worse than the flat it replaces.
Smoothing keeps the uneven corners, the gradient, the wide blotches: everything smooth at the
chosen scale.

**What it does**, on each Bayer position separately (on mono, on the whole frame):

1. the median of tiles of ~150 sensor px, keeping the partial tiles at the edges;
2. at each tile, a local quadratic fit over the neighbouring tiles, weighted by a Gaussian of
   sigma = the scale (300 sensor px by default);
3. robustly: the tiles that deviate from the local fit by more than 2.5 MAD *and* more than 2 % of
   the level (dust, spots, the deepest cells) no longer weigh in anyone's fit;
4. at each pixel, the bilinear mean of the predictions of the four tiles around it.

There is no radial model, on purpose: two variants that had one failed on the real masters. A global
polynomial does not follow the medium-scale structure every master has (a monitor's blotches at
1000-1500 px, an off-centre ring), and a model on rings **puts rings into the file**: beyond the
inscribed circle the median of a ring jumps from one ring to the next, and a local field of 150 px
cannot make up for it. The smoothing above knows of no centre, so it cannot make rings.

**What it reports** is *what it removed*, because that is the only figure by which you can decide
whether the file can be trusted: p1 / p99 per pixel, the large-scale structure (p95 - p5 over the
tiles) and the most deviant tile of the ratio `original / smoothed`, the deepest spot, and the
structure battery on the input and on the output, side by side. When the most deviant tile
reaches 2 %, the report warns that something of large scale was removed too (deep dust, a spot, or
a real structure narrower than the scale) and leaves the decision to you.

Measured on five real masters (the Nikkor 35 DX on the sky at two ISOs, the Tokina 20 with the
diffuser close to the lens and on the monitor, the Samyang 135 on an LCD), at 300 px: every verdict
and vignetting depth stays the same; the five tests move by less than half a point; the component
removed is 0.12-0.39 % on the four clean masters (pixel noise plus the dust) and 3.7 % on the
textured one, where the compact spot falls from +6.19 % to +0.62 % and the texture in the file from
±3 % to ±0.3 %. On a synthetic frame with a known truth the method's error is under 0.2 % over the
whole frame, corners included.

**What it does not know.** What is the lens's and what is the source's: a monitor's blotches, the
sky's gradient, a panel's uneven corners stay, because they are smooth; nothing here separates
them. And what it removes can be wanted: a dust shadow is right for the session whose lights have it
too, and wrong in a library flat. A real structure of ~300 px (the Nikkor's off-centre ring,
0.3-0.5 %) is partly taken at 300 px and stays at 150 px; the scale is the only knob. What is left
of the cells is fundamental: a random field with a correlation length of 300 px passes through a
300 px kernel at ~30 % in RMS.

**The file** keeps the input's type (16-bit stays 16-bit; anything else is written as 32-bit
float, so a float master keeps its values) and the input's header card by card (`BAYERPAT`,
`STACKCNT`, `DATE-OBS`), plus two `HISTORY` lines. Nothing is renormalised. Siril's scripts name
`pp_flat_stacked` explicitly, so they do not pick up the smoothed file by themselves: rename it in
place of the master (keep the original) or give it to `calibrate` with `-flat=`. The mode refuses a
raw (a single, uncalibrated frame: for a raw there is `--expo`) and a colour cube (the reader would
average it into one raster).

**A check you can make yourself:** smooth two masters of the same lens and compare them with
`--against`; what is left should be a smooth, low-order field. Whether the smoothed flat really
corrects better only `--light` can say, on calibrated lights, and it refuses a field wider than 40
degrees.

#### Choosing the scale, and `--scale auto`

The scale must sit between the size of what you want removed and the size of the smallest real
structure you want kept. Both can be read from the master, and `--scale auto` reads them for you:

1. **The input's fine texture**: the structured part of the component under ~128 sensor px, with
   the pixel noise subtracted. Under 0.20 % there is nothing to smooth and the tool runs nothing:
   on the real masters, those without texture read 0.04-0.06 %, the one with the diffuser on the
   monitor 0.69 %.
2. Otherwise, **one smoothing per step** (150 / 300 / 600 px, ~15 s in all on 24 MP) and two
   figures per step: the texture left in the file (under 0.05 % it is gone), and how much the
   azimuthal asymmetry and the tilt *fell*. Those are the two tests the texture does not touch, so a
   fall of more than 0.5 points there is real structure leaving.
3. **The smallest step** at which the texture is gone and that guard stays quiet. On the master with
   the diffuser on the monitor: 150 leaves 0.09 % of texture, 300 leaves 0.02 % with the guard at
   0.00 / -0.01, so 300. At 600 the guard catches the Nikkor's real ring (asymmetry -0.79) and the
   monitor's blotches on the Tokina (-0.80).

Three answers. `PROPOSED SCALE`: the smoothing runs at it and writes the file (exit 0).
`NOTHING TO SMOOTH`, and `UNDETERMINED` (the texture and the real structure have the same size):
nothing is written and the exit code is **2**, not an error but "read before you decide". The
rule **proposes, it does not decide**: it does not know what is the lens's and what is the
source's (an LCD's hot spot looks like a ring of the lens), and it is blind to a real structure
that sits only in the corners, outside the inscribed circle. Its thresholds rest on five masters;
one with a faint texture, 0.15-0.25 %, falls near the first.

By hand, the criteria are the same: look at the map of what was removed at ±2 % (only cells and
dust is right; a large smooth shape means the scale is too large) and at the before / after table
("worst spot" and the residual fall when the texture leaves, but the asymmetry and the tilt must
not move).

### 6. Synthetic flat: `--synthetic`, `--lenses`, `--against`

```bash
flatcheck --lenses --lens "Tokina 11-20"                         # which lenses have a profile
flatcheck process/pp_flat_stacked.fit --synthetic process/pp_flat_stacked_synthetic.fit --lens "Tokina AT-X 11-20mm F2.8 PRO DX" --fstop 2.8
flatcheck process/pp_flat_stacked.fit --against process/pp_flat_stacked_synthetic.fit   # how far the master is from the model
```

Not a test, and no verdict. It writes **a radial flat from the lens's published vignetting
profile**: the model of the lens, not a measurement of your copy. It is there for when no light
source gives the real depth of the vignetting. Measured on the Tokina at 20 mm f/2.8: the sky gives
0.27 stops in the corner (the dome brightens toward the horizon all around the zenith and fills the
corners), a monitor up close 0.78-0.85 (the panel's angular fall-off), and the two public databases
0.66 (Adobe) and 0.83 (lensfun). A synthetic flat has no source in it, so neither the sky's
curvature nor the panel's fall-off.

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
report says it is a LARGE extrapolation. Then take a profile calibrated on your format: Adobe keeps
files per body, such as "NIKON D750 (lens)", which the program prefers when the reference frame
names that body. The lensfun camera database has 1053 bodies; for one that is missing, give `--crop`
one of the six nominal crops (full frame 1.0, APS-C 1.5, APS-C Canon 1.6, APS-H 1.3, Micro 4/3 2.0,
1" 2.7), or 36 divided by the sensor's width in mm.

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
says `distance unspecified`, not `infinity`. On the Nikkor 35 f/1.8G DX the entry calibrated on
APS-C has the shape of Adobe's profile at 0.36 m, not of the one at 1.5 m (a wall up close, most
likely), and this lens's vignetting drops a lot at close focus.

The focal length comes from `FOCALLEN` or from the raw; the aperture only from the raw (a Siril
FITS carries none) or from `--fstop`. The same lens on two mounts (Adobe has a file per mount) is
chosen by the camera in `INSTRUME`; when several lenses still remain, they are listed and the exit
code is 2.

**A focal length or an aperture the lens has no profile for is not refused.** The nearest profile
is taken (500 mm f/1.0 on the Nikkor 35 f/1.8 ends at 35 mm f/1.8), and the report says so under
`profile used`, with a `WARNING`: what was asked, what was taken, what the lens has, and that the
depth written is the profile's, not that of the values asked. The threshold is 1 mm or 5 % on the
focal length and half a stop on the aperture; Adobe's grid is in whole stops, so any request inside
the lens's range passes without a word, and only those outside it are flagged. The same sentence
appears in the structure mode's vignetting table when the raw's focal length or aperture falls far
from a lens's profiles.

**A lensfun lens can be in the list twice**, with the same name and mount: the database holds it
once for every body someone calibrated it on, and the calibration format stands beside the mount
(`Nikon F AF, calibrated on APS-C 1.528` and `..., calibrated on full frame 1`). They are two
different profiles, not a duplicate: on the Nikkor 35 f/1.8G DX the entry calibrated on APS-C gives
0.19 stops in the corner at f/2.8, the one calibrated on a D780 0.51, and Adobe 0.61; both enter the
structure mode's vignetting table. The name alone matches both (exit 2, with the list); add the
format's word (`--lens "Nikkor 35mm f/1.8G full"`) to choose one.

**What it reports:** the depth written, in per cent of the axis and in stops, at four points: the
corner (the corner test's 5 % box), the long edge, the short edge, and half the half-diagonal,
exactly the points at which the real masters were compared with the databases. On the Tokina at
20 mm f/2.8 on a D3300: Adobe 0.67 / 0.51 / 0.31 / 0.28 stops, lensfun 0.85 / 0.56 / 0.26 / 0.24 (on
a D3300 the corner falls at r = 1.06, beyond the calibration sensor, and the report says so).

**What it does not hold**, as the report says too: the chromatic part of the fall-off (on the sky
master R falls ~25 % more than B), the shading specific to the body, the dust, any non-radial
structure, and your copy of the lens. The two databases differ from each other by ~0.2 stops in the
corner on the same lens; ask no more precision of it. The file is float, at the reference frame's
size, with its header copied (`BAYERPAT` included, so Siril takes it as a CFA master) and `HISTORY`
lines naming the profile used; 1.0 on the axis, nothing renormalised.

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
the Tokina: the monitor up close against the Adobe synthetic +0.19 stops, the sky without a
diffuser -0.39, the Adobe synthetic against the lensfun one -0.18, with the shape PERFECT (figures
of 2026-09-18, with the camera's crop in the Adobe model).

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

**A concrete counter-example.** The Tokina at 20 mm on a D3300 covers ~70 degrees of diagonal. On a
real Milky Way session with that setup, the correlation between the radial component reported and
the sky gradient came out **-0.94** (R² = 0.88): 88 % of what the tool reported as "flat" was
explained by the sky. On data like these the flat is judged on its own, by the master's structure
tests, which do not involve the sky at all.

## The residual scale

The five deviation tests of the structure mode (the corner spread, the azimuthal asymmetry, the
residual after the radial model, the tilt and the worst spot) measure *the same thing* from
different angles: how much of the frame a purely radial function does not explain. All five are
amplitudes in per cent, so they share one scale, not one threshold:

| band | verdict | means |
|---|---|---|
| under 2 % | `PERFECT` | perfect quality |
| 2-10 % | `GOOD` | usable; background extraction removes what is left |
| 10-20 % | `ACCEPTABLE` | usable, but confirm with `--light` before a hard stretch |
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
one. Measured on the author's library: a Nikkor 35 DX master has 4.00 % of corner spread, of which
only ~1.3 % was the sky; the rest survived a 180-degree rotation, so it is fixed to the camera.

The illumination centre's offset is **not** on this scale, and not a test: it appears under
`INFORMATIVE (not tests)`, with no verdict. It measures a position, and that position is the ratio
of the gradient to the curvature, so it is neither a fraction nor scale-invariant. With the same
11 % gradient held fixed, the offset rises from 2.4 % on a lens with 2 stops of vignetting to 57 %
on one with 0.08 stops, and below that the illumination peak leaves the frame. When it was a test,
the figure then dropped abruptly to 0.00 %: on the flattest lenses, the larger the defect, the
cleaner the report looked. Now a peak outside the frame reads `undetermined`. The tilt measures the
same defect as an amplitude, which is what the scale asks for, and still catches a real
decentring of the optical axis, weighted by what it actually costs.

## Thresholds

They are **empirical rules of thumb, not physical constants**: a starting point, to calibrate on a
setup you are sure of. The verdict thresholds are all in `FlatCheck.Core/Thresholds.cs`; a few
tolerances sit beside their code: whether a frame is CFA (1.15), the lens and sensor-format
matching (a quarter stop on the aperture, 5 % on a crop), and when an extrapolation is large
(10 %).

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
| fine texture of the master | `--smooth --scale auto` | under 0.20 % nothing to smooth; under 0.05 % gone |
| fall of the asymmetry or the tilt | `--smooth --scale auto` | above 0.5 points: real structure leaving |
| most deviant tile removed | `--smooth` | 2 %: a warning |

The thresholds of the series are the least calibrated of all: they come from a single real session.
Recalibrate them when you have one you are sure of.

## Exit codes

| code | means |
|---|---|
| 0 | `OK` / `PERFECT` / `GOOD`: usable as it is |
| 1 | an error, or an input that does not suit the mode (a field too wide for `--light`, a file that is not a raw flat for `--expo`) |
| 2 | `SUSPECT` / `ACCEPTABLE` / `UNDETERMINED`, and `--expo`'s "acceptable, but" bands |
| 3 | `PROBLEM` / `REDO`, and `--expo`'s TOO LOW and TOO HIGH |

The severity is monotonic, so "anything but 0" means "not clean, look". The modes without a verdict:

- `--histeq`: 0 when the picture was written, 1 when it could not be.
- `--smooth`: 0 when it wrote the smoothed flat and the maps, 1 when it refused the input (a raw, a
  colour cube) or could not write; with `--scale auto`, 2 when it wrote nothing because there is
  nothing to smooth or the scale is undetermined.
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
flatcheck <flat.fit> --smooth <smoothed.fit>                          # a smoothed flat: no texture, dust or spots
flatcheck <reference.fit> --synthetic <synthetic.fit> --lens <text>   # a synthetic flat from the lens profile
flatcheck --lenses [--lens <text>]         # the lenses with a profile (Adobe on the disk, lensfun/ beside the exe);
                                           # their focal lengths, apertures, distances and number of profiles, on a list of at most 40
flatcheck <A.fit> --against <B.fit>        # how much and how two flats differ

  -o, --out <file.png>     the output map (default flatcheck.png; with --histeq the equalised
                           picture; --smooth writes a second one beside it, suffix -after)
  --sky                    the thresholds for sky flats (--expo)
  --sat <white>            the saturation level, when the auto-detection is wrong (--expo)
  --black <level>          the black level (--expo)
  --rainbow                the equalised picture in Siril's false colours, magenta = lowest,
                           red = highest (--histeq; alone it is an error)
  --pattern <pattern>      which files of a folder it takes (--light; default pp_light_*.fit)
  --fov <degrees>          the field's diagonal, when the header does not give it (--light)
  --scale <px>|auto        the smoothing scale, in sensor pixels (--smooth; default 300);
                           'auto' proposes it from the master and smooths at it, or exits with 2
  --lens <text>            the lens, by words of its name / maker / mount (--synthetic, --lenses;
                           for structure, compares the master's vignetting with every lens that matches)
  --raw <file>             a raw beside the master (structure): the aperture and the lens are read from it
  --source adobe|lensfun   one of the two sources only (--synthetic, --lenses, structure with --lens/--raw)
  --focal <mm>, --fstop <f>, --crop <factor>   what cannot be read from the reference
                           (--synthetic, structure with --lens/--raw)
  --profiles <folder>      where to take the profiles from instead of the machine: any .lcp under it,
                           lensfun/ below it (the tests run so; --synthetic, --lenses,
                           structure with --lens/--raw)
  --lang en|ro             the language of the texts: en English (default), ro Romanian; --lang=ro works too
  -h, --help               the usage and the licence notice
```

A flag may come anywhere: `flatcheck --expo f.fit` and `flatcheck f.fit --expo` are the same. The
modes exclude each other; given two, the first in the order `--lenses`, `--synthetic`, `--against`,
`--smooth`, `--histeq`, `--expo`, `--light` runs. A `--lang` with a value other than `en` or `ro`, or
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
the structure residual is `image / model ≈ 1`, the maps of `--smooth` are centred on 1 (what was
removed, and the smoothed flat's residual), and the map of `--light` is `1 + radial / a`. The figures
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
a 10 % gradient the map's ends are at -4.9 / +4.8 %. No cap is right for both, so it is explained
here and the figures are not changed.

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
"How to read the map".

| shape | what it meant |
|---|---|
| blotches with no direction | Small deviations, with no preferred axis: the diffuser's signature plus the noise of the sky. Background extraction removes them without effort; the figures decide whether they can be ignored. |
| a dipole (one side blue, the other red) | A gradient, so a direction; it matches "tilt". An uneven source, ambient light, or an obstruction lying asymmetrically over the lens. Whether it is fixed in the world (the sky) or to the camera (the diffuser, the lens hood) one master cannot say; a second session with the same lens can. |
| concentric circles or arcs | A ring centred on the sensor can NOT stay in the map: the radial model absorbs it by construction. So if you see one, it is centred elsewhere: something round in front of the lens is off the axis, or the light is not diffuse. It has not been seen yet on a real master; the rings that once appeared on masters with a large gradient were an artefact of the radial model, corrected since. |
| a local spot in a corner or at an edge | A light leak: the viewfinder, the mount, the diffuser's side. It is fixed to the camera, so it comes back in every session until its cause is found. |
| a round spot in the centre | Illumination contaminated symmetrically about the axis: a panel too close, a monitor's backlight. When it is exactly centred it can NOT appear on the map, and no test sees it: the radial model absorbs all of it, exactly like vignetting; measured, a 10 % spot in the centre leaves the map empty and every figure under 0.1 %. Its only trace is the vignetting figure, which grows. It shows only when it is slightly off-centre, and then as a crescent, not a disc. No rotation removes it; the counter-test is distance: one set at 30 cm, one at 60. |
| fine grain everywhere | Noise, not structure: too few frames or too low an exposure. The figures stay small; there is nothing to fix at the source, only more to stack. |

### Who decides: the figures or the map

The figures and the map do not play the same role, although they look like two views of the same
thing.

- **How much: the figures decide, not the map.** "Residual after the radial model" is the very
  measure of how much of the frame a purely radial function does not explain, on the residual
  scale. Under 2 % it does not matter what shape it has; between 2 and 10 % background extraction
  removes what is left, whatever the shape. **The verdict never depends on what you see on the
  map.**
- **What kind: here the eye comes in, but not from scratch.** The shape says what to change at the
  next session, not whether the flat is good. And the figures do part of the work before you look:
  if "tilt" is close to the residual, it is a gradient, and the map says in which direction; if
  "worst spot" is far above the residual, it is something compact (a small leak, condensation), and
  "local spots" says where; and if the dust warning appeared, the question is no longer whether the
  flat is good but what you use it for: the session whose lights have the same spot, or the
  library. What is left for the eye is the table above.

The same explanation is in the window, under the Structure tab's verdict.

## The window

`FlatCheck.Wpf.exe` makes **exactly the same measurements** as `flatcheck.exe`, on a whole session
folder, with tabs instead of runs. Both applications sit on the same library, `FlatCheck.Core`, and
go through the same chain of calls, so the figures are the same by construction, not by care; the
report the window saves on disk is produced by the code that writes the console's, and a test
compares the two character by character.

### How to use it

Give it **a folder**: the working directory of a Siril session, the one with the subfolders
`flats/`, `darks/`, `biases/`, `lights/`, and `process/` or `masters/`. Choose it in the window
(**Choose folder...**), or give it on the command line, with `--lang` before or after it:

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida" --lang ro
```

As soon as the folder is chosen, the **structure** test and the **end-to-end** test start by
themselves, and beside them the Synthetic tab's prefill, which loads the lens list and writes
nothing. While they run the window shows no tab: a panel lists the three steps with their state
(waiting, running, done, or the tab's own message, such as no lights to run on), the running
step's line and its progress bar, and the tabs appear only once all three have ended, so nothing
can be clicked before the folder has been read whole. If a click's analysis of the previous
folder is still computing, the panel keeps the two tests on "waiting" until it ends (the lens
list loads meanwhile). The two tests run one after the other, not in parallel, and no analysis
started by a click (Exposure, Histogram, Smoothing, Synthetic, the Split's analysis) runs beside
another: every one passes through a single gate, one at a time.
Measured on 24 MP masters: one analysis takes 0.7-1.25 GB of memory, two at once 1.3-2.2 GB. A
click made during another click's run waits for its turn, and the waiting screen says what it is
waiting for.

The master flat, `pp_flat_stacked.fit`, is looked for in **two places**, because Siril's scripts
leave it in two: `process/` (the scripts from Siril's scripts repository, such as
`OSC_Preprocessing_WithoutDark.ssf`) and `masters/` (the scripts installed with Siril 1.4, such as
`OSC_Preprocessing.ssf`). Both work. If it is in both, the **newest** is taken, and the Structure tab
shows a warning with both paths: two masters mean two preprocessing runs, possibly of two sets of
flats, and you need to know which one you are looking at. `process/` itself is not required: a
folder with the master in `masters/` and no frames runs the structure test, and the End to end tab
says it has no series.

If the master is in neither place, the window does not open the folder and says what to do: run the
preprocessing script in Siril. That file is the structure mode's input, by convention.

If the folder is there but one of its folders cannot be read (permissions that deny listing it, a
link to a drive that is gone), the window says so in a card, with the folder and the system's
message, instead of opening empty tabs; the details are in the log (see "Logs").

#### Running the Siril script from the window

On that same screen a **Run in Siril** button runs the script without opening Siril: it starts
`siril-cli.exe` from your installation on the session folder, with the script chosen from a list.
The list is the one in Siril's own script menu (the `.ssf` files ticked in *Preferences › Scripts*,
plus those in the script folders set there, read from `config.1.4.ini`), and the one proposed is
**`OSC_Preprocessing_WithoutDark.ssf`**, from the scripts repository Siril downloads: the author's
script, for a camera without darks. `OSC_Preprocessing.ssf` from the installation works as well: it
leaves the master in `masters/`, where the window looks too. The window brings no script with it and
changes none; what runs is exactly the file Siril would run.

Your Siril settings (`config.1.4.ini`) stay untouched. `siril-cli` rewrites its settings file at the
end of every script, and a failed save can delete it: measured, in 5 to 10 per cent of the runs on
the author's computer, and that is how all of Siril's settings were once lost. So the window gives
`siril-cli`, with `-i`, a copy of them in a temporary folder of the run (`%TEMP%\FlatCheck\siril-…`),
deleted at the end; yours are only read. Siril works with the same settings, but nothing from the
run flows back into them. Two visible consequences: Siril no longer starts in the last folder run
from the window, and its log may sometimes say `Could not save the settings in …\siril.ini`, which
is about the copy and changes nothing. If the settings cannot be read (for example while Siril is
saving them), the run does not start, the message names the file, and you press again.

A window opens with Siril's output, live, like Siril's own log panel: the current command
(`command 7 of 20:` with its whole text, because Siril prints only the verb), the progress bar of
the operation under way, and the log lines. The progress lines, three quarters of what Siril writes,
become the bar, not text. At the end the window says whether the script succeeded or at which
command it stopped. When it succeeded, closing the window opens the folder again and the analysis
starts on what the script produced; after a failure the folder stays as it was. **Stop** kills the
process; what it had written stays in `process/`, and a new
run overwrites everything (checked: the script run a second time over a complete or a partial
`process/` remakes every file and exits with 0).

Three things can keep the button disabled, and the screen says which: `siril-cli.exe` not found
(looked for through the installation's entry in the registry, then in `Program Files\Siril\bin`), no
`.ssf` script in Siril's configuration, or an input subfolder missing or empty. The chosen script is
read beforehand, and the folders it enters (`biases/`, `flats/` and `lights/` for WithoutDark) must
exist and hold files, otherwise Siril would stop at its third line. If Siril itself is open, a
warning shows, not a block: two processes writing into the same `process/` spoil its files, but the
window cannot know which folder the other one is working on.

#### About

The **About** button in the top bar (or **F1**) opens a window with the version, the copyright, the
licence and warranty notice (the text the console prints at `--help`) and the list of third-party
components, each with its licence and a link to its project. It is there by obligation, not only by
courtesy: GPLv3 asks a program with an interface to show its notice itself, and the LGPL under which
LibRaw is taken asks for the library's notice to appear beside the program's. An `.exe` copied alone
has no `LICENSE` and no `THIRD-PARTY-NOTICES.md` beside it, so the window is the one place where the
notices surely travel with the program.

### The seven tabs

| tab | what it does | when it runs |
|---|---|---|
| **Structure** | mode 1 on `pp_flat_stacked.fit` from `masters/` or `process/`, with the vignetting compared with the lens profile when `flats/` holds a raw and the **I didn't use any filters on my lens** box is ticked | when the folder opens; flipping the box redoes the comparison and the verdict without reloading the master |
| **End to end** | mode 2 on the whole `pp_light_*.fit` series in `process/` | when the folder opens |
| **Exposure** | mode 3 on a raw from `flats/` | when chosen, and when the **Sky flat** switch changes |
| **Histogram** | mode 4 on the session's master, on the smoothed flat beside it (when there is one) or on a frame from `flats/`, raw or FITS | when chosen; changing the view recomputes nothing |
| **Smoothing** | mode 5 on the session's master; writes `pp_flat_stacked_smoothed.fit` beside it | on its button, never by itself: it writes a file |
| **Synthetic** | mode 6: chooses the lens from the Adobe profiles on the disk and the lensfun database beside the program, writes `pp_flat_stacked_synthetic.fit` beside the master and compares it with the master (`--against`) | on its button; only the prefill runs by itself |
| **Split** | splits the flats into 2 or 4 groups, runs the Siril script on each in a temporary session and puts the groups' masters beside the whole master: the five tests, the vignetting against the profile, the maps side by side; writes `Split result.txt` and `flatcheck-split.png` in the session folder | on its button, never by itself: it copies gigabytes and runs Siril N times |

With `--lang ro` the tabs read Structura, End to end, Expunere, Histogram, Netezire, Sintetic and
Split. A screen reader such as Narrator reaches what each tab shows, not only the tabs' names.

Two switches and a choice are **remembered per session folder**, in `flatcheck.json` in its root:
the Structure tab's **I didn't use any filters on my lens** (`noFilters`, `true` by default), the
Exposure tab's **Sky flat** (`skyFlat`, `false` by default) and the Structure tab's **My lens**
(`lens`, the lens's catalog key, absent until you pick one). The file is read when the folder
opens and written whenever one of them changes; a folder you never touch gets no file, and a
file that cannot be read gives the defaults.

### Structure

It shows the console's figures: the five deviation tests with each one's verdict, the residual scale
with the current band marked, the informative rows with no verdict, the dust warning under the
verdict when there is one, each row with its description under it; plus the residual map.

Under the tests stands **the vignetting against the lens's profile**. When `flats/` holds a raw, the
run is the console's with `--raw` on the first of them (the lens and the aperture from the raw, the
crop from the lensfun database by `INSTRUME`): one row per profile, the row that gives the verdict in
bold, the depth scale as a table of four bands in stops (the same residual scale, translated, not a
new threshold) with the verdict's band marked, and the run's verdict, the worse of shape and depth,
in the pill at the top, with its two parts written under it. With no raw, the card says why nothing
could be compared, and the report stays the plain one. With the box **I didn't use any filters on
my lens** at the top of the card unticked, nothing is compared either, on purpose: the card keeps
the master's depth and says why, the verdict is the five tests', and the report is the plain one;
the box is remembered in the folder's `flatcheck.json`. When the raw gives only a focal range (a
manual lens, such as the Samyang 135 f/2 on a Nikon: the "non-CPU lens" ID and the range set in
the camera's menu) and more than one lens of your camera's mount has that range, the card says so
and offers **My lens**, a picker with every lens of the range and a first entry meaning no choice,
on which the nearest profile decides. Pick yours and the comparison is redone with that lens alone,
without reloading the master: its row, its verdict, the report saying which lens you chose and
which were left out; the pick is remembered in the folder's `flatcheck.json` and applied when the
folder reopens. A raw that says nothing about the lens (a
camera on a telescope, or a lens without contacts: focal length 0, no ID, no name, no range) is named
as such, with what it lacks, not as a missing raw; the comparison is then made by naming the lens in
the Synthetic tab, or in the console with `--lens`, `--focal` and `--fstop`.

When `flats/` holds a raw and no Adobe lens profile is found on the computer, the card opens with a
red paragraph that says so and, when the `lensfun/` folder beside the program could be read, what
that leaves: lensfun's profiles alone, and a raw that names no lens (a Nikon writes only an ID)
matched by its focal range alone. Under it: install the free Adobe DNG Converter (the link opens
Adobe's page in the browser), or Lightroom Classic, Lightroom or Photoshop's Camera Raw, then
restart FlatCheck; and the folders it looked in. The console's report does not carry this
paragraph.

It saves in the session folder:

- **`Test result.txt`**: the console's output, identical: the version banner, the copyright, the
  report, the map's line;
- **`flatcheck.png`**: the map on the automatic scale, the same bytes the console would write.

The map on screen has the scale chips and the legend described under "The colour scale", plus the
shape guide under it. The file stays on the automatic scale whichever chip is pressed, so it matches
the `auto` chip, not necessarily what you see.

### End to end

It runs on the whole series and fills its table as the frames are done: 32 frames take about two
minutes, and a blank screen for all that time would look frozen. It may legitimately come out
`NOT APPLICABLE`: on a field wider than ~40 degrees of diagonal the sky and the vignetting have the
same shape, so no figure is produced. **That is not an error**: see "When `--light` works and when it
does not". On such data the flat is judged in the Structure tab, which does not involve the sky.

The tab explains itself in place, not in a folded guide: under each of the four figures of the series
summary a line says what the figure is and how it is judged; each column header of the frames table
explains its column when the mouse is over it; and under the map and its legend a paragraph says what
the picture is. They say:

| where | what it says |
|---|---|
| radial component, mean | The mean of the radial column: the figure the verdict is given on, once the series has at least 5 frames. Under 2 % it is OK, above 5 % a problem, suspect in between. The sign is the diagnosis: positive, the background falls toward the edges (under-correction: another aperture at the flats than at the lights, or a contaminated flat); negative, it rises (over-correction: almost always flat-darks missing or wrong). |
| spread (min .. max) | The smallest and the largest value in the radial column. A mean near zero means nothing when the frames sweep from far below it to far above: what sweeps like that is the sky, not the flat, which is the same in every frame. |
| error of the mean | The standard deviation of the radial column divided by the square root of the frame count: how tightly the series pins the mean down. Above 2.00 points the series does not resolve the flat to the precision the thresholds speak at, and the verdict is undetermined. |
| correlation with the sky gradient | Pearson's correlation between the radial column and the sky column. A "flat error" that follows the sky is sky: above 0.70 in absolute value the verdict is undetermined. It is checked only when the sky gradient varies by at least 10 points over the series; on a constant sky the correlation is noise and would reject good data. |
| frame (column) | The calibrated lights of `process/`, in file-name order, those matching `pp_light_*.fit`: the frames Siril wrote after dividing the flat out, before registration. Registered frames (`r_pp_light_*`) are left out: registration shifts the vignetting off the sensor's centre. |
| radial (column) | The radial component of this frame's background, in per cent of its level at the centre, with its sign. One frame holds the sky of its moment too, so it is the series summary that counts, not a row. |
| sky (column) | The sky gradient the fit absorbed in its plane, in per cent of the background: light pollution, the Moon, the frame's tilt toward the horizon. Information, never a condition; above 40 % the console flags it as large. If it moves from row to row, the sky moved during the session. |
| tiles (column) | The tiles kept in the fit out of all the frame's tiles, after four rounds of one-sided clipping threw out those with nebulosity or other bright structure. Few kept tiles mean a fit with less to stand on. |
| the map (under it) | The series' mean radial map: for every frame the radial part of the fitted background (the R² and R⁴ terms), drawn at the frame's size, then averaged pixel by pixel. No star, no nebula, only the shape of the model. White is a background with no radial component; blue corners, one that falls toward the edges (under-correction); red corners, one that rises (over-correction). The colour scale is the map's own range, so a strong colour can still be a small figure. |

### Exposure

The Exposure tab lists from `flats/` **camera raws only** (`.nef`, `.cr2`, `.cr3`, `.arw`, `.dng`,
`.raf`, `.pef`): the mode needs the CFA mosaic intact and the original ADU scale, and on a raw the
white and black levels are read exactly from the file instead of being guessed from the maximum. The
**Sky flat** switch changes the thresholds and recomputes at once; its state is remembered in the
session folder's `flatcheck.json` and restored when the folder is opened again.

The tab also draws **one histogram per channel**, on the same percentage axis all its figures sit on,
with the target band, the clipping stripe and the centre's median drawn over them. Under them, a
paragraph derived from the result says how this particular file is exposed: what decides, how far it
is from saturation, and why the wide curve does **not** mean underexposure. The drawing explains; the
verdict stays the median of the central window of the brightest channel, and nothing else.

The window's guide "How to read the histograms" says:

| what you see | what it means |
|---|---|
| two curves on each channel | The pale, wide one is the whole channel; the solid, narrow one is the central window of 20 %, the very one the verdict is taken from. Each is scaled to its own peak, so the HEIGHTS are not compared with each other; the positions are. |
| the vertical line | The median of the central window, the one figure that decides. On the brightest channel it is the very number in the verdict pill above. |
| the green band | The target, from exactly the same thresholds as the verdict. It moves when you tick **Sky flat**: a sky flat MUST sit lower, because the sky fades continuously and the SNR comes from the number of frames. |
| the yellow and red bands | The same scale further on: yellow "acceptable, but", red too low or too high. On the sky there is no yellow on the left: there the target starts right at the too-low threshold. |
| the red stripe on the right | 98-100 % of white: there the analyser counts the saturated pixels. Any mass of the solid curve in that stripe means TOO HIGH, however well-behaved the median: there the vignetting information is lost, exactly where the signal is highest. |
| why the pale curve is much wider | That is the vignetting, not underexposure: the corners of a flat ARE darker by construction, and the distance between the two peaks is just how deep it is. If you expose by the wide curve, you clip the centre and lose exactly what you measure. |
| why the channels peak in different places | The Bayer filters pass different amounts of the source's light. On a warm panel R may lead, on the sky at the blue hour usually B. The brightest clips first, so it is the limiting one. The tool does no white balance; that is exactly the point. |
| what this drawing is NOT | It is not the camera's histogram. That one is the processed JPEG's (debayer, white balance, gamma) and sits much further right. That is why a frame that looks "at the limit" on the camera's screen can be at 40 % in raw data. |
| the switch above the drawings | It chooses what you look at: the raw data, which the tool measures, or the preview the camera embedded in the raw, the very image from which it computed its on-screen histogram. Only the raw view has the target band, because only it says something about the light on the sensor. On the other there is no target, nor could there be: it would mean exposing by the screen. The table under the drawings puts the two figures side by side, on each channel. |
| why the screen is much further right | Gamma plus the tone curve, which no camera setting removes: a JPEG is gamma-encoded by definition. A compressive curve stretches the low and middle levels the most, which is exactly where a flat sits. And on the cameras measured here the screen saturated before the sensor, by a third of a stop up to a stop: so it is a one-way alarm ("not touching" is a guarantee, "touching" is no information), not a measuring instrument. |

**What the camera showed.** The switch above the drawings goes from the raw data to the JPEG preview
the camera embedded in the raw, decoded from the file. Under the drawings, a table puts the two
figures side by side for each channel, centre against centre. Measured on two sky flats from the
library, the blue channel:

| file | sensor (raw) | camera's screen | peak on the screen |
|---|---|---|---|
| Nikkor 35 DX, ISO 800 | 29.0 % | 81.2 % | 84.7 % |
| Samyang f/2.0, ISO 1600 | 32.1 % | 98.0 % | 100.0 %, with pixels at 255 |

The second looked burnt out on the camera and had 1.6 stops to spare in the file. The difference is
not a constant to remember: on the camera measured it is largest around 25 % raw, right where a flat
sits, and it narrows toward saturation. The preview view has **no target band**, and the verdict does
not look at it at all. On a raw that carries no preview it can decode, the switch is disabled and the
tab says why.

### Histogram

The Histogram tab lists **the master first**, marked `MASTER`; under it, **when it exists,
`pp_flat_stacked_smoothed.fit`**, marked `SMOOTHED` (the file the Smoothing tab writes, exactly at the
path it writes it, not any stack in the folder); then the frames of `flats/`, camera raws **and**
FITS, because for an equalisation a flat converted by Siril is as good as the NEF. A click equalises
a file; another click equalises another, and if the first was still computing it is abandoned: the
list stays clickable, and the waiting screen covers only the picture. Nothing runs by itself when the
folder opens: the Structure tab is already reading the 24 MP master then.

The `SMOOTHED` row **also appears by itself** during the session, as soon as the Smoothing tab has
written the file; what was selected stays selected and nothing is recomputed, with one exception: if
the smoothed flat itself was on screen and was rewritten, the picture is redone, since it would
otherwise show bytes that are no longer on the disk. Under the smoothed flat's picture stands a note:
a shape that shows on the master and is missing here was removed by the smoothing; one that shows on
both is smooth at the chosen scale and stayed. And because the equalisation goes by ranks, what is
left on the smoothed flat, however small its amplitude, comes out at maximum contrast: a smoothed
flat that looks *noisier* than the master is the expected picture, not a defect.

The master is there for one question: **were the imperfections seen on the single frames averaged
out?** A shape that shows on a frame and is missing from the master was transient (a cloud, a shadow,
a flicker) and was averaged out. One that shows on both is fixed to the setup, and that is what the
Structure tab's figures measure. Because the equalisation goes by ranks, the master's lower noise
makes a shared shape look *clearer* there, not weaker.

Above the picture stand the views, on a CFA frame: `Mosaic`, the default (the whole mosaic equalised
as one layer, literally what Siril shows on a raw opened undebayered); `RGB` (the four positions
equalised each on its own distribution, in their colours, with more contrast); and one grey view per
position (`R`, `G1`, `G2`, `B`, or `CFA[y,x]` when the file does not say the order). A mono frame has
a single view and no chips. Switching views rereads nothing. The tab saves no file; the console
writes the composite, shrunk.

The picture in the window is **at the sensor's resolution**, not shrunk like the console's PNG: it is
shown straight from the raster, with no encoding. It sits in a 520 px viewport, fitted whole at first,
with zoom and pan: the wheel zooms around the cursor (with no modifier key, as in Siril; outside the
picture the wheel scrolls the page), dragging pans when the picture is larger than the viewport (the
cursor becomes a hand), a double click switches between fit and 100 %, and the buttons `−`, `+`,
`fit` and `100 %` do the same. At 100 % one screen pixel is one sensor pixel; on `Mosaic` the mosaic's
R/G/B checkerboard shows, as in Siril at 100 %. Above 100 % the pixels are drawn as squares, below
100 % smoothed. Fit never zooms above 100 %: a picture smaller than the viewport is shown at its own
size. The zoom is kept when you change the view and reset when you change the file.

The window's guide "How to read the equalised picture" says:

| what you see | what it means |
|---|---|
| why a clean flat looks noisy | The equalisation stretches ANY distribution over 0..255, even that of a frame with no structure at all. Then what is left to stretch is the noise, and it reaches maximum contrast. Fine grain everywhere means "it has nothing else to show". |
| why the size of a shape cannot be read | Every grey level gets the same number of pixels, however close their values were. A ripple of 0.5 % and a gradient of 10 % with the same shape come out identical. The amplitude is in the Structure tab, not here. |
| the Mosaic view (the default) | The whole mosaic equalised as a single layer: literally what Siril shows on a raw opened undebayered, as it opens it by default; that is why it comes first. The three colours are three populations in the same histogram, so at 100 % the mosaic's checkerboard shows, as in Siril; shrunk, it averages out. |
| the RGB view | The four Bayer positions, each equalised on its own distribution and put in its colour: Siril on a debayered image. It has more contrast than the mosaic, because each channel is stretched over its whole range. A uniform tint means nothing; a tint that VARIES across the frame means one channel has another shape than the others: a source with a colour gradient, a coloured light leak. |
| the per-channel views | The same picture, a single channel, in grey. Useful when the composite is hard to read, or to see whether a shape is in every channel or only in one. |
| the False colours chip | Siril's rainbow table over the view on screen: the hue runs from magenta at the lowest level through blue, cyan, green and yellow to red at the highest, so bands of one colour are contours of equal rank and the SHAPE of the vignetting (round, off-centre, square) reads at a glance. The colours are ranks, not values: the amplitude is still gone. It applies to one layer (Mosaic, a channel, mono), never to the RGB view, where the chip is greyed out. It stays on when you change the view or the file. |
| zoom and pan | The wheel zooms around the cursor, dragging with the mouse pans, a double click switches between fit and 100 %. The page scrolls with the wheel outside the picture. At 100 % one screen pixel is one sensor pixel: the picture is at full resolution, not shrunk. The zoom is kept when you change the view and reset when you change the file. |
| the master against a frame | The master is the stack of the frames under it. A shape that shows on a frame and is missing from the master was transient and was averaged out; one that shows on both is fixed to the setup, and that is what the Structure tab's figures measure. |
| where the figure is | In the Structure tab: the residual after the radial model, the corners, the tilt, the worst spot. The picture here says WHAT SHAPE the thing they measure has. |

### Smoothing

Mode 5 on the session's master. Like Split, the tab **never runs by itself**: it writes a file of
~96 MB beside the master, and a file is not produced because a folder was opened. Its guide stands
open above the button, because it is the button's precondition; it says what mode 5 says above. Under
it are three scale chips (150 / 300 / 600 px, 300 by default), the **Propose a scale** button, which
runs the rule of `--scale auto`, shows the table of the steps and ticks the chip proposed, writing
nothing, and the **Smooth and save** button. That button waits for any analysis still running
(the structure test reads the same 24 MP master) and asks before it overwrites an existing smoothed file; a "no"
leaves the figures on screen and the old file on the disk, and says so.

After the run: the figures of *what was removed*, the yellow warning when something of large scale was
removed too, the structure battery **before** and **after** (the Structure tab's rows, each with its
verdict), and on the right two maps behind a switch: `original / smoothed`, exactly what left, and the
smoothed flat's residual after its own radial model, what the lens does once the texture is gone. The
maps' scale starts at ±2 %, not at ±10 % as on the other tabs: what is removed is small, and at 10 % the
map would be white.

The smoothed file **does not become the session's master**: its name is not one of the two paths the
window looks for, so the Structure tab still measures the master photographed and still shows the
texture that made the smoothing necessary. It **appears in the Histogram tab** as a row of its own,
under the master, marked `SMOOTHED`, as soon as it is written, so what the smoothing removed and what it
left can be seen side by side.

### Synthetic

Mode 6.

- **The list of lenses** comes from both sources, with an `Adobe` or `lensfun` chip on each row and the
  mount on the right; for lensfun, also the format the profile was calibrated on, because the same lens
  can be in the database twice, calibrated on two formats (see mode 6). It is searched by text: every
  word must appear, the format included. Under each row stand **the focal lengths, apertures and
  distances** of its profiles, with their count, exactly what `--lenses` prints, but only when the list
  has at most 40 rows, because an Adobe lens's details need its file read; on a longer list the rows
  stay bare and a note under the list says to type a text.
- **The focal length, the aperture and the crop are prefilled** from the files: the focal length and
  the camera from the master's header (the header only, not the 96 MB), the aperture and the lens from
  a raw in `flats/`, when there is one. From the raw it reads the lens's name as the camera wrote it (a
  Canon or a Sony writes it; the Nikon and the Pentax measured write none, whatever the lens), the lens
  ID the camera wrote (Nikon's on a Nikon, the maker's on Canon, Sony and Pentax) **and the lens's range**
  (the shortest and longest focal length, the widest aperture) from the MakerNote. The name, when there
  is one, is the key: it is matched letter for letter against the profiles' names, and it is the only
  key on Sony E, whose Adobe profiles carry no ID. Without a name, the ID alone is not enough (139 is
  the Tokina 11-20 f/2.8, the Nikkor 18-200 f/3.5-5.6 and the Sigma 18-35 f/1.8 alike), so the lenses
  whose name contradicts the range are dropped, and the lensfun lenses of the same maker with the same
  range join (lensfun carries no IDs). One left, it is chosen; several left (the Tokina 11-20's two
  Adobe generations and its two lensfun entries), the list narrows to them and you choose. The note
  under the fields says what was read from the raw, or that the raw says nothing about the lens (no ID,
  no name, no range: a telescope or a lens without contacts); then the aperture stays empty, and you
  type the focal length and the aperture and choose the lens.
- **The sensor-format chips** under the fields (full frame, APS-C, APS-C Canon, APS-H, Micro 4/3, 1")
  show the format of the value in the box: the nearest lights up, within 5 %, so 1.523 from the lensfun
  database lights APS-C 1.5, and the note under the box always says where the crop comes from and what
  format it is (`crop 1.523 from the lensfun database (Nikon D3300) - format APS-C`). A chip pressed puts
  its nominal crop in the box, for a camera the database does not have; a typed value lights its chip,
  and one with no format (1.15) leaves them all off, and the note says so.
- **When a lens is chosen**, how its profile fits your sensor appears under the fields: the format it
  was calibrated on, yours, and what follows (the same format; another format, with the radius
  recomputed through millimetres; your corner inside the profile, a small extrapolation or a LARGE one).
  The console prints the same sentence at `format`, and the tab shows it again under the depths
  written. There too, in yellow, stands **the mismatch warning** when the focal length or the aperture
  in the box is far from every profile of the lens (see mode 6); it is recomputed at every key, and
  after writing the same sentence stands under `profile used`.
- **For the Tokina the choice does not matter**: the atx-i 11-20 F2.8 CF (2020) is the AT-X 11-20 F2.8
  PRO DX (2015) in another housing, the two Adobe files have byte-identical vignetting parameters, and
  the lensfun entries, calibrated on AT-X copies, apply to it too.
- **The fields stay editable**; what you type beats what was read.
- The panel **The profiles: where they come from and how they are updated** is folded by default: it
  says where the Adobe profiles are looked for, which lensfun copy is used (the folder, the commit, the
  date) and how it is replaced.
- **Write synthetic flat** waits for any analysis still running, asks before it overwrites, shows the depth
  written at the four points, **and starts the comparison with the master by itself**: `--against` on
  the master and the file just written, with the difference's verdict (shape and depth, the worse of
  them) and the table of three columns (the master, the synthetic flat, the difference) for the
  vignetting (with the depth verdict), the offset, the local spot and the five tests, each row with its
  label and its note on the left and the three values together on the right; plus the map, on a
  switchable scale. The four depths written carry a note each: which point it is and what it is
  compared with.
- **Compare with the master** runs the same comparison on its own, for a synthetic file written
  earlier.
- The file does not become the session's master, any more than the smoothed one does.

### Split

The Split tab is for flats shot in rotated groups (15 + 15 at 180 degrees, or 4 x 10 at 90 degrees), to
see what the rotation cancelled and what it did not.

- Choose the number of parts (2 or 4; the number of frames in `flats/` must divide exactly, otherwise
  the button says why not), choose the Siril script from the same list as on the problem card
  (`OSC_Preprocessing_WithoutDark.ssf` proposed), and press **Start Split**.
- For each part a temporary session is built in `%TEMP%\FlatCheck\split-<date-time>\part-<i>\`: every
  folder the script enters (`biases/`, `darks/`), whole, but never `process/` or `masters/`, which are
  what Siril writes; **at most two** frames of `lights/` (the first by name: enough for the script to
  run to its end, since the lights do not count for the flat master); and the part's slice of `flats/`,
  consecutive blocks in file-name order (a `readme.txt` in `flats/` is not a frame).
- The script runs in each part, one after the other, each on its own copy of Siril's settings as on the
  problem card, with the phase, one progress bar over the whole flow, Siril's log and a **Stop** button
  on the tab. Then the parts' masters and the whole master go through the same structure analysis as
  the Structure tab, with the raw of `flats/` for the profile.

The result is **a table with one column per part, one for the whole and one for the profile**: the five
tests, each cell with its value and its verdict pill right after it; the vignetting in stops; **one row
per profile** (every profile the Structure tab shows, Adobe and lensfun), with each column's difference
from it and its verdict, and in the `vignetting profile` column the depth that profile predicts; the
reference profile's row marked; and, on the last row, each column's verdict, the worse of its shape and
its depth against the reference profile (not against the column's own nearest profile, so that the
rows speak of the same profile). The reference profile is **the whole's nearest profile, the same for
every column**: "nearest" is chosen on each master separately, and on the first real session part 1
and the whole had chosen different profiles; a table with another reference per column would compare
nothing. Under the table stand the residual maps, side by side, on a switchable scale.

In the session folder it saves `Split result.txt` (the version banner, the date and time, the table,
what it means) and `flatcheck-split.png`: the parts from left to right, then the whole, **on one colour
scale** (the automatic one, over all the maps at once), each with its label, and with the version, the
date and the scale's ends on the title row, drawn with a bitmap font of its own, with no dependency.

The original session is **only read**: nothing in it is moved or deleted, and only the two files above
are written, overwritten without a question, like `Test result.txt`. The temporary folder is deleted
after the analysis and on **Stop** (and when you choose another folder, or close the window during the
run: it asks, stops Siril, deletes, then closes). After a failure (Siril exits with a non-zero code, or
the script leaves no master) it is kept, with its path on screen, so you can see why. The parts'
masters are not kept: the whole master is the one that counts, and the report holds all their figures.

Under the frame count, the tab says **how much disk space** the chosen split needs: the copies exactly,
from the files' sizes, and what Siril will write into `process/`, estimated from a bytes-per-pixel
model measured on 2026-09-28 (a converted frame 2 B/px, a calibrated or stacked one 4 B/px, a
calibrated or registered light 12 B/px; on a quarter of a session the model gives 4.6 GB against 4.35 GB
measured), plus the free space on the drive of `%TEMP%`, in Explorer's units (1 GB = 1024³ bytes), so
the figure is the one Explorer shows. Time is not estimated: it depends on Siril and on the machine. As
an order of magnitude, 40 flats in 4 parts meant ~3 GB copied, ~17 GB of temporary `process/` and ~3
minutes of Siril.

What is large on the parts and small on the whole is what the rotation cancelled: at 180 degrees the
odd part (the gradient), at 90 degrees the saddle too. What the parts share with the whole is fixed to
the camera, or radial, and no rotation removes it. A part has fewer frames, so more noise: the fine
figures (the worst spot) come out larger on the parts without meaning a defect.

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
(not a run you cancelled yourself, nor what the window drops while it closes); at the end the
exit code and the time taken. The log's own words are English whatever `--lang` says; an error's
or a refusal's message appears as you saw it, so in Romanian under `--lang ro`. It holds the
paths of your files and the figures measured on them, not their pixels.

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

The two publishes `scripts/release.ps1` runs (`kit.json` lists them):

```bash
dotnet publish FlatCheck/FlatCheck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish FlatCheck.Wpf/FlatCheck.Wpf.csproj -c Release -r win-x64 --self-contained
```

The last two flags of the first keep `flatcheck.exe` a single file to copy: LibRaw comes as native DLLs
(`raw_r`, `jpeg8`, `lcms2`, `zlib1`, ~2.1 MB), and without `IncludeNativeLibrariesForSelfExtract` they
stay separate files beside the executable. If they get lost (only the `.exe` of a plain publish copied),
the FITS modes work unchanged and a raw is refused with a message naming what is missing; there is no
path by which their absence produces wrong figures. The window is published without `PublishSingleFile`,
which WPF does not pack as cleanly. Both builds and both publishes put `lensfun/` beside the executable.

The gate is `scripts/verify.ps1`, about 3 minutes: the files shared with the starter kit, the test data
manifest, the decision records and the design notes, then restore, the `dotnet format` check, a build
with warnings as errors, and every test but the validation ones. The git hooks run it before every
commit and every merge that touches FlatCheck.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

The golden files, `FlatCheck.Tests/Golden/*.txt`, pin the console's text of every mode, in both
languages. After an intended change of the output, regenerate them and read the diff, which is where
their value is:

```bash
FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln --filter "GoldenOutputTests|EnglishOutputTests"
```

The 39 `parity_*.txt` among them hold the figures the Python prototype confirmed before it was frozen;
they are recaptured only after the owner's approval. The figures measured on the real masters
(`docs/validation.md`) come from the validation tests, outside the gate because they take minutes:
`dotnet test FlatCheck.sln -c Release --filter "Category=Validation"`, with `FLATCHECK_TESTDATA_LARGE`
naming the masters' folder.

Where the rest is written:

- `CLAUDE.md` and `.claude/rules/kit/process.md`: the project's rules and those of the process it
  follows, from the starter kit in the `ClaudeCodeStarterKit` folder beside this one;
- `docs/requirements.md`: the behaviour, with the IDs the tests carry;
- `docs/decisions/`: the decisions, one record each, with the alternatives rejected;
- `docs/design/`: how each area works and why, indexed by `docs/design/README.md`;
- `docs/validation.md`: the figures measured on the real masters;
- `CHANGELOG.md`: the changes, version by version.

`flatcheck.py` is the Python prototype FlatCheck was ported from, frozen at 2.2.0 since 2026-09-26 and
no longer maintained; it stays as prior art, beside `crosscheck.py`, its comparison tool. Its
intentional differences from the C# are listed in `docs/design/python-parity.md`.

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

The tool links four components written by others and ships a database made by others, all
compatible with GPLv3:

| component | role | licence |
|---|---|---|
| Sdcb.LibRaw | the C# wrapper over LibRaw | MIT |
| LibRaw (the native DLLs) | reading camera raw files | LGPL-2.1 (the option chosen of LibRaw's dual LGPL-2.1 / CDDL-1.0) |
| SixLabors.ImageSharp 3.1.x | decoding the JPEG preview a raw carries | Six Labors Split License 1.0, which grants open-source projects the terms of Apache 2.0. Pinned to 3.1.x because from 4.0 the package asks for a licence key at compile time, an error on Release builds. |
| NLog and NLog.Targets.AtomicFile 6.2.x | writing the diagnostic log (see "Logs"), two windows into one file | BSD-3-Clause |
| the lensfun database (the `lensfun/` folder) | the vignetting profiles of mode 6 and of the structure's profile comparison; data, not code, copied unchanged | CC BY-SA 3.0 |

Adobe's profiles (`.lcp`) are not a component: they are read on the machine, where Adobe put them, and
copied nowhere.

The FITS reader, the PNG encoder, every computation and every threshold are written by hand.

Why each licence is compatible, including why LibRaw is taken under the LGPL and not the CDDL, is in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). The window's **About** shows the same list, with each
component's copyright notice.
