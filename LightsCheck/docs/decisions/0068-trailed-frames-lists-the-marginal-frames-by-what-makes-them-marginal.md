# 0068. TRAILED FRAMES lists the marginal frames by what makes them marginal

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

A frame is marginal for one of four things (R-022, R-090): an elongation between
`Th.ElongationMarginal` and `Th.ElongationBad`, stars wider than 1.3 times the session's, fewer
stars than 0.6 times the session's, or a tail beside a sharp core. The frames with a tail have
had a row of their own in TRAILED FRAMES since ADR 0057. All the others stood on one row,
`marginal (elongation between 1.45 and 1.73)`: its label states bounds on the elongation and
its list held frames far outside them. On the synthetic classes session it read `3, 4, 5`,
with frame 4 marginal for wide stars and frame 5 for too few stars, both at an elongation of
1.01. R-085 said the row lists every marginal frame on purpose, since the Rejected tab's
explanation sends the user to it for them, so the review of 2026-11-13 left the entry (CR-020,
evidence `owner`) to the owner: the label, or the list.

## Decision

Two rows. `marginal (elongation between 1.45 and 1.73)` lists the frames that are marginal by
their elongation, as its label says. A row under it, there only when such a frame exists,
lists the marginal frames that are neither that nor a tail:

- EN: `marginal for another reason: 4, 5 (wide stars or too few stars)`
- RO: `la limita din alt motiv: 4, 5 (stele late sau prea putine stele)`

The `tail or double` row follows, as before. The second paragraph of the Rejected tab's
explanation points at both rows (R-094 quotes it). The owner chose it on 2026-11-20 among
three options and approved the texts in the plan of the task
(`docs/plans/2026-11-20-fix-cr-017-to-cr-021.md`).

## Alternatives rejected

- **One row with every marginal frame, under a label without the bounds** (`marginal (the
  frame table gives each one's reason)`). The label of the `bad` row beside it keeps its
  bound, and the user loses, on the Summary tab, the one thing the row can say at a glance:
  which frames are nearly trailed.
- **The first row alone, the other frames listed nowhere in the section.** The explanation of
  the Rejected tab promises the list of the marginal frames here, and the summary's count of
  marginal frames would not add up with the rows under it.
- **Leaving the row as it was.** A label that states a bound over a list that breaks it.

## Consequences

The section is still named TRAILED FRAMES and now holds a row of frames that are not trailed,
as it did before without saying so. The parenthesis names the two reasons a frame can have
today (`Soft`, `Clouds`); a new reason for a marginal class would have to be added to it.
