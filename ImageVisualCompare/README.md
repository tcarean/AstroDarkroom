# ImageVisualCompare

A before/after slider for two versions of the same astrophotography image: image A left of a
vertical line you drag, image B right of it, both at the same zoom and the same position, with
the RGB of the pixel under the cursor read from both. It is made for two versions of one
processing, such as before and after star removal, two background extractions or two
stretches, and to show a difference of one or two levels in a dark field, where the eye cannot
see it.

A Windows application (Windows 10 or 11, x64, .NET 8) with **no NuGet package**: PNG and TIFF
are decoded by WIC, the Windows codec that WPF uses anyway, and FITS, which WIC does not read, by
the project's own reader. It only reads: it never writes, moves or changes a file.

Everything on screen is dark, whatever the Windows theme: the window, its title bar and its
messages, because a bright frame around an astro image spoils the judgement of what is really
dark in it. The one exception is the Windows dialog that opens the files, which follows the
system theme.

ImageVisualCompare is one of the five tools of Astro Darkroom. The repository's front page, the
[`README.md`](../README.md) at its root, describes the suite and how to build it, and the
suite's launcher opens this tool in the launcher's language.

---

## What files it opens

**PNG and TIFF** in any pixel format WIC reads: 8 or 16 bits per channel, RGB, RGBA, gray or
indexed, and TIFF of 32-bit float samples (see "Float TIFF" below). **FITS**, as "FITS files"
below says. Every image is converted to 8 bits per channel when it loads, the levels the screen
shows; the status bar says what each file really was, so you always know what you opened. Once
loaded, a file is not kept locked: you can export over it while it is on screen, then open it
again.

The two may differ in bit depth and channels: a 16-bit PNG against an 8-bit TIFF is a normal
pair. Only their pixel dimensions are compared.

A "32-bit" export from Siril reaches the disk as a PNG of **16 bits per channel** (48 bpp),
since PNG has no 32-bit mode. The status bar says `16-bit RGB`, and Windows says "bit depth 48".

**When the pixel dimensions differ, you are asked before anything is decoded**, in the dialog
`Images have different sizes`. It names both sizes and the common canvas, the larger size on
each axis:

```
The two images have different dimensions.
A: de_printat.png — 5938 × 3888
B: de_printat_50x35_TIFF8.tif — 5554 × 3888

Both can be padded with black to 5938 × 3888,
anchored top-left, and compared on that canvas.
The image files themselves are not modified.

Continue?
```

`Yes` compares the two on that canvas, each anchored at its top-left corner, with black where an
image does not reach: the narrower one has black on its right, the shorter one below it. `No`,
`Escape` or closing the question brings the open dialogs back. Neither the files nor the pixels
in memory are touched: the black is the backdrop both images are drawn over anyway, not a larger
copy of them. The pixel readout says `—` where an image does not reach, so the padding never
passes for a black area of the picture.

**A file that cannot be read** (missing, locked, damaged, in a format no installed codec reads,
or too large to decode) gives a message with its name and the reason, such as
`Could not read broken.png.` followed by the codec's own words, in the dialog
`Cannot compare these files`; then the open dialogs come back. The application does not close.

### FITS files

FITS files (`.fit`, `.fits`, `.fts`) open as image A, as image B or as both, against each other
or against a PNG or a TIFF. Integers of 8, 16 and 32 bits and floats of 32 and 64 bits are read,
with one plane, shown gray, or three, shown as RGB. A file is known to be FITS by its first
bytes, not by its extension, and the image shown is the first one in it, in the primary unit or
in an `IMAGE` extension.

- **The values are shown as stored, with no stretch.** A processed FITS looks exactly like its
  16-bit PNG or TIFF export, level for level, so the two compare fairly. A linear stack, not
  stretched, shows almost black: that is correct, it is what the file holds. To see what is in
  it anyway, there are the two display modes below.
- **The rows are the way up Siril shows them.** The first row in the file is the bottom of the
  image, whatever `ROWORDER` in the header says: Siril reads that keyword only to debayer, never
  to display, and files that say `TOP-DOWN` over rows written bottom-up exist.
- **Floats in 0..1 or in 0..65535.** Siril writes floats between 0 and 1. If more than 1 % of
  the samples in the file are above 2, the file is read as 0 to 65535, and the status bar says
  so: `32-bit float RGB (0..65535)`. A few hot pixels above 2 do not change the scale. Negative
  values and NaN show black, values above the top white.
