# 2026-09-26 One `scripts/` for every project: parameters in `kit.json`, source in the kit

## Context

The owner expected `scripts/` to be copied into a project unchanged and kept identical
everywhere, with the kit as the only place a script is fixed or extended. FlatCheck's adoption
(2026-09-25) instead edited six of the eight copied scripts. Measured on 2026-09-26 with `diff`
between `ClaudeCodeStarterKit/scripts/` and `FlatCheck/scripts/`:

| Script | What really differs | Other |
| --- | --- | --- |
| `verify.ps1` | solution name; `--filter 'Category!=Validation'` on the test step | comments |
| `check-staged.ps1` | list of code folders instead of the `src/` prefix | comments |
| `hooks/claude-stop-gate.ps1` | lists of code and test folders | message wording |
| `check-testdata.ps1` | testdata folder; name of the large-data environment variable | two bug fixes made only in FlatCheck: the manifest is written without a byte order mark and with LF (Windows PowerShell 5.1 writes a BOM, `System.Text.Json` refuses it), and `name` may hold `/` |
| `mutate.ps1` | default test project | comments |
| `release.ps1` | publish: two projects with different arguments into `console/` and `wpf/`; `-PublishProject` dropped | comments; the tag is already generic |
| `hooks/claude-commit-gate.ps1`, `hooks/claude-track-edits.ps1` | nothing | comments |

Every FlatCheck copy also carries `# SPDX-License-Identifier: GPL-3.0-or-later` on its first
line (FlatCheck rule 18); the kit has no LICENSE and no header. FlatCheck did not copy
`install-hooks.ps1`, `rename.ps1`, `hooks/pre-commit` and `hooks/commit-msg` (FlatCheck ADR 0007).
So every customisation is data, not logic; the one structural change is that publish becomes
a list. The two `check-testdata.ps1` fixes are the case the new rule prevents: they never
reached the kit.

Out of scope, noted for a later task: the six skills and the `docs-auditor` agent also differ
between the kit and FlatCheck (prose for the model, not code).

## Decisions taken by the owner (2026-09-26, in chat)

1. The kit gets a licence: GPL-3.0-or-later, the same as FlatCheck. `LICENSE` is copied from
   FlatCheck; every script's first line is the SPDX line. A project's copy is then identical.
2. `sync.ps1` mirrors `scripts/` with an exception list: `rename.ps1`, `install-hooks.ps1`,
   `hooks/pre-commit` and `hooks/commit-msg` stay in the kit only (the sync neither copies nor
   deletes them in a project), as FlatCheck ADR 0007 decided.
3. Drift is red in `verify.ps1`, not a warning: an isolated edit in a project's `scripts/`
   cannot be committed, and after a kit change every adopter's next commit demands the sync.
4. Skills and the agent are a separate, later task.

## Design

- **`kit.json` at the project root**, beside `CLAUDE.md`, never inside `scripts/`, so the folder
  can be overwritten whole. Keys, with the kit's own values as the defaults:
  `solution` (`Starter.sln`), `testFilter` (empty), `codeFolders` (`["src/"]`), `codeFiles`
  (`[]`), `testFolders` (`["tests/"]`), `testData.folder` (`tests/testdata`),
  `testData.largeEnv` (`STARTER_TESTDATA_LARGE`), `mutate.testProject` (`tests/Starter.Tests`),
  `release.publish` (a list of `{ "project", "output", "args" }`; the kit publishes
  `src/Starter.Cli/Starter.Cli.csproj` to `artifacts/<version>/` with `output` empty),
  `kit` (`../ClaudeCodeStarterKit`, relative to the project folder; works from an app worktree
  too, because a worktree of `photo` holds the whole repository). Folder values use forward
  slashes and end with `/`. The kit ships its own `kit.json`; `rename.ps1` renames it with the
  rest.
- **`scripts/kit-config.ps1`**, dot-sourced by every script: reads `<root>/kit.json`, fills
  missing keys with the defaults above, fails with one clear line when the file is absent (a
  project that has `scripts/` must declare itself; the git pre-commit hook then refuses the
  commit through verify). Windows PowerShell 5.1 compatible, like the rest (ADR 0003).
- **`scripts/sync.ps1 [-Check]`**: run in a project, makes its `scripts/` identical to the kit's
  at `kit.json:kit`, minus the exception list, deleting files the kit no longer has. Run in the
  kit (the `kit` path resolves to the project itself), does that for every folder directly under
  the git root that has a `kit.json` naming this kit. `-Check` only reports the differing paths
  and exits 1 when there are any. Prints what it copied or deleted.
