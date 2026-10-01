# Running the kit inside a monorepo

The kit does not need to be the root of a git repository. Claude Code takes the folder it is
started in as the project root, so `CLAUDE.md`, `.claude/settings.json`, the hook, the skills,
`global.json`, `Directory.Build.props` and `.editorconfig` all work per folder. The pieces
below are repo-wide and are handled here.

| Piece | Standalone repo | Monorepo |
| --- | --- | --- |
| pre-commit hook | `core.hooksPath = scripts/hooks` | one copy at `<root>/.githooks/pre-commit`, runs `verify.ps1` only in the project folders touched by the staged files; before that it refuses a Claude Code session's commit on a `claude/` branch in the main checkout (ADR 0026) |
| pre-merge-commit hook | same | one copy at `<root>/.githooks/pre-merge-commit`; runs the pre-commit copy beside it, so a merge git records without conflicts gets the same gate on the merged result (ADR 0022) |
| commit-msg hook | same | one copy at `<root>/.githooks/commit-msg`; the one-line rule then applies to every commit in the repository |
| install | `scripts/install-hooks.ps1` | same script, the kit's copy (each copy writes its own hooks over `.githooks/`); it detects the layout. A project added later needs no install. A fresh clone runs it once: `core.hooksPath` is the absolute path of the main checkout's `.githooks/`, so every worktree runs the hooks the main checkout has checked out, normally `main`'s (ADR 0016) |
| CI | `bitbucket-pipelines.yml`, `.github/workflows/ci.yml` in the kit | `monorepo/bitbucket-pipelines.yml` or `monorepo/github-ci.yml` copied to the root, one step per project with a path filter |
| line endings | `.gitattributes` and `.editorconfig` in the project | the project's files still apply per folder; one `<root>/.gitattributes` gives the projects without their own the same baseline: LF everywhere, CRLF for `.cmd`/`.bat`, binaries (ADR 0014) |
| Claude Code worktrees | the line in the kit's `.gitignore` | `**/.claude/worktrees/` in the root `.gitignore`, once for every project; without `**/` it matches only the root (ADR 0015) |
| version tags | `v1.2.0` | `flatcheck-v1.2.0` |
| Claude Code | start it in the project folder | same; never in the git root, and not with the desktop app's worktree option, which starts the session at the root of a whole checkout (ADR 0027) |

A project folder without `scripts/verify.ps1` is left alone by the hook, so existing projects
can adopt the gate one at a time: copy `scripts/` from the kit (without `rename.ps1`,
`install-hooks.ps1` and the three git hooks), write a `kit.json` with the project's values and
`"kit": "../ClaudeCodeStarterKit"`, done; the same copy brings `.claude/skills/`,
`.claude/agents/`, `.claude/rules/kit/` and `.claude/settings.json`, with the project's own
skill steps in `docs/skills.md` and its own rules in `CLAUDE.md`. From then on those folders and
the process keys of the settings are not edited in the project: step 0 of `verify.ps1` compares
them with the kit's, and `scripts/sync.ps1` copies the kit's version after a change there
(ADR 0017, ADR 0018, ADR 0028). There is no `CLAUDE.md` or `.claude/` at the git root: Claude
Code would load them in every folder, including projects that have not adopted the kit.
