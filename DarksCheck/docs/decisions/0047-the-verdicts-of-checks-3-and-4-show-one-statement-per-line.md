# 0047. The verdicts of checks 3 and 4 show one statement per line

Date: 2026-11-10
Status: accepted 2026-11-10

## Context

The owner, 2026-11-10: the verdict of light leaks and differences across the frame (check 3)
is hard to read as one block, and the warming's (check 4), when it says more than one thing,
should say each on a line of its own. Both verdicts are sentences joined by `; `: the OK of
check 3 names the largest departure, then whether a frame has more light, then the sides; its
warning joins what it found with `; in plus, `; check 4's `REFA` gives the count, the advice
and the frames Siril's `-opt` cannot match. ADR 0041 gave rows `ReportRow.Lines` for the
window and rejected splitting the text on `; ` in the window, which would parse prose there.

## Decision

The core cuts the two verdicts into statements: `ReportRow.WithStatements` sets `Lines` from
`ReportRow.StatementsOf(Rest)`, which cuts at every `; ` that stands outside round and square
brackets (`(in afara 0.7 .. 1.4; cel mai departe 1.93)` stays whole), and only when there are two
or more. The value is untouched, so the console prints what it printed; the words are the
console's, `in plus,` included at the start of its line. The window's row template already
draws the lines one under the other, the verdict's chip beside the first. The plan's example
of the `REFA` verdict was shown to the owner before the change and approved with the plan.

## Alternatives considered

- **Each verdict built as a list of its statements.** The same lines, but `Leak`, `Drift` and
  the cause words would all be restructured, and a statement that grows a `; ` later (the
  cause words have one) would have to be split by hand again. Rejected for the one rule.
- **Dropping `in plus,` / `also,` on the window's lines.** A rewording of the user's texts, not
  asked for (the rule "Plain words": a rewrite is shown first). Kept as it is.
- **The rule for every row.** The other rows' `; ` lists (the counts per level, the banding of
  both directions) read better on one line; only the two verdicts were asked for.
- **`adica ...` / `that is, ...` joined to the line above.** The warning of check 3 ends with
  what its light means against the dark's noise (`adica +0.79 ADU e 0.55 din zgomotul pe pixel al
  masterului dark`), cut onto a line of its own. Joining it back needs a case of its own for
  those words. The owner, 2026-11-10, shown the real leak set's warning: keep it on its own line.

## Consequences

The goldens do not move for this. The verdicts of checks 3 and 4 carry their statements in
`Lines`; a one-statement verdict (`NEMASURAT` alone) keeps none and stays one line.
