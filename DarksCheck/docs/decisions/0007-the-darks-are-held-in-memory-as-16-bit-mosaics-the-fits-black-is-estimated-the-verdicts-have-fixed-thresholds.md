# 0007. The darks are held in memory as 16-bit mosaics, a FITS black is estimated, and the verdicts of checks 2 to 8 have fixed thresholds

Date: 2026-10-11
Status: accepted 2026-10-11

## Context

Checks 2 to 8 need a median master, so every frame's value at every pixel must be at hand at
once: 24 MP x 2 bytes = 48 MB per frame as 16-bit, 194 MB as the doubles the readers return.
The owner's series are 20 to 60 frames; the machine has 32 GB and 8 cores. The plan's
decision 8 asked for a measurement before a cap was set. A FITS carries no black level, and a
FITS Siril converts from a raw keeps the camera's pedestal (150 on the D3300): read as 0, every
pixel would count as hot. The prototype's figures (`docs/validation/2026-08-12-smida/`) set
the scale of what a good series looks like: edges within 0.2 ADU of the centre on
dark-minus-bias, a warming of 2.3 and 3.4 °C over a series, residual sigmas within 17 % of
each other, structured banding of 0.5 ADU peak to peak, a master noise of 0.34 ADU against a
step of 2.

## Decision

- The darks are decoded into memory as `ushort` mosaics, `threads` at a time (half the cores
  by default), the first file alone so it sets the geometry; the readers' doubles are
  converted on arrival and a forced collection runs per frame. Measured on 2026-10-11: with
  neither that collection nor a GC setting the working set reached 14 GB on the Smida session
  (50 frames), garbage the collector saw no reason to take back; with
  `System.GC.ConserveMemory=9` in the console's runtime config it is 4.5 to 4.7 GB on Smida
  (30 biases then 20 darks, 35 s) and 2.9 to 3.3 GB on the 30-frame library (24 to 26 s) over
  the runs of 2026-10-11 and 2026-10-16, at the same speed.
  Above `Th.MaxFramesInMemory` = 60 frames (2.9 GB of mosaics) every k-th file is decoded.
- The biases are read before the darks and released; a FITS stack's black is the median of
  the first bias' reference plane when there are biases, else of the first dark's, and the
  report says so (`negru 150 estimat din mediana cadrului 1`).
- The six verdicts use the constants of `Thresholds.cs`: a leak or glow at 1 ADU-eq of
  edge-to-centre departure (the largest over the eight regions, brighter read as light,
  darker as a readout or bias offset), a drift at 1 °C by the 6 °C doubling rule, a noise
  outlier at 1.5 x the median residual sigma, a pattern at 2 ADU-eq of structured banding,
  any dead or saturated pixel, and enough frames when there are at least 10 and the median
  master's predicted noise, 1.25 x the temporal sigma / sqrt(N), is under one quantisation
  step (the temporal sigma is the frame-against-frame point of the noise curve, so the fixed
  pattern does not inflate it; the median's factor is sqrt(pi / 2)). Hot-pixel levels are in
  ADU equivalent (ADR 0004) with a floor of 5 raw ADU, so the Smida master counts pixels
  above 19.9 ADU at 119.2 s (443) where the prototype counted above 20 (433): ten pixels sit
  at exactly 20.0; the dead limit stays raw, since a dead pixel does not respond, and the
  biases keep raw levels, since their own exposure is no scale. The random-telegraph limit is
  6 x the median per-frame sigma, as the prototype's. The banding profiles are read with the
  hot pixels clamped to the hot threshold, which the prototype did not do: on a small
  synthetic frame one 600 ADU pixel lifted its row by 4.7 ADU.

## Alternatives rejected

- **Judging "enough" by the spatial sigma over sqrt(N) alone, as the first draft did.** It
  said OK for a single dark on the D3300 (1.51 / 1 < 2) and used the pattern-laden spatial
  sigma for a median master that is 1.25 x noisier than a mean's.
- **Naming a corner only when it beats the strongest edge by a fifth.** The verdict would
  then judge the edge's value and could pass a corner over the threshold.
- **Streaming the median in row bands.** Each band re-decodes every file: 20 decodes per band
  at a second each, minutes per band.
- **Keeping the doubles.** 194 MB per frame, 4 GB for 20 frames, with no gain: the raw values
  are integers.
- **A black of 0 for a FITS.** Every pixel of a Siril-converted dark would be a hot pixel.
- **A rejection-mean master.** The median is what the prototype measured against; a
  sigma-clipped mean is Siril's business.
- **Reading the structured banding with the hot pixels in, as the prototype.** Right on a
  24 MP sensor, wrong on a small frame; the clamp changes the Smida columns from 0.50 to
  0.17 ADU peak to peak (a hot pixel's doing) and nothing else.

## Consequences

R-020 to R-041; the design note `docs/design/dark-analysis.md`; V-003 and V-004 record the
figures. The window (plan step 4) exposes the thread count and the frame cap as settings. The
memory knob lives in `src/DarksCheck.Cli/runtimeconfig.template.json` and the window's project
gets the same file. A library of more than 60 frames reads a subset until the cap is raised.
