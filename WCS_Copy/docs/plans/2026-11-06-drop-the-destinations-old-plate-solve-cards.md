# Drop the destination's old plate-solve cards before a copy

Plan for the task branch `claude/Drop-the-old-plate-solve-cards`, worktree
`.claude/worktrees/Drop-the-old-plate-solve-cards` (the project is `WCS_Copy\` inside it).
Approved by the owner in plan mode on 2026-11-06, after the owner chose option (b) of the three
put to them the same day.

## Context

`WcsCopy.Run` copies each selected key with `FitsHeader.SetCard`, which replaces a card of the
same keyword or appends one. It never removes a card. So when the destination already holds a
plate solve of its own, the cards of it that the source lacks stay beside the copied ones.

Measured on 2026-11-06, read-only:

- **Siril 1.4.4 reads `CD` over `PC` + `CDELT`.** A 200 x 100 test file held both: PC = identity
  at 0.001 deg/px, and CD = 0.002 deg/px turned 30 deg. Siril loaded and saved it (`siril-cli`
  with `-i` on an empty private settings file). The saved file holds CDELT 0.002 and a PC turned
  30 deg, and Siril dropped the CD cards on save. astropy 8.0.1 (WCSLIB) reads PC from the same
  file, with no warning.
- **In the owner's data**: 3,884 FITS files under `E:\Photografie` (outside `Code`), 231 of them
  plate-solved.
  - 222 hold PC + CDELT only.
  - 5 hold CD only (Siril 0.99.8.1).
  - 4 hold PC + CDELT + CD that disagree. In those, CD1_2 and CD2_2 have the opposite sign, so
    the CD is the PC solve flipped top to bottom. These are the `*_syqon_studio_in.fit` outputs
    of the SyQon CLI (`SYQON_WCS_ROW_TRANSFORM = 'FITS_BOTTOM_UP_TO_TOP_DOWN'`).
  - No CROTA, and no SIP term above `A_ORDER`.

So a copy into any of those 9 files leaves cards that Siril, the program the user checks with,
reads in place of the copied ones. The owner chose, on 2026-11-06, to drop them (option b).

## The behaviour (R-016, new)

When the source has `CTYPE1`, the copy first drops from the destination every card that meets
both conditions:
- its keyword is one of the 30 `WcsMetadataKeys.WcsKeys` or a SIP coefficient (`A|B|AP|BP_i_j`);
- the source does not have that keyword.

The target then holds the source's plate solve and no other.

- The optical keys are never dropped.
- Nothing is dropped when the source has no `CTYPE1`: the warning of R-006 stands, and the
  destination's own plate solve stays.
- A refusal drops nothing.
- A preview names the keys it would drop, and so does a copy. In place, they leave the
  destination. With `-o`, they are not carried into the new file, and the destination stays
  untouched as before.

The core's lines, one per run, only when something is dropped (Romanian without diacritics):

| When | English | Romanian |
| --- | --- | --- |
| preview, printed after the cards | `Old plate-solve keys of the destination that would be dropped (4): CD1_1, CD1_2, CD2_1, CD2_2` | `Chei vechi de plate solve ale destinatiei care ar fi eliminate (4): CD1_1, CD1_2, CD2_1, CD2_2` |
| copy, printed before `Copied N keys...` and the reminder | `Dropped 4 old plate-solve keys of the destination: CD1_1, CD1_2, CD2_1, CD2_2.` | `Eliminate 4 chei vechi de plate solve ale destinatiei: CD1_1, CD1_2, CD2_1, CD2_2.` |

R-012's last two lines stay the last two lines.

## The code

- `src/CopyWcs.Core/WcsMetadataKeys.cs`: add `SelectKeysToDrop(FitsHeader source, FitsHeader dest)`.
  It returns the destination's keywords that are WCS keys or match `SipCoefficient`, that the
  source lacks, distinct and in the destination's order. It is pure; the `CTYPE1` rule lives in
  the run.
- `src/CopyWcs.Core/FitsHeader.cs`: add `RemoveCards(string key)`, which removes every card of
  that keyword and returns the removed cards, raw, so each can be logged.
- `src/CopyWcs.Core/WcsCopy.cs`:
  - In the `select` stage, compute the keys to drop when `source.Contains("CTYPE1")`.
  - Log them at Debug, one line per card: `drop CD1_1: <the destination's card>` through
    `LogText.Card`, in a dry run too.
  - In the `write` stage, remove them before the `SetCard` loop.
  - `WcsCopyOutcome` gains `string? Dropped`, the worded line above, null when there is none or
    the run was refused.
  - The done line's counts become `(new a, replaced r, identical i, dropped d)`, always with all
    four. The version stays 1.0.0 until the owner bumps it, so `dropped 0` is what tells a log of
    this build from an older one.
- `src/CopyWcs.Cli/CopyWcsCli.cs`: a preview prints `Dropped` after the cards. A copy prints it
  before its notes.
- `src/CopyWcs.Wpf/ViewModels/MainViewModel.cs`: `Notes` becomes the core's `Dropped` line (when
  there is one) followed by the copy's notes. A preview therefore shows the line under its cards.
  The XAML does not change, and the window composes no sentence (ADR 0007).

