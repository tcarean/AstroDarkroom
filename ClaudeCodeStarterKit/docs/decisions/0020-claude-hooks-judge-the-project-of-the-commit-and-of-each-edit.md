# 0020. The Claude Code hooks judge the project of the commit and of each edited file

Date: 2026-09-27
Status: accepted 2026-09-27; check-staged and the CHANGELOG check superseded by 0023

## Context

The commit gate and the Stop hook took the project folder from `CLAUDE_PROJECT_DIR`. The Claude
Code documentation (hooks reference, worktrees page) says that variable stays at the folder the
session started in, also when the session works in a worktree, while the JSON every hook gets on
stdin carries `cwd`, the current folder, which follows `cd` and worktrees. The kit's workflow
puts every task in a worktree the assistant makes by hand from a session opened on the project
folder (ADR 0013, ADR 0019), so the hooks looked at the shared checkout. Measured on 2026-09-27,
in the session that did ADR 0017 and ADR 0018: 76 edited paths, 75 under hand-made worktrees and
none under the shared checkout, so the Stop hook judged nothing in three tasks; and a commit
from a green FlatCheck worktree was blocked because the gate ran `verify.ps1` in the shared
checkout, red at step 0. A `git -C <path> commit` was not recognised as a commit at all. In a
worktree the app opens the variable is the worktree, so these sessions were not affected.
(Corrected 2026-09-30: in `photo` those sessions started at the root of a whole-repository
checkout, where no project settings load, so none of these hooks ran there at all; the same
holds for "Right for the app's own worktrees" below. ADR 0027.)

## Decision

The hooks find the project from the work, not from the session. The commit gate finds the
commit's folder: the payload's `cwd` (then `CLAUDE_PROJECT_DIR`), moved by every change of
folder before the commit in the same command (`cd`, `pushd`/`popd` and their PowerShell forms;
heredoc text does not count) and by `git -C <path>`. A folder the command names but the hook
cannot find, such as a shell variable, blocks the commit instead of being guessed. In that
folder it asks git which files the commit takes (staged, plus changed tracked files with `-a`)
and runs `check-staged.ps1` and `verify.ps1` of every project they touch, a project being the
nearest folder holding `scripts/verify.ps1` without leaving the repository or worktree, exactly
the `pre-commit` rule; a commit with no files falls back to the folder's own project, and one
that touches no project passes. It also blocks every form of skipping the git hooks it can
read: `--no-verify` and its prefixes, `-n` alone or grouped (`-anm`), `-c core.hooksPath`. The
Stop hook groups the edited files by the nearest folder holding `kit.json` (same walk, same
stop at `.git`) and judges each project with its own `kit.json`, stamp and CHANGELOG, naming
the project in its message. The owner chose these rules in chat on 2026-09-27, on the
assistant's recommendation, and the fixes from that day's code review of the first version.

## Alternatives rejected

- **Only `cwd` for the commit.** A Bash session that runs `cd <worktree> && git commit` from the
  project folder commits in the worktree while `cwd` still names the folder before the `cd`;
  the command itself says where the commit runs.
- **Block a commit outside every project.** The git `pre-commit` hook already checks each
  project its staged files touch, and a commit in the repository root (the monorepo's own
  files) has no `verify.ps1` to run.
- **One folder for the whole turn (`cwd` at the Stop).** A turn can edit two projects, or the
  kit and a project, and `cwd` at the end says only where the last command ran.
- **Start every task session inside its worktree.** Right for the app's own worktrees, but a
  session that makes the worktree itself cannot move its start folder, and the hooks would
  still be wrong the day someone forgets.
- **The project of the commit's folder alone** (the first version of this decision). The code
  review found that a commit from the repository root, or one whose staged files span two
  projects, then ran one project's gate or none; the staged files say what is committed,
  wherever the command runs.
- **A project is a folder with `kit.json`, for the commit too.** `pre-commit` recognises
  `scripts/verify.ps1`; two layers that disagree on what a project is would let a project with
  a gate but no `kit.json` through one of them.
- **Read shell variables.** The hook would have to run the command's own assignments; a
  literal path is one word more for the model, and the gate never has to guess.
- **Stop the walk at the first marker only.** A worktree made under a project's
  `.claude/worktrees/` sits inside that project, so a file at the worktree's root would be taken
  for the enclosing project's; the walk stops at `.git`, as the `pre-commit` hook stops at the
  repository root.

## Consequences

The hooks follow ADR 0016 and ADR 0019: the git hooks come from the main checkout, the Claude
Code hooks from the session's folder, and both now judge the worktree's copy of the project.
The session's own `settings.json` still decides which hook scripts run, so a hook change takes
effect in a project after `sync.ps1` and, for sessions started in the shared checkout, after the
merge. `HookTests` guards the behaviour: two projects in a throwaway repository with stub
scripts, the commit folder from `cwd`, `git -C`, `cd` (quoted, relative, Git Bash, escaped
space, PowerShell, `pushd`/`popd`, not inside a heredoc), a variable path blocked, the projects
of the staged files (both of two, one from the repository root, the tracked files with `-a`),
every form of skipping the hooks blocked before anything runs while an `-n` in a message is not,
a commit outside every project let through, and the Stop hook judging each edit by its own
project. The tests run on Windows only, where the hooks run; the Linux CI skips them. FlatCheck
takes the change through its routine sync commit (its ADR 0048) and checks it live from a
hand-made worktree.
