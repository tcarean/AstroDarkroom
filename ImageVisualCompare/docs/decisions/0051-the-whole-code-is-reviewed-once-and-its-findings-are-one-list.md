# 0051. The whole code is reviewed once and its findings are one list

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

ImageVisualCompare's first code was written by an earlier model and had almost no code review;
the reviews the project has known since look only at the change of one task. On 2026-11-13 the
owner asked for the whole project to be reviewed, and for one documented list of problems that
later sessions take one at a time in the order of their severity. The owner asked whether the
project should be split into areas, so the context is not filled, or read in one pass, so the
list is one, and approved in chat the answer below. FlatCheck, in the same repository, was
reviewed the same way the same day (its ADR 0100).

Measured that day, blank lines included: 8,864 lines of code in 57 files
(`src/ImageVisualCompare.Core/` 3,217, `src/ImageVisualCompare.Wpf/` 5,647) and 8,081 of tests.
That fits one context, but a long reading is done well at its start and thinly at its end.

## Decision

1. **Read by area, listed once.** `src/` is split into six areas of 1,300 to 1,600 lines. Each
   is read whole by a reviewer with a clean context, a subagent on the session's model, which
   reads `CLAUDE.md`, `docs/design/code-map.md` and the area's design notes, ADRs and
   requirements first and changes nothing. The session then opens the lines of every finding
   itself before the finding enters the list, reads the seams between the areas, and writes one
   document.
2. **The list is `docs/review/2026-11-13-code-review.md`.** One entry per defect, with an ID
   (`CR-NNN`) that never changes, the file and lines, the case, what should happen, the
   direction of a fix, the test that covers or would reproduce it, and the state of the
   evidence. A summary table at the top orders the entries.
3. **Four severities, by the kind of harm.** 1, the comparison lies: a wrong pixel shown, the
   two images out of step, a wrong value read, or one file named while another is shown; the
   worst a tool built to show a difference of one level can do. 2, a crash, a hang, a file left
   locked, or memory never given back. 3, a wrong text or a wrong layout. 4, robustness and
   cleanliness. Beside it the entry says its reach (an ordinary session, some files, some
   setups, a slip, rare, a crafted file), so that within a severity the common ones come first.
4. **Three states of evidence.** `confirmed`: the lines were read and the case follows.
   `to measure`: the case rests on something only a run of the application or an outside source
   shows, and the task that takes the entry starts by showing it (the kit's rule "Measured
   numbers"). `owner`: the code does what is described and whether that is a defect is the
   owner's call.
5. **One entry per task.** A task takes one entry, in the table's order unless the owner picks
   another. It follows the kit's rules "Requirements first" and "Test before fix" and this
   project's "Run the application", and closes the entry in the list: `fixed <commit>`, or
   `rejected: <reason>`. The list is not rewritten otherwise; a new defect found later is a new
   entry at the end.
6. **Scope.** In: `src/`. Out: `scripts/` and `.claude/` (the kit's), and the tests line by
   line, whose strength `/mutate` measures for Core.

## Alternatives rejected

- **One pass in one context.** The list would be one without any merging, and 8,864 lines fit;
  but the files read last would be read thinly, and the reader of the window would carry the
  FITS reader's details for nothing.
- **One session per area, each with its own list.** The lists have to be merged by hand, the
  same defect seen from two areas is listed twice (seven entries of this list were), and nobody
  reads the seams.
- **A second agent that tries to refute each finding.** About twice the agents for a first
  pass. The session's own check of every finding, and the `to measure` state for what a reading
  cannot settle, cover the same risk at this size; an entry that turns out wrong is closed as
  `rejected` by the task that takes it.
- **Running the application to settle the `to measure` entries during the review.** Half of the
  entries rest on how WPF draws or what a dialog does, and each needs a pair made for it and a
  capture read; that is the first step of each fix, not of a reading of the whole code.
- **Fixing while reviewing.** A fix needs a requirement, a failing test and the gate; mixed into
  the reading it would have made one task of twenty-eight.

## Consequences

The list holds 28 entries (9 of severity 1, 1 of severity 2, 9 of severity 3, 9 of severity 4)
and a cleanup section, from 35 findings reported by the reviewers; none was rejected at the
check; 9 entries are `confirmed`, 14 `to measure` and 5 `owner`. `CLAUDE.md` names
`docs/review/` in its layout. The list ages: an entry's line numbers are those of 2026-11-13,
and a task finds its lines again by the names the entry gives. The review read no test line by
line and ran nothing, so it says nothing about what the tests miss, and its `to measure` entries
are not proved; the two common ones of severity 1 (CR-001, whose arithmetic is confirmed, and
CR-002) are where the application has to be run first.
