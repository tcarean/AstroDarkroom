# DarksCheck

A quality check for the dark frames of a DSLR astrophotography session, made for the author's
Nikon D3300 and a Siril workflow. Give it a Siril session folder or a folder of darks, a
library. It repeats the nine checks of the hand analysis it grew out of, made on the Smida
session of 2026-08-12: the metadata and the cadence, light leaks, thermal drift, noise, hot
pixels, fixed pattern, dead pixels, whether there are enough darks, and the lights against the
darks. Then it computes the index, a number for how warm the sensor ran, on darks, on a library
and on lights, so that a master from a library can be matched to a night. The frames are only
read: nothing in their folders is modified, moved or written.

There are two programs over one core, and they give the same figures: the console,
`darkscheck.exe`, and the window, `DarksCheck.Wpf.exe`. Both speak English, or Romanian with
`--lang ro`.

The figures are checked against the author's sessions (`docs/validation.md`): the darks, biases
and lights of Smida, against the hand analysis; three libraries of 30 darks at ISO 1600, taken
at 15.8, 13.3 and 9.5 °C; a set of darks with a deliberate light leak; and the Veil night of
2026-10-06, whose lights have no darks of their own.

## Getting it

The window is one of the tools of the Astro Darkroom suite, described on the front page of this
repository ([../README.md](../README.md)), whose "Download" section is where the suite's zip is
published; opened from the suite's launcher, it starts in the launcher's language. The console
is not part of the suite: build it from this folder, as "Building" says below.

What it needs: Windows 10 or 11, x64, and memory: a session of 20 darks, 30 biases and 32 lights
of 24 MP takes up to 6 GB. The published executables are self-contained, so there is nothing
else to install, no .NET and no Siril. It reads camera raws (`.nef`, `.cr2`, `.cr3`,
`.arw`, `.dng`, `.raf`, `.pef`) through LibRaw, and FITS files. Its thresholds and figures were measured
on a Nikon D3300 only, and the electrons of the ADU scale are known for the D3300 alone.

## The folder you give it

A Siril session folder, the one with `darks\` and, optionally, `biases\` and `lights\`:

```
2026-08-12-Smida\
    darks\      DSC_0034.NEF ... DSC_0053.NEF     <- required
    biases\     DSC_0042.NEF ...                  <- optional
    lights\     DSC_0002.NEF ...                  <- optional
    flats\      ...                               <- not touched
    process\    ...                               <- not touched
