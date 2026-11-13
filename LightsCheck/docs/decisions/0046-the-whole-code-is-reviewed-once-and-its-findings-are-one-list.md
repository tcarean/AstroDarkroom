# 0046. The whole code is reviewed once and its findings are one list

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

LightsCheck's first code was written by an earlier model and had almost no code review; the
reviews the project has known since look only at the change of one task. On 2026-11-13 the
owner asked for the whole project to be reviewed, and for one documented list of problems that
later sessions take one at a time in the order of their severity. The owner asked whether the
project should be split into areas, so the context is not filled, or read in one pass, so the
list is one.

Measured that day, blank lines included: 16,507 lines of code (`src/LightsCheck.Core/` 11,492,
`src/LightsCheck.Cli/` 567, `src/LightsCheck.Wpf/` 4,448) and 10,912 of tests. That does not
fit one context read with care. FlatCheck and ImageVisualCompare were reviewed the same way the
same day (FlatCheck's ADR 0100), and this decision follows theirs so the lists read alike.

## Decision

1. **Read by area, listed once.** `src/`, the folder `kit.json` names as code, is split into
   seven areas of 2,000 to 2,800 lines. Each is read whole by a reviewer with a clean context, a
   subagent on the session's model, which reads `CLAUDE.md`, the area's design notes and its
   requirements first and changes nothing. The session then opens the lines of every finding
   itself before the finding enters the list, reads the seams between the areas, and writes one
   document.
2. **The list is `docs/review/2026-11-13-code-review.md`.** One entry per defect, with an ID
   (`CR-NNN`) that never changes, the file and lines, the case, what should happen, the test
   that would show it and the state of the evidence. A summary table at the top orders the
   entries.
3. **Four severities, by the kind of harm.** 1, a wrong figure or a wrong verdict: the worst a
   tool that says which frames to leave out can do. 2, a crash, a hang, or a file written where
   it must not be. 3, a wrong text or a wrong display. 4, robustness and cleanliness. Beside it
   the entry says its reach (an ordinary run, another setup than the tested one, a slip, rare,
   a crafted file), so that within a severity the common ones come first. The scale is
   FlatCheck's; the fifth level named in chat, cleanliness alone, is the list's cleanup section.
4. **Three states of evidence.** `confirmed`: the lines were read and the case follows.
   `to measure`: the case rests on something only a run, a screenshot or an outside source
   shows, and the task that takes the entry starts by showing it (the kit's rule "Measured
   numbers"). `owner`: the code does what is described and follows a requirement or a note;
   whether that is a defect is the owner's call.
5. **The tests are read for one question.** Each reviewer reads its area's tests to say which
   cannot fail and which do not check what their name says; those are entries (CR-058 to
   CR-060). How strong the tests are line by line stays `/mutate`'s job. This is where the list
   differs from FlatCheck's, which left the tests out.
6. **The copied files have their own section.** A defect in a file of
   `src/LightsCheck.Core/Imaging/` marked "copied from FlatCheck unchanged" is listed as
   `CP-N`, with the entry of FlatCheck's list that names it, and is fixed in FlatCheck first
   (the rule "Copied files stay copies").
7. **One entry per task.** A task takes one entry, or the group one entry names, in the table's
   order unless the owner picks another. It follows the kit's rules "Requirements first" and
   "Test before fix" and the rules "Measured on the sessions", "Plain words" and "Golden
   files", and closes the entry in the list: `fixed <commit>`, or `rejected: <reason>`. The
   list is not rewritten otherwise; a new defect found later is a new entry at the end.
8. **Scope.** In: `src/`, and the tests as point 5 says. Out: `scripts/` and `.claude/` (the
   kit's) and `prototype/` (frozen, ADR 0003).

## Alternatives rejected

- **One pass in one context.** The list would be one without any merging, but about 16,500
  lines of code and their notes read in one context are read well at the start and thinly at
  the end.
- **One session per area, each with its own list.** No context is ever full, but the lists
  have to be merged by hand, the same defect seen from two areas is listed twice, and nobody
  reads the seams. Here the output folder that can be `lights/` was seen from four areas and is
  one entry.
- **A second agent that tries to refute each finding.** About twice the agents for a first
  pass. The session's own check of every finding, and the `to measure` state for what a reading
  cannot settle, cover the same risk at this size; an entry that turns out wrong is closed as
  `rejected` by the task that takes it.
- **Fixing while reviewing.** A fix needs a requirement, a failing test and the gate, and a
  text change needs the owner's yes; mixed into a reading of the whole code it would have made
  one task of sixty.
- **Entries as issues outside the repository.** The list has to be readable by a session that
  has only the repository, and to change in the same commit as the fix.

## Consequences

The list holds 61 entries (13 of severity 1, 2 of severity 2, 29 of severity 3, 17 of severity
4), six entries for the copied files and a cleanup section, from 84 findings reported by the
reviewers; none was rejected at the check; 16 entries are `to measure` and 7 are `owner`.
`CLAUDE.md` names `docs/review/` in its layout. The list ages: an entry's line numbers are
those of commit `d2345bf7`, and a task finds its lines again by the names the entry gives. The
review ran nothing in the project: no entry is proved by a test yet, the window was never
looked at, and the figures quoted from the reviewers' replicas are theirs.