## Requirements first (`docs/requirements.md`)

- Add R-016 as above, with the example: a destination with `CD1_1` to `CD2_2` and a source with
  `CDELT` and `PC` writes a target with no `CD` card and shows the line.
- R-009: the dry run prints the line of R-016 after the cards, when there is one.
- R-031: the window shows that line under the cards.
- R-032: the window shows it before the two closing lines.
- R-042: the `drop` lines at Debug, and the done line's `dropped` count, with the example updated
  to `(new 14, replaced 1, identical 0, dropped 0)`.

## Tests (each fails first)

- `WcsMetadataKeysTests`:
  - `R016_SelectKeysToDrop_...`: the CD matrix, `CROTA2` and `A_3_0` are dropped; a key the
    source has, and an optical key such as `OBJECT`, are not; the destination's order is kept.
- `WcsCopyTests`:
  - `R016_Run_Copy_...`: the target holds no CD, CROTA or extra SIP card; `Dropped` comes before
    the count.
  - `R016_Run_DryRun_...`: the run names the keys and writes nothing.
  - `R016_Run_SourceWithoutCtype1_DropsNothing`.
  - `R016_Run_NothingToDrop_HasNoLine`.
- `CopyWcsCliTests`:
  - `R016_...`: the preview's line comes after the cards; the copy's line comes before the last
    two lines; both are checked in English too.
- `LanguageTests`:
  - Add the scenarios `drop` and `drop-dry-run`.
  - Add the Romanian words `Chei vechi`, `Eliminate`, `eliminate` and `destinatiei`.
- `LoggingTests`:
  - `R042_ADroppedKeyIsLoggedWithTheDestinationsCard`.
  - The two done-line regexes gain `, dropped 0`, as R-042's new text says.
- `MainViewModelTests`:
  - `R016_...`: the preview's `Notes` and the copy's `Notes` equal the console's lines for the
    same pair.
- `ValidationTests`:
  - Add **V-002** on real data. A new fixture holds the 4 header blocks (11,520 bytes, no image)
    of `E:\Photografie\Astro\Veil\2026-10-06-3h\procesate\bge-cc\denoised_syqon_studio_in.fit`,
    the SyQon output of the same Veil stack as V-001's fixture: 5601 x 3692, PC + CD, no site
    cards.
  - Copying V-001's solve into it drops exactly `CD1_1, CD1_2, CD2_1, CD2_2`, copies 72 keys and
    leaves no CD card.
  - Record it in `validation.json` (a case with a `destination`) and in `validation.md`. Add the
    fixture to the manifest with `scripts/check-testdata.ps1 -Update`. The R-024 reader skips a
    case without `keysWithOptical`.

## Documentation

- ADR `0011-a-copy-drops-the-destinations-old-plate-solve-cards-the-source-lacks.md`:
  - Context: the measurements above.
  - Rejected: keep and document; warn only; drop only the conflicting form.
- Design notes:
  - `fits-header.md`: `RemoveCards`, and the selector in "The key lists".
  - `run-and-console.md`: the step, the outcome's field and the print order.
  - `window.md`: `Notes`.
  - `logging.md`: the `drop` lines and the count.
- `README.md` "What is copied": the drop, why (Siril reads CD first), what is never dropped, and
  what the dry run shows.
- `CHANGELOG.md` `[Unreleased]` Fixed: one line.
- This plan, with an Outcome section at the end.

## Steps

1. Create the worktree: `git worktree add .claude/worktrees/Drop-the-old-plate-solve-cards -b
   claude/Drop-the-old-plate-solve-cards main`, from `E:\Photografie\Code\WCS_Copy`. The project
   is `<worktree>\WCS_Copy`. Never build in the main checkout.
2. Save the plan. Write R-016 as `draft`, together with the edits to R-009, R-031, R-032 and R-042.
3. Write the tests and watch them fail. Write the code. Mark R-016 `implemented`.
4. Write the ADR, the notes, README, CHANGELOG, V-002 and the manifest.
5. Run `dotnet build -c Release --no-incremental -warnaserror`, then `scripts/verify.ps1`. When
   both are green, commit on the branch.
6. Have a subagent review the diff. Fix what holds up, commit, and have the fix commit reviewed.
   Then propose the merge (`--no-ff`, after checking that ADR 0011 is still free on `main`).
7. After the merge, update the wiki box "A destination that is already plate-solved" in
   `E:\Photografie\AstroDarkroomWiki\CopyWcs.md` on a `claude/` branch of the wiki, and ask
   before pushing it.

## Verification

- The gate is green, and the new R016 tests fail before the code and pass after it.
- An end-to-end check on the real pair, with the worktree's Release console:
  - `copywcs <Veil result_final.fit> <procesate\bge-cc\denoised_syqon_studio_in.fit> --dry-run`
    lists 72 keys and the line naming `CD1_1, CD1_2, CD2_1, CD2_2`. It writes nothing.
  - `-o` into the scratchpad gives a header with no CD card.
  - `siril-cli` with `-i` on an empty private ini loads and saves that output. Its CDELT and PC
    then equal the copied ones, so Siril reads the copied solve.
- Nothing touches the owner's files: a dry run and a copy into the scratchpad only.
