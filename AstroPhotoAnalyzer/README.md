# AstroPhotoAnalyzer

A print-readiness check for a finished astrophoto. Open the stretched image you would send to
print, a TIFF, PNG or FITS as it comes out of Siril, and it tells you, in the order the decisions
are made, how to send it: the file format, whether the background survives the paper's black,
the colour cast and the gradients, the largest print size the real detail supports from the
distance it will be seen at, and how much denoise the print version needs. Every recommendation
comes with the figure it measured and the threshold it judged it by.

It measures and recommends; the export itself is made cleanly from Siril. Besides its report and
its log (see "Logs") it writes one file, and only when you ask for it: from the window's
**Curves Editing** tab, a new FITS with a suggested contrast curve applied. The image you open is
never modified.

There are two applications over one core, so both give the same figures: the window,
`AstroPhotoAnalyzer.Wpf.exe`, with a dark theme, and the console, `AstroPhotoAnalyzer.exe`, which
prints the report as text. Both speak English, or Romanian with `--lang ro`; the words change,
the figures do not.

## What it needs

- **A finished, stretched image**: a TIFF, PNG or FITS (`.fit`, `.fits`, `.fts`), or a JPEG; the
  extension decides how the file is read. A linear image, a stack before its stretch, is refused
  with a message instead of being measured wrong: the median of a stretched image sits around
  0.08 to 0.25, that of a linear stack around 0.001 to 0.01, and under 0.02 the tool stops. The
  pixels are measured as sRGB; an embedded ICC profile is named in the report, not applied.
- **The paper's Dmax**, the depth of its black, which decides how dark a tone can be and still
  print apart from black. It comes from the print medium, baryta (Dmax 2.4), canvas (1.5) or a
  C-print under acrylic (`acrilic`, 2.2); or from your paper's own Dmax; or, best, from the ICC
  profile of the paper on the printer that will print it, which a lab usually publishes. The same
  paper on another printer has another black, so the profile of the pair is the one that counts.
- **Optionally, a plate-solved FITS**, so that the dark nebulae in the frame are found by name.
  Siril writes the result of plate solving, the WCS, into the FITS header. A TIFF or PNG exported
  from Siril carries no header, but the stack it came from does: give that FITS with `--wcs`, or
  in the window's "WCS from a FITS" field. It must have exactly the image's size.

## Getting the programs

The window comes with Astro Darkroom, the suite of this repository's five tools, one zip with
the .NET 8 runtime inside; the repository's front page, [`../README.md`](../README.md), says where
to download it and how to build it from a checkout. The console is not in the suite: its runtime
and the windows' carry DLLs with the same names and different contents, so they cannot share one
folder. Build it from this folder, as "Building" below shows.

## The window

```bash
AstroPhotoAnalyzer.Wpf.exe "E:\Astro\image.tif"
```

```bash
AstroPhotoAnalyzer.Wpf.exe --lang ro "E:\Astro\image.tif"
```

The window opens maximized. An image given on the command line is analysed at once; without
one, the dialog to open one comes up. The interface is in English without `--lang`; `--lang ro`
(or `--lang=ro`), anywhere among the arguments, puts it in Romanian, and `--lang en` in English.
An unknown value is said in a message, and the window opens in English. Started from the Astro
Darkroom launcher, it speaks the launcher's language.

**On the left, the parameters**, then **Analyse**:

