# 0042. The leak set moves to code-testdata, and V-011 leaves the validation

Date: 2026-11-08
Status: accepted 2026-11-08

## Context

On 2026-11-08 the owner moved two sets out of the darks library into
`Flats and Darks/Darks/stricate/` (broken): the deliberate leak set of 2026-10-20 (V-008, 30 darks,
30 biases and 10 frames set aside, 1.3 GB) and the 2 °C darks of 2026-11-07 (V-011, the one
series that reads `REFA`, ADR 0038). Both validation cases then printed `nothing checked`. The
owner first asked to remove them from the validation; told that V-008 is the only real set with
light in the frames and V-011 the only real `REFA`, the owner chose to keep them under
`stricate/`, then changed their mind: keep the leak set, moved to a test-data folder, and drop V-011.
Asked where, the owner answered `Astro\code-testdata\`.

## Decision

1. **A real set kept for the tests alone sits in `E:\Photografie\Astro\code-testdata\`**, under
   the folder `DARKSCHECK_TESTDATA_LARGE` names, so a case names it like any session
   (`code-testdata/Lightleaks`) and no code changes. The leak set moved there whole, its
   `darks\`, `biases\`, `aruncate\` and `conditii.txt` (71 files before and after).
2. **V-011 leaves `validation.json`.** Its folder stays in `stricate/`; its figures of
   2026-11-07 stay in `docs/validation.md`'s History, and R-023 keeps them as its example of
   `REFA`. No real `REFA` set is validated now; the synthetic tests of R-023 still hold the verdict.
3. **V-008's figures do not change**: the frames are the same files.

## Alternatives rejected

- **`tests/testdata/large/` in the repository.** It is ignored by git, so a task's worktree,
  where the validation runs, does not have it; the validation reads one folder,
  `E:\Photografie\Astro`, so a second one would need a code change; and `git clean -x` would
  delete 1.3 GB of the owner's frames.
- **The frames in git.** The `photo` repository is public on GitHub, and the raws are gigabytes.
- **V-008 and V-011 left under `stricate/`**: the owner's first answer, changed the same day.

## Consequences

- `validation.json` holds eleven cases, V-001 to V-010 and V-012; `validation.md` drops V-011's
  row and records the move; `tests/testdata/README.md` names `code-testdata\`.
- ADR 0038's consequence that V-011 is the one real `REFA` case gets a dated note.
