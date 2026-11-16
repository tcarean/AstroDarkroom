# 0061. A frame too short to stack casts no vote for the RA axis

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

Without a plate solve the RA axis of the image is read from the trailed frames: the mean
direction of the trails of at least `Th.TrailAxisMinFrames` (2) frames whose stars agree on a
direction, on the assumption that the tracking made the trails (R-025). Since ADR 0040 a frame
exposed under half the usual time is bad whatever it shows, and R-085 says of it that "it casts
no vote for the RA axis". When the rows of TRAILED FRAMES were later made to go by the trail
and not by the class, a short frame that is also trailed kept its `Trailed` flag, and the vote,
which reads that flag, counted it again. The review of 2026-11-13 (CR-012, `owner`) found it on
the fixture `SeriesAnalyzerTests.ShortAndTrailed()`: one 120 s frame and one 0.2 s frame, both
at elongation 1.8 and 88 degrees, give two votes, so the axis reads Y "from the direction of
the trails", one of the two votes being the 0.2 s frame's. R-085's sentence could be read
either way for a short frame that is also trailed. The owner chose on 2026-11-16.

## Decision

A frame flagged `ShortExposure` casts no vote, trailed or not: it is left out of the frames
whose angles give `TrailAngleDeg` and, through it, the RA axis. It keeps its `Trailed` flag and
its place in the `bad` row of TRAILED FRAMES, and its own trail is still named against the axis
the other frames decide.

## Alternatives rejected

- **The short trailed frame keeps its vote**, R-085 rewritten to say so. The vote rests on the
  tracking having drawn the trail, and in a fraction of a second the tracking cannot draw one:
  at 15 arcseconds a second a mount that stood still for 0.2 s moves the stars by 3
  arcseconds, half a pixel at 135 mm on a D3300. What elongates the stars of such a frame is
  something else (a knock, the mirror, few faint stars), and it says nothing of where RA is.

## Consequences

A session with one trailed frame and a trailed test shot no longer reads an axis from the two;
it reads `unknown`, as with one trailed frame alone, and the figures that need the axis (the
drift on RA and DEC, the lower bound of the polar error) are then not given. On a session with
a plate solve nothing changes but the mean trail angle, when a short trailed frame was among
its voters. Guarded by `R085_ATrailedShortFrameCastsNoVoteForTheRaAxis`.
