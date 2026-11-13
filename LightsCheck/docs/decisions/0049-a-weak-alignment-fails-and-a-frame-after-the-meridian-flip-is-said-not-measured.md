# 0049. A weak alignment fails, and a frame after the meridian flip is said, not measured

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The registration of R-018 accepted any fit with 10 matched stars of 600, and votes for one
shift, which a frame turned by 180 degrees does not have. The review of 2026-11-13 (CR-004,
`to measure`) named two cases, and both were shown in the C# code that day, on the fields of
`RegistrationTests` (600 stars on 6016 x 4016, 8 fields per angle, a shift of (50, -30) px):

| Rotation | Right | Failed | `Ok` and wrong |
| --- | ---: | ---: | ---: |
| 0.2, 0.3, 0.5 degrees | 8 | 0 | 0 |
| 0.7 degrees | 7 | 0 | 1 (10 stars matched of 576, rotation 0.02, shift (66, -53), RMS 1.8 px) |
| 1.0 degree | 1 | 6 | 1 (11 of 580, rotation 0.03, shift (76, -60), RMS 1.8 px) |
| 1.5, 2, 5, 180 degrees | 0 | 8 | 0 |

A right alignment matched 524 to 543 stars of 564 to 583 (92 % or more). On the recorded
sessions the fewest matched stars of a frame were 370 of 600 on `Milkyway-Core/2026-08-12-Smida`
(62 %), 503 of 600 on `Veil/2026-10-06-3h` (84 %) and 543 of 600 on `Veil/2026-11-06` (90 %).
A frame at 180.3 degrees, turned back by 180 degrees about the frame's centre before the
registration, aligned with 536 to 541 stars. After a flip every frame was `could not be
aligned`, and the dithering, the drift, the pattern and the walking-noise verdict described the
first part of the night with no word about it. The owner chose on 2026-11-13.

## Decision

1. A registration fails when its last round matched under `Th.MinMatchFraction`, a fifth, of the
   shorter of the two star lists: ten times the share of the wrong fits measured, a third of
   the lowest share of a real frame. The frame reads `could not be aligned`, as before.
2. A frame that fails as it is and registers with its stars turned by 180 degrees is a frame
   taken after a meridian flip. It is said, not measured: it has no position, its flag is
   `AfterFlip` with a reason of its own, its class does not change, and DITHERING opens with a
   `meridian flip` row that names the frames and says the dithering, the drift, the field
   rotation and the fixed pattern are measured on the other frames (R-087). The texts were
   approved with the plan `docs/plans/2026-11-13-fix-cr-001-to-cr-005.md`.

## Alternatives rejected

- **Aligning the flipped frames and measuring the whole night as one.** After a flip the
  sensor's pattern lands turned on the sky, a move to the east shifts the stars the other way in
  the image, and the re-centring is one large step: the drift, the rotation, the moves'
  directions, the polar reading and the pattern would each need their own rule for the two
  halves. A feature of its own, not a fix.
- **Only the weak fit now, the flip as a new entry of the list.** The silence about half the
  night is the part of CR-004 a user meets; saying it costs one more registration of the frames
  that failed.
- **A limit on the fit's residual instead of the share.** The wrong fits read 1.8 px against
  0.8 px at most on the real frames: a factor of 2, where the share separates by 30.
- **A vote that follows a rotation.** It would align the frames at 1 degree and more that fail
  today; no recorded session turns by more than 8.5 arcminutes, and a failed frame says so.

## Consequences

A frame that shares under a fifth of its stars with the reference (a field moved by most of
the frame, thick cloud over most of it) is no longer aligned; before, it was aligned when ten
stars matched. A session whose reference frame is the only one before the flip reports its
dithering on one frame, and says so. `Registration.Register` runs `Align` twice for a frame
that fails. Guarded by `R018_ARotationTheVoteCannotFollowIsAlignedRightOrNotAtAll`,
`R018_UnderAFifthOfTheShorterListMatchedIsAFailedRegistration`, the three `R087_` tests of
`RegistrationTests`, `SeriesAnalyzerTests` and `SeriesTests`; the last draws its frames upside
down on a field of scattered stars, since the fixture's star grid turned by 180 degrees falls
nearly on itself and aligns as it is.
