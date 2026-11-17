# 0111. The x 257 share leaves out pure black and pure white

Date: 2026-11-17
Status: accepted 2026-11-17
Supersedes: 0105 in part (the x 257 share counted over every sample)

## Context

A 16-bit file is called an 8-bit upscale when over half of its samples equal their high byte
times 257 (R-031, ADR 0105). Level 0 is of that form, and so is 65535, in a real 16-bit file as
in an upscale, and those are the two values clipping gathers samples at. The review of the fixes
of CR-075 and CR-076 found it (CR-078 of the code review of 2026-11-13).

Measured on 2026-11-17, before the fix: real 16-bit data (uniform, three planes of 64 x 64) with
40 % of every plane at pure black and 15 % at pure white read a share of 55.2 %, and the whole
analysis of such a scene gave the warning "the file holds only 8 bits of information ... you
lose nothing going to 8 bits", with thousands of distinct levels printed in its evidence. The
entry's first case, a frame more than half pure black, does not reach the warning: its median is
0 and it is refused as a linear image (R-015). What reaches it is a green plane under half
black with the rest of the share made up elsewhere: pure white beside the black, or the red and
blue planes clipped more than the green one (90 % black on each and 40 % on the green read
about 73 %).

## Decision

The share of samples of the form level x 257 is taken among the samples that are neither 0 nor
65535; when every sample is at an end it is 0. The threshold stays 0.5. The warning's evidence
says what is left out: "... % of the values, pure black and pure white left out, are an 8-bit
level copied into 16 bits" / "... % din valori, fara negrul pur si albul pur, sunt un nivel de 8
biti copiat in 16 biti", the wording the owner approved on 2026-11-17.

## Alternatives rejected

- **Ask the distinct levels too before calling the x 257 form.** An 8-bit image with a 16-bit
  part painted in (a gradient, a watermark) has more than 256 levels and is still mostly an
  upscale; the share alone says so today, and would stop.
- **Leave out all 256 values of that form that a real file can hit.** They are the whole
  measure; only the two ends are special, because clipping puts samples there.

## Consequences

- An upscale stays one: its share is 1 on the samples that can tell, however much of it is
  clipped. A file of pure black and pure white alone has a share of 0 and is 8 bits by its two
  levels (ADR 0105).
- The reference image's share reads 0.39 % for 0.42 % (V-001 `bitDepth.upscaledFraction`
  0.0042 -> 0.0039, recorded at the owner's word of 2026-11-17): about 0.03 % of its samples
  are at the ends, by the two figures. No other figure of the seventeen cases moved.
- A file saved by a shift (level x 256) reads a share of exactly 0, whatever part of it is
  black: of its levels only 0 is also a level x 257, and 0 is left out. It is 8 bits by its
  levels and takes that clause's sentence (ADR 0108), never "you lose nothing".
- Guards: `R031_AShiftedFileHasNoSampleOfTheCopiedForm`,
  `R031_ClippedBlackAndWhiteDoNotMakeAFrameAnUpscale`,
  `R031_AnUpscaleWithClippedAreasIsStillCaught`,
  `R096_AClippedSixteenBitFrameIsNotWarnedOfAsEightBits`,
  `R096_TheShareOfCopiedLevelsSaysWhatItLeavesOut`.
