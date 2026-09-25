# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`.

## [Unreleased]

### Added

- Kit: solution with Core, Cli and Tests; verify gate; requirements, decisions, validation, plans
  and design scaffolding; Claude Code settings, hooks and skills; CI for GitHub and Bitbucket.
- Deterministic backstops for three prose rules (ADR 0005): `check-staged.ps1` refuses a commit
  touching `src/` without a CHANGELOG line; `RepoInvariantTests` ties implemented requirements to
  `R###_` test names both ways; a Stop hook refuses to end a turn that edited code until verify
  ran green and CHANGELOG changed.
- `GHID.md`: the reference guide (Romanian). Started as an export of the claude.ai document; since
  2026-09-25 it is the master copy, edited directly. Added: gate versus hook, when each trigger
  runs, what `/wrap-up` does and when to use it, migrating an existing project in three layers,
  what an ADR is, when it counts as accepted, and how to supersede one. ADR template gains a
  `Supersedes:` line and a dated `accepted`; `/new-feature` waits for the owner's yes before
  planning, `/wrap-up` flags an ADR still `proposed` under code about to be committed.
- Mutation testing (ADR 0008): Stryker.NET 4.16.0 as a versioned local tool, `stryker-config.json`
  targeting Core, `scripts/mutate.ps1` printing score and survivors, `/mutate` skill. Not in the
  gate. Baseline on the sample code: 26 mutants tested, score 88.5 %, 3 min 42 s.
- Rule 13 (ADR 0007): one-line English commit messages without trailers; the assistant proposes
  commits and merges and asks each time who runs them. Enforced by a `commit-msg` git hook
  (installed beside pre-commit) and by `permissions.ask` for commit, merge, push and rebase.
- Monorepo support: the pre-commit hook verifies only the project folders touched by the staged
  files; `install-hooks.ps1` detects the layout; root CI templates under `monorepo/`.
- `/docs-health` now launches the `docs-auditor` subagent (`.claude/agents/`, ADR 0009): clean
  context, never opens `src/`, fixed report format ending with the files it opened. First run on
  the kit: 17 findings, none from `src/`.

- `/release` skill over `scripts/release.ps1` (ADR 0010): from `main` with a clean tree, SemVer
  proposed from the `[Unreleased]` sections, verify, version bump, dated CHANGELOG section,
  publish to `artifacts/<version>/`; commit and tag proposed, never run by the script. Tag is
  `vX.Y.Z` standalone, `<folder>-vX.Y.Z` in a monorepo.
- Doc path check (ADR 0011): in the kit, `RepoInvariantTests` fails when `GHID.md` or `README.md`
  names a script, folder or file that does not exist; the four names absent on purpose are
  listed in the test with their reason. `/wrap-up` gains step 8: when a change touches
  `scripts/`, `.claude/`, `CLAUDE.md` or `monorepo/`, check what the guide and README say about it.
- `AI Prompt to Generate Starter Project.md`: a stack- and model-neutral prompt that asks a future
  agent to rebuild a setup with the same principles, gates, documentation set and acceptance
  checklist from the best practices of its own day; it describes goals and proofs, not this
  implementation.
- Branch names and clean-up (ADR 0013): a branch is named `claude/` plus its task, in English,
  as an imperative phrase with hyphens; a generated name (an app worktree's `claude/` plus random
  words) is renamed once the task is clear, before its first commit, and `/wrap-up` checks it
  before proposing the commit. Once merged into `main`, the assistant deletes the branch with `git branch -d`; `git branch -D`
  joins the `ask` list. `/wrap-up` now states 72 characters for the commit line, as rule 13 and
  the hook do, not 70. The guide's `/code-review` and `/simplify` examples use `main...<branch>`.

### Changed

- ADR 0013 and the CLAUDE.md workflow: a task branch lives in its own worktree; the main checkout
  stays on `main`, where the owner merges and which parallel sessions share. The guide says why.
