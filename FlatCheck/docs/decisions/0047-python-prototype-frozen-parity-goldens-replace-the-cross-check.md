# 0047. The Python prototype is frozen at 2.2.0; 43 parity goldens replace the cross-check, and /validate replaces the parity rule

Date: 2026-09-26
Status: accepted 2026-09-26
Supersedes: 0005, 0038

## Context

Since the port, `flatcheck.py` was the numeric oracle: `crosscheck.py` ran both implementations
over 43 identical FITS scenarios and compared every number (ADR 0038), rule 16 and the
`/new-feature`, `/wrap-up` and `/release` skills demanded it after any formula change (ADR
0005), `scripts/release.ps1` bumped `__version__` with `<Version>`, two tests tied the Python
version and copyright to the C#, and the hooks treated `flatcheck.py` as code. Every formula
therefore had to be written twice, in two languages, for a prototype nobody runs. On 2026-09-26
the owner decided to stop maintaining it. Measured that day on `main` at 2.2.0, before anything
changed:

```
scenarii              : 43
identice la byte      : 26
doar text (numere ok) : 16
divergente cunoscute  : 1
DIFERENTE NUMERICE    : 0 []
```

So on that day the console printed, for all 43 scenarios, numbers an independent implementation
had confirmed. Of the 43, about 11 shared their bytes with a golden file in `GoldenOutputTests`,
about 22 had their branch covered by a unit test (exposure bands, BITPIX, `BOTTOM-UP`, a
constant frame, nebulosity, the dust spot, the float 0..1 levels line), and about 10 had no C#
test at all (the offset pedestal, 768x512 and 512x768 end to end, `--ratio` mild/heavy and
wide/wide, `--fov` override, a series with `--pattern`).

## Decision

1. `flatcheck.py` and `crosscheck.py` stay in the repository, frozen at 2.2.0 and unmaintained:
   no version sync, no mirrored formulas, no cross-check. `scripts/crosscheck.ps1` is deleted;
   `release.ps1` writes `<Version>` only; `check-staged.ps1` and the Stop hook no longer treat
   `flatcheck.py` as code; the two tests that read `__version__` and `__copyright__` are
   deleted. The files keep their GPL notice and R-096 still scans them.
2. All 43 scenarios become golden files, `Golden/parity_*.txt`, captured on 2026-09-26 from the
   console while the prototype still agreed: `ParityGoldenTests` (R-098) writes the same
   fixtures (`ParityFixtures`, the former body of `CrossCheckFixtures`) and pins text and exit
   code. These goldens carry the oracle's last word; a re-approval there is a C#-only decision
   from now on and is read as such. `CrossCheckFixtures` keeps the env-gated dump, so the frozen
   prototype can still be compared by hand.
3. Rule 16 becomes: a formula change re-runs `/validate` on the real masters before the task
   ends and pastes the drift table. `validation.json` holds 40 cases, several of them confirmed
   by the earlier numpy bench (`docs/validation.md`, "earlier figure"), and the parity goldens
   catch a synthetic drift in the gate itself.

## Alternatives rejected

- **Delete the two Python files.** The owner chose to keep them as prior art and as the frozen
  reference the parity goldens were captured against; git history alone would make the dump
  test meaningless.
- **Snapshot only the ~10 scenarios without C# coverage.** A unit test covers a branch, not
  the printed text of the whole chain on that input; the 33 others cost nothing but 33 files
  read once, and the set stays the one the prototype was compared on.
- **Keep the cross-check optional, run when Python is present.** ADR 0005 already rejected a
  check that silently skips; an unmaintained prototype would drift on the first formula change
  and the optional check would turn red for a reason nobody acts on.
- **Delete everything and rely on the existing goldens.** Loses the only confirmed numbers for
  the ~10 uncovered scenarios, for no gain.

## Consequences

- There is no independent implementation any more. A formula change is judged by the parity
  goldens (synthetic, confirmed once) and by `/validate` (real masters, figures recorded with
  their provenance). A new numeric branch gets a parity golden scenario the same way it got a
  cross-check one (`docs/design/process.md`, "Adding a parity golden scenario").
- ADR 0039 is amended: the version is declared once, in `Directory.Build.props`; `flatcheck.py`
  keeps `__version__ = "2.2.0"` as the mark of where it froze. The FitCenter guard of ADR 0038
  stays as it was, and its reason is kept in `docs/design/python-parity.md`.
- ADR 0042 is amended too: the `crosscheck.py` guards it names (`norm()`, `check_versions()`)
  went with the cross-check; `ScrubVersion` in `GoldenOutputTests` is the only scrub left.
- Guards: `ParityGoldenTests` (43 scenarios plus a count and uniqueness check), the 43
  `Golden/parity_*.txt`, `R097_TheVersionComesFromDirectoryBuildProps`, `ValidationTests`.
- Source notes: `docs/design/python-parity.md` (the frozen prototype, the last result, the
  intentional divergences), `docs/design/process.md` (adding a scenario).
