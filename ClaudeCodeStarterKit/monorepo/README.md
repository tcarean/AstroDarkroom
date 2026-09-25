# Running the kit inside a monorepo

The kit does not need to be the root of a git repository. Claude Code takes the folder it is
started in as the project root, so `CLAUDE.md`, `.claude/settings.json`, the hook, the skills,
`global.json`, `Directory.Build.props` and `.editorconfig` all work per folder. Three things are
repo-wide and are handled here.

| Piece | Standalone repo | Monorepo |
| --- | --- | --- |
| pre-commit hook | `core.hooksPath = scripts/hooks` | one copy at `<root>/.githooks/pre-commit`, runs `verify.ps1` only in the project folders touched by the staged files |
| commit-msg hook | same | one copy at `<root>/.githooks/commit-msg`; the one-line rule then applies to every commit in the repository |
| install | `scripts/install-hooks.ps1` | same script; it detects the layout |
| CI | `bitbucket-pipelines.yml`, `.github/workflows/ci.yml` in the kit | `monorepo/bitbucket-pipelines.yml` or `monorepo/github-ci.yml` copied to the root, one step per project with a path filter |
| version tags | `v1.2.0` | `flatcheck-v1.2.0` |
| Claude Code | start it in the project folder | same; never in the git root |

A project folder without `scripts/verify.ps1` is left alone by the hook, so existing projects
can adopt the gate one at a time: copy `scripts/verify.ps1` and `scripts/check-testdata.ps1`,
adjust the solution name, done.
