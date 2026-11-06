# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, in the gate: the fixtures are in the repo, ADR 0005)
and is explained here. A figure changes only after a re-measurement, with a dated line under
History saying old value, new value and why.

## The fixtures

`tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit` is the header of
`E:\Photografie\Astro\Veil\2026-10-06-3h\result_final.fit`, the owner's stack of the Veil
session of 2026-10-06 (80 frames of 120 s, Nikon D3300, 130 mm), plate-solved by Siril 1.4.4
(`PROGRAM = 'Siril 1.4.4'`, `PLTSOLVD = T`, `CTYPE1 = 'RA---TAN-SIP'`, a SIP polynomial of
degree 3): its three 2880-byte blocks up to and including the one that holds `END`, 8640 bytes,
nothing of the image. The header holds no `SITELAT`, `SITELONG` or `SITEELEV`. The tool reads
nothing past the header, so a dry run on it is a run on the real file.

`tests/testdata/fixtures/veil-2026-10-06-syqon-cli-1.1.8-denoised.fit` is the header of
`E:\Photografie\Astro\Veil\2026-10-06-3h\procesate\bge-cc\denoised_syqon_studio_in.fit`, a
processed version of the same stack, denoised by the SyQon CLI 1.1.8
(`SYQON_CLI_MODEL = 'prism-max'`): its four blocks, 11520 bytes, nothing of the image. Its header
declares the rows top-down (`ROWORDER = 'TOP-DOWN'`, `SYQON_SOURCE_ROWORDER = 'BOTTOM-UP'`,
`SYQON_WCS_ROW_TRANSFORM = 'FITS_BOTTOM_UP_TO_TOP_DOWN'`) and holds a `CD` matrix beside Siril's
`PC` and `CDELT`: `CD1_2` and `CD2_2` have the opposite sign of `CDELT1 * PC1_2` and
`CDELT2 * PC2_2`, so the two describe solutions mirrored top to bottom. The image itself is
flipped: measured on the real files, rows 200, 1000, 2800 and 3600 of the stack
`procesate\bge-cc\result-bge-cc.fit` correlate 0.98 to 1.00 with the mirrored rows of this file
and about 0 with the same rows, and its `CRPIX2` (1846) and SIP coefficients are the flipped
ones, so its `CD` is the solve that fits it (ADR 0012). It is 5601 x 3692, like the first, and
holds no site keys either.

## Cases

| ID | Fixture | Measured on | What it guards |
| --- | --- | --- | --- |
| V-001 | `veil-2026-10-06-siril-1.4.4-solve.fit` | 2026-10-26 | R-007 and R-024 on a real Siril solve: the image is 5601 x 3692; the keys selected are 72 with the optical ones and 61 without, in the order `validation.json` lists them: 21 WCS keys (Siril writes `PC`, `CDELT`, `LONPOLE` and `EQUINOX`; no `WCSAXES`, `RADESYS`, `LATPOLE`, `CROTA` or `CD` matrix), the 40 SIP coefficients `A_`, `B_`, `AP_`, `BP_` of degree 3 in the file's order, then 11 optical keys (`FOCALLEN`, `XPIXSZ`, `YPIXSZ`, `XBINNING`, `YBINNING`, `OBJCTRA`, `OBJCTDEC`, `RA`, `DEC`, `DATE-OBS`, `INSTRUME`; no `FOCAL`, `OBJECT`, `SITE*` or `TELESCOP` in a Siril DSLR stack). The console's `--dry-run` on the fixture against a header-only destination of the same size lists the same 72 cards. |
| V-002 | `veil-2026-10-06-siril-1.4.4-solve.fit` into `veil-2026-10-06-syqon-cli-1.1.8-denoised.fit` | 2026-11-06 | R-016 and R-017 on a real destination that holds a plate solve of its own. The two solves are mirror images, `mirrored` in `validation.json`: read `CD` first, the source's determinant is -2.9641E-6 and the destination's +2.9641E-6, so without `--force` the copy is refused, a dry run too (R-017). Forced, a copy of V-001's solve copies the same 72 keys and drops exactly the 4 keys the destination has and the source lacks, `CD1_1`, `CD1_2`, `CD2_1`, `CD2_2`, in that order; its SIP coefficients are the source's 40 and are replaced, not dropped. The file written holds no `CD` card. |

## How to re-measure

```bash
dotnet test CopyWcs.sln -c Release --filter "FullyQualifiedName~ValidationTests"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures. The measurement itself was the console:

```bash
copywcs tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit <dest 5601x3692> --dry-run
```

whose third line was `Keys that would be copied (72):` on 2026-10-26, and 61 with
`--no-optical`. For V-002, the dry run of the first fixture into the second:

```bash
copywcs tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit tests/testdata/fixtures/veil-2026-10-06-syqon-cli-1.1.8-denoised.fit --dry-run --lang en
```

which, with the Release build of the branch that brought R-016 and R-017, printed on
2026-11-06 the banner, the copyright and the refusal of R-017 on stderr, exit 1:

```
The destination's own plate solve is a mirror image of the source's: the image was mirrored or flipped after the source was solved, so the copied WCS would be wrong. Flip the image back first, or keep its own plate solve. Use --force if you know what you are doing.
```

and with `--force` added (77 lines, exit 0; the 72 cards between the fourth line and the last):

```
copywcs 1.0.0
Copyright (C) 2026 Tudor Carean
WARNING: the destination's own plate solve is a mirror image of the source's. Continuing anyway (--force).
Keys that would be copied (72):
  CTYPE1  = 'RA---TAN-SIP'       / TAN (gnomic) projection + SIP distortions
...
  INSTRUME= 'Nikon D3300'        / Instrument name
Old plate-solve keys of the destination that would be dropped (4): CD1_1, CD1_2, CD2_1, CD2_2
```

The log of the refused run holds `copy done refused: mirrored plate solve (source det
-2.9641E-6, destination det +2.9641E-6)`.

## History

- 2026-10-26: V-001 recorded from the console's dry run on the fixture, cut the same day from
  `result_final.fit` (SHA-256 of the fixture in `tests/testdata/manifest.json`). The plan had
  named a `pp_light` solve of the same session; those files turned out unsolved (no `CTYPE1`),
  so the session's solved stack was taken instead (ADR 0005).
- 2026-11-06: V-002 recorded from the console's dry run above, the second fixture cut the same
  day from `denoised_syqon_studio_in.fit` (ADR 0011). The same day the review of the branch
  found the pair to be mirror images, and R-017 (ADR 0012) refuses it without `--force`: V-002
  gained `mirrored: true`, its copy is measured forced, and the dry run above was measured
  again with the build that refuses (76 lines without the warning before, 77 with it now).