- **Print medium** and **Dmax**. The Dmax field shows the value the analysis will use: choosing
  a medium writes that medium's Dmax into it, and you can type your paper's over it (emptied, it
  means the medium's again). **From ICC profile...**, beside it, lists the printer profiles
  installed in Windows, each with its Dmax, and **Choose file...** adds a `.icc` or `.icm` file
  from anywhere; **Use** writes the chosen profile's Dmax into the field and, with an image
  open, runs the analysis again at once. The profile in use is shown in the top bar, under the
  image's name, with its Dmax; **Clear** beside it drops it, puts the medium's Dmax back and runs
  the analysis again. Typing another Dmax over the profile's, or choosing a medium, drops it
  too (the bar says so; those apply at the next **Analyse**). The medium keeps its name, and
  Appendix F of the report names the profile the Dmax came from. The chosen profile is
  remembered in `%LOCALAPPDATA%\AstroPhotoAnalyzer\settings.json` and is still in use when the
  window is next opened; if its file has gone meanwhile, the bar says so in red and the medium's
  Dmax is used until you choose another or press **Clear**.
- **Viewing distance** (empty: 1.5 x the diagonal of each print size), **candidate widths**,
  **target aspect ratio** (empty: the image's own, no crop), **extra target background** (one
  more row in the background simulation), **WCS from a FITS**, **passepartout** and **frame**.
  Hover over the distance, the aspect ratio, the target background, the WCS, the passepartout or
  the frame to read what it means, where its default comes from and when to change it.
- Numbers take a decimal point, whatever the language of Windows. The parameters apply to the
  next analysis.

Give the viewing distance when you know it. Left empty, it is 1.5 x the diagonal of each size,
and no size is recommended: a star's size on paper and that distance both grow with the print, so
the test answers the same at every size, and the report only says up to which width the image is
equally sharp from its natural distance. A starless image has no stars to measure: its sizes are
judged by the dpi alone (180 dpi without a distance). An image counts as starless with fewer than
20 stars, or fewer than 10 per megapixel: a strong denoise can leave a few dozen soft blobs on a
starless frame, too few to judge the detail by. A star must stand 12 times the local noise
over its surroundings, so the grain of a stretched sky is not counted as one; faint stars are
left out with it, and the resolution is the bright stars'.

**On the right, seven tabs.** Most figures have a line under them saying what they measure and,
where one applies, the threshold they are judged by. A screen reader such as Narrator reaches
what each tab shows, not only the tabs' names.

| Tab | What it shows |
| --- | --- |
| **Report** | The decisions, in the report's order, each with its verdict chip and its "because:" line; the whole text report behind a check box. |
| **Tones** | The L\* histogram, with paper black as a red line and the visibility threshold L\* 15 as a dashed yellow one; the tonal figures, the background level among them; the background simulation, with a **Preview** button on every row; under it, the definition of the dust and the sky used on this file, a red line when the file has no WCS and the **Dust / sky map** button; the tab's landmarks in level and in L\*; how to read the tab. |
| **Export** | The JPEG table, with what its columns mean and, when a quality under 100 spoils the stars and the edges, which one and by how much; the size of the 8-bit TIFF; the 16 to 8 bit rounding; the shadow-lift stress test, marked when the 16-bit master is worth keeping; the plateau, how wide the flat steps of the 8-bit sky would print and on what share of the frame, which decides the banding risk. |
| **Resolution** | The stars' FWHM, in the centre and in the four corners, and the real detail it leaves; the table of print sizes; the scale of dpi against viewing distance. |
| **Colour** | The background colour, R / G / B, taken on the sky patches, with its chroma in L\*a\*b\*, which decides the cast, and the two level figures that name its direction; the four corners and the four edges against it, each on its own sky pixels, a flagged one marked "! coloured" or "! neutral"; how to read them. |
| **Curves Editing** | Suggested contrast curves, computed with the analysis: see below. |
| **Soft proof** | What the paper would do to the image, through its ICC profile, the way Lightroom soft-proofs: see below. |

**Save the report...** writes `Report.txt` beside the image by default (`Raport.txt` in
Romanian), byte for byte what the console writes with `-o`, and says under the buttons where it
wrote it, or, in red, why it could not (a file another program holds open, a folder that refuses
writing). **About**, or F1, shows the version, the licence and the components the tool uses.

Two windows open from the **Tones** tab, both maximized and both for looking only: they compute
nothing and show what the analysis decided. Esc closes them.

- **Dust / sky map** paints the five classes of 32 px patches over the whole image, each in its
  colour, with its share of the patches and its definition in the legend, the image's own
  thresholds and, for the dust and the sky, the L\* the verdict took; the classes are those of
  "How the background is judged" below. With a WCS it circles the catalogued clouds and writes
  the names of the darkest 15 on the image; the list beside it names them all, each with its L\*,
  its target and its patch count, the one place the window lists the clouds. Every layer has
  its check box, so do the clouds (disabled without a WCS), and the list of clouds has **All** /
  **None** and a check box per cloud. It is made for checking the classes by eye on your own
  images.
- **Preview**, on a row of the simulation, shows the image lifted to that row's background level
  (the "as is" row included). Its two check boxes colour the pixels under paper black and those
  under L\* 15; both start unchecked, so the window opens on the image alone, and each box's
  label gives the share of the pixels in its class either way.

### Suggested contrast curves

The **Curves Editing** tab is filled right after the analysis, so it is ready when you open it:
a gallery of the original, then three or four suggestions, **Gentle**, **Moderate**, **Strong**
and, when the peaks still have room under 0.92 of the scale, **Maximum**, each at the size that
fits the window. Choose **Print** or **Screen** for the evidence under them; **Calculate**
computes the gallery again, at the size the tab has now. Browse them with **< Back** and **Next >**,
or with the Left and Right arrow keys.

The curves are contrast curves: they do not move the dust or the sky, they spread the classes
above them, taking the peaks towards 0.92 of the scale. Their points come from the classes of the
**Tones** tab, on either of its two paths: the dust and the sky as there, then the upper part of
the band and of the bright clouds and the median of the peaks (on the sky path, of the nebula,
the bright nebula and the peaks); the tab names the path, and the points carry the classes'
names, the same as on the map. A sky too dark for paper is not lifted with a curve: lift it at
the stretch, as the **Tones** tab recommends, then come back. When the peaks are already at or
over 0.92 of the scale, no curve is suggested, since these curves lower no point and the image is
already at the ceiling; the tab says so in red, as it does when a class the curves need is empty.

Under the image, each suggestion shows its evidence: the curve's points, the L\* of the five
classes before and after, how many times the noise of the sky grows, and how many pixels end up
clipped. **Print** and **Screen** give the same curves; **Print** adds the paper's checks: a
background too low, the noise over the print target and, when the Dmax came from an ICC profile,
how much of the image falls outside the paper's gamut (a figure only: saturation is corrected
locally, at soft proofing, not with the curve).

