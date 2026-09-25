# 2026-09-25 Doc path checks and hook-install corrections

## Context

The owner asked who checks that the documentation is up to date. The honest answer: only a
few links are checked mechanically (requirement IDs and tests, validation figures, CHANGELOG
presence). `/wrap-up` and `/docs-health` read content only when someone runs them. And nothing
checks `GHID.md`: it sits outside `docs/`, the `docs-auditor` never reads it, and no test
touches it. The owner found a stale instruction in it just by asking: "Stratul 1" tells a
project in the monorepo to run its own copy of `install-hooks.ps1`, and that copy would
overwrite `.githooks/` with whatever older hooks the project carries. While re-reading, I also
found an error I introduced today (GHID, the "Hook-ul e comun" section): it says a project does
not need its own `scripts/hooks/`. It does. The Claude Code hooks (`claude-*.ps1`) live there
and `.claude/settings.json` calls them from the project folder.

Outcome: a cheap mechanical check for the kind of staleness a test can see (paths), a
judgement step for the kind it cannot (explanations), and the hook-install text corrected.

## Measured before planning

A read-only prototype of the path rule, run on the current files: GHID.md has 62 path
references, 4 missing, all absent on purpose (`docs/ai-workflow.md` deleted history,
`docs/design/x.md` example, `settings.local.json` and `tests/testdata/large/` git-ignored).
README.md has 30 references, 1 missing (`settings.local.json`). So the test passes today with
those 4 names in its allowlist.

## Decision (ADR 0011)

1. **Paths are tested.** New `RepoInvariantTests.KitGuideAndReadme_NameOnlyPathsThatExist`.
   A path reference is an inline code span without whitespace or placeholders (`< > * [ ] |`),
   or the argument after `-File` (catches the commands in README's code blocks).
   - If it starts with a kit top-level folder (`scripts/ docs/ tests/ src/ .claude/ .github/
     .config/ monorepo/`), it must exist relative to the kit root.
   - If it is a bare file name (`*.md|ps1|json|props|yml|sln|csproj|cs`), it must exist
     somewhere in the kit, outside build output (`bin obj StrykerOutput artifacts TestResults
     .vs .git worktrees`). Build output is excluded because `bin/` holds copies of docs that
     would hide a rename.
   - The 4 names above are allowlisted in the test, each with its reason. A new entry is an
     owner decision, never a way to silence a stale path (rule 3).
   - Kit only: it returns early when `GHID.md` is absent, as in a project made from the kit,
     whose README still points at the kit's guide. No new package; xUnit 2 has no dynamic
     skip without one (rule 5).
   - It catches renamed or deleted files and folders. It does not catch stale explanations,
     so it would not have caught the "Stratul 1" problem.
2. **Content is a wrap-up step.** New step 8 in `.claude/skills/wrap-up/SKILL.md` (old 8 and 9
   become 9 and 10): when the change touched `scripts/`, `.claude/`, `CLAUDE.md` or
   `monorepo/`, say what `README.md` and, in the kit, `GHID.md` say about the touched piece,
   and fix what no longer holds, in the same task. In a project, a change to the process itself
   is flagged as belonging in the kit.
3. **Rejected:** adding GHID to the `docs-auditor` inputs (it audits a project's rebuild
   readiness, and it runs monthly, which is too late); a Stop hook demanding a GHID edit
   whenever `scripts/` changes (it forces edits even when nothing needs saying, and the model
   learns to make token edits); a test comparing the guide's content with the scripts (not
   feasible beyond names).

Written as `accepted 2026-09-25`: approving this plan is the owner's yes to the decision
(GHID: an ADR is accepted when the owner says yes explicitly). If you want to accept it
separately, say so.

## Steps

Order matters because of the parallel session (see below). Files the other session does not
touch go first.

1. Save this plan to `docs/plans/2026-09-25-doc-path-checks.md`.
2. `docs/decisions/0011-guide-paths-tested-content-reviewed-at-wrap-up.md`. First check that
   0011 is still free.
3. `tests/Starter.Tests/TestData.cs`: `FindRepoRoot()` goes from `private` to `internal`, so it
   can be reused. It already walks up to `Starter.sln`, which `rename.ps1` renames in copies.
4. `tests/Starter.Tests/RepoInvariantTests.cs`: the new test plus `[GeneratedRegex]` helpers,
   in the file's existing style (explicit types, ordinal comparisons, message listing
   `doc: path`). Line endings are normalised with `ReplaceLineEndings("\n")` before matching
   fences. Run the one test at once, so the shared tree never holds a red test for long.
