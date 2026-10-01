# 0030. Design choices are measured, put to the owner with a recommendation, and not asked again

Date: 2026-10-01
Status: accepted 2026-10-01

## Context

Since 2026-09-25 the first adopter's `CLAUDE.md` carried two sentences the kit's rules lacked.
Its rule on numbers added that a design is chosen after a measurement on its real data, and its
workflow said, as the owner's standing instruction, to discuss and measure before designing:
present the measured facts and the decisions to take, wait for them, then implement without
re-asking. Its notes say that a design chosen there without a measurement was wrong three times.
In the kit only `/new-feature` did part of this: its step 3 asked on a design choice with more
than one reasonable answer, waited for the owner's yes and said to measure before designing.
Nothing said so for a design outside a new feature, such as a fix, and nothing said not to ask
again. On 2026-09-30 the owner decided, in chat, decision Q5 of the programme: the practice is
the kit's, one sentence in each of the rules "Two readings" and "Measured numbers", and
`/new-feature` goes on without re-asking; the assistant's memory keeps a pointer, applied by
hand in the projects without the kit. On 2026-10-01, reviewing this decision's branch, the owner
added three things. First, a new fact against an answer goes back to the owner and options
always come with a recommendation, as a general rule of the kit for every question. Second, the
approval of a plan accepts a choice answered in an earlier session. Third, the first adopter
keeps its wider rule.

## Decision

- The kit's rule "Two readings" gains: "A design choice with more than one reasonable answer:
  measure, present the facts and the options, and wait; once answered, implement without asking
  again." Not asking again means that the answered choice is not put to the owner a second time
  and that no permission to start is asked; the approval of the plan in plan mode (the rule
  "Plan first") and the merge (the rule "Commits and merges") are other questions and stay.
- A new kit rule, "Questions to the owner", holds for every question put to the owner, not only
  a design choice: "A new fact that contradicts an answer the owner gave goes back to the owner
  as a new question. Options put to the owner always come with a recommendation."
- The kit's rule "Measured numbers" gains: "A design is chosen after a measurement on real data."
  A project whose real data are not obvious names them in its `CLAUDE.md`, in a rule that
  extends "Measured numbers".
- `/new-feature` step 3 writes the alternatives and a recommendation into the `proposed` ADR and,
  after the owner's yes, goes on to the plan and the code without re-asking. A choice the owner
  answered in an earlier session is not asked again: the plan names its ADR, and the owner's
  approval of the plan is the yes.
- A project rule that said this keeps only what the kit's rules do not say, or is deleted. The
  practice gets no project ADR (ADR 0025).

## Alternatives rejected

- **Keep it in the first adopter's `CLAUDE.md` only.** The owner works this way in every
  project; outside `/new-feature` the kit and the next adopter would not have it, and a copy per
  project is the drift ADR 0028 removed.
- **Keep it in the assistant's memory.** Per machine and not versioned (ADR 0001), and the note
  was already out of date. Memory keeps only the pointer, for the projects without the kit.
- **A rule of its own for designs.** Measuring and asking on a design are cases of two rules
  already there, "Measured numbers" and "Two readings"; a third name for designs would say the
  same thing again. What holds for every question has its own rule, "Questions to the owner".
- **The new fact and the recommendation inside "Two readings".** There they read as part of a
  design choice; the owner wants them for every question.
- **A sharper test than "more than one reasonable answer",** such as "a choice that would get an
  ADR". Circular: under the rule "Decisions" every choice settled in chat gets an ADR. The test
  is the one `/new-feature` already used.
- **Let the model choose and state the assumption,** as "Two readings" allows when the readings
  lead to the same work. A choice with more than one reasonable answer is the owner's to make,
  and a guess is what the first adopter's notes record as wrong.
- **Options without a recommendation.** The owner asked for one with every set of options.
- **The exception for a new fact in this ADR only.** Sessions do not load ADRs; a model whose
  measurement goes against the owner's answer would have only "without asking again" to go on.
- **Ask once more before implementing,** also when the answer came in an earlier session. It
  costs the owner a turn and reopens what was settled; the approval of the plan is the one check
  before code.

## Consequences

Every project gets the sentences, the new rule and the skill's words through `scripts/sync.ps1`.
The first adopter's `CLAUDE.md` keeps what is its own: its rule on designs still covers every
design, not only a choice with more than one reasonable answer, and its rule on numbers names
its real data. The assistant's memory points at these rules, to be applied by hand in a project
without `.claude/rules/kit/`. Nothing checks the practice mechanically: whether a choice had more
than one reasonable answer, and whether a question repeats one already answered, are judgments
the owner sees in the session. The guide gives the reasons, for people.