Two buttons under the image:

- **Curve...** opens the table of the points (input and output, to three decimals) and two
  charts: the whole curve, and the shadows enlarged, where the dust and the sky sit. The table is
  made for redoing the curve by hand in Siril: *Curves Transformation*, *cubic spline* mode, the
  same points on R, G and B.
- **Save as...** writes a new `.fit` file with the curve applied to the whole image, whatever the
  input's format. From a FITS it keeps every card of the header (WCS, exposure, instrument) and
  the data's encoding, and adds `HISTORY` lines with the suggestion and its points; from a PNG,
  TIFF or JPEG it writes a 32-bit float FITS, without the source's ICC profile. A WCS given in
  the "WCS from a FITS" field serves the analysis only and is not written. The orientation is
  Siril's: a FITS is read with the file's first row at the bottom, as Siril shows it whatever its
  `ROWORDER` says; the new FITS keeps a FITS input's own row order, and from a PNG, TIFF or JPEG
  it is written from the bottom up with `ROWORDER = 'BOTTOM-UP'`, so that Siril shows the new
  file exactly as it was analysed. The input image is never modified, and saving over it is
  refused.

### Soft proofing

The **Soft proof** tab shows what the paper would do to the image, through the ICC printer
profile chosen for the Dmax (**From ICC profile...**); without one it shows only a sentence
saying so. It is modelled on Lightroom Classic's soft proofing and, like it, is for looking only:
nothing is edited, nothing is written. The proof is computed right after the analysis.