- **What is not read:** a compressed FITS (tile-compressed, `.fz`), 64-bit integers, an image
  with other than 2 or 3 axes, or a cube of other than 1 or 3 planes. The message says which, not
  a generic error; the same for a damaged header, a file cut short or an image too large to show.
  A raw frame with a Bayer matrix opens, but shows as the gray mosaic it is: it is not
  debayered.

### Histogram and false colours

For a pair whose two files are both FITS or float TIFF (the files shown "as stored"), there
are two display modes besides the linear one, both copied from Siril 1.4.4, to see in a linear
stack what the eye cannot: a one-level gradient, a patch, a band.

- **`Histogram`** (the toolbar button, `Histogramă` in Romanian, or the `H` key) is Siril's
  "Histogram" display mode: histogram equalisation, each channel on its own histogram, with
  65 536 bins on the 16-bit value. Every grey level on the screen receives the same number of
  pixels, so a tiny difference in the data becomes many grey steps on the screen. Amplitude is
  destroyed by construction: a 0.5 % and a 10 % difference of the same shape look the same.
  Pixels at 0 stay 0.
- **`False colours`** (the button, `Culori false`, or the `F` key) is Siril's "rainbow" table
  over one layer equalised as above: the hue runs from magenta (level 0) through blue, cyan,
  green and yellow to red (level 255). The layer is the mean of the three planes of an RGB
  FITS, or the plane itself of a gray one; Siril never colours the RGB composite, and neither
  does this program.

A switch decodes both files again in the new mode (a second or two on a 23 MP stack, with no
memory kept beyond the pair) and keeps the zoom, the position and the line where they were;
press the same button or key again to go back to linear. The mode stays for the next pair when
that one allows it too. On a pair with a PNG or an integer TIFF the buttons are disabled and the
keys do nothing: 8-bit levels are not equalised.

### Float TIFF

A TIFF of 32-bit float samples, as the "32 bit" exports of GraXpert and Siril are, is read like a
FITS of the same values: the status bar says `32-bit float RGB` (or `gray`, `RGBA`), and the
values are shown as stored, with no stretch, between 0 and 1 or between 0 and 65535 by the same
rule as a FITS. Windows' own conversion would have shown them much brighter, by 73 levels of 255
on average on two GraXpert exports: the 16-bit stack from before GraXpert and its float result
would have looked one twice as bright as the other, which the data is not. Only 32-bit float
samples are read this way: a TIFF of 16-bit (half) or 64-bit floats is left to Windows' own
conversion, as every file was before, which shows float samples brighter than stored.

## Starting it

Without arguments, it asks for the two files in two open dialogs, one after the other:
`Select image A  —  shown left of the divider`, then
`Select image B  —  shown right of the divider`. They show PNG, TIFF and FITS files; their list
of file types also offers each kind alone, and `All files`. The second dialog opens in the
folder of the first file. The first opens where Windows chooses, and at a later `Open…` in the
folder of the last image A.

Cancelling either dialog closes the application when no pair is on screen yet; with a pair on
screen, that pair stays.

With two paths as arguments, it skips the dialogs:

```
ImageVisualCompare.Wpf.exe "A.png" "B.png"
```

The first path is the image shown left of the line, the second the one shown right of it. With a
single path, or a path that does not exist, it starts normally and asks through the dialogs; a
third path is ignored. The two paths are used once: `Ctrl+O` and `Open…` always go through the
dialogs.

The window starts maximised, so the first fit is worked out on the whole screen. While a pair
loads, an overlay shows the progress (`Opening…`, `Decoding <file name>…`, `Almost there…`); the
two files are decoded one after the other. Every new pair appears at fit, with the line in the
middle.

### The interface language

The interface is in English or in Romanian, chosen on the command line with `--lang`:

```
ImageVisualCompare.Wpf.exe --lang=ro "A.png" "B.png"
```

`--lang ro`, with a space, works too, and the option can stand anywhere among the arguments, in
capitals or not. Without `--lang` the interface is English. A language the application does not
have (`--lang=de`), or none (`--lang=`), shows a message first; then the interface starts in
English. Opened from the Astro Darkroom launcher, the tool starts in the launcher's language.

Every text is translated: the buttons, the titles, the status bar, the messages and the
questions, the titles and the file types of the open dialogs. Numbers read the same in both
languages (`5938 × 3888`, `152%`, `560 MB`), and the reason Windows or .NET gives for a failure,
such as why a codec could not read a file, is not translated. Below, a name in parentheses is
the one the Romanian interface shows.

## Controls

