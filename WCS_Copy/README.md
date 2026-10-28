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

The two images must have the same pixel grid: the tool compares `NAXIS1` and `NAXIS2` and
refuses to copy when they differ, because the solution would point at the wrong pixels.
`--force` copies anyway, for the case you know is right.

**Check the result by eye.** If the image was mirrored or flipped during processing, the
copied solution is wrong without any error being possible: the header cannot know what
happened to the pixels. Open the result in Siril and look at the annotation before trusting
it.

## The console

```bash
copywcs <solved.fit> <destination.fit> [options]
```

| Option | What it does |
| --- | --- |
| `-o, --output <file>` | Write a new file instead of changing the destination in place |
| `--dry-run` | Show the cards that would be copied, without writing anything |
| `--force` | Continue even when the images' dimensions differ |
| `--no-optical` | Copy only the WCS keys, not the optical metadata |
| `--lang en\|ro` | The language of the texts: `en` (English, the default) or `ro` (Romanian) |
| `-h, --help` | Show the usage and the warranty notice |

Every run begins with the version (`copywcs 1.0.0`) and the copyright line, so a pasted output
says which version produced it. Exit codes: 0 on success or a dry run, 1 when the copy was
refused (a file that cannot be read, different dimensions without `--force`, no relevant key,
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
or "Send to" works), choose whether to write into the destination or into a new file, keep or
drop the optical metadata, allow or refuse different dimensions, then **Preview** or **Copy**.
Under each file box the window shows what it read from the header (`Source: 5601 x 3692,
plate-solved`, `Destination: 5601 x 3692`); after a copy the destination reads `already
plate-solved`.

The window computes nothing of its own: Preview lists exactly the cards `copywcs --dry-run`
would print, Copy writes exactly the bytes the console writes, and a refusal or a warning is
shown with the console's words (a refused copy writes nothing). The band at the top shows the
version, the copyright and one sentence on what the tool does; its **About** button (or F1)
opens the version, the copyright and the GPLv3 notice. The window is Windows-only; the console
builds anywhere.

## The two languages

The texts are English unless `--lang ro` asks for Romanian (written without diacritics), in
the console and in the window alike; a `--lang` value that is not `en` or `ro` stops the
console with exit 1 and opens the window in English with a message saying so. The `HISTORY`
card written into the file is English in both languages: it is data other programs read, not
interface text. Numbers never change with the language.

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

## Licence

Copyright (C) 2026 Tudor Carean. This program comes with ABSOLUTELY NO WARRANTY, not even the
implied warranty of merchantability or fitness for a particular purpose. It is free software:
you may redistribute and modify it under the terms of the GNU General Public License as
published by the Free Software Foundation, either version 3 of the License, or (at your option)
any later version. The full text is in `LICENSE`, or at
<https://www.gnu.org/licenses/gpl-3.0.html>.