- **Intent**: **Perceptual** (the profile's own perceptual table, Lightroom's default),
  **Relative** (the colorimetric table with black point compensation, so the image's black lands
  on the paper's black, as Lightroom's Relative), and a third Lightroom does not have,
  **Relative, no black point compensation**: every tone darker than the paper's black prints as
  that black, which is what the **Tones** tab counts as "under paper black".
- The paper's white shows as its own colour and its black as its own grey, always: the print
  as it will look beside a white. The margin around the image is that white by default, so the
  eye has it to adapt to.
- **Paper gamut warning** paints in red the pixels whose colour the paper does not reproduce, by
  the rule of the **Curves Editing** tab, so its share, in the legend under the image, is the
  same figure.
- **Original** shows the image as it is inside the same margin, for comparison.
- **Margin**: **Paper white** by default; the others are fixed greys, white to black.
- In the tab and in the window, the mouse wheel zooms around the cursor, up to 8:1 at 100 % display scaling, dragging
  with the left button pans, and a double click fits the image again.
- **Full screen** opens the same proof over the whole screen, nothing else: no title bar, the
  margin filling the screen. The controls, with a **Close** button, appear while the mouse is
  over the band they occupy at the top of the screen and hide when it leaves; the keys G and O
  toggle the two boxes, I moves to the next intent, Esc or **Close** leaves the full screen.

Under the image: the profile's name, its paper white in L\*a\*b\* (what the image's white
becomes), its paper black in L\* and Dmax, and the warning's share.
The pixels and the screen are assumed sRGB, as everywhere in the tool; the proof is a reduced
copy. The controls keep their state from one image to the next and start afresh with the window.

## The console

```bash
AstroPhotoAnalyzer image.tif
```

```bash
AstroPhotoAnalyzer image.tif --medium baryta --distance 1.5 --sizes 60,70,80 --aspect 50:35 -o report.txt
```

```bash
AstroPhotoAnalyzer image.tif --lang ro
```

The report goes to the screen, and with `-o` to a file as well. The options may come before or
after the image; `--help` lists them with their defaults, which are the window's too.

| Option | What it sets | Default |
| --- | --- | --- |
| `--medium <name>` | the print medium, which gives the paper's Dmax: `baryta` 2.4, `canvas` 1.5, `acrilic` 2.2 | `baryta` |
| `--dmax <n>` | your paper's Dmax instead of the medium's, over 0 and up to 4 | the medium's |
| `--icc <profile.icc>` | the ICC profile of the paper on its printer, whose Dmax replaces the medium's; not together with `--dmax` | none |
| `--distance <m>` | the viewing distance, in metres | 1.5 x the diagonal of each size, and no size recommended |
| `--sizes <cm,cm,...>` | the candidate print widths, in cm | 50,60,70,80,90,100 |
| `--aspect <w:h>` | the target aspect ratio, such as `50:35` (or `50x35`) | the image's own, no crop |
| `--target-bg <x>` | one more target in the background simulation, besides the 0.05..0.20 grid: strictly between 0 and 1, rounded to 3 decimals | none |
| `--passepartout <cm>` | the width of the passepartout: at least twice the frame, 0 = none | 7 |
| `--frame <cm>` | the width of the frame | 3 |
| `--wcs <file.fit>` | a plate-solved FITS of the same size, whose WCS is used to find the dark nebulae | a FITS image's own |
| `--lang en`, `--lang ro` | the language of every text | `en` |
| `-o <file>`, `--out <file>` | also writes the report to this file | none |
| `-h`, `--help` | the help | |

