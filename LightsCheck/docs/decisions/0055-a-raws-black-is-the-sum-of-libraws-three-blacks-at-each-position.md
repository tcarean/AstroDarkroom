# 0055. A raw's black is the sum of libraw's three blacks at each position

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

`RawReader.Decode` took the black as `color.black` plus `color.cblack[i]` for i = 0 to 3, and
the median of the four. The review of 2026-11-13 (CR-009, `to measure`) said that libraw keeps
a third part, the repeat pattern a DNG states with `BlackLevelRepeatDim`, in `cblack[6]` and
on, with its size in `cblack[4]` and `cblack[5]`, and that `unpack()` does not fold it into the
others. Shown on 2026-11-14 on a DNG written with `BlackLevelRepeatDim 2 2` and `BlackLevel 64
64 64 64`: libraw's line read `black 0 per channel [0, 0, 0, 0]`, the scale came out `0 ..
16383` and every level 64 ADU too high, where the same pixels with one `BlackLevel 64` read
`black 64` and the scale `64 .. 16383`. No such file from a camera was at hand, and libraw's
source was not read here; the review's reading of it is the reviewer's.

## Decision

The black of each of the four positions of the visible mosaic's first 2 x 2 block is the sum of
the three parts (`RawReader.BlackLevels`): the common black; the black of the position's colour,
the colour index being dcraw's `FC` of `filters` at the visible origin, as
`CfaPattern.FromLibRaw` evaluates it, and the position's own number when the mask has no 2 x 2
order; and the pattern's value at that position, laid from the first visible pixel. The frame's
black stays the median of the four. A pattern larger than 2 x 2 is read at that first block
only, and one whose values are not all in the array counts as none.

## Alternatives rejected

- **The four blacks kept by colour index, as the other two parts were summed, with the
  pattern added to them.** The pattern is laid by
  position and the per-colour blacks by colour, so the sum is only right per position; the
  median is the same on a raw whose four colour indices differ, which a D3300's do.
- **The median over the whole pattern, whatever its size.** One more rule for a case no file
  showed; the first block is what the levels are read against.
- **Asking libraw to subtract the black itself.** That is its developing path, and the tool
  reads the mosaic undeveloped, at the original scale (R-007).

## Consequences

A DNG with a 2 x 2 or a 1 x 1 pattern reads its black. A D3300 raw reads 150 as before
(`black 150 per channel [150, 150, 150, 150]` on the Veil session, 2026-11-14), and the 14
validation cases pass. A raw of another camera whose black libraw holds as a pattern would now
read it too, with levels lower by that black than before; which cameras those are was not
measured. Guarded by `R007_TheBlackOfEachPositionSumsTheCommonTheColoursAndThePatterns` and,
end to end on files the test writes (`DngTestData`),
`R007_ADngWhoseBlackIsATwoByTwoPatternIsReadWithThatBlack` and
`R007_AGeneratedDngDecodesToItsMosaicOnItsOwnScale`.
