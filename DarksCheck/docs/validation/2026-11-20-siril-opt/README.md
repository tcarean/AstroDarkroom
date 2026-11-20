# Siril's -opt on mismatched darks, 2026-11-19 and 20

The result files of the measurement ADR 0070 rests on. The frames were copies, deleted after
the run; the originals were hashed before and after and did not change. Siril 1.4.4, Nikon
D3300, ISO 1600, 120 s, L-eNhance filter; every third light of the Veil nights of 2026-10-06
(V1006), 2026-11-06 (V1106) and 2026-11-08 (V1108) against the library masters idx13 to idx67
(D13 .. D67).

- `pairs.json` - the 18 pairs: the right factor (median, lowest, highest over the lights), the
  factor Siril's `-opt` printed (`k0_*`), and what is left at the master's hot pixels per light,
  by class of the master's level in ADU, for `opt` (Siril's factor), `stk` (the dark as it is)
  and `fix` (the dark multiplied by the right factor); `*_beyond5` is the share (0 to 1) of a
  class's pixels left more than 5 times the noise floor (`floor`, in ADU) away.
- `stack-veil-2026-11-06-idx33.json`, `stack-veil-2026-10-06-idx18.json` - the two stacks of 28
  lights on one shared registration, the three ways compared: the stack's noise, the difference
  of `stk` and `opt` from `fix`, the dots left, per colour.
- `darkscheck-on-the-copies.csv` - what DarksCheck itself read on twelve of the pairs: ratio,
  degrees, what the subtraction leaves and the verdict as it was worded then.

Nothing here is read by a test.