The exit code follows the overall verdict, the most severe one in the report: `0` OK, `2`
caution, `3` problem, so any code but `0` means "look". `1` is an error: a bad argument or a
bad combination (a passepartout under twice the frame), a missing or unreadable file, an ICC
profile or a WCS file it cannot use, a linear image, a report `-o` cannot write. An error prints
`ERROR: ` and its reason; the codes are the same in both languages.

The report starts with the file and its facts (format, bit depth, pixels, compression, file
size, ICC profile), the medium with its paper black, the viewing distance and the overall
verdict. Six sections follow, in the order the decisions are made: 1. the format to send, 2. the
background and the paper, 3. the background colour and the frame's edges, 4. the size of the print,
5. denoise for the print version, 6. warnings. Each section has its verdict, and each
recommendation a mark, `-` for OK, `!` for a caution, `!!` for a problem, with a `because:` line
under it that gives the figure and the threshold. The appendices A to F follow, with the tables
behind the decisions: tones, the background simulation, export, resolution and size, the
background colour, and the parameters as used. The verdict and the first section on the
reference image, `de_printat.png`, a 23 MP Milky Way frame analysed with the defaults (exit code
2):

```
VERDICT: CAUTION

1. The format to send  [OK]
   - Send the lab an 8-bit TIFF with LZW compression, which loses nothing (20.7 MB). Going from 16 to 8 bits only rounds the values; nothing is lost.
     because: in the rounding a pixel moves by 0.25 of a level on average and by 0.50 at most (a rounding cannot move it by more than half a level)
   - If the lab takes only JPEG: quality 100, with the colour kept at full resolution (the 4:4:4 setting) (12.5 MB).
     because: against the original, the JPEG changes a pixel by 0.60 levels on average and by 4.5 at most; 1.2 % of the values change by more than 2 levels, mostly around level 31 (in the dark sky, where JPEG does worst)
   - At 8 bits no visible steps appear in the sky: the flat areas cover too little of the frame to show.
     because: at 151 dpi, flat areas, with no change of tone, wider than 1.0 mm cover 0.00 % of the frame (threshold 0.10 %); in the 20 small areas of smooth sky (0.4 % of the frame) a flat area is usually 2.7 px = 0.45 mm, and the noise left there is 0.32 levels
```

## How the background is judged

The second decision, the background and the paper, asks whether the darkest parts of the image
keep their tones on paper. The tool looks for the **dust**, the darkest extended structure of the
image, and the **sky**, and recommends the **lowest** background level at which the dust sits on
the paper's black without going under it and the sky reaches the visibility threshold, L\* 15; on
an image brighter than it needs to be, it says how low you can go. Levels run from 0 to 1, as
Siril shows them; L\* is the CIE 1976 lightness, 0 for black and 100 for white.

It measures on patches of 32 x 32 pixels, each read as the L\* of its mean luminance so that noise
and single pixels do not count, and sorts them into five classes on the image as it is, on one of
two paths. The report says which path it took and why, and gives each class's share.

- **The band path**, on a plate-solved FITS (a WCS with `CTYPE` `RA---TAN` and `CD`, `PC` with
  `CDELT`, or `CDELT` with `CROTA2`; SIP terms are ignored) or with `--wcs`. The band of the
  Milky Way is galactic latitude |b| under 10 degrees, and the sky is modelled from the patches
  outside it. A patch is **dust** when it lies 5 L\* under the band's local level (a median over
  about 480 px) or under the modelled sky, that is, darker than the band would be there; a
  **bright cloud** when it lies 10 L\* over the band's local level; a **peak** when it is among
  the brightest 0.1 % of the patches; **band** for the rest of the band, and outside it for a
  patch 10 L\* over the modelled sky; **sky** for the rest outside the band. The path needs the
  band and the sky outside it to hold at least 10 % of the patches each, and a frame spanning at
  least 20 degrees of galactic latitude, the band's width: it is made for wide Milky Way frames,
  where the band is a strip with sky beside it.