| action | how |
| --- | --- |
| move the line | drag it, anywhere along its height, or drag its handle |
| move the line finely | `←` / `→` by 1 pixel, `Shift+←` / `Shift+→` by 10 |
| pan | drag anywhere off the line, or use the scrollbars |
| zoom | the mouse wheel, 15 % a notch, anchored at the cursor; `Ctrl` + wheel does the same |
| pan sideways | `Shift` + wheel |
| zoom by steps | the `+` and `-` keys, on the main keyboard or the keypad, or the toolbar's `+` and `−`; about the centre of the view |
| fit the whole image | `Ctrl+0`, or the `Fit` button (`Încadrează`) |
| actual size | `Ctrl+1`, or the `100%` button; about the centre of the view |
| jump to another area | click or drag in the minimap, bottom left |
| histogram (FITS, float TIFF) | `H`, or the `Histogram` button (`HistogramÄ`); once more goes back to linear |
| false colours (FITS, float TIFF) | `F`, or the `False colours` button (`Culori false`); once more goes back to linear |
| open another pair | `Ctrl+O`, or the `Open…` button (`Deschide…`); not while a pair is opening |

The keyboard shortcuts work while the comparison area has the keyboard focus. It takes the focus
when a pair loads and at any click in it, and gets it back after a click on a toolbar button, so
the keys work right after `Fit`, `100%`, `+` or `−`. `Ctrl+O`, `H` and `F` work wherever the
focus is.
The arrows move the line in device-independent pixels, like everything about the line: at 100 %
Windows scaling that is one screen pixel, at 150 % the line moves 1.5 screen pixels, or 15 with
`Shift`.

The line belongs to the window, not to the images: zooming and panning move both images under it
and leave it where it is. When the window is resized, the line keeps its share of the width and
the point at the centre of the view stays at the centre. Every new pair puts the line back in
the middle.

**The zoom stops at the fit**, where the whole image is visible: below it you would only get a
thumbnail lost on an empty background. The exception is an image smaller than the window, which
can go down to 100 %, or you could not see it at its own resolution. The top is 1600 %. The
steps of `+` and `−` are 2, 3, 5, 8, 12.5, 17, 25, 33, 50, 67, 100, 150, 200, 300, 400, 600,
800, 1200 and 1600 %.

**`100%` puts one image pixel on one physical screen pixel**, not on one device-independent
pixel. On a screen scaled to 125 % or 150 % the difference matters, and for pixel peeping you
want the honest one. It holds on every monitor, also when the monitors are scaled differently:
the application follows the scaling of the monitor its window is on. From 400 % up the images
are drawn nearest-neighbour, so you see the real pixels as squares, not an interpolation.

## What the window shows

The toolbar names the pair, `A   vs   B` with the two file names. On its right, after the label
`Navigation` (`Navigare`), come the buttons `−` and `+` around the zoom in percent, then `Fit`,
`100%`, `Open…` and `About` (`Despre`). The title bar names the pair too, followed by the
program's name and version. The zoom is also shown in a badge at the top right of the comparison
area.

The minimap, bottom left, is a thumbnail of the whole canvas, drawn from image A, with a
rectangle around the part on screen; the rectangle disappears when the whole image is visible.
A scrollbar appears over the image on an axis it overflows, without taking room from it.

### The status bar

```
5794 × 3637  ·  A 16-bit RGB  ·  B 16-bit RGB    2617, 1839    A  35  35  36    B  35  35  35    managed 2 MB, process 560 MB
```

From the left: the size of the pair and the real format of each file; the coordinate of the
pixel under the cursor; **the RGB of that pixel in both images**; the memory. The pixel values
are often the only way to see a difference: on a dark background, a difference of one or two
levels out of 255 is invisible to the eye and plain in the numbers. They are the 0–255 levels on
the screen; off the image the three readouts are empty. A 16-bit file is shown, and read, at 8
bits, the high byte of each value, so a difference of less than 256 out of 65 536 may not show
in the readout at all.

For a FITS, as for a float TIFF, the format is the depth and the planes of the file, such as
`A 32-bit float RGB` or `B 16-bit gray`. The values read are still the 0–255 levels on the
screen, not the float values of the file.

In the histogram mode the first part ends with `Â· Histogram` (`Â· HistogramÄ`), and the values read
are the equalised levels on the screen, not the file's: press `H` again to read those. In false
colours it ends with `Â· False colours` (`Â· Culori false`), and the readout is one number per
image, `A 143`, `B 141`: the equalised level the colour under the cursor stands for, not the
colour's own RGB.

When the pair needed padding, the first part names the canvas and each image's own size:
`5938 × 3888 padded  ·  A 5938 × 3888  ·  B 5554 × 3888  ·  A 16-bit RGB  ·  B 8-bit RGB`. The
canvas alone would be half the truth, since one of the images does not have that size.

