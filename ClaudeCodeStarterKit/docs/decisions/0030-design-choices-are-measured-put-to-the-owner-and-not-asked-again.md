# 0030. Design choices are measured, put to the owner, and not asked again once answered

Date: 2026-10-01
Status: accepted 2026-10-01

## Context

Since 2026-09-25 the first adopter's `CLAUDE.md` carried two sentences the kit's rules lacked.
Its rule on numbers added that a design is chosen after a measurement on its real data, and its
workflow said, as the owner's standing instruction, to discuss and measure before designing:
present the measured facts and the decisions to take, wait for them, then implement without
re-asking. Its notes say that a design chosen there without a measurement was wrong three times.
The kit's rule "Two readings" asks only when two readings of a task lead to different work, and
"Measured numbers" speaks of figures, not of designs, so a project made from the kit inherited
neither. The assistant's memory held the same preference, per machine; by 2026-10-01 it pointed
at a section of that `CLAUDE.md` the practice had already left. On 2026-09-30 the owner decided,
in chat, decision Q5 of the programme: the practice is the kit's, one sentence in each of the two
rules, and `/new-feature` goes on without re-asking.

## Decision

- The kit's rule "Two readings" gains: "A design choice with more than one reasonable answer:
  measure, present the facts and the options, and wait; once answered, implement without asking
  again." Not asking again means that the answered choice is not put to the owner a second time
  and that no leave to start is asked. Still asked: the approval of the plan in plan mode (the
  rule "Plan first"), the merge (the rule "Commits and merges"), and a new fact that contradicts
  the answer, put as a new question with that fact.
- The kit's rule "Measured numbers" gains: "A design is chosen after a measurement on real data."
  A project whose real data are not obvious names them in its `CLAUDE.md`, in a rule that
  extends "Measured numbers".
- `/new-feature` step 3, after the owner's yes, goes on to the plan and the code without
  re-asking.
- A project rule that said this keeps only what the kit's rules do not say, or goes. The
  practice gets no project ADR (ADR 0025).

## Alternatives rejected

- **Keep it in the first adopter's `CLAUDE.md` only.** The owner works this way in every
  project; the kit and the next adopter would not have it, and a copy per project is the drift
  ADR 0028 removed.
- **Keep it in the assistant's memory.** Per machine and not versioned (ADR 0001), and the note
  was already out of date.
- **A rule of its own.** The practice is a case of two rules already there, asking when the
  answer changes the work and measuring instead of guessing; a third name in a file loaded at
  every session would say the same thing again.
- **Let the model choose and state the assumption,** as "Two readings" allows when the readings
  lead to the same work. A choice with more than one reasonable answer is the owner's to make,
  and a guess is what the first adopter's notes record as wrong.
- **Ask once more before implementing.** It costs the owner a turn and reopens what was settled;
  the approval of the plan is the one check before code.

## Consequences

Every project gets the two sentences and the skill's words through `scripts/sync.ps1`. The first
adopter's `CLAUDE.md` loses one of its two rules on this and keeps of the other only which data
are real there. Nothing checks the practice mechanically: whether a choice had more than one
reasonable answer, and whether a question repeats one already answered, are judgments the owner
sees in the session. The guide gives the reasons, for people.
