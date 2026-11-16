# 0104. At most 256 levels in every plane is an 8-bit file, whatever the conversion

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

`BitDepthAnalyzer` called a 16-bit file an 8-bit upscale only by one form: over half of the
samples equal to their high byte times 257, what Siril, GIMP and ImageMagick write. It also
counted the distinct levels of each plane, which reached no rule and were printed only inside
the warning the first form decided. The code review of 2026-11-13 (CR-008) gave the case: a
16-bit TIFF whose samples are a byte copied into the high byte (`level x 256`) has none of the
first form beyond level 0, reads as real 16-bit data with at most 256 levels per channel, and
gets no "only 8 bits of information" warning (R-096). The owner was asked on 2026-11-16 which
rule catches it, and chose the levels.

## Decision

A 16-bit integer file is an 8-bit upscale when over half of its samples are `level x 257`, as
before, or when no plane uses more than `Thresholds.EightBitLevels` (256) distinct levels. The
warning's sentence is the same; its evidence is the clause that held: the fraction with the
levels beside it, or, without that form, the levels alone ("different levels used on each
channel: 256 / 256 / 256, at most the 256 that 8 bits can hold, of the 65536 a 16-bit file has").

## Alternatives rejected

- **Add only the shift form** (over half of the samples with a low byte of 0). It catches the
  case of the entry and misses every other conversion: a constant low byte that is not 0, a
  table.
- **A limit on the samples a plane must have.** A plane of 256 pixels or fewer cannot use more
  levels, and a flat synthetic plane uses one; neither is a file sent to print, and "8 bits of
  information" is true of both.

## Consequences

- Real 16-bit data is nowhere near the limit: the reference image uses 63862 / 39918 / 42878
  levels (V-001).
- One level more than 256 in one plane and the levels no longer say 8 bits: an 8-bit image with
  a few 16-bit pixels painted in passes as 16 bits, as it did.
- Guards: `R031_AnEightBitImageShiftedIntoTheHighByteIsCaughtByItsLevels`,
  `R031_OneLevelMoreThanEightBitsHoldIsNotAnUpscale`,
  `R096_AShiftedEightBitFileIsWarnedOfByItsLevels`.
