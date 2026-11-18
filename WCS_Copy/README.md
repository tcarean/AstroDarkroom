# CopyWcs

CopyWcs copies the plate solve of one FITS file into another. You solve a stacked image in
Siril, then stretch, denoise or export it elsewhere and lose the astrometric header on the
way; CopyWcs puts the WCS cards of the solved file back into the processed one, so that the
annotation, the catalogue overlays and the mosaic tools find the sky again. It copies the
cards byte for byte, adds one `HISTORY` line, and leaves the image data of the destination
exactly as it was. It is a small tool: it reads no pixel and
needs no other program.

The window has a manual in plain words in the suite's wiki:
<https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs>.

It is free software under the GNU General Public License v3 or later; see `LICENSE`.

## What is copied

- The astrometric solution: `CTYPE`, `CUNIT`, `CRPIX`, `CRVAL`, the scale and rotation in
  whichever form the solver wrote (`CDELT` and `CROTA`, the `CD` matrix or the `PC` matrix),
  `EQUINOX`, `RADESYS`, `LONPOLE`, `LATPOLE`, `WCSAXES`, Siril's `PLTSOLVD`, and every SIP
  distortion coefficient (`A_i_j`, `B_i_j`, `AP_i_j`, `BP_i_j`) with its order.
- Unless `--no-optical`, the optical and observation metadata a later re-solve needs as a
  starting point: `FOCALLEN`, `XPIXSZ`, `YPIXSZ`, the binning, the pointing (`OBJCTRA`,
  `OBJCTDEC`, `RA`, `DEC`, `OBJECT`), the site, `DATE-OBS`, `INSTRUME`, `TELESCOP`.

Only the keys present in the source are copied. A Siril 1.4.4 solve of a stack, for example, is
72 cards: 21 WCS keys, 40 SIP coefficients and 11 optical keys. A text too long for one card,
an `OBJECT` or a `TELESCOP` of more than 68 characters, goes on in `CONTINUE` cards under it:
those are copied with it, and a long value of the destination's that the copy replaces takes
its own `CONTINUE` cards away with it, so the value reads whole and nothing of the old one stays.

**The destination's old plate solve is dropped.** When the destination already holds a plate
solve, one another program made for example, its plate-solve keys that the source does not have
are removed before the copy: a `CD` matrix beside Siril's `PC` and `CDELT`, a `CROTA`, a SIP
coefficient of a higher degree. Siril 1.4.4 reads a `CD` matrix in place of `PC` and `CDELT`
when a file holds both, so a `CD` left behind would hide the copied solution from the very
program the copy is checked in. The optical keys are never removed, and no plate-solve key is
removed when the source has no `CTYPE1`. `--dry-run` and the window's Preview name the keys
that would be dropped under the cards, and a copy names them before its closing lines.

**The destination's checksum is removed.** Some programs, the SyQon CLI among them, end the
header with a `CHECKSUM` card, a sum another program can use to check that the file is intact.
A copy changes the header, so that sum would no longer match and the file would look damaged
to a program that checks it; the copy removes the card, as the FITS convention asks of a
program that changes a file. `DATASUM`, the sum of the image data alone, stays: the data is not
touched. Siril 1.4.4 checks neither, so nothing changes there; the screen does not mention it,
the log does.

The two images must have the same pixel grid: the tool compares `NAXIS1` and `NAXIS2` and
refuses to copy when they differ, because the solution would point at the wrong pixels.
`--force` copies anyway, for the case you know is right.

**A mirrored destination is refused.** When the destination holds a plate solve of its own and
it is the mirror image of the source's, its pixels were mirrored or flipped after the source was
solved, and a copied solution would point the wrong way: the copy is refused, a dry run too,
unless `--force`. The SyQon CLI's output is such an image: it stores the rows top-down and writes
a flipped `CD` matrix for them, so Siril already shows it right. `--force` is also for the rare
destination whose own plate solve was left mirrored after its pixels were flipped back.

**Check the result by eye.** The mirror check sees a flip only when whatever flipped the pixels
flipped the destination's own plate solve with them, as Siril and the SyQon CLI do. A program
that flips the pixels and passes the header through, or a destination with no plate solve of its
own, leaves nothing to compare, and the copied solution is then wrong without any error being
possible: the header cannot know what happened to the pixels. Open the result in Siril and look
at the annotation before trusting it.

## The console