`managed` is the memory .NET manages and `process` the process's private memory, in megabytes,
read every 2 seconds. After them comes a hint,
`wheel to zoom · drag the line to move it · drag to pan · ←/→ nudge · Ctrl+O open`, cut short
when the window narrows; on the right, always, the version and the copyright (there is no
`--version` option). `About` shows the licence notice.

In the Romanian interface the same bar reads `A RGB pe 16 biți` (or `gri float pe 32 de biți`,
`RGB float pe 32 de biți (0..65535)`) and `memorie .NET 2 MB, proces 560 MB`, and a padded pair
starts with `pânză 5938 × 3888`.

## Memory and speed

A pair of 21 MP images takes about 170 MB once decoded: each image becomes 4 bytes a pixel, about
80 MB at 21 MP, whatever it took on disk. Drawn at fit, the process holds about 280 MB of private
memory; after several zoom levels it settles on a plateau of about 570 MB private and 300 MB
resident, which is bounded, not a leak: going through the zoom levels again does not raise it.
While another pair opens, the current one stays on screen, so both are in memory during the
load; as soon as the new one is shown and rendered once, the old one is released explicitly, and from
then on only one is held. The order matters: the surface keeps the old bitmaps in its render
content until the new ones are first rendered, so a release before that frees nothing (measured
on 2026-10-26). The same holds for a switch between linear, histogram and false colours.

Decoding two 16-bit files of 100 to 130 MB takes about three seconds.

## What it does not do (yet)

- No difference mode (`|A−B|` amplified): it would need the full-depth data kept beside the
  displayed images, about 250 MB more for a 21 MP pair at 16 bits.
- No key that flips the whole view between A and B.
- No drag and drop onto the window.
- The tool itself remembers nothing between runs, neither the last pair nor the language; the
  Astro Darkroom launcher remembers its own language and passes it on.

---

## Building

On Windows, with the .NET 9 SDK, version 9.0.318 or a later 9.0.3xx patch (`global.json`); the
projects target .NET 8, and Visual Studio is not needed. From this folder:

```
dotnet build ImageVisualCompare.sln -c Release
```

```
dotnet run --project src/ImageVisualCompare.Wpf -c Release -- --lang=ro "A.png" "B.png"
```

```
dotnet test ImageVisualCompare.sln -c Release --filter "Category!=Validation"
```

A release publishes the application self-contained, with the .NET 8 runtime inside and `LICENSE`
beside `ImageVisualCompare.Wpf.exe`, into `artifacts/X.Y.Z/`. The same folder, without a release
and without changing a tracked file:

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version X.Y.Z -PublishOnly
```

It empties `artifacts/X.Y.Z/` first, and writes `release.json` there beside the program, with the
version, the date and the commit it was built from. `X.Y.Z` names the folder; the version the
application shows is `<Version>` in `Directory.Build.props`, so passing that number keeps the
two the same.

## For developers

The project follows the process of `ClaudeCodeStarterKit`, the folder beside this one, and is
maintained with Claude Code. Its rules are in `CLAUDE.md` and `.claude/rules/kit/process.md`;
the behaviour, with the IDs the tests carry, in `docs/requirements.md`; the decisions in
`docs/decisions/`; how the code works, area by area, in `docs/design/` (its `README.md` is the
index); the figures measured on the author's files of one 23 MP frame, three exports and two
FITS stacks, in `docs/validation.md`; the changes in `CHANGELOG.md`. How to use the tool is
written here and nowhere else (ADR 0025).

Every commit passes the gate first: the kit's checks, the format check, a build with warnings as
errors and every test but the validation ones, in about 20 seconds:

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

The version is `<Version>` in `Directory.Build.props` and changes only at the owner's word. A
release is cut on `main` with `scripts/release.ps1 -Version X.Y.Z`; the owner commits it and
tags it `imagevisualcompare-vX.Y.Z`.

---

## Licence

Image Visual Compare, a before/after slider for two versions of the same image.

Copyright (C) 2026 Tudor Carean

This program is free software: you can redistribute it and/or modify it under the terms of the
**GNU General Public License** as published by the Free Software Foundation, either **version 3**
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but **WITHOUT ANY WARRANTY**;
without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See
the GNU General Public License for more details.

The full text of the licence is in the [LICENSE](LICENSE) file, which also sits next to the
program in the folder it is built or published into, and at
<https://www.gnu.org/licenses/gpl-3.0.html>. The `About` button shows the same notice, short;
the version and the copyright are always on the status bar.
