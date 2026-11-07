# Default the SynScan arrows to the Star Adventurer GTi with SynScan Pro

Approved in plan mode on 2026-11-07, with the texts below as recommended (all of them). The
meridian question was left unanswered, so the docs say only that the arrows were tested on
that pair.

## Context

On 2026-11-07 the owner tested the SynScan Pro app driving their Star Adventurer GTi: `→` moves
the mount east, `←` west, `↓` north, `↑` south. LightsCheck's default since ADR 0014
(2026-10-10) is `→` east and `↑` north, read off the Veil session, never tested: east is now
confirmed, north is the other way round. The owner asks for the tested arrows as the default,
still configurable (`--east`/`--north`, the EAST/NORTH selectors), and documented as the arrows
of this mount with this app.

What the user sees change: under the default keys every signed DEC cell swaps its arrow,
`DEC 12 px ↓ S` becomes `DEC 12 px ↑ S` and `↑ N` becomes `↓ N`. The sense letters (from the
plate solve) and every figure stay. The owner's own window already stores `north = ↓`
(`%LOCALAPPDATA%\LightsCheck\settings.json`, written today), so it shows what it showed.

Branch `claude/Default-the-arrows-to-GTi-SynScan-Pro`, worktree
`LightsCheck/.claude/worktrees/Default-the-arrows-to-GTi-SynScan-Pro`; this plan saved as
`docs/plans/2026-11-07-default-the-arrows-to-the-gti-with-synscan-pro.md`.

## Decisions (ADR 0041, superseding ADR 0014 in part: the default north arrow)

- `ArrowKeys.Default` (`src/LightsCheck.Core/Runs/SeriesRun.cs`) = right east, down north.
- `--help` takes its default words from `ArrowKeys.Default`, as the window's tips already take
  the symbols (`MainViewModel.EastTip`/`NorthTip`). Today the help's `(default up)` is a literal:
  a change of the core alone would leave it wrong and no test would fail. A small table in
  `Options.cs` gives each key's option word in both languages (`down`/`jos`); the parser stays as
  it is.

## Texts you read: original and proposal (plain-words rule; made only on your yes)

| Where | Now | Proposed |
| --- | --- | --- |
| EAST tip, end | `Default: → east.` / `Implicit: → est.` | `Default: → east, as on a Star Adventurer GTi with the SynScan Pro app.` / `Implicit: → est, ca pe un Star Adventurer GTi cu aplicatia SynScan Pro.` |
| NORTH tip, end | `Default: ↑ north.` / `Implicit: ↑ nord.` | `Default: ↓ north, as on a Star Adventurer GTi with the SynScan Pro app.` / `Implicit: ↓ nord, ca pe un Star Adventurer GTi cu aplicatia SynScan Pro.` |
| `--help`, north line | `(default up)` / `(implicit sus)` | `(default down)` / `(implicit jos)` |
| `--help`, new line under the two arrow options | (none) | `The defaults are those of a Star Adventurer GTi with the SynScan Pro app.` / `Implicit, sagetile sunt cele ale unui Star Adventurer GTi cu aplicatia SynScan Pro.` |
| README, the arrows paragraph | `Which arrow moves the mount east and which north depends on the settings of the SynScan app: by default → is east (so ← is west) and ↑ north (so ↓ is south). Change them with --east and --north, or with the EAST and NORTH selectors of the window.` | `Which arrow moves the mount east and which north depends on the mount and the app. The defaults are those of a Star Adventurer GTi driven by the SynScan Pro app, tested on that pair: → moves the mount east and ← west, ↓ moves it north and ↑ south. With another mount or app, press one arrow alone between two frames of a session with a plate-solved FITS at the root: the next frame's cell gives the sense on the sky, which comes from the solve, not from the arrows. Change them with --east and --north, or with the EAST and NORTH selectors of the window.` |
| Wiki `LightsCheck.md`, EAST/NORTH bullet | `The default is → east and ↑ north, so ← is west and ↓ south.` | `The defaults are those of a Star Adventurer GTi driven by the SynScan Pro app: → east and ↓ north, so ← is west and ↑ south.` |

Recommendation: all of them. The README also changes three values only (`right and up` to
`right and down`, `→ and ↑` to `→ and ↓`, the example `DEC 29 px ↓ S` to `↑ S`).

One question, not blocking: on a German equatorial mount the DEC arrows can swap north and south
after a meridian flip (the DEC axis turns over with the camera), and I do not know whether
SynScan Pro makes up for it. If your test was on one side of the meridian only and you know
which, tell me and the README says so; otherwise the docs say only "tested on that pair"
(recommended unless you know).

## Steps: requirements, then tests, then code

