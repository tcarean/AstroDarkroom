# 0059. Biases at another ISO than the darks are left out, before they are read

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The biases of a session serve the darks in three places: their read noise is taken out of the
darks' noise in check 5, their mean map is subtracted from the darks' for the leak map and the
sides of check 3 (ADR 0014), and for FITS darks, which carry no black level, the biases'
median is the black (ADR 0007). Nothing compared the biases' ISO with the darks': only their
size was checked (R-041). The code review of 2026-11-13 (CR-010) followed the case of darks at
ISO 1600 with `biases\` at ISO 400: check 5 reads the noise the exposure adds from a read
noise of another gain, too large and with no word, and check 3 subtracts a readout pattern of
another gain. ADR 0012's table has the bottom edge of the bias at 0.38 ADU at ISO 400 and 0.58
at ISO 1600, a difference as large as the limit of the constant light, `Th.LeakConstantAdu`
(0.15). The entry is `confirmed`; it named two directions, leaving the biases out or saying
the mismatch in check 1. The plan of 2026-11-14, approved by the owner, took the first.

## Decision

Biases whose ISO is not the darks' are left out of every figure, with the WARNING line biases
of another size get, `the biases could not be used: the biases are at ISO 400, the darks at
ISO 1600`, and the run exits 1. The two ISOs are the inventory's dominant ones (R-007's
clusters), the partner's darks in a lights-only run; when either group does not give its ISO
the biases are used as before. The decision is taken in `DarkSeriesRun.Run` before a bias is
read, so the biases' stack gives the darks no black either.

## Alternatives rejected

- **Only a line in check 1.** The figures of checks 3 and 5 would stay wrong under a warning
  the reader has to connect to them.
- **Bring the biases to the darks' ISO by the gain.** The read noise and the readout pattern
  do not scale with the gain as a signal does (ADR 0012's table: 0.38 against 0.58 ADU for
  four times the gain).
- **Keep them for the black of FITS darks alone.** "Left out of every figure" is R-041's rule
  for unusable biases, and a pedestal read at another ISO is one more thing to argue for.
- **Compare after both groups are measured, as the size is.** The darks would already have
  taken their black from the biases (CR-011, open, is that defect for the size).

## Consequences

A session whose biases have the darks' ISO, or do not give one, reads as before; no golden
file changed. The owner's two sessions with biases have them at the darks' ISO (Smida at ISO
400, the leak set at ISO 1600). Whether a verdict flips on real biases of another ISO was "to
measure" in the entry and was not measured: with the biases left out the question no longer
decides anything. Biases of another exposure need no rule: check 1 already asks them to be
short. Guarded by `R041_BiasesAtAnotherIsoThanTheDarksAreReportedAndLeftOut`.
