# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, in the gate: the fixture is in the repo, ADR 0005)
and is explained here. A figure changes only after a re-measurement, with a dated line under
History saying old value, new value and why.

## The fixture

`tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit` is the header of
`E:\Photografie\Astro\Veil\2026-10-06-3h\result_final.fit`, the owner's stack of the Veil
session of 2026-10-06 (80 frames of 120 s, Nikon D3300, 130 mm), plate-solved by Siril 1.4.4
(`PROGRAM = 'Siril 1.4.4'`, `PLTSOLVD = T`, `CTYPE1 = 'RA---TAN-SIP'`, a SIP polynomial of
degree 3): its three 2880-byte blocks up to and including the one that holds `END`, 8640 bytes,
nothing of the image. The header holds no `SITELAT`, `SITELONG` or `SITEELEV`. The tool reads
nothing past the header, so a dry run on it is a run on the real file.

## Cases

| ID | Fixture | Measured on | What it guards |
| --- | --- | --- | --- |
| V-001 | `veil-2026-10-06-siril-1.4.4-solve.fit` | 2026-10-26 | R-007 and R-024 on a real Siril solve: the image is 5601 x 3692; the keys selected are 72 with the optical ones and 61 without, in the order `validation.json` lists them: 21 WCS keys (Siril writes `PC`, `CDELT`, `LONPOLE` and `EQUINOX`; no `WCSAXES`, `RADESYS`, `LATPOLE`, `CROTA` or `CD` matrix), the 40 SIP coefficients `A_`, `B_`, `AP_`, `BP_` of degree 3 in the file's order, then 11 optical keys (`FOCALLEN`, `XPIXSZ`, `YPIXSZ`, `XBINNING`, `YBINNING`, `OBJCTRA`, `OBJCTDEC`, `RA`, `DEC`, `DATE-OBS`, `INSTRUME`; no `FOCAL`, `OBJECT`, `SITE*` or `TELESCOP` in a Siril DSLR stack). The console's `--dry-run` on the fixture against a header-only destination of the same size lists the same 72 cards. |

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
`--no-optical`.

## History

- 2026-10-26: V-001 recorded from the console's dry run on the fixture, cut the same day from
  `result_final.fit` (SHA-256 of the fixture in `tests/testdata/manifest.json`). The plan had
  named a `pp_light` solve of the same session; those files turned out unsolved (no `CTYPE1`),
  so the session's solved stack was taken instead (ADR 0005).
