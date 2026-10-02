# 0001. Adopt the kit's process; the specification splits into requirements, decisions and design notes

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

AstroPhotoAnalyzer was built in two days, 2026-09-20 and 2026-09-21, with Claude Code, in seven
phases, as a sibling of FlatCheck and before the kit at `../ClaudeCodeStarterKit` existed. Its
documentation followed FlatCheck's pre-kit pattern: a 201-line `CLAUDE.md` with the rules, the
structure, the dependency rule, versioning and committing; `docs/specification.md`, 727 lines,
called "the requirements document", which held the behaviour, the mechanics of every analyzer,
the thresholds, the report layout, the window, the tests, a file map and a checklist per kind of
change in one file; `docs/implementation-plan.md`, 231 lines, the decisions taken before the
first line and the lessons of each phase; `docs/acceptance-de-printat.md`, the run on the
reference image against the numbers made by hand. There was no gate before a commit (the root
pre-commit hook skips a project without `scripts/verify.ps1`), no requirement IDs, no decision
records, no changelog, and no test read the acceptance figures. FlatCheck adopted the kit on
2026-09-25 (its ADR 0001). On 2026-10-02 the owner approved the same for this project
(`docs/plans/2026-10-02-adopt-the-starter-kit.md`), with three commits, one per layer.

## Decision

AstroPhotoAnalyzer adopts the kit's process as the kit's guide (`GHID.md`, "Migrarea unui
proiect existent") describes it for an existing project, in three layers:

- **The gate.** The kit's `scripts/`, skills, agent and rules, synced and never edited here;
  `kit.json` (ADR 0002); `.claude/settings.json`; `CHANGELOG.md` (ADR 0006); central package
  versions and lock files; one `dotnet format` pass (ADR 0003); the test data manifest (ADR 0004).
- **The documentation.** `docs/specification.md` is split by what each part is: the behaviour
  becomes `docs/requirements.md`, with IDs; the decisions become `docs/decisions/` (0007 to 0024,
  retroactive); the mechanics become notes in `docs/design/`, with an index; the file map becomes
  `docs/design/code-map.md`. Then the file is removed, since every section has a home.
  `docs/implementation-plan.md` moves unchanged to `docs/plans/2026-09-20-implementation-plan.md`:
  a plan is history. `docs/acceptance-de-printat.md` becomes `docs/validation.md` plus the cases
  of `docs/validation.json`. `CLAUDE.md` keeps only the project's rules, the commands and the
  pointers; the process rules are the kit's, in `.claude/rules/kit/process.md`.
- **Requirements and tests.** `RepoInvariantTests`, `ValidationTests` on the reference image, and
  the first batch of tests named `R###_...`.

**Nothing written before 2026-10-02 is deleted: it is moved.** The only new sentences in the
moved text are pointers. Where a requirement row, a design note and an ADR were written from the
same paragraph, the three say it in their own words for now; cutting the notes down to what
neither a row nor an ADR says is a task of its own, as FlatCheck's ADR 0051 was two days after
its migration.

## Alternatives rejected

- **Keep the specification as the requirements document and add the kit beside it.** The kit's
  rule "Requirements first" and `RepoInvariantTests` read rows with IDs in `docs/requirements.md`;
  727 lines of prose cannot be tied to test names in both directions. And the specification mixed
  what the tool does with how the code does it, which the kit's rule "Design notes" keeps apart
  so that a note changes with the files it names.
- **Rewrite the specification as new prose.** It was accurate on 2026-10-02, kept so by the old
  rule that every behaviour change updated it in the same change; a rewrite risks a measured
  detail, a move keeps it.
- **One commit for the whole migration, as in FlatCheck.** The owner chose three, so each layer
  can be read alone and the gate is green at each.

## Consequences

- Every commit that touches the project runs `scripts/verify.ps1` through the root
  `.githooks/pre-commit`, and every merge through `pre-merge-commit`.
- The old rule "every change to behaviour updates `docs/specification.md` in the same change"
  becomes the kit's rules "Requirements first" (the row, then the test, then the code) and
  "Design notes" (the note of the area in the same task). `docs/design/extending.md` keeps the
  checklist per kind of change that the specification's §15 was.
- From layer 3 on, `RepoInvariantTests` ties the `implemented` rows to `R###_` test names both
  ways; most rows stay `accepted` until their area is next touched.
- The owner's approval of the plan, which listed ADRs 0007 to 0024 by title and source, is their
  acceptance; their text is what the owner reads in the diff.