- **The sky path**, without a WCS, or on a frame without the band, wholly inside it, or too
  narrow for the band to be more than a gradient (a deep-sky frame such as the Veil, which spans
  11 degrees of latitude). The classes are relative to the sky modelled from all the patches (the
  25th percentile of each cell, so that an object does not lift it), with thresholds taken from
  the image: **nebula** more than 4 sigma of the sky's noise over the model, **dust** as much
  under it, **bright nebula** over the Otsu threshold of the nebula, **peaks** as above. The
  report gives the thresholds in L\*.

On either path the verdict measures the same way. The **sky** is the median of the sky patches.
The **dust** is the darkest extended area: the 25th percentile of the dust patches when the dust
class holds at least 5 % of the patches (fewer are cores or artefacts), or the darkest 1 % of the
sky patches, whichever is darker; the report says which one decided. When the dust class
decides and the dust sits on paper black, three quarters of the dust patches keep tones above
black, and the darkest quarter, cores that look black on screen too, prints flat black.

With a WCS, Lynds' catalogue of dark nebulae, which ships inside the tool, says which clouds are
in the frame and where. Every cloud covering at least 9 patches gets a row with its L\* (the 10th
percentile of its patches) and the background level at which it reaches paper black, darkest
first, so that you can read which clouds stay black at the recommended level.

Besides paper black there is a second question, the viewer's: a sky under L\* 15 (a grey of
level 0.148) prints apart from black, but in ordinary room light it looks almost black, however
deep the paper's black. The report gives it as a caution, and the recommended row has the dust on
paper black and the sky at L\* 15 or above. The threshold is a rule of thumb, not a measurement:
a test strip seen in the light of the room where the print will hang says more.

The section's verdict is a problem when the sky as it is lies under paper black (the sky and the
dust would print as the same black), a caution when only the dust does or when the sky is under
L\* 15, and OK otherwise. Appendix B gives the whole simulation, the grid 0.05 to 0.20 with the
image as it is among its rows; the exact level that puts the dust on paper black, also at a Dmax
0.1 lower and 0.1 higher, since a paper's Dmax on your printer is rarely known better than that;
the Dmax from which the dust as it is reaches paper black (the dust's figure alone: with the sky
under L\* 15 no paper makes the image print-safe as it is, and the report says so beside it); the
level at which the sky reaches L\* 15; and the darkest clouds.

Two cautions on reading it:

- The simulation is a gamma curve on the stretched image. A real stretch to the same background
  level (Siril, VeraLux) keeps the shadows lower, so the exact level is the curve's: read it as a
  lower bound on what the stretch will need.
- Paper black is taken as a clip: everything under it prints as one black. A lab that converts
  with black point compensation squeezes the shadows onto paper black instead, so "prints pure
  black" is then the pessimistic reading.

The other known limits, each with what was measured, are in
[`docs/design/known-limitations.md`](docs/design/known-limitations.md).

## How the colour is judged

The third decision, the background colour and the frame's edges, compares the colour of the sky with
a neutral grey, and the edges of the frame with the sky.

The **sky's colour** is the median, on each channel, of the patches the background section calls
sky (the band, the dust and the nebulae left out), without the bottom quarter of the frame, where
the horizon and its glow sit. Its **cast** is its distance from a grey of the same lightness in
CIE L\*a\*b\*, the chroma: over 3 it is a cast to neutralise before printing, under 3 the
background is neutral. About 2 is the smallest difference an eye sees beside a grey; on a print,
with no grey beside the sky, a cast begins to read at 3 to 5. The two level figures, G - (R+B)/2
and R - B, name the cast green, magenta, warm or cool. A colour calibration on the stars leaves
the sky its own colour, green from airglow, yellow from light pollution, so a faint green after
it is expected and not an error.

