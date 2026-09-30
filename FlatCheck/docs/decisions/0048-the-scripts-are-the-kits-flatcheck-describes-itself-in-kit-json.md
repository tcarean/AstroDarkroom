# 0048. scripts/ is the kit's, byte for byte; FlatCheck describes itself in kit.json

Date: 2026-09-26
Status: accepted 2026-09-26; FlatCheck's CheckStagedTests superseded by 0053
Supersedes: 0002

## Context

ADR 0002 accepted that three of the kit's scripts carry FlatCheck's folder lists and that "a
future kit change to those scripts is merged by hand". On 2026-09-26 the diff between the kit's
`scripts/` and FlatCheck's showed six of eight copied scripts edited, all for values (solution
name, test filter, folder lists, test data folder and variable, Stryker's project, two publish
targets), and two bug fixes in `check-testdata.ps1` (manifest without a byte order mark and with
LF; `/` in a name) that never reached the kit. The owner decided that every project uses the
same `scripts/`, with the differences in a JSON file, and that a script is fixed in the kit and
copied to every project, never in one project alone. The kit implements it in ADR 0017:
`kit.json`, `scripts/kit-config.ps1`, `scripts/sync.ps1`, drift red at step 0 of `verify.ps1`,
the Stop hook refusing a turn that edited `scripts/` outside the kit.

## Decision

FlatCheck's `scripts/` is taken from the kit with `scripts/sync.ps1` and never edited here. What
ADR 0002 put into the scripts is now in `kit.json`: the three code folders and the two test
folders (the layout itself stays at the folder root, as ADR 0002 decided), the
`Category!=Validation` filter of ADR 0004, `FlatCheck.Tests/testdata/` with
`FLATCHECK_TESTDATA_LARGE`, `FlatCheck.Tests/` for Stryker, and the two publish entries of the
release: the console as a self-contained single file with the native libraries extracted, under
`console/`, and the window under `wpf/`. `"kit": "../ClaudeCodeStarterKit"` names the kit;
the path holds in an app worktree too, because a worktree of `photo` holds the whole repository.
The second point of ADR 0007 (`install-hooks.ps1`, `rename.ps1`, `pre-commit` and `commit-msg`
are not copied) stays true, now as `sync.ps1`'s exception list rather than a choice made here.
Rule 18 is unchanged: the kit's scripts carry the same `SPDX-License-Identifier:
GPL-3.0-or-later` line, so the copies are identical whatever the header rule says.

## Alternatives rejected

- **Keep the edited copies and merge kit changes by hand** (ADR 0002). The two lost bug fixes
  are the measurement; and every difference was a value, so nothing needed a script of its own.
- **Move the projects under `src/` and `tests/` to match the kit's defaults.** Rejected in ADR
  0002 for the churn; now unnecessary, the lists are five lines of JSON.

## Consequences

After a change in the kit, FlatCheck's next commit is red at step 0 until `scripts/sync.ps1`
runs here: one small commit per kit change. A new project folder in FlatCheck goes into
`kit.json`'s `codeFolders` or `testFolders` and into `LicenseHeaderTests.Projects`, nowhere
else. `release.ps1 -PublishProject` is gone; the publish list is in `kit.json`. The scripts'
tests live in the kit (`CheckStagedTests`, `SyncTests`); FlatCheck's own `CheckStagedTests`
copies the real `kit.json` beside the script and checks FlatCheck's configuration: `FlatCheck.Core/`
is code, `CLAUDE.md` is not. The six
skills and the `docs-auditor` agent still differ from the kit's; that is a later task.
