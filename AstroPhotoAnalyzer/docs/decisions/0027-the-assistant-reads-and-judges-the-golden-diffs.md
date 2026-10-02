# 0027. The assistant reads and judges the golden diffs; a line that moved without a cause is a finding

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

The project's rule "Golden files" (CLAUDE.md, extending the kit's rule "No test edited to
pass") said the console snapshots `Golden/*.txt` are re-approved only after the owner has read
the diff. In the task of ADR 0025 and ADR 0026 the goldens were regenerated twice, five files and
some hundred lines, and the owner was shown summaries, not the diff; the code review of
2026-10-02 flagged the rule as broken. Asked to read the diff, the owner answered instead that
the assistant should read and judge it: "Golden-urile nu le poți citi tu și judeca tu dacă
schimbările sunt cele așteptate? Aș prefera să faci tu asta pe viitor." Reading a snapshot line by
line against the code change that should explain it is work the assistant does completely and
quickly; what the owner needs is the verdict and anything unexplained.

## Decision

After `APA_UPDATE_GOLDEN=1`, the assistant reads the whole diff of `Golden/*.txt` against the
previous commit, ties every changed line to the code change that caused it, and reports in the
task's wrap-up which lines changed and why. A line that moved without a cause in the change is a
finding: the assistant stops and asks, as the kit's rule "No test edited to pass" says for a
wrong test, and does not re-approve it. A golden is still never changed to make something pass.
The owner reads the report, and may read the diff, but the re-approval does not wait for it.

## Alternatives rejected

- **Keep the owner's read.** The owner declined it; in practice it had already lapsed, which is
  worse than a rule that is followed.
- **Drop the golden files.** They pin the report's layout and wording, which nothing else tests
  as a whole; the review of a report change is exactly the diff of a golden.

## Consequences

- `CLAUDE.md`, the rule "Golden files" and the Commands line; `docs/skills.md`, the steps of
  `/new-feature` and `/wrap-up` that mention the goldens.
- The wrap-up report grows one item: the golden lines that changed and their causes, or "none".
