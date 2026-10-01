# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in `Directory.Build.props`.

## [Unreleased]

### Added

- The kit's rule "Version and year are the owner's": the version and the copyright year change
  only at the owner's word (ADR 0029). `git tag` joins the `ask` list.
- `.claude/rules/kit/process.md`, the process rules every project follows, kept once in the kit
  and synced like `scripts/` (ADR 0028): Claude Code loads it beside `CLAUDE.md`. The list of
  shared folders is written once, in `kit-config.ps1`, for `sync.ps1` and the Stop hook. Step 0
  of `verify.ps1` also compares the process keys of `.claude/settings.json` with the kit's
  (`hooks`, `ask`, `deny`, the git `allow` entries, the new empty `attribution`, which keeps
  Claude Code from adding a `Co-Authored-By` line, and `disableAllHooks` and
  `permissions.defaultMode`, which stay unset), and `sync.ps1` copies them, leaving a project's
  other `allow` entries alone. The `docs-auditor` reads `.claude/rules/`.
- The git `pre-commit` hook refuses, before its own gate, a commit a Claude Code session makes
  on a `claude/` branch in the main checkout, which stays on `main` (ADR 0026); through
  `pre-merge-commit`, a session's merge commit onto such a branch too. It tells a session by
  the `CLAUDECODE` variable Claude Code sets, so the owner's commits, from shells that do not
  set it, are not refused, and it prints how to move the work to the branch's own worktree.
  `/wrap-up` merges into `main` with `--no-ff`, since a fast-forward runs no git hook.
- `verify.ps1` checks the decision records (ADR 0024): a new step, `decisions`, runs
  `scripts/check-decisions.ps1`, synced to every project, which checks the header lines of each
  ADR (title with its number, `Date`, `Status`, optional `Supersedes`), unique numbers, one
  `## Decision` with no dated correction note inside it, and that every supersession, whole or
  in part, is written in both ADRs. An accepted ADR merged into `main` changes three ways only: in
  place for a typo or a renamed path, a dated `Corrected`/`Restated` note for a wrong figure or a
  retired reason, a superseding ADR for anything that changes what is done. The back-pointers
  missing so far are filled in.
- The six skills and the `docs-auditor` agent are shared like `scripts/` (ADR 0018):
  `scripts/sync.ps1` mirrors `.claude/skills/` and `.claude/agents/` too, step 0 of `verify.ps1`
  and the Stop hook cover them, rule 15 names them. A skill takes its values from `kit.json` by
  reference (the solution, the folders, the publish list) and reads `docs/skills.md`, the
  project's additions with one section per skill, before its first step; the kit ships that file
  with empty sections. The agent reads `kit.json` first, never opens `codeFolders` or
  `codeFiles`, may read the optional `audit.alsoRead` list, and reports in English with the
  files it opened. `ValidationTests` carries `Category=Validation`, the trait `/validate` selects
  in every project. Improvements FlatCheck had made to its copies are in the kit's now: a
  `/validate` step after numeric code and `Internal` in the CHANGELOG step of `/wrap-up`, a new
  `R###` test moves its requirement, ADRs for choices settled in chat, README as user
  documentation, `git status -- .` and the owner's version in `/release`, a fallback when the
  agent type is not registered yet, the exit code in a requirement and a "Figures" step in
  `/new-feature`, "on a large suite pass a filter" in `/mutate`. `SyncTests` covers a skill
  edited and an agent added in a project.

