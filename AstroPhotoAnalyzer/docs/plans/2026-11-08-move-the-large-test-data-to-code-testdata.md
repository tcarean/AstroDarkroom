# Move the large test data to code-testdata

Date: 2026-11-08. Branch `claude/Move-large-test-data-to-code-testdata`, worktree
`.claude/worktrees/Move-large-test-data-to-code-testdata` (from `main` at e6cb9601, the kit's rule
"Worktrees"). ADR 0088 records the owner's choice.

## Context

The twelve large test files of AstroPhotoAnalyzer (1.4 GB: `de_printat.png`, nine FITS frames,
the lab's two ICC profiles) sit in the main checkout's git-ignored
`AstroPhotoAnalyzer.Tests/testdata/large/` (ADR 0004). A task's worktree has none of them, so
every `/validate` copies up to 1.4 GB into the worktree first, and `git clean -x` in the main
checkout would delete them. The owner asked on 2026-11-08 to move them to
`E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer`, the place DarksCheck's leak set went the
same day (its ADR 0042), and to update the paths. `manifest.json` and the test data `README.md`
stay in the repository, tracked by git as they are now (my reading of "cred ca ar trebui sa fie
pe git"). Asked how the tests find the new folder, the owner chose **a fixed default path in
the tests** (`TestData.LargeDir()`), the variable `APA_TESTDATA_LARGE` kept as an override; the
kit's `check-testdata.ps1` is not changed, so it checks the hashes only when a run sets the
variable (in `/validate`), and the gate shows the files "absent", as in every worktree today.

## Steps

1. **Worktree.** From `E:\Photografie\Code\AstroPhotoAnalyzer`:
   `git worktree add .claude/worktrees/Move-large-test-data-to-code-testdata -b claude/Move-large-test-data-to-code-testdata main`.
   This plan saved there as `docs/plans/2026-11-08-move-the-large-test-data-to-code-testdata.md`.
2. **Move the files** (same volume, a rename, nothing deleted): the twelve files from
   `E:\Photografie\Code\AstroPhotoAnalyzer\AstroPhotoAnalyzer.Tests\testdata\large\` to
   `E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer\` (empty now); count 12 before and
   after, then remove the empty `large\` folder. Check the hashes:
   `$env:APA_TESTDATA_LARGE='E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer'; scripts/check-testdata.ps1`
   in the worktree, 12 `ok`.
3. **Code (tests only).** `AstroPhotoAnalyzer.Tests/TestData.cs`: `LargeDir()` returns
   `APA_TESTDATA_LARGE` when set, else `E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer`
   (a constant with its comment, ADR 0088); the class and method doc comments say so.
   `StarPairTests.cs` line 23: "the real frames of testdata/large" -> "of the large test data
   folder". No other code reads the folder (`ValidationTests` and `StarPairTests` go through
   `TestData.Large`).
4. **ADR 0088** "The large test data lives in code-testdata, outside the repository":
   the owner's request and choice, the default path in `TestData`, the variable an override,
   `check-testdata.ps1` untouched (the hash check needs the variable; the gate would otherwise
   read 1.4 GB on the HDD each run), alternatives rejected (the variable alone; a kit key
   `testData.largeFolder` synced to nine projects; the copy in `large/`). `Supersedes: 0004 in
   part (the copy in testdata/large/)`; ADR 0004's Status gains
   `; the copy in testdata/large/ superseded by 0088`.
5. **Texts naming `large/`** (paths only, no figure changes):
   - `AstroPhotoAnalyzer.Tests/testdata/README.md` (the "Large, outside git" paragraph and the
     fresh-clone instructions) and `manifest.json`'s `$comment` (the entries and hashes stay).
   - `CLAUDE.md` (the Commands line on the figures, the Workflow line on worktrees: no copy
     needed any more), `kit.json`'s `$comment`, `docs/skills.md` `/validate` step 1 (the folder;
     set the variable for `check-testdata.ps1`; no copy into the worktree).
   - `docs/validation.md` (where the data is, the `NOT MEASURED` sentence, a History line for the
     move: the same bytes, no figure changes), `docs/validation.json`'s `$comment`,
     `docs/design/code-map.md` line 87, `README.md` (the docs list's validation sentence).
   - `.gitignore`: the `large/` line stays, its comment saying it now only keeps a stray copy out.
   - Old plans and other accepted ADRs (0025's "a copy in testdata/large/") are history: left.
6. **CHANGELOG**: one `Internal` line.
7. **Gate and proof**: `scripts/verify.ps1` green in the worktree (`--no-incremental
   -warnaserror` build before the last run). Then, without copying anything into the worktree,
   `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"` in the
   background: every case and `StarPairTests` measured from the new folder (no `NOT MEASURED`),
   all passing with the figures unchanged; the drift table pasted. No formula moved, so this
   is the proof of the move rather than the rule "Formula changes re-validate".
8. **Wrap-up** (`/wrap-up`): read the whole diff, commit on the branch (one line), memory note,
   offer the subagent code review, propose the merge.

## Verification

- `check-testdata.ps1` with the variable set: 12 `ok`; without it: 12 yellow `absent`, exit 0.
- The validation run from the worktree prints `MEASURED` for every case, with paths under
  `E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer`, and every case passes.
- `grep -rn "testdata/large"` in the project: only old plans, ADR 0004/0025 history, the
  CHANGELOG's 1.0.0 history, the kit's own scripts and skill (not ours to edit) and the
  `.gitignore` line remain.

## What deviated

- `docs/design/testing.md` also said `TestData.Large` finds the copies in `testdata/large/`; it
  was updated with the other texts (step 10 of `/wrap-up` names it, as it covers `TestData.cs`).
- The build before the last gate run took `--no-restore` beside `--no-incremental -warnaserror`:
  a fresh restore with `-warnaserror` turns ImageSharp's NU1902/NU1903 advisories into errors
  (ADR 0036).
- The kit's `/validate` skill still says the folder is `<testData.folder>/large/` or the one
  the variable names; that remains true of `check-testdata.ps1`, the kit's, and
  `docs/skills.md` says to set the variable. Not changed.
- Measured: the move kept 12 files and 1 368 MB, `check-testdata.ps1` with the variable gave
  twelve `ok`; from the worktree, with no copy and the variable unset, `Category=Validation`
  ran 27 tests, all passed, none `NOT MEASURED` (2.9 min); the gate was green (544 + 140 tests),
  the twelve files listed `absent` in yellow as planned.
