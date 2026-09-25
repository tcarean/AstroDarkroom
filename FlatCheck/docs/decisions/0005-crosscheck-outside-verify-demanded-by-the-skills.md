# 0005. The Python cross-check stays outside verify; the skills demand it when a formula changed

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

`flatcheck.py` is the numeric oracle: `crosscheck.py` runs both implementations over 43 identical
FITS scenarios and compares the full text and every numeric token (42 of 43 agree numerically;
see `docs/design/python-parity.md`). It is the only check that catches a formula drift the golden
files do not (the goldens are C#-only snapshots). It needs Python with numpy, astropy and rawpy,
present on the owner's machine and on no CI runner the kit's templates describe; it takes the
fixture dump test plus a Python run, about a minute.

## Decision

`scripts/crosscheck.ps1` wraps the two steps (dump with `FLATCHECK_DUMP_FIXTURES`, then
`crosscheck.py`) and is not part of `scripts/verify.ps1`. CLAUDE.md rule 16 and the `/new-feature`
and `/wrap-up` skills require it whenever a change touched `FlatCheck.Core/Metrics.cs`,
`Stats.cs`, `Analysis/`, `Report/` or `flatcheck.py`, with the summary pasted in the session.
`check-staged.ps1` and the Stop hook treat `flatcheck.py` as code, so a Python-side change also
needs a CHANGELOG line and a green verify.

## Alternatives rejected

- **A verify step that runs crosscheck when Python is present and skips otherwise.** A silent
  skip is how a parity break reaches `main` on a machine without Python; a gate is either run or
  red.
- **A verify step that requires Python.** The gate would then depend on a second toolchain, and
  the kit's CI templates would fail on every runner.
- **Drop the Python side.** It is the reference the C# was written against and the only
  independent implementation of the formulas; ADR 0038.

## Consequences

Parity depends on the model or the owner running the script after a formula change; the Stop
hook does not enforce it. `VersionTests.R097_PythonReportsTheSameVersionAsTheCSharp` and
`crosscheck.py`'s own version check keep the two versions in step, and `scripts/release.ps1`
writes both.
