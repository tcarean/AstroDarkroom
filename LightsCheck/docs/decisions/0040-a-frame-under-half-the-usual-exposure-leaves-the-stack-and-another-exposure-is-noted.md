# 0040. A frame under half the usual exposure leaves the stack, and any other exposure is noted

Date: 2026-11-07
Status: accepted 2026-11-07

## Context

On the Veil session of 2026-11-06 the intervalometer misfired: among 85 frames of 120.2 s,
frames 58 and 64 were exposed 0.2 s, frame 16 34.9 s and frame 51 137.5 s. The series table
showed no exposure, and the frames came out through other rules: the 0.2 s ones `marginal` for
too few stars (131, "clouds or haze") and a -100 % background jump, the 34.9 s one `good` with
a -71 % jump, the 137.5 s one `good` with a +14 % jump that is exactly 137.5 / 120.2. The owner
asked whether such frames should leave the stack, and whether Siril handles a longer frame.
What Siril does: before stacking it brings every frame to the reference's level by its sky
background and its spread (`additive with scaling`, the default for lights and what the OSC
scripts use), so a frame with 14 % more signal and sky is scaled down by 14 %, noise included,
and the rejection runs after that; the same normalisation carries the night's own +48 % rise of
the sky (238 to 353 ADU on this session). A frame of a third of the usual exposure is stretched
3.4 times, noise included, and adds almost nothing; one of 0.2 s is noise alone.

## Decision

The session's usual exposure is the one most frames carry (to a tenth of a second, the longer
on a tie), and an exposure within 2 % of it counts as the same. A frame exposed under half the
usual one is `Bad` with the reason "too short to be worth stacking"; it gets neither the clouds
nor the background-jump flag, its stars stay out of the session's usual count and its sky out of
its neighbours' jump medians, since all three are the exposure's doing. A frame with any other
exposure keeps its class and gets a note; its background jump stays, and when the jump is within
the jump threshold of what the exposure ratio predicts, the reason says the exposure explains
it. The table gains an `exposure` column after `time`. Half is the owner's call of 2026-11-07
("mergem pe recomandarea ta") on the figures above: the 34.9 s frame leaves, the 137.5 s one
stays.

## Alternatives rejected

- **A quarter instead of half.** It keeps the 34.9 s frame (29 %) as good; stretched 3.4 times,
  it adds 35 s of signal to 2 h 45 min at the others' noise or worse.
- **Every differing exposure bad.** A longer frame is sound after Siril's normalisation, and so
  is one a little shorter; dropping them throws away good signal.
- **Scaling the shorter frame's figures instead of flagging it.** The star count and the sky of
  a 0.2 s frame tell nothing about the night; the frame is not a measurement to correct but a
  shot to leave out.
- **A separate flag kind for a jump the exposure explains.** One kind with a detail keeps the
  jump's words and count in one place; the detail only adds the tail.

## Consequences

`FlagKind.ShortExposure` and `OtherExposure`, `FrameFlag.ExposureDetail`,
`SeriesAnalysis.UsualExposureSeconds`, `Th.ExposureShortFraction` (0.5) and `Th.ExposureSamePct`
(2), `FrameTableRow.Exposure` and the window's column, the fifth paragraph of the REJECTED
explanation, with R-085 and its tests. The classes fixture gains a 0.2 s frame, a 137.5 s frame
with a 14 % brighter sky and a plain frame after them, so the goldens of the classes session
change throughout (the counts, the dither figures and the new rows), beside the `exposure`
column every series golden gains. V-014's classes move the owner's way: 80 good, 2 marginal, 3
bad (16, 58, 64), where the old rules read 81, 4 and 0; V-008's frame 75 (133.2 s) gains a note
and its +11 % jump the exposure's tail, its class unchanged.
