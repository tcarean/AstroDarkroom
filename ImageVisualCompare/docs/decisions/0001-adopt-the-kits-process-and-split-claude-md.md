# 0001. Adopt the kit's process; CLAUDE.md splits into requirements, decisions and design notes

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

ImageVisualCompare was built on 2026-09-08 and 2026-09-09 with Claude Code, in ten commits,
before the kit at `../ClaudeCodeStarterKit` existed. Its documentation was one 247-line
`CLAUDE.md`: a file map ("Structure"), ten invariants that each record a decision and the defect
that taught it, the sections "No imaging dependency", "Memory, measured", "Testing",
"Versioning" and "Licensing", and a section "Committing" that told the assistant not to commit
("Make the changes, run the tests, and hand over a commit message for the user to use") and to
write messages with "a short lowercase subject line, then a body that explains *why*". The user
documentation was, and stays, the Romanian `README.md`. There was no `docs/` folder, no gate
before a commit (the root pre-commit hook skips a project without `scripts/verify.ps1`), no
requirement IDs, no decision records, no changelog, and no test read a measured figure.
FlatCheck adopted the kit on 2026-09-25 (its ADR 0001) and AstroPhotoAnalyzer on 2026-10-02 (its
ADR 0001). On 2026-10-02 the owner approved the same for this project
(`docs/plans/2026-10-02-adopt-the-starter-kit.md`): "Aprob planul, trei commit-uri, mută
versiunea în Directory.Build.props".

## Decision

ImageVisualCompare adopts the kit's process as the kit's guide (`GHID.md`, "Migrarea unui
proiect existent") describes it for an existing project, in three layers, one commit each:

- **The gate.** The kit's `scripts/`, skills, agent and rules, synced and never edited here;
  `kit.json` (ADR 0002); `.claude/settings.json`; `CHANGELOG.md` (ADR 0007); central package
  versions and lock files and one `dotnet format` pass (ADR 0003); the version in
  `Directory.Build.props` (ADR 0004); the test data manifest (ADR 0005); English comments
  (ADR 0006).
- **The documentation.** `CLAUDE.md` is split by what each part is: the ten invariants and the
  topical sections become decision records (ADR 0008 to ADR 0026, retroactive) and design notes
  in `docs/design/`, moved unchanged; "Structure" becomes `docs/design/code-map.md`; the
  behaviour the README and the code describe becomes `docs/requirements.md`, with IDs; the
  measured memory figures become history in `docs/validation.md`. `CLAUDE.md` keeps only the
  project's rules, the commands and the pointers; the process rules are the kit's, in
  `.claude/rules/kit/process.md`.
- **Requirements and tests.** `RepoInvariantTests`, `ValidationTests` on a real pair of images,
  and test names that carry their requirement's ID.

The section "Committing" is superseded by the kit's rule "Commits and merges": the assistant
commits, without asking, on the `claude/` branch it made for its task, one line in English, no
body (the kit's ADR 0021). **Nothing written before 2026-10-02 is deleted: it is moved.** The only
new sentences in the moved text are pointers. Where a requirement row, a design note and an ADR
were written from the same paragraph, the three say it in their own words for now; cutting the
notes down to what neither a row nor an ADR says is a task of its own, as FlatCheck's ADR 0051
was two days after its migration.

## Alternatives rejected

- **Keep `CLAUDE.md` whole and add the kit beside it.** It loads into every session; its
  invariants are decisions with a "why", which the kit's rule "Decisions" keeps in
  `docs/decisions/`, and its file map says how the code works, which the kit's rule "Design
  notes" keeps in a note that changes with the files it names. `RepoInvariantTests` reads rows
  with IDs, which no section of it was.
- **Rewrite it as new prose.** It was accurate on 2026-10-02; a rewrite risks a measured detail
  (the 50 %-alpha check of the backdrop, the memory table), a move keeps it.
- **One commit for the whole migration, as in FlatCheck.** The owner chose three, so each layer
  can be read alone and the gate is green at each.

## Consequences

- Every commit that touches the project runs `scripts/verify.ps1` through the root
  `.githooks/pre-commit`, and every merge through `pre-merge-commit`.
- The assistant commits on its task branch; merging into `main` stays the owner's call, asked
  each time.
- From layer 3 on, `RepoInvariantTests` ties the `implemented` rows to `R###_` test names both
  ways.
- The owner's approval of the plan, which listed ADR 0008 to ADR 0026 by title and source, is
  their acceptance; their text is what the owner reads in the diff.
