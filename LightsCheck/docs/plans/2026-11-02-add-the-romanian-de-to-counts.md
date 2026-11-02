# Add the Romanian "de" to counts of 20 and up

## Context

Romanian puts `de` between a count and the word it counts from 20 up, unless the count's last
two digits are 01 to 19: `20 de cadre`, `101 cadre`, `120 de cadre`. LightsCheck's Romanian texts
never wrote it: on the Veil session `lights\ (87 cadre .NEF)`, `din 87 cadre`, `22 cadre slabe`,
`din 39 perechi de cadre`, `22 ori`, and `dara pe RA (-90 grade)` on every trailed frame.
DarksCheck fixed the same on 2026-11-02 (its ADR 0029, `Ui.RoCount`); the owner asked for it here
after the plain-words tabs ("Da, fa-o aici") and approved the list shown in chat ("Da,
aplica-le"), with V-010's recorded word `22 ori` becoming `22 de ori`. English is unchanged and no
figure moves.

## Branch, numbers

- Branch `claude/Add-the-Romanian-de-to-counts`, worktree
  `LightsCheck/.claude/worktrees/Add-the-Romanian-de-to-counts`, from `main` (29e872a4).
- R-082 and ADR 0037 (main holds R-081 and ADR 0036; checked again before the merge).
- This plan saved as `docs/plans/2026-11-02-add-the-romanian-de-to-counts.md`.

## The rule and where it goes

- `Ui.RoCount(long n)` and `Ui.RoCount(string figure)` in `src/LightsCheck.Core/Language/Ui.cs`,
  beside `T`, as DarksCheck writes them: the figure and a space, with `de ` after it when the
  count is not 0 and its last two digits are 00 or 20 to 99; a figure with decimals or any text
  that is not a whole number takes none (`1.3 ori`, `45.2 grade`, `-`). Units take none (`px`,
  `s`, `min`, `h`, `%`, `ADU`), so they never go through it.
- Through the helpers that already count, so every caller follows: `SeriesReport.Count`,
  `FramesWord`, `TimesWord`, `PairsWord`, `SharingPairsWords`.
- Inline Romanian counts (all `using static LightsCheck.Core.Ui`):
  - `SeriesReport`: the trailed-frames row (`22 de slabe, 17 la limita, 48 de bune`, unread too),
    `primele / ultimele N cadre bune`, the thresholds' counts (`sub N perechi`, `mai putin de N
    cadre`, `mai mult de N cadre la rand`), `N dark-uri`, `N pixeli calzi`, `peste N ori`, the
    dithering counts' `la N cadre`, `CropCaption` (`unghi 86 de grade`), `TrailSuffix` (`dara la
    88 de grade`, `(-90 de grade)`), the trail direction row (`-90 de grade fata de orizontala`),
    the polar row's `la DEC 45.2 grade` (decimals, unchanged).
  - `InventoryReport`: `lights\ (87 de cadre .NEF)`, `din 87 de cadre`.
  - `FrameReport` (`--frame`): `40 de detectate, 40 de luminoase, 0 saturate; 40 de masurate`,
    `unghi 83 de grade`, `la cele mai luminoase 40 de stele`, `40 de stele retinute`.
  - `FrameRun`: `sesiunea are 87 de cadre`.

## Steps

1. Worktree and branch; this plan under `docs/plans/`.
2. `docs/requirements.md`: R-082 (the rule, its exceptions, what it covers); the Romanian quotes
   of R-0xx rows with a count of 20 and up take `de` (`(87 de cadre .NEF)`, `(30 de cadre .NEF)`,
   `(20 de cadre .NEF)`, `(-90 de grade)`, `(-85 de grade)`, `dara la -90 de grade`, `din 87 de
   cadre`, `cu 87 de cadre`, `din 39 de perechi`, `din 29 de perechi`).
3. ADR 0037 "A Romanian count of twenty and up takes de", the rule as DarksCheck's ADR 0029 and
   AstroPhotoAnalyzer's `Fmt.RoCount` write it, LightsCheck's own helper in `Ui`.
4. Test first, failing: `R082_ARomanianCountOfTwentyAndUpTakesDe` (`SeriesTests`): the rule's
   edges through `Ui.RoCount` (0, 1, 19, 20, 99, 100, 101, 119, 120, -90, -5, `2.0`, `-`), and a
   24-frame synthetic series (the eight-frame pattern three times) whose Romanian report holds
   `lights\ (24 de cadre .fit)`, `din 24 de cadre`, `(-90 de grade)` and no count of 20 and up
   without `de` before the words counted (cadre, perechi, miscari, locuri, ori, grade, dark-uri,
   pixeli, stele, slabe, bune, ...), while its English report is unchanged in kind
   (`24 frames`).
5. The code: `Ui.RoCount` and every place above; the doc comments that quote such counts follow.
6. Tests that quote an old count follow R-082: `SeriesAnalyzerTests` R033 (`88 de grade`),
   `SeriesTests` R033 and R079 (`-90 de grade`), `AcceptanceTests` `--frame` (`40 de detectate`,
   `40 de stele retinute`). Goldens recaptured: the Romanian `series`, `series-classes`, `frame`
   (and any other that counts 20 and up); every changed line only gains `de`, no figure moves,
   the English goldens do not change.
7. `docs/validation.json` V-010: `22 ori` becomes `22 de ori`, at the owner's word, with a dated
   line in `docs/validation.md` (old word, new word, why).
8. Docs: `language.md` (the rule and the helper), `series-analysis.md` where it quotes such a
   count, CHANGELOG (Fixed), the plan's "What deviated".
9. Build `--no-incremental -warnaserror`, gate, `/validate` (V-010 passes with its new word;
   V-008/V-011 as before), commit, merge proposal.

## Verification

- R082 fails first, passes after; the whole gate green.
- Golden diffs: Romanian lines that only gain `de`; English goldens untouched.
- `/validate`: no figure moves; V-010 passes on `22 de ori`.

## What deviated

- `Ui.RoCount` went in before the test could build, since the test calls it; the test then failed
  on the report (`lights\ (24 de cadre .fit)` missing) and passed once every count went through
  it.
- The goldens that changed are the Romanian `series`, `series-classes` and `frame`; `inventory`
  counts under 20 and stayed, and no English golden changed. Every changed line only gains `de`
  (script over the series goldens; `frame`'s figures checked by hand).
- `FrameReport` writes the `--frame` angle through `Ui.RoCount` in place of `Opt`'s unit suffix,
  so `unghi 83 de grade`, and `-` without an angle as before.
