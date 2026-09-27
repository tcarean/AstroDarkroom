# The frozen Python prototype and the parity goldens

> Moved from CLAUDE.md on 2026-09-25 (ADR 0001), rewritten on 2026-09-26 when the cross-check
> was retired (ADR 0047), then cut on 2026-09-27 to what neither CLAUDE.md nor a decision record
> says (ADR 0051): the retirement and its last result are ADR 0047, the divergences ADR 0038,
> the `FitCenter` guard ADR 0014. **Covers:** in full, `FlatCheck.Tests/ParityGoldenTests.cs`,
> `ParityFixtures.cs`, `CrossCheckFixtures.cs` and the coverage of `Golden/parity_*.txt`; by
> pointer, `flatcheck.py` and `crosscheck.py` (frozen at 2.2.0) and the intentional divergences
> of the frozen file. A change to the code this describes means this note is updated in the
> same task, or deleted with the decision it explains.

## What the cross-check was

ADR 0047 (and the superseded ADR 0005 and ADR 0038) hold it: `flatcheck.py` as the numeric
oracle, `crosscheck.py` over 43 scenarios comparing the full text and every numeric token (as
floats, tolerance 1e-9), the check outside the gate because it needed a second toolchain, and
its last run on 2026-09-26 (26 byte-identical, 16 text-only, 1 known divergence, 0 numeric
differences).

## What replaced it

ADR 0047 holds the decision (the two files frozen at 2.2.0 and unmaintained, the 43 scenarios
as golden files carrying the oracle's last word, a re-approval there as a C#-only call, rule 16
pointed at `/validate`); CLAUDE.md "Layout" says the same in one line. The mechanics:

- `ParityFixtures.WriteAll` writes the same synthetic FITS the cross-check used, with the same
  names.
- `ParityGoldenTests` holds the scenario table, name and arguments exactly as `SCENARIOS` in
  `crosscheck.py` had them, runs the console in-process over each one through
  `GoldenOutputTests.RunCli` (same scrubbing of paths and banner) and compares with
  `Golden/parity_<slug>.txt`, where the slug is the scenario name lower-cased with every run of
  non-alphanumerics turned into `_` (`expo 25% --sky` is `parity_expo_25_sky.txt`). The exit
  code is the last line of each file, `[exit N]`.
- `CrossCheckFixtures.DumpsFixturesWhenAsked` still dumps the fixtures to the folder named by
  `FLATCHECK_DUMP_FIXTURES`, so `python crosscheck.py <folder>` can be run by hand against the
  frozen prototype for as long as its output means anything. Nothing requires it.

## Coverage of the 43 scenarios

Kept whole on purpose (ADR 0047): 15 structure, 4 `--ratio`, 11 `--light` (three of them
series folders), 13 `--expo`. Across them: all four residual bands (`grad_bun`, `grad`,
`grad_refa`, plus the clean field), both gradient directions and a diagonal, an inverted
(over-corrected) field, a pure sky plane, a constant frame, mild and heavy vignetting, an offset
pedestal, nebulosity in one quadrant, a dust shadow (the local-spot line and its position),
768x512 and 512x768 frames, BITPIX 16, 32 and -32, a float master divided by 65535 as Siril
writes it (the levels line that used to print `0 / 0`), a CFA frame with `BAYERPAT` and the same
with `ROWORDER = 'BOTTOM-UP'` (the phase moves by one row on an even height), the exposure bands
either side of each boundary with and without `--sky`, a 12-bit frame with `--sat` and
`--black`, the `--light` field-of-view refusal and its `--fov` override, and a series with
`--pattern`. The ~11 that share their bytes with a golden in `GoldenOutputTests` printed the
same numbers on 2026-09-26, which was the sanity check of the capture.

## Intentional divergences of the frozen file

ADR 0038 lists them (no 4-panel figure, no `suggest_shutter`, `Percentile` on an empty list,
`libraw` against `rawpy` in two lines, no console histograms, `--sat=4095` against
`--sat=4095.0`, the capitalised `--light` diagnosis, and the five modes that exist only in C#);
ADR 0047 and CLAUDE.md ("Layout") make the prototype prior art, not a reference, so nothing is
"fixed" on either side. ADR 0014 and ADR 0038 hold the one numeric divergence: on a perfectly
constant frame the C# guard `|det| < 1e-15` in `FitCenter` prints `nedeterminat` where Python
placed the illumination peak at (136, 136) with a 6.35 % offset; the C# is the correct one and
stays (pinned by `FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre` and
`parity_structure_uniform.txt`). Two facts the ADRs do not spell out:

- Structure with `--raw` / `--lens` exists only in C#; without the two flags the output is
  identical, which the 15 structure scenarios pin.
- The capitalised diagnosis ("Fondul CADE/CRESTE spre margini") accounted for the wording-only
  differences in the `--light` scenarios that print one; the sign *is* the diagnosis, so the
  emphasis is deliberate.

The two conventions kept for parity, `LargeScalePtp`'s top-left grid and the half-pixel centre
of known issue 5, are ADR 0016 and ADR 0013.
