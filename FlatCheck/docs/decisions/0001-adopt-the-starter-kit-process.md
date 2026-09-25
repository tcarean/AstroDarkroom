# 0001. Adopt the ClaudeCodeStarterKit process; rules in CLAUDE.md, explanations in docs/design

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

FlatCheck was developed almost entirely with Claude Code, and by 2026-09-25 it carried a
2709-line CLAUDE.md: the rules, the per-file rationale, the algorithms, every measurement and
every rejected design, loaded into every session and competing with the code for context. It
had 960 green tests and four evidence notes in `docs/`, but no requirements with IDs, no
decision records (decisions lived in chat and in the owner's Claude memory), no changelog, no
gate before a commit, and nine build warnings. The owner had meanwhile built a template for new
projects, the kit at `E:\Photografie\Code\ClaudeCodeStarterKit`, whose guide (`GHID.md`)
describes migrating an existing project in three layers and uses FlatCheck as the worked
example. On 2026-09-25 the owner asked for that migration, all three layers.

## Decision

FlatCheck adopts the kit's process as the guide describes it: one gate script run by the git
pre-commit hook, the Claude Code commit hook and the skills; `docs/requirements.md` with IDs
carried in test names; `docs/decisions/` for every decision with a "why"; `docs/validation.json`
read by tests; a `CHANGELOG.md` a commit cannot skip; six skills and the `docs-auditor` agent.
CLAUDE.md is cut to the rules, the commands and the pointers (136 lines). **Nothing written in
the old CLAUDE.md is deleted**: every section moved unchanged into a note under `docs/design/`,
indexed by `docs/design/README.md`, which CLAUDE.md points at under "Read before touching". The
reasons behind the process itself stay in the kit's `GHID.md`, once, and are not copied.

The migration was planned in one session (`docs/plans/2026-09-25-adopt-starter-kit.md`) and
carried out in one, at the owner's instruction, rather than one commit per layer.

## Alternatives rejected

- **Trim CLAUDE.md by deleting.** Every paragraph in it records a measurement or a rejected
  design that had already prevented one regression; the guide's rule for migration is "nothing
  is deleted, it is moved".
- **Keep the design prose in CLAUDE.md and add the kit files beside it.** The guide's second
  principle: context is the scarce resource, and a rule in a 2700-line file is followed less
  often than one in a 130-line file. The prose is now loaded only when a task needs it.
- **Copy `GHID.md` into the project.** ADR 0006 of the kit: one copy, in the kit; a copy per
  project goes stale at every kit change.

## Consequences

Every commit that touches FlatCheck now runs `scripts/verify.ps1` through the root
`.githooks/pre-commit` (about 3 minutes; ADR 0003). The design notes are maintained under the
same rule as before: each says which code it covers and is updated or deleted with that code.
The retroactive ADRs 0008 to 0041 were written on the same day from those notes; the owner's
approval of the migration plan is their acceptance, and their text is what the owner reads in
the diff. `RepoInvariantTests` ties `docs/requirements.md` to the `R###_` test names from now on.