The **edges** are four corners, 15 % of the height by 20 % of the width, and the four bands
between them. Each is measured on its own sky pixels, so that the Milky Way or a dust lane
entering a corner does not pass for a gradient; a region with under a quarter of sky is measured
on every pixel, and its row says so. A region more than 3 levels from the sky on some channel is
flagged:

- **Neutral**, its channels moving together: most likely the object; one note asks you to look
  at the edges before you correct anything.
- **Coloured**, its channels moving differently: a caution. Darker than the sky on every
  channel, it is a **darkening**: dust at the edge of the frame or a gradient left over, to look
  at by eye; on the bottom row, where the horizon sits, a region leaning red over blue keeps
  the horizon's name instead. Any other coloured region is named warm (on the bottom row,
  horizon glow or light pollution), cool, greenish or another colour, to correct on the linear
  data or keep as part of the composition.
- An edge flagged with the colour of the corner beside it, and less than it, is that corner's
  glow reaching along the edge: one line, the corner's, with the edge's figures under it.

On a mono image there is no cast, and no region is coloured. Appendix E gives the sky's colour
with its L\*, a\*, b\* and chroma, and the eight regions with their deltas.

## Logs

Both programs keep a log of what they did, for the day something goes wrong. To report a problem,
attach the whole `log` folder: it holds what is needed to see what happened.

**Where it is.** In a folder named `log` beside the program: `log\AstroPhotoAnalyzer.log` beside
`AstroPhotoAnalyzer.exe`, `log\AstroPhotoAnalyzer.Wpf.log` beside `AstroPhotoAnalyzer.Wpf.exe`.
When that folder cannot be written (a program under Program Files, a read-only drive), the log
goes to `%LOCALAPPDATA%\AstroPhotoAnalyzer\log` instead, beside the window's settings, and its
first line says so. Past 1 MiB (1,048,576 bytes, so a file ends one line past that) a file is
left and the next one is started (`AstroPhotoAnalyzer_01.log`, `AstroPhotoAnalyzer_02.log`, ...);
the five most recent files are kept and the older ones deleted, so the folder holds about 5 MiB
per program at most. An analysis of a 23 MP image writes about 25 KB in the console and about
30 KB in the window, with its curves and its soft proof, so the log keeps some two hundred of
them. Two windows open at the same time write into the same file, each line marked with its
process. The log is written beside the program, wherever that is: keep the program outside your
image folders.

**What is in it.** One line per event, numbers with a point: at the start the version, the
command line, the Windows and .NET versions and the folders; every image read, with its size and
what its file says (format, bit depth, compression, ICC profile, WCS), and for a FITS the keywords
that decide how it is read; every ICC profile read, with its Dmax or why it cannot be used; for
each analysis its parameters, every figure of every measurement, every recommendation with its
"because:", the verdict and the time each stage took; the same for the suggested curves and the
soft proof; every file the programs write for you, with its size; in the window, each run, the
dialogs and the buttons pressed; every error with its technical details, and the problems the
programs work around without a word (a profile or a settings file that cannot be read, a link
that does not open), though not a temporary file that a failed save could not remove; at the end
the exit code and the time taken. The log's own words are English
whatever `--lang` says; an error's message, a refusal and a recommendation appear as you saw
them, so in Romanian under `--lang ro`. It holds the paths of your files and the figures measured
on them, not their pixels.