- **Enforcement** (prose rules become mechanisms, ADR 0005):
  1. `verify.ps1` step 0, "kit scripts": `sync.ps1 -Check`; red on drift with the message
     "run scripts/sync.ps1". When the `kit` path does not exist (standalone repository, CI,
     the kit itself), one yellow line and the step is skipped.
  2. `hooks/claude-stop-gate.ps1` refuses to end a turn that edited a file under `scripts/`
     when the project is not the kit: "scripts/ is the kit's; make the change there, then
     scripts/sync.ps1".
  3. The rule in text: kit `CLAUDE.md` (a change under `scripts/` ends with `sync.ps1` and one
     commit per adopter, proposed like any commit), a project's `CLAUDE.md` (never edit
     `scripts/`; `kit.json` is the only knob), `GHID.md` (why), `README.md`, `monorepo/README.md`.
- The scripts' tests live only in the kit: `CheckStagedTests` gains a case with custom
  `codeFolders` from a `kit.json`; a new `SyncTests` builds a throwaway repository with a kit
  and a project folder and checks `-Check` (identical: 0; a changed byte: 1 and the path
  named; an exception-list file missing in the project: 0) and the copy.

## Branch

Worktree `ClaudeCodeStarterKit/.claude/worktrees/parametrize-scripts`, branch
`claude/Parametrize-the-kit-scripts-with-kit-json`, added from the shared checkout so it runs
the hooks (ADR 0016). The session doing the work starts in that worktree's kit folder; the
FlatCheck part is a second task in a FlatCheck session.

## Steps

### Session 1, in the kit

1. `LICENSE` (GPL-3.0-or-later, from FlatCheck) and the SPDX first line on every script,
   including the two sh hooks (`# SPDX-License-Identifier: GPL-3.0-or-later` after the shebang).
2. `kit.json` with the kit's values; `scripts/kit-config.ps1` with the defaults and the
   missing-file error.
3. Refactor the six scripts to read the loader; `release.ps1` publishes the list;
   `check-testdata.ps1` takes the two FlatCheck fixes (BOM/LF, `/` in `name`). Comments name the
   `kit.json` key, not `src/` or `Starter.sln`, as the fact.
4. `scripts/sync.ps1`; verify step 0; the `scripts/` refusal in `claude-stop-gate.ps1`.
5. Tests: `CheckStagedTests` custom folders; `SyncTests`. `RepoInvariantTests` doc-path check
   sees the new names (`kit.json`, `sync.ps1`, `kit-config.ps1`) once the docs name them.
6. Docs: `CLAUDE.md` rule, `README.md` (adoption: copy `scripts/`, write `kit.json`, done),
   `GHID.md` (the section "Hook-ul e comun, gate-ul e al fiecarui proiect" becomes "scripts/ e
   comun; kit.json e al fiecarui proiect"; the migration layer 1 no longer says "adapt the
   solution name in verify, mutate and release"), `monorepo/README.md`, ADR 0017 (this design,
   the three alternatives: keep per-project edits and merge by hand, as FlatCheck ADR 0002
   accepted; environment variables instead of a file; a PowerShell module installed once),
   CHANGELOG.
7. `scripts/verify.ps1` green in the kit; `/wrap-up`; commit proposal.

### Session 2, in FlatCheck (its own plan, `claude/Adopt-the-shared-kit-scripts`)

1. `kit.json` with FlatCheck's values (three code folders, two test folders,
   `FlatCheck.Tests/testdata`, `FLATCHECK_TESTDATA_LARGE`, `Category!=Validation`, the two
   publish entries); `scripts/sync.ps1` from the kit's copy, then verify green: the proof that
   the parameters cover everything.
2. `CLAUDE.md`: the new rule; rule 18 unchanged (the scripts carry the same SPDX line from the
   kit). An ADR superseding ADR 0002 ("the scripts carry the list") and amending ADR 0007 (the
   exception list is now the sync script's, not a copying decision); `Internal` line in CHANGELOG.

## Verification

Kit: `scripts/verify.ps1` green, with the new tests; `sync.ps1 -Check` in the kit lists
FlatCheck as drifted (before session 2) and exits 1; `release.ps1 -Version 0.2.0 -WhatIf`
unchanged in output. FlatCheck, after session 2: `sync.ps1 -Check` exits 0; `verify.ps1` green;
`release.ps1 -WhatIf` still names both publish targets; a one-byte edit in
`FlatCheck/scripts/verify.ps1` turns the gate red with the sync message.

## What deviated

- `kit-config.ps1` has no defaults for missing keys: every key except `testFilter`, `codeFiles`
  and `kit` is required and a missing one is an error naming it. A silent default (`tests/`
  for a project whose tests are elsewhere) would have made the Stop hook blind without anyone
  noticing; the kit's own `kit.json` is the example instead.
- The kit is told apart by `"kit": "."` in its `kit.json`, not by a separate flag.
- The SPDX line in the two sh hooks goes after the shebang; the root `.githooks/` copies were
  refreshed with `install-hooks.ps1` in the same change.
- `ScriptRunner` was extracted from `CheckStagedTests` so `SyncTests` could share it.
- Session 1 ran from the FlatCheck session at the owner's instruction, with the kit's
  `verify.ps1` run by hand in the worktree (29 tests green).
