# 0011. Paths in the guide and README are tested; their content is reviewed at wrap-up

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The mechanical checks cover a few links between documents and code: requirement IDs and test
names, the figures in `validation.json`, a CHANGELOG line for `src/`. `/wrap-up` and
`/docs-health` judge content, but only when someone runs them. Nothing checked `GHID.md` at
all: it lives outside `docs/`, the `docs-auditor` does not read it, and no test opens it. The
owner found a stale instruction in it by asking a question: layer 1 of the migration told a
project inside the monorepo to run its own copy of `install-hooks.ps1`, which would overwrite
the shared `.githooks/` with that project's hooks. A read-only prototype of a path check on
2026-09-25 found 62 path references in the guide and 30 in the README; the only ones absent
were four named on purpose.

## Decision

Two backstops, one for each kind of staleness:

1. **Paths, by a test.** `RepoInvariantTests` checks that every path `GHID.md` and `README.md`
   name exists in the kit. A path is an inline code span without spaces or placeholders, or the
   argument after `-File`. One that starts with a top-level folder of the kit must exist at that
   place. A bare file name must exist somewhere in the kit outside build output, because `bin/`
   holds copies of docs that would hide a rename. The names absent on purpose are listed in the
   test, each with its reason. The test runs only where `GHID.md` exists, which is the kit.
2. **Explanations, by judgement.** `/wrap-up` gains a step. When a change touches `scripts/`,
   `.claude/`, `CLAUDE.md` or `monorepo/`, the model says what the README and, in the kit, the
   guide say about that piece, and fixes what no longer holds in the same task.

## Alternatives rejected

- **Add `GHID.md` to the `docs-auditor` inputs.** The auditor measures whether a project's own
  docs are enough to rebuild it. The guide is not part of a project. It also runs monthly,
  which is too late for a guide that changes with every kit task.
- **A Stop hook that demands a guide edit whenever `scripts/` changes.** It would force an
  edit even when nothing needs saying, and the model would learn to make token edits.
- **A test that compares the guide's explanations with the scripts.** Not feasible beyond
  names: what a script does is prose on one side and code on the other.

## Consequences

A renamed or deleted script, folder or document now fails the gate with the doc and the path
named, so the guide and the README cannot keep pointing at nothing. A stale explanation still
passes the test, and the "Stratul 1" error would have passed it; that one is caught only if
`/wrap-up` runs. A docs-only change does not trigger the Stop hook, so the owner asks for it.
An entry in the test's allowlist is an owner decision, like any change to a test (rule 3),
never a way to silence a stale path. In a project made from the kit, `GHID.md` is deleted, so
the test returns without checking anything. A project that later leaves the `photo` repository
takes a copy of the guide with it (ADR 0006), and from then on the test checks that copy's paths
against the project.
