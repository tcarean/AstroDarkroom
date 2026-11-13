# 0100. The whole code is reviewed once and its findings are one list

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

FlatCheck's first code was written by an earlier model and had almost no code review; the
reviews the project has known since look only at the change of one task. On 2026-11-12 the owner
asked for the whole project to be reviewed, and for one documented list of problems that later
sessions take one at a time in the order of their severity. The owner asked whether the project
should be split into areas, so the context is not filled, or read in one pass, so the list is
one.

Measured that day, without blank lines: 28,784 lines of code (`FlatCheck.Core/` 14,414,
`FlatCheck/` 748, `FlatCheck.Wpf/` 13,622) and 23,596 of tests. That does not fit one context
read with care.

## Decision

1. **Read by area, listed once.** The code folders `kit.json` names are split into ten areas of
   2,000 to 5,000 lines. Each is read whole by a reviewer with a clean context, a subagent on
   the session's model, which reads `CLAUDE.md`, `docs/design/known-issues.md` and the area's
   design notes first and changes nothing. The session then opens the lines of every finding
   itself before the finding enters the list, reads the seams between the areas, and writes one
   document.
2. **The list is `docs/review/2026-11-13-code-review.md`.** One entry per defect, with an ID
   (`CR-NNN`) that never changes, the file and lines, the case, what should happen, the
   direction of a fix and the state of the evidence. A summary table at the top orders the
   entries.
3. **Four severities, by the kind of harm.** 1, a wrong figure or a wrong verdict: the worst a
   diagnostic can do. 2, a crash, a hang, or a file overwritten or lost. 3, a wrong text or a
   wrong display. 4, robustness and cleanliness. Beside it the entry says its reach (an ordinary
   run, another setup than the tested ones, a mistyped argument, rare, a crafted file), so that
   within a severity the common ones come first.
4. **Three states of evidence.** `confirmed`: the lines were read and the case follows.
   `to measure`: the case rests on something only a run or an outside source shows, and the
   task that takes the entry starts by showing it (the kit's rule "Measured numbers"). `owner`:
   the code does what is described and whether that is a defect is the owner's call.
5. **One entry per task.** A task takes one entry, or the group one entry names, in the table's
   order unless the owner picks another. It follows the kit's rules "Requirements first" and
   "Test before fix" and FlatCheck's "Formula changes re-validate", and closes the entry in the
   list: `fixed <commit>`, or `rejected: <reason>`. The list is not rewritten otherwise; a new
   defect found later is a new entry at the end.
6. **Scope.** In: the three code folders. Out: `scripts/` and `.claude/` (the kit's),
   `flatcheck.py` and `crosscheck.py` (frozen, ADR 0047), `lensfun/` (data), and the tests line
   by line, whose strength `/mutate` measures.

## Alternatives rejected

- **One pass in one context.** The list would be one without any merging, but about 29,000
  lines read in one context are read well at the start and thinly at the end.
- **One session per area, each with its own list.** No context is ever full, but the lists
  have to be merged by hand, the same defect seen from two areas is listed twice, and nobody
  reads the seams.
- **A second agent that tries to refute each finding.** About twice the agents for a first
  pass. The session's own check of every finding, and the `to measure` state for what a reading
  cannot settle, cover the same risk at this size; an entry that turns out wrong is closed as
  `rejected` by the task that takes it.
- **Fixing while reviewing.** A fix needs a requirement, a failing test and the gate; mixed
  into a reading of the whole code it would have made one task of sixty.
- **Entries as issues outside the repository.** The list has to be readable by a session that
  has only the repository, and to change in the same commit as the fix.

## Consequences

The list holds 60 entries (10 of severity 1, 5 of severity 2, 31 of severity 3, 14 of severity
4) and a cleanup section, from 70 findings reported by the reviewers; none was rejected at the
check; 13 entries are `to measure` and 4 are `owner`. `CLAUDE.md` names `docs/review/` in its layout. The
list ages: an entry's line numbers are those of 2026-11-13, and a task finds its lines again by
the names the entry gives. The review read no test line by line and ran nothing, so it says
nothing about what the tests miss; `/mutate` does.