1. `docs/requirements.md`: R-040 (the default is the pair's tested arrows, ADR 0041; examples
   `RA 15 px ← V, DEC 12 px ↑ S`, CSV `←, ↑`, Veil frame 17 `DEC 28 px ↑ S`; `--help` reads its
   default words from the core and names the pair), R-038 (the cell example; "the nine DEC jumps
   ... all read `↑ S`"), R-039 (frame 3 `DEC 12 px ↑ S`), R-041 (`NORD` (`↓` or `↑`)), R-083 (the
   arrow tips name the pair).
2. Tests first, red against today's code:
   - `SeriesAnalyzerTests`: R040 default right/down, south = up; its "swapped" keys become
     left/up so both still differ from the default; the R038 cells under the default.
   - `SeriesTests`: R038 frame cells `↑ S`; R040 default run `↑ S`, swapped run
     `--est stanga --nord sus` reads `→ V` and `↓ S`; the help holds `(implicit jos)` and the line.
   - `LanguageTests` R064: the English help holds `(default down)`.
   - `MainViewModelTests` R041: a fresh window `↓`, choices `↓, ↑`, the stored non-default `↑`,
     file `{"east":"←","north":"↑"}`, a broken file gives `↓`.
   - `WindowLanguageTests` R083: the north tip holds `↓`; both arrow tips name the pair.
3. Code: `SeriesRun.cs` (the default and its comment), `Options.cs` (the word table),
   `Program.cs` (both usage texts read it, plus the new line), `WindowTexts.cs` (the two tips).
   The view-model needs nothing: its choices and fallback already read `ArrowKeys.Default`.
4. Goldens `help.txt` and `en_help.txt` regenerated; the expected diff is the north line's
   default word and the one new line, nothing else; read and reported at wrap-up.
5. Docs: ADR 0041; ADR 0014's Status `accepted 2026-10-10; the default north arrow superseded by
   0041`; the design notes `series-analysis.md` (the default, the example) and
   `session-and-console.md` (the default, the help's words from the core); README; CHANGELOG
   `[Unreleased]`, Changed.
6. Validation. V-011's cells hold the default arrows (frames 3, 5, 17, 19 and 80 have a DEC
   part), and the Veil root has had no solved FITS since 2026-10-27, so V-011 fails as recorded.
   Re-measure it on a scratch mirror: junctions to the session's `lights\` and `biases\` and a
   copy of `procesate\result.fit` at the mirror's root, `LIGHTSCHECK_TESTDATA_LARGE` pointing at
   the mirror; nothing is written into the session folder. Run V-011 before the code change
   (expected to pass with today's cells, showing the mirror reproduces the case) and after it
   with the new cells `DEC 13 px ↑ S`, `RA 4 px ← V, DEC 8 px ↓ N`, `DEC 28 px ↑ S`,
   `RA 10 px ← V, DEC 26 px ↑ S`, `RA 4 px → E, DEC 12 px ↑ S`; the pixels, the senses and the
   four counts (11 S, 2 N, 11 E, 24 W) stay. A History line in `docs/validation.md`, V-011's row
   updated, the junctions removed with `rmdir` (never a recursive delete). No other case reads
   the arrows and no formula or threshold moves, so the other cases are not re-run.
7. Gate: a `--no-incremental -warnaserror` build, then `scripts/verify.ps1`; read the whole diff,
   commit on the branch, a review subagent, then the merge proposal.
8. After the merge: the wiki bullet above on a wiki `claude/` branch, then the fast-forward into
   `master` and the push, asked first.

Out of scope, proposed as its own task at the end: `DitherCalculator/CLAUDE.md` still says `↑`
north and "`→` east unverified"; your test settles both.

## Verification

- Gate green: unit, acceptance, goldens, the window's tests.
- V-011 on the mirror passes before (today's cells) and after (the new cells); output pasted.
- The window's NORTH selector starts at `↓` without a settings file (R041's test); the synthetic
  console run reads `DEC 12 px ↑ S` by default and `↓ S` with `--north up` (R040's test).

## What deviated

- The mirror's solved FITS. `procesate
esult.fit`, which `docs/validation.md` names since
  2026-11-02 as the solved stack moved off the root, carries no WCS (no `CTYPE1`, no `PC`
  matrix): a first "before" run with it failed as V-011 has since 2026-10-27 (`frame 2: cell
  'RA 5 px', expected 'RA 5 px ← V'`). The plate-solved stack is
  `procesatege-cc
esult-bge-cc.fit`, the file ADR 0013 read; with it the mirror reproduced
  V-011 as recorded.
- One more test pinned the old default and was missed in step 2: R-071's log test reads the
  series start line `east=Right north=Up`; the first gate run failed on it alone (406 + 120
  other tests green), and it now reads `north=Down`.
- R-039's example cell is pinned by the R-038 frame-table test in `SeriesTests`; the window's
  R039 test reads a session without a solve and needed no change.
- The design notes `window.md` and `language.md`, which cover touched tests and
  `WindowTexts.cs`, needed no change: neither describes the tips' words nor the arrows' default.
