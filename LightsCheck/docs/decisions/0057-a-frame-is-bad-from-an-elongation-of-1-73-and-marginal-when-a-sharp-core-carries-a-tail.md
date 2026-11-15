# 0057. A frame is bad from an elongation of 1.73, and marginal when a sharp core carries a tail

Date: 2026-11-15
Status: accepted 2026-11-15
Supersedes: 0007 in part (the elongation limits 1.35 and 1.6, and the elongation alone deciding a bad frame)

## Context

The classes of R-022 read a frame's median elongation against 1.35 (marginal) and 1.6 (bad),
limits the owner chose on the figures of the prototype's moments. ADR 0056 replaced those
moments: the frame now has the elongation of the whole star and that of its core.

With the whole-star elongation the classes of `Veil/2026-10-06-3h`, the session the limits
were chosen on (87 frames; 48 good, 17 marginal, 22 bad), come out 38 / 19 / 30 at 1.35 and
1.6: the new figure is not diluted by the noise, so a trailed frame reads higher (frame 1 went from
1.68 to 1.78, frame 2 from 1.38 to 1.49). At 1.45 and 1.73 they come out 48 / 17 / 22, the same frames in each
class.

On `Veil/2026-11-06` (82 frames, 80 good and 2 marginal until now) those limits make 11
frames bad and 7 marginal. Their stars carry a faint tail along RA, 11 to 17 px long, always
on the same side: 6 to 17 % of each star's light, where the other 64 frames keep under 2 %
there. The cores are within a fifth of the other frames' width on all but frames 4 and 59, a
third wider (the tail of `DSC_0001` was seen in the
pixels; a frame's average star, each star scaled to its core's flux, gave the shares). The
moments in a circle of 6 px read those frames 1.13 to 1.31: at 4.7 px stars the circle does
not reach the tail. The owner asked whether such frames should leave the stack. Measured on
2026-11-15:

- The owner's stack of the 82 frames (`result_9873s.fit`, Siril, mean with winsorized
  rejection at 3 sigma): the average star keeps, where the tails are, 0.19 % of its peak on
  the faintest stars measured (0.12 of the stack's noise per pixel), 0.05 % on the medium
  ones and 0.01 % on the bright ones. The rejection takes the tail off the bright stars, and
  on the faint ones it is under the noise.
- A simulated mean without rejection: 0.31 % of the peak with all frames, 0.11 % without the
  11.
- Leaving the 11 out costs 13 % of the exposure, 7.5 % more noise over the whole picture.

So those frames do no harm a stack with rejection does not undo, and leaving them out does.
The bad frames of `Veil/2026-10-06-3h` are of two kinds by the same measure: 17 with a
stretched core (1.4 to 1.9 times the session's width, a core elongation of 2.0 to 2.9), which
no stacking repairs, and 5 with a sharp core and a second copy of every star (frames 17, 23,
54, 22 and 60: 30, 24, 22, 15 and 12 % of the light out of place), the kind of the tails. Of
the 21 frames the rule below flags as a tail on the two sessions (5 and 16), 18 come right
after a move of the mount of 5 px or more, two after a step of 4 px and one is the first
frame; the tail lies on the same side whatever the direction of the move. On `Veil/2026-11-06` 75 of the 82 frames follow a
move, so that alone shows little there; on `Veil/2026-10-06-3h` 41 of 87 do, and 4 of the 5.

Put to the owner with three options and the first recommended; the owner chose it, and asked
that the reason say the stack repairs most of them and, where it is probable, the mount.

## Decision

A frame is bad from a median whole-star elongation of `Th.ElongationBad` (1.73) and marginal
from `Th.ElongationMarginal` (1.45), unless the elongation comes from a tail or a second copy
beside a sharp core: the whole star at least `Th.TailCoreRatio` (1.2) times as elongated as
the cores, the cores under `Th.ElongationBad`. Such a frame is marginal whatever it reads,
with the flag `Tail` and a reason that gives both figures, says that stacking with rejection
removes the tail on most stars and, when the step into the frame is a move of the dithering
analysis, that the mount was probably still settling when the exposure began (R-090). TRAILED
FRAMES lists those frames in a row of their own, and the Rejected tab's explanation has a
paragraph on them.

## Alternatives rejected

- **The elongation alone, as ADR 0056 was first proposed to the owner.** The report would
  tell the owner to leave 11 frames of `Veil/2026-11-06` out, against the measurement above.
- **Bad from a large share of light out of place** (20 %: frames 17, 23 and 54 of
  `Veil/2026-10-06-3h` would stay bad). The share needs a measurement of its own per frame,
  and nothing measured says a single doubled frame in 87 harms a stack with rejection.
- **A frame bad only when its cores pass a limit of their own (1.8).** The same classes but
  for frame 4 of `Veil/2026-11-06` (whole star 1.79, cores 1.71, 1.36 times the session's
  width), which would be marginal with a reason reading `elongation 1.79` beside a limit of
  1.73; the owner chose to leave it bad, its core being stretched.
- **The classes from the cores' elongation.** It does not order the frames of
  `Veil/2026-10-06-3h` as the old figure did: a good frame reads up to 1.59 and a marginal one
  from 1.46.

## Consequences

`Veil/2026-10-06-3h`: frames 17, 22, 23, 54 and 60, bad until now, are marginal; the other
classes stay. `Veil/2026-11-06`: 17 frames marginal for a tail or a slight elongation, and
frame 4 bad. The Smida session stays 32 good. Frame 17 of the first session, the one the hand
analysis called the worst, stays in the stack by the report's advice; that a stack with
rejection clears a doubled frame was measured on the stack of `Veil/2026-11-06` only. A stack
without rejection, or of a few frames, keeps the tails: the reason says `on most stars`, not
that the frame is clean. A star doubled closer than about twice its width reads as a stretched
core (ADR 0056) and its frame as trailed. The figures are in `docs/validation.md`, entry of
2026-11-15. Guarded by `R090_ATailIsToldFromAStretchedStarByItsCore`,
`R090_AFrameWithATailBesideASharpCoreIsMarginalAndSaysTheStackRepairsIt`,
`R090_ASessionWithoutATailHasNoTailRow` and `R022_EveryClassAndFlagHasItsRule`.
