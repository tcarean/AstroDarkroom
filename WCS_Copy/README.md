# CopyWcs

CopyWcs copies the plate solve of one FITS file into another. You solve a stacked image in
Siril, then stretch, denoise or export it elsewhere and lose the astrometric header on the
way; CopyWcs puts the WCS cards of the solved file back into the processed one, so that the
annotation, the catalogue overlays and the mosaic tools find the sky again. It copies the
cards byte for byte, adds one `HISTORY` line, and leaves the image data of the destination
exactly as it was. It is a small tool for the author's own stacks; it reads no pixel and
needs no other program.

It is free software under the GNU General Public License v3 or later; see `LICENSE`.

## What is copied

- The astrometric solution: `CTYPE`, `CUNIT`, `CRPIX`, `CRVAL`, the scale and rotation in
  whichever form the solver wrote (`CDELT` and `CROTA`, the `CD` matrix or the `PC` matrix),
  `EQUINOX`, `RADESYS`, `LONPOLE`, `LATPOLE`, `WCSAXES`, Siril's `PLTSOLVD`, and every SIP
  distortion coefficient (`A_i_j`, `B_i_j`, `AP_i_j`, `BP_i_j`) with its order.
- Unless `--no-optical`, the optical and observation metadata a later re-solve needs as a
  starting point: `FOCALLEN`, `XPIXSZ`, `YPIXSZ`, the binning, the pointing (`OBJCTRA`,
  `OBJCTDEC`, `RA`, `DEC`, `OBJECT`), the site, `DATE-OBS`, `INSTRUME`, `TELESCOP`.

Only the keys present in the source are copied. On a Siril 1.4.4 solve of a DSLR stack that
is 72 cards: 21 WCS keys, 40 SIP coefficients and 11 optical keys (`docs/validation.md`).

**The destination's old plate solve is dropped.** When the destination already holds a plate
solve, one another program made for example, its plate-solve keys that the source does not have
are removed before the copy: a `CD` matrix beside Siril's `PC` and `CDELT`, a `CROTA`, a SIP
coefficient of a higher degree. Siril 1.4.4 reads a `CD` matrix in place of `PC` and `CDELT`
when a file holds both, so a `CD` left behind would hide the copied solution from the very
program the copy is checked in (ADR 0011). The optical keys are never removed, and nothing is
removed when the source has no `CTYPE1`. `--dry-run` and the window's Preview name the keys
that would be dropped under the cards, and a copy names them before its closing lines.

The two images must have the same pixel grid: the tool compares `NAXIS1` and `NAXIS2` and
refuses to copy when they differ, because the solution would point at the wrong pixels.
`--force` copies anyway, for the case you know is right.

**A mirrored destination is refused.** When the destination holds a plate solve of its own and
it is the mirror image of the source's (the determinant of its scale and turn, read `CD` first
as Siril reads it, has the opposite sign, both solves naming RA first), its pixels were mirrored
or flipped after the source was solved, and a copied solution would point the wrong way: the
copy is refused, a dry run too, unless `--force` (ADR 0012). The SyQon CLI's output is such an
image: it stores the rows top-down and writes a flipped `CD` matrix for them, so Siril already
shows it right. `--force` is also for the rare destination whose own plate solve was left
mirrored after its pixels were flipped back.

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
refused (a file that cannot be read, different dimensions or a mirrored plate solve without
`--force`, no relevant key,
a write error, a `--lang` value that is not `en` or `ro`), 2 on a usage error.

Example: preview, then copy into a new file.

```bash
copywcs result_final.fit result_final_stretched.fit --dry-run
copywcs result_final.fit result_final_stretched.fit -o result_final_stretched_wcs.fit
```

Without `-o` the destination is rewritten in place: the new header is written to a temporary
file beside it and moved over the original at the end, so a failure leaves the original as
it was.

## The window

`CopyWcs.Wpf.exe [--lang en|ro] [<solved.fit> [<destination.fit>]]` opens the same tool as a
window: dark, one page. Pick the plate-solved source and the destination (or pass them on the
command line: the first two arguments that are existing files fill the boxes, so a shortcut
or "Send to" works; a path pasted in double quotes, as File Explorer's "Copy as path" gives
it, is taken without them), choose whether to write into the destination or into a new file, keep or
drop the optical metadata, allow or refuse different dimensions or a mirrored image, then
**Preview** or **Copy**.
Under each file box the window shows what it read from the header (`Source: 5601 x 3692,
plate-solved`, `Destination: 5601 x 3692`); after a copy the destination reads `already
plate-solved`.

The window computes nothing of its own: Preview lists exactly the cards `copywcs --dry-run`
would print, Copy writes exactly the bytes the console writes, and a refusal or a warning is
shown with the console's words (a refused copy writes nothing). The band at the top shows the
version, the copyright and one sentence on what the tool does; its **About** button (or F1)
opens the version, the copyright, the GPLv3 notice and the third-party components. The window is
Windows-only; the console builds anywhere.

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
`%LOCALAPPDATA%\CopyWcs\log` instead, and its first line says so. Past 1 MiB (1,048,576 bytes,
so a file ends one line past that) a file is left and the next one is started
(`copywcs_01.log`, `copywcs_02.log`, ...); the five most recent files are kept and the older ones
deleted, so the folder holds about 5 MiB per program at most. Two windows open at the same time
write into the same file, each line marked with its process.

**What is in it.** One line per event, numbers with a point: at the start the version, the
command line, the Windows and .NET versions and the folders; every file read, with its size and
the header keys that say what it is (`BITPIX`, `NAXIS`, the sizes, `EXTEND`, `CTYPE`,
`ROWORDER`, the SIP order, `PLTSOLVD`); the options of each copy, every card copied and the card
of the destination it replaced, every card of the destination dropped, the file written and the
time each stage took; the warnings, the
refusals with their reasons and every error with its technical details; in the window the
language, the files picked and the About window; at the end the exit code and the time taken.
The log's own words are English whatever `--lang` says; an error's message appears as you saw
it, so in Romanian under `--lang ro`. It holds the paths of your files and the header cards
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
development process (requirements with IDs, decisions, the gate) is described in `CLAUDE.md`
and `docs/`.

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
