# 0029. A sky under L\* 15 is an Attention, whatever the paper's black; the recommended row has the sky at L\* 15 or above

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

Since ADR 0025 the background verdict asked one question: is the dust at or above paper black?
At baryta's old Dmax of 2.0 paper black was L\* 9.0, next to the reference image's sky (L\* 9.4),
so that one rule also kept the sky visible, by accident. ADR 0028 moved baryta to 2.4 (paper
black L\* 3.6), and on `de_printat.png` the tool then said "print-safe as it is; you can go down
to 0.10", with 82.8 % of the pixels under L\* 15, the level `Thresholds.LowSeparationLstar` has
called "the paper separates, the eye in room light barely" since the first version, and which
no rule read. The owner asked, with the window open, whether that threshold still holds at a
higher Dmax and whether it should count.

It holds at any Dmax, because it is about viewing, not about the paper: dim light and the
reflections of the paper and of the glass put a veil over the blacks. And it is the same rule as
the help text's "a print wants the sky at 0.15 to 0.25" (`PrintBackgroundLo`): a grey of L\* 15
is the level 0.148. The number 15 has no measurement behind it; it is a rule of thumb of the
conversation that started the tool.

Measured on `de_printat.png`, baryta at Dmax 2.4, full frame, no WCS, on 2026-10-05:

| row | dust | sky | pixels under L\* 15 |
|---|---|---|---|
| as it is (0.103) | L\* 4.1 | L\* 9.4 | 82.8 % |
| 0.15 | L\* 7.8 | L\* 15.3 | 48.0 % |

## Decision

1. The as-is sky under L\* `LowSeparationLstar` (15) is an Attention of the background topic,
   also when the dust is above paper black: `Cerul e sub L* 15: hartia il separa de negru, dar
   in lumina de camera s-ar vedea aproape negru`. A sky under paper black stays a Problem and
   dust under paper black an Attention, as before; they are asked first.
2. The recommended row of the simulation is the lowest row that is print-safe **and** has the
   sky at L\* 15 or above. "You can go down to" therefore stops at the visibility threshold.
3. What is compared with the threshold is the sky (R-044's sky: the median of the sky patches),
   not the dust and not a share of the frame: the sky is the tone that fills the print, and the
   share of pixels under the threshold goes into the `Because` as evidence.
4. Anexa B gives the target at which the sky reaches the threshold, read on the same ladder as
   the exact target. The `print-safe` mark of a row keeps its meaning: the dust on paper black.

## Alternatives rejected

- **Leave the threshold as a figure nobody reads.** On the default paper the tool would tell
  the owner's image it can be printed darker than it is, before a real print.
- **Judge the share of pixels under L\* 15.** A share needs a second threshold with no source
  either, and a frame half filled by a bright nebula would pass with a black sky.
- **Judge the dust against L\* 15.** The dust is by definition the darkest extended structure;
  asking it to reach 15 would wash the sky out (the reference image's dust reaches 7.8 when the
  sky is at 15.3).
- **Derive the threshold from the viewing light.** Nothing measures the room; a field for it is
  a possible later step, with the BPC task.

## Consequences

- On the default paper the reference image is told to re-stretch to 0.15, not to 0.17 (the old
  paper's answer) and not "as it is". The simulation is still a gamma curve that a real stretch
  undercuts (`docs/design/known-limitations.md`).
- The threshold is an unsourced rule of thumb and says so in `Thresholds.cs`; the only true check
  is a test strip looked at in the light of the room where the print will hang.
- The grid's floor (0.10) and the help text's print range (0.15 to 0.25) stop contradicting the
  verdict: the recommendation no longer goes under 0.148 on any paper.
- Guards: `R092_ASkyUnderTheVisibilityThresholdIsAnAttentionEvenAbovePaperBlack`,
  `R043_TheRecommendedRowAlsoHasTheSkyAtTheVisibilityThreshold`, `R043_ACustomGridIsHonoured`
  (no row to recommend when the grid stops under the threshold); the goldens; V-003 and V-005
  in `docs/validation.json`.