- ADRs 0001 to 0005 carry the date of acceptance in their status, as the template requires.
- CLAUDE.md rule 13 states the 72-character limit; the mutate command lists `-BreakAt`.
- Guide brought in step with the kit on 2026-09-25: structure table (check-staged, hooks,
  install/rename, monorepo/, GHID.md, `ask` list), all 13 rules, trigger table with `ask` and
  commit-msg, the wrap-up commit step, CHANGELOG rule as enforced, `/mutate` and `/release` in the
  start-up steps, agents in the monorepo note, layer 1 of migration.
- Guide: `/code-review` runs in the same session (it is already a background subagent with its
  own context), not in a second one; branch form `main...<branch>`; `/wrap-up` suggests it before
  proposing a merge. Findings by category: only correctness findings need a failing test first.
  `/simplify` documented: per-session diff, report first then `--fix`, whole project only per
  area as separate tasks.
- Guide and READMEs on git hooks: installing means setting `core.hooksPath`, once per clone; the
  hook is shared, the gate (`verify.ps1`) is per project; a fresh clone of the monorepo needs only
  `git config core.hooksPath .githooks`; `install-hooks.ps1` runs from the kit; a project inside
  `photo` needs neither `git init` nor an install. Guide: who checks the documentation, and what
  nothing checks.
- Every script in `scripts/` and `scripts/hooks/` opens with a plain-English header: what it
  does, who calls it (you, git, Claude Code, CI, skills, other scripts), its options and its exit
  codes; the less obvious lines have a short comment. Comments only, no code changed.
- Rule 14 (ADR 0012): English in everything the model reads or writes, Romanian only in the
  guide and the READMEs; the guide no longer says comments may stay in Romanian.
- Line endings (ADR 0014): LF in the repository and on disk on every platform (`* text=auto
  eol=lf`, `end_of_line = lf`), `.cmd`/`.bat` CRLF, `.ico` binary, fixtures `-text` so the
  manifest's hashes hold in every clone. A new worktree on Windows used to get CRLF fixtures and
  fail step 1 of verify. The guide explains the order of a change of rules; `monorepo/README.md`
  adds the root `.gitattributes`; `tests/testdata/README.md` says fixtures are never converted.

### Removed

- `docs/ai-workflow.md` (ADR 0006): it duplicated CLAUDE.md and `GHID.md` and nothing read it in
  the working loop. CLAUDE.md holds the rules, `GHID.md` the explanations, kept once in the kit;
  each project points to it instead of carrying a copy.

### Fixed

- `check-staged.ps1` from a linked worktree: git runs the hook with `GIT_DIR` set, and
  `git rev-parse --show-toplevel` then named the project folder as the repository root, so in a
  monorepo the check never matched `src/` and passed every commit. The script now finds the root
  by walking up to the nearest `.git`. `CheckStagedTests` runs it in a throwaway repository with
  and without `GIT_DIR`.
- `verify.ps1 -SkipTests` wrote `.verify-stamp` too, so the Stop hook let a turn that edited code
  end with the tests never run. Only a run with tests writes the stamp now; the hook's message
  says that `-SkipTests` does not count.
- Guide: layer 1 of the migration told a project inside the monorepo to run its own copy of
  `install-hooks.ps1`, which would overwrite the shared hooks with the project's; and it said a
  project does not need `scripts/hooks/`, where the Claude Code hooks live.
- README: a blank line split the "Ce e unde" table before the `monorepo/` row.
- `rename.ps1` also rewrote the kit's own name, so a new project's CLAUDE.md pointed the guide at
  `ClaudeCode<Name>Kit`, a folder that does not exist. `ClaudeCodeStarterKit` now survives the
  rename.
- Stop hook: once a session's `src/` edits and their CHANGELOG line were committed, every later
  turn was refused with "CHANGELOG.md is unchanged". The CHANGELOG check now applies only while
  an edited `src/` file is still uncommitted.
- Claude commit gate failed open: when a check printed to stderr (dotnet format does), Windows
  PowerShell turned that into an error, the hook ended with exit 1 and Claude Code let the
  commit run. The checks now run through `cmd`, which merges stderr itself, and any error inside
  the hook blocks the commit (exit 2).

## [0.1.0] - 2026-09-24

Placeholder first version. Delete this section when the real project starts.
