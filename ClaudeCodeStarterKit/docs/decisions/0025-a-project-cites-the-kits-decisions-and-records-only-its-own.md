# 0025. A project cites the kit's process decisions and records only its own

Date: 2026-09-30
Status: accepted 2026-09-30
Supersedes: 0009 in part (the list of inputs); 0018 in part (the auditor's extra inputs)

## Context

The kit's process decisions (branches, commits, the gate, sync, hooks) apply to every project
that adopts the kit. The first adopter recorded some of them again as short ADRs of its own that
point back at the kit, because the owner wanted the rules found from the project's own decisions,
and others not: of the eleven kit decisions taken since it adopted the kit, 0014, 0020 and 0022
have none. The cost showed within a week: one project ADR per kit change, written only when
someone remembered; numbers clashing between parallel branches, so that three accepted kit ADRs
(0013, 0015, 0021) were edited to follow the adopter's renumbering; the decision text in two
places. Separately, the synced files cited bare "ADR NNNN" and "rule N", which in an adopter
resolve to its own, unrelated decisions and rules: `/wrap-up` called the shared folders "the
kit's (rule 15)", and the adopter's rule 15 is about metrics. On 2026-09-30 the owner decided, in
chat, to stop mirroring.

## Decision

- A kit process decision lives only in the kit's `docs/decisions/`. A project that adopts the kit
  follows every kit ADR unless one of its own ADRs records a deviation. A project writes an ADR
  only for its own choice: a deviation, or a value or step of its own (`kit.json`,
  `docs/skills.md`). A kit decision that reaches a project through `scripts/sync.ps1` gets no
  project ADR.
- Outside the kit, a kit decision is cited as "the kit's ADR NNNN". The kit's synced files
  (`scripts/`, `.claude/skills/`, `.claude/agents/`) and its git hooks, which run in every folder
  of a monorepo, cite every ADR that way, and cite a rule by the ADR that records it or by its
  words, never by its number: each project numbers its `CLAUDE.md` rules its own way.
- A new kit ADR names no adopter's ADR numbers; those that already do are not rewritten. When a
  kit ADR supersedes part of what a project ADR says, the kit's task adds
  `; <part> superseded by the kit's ADR NNNN` to that project ADR's Status, on the task branch.
- The `docs-auditor` also reads the kit's `docs/decisions/`, found through `kit.json`'s `kit`
  key: titles and Status first, bodies when a question needs them. ADR 0011 kept the guide out
  of the audit because it is not part of a project. The kit's ADRs are not part of it either,
  but the project follows them as its own process decisions, so a model rebuilding it from its
  docs needs them; the guide only explains them. This also changes the auditor's extra inputs,
  which ADR 0018 limited to `audit.alsoRead`.
- Project ADRs that already restate kit decisions stay as they are; nothing is backfilled.

## Alternatives rejected

- **Keep a pointer ADR in each project for each kit decision.** One ADR per kit change per
  project, number clashes between branches, the decision text twice; and several kit decisions
  had none anyway.
- **An index of the kit's ADRs in the project's adoption ADR.** An accepted ADR is not rewritten
  (ADR 0024), and a list kept by hand goes stale with every new kit ADR.
- **A synced copy of the kit's decisions inside each project.** Findable with a grep in the
  project, but every kit ADR twice, which is what this decision removes; kept as the fallback if
  the auditor's route proves not enough.
- **Qualify the ADR references and keep rule numbers.** The kit and its first adopter already
  number their rules differently.

## Consequences

`RepoInvariantTests.SyncedFiles_CiteTheKitsAdrsAndNoRuleNumbers` fails on a bare ADR number or a
rule number in the synced folders or the git hooks. `/wrap-up` step 6 carries the rule when an ADR
is written, step 9 the Status update when a kit ADR supersedes part of a project ADR. A project
with no kit beside it (no `kit` key) gives its auditor no kit decisions, as step 0 of the gate
skips the sync check there.
