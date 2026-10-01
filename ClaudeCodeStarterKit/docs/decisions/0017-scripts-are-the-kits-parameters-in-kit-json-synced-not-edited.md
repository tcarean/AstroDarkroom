# 0017. scripts/ is the kit's: parameters in kit.json, copied by sync.ps1, never edited in a project

Date: 2026-09-26
Status: accepted 2026-09-26; the kit-only file list superseded by 0022; CheckStagedTests superseded by 0023; the shared folders and the rule's place in CLAUDE.md superseded by 0028
Supersedes: 0003 in part (the list of steps)

## Context

The kit's `scripts/` was meant to be copied into a project unchanged. FlatCheck's adoption on
2026-09-25 edited six of the eight scripts it copied: the solution name and a test filter in
`verify.ps1`, lists of code and test folders in `check-staged.ps1` and `claude-stop-gate.ps1`
(FlatCheck ADR 0002), the test data folder and environment variable in `check-testdata.ps1`,
the test project in `mutate.ps1`, and a publish step with two applications in `release.ps1`.
Every one of those edits is data, not logic. Two real bug fixes made in FlatCheck's
`check-testdata.ps1` (a manifest written without a byte order mark and with LF; `/` in a
file name) never came back to the kit. The owner asked on 2026-09-26 that every project use
the same `scripts/`, with the differences in a JSON file, and that a script change be made in
the kit and copied to every project, never in one project alone.

## Decision

- `kit.json` at the project root holds what the scripts need: `solution`, `testFilter`,
  `codeFolders`, `codeFiles`, `testFolders`, `testData.folder`, `testData.largeEnv`,
  `mutate.testProject`, `release.publish` (a list of project, output subfolder and extra
  `dotnet publish` arguments) and `kit`, the kit folder relative to the project (`.` in the
  kit). `scripts/kit-config.ps1` reads it; a missing file or key is an error naming it, not a
  default taken from the folder layout.
- `scripts/sync.ps1` mirrors the kit's `scripts/` into a project (add, overwrite, delete), or
  from the kit into every folder under the git root whose `kit.json` points at it. Four files
  stay in the kit only and are neither copied nor deleted: `rename.ps1`, `install-hooks.ps1`,
  `hooks/pre-commit` and `hooks/commit-msg` (the owner's choice; FlatCheck ADR 0007 gives the
  reasons: a project's `install-hooks.ps1` would overwrite the shared `.githooks/`).
- The rule is a mechanism (ADR 0005): `sync.ps1 -Check` is step 0 of `verify.ps1`, red on any
  difference when the kit folder exists beside the project, so an isolated edit cannot be
  committed and every project's next commit after a kit change demands the sync; the Stop
  hook refuses a turn that edited `scripts/` in a project that is not the kit. Rule 15 of
  CLAUDE.md states it.
- The kit gets a licence, GPL-3.0-or-later, and every script starts with the SPDX line, so a
  project with a header rule (FlatCheck rule 18) holds the same bytes.
- The tests of the scripts live in the kit only: `CheckStagedTests` (folders from `kit.json`)
  and `SyncTests`.

## Alternatives rejected

- **Keep per-project edits and merge kit changes by hand** (what FlatCheck ADR 0002 accepted).
  The two lost bug fixes show it does not happen; and every difference found was a value.
- **Environment variables instead of a file.** Not versioned, not visible in a diff, and a
  hook run by git or by Claude Code does not inherit a terminal's variables reliably.
- **A PowerShell module installed once on the machine.** Nothing in the repository would show
  which version a project ran with; a fresh clone or CI runner would have no scripts at all.
- **No exception list, `scripts/` identical whole.** Simpler to state, but `rename.ps1` in a
  project is a loaded gun ("never run it in a project") and a project's `install-hooks.ps1`
  rewrites the shared `.githooks/`; the owner chose the list.
- **Drift as a warning only.** A yellow line is prose with colour; the point of the task is
  that an isolated edit cannot be committed.

## Consequences

After a merge in the kit, every project is red at step 0 until `sync.ps1` runs there: one
extra small commit per project per kit change, the price the owner asked for. A standalone
repository without the kit beside it skips step 0 and syncs by hand. `release.ps1` lost
`-PublishProject`; `mutate.ps1` keeps `-TestProject` as an override of `kit.json`. A new
project folder in FlatCheck goes into `kit.json`, not into three scripts. The skills and the
`docs-auditor` agent still differ per project; that is a separate task. Supersedes nothing in
the kit; FlatCheck ADR 0002 and the second point of ADR 0007 are superseded on its side when
it adopts this. (Restated 2026-09-30: step 0 made ADR 0003's list of steps incomplete, so this
ADR supersedes that list in part; both headers say so since ADR 0024.)