```bash
copywcs <solved.fit> <destination.fit> [options]
```

| Option | What it does |
| --- | --- |
| `-o, --output <file>` | Write a new file instead of changing the destination in place |
| `--dry-run` | Show the cards that would be copied, and the old plate-solve keys that would be dropped, without writing anything |
| `--force` | Continue even when the images' dimensions differ or the destination's own plate solve is mirrored |
| `--no-optical` | Copy only the WCS keys, not the optical metadata |
| `--lang en\|ro` | The language of the texts: `en` (English, the default) or `ro` (Romanian) |
| `-h, --help` | Show the usage and the warranty notice |

Every run begins with the version (`copywcs 1.0.0`) and the copyright line, so a pasted output
says which version produced it. Exit codes: 0 on success or a dry run, 1 when the copy was
refused (a file that cannot be read, the same file given twice, different dimensions or a
mirrored plate solve without `--force`, no relevant key,
a write error, a `--lang` value that is not `en` or `ro`), 2 on a usage error.

**The same file twice is refused.** The source and the destination must be two files, and a new
file (`-o`) must be neither of them: `-o` naming the solved source would overwrite it, and `-o`
naming the destination is the in-place write, which is what leaving `-o` out does. The check
looks at the files themselves, so `Stack.fit` and `stack.fit` are one file, as Windows sees
them. Nothing is written, and a dry run is refused too.

Example: preview, then copy into a new file.

```bash
copywcs result_final.fit result_final_stretched.fit --dry-run
copywcs result_final.fit result_final_stretched.fit -o result_final_stretched_wcs.fit
```

Without `-o` the destination is rewritten in place; with `-o` the new file is written and the
destination is not touched. Either way the file is first written whole under a temporary name
beside it (`result_final_stretched_wcs.fit.copywcs-3f9a0c1e.tmp`) and only then put in place of
the file it replaces. A write that fails, for a missing folder, a full disk or a drive that
drops, leaves the folder as it was: the destination and any file of the new file's name keep
every byte, and the temporary file is removed, unless its drive is gone by then (the log then
names it). The error names your file, not the temporary one, or no file when the system names
none (a read-only file: `Access to the path is denied.`); in a folder that lets you change files
but not create them, it says access to your file is denied, because the temporary file cannot be
made beside it. Any name Windows accepts can be written. A file put in place this way is a
new file: the one it replaces does not keep its creation date, its attributes (Hidden, for one)
or its own permissions, and a symbolic link is replaced by the file itself.

## The window

`CopyWcs.Wpf.exe [--lang en|ro] [<solved.fit> [<destination.fit>]]` opens the same tool as a
window: dark, one page. Pick the plate-solved source and the destination (or pass them on the
command line: the first two arguments that are existing files fill the boxes, so a shortcut
or "Send to" works; a path pasted in double quotes, as File Explorer's "Copy as path" gives
it, is taken without them), choose whether to write into the destination or into a new file, keep or
drop the optical metadata, allow or refuse different dimensions or a mirrored image, then
**Preview** or **Copy**. A file name alone in the new file's box, `out.fit`, is written beside
the destination; the files from the command line show in their boxes with their whole paths, and
the result names the file it wrote with its folder.
Under each file box the window shows what it read from the header (`Source: 5601 x 3692,
plate-solved`, `Destination: 5601 x 3692`). It reads the files again when the window comes back
to the front, when Browse picks a file, the same one too, and after every Preview or Copy, so a
file saved or plate-solved in Siril meanwhile shows as it is now; after an in-place copy the
destination reads `already plate-solved`.

The window does what the console does: Preview lists exactly the cards `copywcs --dry-run`
would print, Copy writes exactly the bytes the console writes, and a refusal or a warning is
shown with the console's words (a refused copy writes nothing). The result empties as soon as
a box or an option changes, since it no longer says what a run would do, and while a Preview or
a Copy runs the window says `Preview in progress...` or `Copy in progress...`. The band at the
top shows the version, the copyright and one sentence on what the tool does; its **Help** button
(or F1) opens the wiki manual in the browser, at the section about what the window shows: the
window itself while no result is shown, the result after a Preview or a Copy, and the section on
problems when the result is a refusal or a warning. **About** opens the version, the copyright, the GPLv3
notice and the third-party components. The window is Windows-only; the console builds
anywhere.

