# 0005. Validation on the header of a real Siril plate solve, kept as a fixture in the repo and run in the gate

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

Every FITS the tests read is built in memory by `FitsTestData` from hand-written cards; nothing
proved what the tool selects from a header Siril actually writes, which is the tool's whole
use. The kit asks for acceptance figures measured on real data (the kit's rule "Measured
numbers"). A solved FITS of the owner's is hundreds of megabytes, but the tool reads nothing
past the header, and a Siril header is three 2880-byte blocks. The plan (Q6) recommended a
`pp_light_000NN.fit` solve of the Veil session of 2026-10-06; on 2026-10-26 those files turned
out unsolved (27 cards, no `CTYPE1`: the solves DitherCalculator recorded were run on private
copies). A scan of `E:\Photografie\Astro` found 150 solved files, none with `SITELAT`,
`SITELONG` or `SITEELEV`; among them the session's own stack, `result_final.fit`.

## Decision

- The fixture `tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit` is the header
  of `E:\Photografie\Astro\Veil\2026-10-06-3h\result_final.fit`: its blocks up to and
  including the one that holds `END` (8640 bytes), byte for byte, nothing of the image. It is
  listed in `tests/testdata/manifest.json` with its SHA-256, which the gate checks, and
  `.gitattributes` keeps git from touching its bytes (the kit's ADR 0014). The header was read
  whole before it was committed: it holds the camera, the exposure, the date, the pointing and
  the solve, and no observing site.
- V-001 (`docs/validation.json`, `ValidationTests`, R-024) records the image size and the two
  key lists the tool selects from it, with and without the optical keys, measured with the
  console's `--dry-run` on 2026-10-26: 72 and 61 keys. The test also runs the console on the
  fixture and compares the listed cards.
- The case runs in the gate: the fixture is in the repo, so `testFilter` in `kit.json` stays
  empty and no `Category=Validation` trait is needed. A header-only fixture is not an edited
  header: no card was changed or removed.

## Alternatives rejected

- **No validation case, as AstroDarkroom.** The tool's value is which cards a real solve yields;
  the in-memory fixtures prove the mechanics, not that.
- **A `pp_light` solve, as the plan said.** None exists on disk; solving one now would make a
  file for the test rather than take a real one. The session's stack is the real case anyway:
  a solved stack is what the user copies the WCS from.
- **The whole file under `large/`, out of the gate.** 248 MB for 8640 useful bytes, and a gate
  that passes without the file and fails with it is not one gate.
- **Blanking a site key had there been one.** An edited header is not a real one; the owner
  would have picked another file.

## Consequences

- A change to the key lists of `WcsMetadataKeys` shows up in V-001 and is re-measured and
  recorded in `docs/validation.md` before the figure changes (the kit's rule "No test edited to
  pass").
- A second real header (another program's solve, a `CD` matrix, a `WCSAXES`) is one more
  fixture and one more case, the same way.
