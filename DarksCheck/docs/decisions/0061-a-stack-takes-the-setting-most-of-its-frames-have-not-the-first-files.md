# 0061. A stack takes the setting most of its frames have, not the first file's

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

Check 1 names a group's dominant setting: the largest cluster of its readable frames by ISO and
by exposure within 2 % (R-007, `MatchAnalyzer.Settings`). The stack that the analysis reads did
not: `FrameStack.Load` took the settings of the first file that decoded and left out every
frame that differed from it (R-020, "the first file sets"). The code review of 2026-11-13
(CR-012, `owner`) gave the input on which the two part: `darks\` holds one test frame of 1 s,
then twenty frames of 120 s. The header read `darks exposure: 119.2 s at ISO 400 (20 of 21
frames)` and `match: OK`, while the analysis kept one frame of 1 s, listed the twenty as
`settings differ from the first frame`, and drew every check, the index and the label from that
one frame. Each requirement did what it said; together they contradicted. The same held for
the biases, whose ISO is compared with the darks' on the dominant one (ADR 0059) while their
stack took the first file's. The owner chose on 2026-11-16.

## Decision

A stack keeps the frames that have the group's dominant setting, the one check 1 names. `Load`
reads the metadata of every file before a pixel is decoded, the files the frame cap skips
included, finds the dominant setting with `MatchAnalyzer.Settings` (the first cluster met on a
tie, as in check 1), and compares each decoded frame with the first readable frame of that
setting: the same ISO when both give it, an exposure within 2 % when both give it. A frame at
another setting is left out wherever it stands, with `other settings than most frames: ISO 400,
1 s` (`alte setari decat majoritatea cadrelor`), and sets neither the size nor the scale, which
come from the first frame that decodes with the group's setting. One `Load` serves the darks,
the biases and a partner's darks, so the three follow the rule. The normalisation factor stays
that of the frame that set the geometry, so a series whose frames all have one setting reads
every figure as before.

## Alternatives rejected

- **The first file decides, and check 1 says so.** A WARNING that only the frames like the
  first are analysed keeps the two in step, and leaves the user with twenty good darks unread
  until the stray file is moved away.
- **The dominant setting among the files the cap chose.** Check 1 reads every file; over the
  cap the two could part again.
- **The metadata handed in by the run, which the inventory has read.** It saves one more read
  of each header (0.1 to 0.6 s for 32 to 87 raws, measured for LightsCheck's ADR 0054), and
  leaves the partner's stack, which has no inventory, on another path.

## Consequences

A folder whose first file is a stray frame is analysed on its series, and the stray frame is
the error that makes the run exit 1. A group split evenly between two settings is read on the
one met first, as check 1 names it. When no file's metadata can be read the first decoded
frame's settings decide, as before. No golden file changed. Guarded by
`R020_AStrayFirstFrameDoesNotSetTheGroupsSettings`,
`R020_AStrayFirstFramePrintsItsReasonAndTheSeriesIsAnalysed_Exit1` and
`R027_AFrameWithOtherSettingsIsLeftOutWithItsError`, whose two texts follow the new wording.
