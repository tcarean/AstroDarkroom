# 0024. An accepted ADR changes by a dated note or a superseding ADR; one header grammar, checked

Date: 2026-09-30
Status: accepted 2026-09-30
Supersedes: 0001 in part ("not edited after acceptance"); 0003 in part (the list of steps)

## Context

ADR 0001 says an accepted ADR is not edited and a new one supersedes it; the guide allows a direct
fix for a typo, and draws the line at "before commit", which predates the assistant committing on
its own `claude/` branches (ADR 0021). Practice went both ways. The kit rewrote accepted ADRs in
place with no trace (0013, 0021, 0022), and a later ADR made part of an earlier one false without
the earlier one's Status saying so: 0021 changed who runs the release commit that 0010 describes,
0023 deleted the `check-staged.ps1` that 0017, 0020 and 0021 name in their decisions. The first
adopting project appended dated "Restated" and "Corrected" notes to four accepted ADRs, and wrote
"ADR N is amended" in new ADRs while N stayed plain `accepted`. The two repositories also wrote a
partial supersession differently, and the only format check, a test in the adopter, rejected the
kit's form. On 2026-09-30 the owner decided, in chat, the three classes below and one checked
grammar.

## Decision

- Until an ADR is merged into `main` it is edited freely on its branch, `proposed` or `accepted`.
- After the merge:
  - a typo, an unclear phrase or a renamed path is fixed in place, with no note;
  - a wrong figure, or a supporting reason that no longer holds while the decision stands, gets a
    dated note next to the text it corrects, outside `## Decision`: `(Corrected YYYY-MM-DD: ...)`
    or `(Restated YYYY-MM-DD: ...)`, saying what was wrong and what holds, and pointing at where
    a measurement is recorded rather than copying it;
  - anything that changes what is done is a superseding ADR. A later ADR that replaces, removes or
    makes false part of an earlier one supersedes that part ("amends" is a partial supersession).
    The old ADR changes only its Status line, the new one names it on its Supersedes line. A
    decision abandoned with no replacement gets a short ADR that supersedes it and says why.
- The header lines are bookkeeping: Status and Supersedes may be completed after the merge so that
  both sides agree.
- The header, lines 1 to 5 of every ADR except `0000-template.md`:
  - `# NNNN. <title>`, with the number of the file `NNNN-slug.md`;
  - `Date: YYYY-MM-DD`;
  - `Status: proposed`, `accepted YYYY-MM-DD` or `superseded by REF`, the last two followed by
    any number of `; <part> superseded by REF`, each with an optional `(<note>)`. REF is a number
    of this project, or, in a project, `the kit's ADR NNNN`;
  - optional `Supersedes:`, items `NNNN` or `NNNN in part (<part>)` separated by `; ` or `, `,
    numbers of this project only.
- `scripts/check-decisions.ps1`, synced like every script, is a step of `scripts/verify.ps1`
  after the test data manifest. It checks the header grammar, unique numbers, one `## Decision`,
  no dated note inside it, that every number named exists and is older, that both sides of a
  supersession agree (a whole `X` against `superseded by N`, `X in part` against a
  `; <part> superseded by N` clause), and in a project that each `the kit's ADR NNNN` exists in
  the kit when the kit is beside it. The steps of the gate are listed once, in the header of
  `verify.ps1`.

## Alternatives rejected

- **Never edit an accepted ADR (0001 as written).** A wrong figure or a retired reason would each
  need a new ADR; numbers multiply and clash between parallel branches (ADR 0022 records one
  clash), and neither repository kept to it.
- **Any in-place edit, with a dated note.** The Decision text would drift from what was decided
  and when; a reader could no longer tell the history.
- **The format check as a test in each project.** Tests are not synced; the one that existed
  already disagreed with the kit's own headers.
- **A `withdrawn` status for an abandoned decision.** No ADR used one; a superseding ADR says
  why and keeps a single grammar.
- **Flag "is amended" in bodies.** The wording varies; whether an edit should have been a new ADR
  stays a judgment at `/wrap-up` and in the owner's reading of the diff, as ADR 0023 left the
  CHANGELOG.

## Consequences

The gate has seven checks; a red `decisions` step names the file and the rule. The back-pointers
missing so far are added in the same task: Status lines of 0001, 0003, 0009, 0010, 0017, 0020 and
0021, Supersedes lines of 0021, 0022 and 0023. `0000-template.md` shows the grammar, and the
guide's section on decisions says the same in Romanian. A project gets the check by sync and
retires any ADR format test of its own. The check cannot tell whether an in-place edit changed a
decision; `/wrap-up` step 6 asks.