- One `scripts/` for every project (ADR 0017): the scripts read `kit.json` at the project root
  (solution, test filter, code and test folders, test data folder and its environment variable,
  Stryker's test project, the list of what a release publishes, where the kit is) through
  `scripts/kit-config.ps1`, so a project changes no script to adopt them. `scripts/sync.ps1`
  copies the kit's `scripts/` into a project, or from the kit into every project whose `kit.json`
  points at it, leaving `rename.ps1`, `install-hooks.ps1` and the two git hooks in the kit;
  `-Check` reports drift and is step 0 of `verify.ps1`, red on any difference. The Stop hook
  refuses a turn that edited `scripts/` outside the kit. Rule 15 says so; `SyncTests` and
  `CheckStagedTests` cover it. `release.ps1` publishes a list (`release.publish`), each entry with
  its own output subfolder and `dotnet publish` arguments, so a project with two applications
  needs no script of its own.
- `LICENSE`: the kit is GPL-3.0-or-later; every script opens with the SPDX line, so a project's
  copy is byte-identical whatever its own header rules say.

### Fixed

- `verify.ps1` and `release.ps1` start their child scripts with the PowerShell that runs them
  (`Get-KitShell` in `kit-config.ps1`), not with `powershell` by name, which does not exist
  under `pwsh` on Linux CI: the `bitbucket-pipelines.yml` run failed at the test data step.
- A merge that completes without conflicts runs the gate (ADR 0022): git calls
  `pre-merge-commit`, not `pre-commit`, for such a merge, so until now it entered `main` with no
  check at all. The new `scripts/hooks/pre-merge-commit` runs the `pre-commit` beside it, on the
  merged result, for the projects the merge touches; a red gate leaves the merge unrecorded.
  `install-hooks.ps1` copies it to `.githooks/`, `sync.ps1` keeps it kit-only, `.gitattributes`
  keeps it LF. `GitHookTests` covers a green and a red merge.
- The Claude Code hooks judge the worktree the work is in, not the folder the session started in
  (ADR 0020). `CLAUDE_PROJECT_DIR` stays at that folder, so in a session that made its worktree
  by hand the Stop hook judged none of the edits and the commit gate ran the shared checkout's
  `verify.ps1`. The commit gate now follows the command to the commit's folder (`cd`,
  `pushd`/`popd`, `git -C`, then the hook's `cwd`), asks git there which files the commit takes,
  and runs check-staged and verify of every project they touch, found as the git `pre-commit`
  hook finds them; a folder it cannot read, such as a shell variable, blocks the commit. It now
  also blocks `-n` inside a group of short options (`-anm`), abbreviations of `--no-verify` and
  `-c core.hooksPath`, and sees `git -C <path> commit`, which passed unchecked before. The Stop
  hook groups the edited files by project and judges each with its own `kit.json`, stamp and
  CHANGELOG, naming it in the message. `HookTests` (Windows only, like the hooks) covers both
  with stub projects; `ScriptRunner` pipes a hook's JSON on stdin.
- `check-testdata.ps1 -Update` writes the manifest without a byte order mark and with LF (Windows
  PowerShell 5.1's `Set-Content -Encoding utf8` writes a mark, which `System.Text.Json` refuses),
  and an entry's `name` may be a path with forward slashes. Both fixes had been made in FlatCheck's
  copy only, on 2026-09-25.

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
- Monorepo: the root `.gitignore` carries `**/.claude/worktrees/`, so worktrees of every project
  stay out of `git status`; the kit keeps its own line for standalone use (ADR 0015).
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

- The kit's rules "Two readings" and "Measured numbers" (ADR 0030): a design choice with more
  than one reasonable answer is measured on real data, put to the owner with the facts and the
  options, and, once answered, implemented without asking it again. `/new-feature` goes on to
  the plan and the code without re-asking after the owner's yes. The practice was FlatCheck's
  own rule until now.
- `release.ps1` runs only on `main` in the main checkout (`release.branch` in `kit.json` names
  another branch for a standalone repository), and its floor is the newest released CHANGELOG heading, not
  `<Version>` (ADR 0029): the owner may bump `<Version>` per feature, and a release of that same
  version leaves `Directory.Build.props` as it is. It refuses a version already released, not
  above the newest heading or below `<Version>`. `/release` runs in a session opened in the
  project folder of the main checkout, proposes `<Version>` when the owner already bumped it,
  checks that HEAD did not move, and proposes a commit limited to the two files it changed.
- `CLAUDE.md` keeps only the project's own content: the process rules and the workflow moved to
  the synced `.claude/rules/kit/process.md` (ADR 0028). Every rule has a name and is cited by it
  in quotes, never by a number; the kit's own two rules are "Core has no UI" and "No code
  structure in docs". The task's plan maps the old numbers, which older ADRs and CHANGELOG lines
  still use, to the names. The guide and the READMEs say where each rule lives.
- The desktop app's worktree option is not used in `photo` (ADR 0027): its sessions started at
  the root of a whole-repository checkout, where no project `CLAUDE.md`, settings or Claude
  hooks load. Task worktrees are made by hand, so the rename of generated branch names is gone
  from `CLAUDE.md`, `/wrap-up` and the guide; `/wrap-up` checks instead that it is not about to
  commit on a `claude/` branch in the main checkout.
- A project cites the kit's process decisions and records only its own (ADR 0025): no project
  ADR for a kit decision, only for a deviation or a project value. The synced files and the git
  hooks cite "the kit's ADR NNNN" and never a rule by its number, which in a project named
  another rule (`/wrap-up` called the shared folders "rule 15"); a kit test guards it. The
  `docs-auditor` also reads the kit's `docs/decisions/`.
- A commit message may be one line of up to 100 characters (was 72), and a CHANGELOG line is a
  judgment made at `/wrap-up`, not a gate (ADR 0023): a line for what a reader of the version
  history cares about, none for a review follow-up or a test-only change. Rules 9 and 13, the
  wrap-up and mutate skills, GHID and README say so; `GitHookTests` checks the 100-character
  limit.

- The assistant commits without asking on the `claude/` branch it created for its task; commits
  on `main`, any other branch or a detached HEAD are the owner's, and only the merge (with push,
  rebase and `branch -D`) is still asked (ADR 0021, superseding ADR 0007). `git commit` moves
  from `permissions.ask` to `allow`; `claude-commit-gate.ps1` refuses a model commit off a
  `claude/` branch before check-staged and verify run, judging the branch of the commit's own
  worktree. `/wrap-up` commits on the task branch and asks only for the merge; `/release`
  leaves the release commit and the tag to the owner. Rule 13, the guide, the README and the
  generator prompt say so; `HookTests` covers `main`, `master`, `feature/...`, a detached HEAD
  and a `claude/` worktree beside a checkout on `main`. `/wrap-up` says how to end a merge that
  stops on conflicts (`git merge --continue`, since the hook refuses `git commit` on `main`) and
  that a gate failing only on the merged result is fixed on the task branch.
- A worktree the assistant makes is named after its branch without `claude/`, created with it in
  one command (`git worktree add .claude/worktrees/<Task-name> -b claude/<Task-name> main`), so
  the owner finds it to review before the commit (ADR 0019). CLAUDE.md and the guide say so; an
  app worktree keeps its generated folder.
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
  hook is shared, the gate (`verify.ps1`) is per project; a fresh clone of the monorepo runs the
  kit's `install-hooks.ps1` once (ADR 0016); `install-hooks.ps1` runs from the kit; a project inside
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

- `scripts/check-staged.ps1` and its tests: `pre-commit`, `pre-merge-commit` and the Claude
  commit gate run `verify.ps1` alone, and the Stop hook no longer demands a changed
  `CHANGELOG.md` (ADR 0023).

- `docs/ai-workflow.md` (ADR 0006): it duplicated CLAUDE.md and `GHID.md` and nothing read it in
  the working loop. CLAUDE.md holds the rules, `GHID.md` the explanations, kept once in the kit;
  each project points to it instead of carrying a copy.

### Fixed

- Git hooks never ran in the Claude desktop app's worktrees of a monorepo (ADR 0016): the app
  copies `core.hooksPath` into each worktree, resolving the relative `.githooks` against the
  project folder, where no hook exists; git said nothing and commits skipped check-staged, verify
  and commit-msg. `install-hooks.ps1` now writes the absolute path of the main checkout's
  `.githooks/` and warns when a `config.worktree` hides it. Guide, README and `monorepo/README.md`
  say a fresh clone runs the script once.
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