Closing the window while a Preview or a Copy runs does not cut it short: the window greys and
takes no more clicks until the copy is done, a second or so for a stack of a few hundred MB, then
closes by itself. The file is written whole, or not at all when the write fails, and the log
says which. Logging off or shutting Windows down ends the copy with the program: the file it
would have replaced keeps every byte, or is replaced whole when the copy ends first, and a
temporary file may stay beside it.

## The two languages

The texts are English unless `--lang ro` asks for Romanian (written without diacritics), in
the console and in the window alike; a `--lang` value that is not `en` or `ro` stops the
console with exit 1 and opens the window in English with a message saying so. The `HISTORY`
card written into the file is English in both languages: it is data other programs read, not
interface text; it is also plain ASCII, as the FITS standard asks, so a letter with a diacritic
in the source's name appears in it as its base letter (`Nebula.fit` for a name with a-breve).
Numbers never change with the language.

## Logs

Both programs keep a log of what they did, for the day something goes wrong. To report a
problem, attach the whole `log` folder: it holds what is needed to see what happened.

**Where it is.** In a folder named `log` beside the program: `log\copywcs.log` beside
`copywcs.exe`, `log\CopyWcs.Wpf.log` beside `CopyWcs.Wpf.exe`. When that folder cannot be
written (a program under Program Files, a read-only drive), the log goes to
`%LOCALAPPDATA%\CopyWcs\log` instead, and its first line says so. Past 1 MiB a file is left and
the next one is started (`copywcs_01.log`, `copywcs_02.log`, ...); the five most recent files are
kept and the older ones deleted, so the folder holds about 5 MiB per program at most.

**What is in it.** One line per event, numbers with a point: at the start the version, the
command line, the Windows and .NET versions and the folders; every file read, with its size and
the header keys that say what it is (`BITPIX`, `NAXIS`, the sizes, `EXTEND`, `CTYPE`,
`ROWORDER`, the SIP order, `PLTSOLVD`); the options of each copy, every card copied and the card
of the destination it replaced, every card of the destination dropped, the file written and the
time each stage took; the warnings, the
refusals with their reasons and every error with its technical details; in the window the
language, the files picked and the About window; at the end the exit code and the time taken.
The log's own words are English whatever `--lang` says; an error's message appears as you saw
it, so in Romanian under `--lang ro`, save that a failed write's names the temporary file the
copy was writing, where you saw your file's name. It holds the paths of your files and the header cards
copied, which include the site's latitude and longitude when the source has them: read it before
you send it. It holds no pixel.

**How much.** The environment variable `COPYWCS_LOG` sets the level. For a program started from
a command window, set it there first:

```
set COPYWCS_LOG=trace
```

That reaches only the programs started from that window. For the window started from the Start
menu or from AstroDarkroom, set it for your account, once (new programs see it; the ones already
open do not):

```
setx COPYWCS_LOG trace
```

`debug`, the default, writes everything above; `trace` adds every card of every header read and
written; `info`, `warn`, `error` and `fatal` write less and less; `off` writes nothing and
creates no folder.

## Building from the checkout

.NET SDK 9 (pinned in `global.json`; the tool itself targets .NET 8):

```bash
dotnet build CopyWcs.sln -c Release
dotnet test CopyWcs.sln -c Release
```

The console is `src/CopyWcs.Cli/bin/Release/net8.0/copywcs.exe`, the window
`src/CopyWcs.Wpf/bin/Release/net8.0-windows/CopyWcs.Wpf.exe`, with `LICENSE` beside it. The
requirements, the decisions and the working rules are in `docs/` and `CLAUDE.md`.

## Third-party components

| Component | What it does here | Licence |
| --- | --- | --- |
| [NLog](https://nlog-project.org/) (`NLog`, `NLog.Targets.AtomicFile`) | writes the diagnostic log | BSD-3-Clause |

The FITS header is read and written by hand; nothing else comes from a package. The licence's
full text and why it is compatible with the GPLv3 are in `THIRD-PARTY-NOTICES.md`; the window's
About lists the same component.

## Licence

Copyright (C) 2026 Tudor Carean. This program comes with ABSOLUTELY NO WARRANTY, not even the
implied warranty of merchantability or fitness for a particular purpose. It is free software:
you may redistribute and modify it under the terms of the GNU General Public License as
published by the Free Software Foundation, either version 3 of the License, or (at your option)
any later version. The full text is in `LICENSE`, or at
<https://www.gnu.org/licenses/gpl-3.0.html>.
