# 0001. Adopt the kit's process; CLAUDE.md splits into requirements, decisions and design notes

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Mindfulness Reminder was built between 2026-08-28 and 2026-09-14 with Claude Code, in eight
commits, before the kit at `../ClaudeCodeStarterKit` existed. Its documentation was one
451-line `CLAUDE.md`: a file map ("Structure"), five "Conventions", seven sections that each
record the decisions of one feature and the defect behind each ("The periodic reminder",
"Pausing for what the user is doing", "Two reminders, two switches, two messages", "The tray
tooltip", "Windows the app shows", "The dark theme", "Settings storage"), "Build & test",
"Versioning", "Licensing", and a section "Committing", quoted whole at the end of this section.
The user documentation was, and stays, the English `README.md`. There was no `docs/` folder, no
gate before a commit (the root pre-commit hook skips a project without `scripts/verify.ps1`), no
requirement IDs, no decision records, no changelog. FlatCheck adopted the kit on 2026-09-25 (its
ADR 0001), AstroPhotoAnalyzer and ImageVisualCompare on 2026-10-02 (their ADR 0001). On
2026-10-09 the owner approved the same for this project
(`docs/plans/2026-10-09-adopt-the-starter-kit.md`): "Aprob planul, trei commit-uri, publish
framework-dependent".

The section "Committing" of the former `CLAUDE.md`, which this decision supersedes, said:

> **Do not commit.** Make the changes, run the tests, and hand over a commit message for the
> user to use. They commit themselves.
>
> This also means: don't `git add`, don't stage, don't amend. Leave the working tree as it is
> so they can review the diff before deciding.
>
> A good commit message here follows the existing log: a short lowercase subject line, then a
> body that explains *why* rather than restating the diff. No `Co-Authored-By` trailer.

Its "Conventions" carried the same rule in one line: "**Never bump the version and never
commit** — both are the user's call. See "Versioning" and "Committing"."

## Decision

Mindfulness Reminder adopts the kit's process as the kit's guide (`GHID.md`, "Migrarea unui
proiect existent") describes it for an existing project, in three layers, one commit each:

- **The gate.** The kit's `scripts/`, skills, agent and rules, synced and never edited here;
  `kit.json` (ADR 0002); `.claude/settings.json`; the strictness in `Directory.Build.props`,
  central package versions and lock files, and one `dotnet format` pass (ADR 0003); English
  everywhere, the README included (ADR 0004); no validation figures and no test data (ADR
  0005); `CHANGELOG.md` (ADR 0006).
- **The documentation.** `CLAUDE.md` is split by what each part is: the conventions and the
  feature sections become decision records (ADR 0007 to ADR 0033, retroactive) and design notes
  in `docs/design/`, moved unchanged; "Structure" becomes `docs/design/code-map.md`; the
  behaviour the README and the code describe becomes `docs/requirements.md`, with IDs.
  `CLAUDE.md` keeps only the project's rules, the commands and the pointers; the process rules
  are the kit's, in `.claude/rules/kit/process.md`.
- **Requirements and tests.** `RepoInvariantTests`, and test names that carry their
  requirement's ID.

The section "Committing", and the commit half of the convention "Never bump the version and
never commit", are superseded by the kit's rule "Commits and merges": the assistant commits,
without asking, on the `claude/` branch it made for its task, one line in English, no body (the
kit's ADR 0021). The version half is the kit's rule "Version and year are the owner's" (the
kit's ADR 0029) and is not written again here. **Nothing written before 2026-10-09 is deleted:
it is moved.** The only new sentences in the moved text are pointers. Where a requirement row,
a design note and an ADR were written from the same paragraph, the three say it in their own
words for now; cutting the notes down to what neither a row nor an ADR says is a task of its
own, as FlatCheck's ADR 0051 was two days after its migration.

## Alternatives rejected

- **Keep `CLAUDE.md` whole and add the kit beside it.** It loads into every session; its
  feature sections are decisions with a "why", which the kit's rule "Decisions" keeps in
  `docs/decisions/`, and its file map says how the code works, which the kit's rule "Design
  notes" keeps in a note that changes with the files it names. `RepoInvariantTests` reads rows
  with IDs, which no section of it was.
- **Rewrite it as new prose.** It was accurate on 2026-10-09; a rewrite risks a detail learned
  the hard way (the Enter that dismissed an unread reminder, the tooltip that counted down to a
  reminder that was not coming), a move keeps it.
- **One commit for the whole migration, as in FlatCheck.** The owner chose three, so each layer
  can be read alone and the gate is green at each.

## Consequences

- Every commit that touches the project runs `scripts/verify.ps1` through the root
  `.githooks/pre-commit`, and every merge through `pre-merge-commit`.
- The assistant commits on its task branch; merging into `main` stays the owner's call, asked
  each time.
- From layer 3 on, `RepoInvariantTests` ties the `implemented` rows to `R###_` test names both
  ways.
- The owner's approval of the plan, which listed ADR 0007 to ADR 0033 by title and source, is
  their acceptance; their text is what the owner reads in the diff.
