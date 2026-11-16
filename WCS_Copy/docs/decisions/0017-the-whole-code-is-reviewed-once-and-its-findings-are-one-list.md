# 0017. The whole code is reviewed once and its findings are one list

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

CopyWcs's first code was written by an earlier model and had almost no code review; the reviews
the project has known since look only at the change of one task. On 2026-11-16 the owner asked
for the whole project to be reviewed, and for one documented list of problems that later
sessions take one at a time in the order of their severity. The owner asked whether the project
should be split into areas, so the context is not filled, or read in one pass, so the list is
one.

Measured that day, blank lines included: 4,447 lines of code (`src/CopyWcs.Core/` 2,271,
`src/CopyWcs.Cli/` 382, `src/CopyWcs.Wpf/` 1,794) and 6,077 of tests. LightsCheck, FlatCheck,
DarksCheck, AstroDarkroom and ImageVisualCompare were reviewed the same way (LightsCheck's ADR
0046), and this decision follows theirs so the lists read alike. It differs in two places,
points 4 and 5 below, which the project's small size allows.

## Decision

1. **Read by area, listed once.** `src/`, the folder `kit.json` names as code, is split into
   four areas: the FITS header engine; the run, the console and the language; the window; the
   diagnostic log. Each is read whole by a reviewer with a clean context, a subagent on the
   session's model, which reads `CLAUDE.md`, the area's design note and its requirements first
   and changes nothing in the repository. The session then opens the lines of every finding
   itself before the finding enters the list, reads the seams between the areas, and writes one
   document.
2. **The list is `docs/review/2026-11-16-code-review.md`.** One entry per defect, with an ID
   (`CR-NNN`) that never changes, the file and lines, the case, what should happen, the test
   that covers the spot or the lack of one, and the state of the evidence. A summary table at
   the top orders the entries.
3. **Four severities, by the kind of harm**, for a tool that writes into the owner's image
   files. 1, a file written wrong or lost (a wrong, missing or stale card, a truncated file, a
   file overwritten), or an outcome said wrongly. 2, a crash, a hang, a frozen window. 3, a
   wrong text or display, a misleading refusal. 4, robustness and cleanliness, a weak test.
   Beside it the entry says its reach (an ordinary run, some files, some setups, a slip, rare,
   a crafted file). Within severity 1 a file lost or truncated comes before a wrong card.
4. **Four states of evidence.** `shown`: the case was run and the entry quotes the output.
   `confirmed`: the lines were read and the case follows. `to measure`: the case rests on
   something only a run of the window, a screenshot or another program shows, and the task
   that takes the entry starts by showing it (the kit's rule "Measured numbers"). `owner`: the
   code does what is described and follows a requirement, a note or a test that pins it;
   whether that is a defect is the owner's call. The first state is this project's addition:
   its tests build any header in a few lines and run in seconds, so the cases a test can build
   were run in one throw-away test class, kept outside the repository, copied in for the run
   and removed. No test of the review stays: a fix writes its own (the kit's rule "Test before
   fix").
5. **The prototype is read to compare.** `prototype/copy_wcs.py` is frozen and not reviewed,
   but a behaviour the port lost without a requirement or an ADR saying so is a finding against
   the C# code (CR-004 and CR-005 are such).
6. **The tests are read for one question.** Each reviewer reads its area's tests to say which
   cannot fail and which do not check what their name says; those are one entry per area
   (CR-028 to CR-031). How strong the tests are line by line stays `/mutate`'s job.
7. **The copied pieces have their own section.** A defect in a piece of the window copied from
   a sibling (ADR 0007) is listed as `CP-N` and fixed in the sibling first.
8. **One entry per task.** A task takes one entry, or the group one entry names, in the table's
   order unless the owner picks another. It follows the kit's rules "Requirements first" and
   "Test before fix", shows the owner any text it would change, original and proposal, and
   closes the entry in the list: `fixed <commit>`, or `rejected: <reason>`. The list is not
   rewritten otherwise; a new defect found later is a new entry at the end.
9. **Scope.** In: `src/`, and the tests as point 6 says. Out: `scripts/` and `.claude/` (the
   kit's), `prototype/` beyond point 5, `Icon/make_icon.py`, and the Help button, in work in
   another task that day and not on `main`.

## Alternatives rejected

- **One pass in one context.** The code alone would fit, but with the notes, the requirements
  and 6,000 lines of tests the last area would be read thinly, and a reviewer that has read
  the session's own reasoning tends to confirm it.
- **One session per area, each with its own list.** The lists have to be merged by hand, and
  the same defect seen from two areas is listed twice. Here the new-file path that names the
  source was seen from three areas and is one entry.
- **A second agent that tries to refute each finding.** The session's own check of every
  finding and the run of the cases cover the same risk at this size; an entry that turns out
  wrong is closed as `rejected` by the task that takes it.
- **Fixing while reviewing.** A fix needs a requirement, a failing test and the gate, and a
  text change needs the owner's yes; mixed into a reading of the whole code it would have made
  one task of thirty.
- **Keeping the throw-away tests.** They assert what the code does wrong today; a test that
  stays must assert what it should do, which is the fix's first step.

## Consequences

The list holds 31 entries (6 of severity 1, 2 of severity 2, 9 of severity 3, 14 of severity
4), three entries for the copied pieces and a cleanup section, from 40 findings reported; none
was rejected at the check; 4 entries are `to measure` and 5 are `owner`. Eleven entries are
`shown`, and three more quote a run. `CLAUDE.md` names `docs/review/` in its layout. The list ages: an entry's line numbers
are those of commit `181f21e3`, and a task finds its lines again by the names the entry gives.
The window was never opened by the review, so its entries rest on a reading.
