# DitherCalculator

`dither_calculator.py` tells the owner how long to hold a SynScan arrow so that the image moves
the wanted number of pixels: manual dithering between exposures on a Sky-Watcher Star Adventurer
GTi driven from the SynScan app. It asks for the focal length, the pixel size, the SynScan speed
step (0-8, the number the app shows between the arrows), the target's declination and the wanted
dither, and prints the image scale, the image speed on each axis, the press time on each axis
and the RA press times for declinations 0 to 75. The defaults are the owner's Nikon D3300
(3.92 µm) on the Samyang 135 mm, at step 1.

One interactive console script in Romanian, Python 3 standard library only (`math`, `sys`). Not
a kit project: no `scripts/verify.ps1`, no tests, no `docs/`, no `.claude/settings.json`, so no
gate and no Claude Code hook runs here. The rules below are kept by hand.

## Commands

- Run: `python dither_calculator.py`. Enter keeps the value in brackets; `d` at "Alt calcul?"
  starts another round whose brackets hold the last round's values.
- Python on this machine is 3.14 (`C:\Users\User\AppData\Local\Python\pythoncore-3.14-64`). The
  script needs 3.7 or later (`sys.stdout.reconfigure`).
- Check after every change; there are no tests. Run it from Git Bash (the Bash tool), whose pipes
  are cp1252, the case the encoding fix is about. The PowerShell tool sets
  `PYTHONIOENCODING=utf-8:surrogateescape` for its own shells and hides that crash.

  ```bash
  printf '\n\n\n41.269\n\nd\n\ninf\n\n2\n89.95\n8\nd\n0.1\n\n0.0001\n9\n0\n-0\n\nd\n135\n\n\n\n1\nn\n' | python dither_calculator.py
  ```

  Expected, in order:
  - round 1, the defaults at dec 41.269: `Scala imaginii: 5,99"/pixel`,
    `Deplasare la treapta 1 (1x sideral):`, `Dec: 2,51 px/s`, `RA (la dec 41,269°): 1,89 px/s`
    with one space after the colon, `Dec:  6,0 s` with the backlash note that names
    `Settings > Backlash`, `RA:   7,9 s`, the table 6,0 / 6,2 / 6,9 / 8,4 / 11,9 / 23,1 s, and
    the closing test `~19 px`;
  - round 2: the declination prompt shows `[41,269]`; `inf` gets `Nu e un număr valid`; step 2
    prints `8x sideral`; at dec 89.95 `Dec:  0,4 s`,
    `RA:   456,3 s - prea mult, urcă la treapta 3 (16x) sau folosește Dec` followed by
    `(spre est, peste 1x motorul se inversează: + backlash)`, the under-1-s warning ending
    `Coboară la treapta 1 (1x).`, and a closing test that asks for a target farther from the pole;
  - round 3, focal 0.1 (an absurd scale, 8085,59"/px, to reach the path where an axis does not
    move): `0.0001` and `9` each get
    `Treapta trebuie să fie un număr întreg de la 0 la 8 (9 = Max nu are o viteză fixă).`;
    step 0 prints `0,5x sideral`; dec -0 prints `RA (la dec 0°)`;
    `Dec:  nu mișcă practic imaginea - urcă la treapta 1 (1x)`, with no "folosește Dec";
  - round 4, step 0 and dither 1: `Dec:  0,8 s` and the warning ending
    `Treapta 0 e cea mai mică; alege un dither mai mare.`; the closing test `~25 px`;
  - exit code 0, no traceback.
- Windows PowerShell 5.1 puts a BOM in front of the text it pipes. Check that the first answer
  still counts (PowerShell tool):

  ```powershell
  "135`n3.92`n1`n0`n15`nn`n" | python dither_calculator.py
  ```

  Expected `Scala imaginii: 5,99"/pixel`; `52,62` means the BOM spoiled the first answer and every
  later answer moved up one prompt.

## The model

- Sidereal rate `VITEZA_SIDERALA = 360 * 3600 / 86164.0905` = 15.041 "/s, one turn per sidereal
  day.
- Speed: the step the app shows, mapped by `TREPTE_SYNSCAN = (0.5, 1, 8, 16, 32, 64, 128, 400,
  600)` to a multiple of sidereal. Step 9 (Max) has no fixed multiple and is refused.
- Image scale `206.265 * pixel_um / focala_mm` in "/px: 5.99 for the defaults.
- DEC: px/s = multiple x 15.041 / scale (2.51 px/s for the defaults at step 1).
- RA: the same times cos(dec). Turning the RA axis by an angle moves the sky by that angle times
  cos(dec): the same for -dec, zero at the poles.
- Seconds = dither px / px/s. `secunde_pentru` returns `None` under 1e-3 px/s (the axis does not
  move the image); `text_secunde` adds "prea mult" above `SECUNDE_MAX` (60 s). The advice then
  names the next step up (`sfat_dec`; `sfat_ra` adds Dec as the way out); a press under 1 s
  names the next step down, because the app's Wi-Fi latency is then a large part of the press.

## What the manual says, and what the script still assumes

Source: SynScan App User's Manual, 2023-05-25 (Sky-Watcher; the owner's copy is
`synscan_app_manual_en_20230525.pdf`).

1. Documented (p. 8, "Slewing the mount"): steps 0 to 9 are 0.5x, 1x, 8x, 16x, 32x, 64x, 128x,
   400x, 600x and Max, in multiples of the sidereal rate, and the app shows the step's number at
   the center of the direction buttons. `TREPTE_SYNSCAN` is that table.