```

or a folder of darks, a library, with the darks in its `darks\` or directly in it:

```
2026-10-10-ISO1600-120s-15.8celsius-idx67\darks\   DSC_0009.NEF ... DSC_0038.NEF
```

A session with `lights\` and no `darks\` (the darks were taken another day, or you keep a
library) is analysed against a partner whose darks stand in for its own: `--against <library>`
in the console, **Choose partner...** in the window (see "A session without darks" below).
Without a partner such a folder is refused with a message that says what to give;
`--inventory` lists it all the same, its `darks` row reading `(none)`.

- An empty `darks\` is an error, never a fall-back to the root of the session, where
  `result.fit` and the other results of the stacking sit. A folder with `biases\` or `flats\`
  but neither `darks\` nor a `lights\` with frames is refused with a message naming the
  subfolder it found.
- The frames of a folder are its camera raws when there are any, otherwise its FITS files
  (`.fit`, `.fits`, `.fts`); the two are never mixed. They are taken in the order of their
  file names.
- `flats\`, `process\` and every other folder are never opened.

## The console

```
darkscheck <session folder or folder of darks> [--against DIR] [--out DIR] [--threads N] [--max-frames N] [--lang en|ro]
darkscheck <folder> --inventory [--lang en|ro]
darkscheck --help [--lang en|ro]
```

- No option: decodes the frames and prints the report (below).
- `--against DIR`: another folder of darks, a library or a session, to compare the session with;
  a session without darks takes its darks as its own.
- `--out DIR`: also writes the report, a CSV file per group of frames and the maps into DIR.
- `--threads N`: how many frames are decoded at once; half the cores by default.
- `--max-frames N`: how many frames are held in memory and how many lights are measured; 60 by
  default.
- `--inventory`: the metadata of the frames only, without decoding them; it does not go with
  `--out` or `--against`.
- `--lang en|ro`: the language of the texts, English by default.
- `-h`, `--help`: the usage and the licence notice.

Every run first prints `darkscheck <version>` and the copyright line. The exit code is `0` when
every frame was read and `1` on an error: a missing folder, a folder without frames, a wrong
option (`ERROR:` on stderr, naming it), or a frame that cannot be read or used (a dark, a bias, a
light or a dark of the partner), which is listed with its error while the others are still
analysed.

`--lang ro` (or `--lang=ro`), anywhere on the command line, the last one counting, gives every
text in Romanian: the usage, the progress, the report and the error messages. `--lang en`, or no
`--lang`, gives English. Another value (`--lang fr`) stops the console with
`ERROR: --lang expects en or ro, got 'fr'` and exit code `1`, in English; the window says it in a
message and goes on in English. The figures, the label and the names of the files written are
the same in both languages; the verdict words are `OK`, `WARNING` and `UNMEASURED` in English,
`OK`, `ATENTIE` and `NEMASURAT` in Romanian.

### The report

`darkscheck <folder>` decodes the biases, if there are any, then the darks, `--threads N` at a
time (the progress goes to stderr: `decoding darks: 3 / 20`), and holds them in memory as 16-bit
mosaics, 48 MB a frame at 24 MP. Above 60 frames, or the cap `--max-frames N` sets, it decodes
one file in k and lists the others as `skipped`, which is not an error. Then it reads the lights
one by one, under the same cap, without keeping them. It prints the session's lines, one section
per check, each with its figures and a `verdict` row that starts with `OK`, `WARNING` or
`UNMEASURED`, then `INDEX`, the frames table, the lights table and `ADU SCALE EXPLAINED`.

On the author's computer (8 cores, 4 threads), the Smida session, 20 darks, 30 biases and 32
lights, takes 63 to 65 seconds and up to 5.9 GB of memory; the library of 30 darks, 24 to 26
seconds and 3.3 GB (measured 2026-10-25). The library as a partner adds about 27 seconds
(2026-10-16).

The figures are in ADU above the black. When the darks are not at ISO 400 and 120 s, the
thresholds and the figures marked `ADU-eq` are divided by the factor ISO / 400 x exposure /
120 s, so that they mean the same at any setting: at ISO 1600 and 120 s, 1 ADU-eq is 4 ADU. The
`normalisation` row gives the factor. A threshold never goes below 5 raw ADU, though, so that a
short series or a low ISO does not count noise as hot pixels; the dead-pixel threshold (the
black - 50 ADU) and the figures of the biases are in raw ADU, since they do not depend on the
exposure.

A frame whose size differs from the first frame's, or whose ISO or exposure does (the exposure
by more than 2 %), is left out of the figures and listed as an error
(`error: different size: ...`, `error: settings differ from the first frame: ...`), so the exit
code is `1`. A single dark is not a series: checks 3, 4 and 8 and the stability of check 5 stay
`UNMEASURED`.

The start of the report on Smida:

```
=== DARKS: 20 frames decoded of 20, 6016 x 4016 px, scale 150 .. 4095 ADU (libraw (exact)), CFA RGGB ===
    normalisation      : ISO 400, 119.2 s -> x0.99 relative to ISO 400 and 120 s (1 ADU-eq = 0.99 ADU)
    quantisation       : step 2 ADU near the black; 44 distinct values in the first frame (G1)

  2. LIGHT LEAKS AND GRADIENT
    master dark        : centre -0.55, corners -0.44 / -0.49 / -0.10 / -0.31 (TL / TR / BL / BR), edges top -0.48 bottom -0.18 left -0.41 right -0.50  [G1, ADU-eq above the black, mean of 20 frames, blocks of 32 px]
    master bias        : centre -0.68, ...
    dark - bias        : centre 0.13, corners 0.22 / 0.17 / 0.15 / 0.13 (TL / TR / BL / BR), ...
    light per frame    : no frame above 5 x the typical deviation (0.10 ADU-eq) and above 0.1 ADU-eq from the line of the mean against the warming
    glow / constant    : at the top-left: glow +0.05 (grows with the warming), constant +0.05, of which in the bias +0.02, leaving +0.04 ADU-eq (raw: glow +0.05, constant +0.05 ADU)
    constant light     : after the bias, against the centre: top +0.04 (±0.05), bottom -0.01 (±0.02), left -0.03 (±0.02), right +0.08 (±0.03) ADU-eq; none above 0.15 and above 3 x the error; ...
    verdict            : OK: no edge or corner more than 1.0 ADU-eq from the centre (the farthest +0.09 at the top-left, on dark - bias); no frame with light above the series' warming; no side with constant light above 0.15 and 3 x the error

  3. THERMAL DRIFT IN THE SERIES
    amplitude          : the hot pixels at 0.83 of the master in the first frame, 1.07 in the last (443 pixels above 20 ADU-eq)
    plateau            : the series approaches a plateau at 1.10 of the master (time constant 15 min); the master, the series' median, is 0.8 degrees below the plateau; at the last frame it is still +0.2 degrees from the plateau
    to discard         : 2 frames (1, 2) more than 2.0 degrees from the plateau (ampl. below 0.87 or above 1.38; the plateau 1.10); without them the master would be 0.7 degrees below the plateau, but with 5 % more noise
    verdict            : OK: the series settled (at the last frame it is still +0.2 degrees from the plateau, under 1.0) and every frame lies in 0.7 .. 1.4 of the master; the warming of 2.2 degrees in 38 min is at the start (the doubling rule at 6 degrees), the frames to discard are in the row above
