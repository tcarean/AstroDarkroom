# 0025. The whole code is reviewed once and its findings are one list

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

AstroDarkroom's first code was written by an earlier model and had almost no code review; the
reviews the project has known since look only at the change of one task. On 2026-11-13 the
owner asked for the whole project to be reviewed, and for one documented list of problems that
later sessions take one at a time in the order of their severity. The owner asked whether the
project should be split into areas, so the context is not filled, or read in one pass, so the
list is one.

Measured that day, blank lines included: 3,061 lines of code (`src/AstroDarkroom.Core/` 1,485,
`src/AstroDarkroom.Wpf/` 1,576 with its XAML) and 2,764 of tests. That fits one session.
FlatCheck, ImageVisualCompare, LightsCheck and DarksCheck were reviewed the same way
(FlatCheck's ADR 0100, LightsCheck's ADR 0046), and this decision follows theirs so the lists
read alike.

## Decision

1. **One pass, read by area, listed once.** The code is small enough for one session, and it
   is still read by area: four areas (the core, the log with the start and the stop, the
   window, the suite's checks with what the release ships), each read whole by a reviewer with
   a clean context, a subagent on the session's model, which reads `CLAUDE.md`, the area's
   design note, its requirements and its ADRs first and changes nothing. The session then opens
   the lines of every finding itself before the finding enters the list, reads the seams
   between the areas, and writes one document.
2. **The list is `docs/review/2026-11-13-code-review.md`.** One entry per defect, with an ID
   (`CR-NNN`) that never changes, the file and lines, the case, what should happen, the test
   that would show it and the state of the evidence. A summary table at the top orders the
   entries.
3. **Four severities, by the kind of harm.** The scale is FlatCheck's and LightsCheck's but
   for its first level: the launcher computes nothing, so the worst it can do is not a wrong
   figure but a wrong statement the user acts on. 1, the launcher or the released suite
   misleads the user or ships the wrong thing: a wrong program started, a tool or a
   prerequisite said present when it is not or the reverse, a wrong version shown, a licence
   text missing from the folder. 2, a crash, a hang, or a file written or lost where it must
   not be. 3, a wrong text or a wrong display. 4, robustness and cleanliness, a check that
   cannot fail included. Beside it the entry says its reach (an ordinary start, another setup
   than the owner's, a slip, rare), so that within a severity the common ones come first.
4. **Three states of evidence.** `confirmed`: the lines were read and the case follows, or it
   was measured. `to measure`: the case rests on something only a run or a screenshot shows,
   and the task that takes the entry starts by showing it (the kit's rule "Measured numbers").
   `owner`: the code does what is described; whether that is a defect, or which of two texts is
   right, is the owner's call.
5. **The tests and the suite's checks are read for one question.** Can this test fail, and
   does it check what its name and its requirement say; those that do not are entries (CR-014
   to CR-018, CR-020 and CR-021). How strong the tests are line by line stays `/mutate`'s job.
6. **One entry per task.** A task takes one entry, or the group one entry names, in the table's
   order unless the owner picks another. It follows the kit's rules "Requirements first" and
   "Test before fix" and the rule "Two interface languages", and closes the entry in the list:
   `fixed <commit>`, or `rejected: <reason>`. The list is not rewritten otherwise; a new defect
   found later is a new entry at the end.
7. **Scope.** In: `src/`, the tests as point 5 says, and what AstroDarkroom hands the release
   (`kit.json`, the project files, the props files, the notices file). Out: `scripts/` and
   `.claude/`, which are the kit's and which the owner left to a review of the kit, and the six
   components, each with its own review.

## Alternatives rejected

- **One reading in the session's own context, without reviewers.** About 5,800 lines with
  their notes fit, but the session that writes the list would also be the only reader of every
  line; a reviewer that starts from nothing reads an area without the session's assumptions,
  and the session's check of each finding is then a second reading.
- **One session per area, each with its own list.** The lists have to be merged by hand, the
  same defect seen from two areas is listed twice, and nobody reads the seams. Here the
  relative path of the setting file was seen from two areas and is one entry.
- **A second agent that tries to refute each finding.** About twice the agents for a first
  pass. The session's own check of every finding, and the `to measure` state for what a reading
  cannot settle, cover the same risk at this size; an entry that turns out wrong is closed as
  `rejected` by the task that takes it.
- **Fixing while reviewing.** A fix needs a requirement, a failing test and the gate, and a
  text change needs the owner's yes; mixed into a reading of the whole code it would have made
  one task of twenty.
- **The kit's scripts in the same review.** They are the same bytes in every project and a
  defect there is fixed in the kit; the owner chose to leave them out.
- **Entries as issues outside the repository.** The list has to be readable by a session that
  has only the repository, and to change in the same commit as the fix.

## Consequences

The list holds 22 entries (1 of severity 1, 1 of severity 2, 5 of severity 3, 15 of severity 4)
and a cleanup section, from 19 findings and 13 notes on tests reported by the reviewers; none
was rejected at the check; 4 entries are `to measure` and 2 are `owner`. `CLAUDE.md` names
`docs/review/` in its layout. The list ages: an entry's line numbers are those of commit
`07ffe328`, and a task finds its lines again by the names the entry gives. The review built
and ran nothing of the project: three claims were measured in a throw-away program outside the
repository, no entry is proved by a test yet, and the window was never looked at.