2. Assumed, not in the manual: an arrow moves the mount at that speed relative to the sky, added
   to or taken from the tracking. At 1x east stops the RA motor and west doubles it, so the image
   moves at 1x either way and nothing reverses on RA. Above 1x east reverses the RA motor, so RA
   also loses time to backlash; the RA backlash note shows only above 1x. The closing note's test
   checks this at step 1: 10 s east and 10 s west should each move a star by the printed pixels.
   If west moves nothing and east twice as much, the app sets the axis speed instead, and the RA
   texts change.
3. DEC backlash is large on this mount: on the Veil session of 2026-10-06, 47 pauses gave only 38
   visible moves (LightsCheck, `docs/decisions/0007`), and a pattern that reverses DEC every time
   moves less than planned. The manual (pp. 21-22) describes Settings > Backlash: when an axis
   reverses, the app first turns it quickly by the set amount to take up the slack. With it set
   right, the printed press times hold after a reversal too.

LightsCheck's dithering table measures each step between frames, in pixels and per axis, from the
lights. A session with known press times read there is another way to check 2.

## Facts from the rest of the repo, for when the script grows

- On the owner's SynScan `→` moves the mount east and `↑` north (LightsCheck
  `docs/decisions/0014`; a forum default with left = east was wrong for this app).
- 10-20 px is a DSLR rule of thumb, not a measurement. LightsCheck's walking-noise verdict uses
  the same kind of threshold: 10 px or more, random, on both axes (its `docs/decisions/0007`).

## Decisions

- 2026-10-11, the owner: the speed is asked as the step the app shows (0-8), not as a multiple.
  The app shows the step, and step 2 is 8x: typing 2 for step 2 gave a press four times too long.
  The advice names the step to move to.
- 2026-10-11, the owner: the Dec backlash note says that SynScan can compensate it (Settings >
  Backlash), after the Veil DEC presses that moved nothing.

## Invariants (each one fixed a bug reproduced on 2026-10-11)

- `sys.stdout.reconfigure(encoding="utf-8")` is the first statement of `main()`. Without a
  console (a pipe, an editor's run pane) Windows writes cp1252 and the first `ă` raised
  `UnicodeEncodeError`.
- `citeste_numar` removes `chr(0xFEFF)` from every answer: the BOM of a PowerShell 5.1 pipe made
  the first answer invalid and shifted every later one. Keep it written as `chr(0xFEFF)`, never
  as the invisible character itself; an edit once wrote the bare character into the source.
- A non-finite answer (`inf`, `nan`) gets "Nu e un număr valid", not the field's range message.
  `+ 0.0` turns `-0` into `0` (it printed `-0°`).
- `calcul(implicit)` returns the values it read and `main` passes them to the next round: the
  banner promises that Enter keeps the value in brackets.
- Only `sfat_ra` offers Dec as the way out; the Dec line never tells the user to use Dec.
- The RA label is padded to 21 characters and followed by one space, so values start in column 22
  like the Dec line's and never touch the colon (at dec 41,269 they did).
- A text that quotes a limit uses its constant (`DITHER_MIN_PX`, `DITHER_MAX_PX`, the last step
  from `len(TREPTE_SYNSCAN)`), never the number.
- The closing test is computed at step 1 (`TREPTE_SYNSCAN[1]`) whatever the round's step: above
  1x an east press reverses the RA motor and the backlash would spoil the test.

## Conventions

- Romanian throughout the script: the UI with diacritics (ș and ț with the comma below), the
  identifiers and the comments. Keep the file in one language. The kit's ADR 0012 (English code
  and comments) is not applied here; translating is the owner's call. This file and the commit
  messages are in English.
- Numbers go out with a decimal comma through `fmt` (fixed decimals) and `fmt_scurt` (`g`);
  answers take a comma or a point.
- Standard library only; no packages.
- LF line endings and UTF-8 without BOM, as the repository's `.gitattributes` (`* text=auto
  eol=lf`) keeps every file.
- No version, no licence notice, no CHANGELOG: adding any of them is the owner's call (FlatCheck,
  WCS_Copy and FocalLenghChecker are GPL-3.0-or-later, with a notice in every file).

## Open, not ruled on by the owner

- `SECUNDE_MAX = 60` was chosen in the review as the longest press that is still practical
  between frames.
- Assumption 2 above is unmeasured.
- Ideas not done: the backlash time as an input, the declination in degrees and minutes, naming
  the arrow to press, a dither pattern that never reverses DEC.

## Git

- The `photo` monorepo, git root `E:\Photografie\Code`; this project is `DitherCalculator/`.
- The kit's rules "Commits and merges", "Branches", "Worktrees" and "Read the diff" apply here by
  hand (`ClaudeCodeStarterKit/.claude/rules/kit/process.md`). Commit messages are one line, in
  English, at most 100 characters, saying why; no body, no trailers. The assistant commits only on
  its own `claude/<Task-name>` branch, in a worktree made from this folder with
  `git worktree add .claude/worktrees/<Task-name> -b claude/<Task-name> main`; the worktree holds
  the whole repository, so the project is at `<worktree>/DitherCalculator/`. `main` is the
  owner's: a merge (`git merge --no-ff`), a push or a rebase happens only when the owner says so,
  each time.
- The repository's git hooks run here too (`core.hooksPath` = `E:/Photografie/Code/.githooks`):
  commit-msg refuses a second line, more than 100 characters or a trailer; pre-commit refuses the
  assistant's commit on a `claude/` branch in the main checkout and runs no gate for this folder
  (no `scripts/verify.ps1`), so the checks above are the gate. Plain `git commit`; never
  `--no-verify`, never `-c core.hooksPath`.