```

#### 2. Light leaks and gradient

The dark master (the mean of the frames), the bias master and their difference, the dark
current, each in blocks of 32 px, read at the centre, in the four corners (TL top-left, TR
top-right, BL bottom-left, BR bottom-right) and on the four edges, on the green plane G1. The
verdict reads `dark - bias` when there are biases (an offset that is in the bias too is the
readout, not light), otherwise the dark master: `OK` when no region departs from the centre by
more than 1 ADU-eq, `WARNING` naming the region that departs most otherwise. A brighter region
gets its cause: the sensor's glow, a light leak, a readout offset, or undecided without biases;
a darker one is a readout or bias offset.

Three rows tell where light comes from, on the principle that the glow is dark current and grows
as the series warms (the hot pixels' amplitude of check 3), while light from outside does not:

- `light per frame` fits every frame's mean on the warming (a robust Theil-Sen line) and names
  the frames with more light than the line, over 5 times the typical deviation and over 0.1
  ADU-eq. Such frames (a lamp, the Moon, the dawn) make the verdict `WARNING`, with the advice
  to take them out of `darks\`, or to redo the series when every frame carries light too.
- `glow / constant` splits the departure of the verdict's region into the part that grows with
  the warming, the glow, and the constant part. With biases, the constant part is split again
  into what is in the bias (a readout offset) and what remains (a leak); without biases a
  constant leak and a readout offset look the same, and the row says so. When the normalisation
  is not 1, the row ends with the raw ADU in brackets.
- `constant light` does the same on the four sides (an edge with its two corners), where a leak
  through the eyepiece or the mount leaves a band. With biases, what remains on a side after the
  bias, over 0.15 ADU-eq and over 3 times its error, is light in every frame, which the
  per-frame test cannot see (it sees only what changes from one frame to the next), and the
  verdict names it: `constant light in every frame: +0.20 ADU-eq (±0.05) on the top side against
  the centre, not in the bias: a light leak (eyepiece, mount)`. A uniform light over the whole
  frame, in every frame, has no reference, and the row says so.

The three need at least 6 frames with hot pixels, and the split also needs a series that warmed
(an amplitude ratio that spans at least 0.1; the author's series span 0.24 to 0.38);
otherwise they say why they cannot. Every light found is also given in raw ADU as a fraction of
the dark master's noise per pixel (check 8), and of the lights stack's when there are lights:
`+0.79 ADU is 0.55 times the dark master's noise per pixel (1.43 ADU)`. On the D3300 the glow is
at the top-left, 0.05 ADU-eq on Smida and 0.22 in the library at 15.8 °C. The test set with the
eyepiece uncovered and a lamp aimed at the camera (2026-10-20) reads 5 frames at +0.17 to +0.47
ADU-eq and a top side at +0.20, which the verdict on the master alone had read as `OK`.

#### 3. Thermal drift in the series

