# 2026-09-25 Adopt the ClaudeCodeStarterKit process

## Goal

Bring FlatCheck onto the process the kit at `E:\Photografie\Code\ClaudeCodeStarterKit`
defines (its `GHID.md`, "Migrarea unui proiect existent"), in the three layers the guide
describes, and bring the documentation to the state it would have had if the process had
been used from the start. Approved by the owner on 2026-09-25 ("Aprob planul. Fa toate
straturile."), with one instruction: no separate commits per layer.

## Measured before planning

| | FlatCheck on 2026-09-25 |
| --- | --- |
| build Release | 20 s, 9 warnings, all in the test projects (8 nullable, 1 xUnit2000) |
| tests | 881 core + 307 WPF, green, 46 s + 1 min 46 s |
| `dotnet format --verify-no-changes` | 12 files with whitespace differences, nothing else |
| CLAUDE.md | 2709 lines |
| `docs/` | 4 evidence notes, no index, no ADRs, no requirements with IDs |
| git hooks | already installed at the root of the `photo` repository (`.githooks/`); they pick up FlatCheck as soon as `scripts/verify.ps1` exists |
| Claude memory | 9 notes about FlatCheck that belong in the repository |

## Decisions (each an ADR, retroactive, accepted with the plan)

1. Layout stays: projects at the folder root, not under `src/` and `tests/`; the kit scripts
   take a list of code folders instead (ADR 0002).
2. Code comments in existing files stay Romanian; process files (docs, scripts, tests of
   invariants) are English (ADR 0006).
3. Format: one whitespace pass on 12 files, then the format step is in the gate; no code
   style analyzers, no `AnalysisLevel`, `Nullable` stays off where it is off (ADR 0003).
4. Validation on the real masters is not in the commit gate (trait `Category=Validation`);
   `/validate` and `/release` run it. Files stay where they are, `FLATCHECK_TESTDATA_LARGE`
   points at them, the manifest holds the hashes (ADR 0004).
5. `crosscheck.py` is not in verify (needs Python); `scripts/crosscheck.ps1` wraps it and the
   skills demand it when a formula changed (ADR 0005).
6. CHANGELOG starts with an empty `[Unreleased]` and a `[2.2.0]` summary; earlier history
   stays in `git log`.
7. CI at the root of the `photo` repository is a separate task.
8. `.github/copilot-instructions.md` (Azure boilerplate from Visual Studio) is deleted.

## Steps

Layer 1, the gate:

1. `scripts/`: `verify.ps1`, `check-staged.ps1`, `check-testdata.ps1`, `mutate.ps1`,
   `release.ps1` (also bumps `flatcheck.py`, publishes console single-file and WPF),
   `crosscheck.ps1`, `hooks/claude-*.ps1`. Not copied: `install-hooks.ps1`, `rename.ps1`,
   the git hooks (the root `.githooks/` copy is the active one).
2. `.claude/settings.json`, six skills, `docs-auditor` agent, adapted to the folders.
3. `CHANGELOG.md`, `global.json`, `.gitattributes`, `.gitignore`, `.editorconfig`,
   `Directory.Packages.props` + lock files, `.config/dotnet-tools.json`, `stryker-config.json`,
   `FlatCheck.Tests/testdata/manifest.json` with the masters and raws hashed.
4. Nine warnings fixed, `dotnet format`, verify green with `-warnaserror`.

Layer 2, the documentation:

5. CLAUDE.md under 200 lines; its content moved unchanged into `docs/design/` with an index
   and a "Read before touching" table.
6. `docs/requirements.md`: the observable behaviour of the seven modes, the window and the
   console contract, with IDs; status `accepted`, `implemented` where an `R###_` test exists.
7. ADRs, retroactive, for the decisions listed in the plan message.
8. `docs/validation.md` + `validation.json` + `ValidationTests` on the real masters.
9. The nine memory notes folded into ADRs, validation and `docs/design/flat-sources.md`;
   README gets a short development section.
10. `/docs-health` run at the end; its report relayed, nothing fixed in that pass.

Layer 3, requirements and tests:

11. `RepoInvariantTests` over the sources of both test projects.
12. The tests CLAUDE.md names as "pinned by" get the `R###_` prefix and their requirements
    move to `implemented`.

## Verification

`scripts/verify.ps1` green; `scripts/crosscheck.ps1` 43/43 (no formula changed, so this is a
no-op check); a probe commit refused by `check-staged.ps1` without a CHANGELOG line;
`release.ps1 -WhatIf` runs its checks.

## What deviated

- `dotnet format` changed 33 files, not 12: the new `.editorconfig` enables `IDE0005`, so unused
  usings went too, and the formatter's default wraps object initialisers one member per line.
  Behaviour-neutral; recorded in ADR 0003 and the CHANGELOG.
- The first batch of `R###_` tests is 64, not the ~24 "pinned by" ones: the tests the design
  notes cite by name were left alone (renaming them would have meant editing the notes), and the
  golden, options, analyzer and session tests that pin each requirement were renamed instead.
  35 of the 47 requirements are `implemented`; the other 12 are `accepted`.
- 40 validation cases, not a handful: every master in the library got a `structure` and a
  `profile` case, since the run costs seconds each and the baseline is the point.
- 41 retroactive ADRs, not ~35; three subagents wrote 0008-0041 from the design notes, one
  wrote `docs/design/flat-sources.md` from the memory notes.
- `check-testdata.ps1 -Update` in Windows PowerShell 5.1 writes a UTF-8 byte order mark, which
  `System.Text.Json` refuses; the script now writes without one. The kit's copy has the same
  behaviour and is not touched here (a kit change is the owner's call).
- The nine memory notes about FlatCheck were reduced to pointers at the repository, with one
  new note saying where everything went.
- Root CI was not added (ADR 0007), as planned. `scripts/release.ps1 -WhatIf` could only be
  exercised up to its dirty-tree refusal, since the migration itself is uncommitted;
  `check-staged.ps1` was probed through a temporary git index so nothing was staged.
