# 2026-09-27 Adopt the shared skills and agent: docs/skills.md in, per-project skill edits out

## Context

Session 2 of the kit's plan `docs/plans/2026-09-27-share-the-skills-and-the-agent-through-sync.md`
(kit ADR 0018, merged as 1f6719a), in FlatCheck, in the same session at the owner's instruction.
FlatCheck's six skills and its `docs-auditor` were edited copies (three kinds of difference, see
ADR 0049); the kit's versions now read `kit.json` and `docs/skills.md`.

## Steps

1. `scripts/sync.ps1` from the kit over `scripts/`, `.claude/skills/`, `.claude/agents/`;
   `sync.ps1 -Check` exit 0.
2. `docs/skills.md` with FlatCheck's steps, one section per skill, each naming the step it
   applies to; `audit.alsoRead` in `kit.json`.
3. ADR 0049; rule 19 and Layout in `CLAUDE.md`; CHANGELOG `Internal`; README process bullet;
   `settings.json` comment.
4. `scripts/verify.ps1` green.
5. `/docs-health` once, through `general-purpose` with the new agent text (the agent type
   registers at the next session start): the report must list `Golden/` among the files opened
   and no code folder.

## Verification

`sync.ps1 -Check` exit 0 from both sides; verify green; the auditor's "Files opened" list.

## What deviated

- Nothing in the steps. The kit's `sync.ps1` was run from the worktree's kit folder (kit mode),
  which also brought the four scripts changed by kit ADR 0018; 11 files copied, `-Check` exit 0.
- The `/docs-health` run went through `general-purpose` with the new agent's text, as the
  skill's fallback says. Its "Files opened" list holds `kit.json`, `CLAUDE.md`, `CHANGELOG.md`,
  `README.md`, `THIRD-PARTY-NOTICES.md`, `docs/**`, the test data README and manifest, four
  files under `FlatCheck.Tests/Golden/` and the test names; nothing under `FlatCheck.Core/`,
  `FlatCheck/`, `FlatCheck.Wpf/`, `.claude/` or `scripts/`. The audit is valid.

## The docs-health report of 2026-09-27 (ADR 0049 points here)

Verdict: **mostly**. A fresh model can plan and place a console change from
`docs/design/process.md` and `code-map.md`, and can explain both recent CHANGELOG entries from
the ADRs and plans; what stops it short is a handful of contradictions between notes and one
undecided rule.

Gaps found (what is missing, where it should go, effort):

1. Whether `--out` exists as the long form of `-o`: `README.md` says `-o, --out`, `Golden/help.txt`
   prints `-o` only, the Options contract names only `OutPng`. R-006, then the wrong file. Minutes.
2. A rule for a flag that must be known before the banner (the dispatch prints the banner before
   `Options.Parse`; R-001 says every run prints it; ADR 0042 settles only the machine-readable
   case). R-001 plus a new ADR. An hour: a decision, not a search.
3. `code-map.md` says plans are deleted once done; CLAUDE.md and `docs/plans/` keep them. Minutes.
4. Project count: "four projects" in `code-map.md` and `process.md` (Licensing), "five" in R-096.
   Minutes.
5. CLAUDE.md Commands "the flag comes after the path" contradicts R-003 (either side). Minutes.
6. `process.md` "Committing" still describes a commit body; "Conventions" says one external
   dependency while two are listed. Minutes.
7. R-001 is `implemented` but its two tests cover six modes, not the three synthetic ones; the
   row does not say the test is partial. A clause, or the test (owner's call, rule 3).
8. Stale paths and headings in `code-map.md` (`docs/lens-profiles.md`; "The six modes"). Minutes.
9. Test name `R011_TheOverallVerdictIsTheWorstOfTheFourTests` says four; R-011 says five since
   ADR 0015. Owner's call.
10. ADR 0042 names `crosscheck.py` guards that ADR 0047 retired without saying it amends 0042.
    One line in 0047's consequences. Minutes.

Top three: the banner-before-parse rule with no carve-out; `-o, --out` against `-o` alone;
contradictory process statements left standing with disclaimers (plans, project count, flag
order, commit body).

What is good: `process.md` "Extending the console" is a complete recipe (the auditor wrote a
full `--quiet` plan from it alone); `code-map.md`'s result and outcome tables place a console or
window change without opening the core; requirements, tests and decisions are in step (37
`implemented` rows, 37 IDs in test names, every ADR `accepted` or `superseded`).

None of the gaps is fixed in this task; each is a task of its own (`/docs-health` step 4).