The amplitude of the master's hot pixels in every frame, against the master; the ratio of the
last frame to the first gives the degrees by the rule "the dark current doubles every 6
degrees". A camera warms itself as it takes frame after frame, on a curve that approaches a
plateau: started cold, it begins about 10 degrees under the plateau and nears it with a time
constant of 15 min outdoors (Smida) and 18 to 28 min indoors (the libraries). That is 2.2
degrees in 38 min on Smida and 2.8 to 3.6 degrees in 58 min on the libraries, even after 20
minutes of frames thrown away first.

From 6 frames up, `plateau` fits the curve and says where the plateau is, how far under it the
master sits (the median of the series, 0.8 to 1.4 degrees on the measured series) and how far
the series still had to go at its last frame (signed: + still rising, - falling). `to discard`
names the frames more than 2 degrees from the plateau (the cold start, with a dark current more
than 21 % under the plateau's; 3 to 5 of 30 on the libraries, 2 of 20 on Smida), says where the
master would be without them and how much its noise would grow (the square root of the frames
over the frames left). You move them out of `darks\` yourself, since the program writes nothing
into the folder, and you leave them when the noise costs more than the half degree is worth.

The verdict is `WARNING` only when the series is still climbing at its last frame (more than 1
degree to go: the master belongs to no temperature, continue the series) or when a frame leaves
0.7 to 1.4 of the master (the margin of Siril's `-opt`: the series started cold); the warm-up at
the start is only stated. Under 6 frames no plateau is fitted and the verdict rests on the 0.7
to 1.4 margin alone. In the frames table the column `plateau` gives every frame's degrees from
the plateau, and a frame to discard has its reason at the end of its row; in the window, the
Frames tab shows that row, the value and the reason in the attention colour, and the chart has
the plateau and the two bounds.

#### Checks 4 to 8

- **4. Noise per frame**: the sigma of the residual (frame minus master) on every plane; the
  read noise from pairs of consecutive biases, and the temporal noise above it (the dark current
  and the quantisation, from the frame-against-frame sigma of check 8); the random banding, the
  spread of the row and column means against what white noise would give. `WARNING` lists the
  frames whose noise is above 1.5 times the median.
- **5. Hot pixels**: how many pixels of the master are above 10 / 20 / 50 / 100 / 500 ADU-eq,
  per plane at 20; their amplitudes (median, p10, p90, p99); how many stay above half the
  threshold in every frame and how many flicker; the temporal sigma of the hot pixels against
  the rest; the RTS candidates, pixels whose value jumps from one frame to the next.
  Informative.
- **6. Fixed pattern**: the master's structured banding (the row and column profiles minus their
  running median, rms and peak to peak, on the worst plane) and the glow (the part of check 2's
  departure that grows with the warming, or the whole departure when it cannot be split).
  `WARNING` above 2 ADU-eq peak to peak or 1 ADU-eq at the edges.
- **7. Dead and saturated pixels**: on the master, below the black - 50 ADU and at the white;
  any such pixel gives `WARNING`.
- **8. How many darks are enough**: the noise of the mean of n frames against sigma / sqrt(n)
  (the frames are independent when the two agree; the point n = 1 is the temporal sigma, frame
  against frame, without the fixed pattern), and the predicted noise of the median master,
  1.25 x the temporal sigma / sqrt(N), against the file's quantisation step. Under 10 frames,
  `WARNING`: the median needs them, so that one odd frame cannot pull it. Above the step,
  `WARNING` with the number of frames that would bring it under. Otherwise `OK`: the master's
  noise adds in quadrature to every calibrated light, and under the step it is as small as the
  file allows.

#### 9. Lights against the dark

In every light the hot pixels are found locally: a pixel more than 6 sigma above the median of
its 8 neighbours of the same colour, under saturation, none of whose immediate neighbours passes
3 sigma (a star lights its neighbours, a hot pixel does not). A pixel found in at least 80 % of
the lights is persistent. The section says how many persistent pixels the master has above 10 /
20 / 50 / 100 ADU-eq and how many of the master's hot pixels show in the lights, both ways; how
many persistent pixels are not in the master (`unmatched`: a star that did not move between the
frames, or a pixel the dark does not have); and the amplitude: how strong the master's hot
pixels are in every light against the master, normalised for ISO and exposure, the median over
the night with the first and the last light. The first lights of a night run cold: on Smida the
first reads 0.72, and the lights from the 13th minute on read 0.92 to 1.07.

The verdict, on the lights / dark ratio:

| Ratio | Verdict | What it means |
| --- | --- | --- |
| 0.9 to 1.1 | `OK: direct subtraction` | the dark is at the night's temperature |
| 0.7 to 1.4 | `WARNING: subtract with -opt` | Siril's `-opt` scales the dark to the lights |
| 0.4 to 0.7 | `WARNING: cosmetic correction only (-cc=dark)` | the dark is much warmer: it finds every hot pixel, but subtracting it would overcorrect |
| other | `WARNING: mismatched` | too cold or too warm |

The degrees come from the doubling rule at 6 degrees. A ratio around zero (the lights do not
show the master's hot pixels at all: in-camera noise reduction, another camera, a FITS turned
another way) is `mismatched`, with its own sentence. A light that does not say its ISO or its
exposure is taken at the darks' settings, with a `WARNING` line that counts such lights.

#### The index and the label

`idx<NN>` is the median amplitude, in ADU-eq, of the master's pixels ranked 5 to 20 per
megapixel (the 122nd to the 483rd strongest of 24.2 MP): one number for how warm the sensor ran.
Smida reads 25; the author's three libraries read idx67, idx47 and idx33 at 15.8, 13.3 and 9.5
°C, one halving for 6.3 degrees, the doubling rule within a degree.

`label` is the folder's name plus `-idx<NN>` (the parent folder's when the darks sit in
`darks\`). It is only shown: you rename the folder if you wish, and the program renames nothing.
A folder already renamed to a label (its name ends in `-idx` and digits, or in the `-idx-` of an
unmeasured index) keeps one suffix: the old one is dropped before the new one is added, when a
name remains in front of it, so `...-15.8celsius-idx67` reads `...-15.8celsius-idx67`, not
`...-idx67-idx67`, and a changed index shows as a changed number. With lights, the `lights` row
gives the night's equivalent index (the darks' index times the lights / dark ratio) and its
degrees against the dark.

The end of the report's sections on Smida:

```
  9. LIGHTS AGAINST THE DARK
    hot pixels         : 86 pixels persistent in the lights (isolated, above 6 local sigma, in at least 26 of 32 frames)
    in the dark master : above 10: 85 of 86 (99 %); above 20: 85 of 86 (99 %); above 50: 70 of 86 (81 %); above 100: 16 of 86 (19 %)  [ADU-eq]
    master in lights   : above 10: 85 of 2283 (4 %); above 20: 85 of 443 (19 %); above 50: 70 of 111 (63 %); above 100: 16 of 24 (67 %)
    unmatched          : 1 persistent pixel in the lights under 20 ADU-eq in the master (their median in the master 3 ADU-eq)
    amplitude          : lights / dark 1.00 over the night (median over 32 frames; the first 0.72, the last 0.94; 443 pixels)
    verdict            : OK: direct subtraction (ratio 1.00, in 0.9 .. 1.1; the dark is at the same temperature)

  INDEX
    darks              : idx25 (25.2 ADU-eq: the median of the pixels ranked 5-20 per Mpx, ranks 122 .. 483 of 24.2 Mpx, normalised to ISO 400 and 120 s)
    label              : 2026-08-12-Smida-idx25
    lights             : equivalent idx25 (dark x 1.00, that is +0.0 degrees against the dark)
```

#### The tables

- **FRAMES TABLE**, one row per dark, on G1: the time, the mean above the black, sigma, the
  residual sigma, the banding (times the white noise), the hot pixels' amplitude against the
  master, `plateau` (degrees from the plateau of the series), `light` (ADU-eq above the line of
  the mean against the warming; `-` under 6 frames), `side` (the side with the most constant
  light against the centre in that frame, after the bias when there are biases, glow included,
  in ADU-eq; the note names the side, and only its constant part is light), and the pixels above
  20 ADU-eq. A frame the cap skipped reads `skipped (...)`, one that could not be read
  `error: ...`.
- **LIGHTS TABLE**, one row per light, on G1: the time, the sky above the black, the local sigma
  (the scale of the detector), the candidates, the isolated pixels, and the amplitude against
  the master with its quartiles.

```
  FRAMES TABLE
     no  file                  time       mean   sigma  resid.   bands    ampl. plateau   light    side  >20   [G1; mean = ADU above the black; ...
      1  DSC_0034.NEF          00:48:53   -0.45    1.46    1.47     4.3    0.826    -2.5   +0.02   +0.03   115   to discard: ampl. 0.826 below 0.87 (more than 2.0 degrees below the plateau 1.10)
      2  DSC_0035.NEF          00:50:53   -0.47    1.46    1.47     4.3    0.846    -2.2   +0.00   +0.02   110   to discard: ampl. 0.846 below 0.87 (more than 2.0 degrees below the plateau 1.10)
      3  DSC_0036.NEF          00:52:53   -0.41    1.48    1.49     5.0    0.900    -1.7   +0.05   -0.03   125
```

#### ADU scale explained

The last section of the report, and the window's tab of the same name: what one ADU is worth on
the open set, in raw ADU, ADU-eq and electrons. Its rows give the file's scale, the
normalisation, the quantisation step, the read noise, the noise per frame and the dark current,
the noise of the masters, the set's thresholds in raw ADU, what check 2 found, the sky of a light
(with lights) and the saturation; four paragraphs then say why a fraction of an ADU in the master
matters, with the set's own numbers. The yardstick is the noise left in the masters: the row
`master noise` gives the per-pixel noise of the dark master (check 8's) and of the lights stack,
with the mean and with the median, and every light check 2 finds is also written as a fraction
of them. In the window the masters' noise and the light found are in bold.

The electrons come from a published source, not from a measurement of the program:
photonstophotos.net (Bill Claff), from the DxOMark curves, for the Nikon D3300 (gain 2.07 e-/ADU
at ISO 400 and 0.53 at ISO 1600, full well 34446 e-, QE 56 %), with the source's warning that its
method overestimates the gain on small sensors. For another camera the row reads
`unknown for <camera>`, and the first paragraph ends by saying that the camera is not in the
application's table, so no figure of the section is in electrons and the source's full well and
read noise are missing.

### Comparing with a partner: `--against`

`--against DIR` compares the session's darks with another folder of darks, a library or another
session: its darks are decoded the same way and their median master is compared with the
session's. `INDEX` then adds:

- `partner`: the partner's folder, index, frames, settings and label;
- `darks / partner`: the ratio of the session's darks to the partner's on the pixels hot in both
  masters (above 20 ADU-eq in each), with its quartiles, the pixel count and the degrees;
- `shared pixels`: how many of the session's pixels above 50 ADU-eq the partner has above 50,
  and how many of the partner's above 50 the session has above 20;
- `lights / partner`: the lights against the partner, through the session's darks;
- `partner verdict`: check 9's thresholds on that ratio (without lights, on the darks' ratio).

On Smida against the library, 0.42 on 434 pixels: the library ran 7.5 degrees warmer, so it is
good for a cosmetic correction only.

```
    partner            : E:\Photografie\Astro\Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius-idx67: idx67 (67.4 ADU-eq; 30 frames of 30, ISO 1600, 120.2 s); label 2026-10-10-ISO1600-120s-15.8celsius-idx67
    darks / partner    : 0.42 (q25 0.38, q75 0.44, 434 shared pixels) after normalisation, that is the session's darks run -7.5 degrees against the partner
    shared pixels      : 111 of the session's 111 above 50 ADU-eq are above 50 at the partner too (100 %); 425 of the partner's 528 above 50 are above 20 in the session (80 %)
    lights / partner   : 0.42 (through the session's darks), that is the lights run -7.5 degrees against the partner
    partner verdict    : WARNING: cosmetic correction only (-cc=dark): ratio 0.42, the partner is warmer by 7.5 degrees and still finds every hot pixel, but subtracting it would overcorrect
```

A partner folder that cannot be opened is refused with `ERROR` before anything is decoded. One
whose frames have another size, or whose darks do not decode, gives a `WARNING` row and exit
code `1`, and the rest of the report stays; a dark of the partner that cannot be used is counted
in a `WARNING` line, also with exit code `1`. `--out` cannot point inside the partner's folder
either.

**A session without darks.** With a partner, the partner's darks take the place of the
session's: checks 2 to 8 run on them (so you also learn how good the library is), check 9
compares the lights with their master, and the index and the label are the partner's. The report
says so: its `darks` row reads `(none; the darks of the partner ... are used)` and its darks line
`=== DARKS (the partner's, ...)`; the rows `partner`, `darks / partner`, `shared pixels` and
`lights / partner` are left out, since there is nothing left to compare the partner with; and
`darkscheck-darks.csv` and the maps of `--out` describe the partner's darks. The Veil session of
2026-10-06 (87 lights, no darks) against the library reads lights / dark 0.43 over the 44 lights
measured (the cap of 60 takes one in two): the library ran 7.2 degrees warmer than the night,
good for a cosmetic correction only.

### FITS frames

A FITS file does not carry the black level (a FITS converted by Siril keeps the camera's
pedestal, 150 on the D3300), so for FITS frames the black is the median of the first bias when
there are biases, otherwise of the first dark; the `=== DARKS` line says where it comes from. Nor
does it carry the white: the saturation of a FITS is read at the file's own white (65535 at 16
bits), so a 12-bit camera that saturates at 4095 inside a 16-bit FITS does not show as
saturated.

### Writing the files: `--out`

`--out DIR` writes into DIR the report (`darkscheck-report.txt`), a CSV file with every figure
per frame and per plane (`darkscheck-darks.csv`, `darkscheck-biases.csv`, and
`darkscheck-lights.csv` when the session has lights) and the maps of check 2 on the G1 plane, as
PNG files (`darkscheck-map-dark.png`, `darkscheck-map-bias.png`,
`darkscheck-map-dark-minus-bias.png`; one block is a 4 x 4 square, and each map is stretched
between its own 1st and 99th percentiles). It prints `-> written: <path>` for each file. The
names are the same in both languages. Nothing is written into the session: a DIR that is the
session folder, or a folder inside it, is refused with `ERROR` before the analysis starts.

### Only the metadata: `--inventory`

Reads the metadata without decoding the pixels: under three seconds for the 82 raws of Smida.

```
=== SESSION: E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida ===
    darks              : darks\ (20 frames .NEF)
    biases             : biases\ (30 frames .NEF)
    lights             : lights\ (32 frames .NEF)
    camera             : Nikon D3300
    darks exposure     : 119.2 s at ISO 400 (all frames)
    biases exposure    : 0.05 s at ISO 400 (all frames)
    lights exposure    : 119.2 s at ISO 400 (all frames)
    darks interval     : 2026-08-13 00:48:53 -> 2026-08-13 01:28:52 (39 min); 20 frames every 120.0 s; no gap over 5 s
    lights interval    : 2026-08-12 23:22:25 -> 2026-08-13 00:45:41 (1 h 23 min); 32 frames every 151.0 s; 31 gaps over 5 s (the longest 125 s before frame 26)
    match              : OK: the darks have the lights' exposure (119.2 s) and ISO (400)
    bias               : OK: 0.05 s

  DARKS
     no  file                  time      exposure   ISO     gap
      1  DSC_0034.NEF          00:48:53     119.2    400       -
      2  DSC_0035.NEF          00:50:53     119.2    400     1 s
```

- `<group> exposure`: the group's most frequent exposure and ISO; when not every frame has them,
  it says how many do: `(86 of 87 frames; different exposures or ISOs)`.
- `<group> interval`: from the start of the first frame to the end of the last, how many frames
  have a time and an exposure, the median interval between two starts (the cadence), and the
  gaps over 5 s between the end of one exposure and the start of the next. Darks taken frame
  after frame have none; a gap means Live View, a menu or the camera left alone, so the sensor
  cooled.
- `match`: `OK` when the darks have the lights' exposure (within 2 %) and ISO, `WARNING` with
  both settings when they do not, `UNMEASURED` without lights.
- `bias`: `OK` with the exposure when the biases are at most 1 s, `WARNING` above, `(none)`
  without biases.
- `gap`, in the tables, is the time from the end of the previous exposure to the start of this
  one. The time comes from the EXIF of a raw (the camera's clock) and from the `DATE-OBS` of a
  FITS (UTC, as Siril writes it).

## The window

`DarksCheck.Wpf.exe [--lang en|ro] [folder]`. The language is chosen as in the console, English
by default. Without a folder it asks for one at start: a Siril session folder (the one with
`darks\`) or a library of darks. The analysis starts by itself, the same as
`darkscheck <folder>`, with a progress bar and a **Stop** button, and the result fills eight
tabs:

- **Summary**: the session's lines, then every check as a card of rows, its verdict a coloured
  chip (green `OK`, amber `WARNING`, grey `UNMEASURED`) with the rest of the text beside it.
- **Frames**: the frames table, every column header with an explanation under the mouse, and
  beside it three charts, frame by frame: the hot pixels' amplitude against the master (the
  warming, with the plateau and the two bounds of check 3), the residual sigma (with the line at
  1.5 times the median) and the random banding. A frame to discard shows its `plateau` value in
  bold and its reason on a second line, in the attention colour.
- **Maps**: the three maps of check 2 (master dark, master bias, dark - bias), the same as the
  PNG files of `--out`, each with its row of the report under it.
- **Hot pixels**: the rows of check 5 and the curve of check 8, the noise of the mean of n
  frames against sigma / sqrt(n); hold the mouse over a point to see its figures.
- **Lights**: the rows of check 9, the lights / dark ratio of every light with the thresholds
  as lines (0.9 and 1.1 green, 0.7 and 1.4 amber, 0.4 red), and the lights table.
- **Index**: the label in large type with **Copy label** (it goes to the clipboard, and you
  rename the folder yourself), the INDEX rows, and the partner: **Choose partner...** (a library
  or another session), **No partner** and **Analyse again**. A partner chosen after a run
  applies to the next one. A session without darks opened without a partner shows the problem
  card with **Choose partner...**: choose the library and the analysis runs again with its
  darks.
- **ADU scale explained**: the rows and the paragraphs of the report's last section.
- **Report**: the console's text, the same to the byte.

With `--lang ro` the tabs are Rezumat, Cadre, Harti, Pixeli fierbinti, Lights, Index, Explicatii
scara ADU and Raport.

The top bar holds four settings: `THREADS`, how many frames are decoded in parallel (empty =
half the cores); `MAX FRAMES`, how many frames are held in memory and how many lights are
measured (60 by default); `GAIN / ISO STOP`, how much the signal grows for one stop of ISO, for
the normalisation to ISO 400 (2 = linear, the D3300's); and `DEGREES / DOUBLING`, how many
degrees double the dark current, for the degrees in the report (6). The four and the partner are
read when a folder is opened and remembered between starts in
`%LOCALAPPDATA%\DarksCheck\settings.json`, the only file the window writes outside the folder
you export to; a value that is not a positive number with `.` as its decimal separator means the
default, and the field says it is invalid. The console always runs with a gain of 2 and 6 degrees
per doubling.

**Write the files...** writes the files of `--out` into a folder you choose; the dialog opens in
the session folder, which is refused, as is the partner's, so choose a folder outside both.
**About** (F1) shows the version, the licence and the third-party components.

## Building

Windows 10 or 11 (x64) and the .NET 9 SDK, 9.0.318 or a later 9.0.3xx (`global.json`); the
projects target .NET 8, which that SDK builds. From this folder:

```
dotnet build DarksCheck.sln -c Release
```

The full gate, the one every commit runs (format check, strict build, tests):

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

The two executables, each one self-contained file with the libraw DLLs inside:

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 1.0.0 -PublishOnly
```

writes `artifacts\1.0.0\console\darkscheck.exe` (67 MB) and `artifacts\1.0.0\wpf\DarksCheck.Wpf.exe`
(141 MB) and changes no tracked file; the `.pdb` and `.xml` files beside them are not needed to
run them. `-Version` only names the folder: the version the programs print is `<Version>` in
`Directory.Build.props`.

## For developers

The rules of the work are in `CLAUDE.md` and `.claude/rules/kit/process.md`; the behaviour, with
IDs, in `docs/requirements.md`; the decisions in `docs/decisions/`; how the code works, area by
area, in `docs/design/`; the figures measured on the real sessions in `docs/validation.md`; the
history of the versions in `CHANGELOG.md`. The analysis the tool grew out of, the Python scripts
and the report on the Smida session of 2026-08-12 and the library of 2026-10-10, is in
`prototype/` and `docs/validation/2026-08-12-smida/`. This file is the user documentation; the
comments in the code do not repeat it.

## Licence

DarksCheck is free software, under the GNU General Public License version 3 or, at your option,
any later version: see [LICENSE](LICENSE). Its raw and FITS readers are copied from LightsCheck,
which copied them from FlatCheck. What the programs ship from others is listed, each with its
licence and why it is compatible, in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
