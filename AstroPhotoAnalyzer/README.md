# AstroPhotoAnalyzer

A print-readiness check for a finished astrophoto. Open the stretched image you would send to
print, a TIFF, PNG or FITS as it comes out of Siril, and it tells you, in the order the decisions
are made, how to send it: the file format, whether the background survives the paper's black,
the colour cast and the gradients, the largest print size the real detail supports from the
distance it will be seen at, and how much denoise the print version needs. Every recommendation
comes with the figure it measured and the threshold it judged it by.

It measures and recommends; the export itself is made cleanly from Siril. Besides its report it
writes one file, and only when you ask for it: from the window's **Curves** tab, a new FITS with
a suggested contrast curve applied. The image you open is never modified.

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
  from anywhere; **Use** writes the chosen profile's Dmax into the field. The medium keeps its
  name, and Appendix F of the report names the profile the Dmax came from.
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
judged by the dpi alone (180 dpi without a distance). A star must stand 12 times the local noise
over its surroundings, so the grain of a stretched sky is not counted as one; faint stars are
left out with it, and the resolution is the bright stars'.

**On the right, seven tabs.** Most figures have a line under them saying what they measure and,
where one applies, the threshold they are judged by.

| Tab | What it shows |
| --- | --- |
| **Report** | The decisions, in the report's order, each with its verdict chip and its "because:" line; the whole text report behind a check box. |
| **Tones** | The L\* histogram, with paper black as a red line and the visibility threshold L\* 15 as a dashed yellow one; the tonal figures, the background level among them; the background simulation, with a **Preview** button on every row; under it, the definition of the dust and the sky used on this file, a red line when the file has no WCS, the **Dust / sky map** button and the table of catalogued clouds; the tab's landmarks in level and in L\*; how to read the tab. |
| **Export** | The JPEG table, with what its columns mean; the size of the 8-bit TIFF; the 16 to 8 bit rounding; the shadow-lift stress test; the plateau, how wide the flat steps of the 8-bit sky would print. |
| **Resolution** | The stars' FWHM, in the centre and in the four corners, and the real detail it leaves; the table of print sizes; the scale of dpi against viewing distance. |
| **Colour** | The background colour, R / G / B, with its cast; the four corners and the four edges against it, a flagged one marked "! coloured" or "! neutral"; how to read them. |
| **Preview** | The centre at 100 % and 200 %, the four corners at 100 %, the whole frame, the print simulation (at the extra target background, or else at the recommended one, when it is not the image as it is) and, when the target ratio cuts, the crop, each with what to look for in it; the rule of screen against paper, with the step back computed for this image. |
| **Curves** | Suggested contrast curves, on request: see below. |

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
  the names of the darkest 15 on the image; the list beside it names them all, each with its L\*
  and its level. Every layer has its check box, so do the clouds (disabled without a WCS), and
  the list of clouds has **All** / **None** and a check box per cloud. It is made for checking
  the classes by eye on your own images.
- **Preview**, on a row of the simulation, shows the image lifted to that row's background level
  (the "as is" row included), with the pixels under paper black and those under L\* 15 coloured,
  each layer with its check box.

### Suggested contrast curves

The **Curves** tab is the only one the analysis does not fill: choose **Print** or **Screen** and
press **Calculate**. You get a gallery: the original, then three or four suggestions, **Gentle**,
**Moderate**, **Strong** and, when the peaks still have room under 0.92 of the scale,
**Maximum**, each at the size that fits the window. Browse them with **< Back** and **Next >**,
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
background and the paper, 3. the background colour and the gradients, 4. the size of the print,
5. denoise for the print version, 6. warnings. Each section has its verdict, and each
recommendation a mark, `-` for OK, `!` for a caution, `!!` for a problem, with a `because:` line
under it that gives the figure and the threshold. The appendices A to F follow, with the tables
behind the decisions: tones, the background simulation, export, resolution and size, the
background colour, and the parameters as used. The verdict and the first section on the
reference image, `de_printat.png`, a 23 MP Milky Way frame analysed with the defaults (exit code
2):

```
VERDICT: CAUTION

1. The format to send  [CAUTION]
   - Send an 8-bit LZW TIFF (20.7 MB). The 16 -> 8 bit conversion is pure rounding, not a loss.
     because: mean rounding error 0.25, max 0.50 levels (half a level is the most possible)
   - If the lab accepts only JPEG: q100, 4:4:4 (12.5 MB).
     because: mean error 0.60, max 4.5 levels; 1.2 % of the samples with an error > 2, at a mean level of 31 (in the dark sky, where JPEG does worst)
   ! Do not go below q100.
     because: q95 has a max error of 27.0 levels (threshold 10), on the stars and on the edges
   - No visible banding at 8 bits: the residual noise dithers the quantization.
     because: a plateau of 2.7 px = 0.45 mm at 151 dpi, under 1.0 mm
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
| Wacton.Unicolour | evaluates the tables of a printer ICC profile, for the paper's Dmax and for the out-of-gamut figure of the Curves tab | MIT |
| Lynds' Catalogue of Dark Nebulae (data, not code) | the catalogue of dust clouds (LDN, 1962) by which the clouds of a plate-solved FITS are recognized | published data (Lynds 1962, ApJS 7, 1), the ADC/NASA version of 1979 distributed by CDS/VizieR as VII/7A; citing the source is the only condition |
