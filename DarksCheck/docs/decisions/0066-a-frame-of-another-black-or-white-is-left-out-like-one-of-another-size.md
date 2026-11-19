# 0066. A frame of another black or white is left out, like one of another size

Date: 2026-11-19
Status: accepted 2026-11-19

## Context

`FrameStack.Load` compared each decoded frame with the frame that set the stack on its ISO, its
exposure and its size, and never on its black and white: the stack's scale was the first
frame's, and every later frame was measured against it. The code review of 2026-11-13 (CR-016,
`confirmed`) gave two inputs. 12-bit and 14-bit raws of one body in one folder have the same
size, ISO and exposure, and a black of 150 against 600 and a white of 4095 against 16383: the
later frames were measured on the first one's scale. And a float FITS was read as "on 0 .. 1"
only when its largest value was at most 1, the decision taken per frame, so in a set on 0 .. 1
one frame with a single value just over 1 was "kept in ADU" and rounded to zeros and ones.

No file of either kind is at hand: the owner's camera writes one bit depth, and the tests
generate FITS. The rule below is therefore reasoned, not measured, and says so.

## Decision

The first frame that decodes with the group's setting sets the size and the scale, as before,
and a later frame is left out, with its error, when its scale is not that one
(`FrameStack.SameScale`): its white differs from the stack's by more than `Th.ScaleTolerance`
(1 %) of the stack's white, or its black does, by the same amount, when both files carry a
black. The row reads `different black and white: 150 and 4095 instead of 600 and 16383`
(`negru si alb diferite: ... fata de ...`), or `different white: 255 instead of 65535` (`alb
diferit`) for a FITS, which carries no black; the wording follows `different size: ... instead
of ...`. A frame is named for the first reason that holds: its settings, its size, then its
scale. One `Load` serves the darks, the biases and a partner's darks.

The limit is 1 % of the white and not "any difference" because some cameras write a black
measured in each frame (from the masked pixels), which moves by a few ADU from frame to frame;
such frames belong to one series and must stay. Files of another bit depth differ by 450 ADU in
black and four times in white, far over the limit, which is 164 ADU at 14 bits and 41 at 12.

A float FITS is taken as already in ADU when more than `Th.FloatAduMinShare` (0.1 %) of its
values are over 1, each frame on its own (`FrameStack.ToUshort`, which the lights' `DecodeOne`
shares). A frame in ADU has nearly every value over 1, a pedestal or noise of several ADU; a
frame on 0 .. 1 has at most a few, which clip at the white.

## Alternatives rejected

- **Any difference of black or white.** It would drop good frames of a camera whose black is
  measured per frame, on files that cannot be tried here.
- **Each frame measured on its own black and white.** The stack is one array of 16-bit values
  with one black; frames of two bit depths have two gains as well, so their values do not
  compare even after each black is subtracted.
- **The scale most frames have decides, as the setting does (ADR 0062).** The scale is known
  only once a file is decoded, while the setting is read from the metadata of every file first;
  the size has the same limit and stays the first frame's.
- **A float frame of another reading listed as another scale.** In a set on 0 .. 1 the frame
  with a value over 1 is a good frame; reading it right keeps it.

## Consequences

Files of two bit depths in one folder: those unlike the first frame are listed and the run
exits 1. The limit kept, as for the size: when the stray file is the first, the others are the
ones left out, each row saying why. A black that differs by less than 1 % of the white is
measured against the first frame's, as before. Mixed float sets (some on 0 .. 1, some in ADU)
are each read on their own reading and share the white of 65535, as before. No golden file
changed. Guarded by `R020_AFrameWithAnotherWhiteIsLeftOutWithItsError`,
`R020_ABlackOrWhiteWithinOnePercentOfTheWhiteIsTheSameScale`,
`R020_AFloatFrameWithOneValueOverOneIsStillReadOnZeroToOne` and
`R020_AFrameWithAnotherWhitePrintsItsReasonAndTheOthersAreAnalysed_Exit1`; the raw case is
covered by the rule's test alone, since no test decodes a raw.