5. `.claude/skills/wrap-up/SKILL.md`: the new step 8, and renumber.
6. `README.md`, step 4 rewritten: standalone means `git init` plus `install-hooks.ps1`; a folder
   inside `photo` needs neither, because the hooks are already at the root; a fresh clone needs
   only `git config core.hooksPath .githooks`; after a hook change, run the kit's copy of the
   script. Also:
   - add the new check to "Ce e impus mecanic" and the guide/README review to "Ce ramane pe
     judecata";
   - fix the stray blank line that splits the "Ce e unde" table before the `monorepo/` row.
7. `monorepo/README.md`, install row: run the kit's copy; on a fresh clone,
   `git config core.hooksPath .githooks`.
8. **Only after the other session's changes are committed** (checked with `git status`):
   - **`GHID.md`:**
     - Step 4 of "Cum se foloseste kit-ul": the same split as the README.
     - "Hook-ul e comun": correct the `scripts/hooks/` sentence.
     - The consequences paragraph: the clone command, and run the script from the kit.
     - "Stratul 1": in `photo`, run nothing, because the project's copy would overwrite the
       shared hooks.
     - The gates table, `RepoInvariantTests` row: add the kit-only path check.
     - New paragraph "Cine verifica documentatia" after "Ce e garantat si ce nu": the three
       levels (mechanical, skills on demand, owner) and what nothing checks.
     - The `/wrap-up` step list: add the new step and renumber.
   - **`CHANGELOG.md`:** Added (the check and the wrap-up step) and Changed (the hook-install
     text and the "who checks" paragraph in the guide).
9. Verify, then `/wrap-up`, with a "What deviated" note in the saved plan.

## Parallel session, no branch

Another session ("Ce face scriptul verify.ps1") is working in this kit right now. It has
uncommitted changes in `GHID.md`, `CHANGELOG.md`, `scripts/verify.ps1` and
`scripts/hooks/claude-stop-gate.ps1`. So:

- **No branch** for this task, a deliberate deviation from "branch per task". The repo has one
  working tree: a `git switch -c` would carry the other session's uncommitted work onto my
  branch, and its next commit would land there too.
- **`GHID.md` and `CHANGELOG.md` are edited last, after that session commits.** Otherwise its
  `git add` of those files would sweep my edits into its commit. If it has not committed when I
  reach step 8, I stop and ask.
- The shared tree also means its pre-commit runs verify over my files. That is why step 4 keeps
  the tree green: the test passes from the moment it exists.

## Verification

- `dotnet test Starter.sln --filter "FullyQualifiedName~KitGuideAndReadme"` is green on the
  real files.
- Negative check: temporarily change one path in README (for example
  `scripts/check-staged.ps1` to `scripts/check-stage.ps1`). Run the test: it must fail and name
  `README.md: scripts/check-stage.ps1`. Undo the change with Edit, not `git checkout`, and
  confirm the README diff holds only the intended edits.
- `scripts/verify.ps1` is green with tests (the Stop hook needs the stamp). Paste the last lines.
- Read the whole diff back and summarise it.

## Commit (proposed at wrap-up, not run)

One commit, message about "Check guide and README paths and correct hook install advice". I ask
who runs it. No merge involved, since the work is on `main`.

## What deviated

- **Two commits instead of one.** The owner committed the other session's work as `d800c5f`
  ("script fixes") with everything in the tree at that moment. That commit also took steps 1 to 7
  of this plan: the test, `TestData`, ADR 0011, this plan, the wrap-up step and both READMEs.
  The gate was green on that state (19 tests). Step 8 (`GHID.md`, `CHANGELOG.md`) and this note
  go in a second commit.
- **Two negative checks, not one.** Besides `scripts/check-stage.ps1` in the README table, a
  broken `-File scripts/verfy.ps1` inside a code block was caught as well, which proves the
  `-File` branch works. Both edits were undone. The test takes 25 ms on the real files.
- **Added at wrap-up (step 8 of the new list, applied to this very change):**
  - the "instalare" row of the guide's monorepo table was corrected, to match `monorepo/README.md`;
  - the guide's step 5 now says 19 tests, not 18;
  - an older CHANGELOG line that cited "wrap-up step 8" now says "the wrap-up commit step",
    since the numbering moved;
  - CHANGELOG `Fixed` records the two guide errors and the split README table.
- **Not changed:** `CLAUDE.md` does not mention the path check. It is the template for projects,
  where the check does nothing, and the test's failure message names the doc and the path.
