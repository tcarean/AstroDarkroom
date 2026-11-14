# 0096. A FITS whose values are not on a 0 to 1 scale is refused, not read as a white frame

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

R-013 clips every FITS sample outside [0, 1] and notes the count, with no limit on it. The code
review of 2026-11-13 (CR-004) showed what that does to a 32-bit float FITS on a 0 to 65535
scale, as other stackers write: every sample becomes 1, the median is 1.0 so the linear refusal
of R-015 does not fire, and the whole report describes a white image, with the count as an
Ok-level note. Measured on 2026-11-14 on the nine float FITS of the reference folder, read raw:
eight have no sample over 1, and the ninth (`Andromeda_WithStars_andWCS.fit`) has 156 of 28
million, 0.0006 %, the largest 1.137. A file on a 0 to 65535 or a 0 to 255 scale has nearly all
of its samples over 1. The owner was asked on 2026-11-14 and chose the refusal.

## Decision

When more than `Thresholds.OffScaleShare` (0.5) of a FITS's samples are over 1, the reader
refuses the file with a `RefusedImageException`: the console prints `ERROR:` with the message and
exits 1, the window shows it on its error card, as for a linear image (ADR 0008). The message
gives the share over 1 and the largest value, says the tool reads a FITS on a scale from 0 to 1,
and tells the user to save it from Siril as 32-bit on that scale, or as 16-bit. Under the
threshold nothing changes: the samples are clipped and counted in the note.

## Alternatives rejected

- **Divide by 65535 and say so in a note.** The analysis would run, but the scale is a guess: a
  file on 0 to 255 or 0 to 4095 would be measured wrongly without a refusal, which is what the
  rule "The image is display-referred and final" forbids.
- **Leave it as R-013 said.** The report of a white frame looks like a result; a user who does
  not read the note in section 6 takes it for one.
- **Refuse on the median, as the linear refusal does.** The same files, but the count is already
  made by the reader, on every sample, and gives the message its figure.

## Consequences

- A float FITS from another stacker must be saved on a 0 to 1 scale, or as 16-bit, before it is
  analysed; the message says so. The window's comparison with another image refuses such a file
  with the same sentence (`ProofRun.PrepareOther` shows the reader's message).
- Half is far from both groups of files measured (0.0006 % and nearly 100 %); a frame with a
  wide black border on a large scale still passes it.
- Guards: `R013_AFitsWithMostValuesOverOneIsRefusedNotReadAsWhite`,
  `R013_UpToHalfOfTheValuesOverOneAreStillClippedAndNoted`,
  `R013_AFitsOffTheScaleExitsOneWithTheReason`.
- One existing fixture was such a file: `R011_BottomUpRowOrderIsFlippedSoThatRowZeroIsTheTop`
  built a 2 x 3 float FITS with the rows 0, 1000 and 2000, four of its six samples over 1. The
  owner was asked on 2026-11-14 and chose to change the middle row alone, to 0.5: the file is
  read, and the test's three assertions stand as they were.