**How much.** The environment variable `APA_LOG` sets the level: `off`, `fatal`, `error`, `warn`,
`info`, `debug` (the default) or `trace`, which adds every header card of each FITS read; `off`
writes no log and creates no folder. In a command window, `set APA_LOG=trace` holds for the
programs started from that window; for the window started from a shortcut or from Astro
Darkroom, set it once with `setx APA_LOG trace` (or in Windows' environment variables) and start
the program, or Astro Darkroom, again.

## Building

The solution needs Windows, because of the window; the console's project builds alone on any
system the .NET SDK runs on. The SDK is .NET 9, version 9.0.318 or a later 9.0.3xx patch
(`global.json`); the programs target .NET 8.

```bash
dotnet build AstroPhotoAnalyzer.sln -c Release
```

```bash
dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category!=Validation"
```

```bash
dotnet build AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj -c Release
```

Self-contained programs for Windows x64, the console as one file you can copy anywhere and the
window as a folder:

```bash
dotnet publish AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

```bash
dotnet publish AstroPhotoAnalyzer.Wpf/AstroPhotoAnalyzer.Wpf.csproj -c Release -r win-x64 --self-contained
```

The release script publishes both at once, into `artifacts/X.Y.Z/console/` and
`artifacts/X.Y.Z/wpf/`; with `-PublishOnly` it runs no check and changes no tracked file. The
version passed names the folder and goes into its `release.json`; the version the programs show
is `<Version>` in `Directory.Build.props`.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 1.0.0 -PublishOnly
```

## For developers

AstroPhotoAnalyzer follows the process of `ClaudeCodeStarterKit`, the folder beside it in this
repository, and is developed with Claude Code. What is where:

- `CLAUDE.md`: the project's rules and commands; `.claude/rules/kit/process.md`: the kit's
  process rules, the same in every project made from the kit. `GHID.md`, in the kit, gives the
  reasons behind them, in Romanian.
- `docs/requirements.md`: the behaviour, with IDs that the tests carry in their names.
- `docs/decisions/`: the decisions, one ADR each, with the alternatives rejected.
- `docs/design/`: how the code works, area by area; the index is `docs/design/README.md`.
- `docs/validation.md` and `docs/validation.json`: the figures measured on the reference images,
  which stay out of git; the validation tests need a copy of them in
  `AstroPhotoAnalyzer.Tests/testdata/large/`, and `AstroPhotoAnalyzer.Tests/testdata/manifest.json`
  holds their hashes.
- `CHANGELOG.md`: the changes, version by version.

The gate runs before every commit, through the git hooks and Claude Code's: the kit's shared
files, the test data's hashes, the ADR headers, the design notes, a format check, a build with
every warning as an error, and the tests except the validation ones, in about half a minute.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

`scripts/`, `.claude/skills/`, `.claude/agents/` and `.claude/rules/kit/` are the kit's, the same
bytes in every project, and change in the kit only. A release is cut on `main`, at the owner's
word, with the `/release` skill (`scripts/release.ps1 -Version X.Y.Z`): it runs the gate, writes
the version into `Directory.Build.props`, closes the CHANGELOG's section and publishes into
`artifacts/X.Y.Z/`; the commit and the tag, `astrophotoanalyzer-vX.Y.Z`, stay the owner's. The
other skills: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`.

## Licence

AstroPhotoAnalyzer is free software, under the GNU General Public License version 3 or (at your
option) any later version: see [LICENSE](LICENSE). The components it ships from others are listed
in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), each with its licence and why it is
compatible with the GPL:

| Component | What it does here | Licence |
| --- | --- | --- |
| SixLabors.ImageSharp | reads TIFF, PNG and JPEG, and encodes the JPEGs and the 8-bit TIFF of the compression table | Six Labors Split License 1.0 (the Apache 2.0 terms for open-source software) |
| Wacton.Unicolour | evaluates the tables of a printer ICC profile, for the paper's Dmax, for the out-of-gamut figure of the Curves Editing tab and for the Soft proof tab | MIT |
| NLog and NLog.Targets.AtomicFile 6.2.2 | writing the diagnostic log (see "Logs"), two windows into one file | BSD-3-Clause |
| Lynds' Catalogue of Dark Nebulae (data, not code) | the catalogue of dust clouds (LDN, 1962) by which the clouds of a plate-solved FITS are recognized | published data (Lynds 1962, ApJS 7, 1), the ADC/NASA version of 1979 distributed by CDS/VizieR as VII/7A; citing the source is the only condition |
