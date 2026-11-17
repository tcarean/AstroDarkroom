# 0065. The trail direction row says that only frames long enough to stack count

Date: 2026-11-17
Status: accepted 2026-11-17

## Context

Since ADR 0061 a frame too short to stack casts no vote for the direction of the trails, while
it keeps its place in the `bad` row of TRAILED FRAMES when it is also trailed. The row under
it, `trail direction`, read `not known (too few bad frames with trails in the same direction)`
when fewer than `Th.TrailAxisMinFrames` (2) frames voted. With one trailed frame and one
trailed test shot the two rows then read against each other: `bad` lists two frames, trailed
the same way, and the next row says there are too few. The plan of the task that made ADR 0061
left the rewording to the owner (`docs/plans/2026-11-16-fix-cr-011-to-cr-016.md`, "Left for the
owner, a text"). The owner saw the original and the proposal side by side and said yes on
2026-11-17.

## Decision

The row names the frames that count:

- EN: `not known (too few bad frames, long enough to stack, with trails in the same direction)`
- RO: `nu se stie (prea putine cadre slabe, cu expunere destul de lunga, cu dare in aceeasi directie)`

The sentence is the same whether or not the session has a short frame.

## Alternatives rejected

- **The longer sentence only when a short trailed frame is listed above.** Two wordings of one
  row for a case few sessions meet; the one sentence is true in both.
- **The short trailed frame taken out of the `bad` row.** The row lists the trailed frames
  (R-085), and that frame is one: the owner reads there why its stars are long.

## Consequences

The row is longer by a few words. When the direction is known the row still reads `on the bad
frames`, and the angle is then that of the bad frames long enough to stack: a short trailed
frame among them is not said there. Guarded by
`R085_TheTrailDirectionRowSaysThatOnlyFramesLongEnoughToStackCount`; the goldens
`series-classes.txt` and `en_series-classes.txt` carry the sentence.
