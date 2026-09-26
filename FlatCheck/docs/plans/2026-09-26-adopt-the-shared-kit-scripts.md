# 2026-09-26 Adopt the shared kit scripts: kit.json in, per-project script edits out

## Context

The kit's plan `docs/plans/2026-09-26-parametrize-the-kit-scripts-with-kit-json.md` (kit ADR
0017, merged as f61f2d4) makes `scripts/` byte-identical in the kit and in every project, with
the project's values in `kit.json`. FlatCheck's copies edited six of eight scripts (ADR 0002:
the folder lists; the test filter; the test data folder and variable; the two publish targets)
and left out `install-hooks.ps1`, `rename.ps1` and the git hooks (ADR 0007). This is session 2
of that plan, in FlatCheck, at the owner's instruction, in this same session.

## Steps

1. `kit.json` with FlatCheck's values: `FlatCheck.sln`; `Category!=Validation`; the three code
   folders and the two test folders; `FlatCheck.Tests/testdata/` and `FLATCHECK_TESTDATA_LARGE`;
   `FlatCheck.Tests/` for Stryker; the console (single file, self-contained, native libraries
   extracted) and the window, each to its own subfolder; `"kit": "../ClaudeCodeStarterKit"`.
2. `scripts/sync.ps1` and `kit-config.ps1` copied from the kit, then `sync.ps1` run here, then
   `sync.ps1 -Check` exit 0.
3. `scripts/verify.ps1` green: the proof that the parameters cover everything the edited copies
   did (the filter, the folders, the publish list are read from `kit.json` from now on).
4. ADR 0048: supersedes ADR 0002 (the scripts no longer carry the lists) and amends the second
   point of ADR 0007 (the four kit-only files are now `sync.ps1`'s exception list, not a copying
   choice). ADR 0002 marked superseded.
5. `CLAUDE.md`: `kit.json` in Layout, `sync.ps1` in Commands, the rule that `scripts/` is the
   kit's; rule 18 unchanged (the kit's scripts carry the same SPDX line). CHANGELOG `Internal`
   line; `README.md` and `FlatCheck.Tests/testdata/README.md` still true, checked.

## Verification

`scripts/sync.ps1 -Check` exit 0; `scripts/verify.ps1` green with the kit's scripts;
`release.ps1 -Version 9.9.9 -WhatIf` cannot run on a dirty tree, so the publish list is
checked by reading `kit.json` against the old `release.ps1` diff.

## What deviated

- FlatCheck has its own `CheckStagedTests` (missed when planning; the kit's plan said the
  scripts' tests live only in the kit). It copied the script alone into a throwaway repository,
  so two of its four cases failed at the first verify: the script now needs `kit-config.ps1` and
  a `kit.json` beside it. The fixture copies both, with FlatCheck's real `kit.json`, and the
  class now checks FlatCheck's configuration rather than the script. ADR 0048 says so.
- The README's process section got one bullet on the rule; the plan had listed the README only
  as "checked".
