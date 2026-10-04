# 0033. The assistant reads and judges the golden diffs; a line without a cause is a finding

Date: 2026-10-04
Status: accepted 2026-10-04

## Context

A golden file is a whole output, such as a console report, kept in the repository and compared
byte for byte by a test; a project regenerates its golden files, through an environment variable,
when that output changes on purpose. Two of the kit's adopters have them; the kit's sample has
none. The kit said three things about them, none in its rules file: the guide, that the model does
not regenerate them without the owner's explicit OK; `/new-feature` step 6, that one is
re-approved only after the owner has read the diff and said yes; `/wrap-up` step 2, that one that
changed is a finding. The rule "No test edited to pass" names a test, a fixture, a hash and a
figure, and read as written it forbids the regeneration that every intended change of an output
needs. In one adopter, on 2026-10-02, the golden files were regenerated twice in two tasks, five
files and some hundred lines, and the owner was shown summaries, not the diff; the code review
flagged the rule as broken. Asked to read the diff, the owner answered instead that the assistant
should read and judge it (translated from Romanian): "Can't you read the golden files yourself and
judge whether the changes are the expected ones? I would rather you did that from now on." That
adopter recorded the rule in an ADR of its own the same day, while the shared text loaded beside
it kept saying the opposite. On 2026-10-04 the owner asked to move the rule into the kit. Asked
the same day about another adopter's golden files whose numbers an independent implementation
confirmed before it was frozen, the owner chose to keep those figures.

## Decision

- A golden file changes only with an intended change of the output it holds, never to make a test
  pass: it is a test under the rule "No test edited to pass".
- After a regeneration the assistant reads the whole diff of the golden files, ties every changed
  line to the change that caused it, and reports in the task's wrap-up which lines changed and
  why, or that none did. A line with its cause is re-approved without waiting for the owner, who
  reads the report and may read the diff.
- A line that moved without a cause in the change is a finding: the assistant says so and stops,
  as the rule says of a wrong test, and does not re-approve it.
- A golden file whose numbers were confirmed outside the code it tests, by an independent
  implementation or on real data, is a figure as well: every changed line there is a finding,
  with a cause or without, and the owner decides, as for a figure that moved. The project's
  `CLAUDE.md` names such golden files.
- The rule is a sentence of "No test edited to pass" in the rules file, which every session loads,
  because a golden file is regenerated wherever an output changes, in a fix as much as in a
  feature; `/new-feature` step 6 and `/wrap-up` step 2 carry the steps. A project names its golden
  files and the command that regenerates them in its `CLAUDE.md` and writes no ADR for the rule
  (ADR 0025); a project ADR that recorded it is superseded by this one.

## Alternatives rejected

- **The owner reads every golden diff (the old text of the skill).** The owner declined it; in
  practice it had already lapsed, which is worse than a rule that is followed.
- **The rule in each project's `CLAUDE.md` only.** The shared skills and the guide said the
  opposite in the same session, and the next adopter with golden files would start from the old
  text.
- **The rule in the skills only, the rules file silent.** The skills load only when invoked, and
  the rules file, read as written, forbids the regeneration itself.
- **Every golden file judged alike, confirmed numbers included.** A cause in the change explains
  why a confirmed number moved but cannot confirm the new one; the owner chose to keep such golden
  files figures.
- **Drop the golden files.** They pin an output's layout and wording, which nothing else tests as
  a whole; the review of a change to an output is exactly the diff of a golden file.

## Consequences

The rules file, `/new-feature`, `/wrap-up` and the guide change, and every project gets them
through `scripts/sync.ps1`. The wrap-up report grows one item where a golden file changed: its
changed lines and their causes. A project keeps in its `CLAUDE.md` where its golden files are, how
they are regenerated and which of them carry confirmed figures. Nothing checks the rule
mechanically: whether a line has its cause in the change is a judgment the owner reads in the
report.
