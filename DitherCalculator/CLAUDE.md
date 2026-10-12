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

## What the manuals say, and what the script still assumes

Source: SynScan App User's Manual, 2023-05-25 (Sky-Watcher; the owner's copy is
`synscan_app_manual_en_20230525.pdf`); for 2 and 4, also the research of 2026-10-12 (next
section).

1. Documented (p. 8, "Slewing the mount"): steps 0 to 9 are 0.5x, 1x, 8x, 16x, 32x, 64x, 128x,
   400x, 600x and Max, in multiples of the sidereal rate, and the app shows the step's number at
   the center of the direction buttons. `TREPTE_SYNSCAN` is that table.
2. Documented for the hand control, assumed for the app: an arrow moves the mount at that speed
   relative to the sky, added to or taken from the tracking. The SynScan V3 and V4 hand-control
   manuals (and Orion's SynScan V5) say so for equatorial mounts at rates 0 and 1: the rate is how
   fast the object moves in the field with tracking on, not how fast the axis turns. The app
   manual has the same rate table without that note. Two hints that the app does the same: its
   PPEC training without an autoguider keeps the star centered with the direction buttons, which
   works only if the low rates correct on top of the tracking (a deduction); and Sky-Watcher,
   quoted on the SharpCap forum, says that a guide pulse on an axis already moving only speeds it
   up, slows it or stops it, never reverses it (about guiding, not the arrows). At 1x east stops
   the RA motor and west doubles it, so the image moves at 1x either way and nothing reverses on
   RA. Above 1x east reverses the RA motor, on the press and again on the release, so RA also
   loses time to backlash; the RA backlash note shows only above 1x. The closing note's test
   checks this at step 1: 10 s east and 10 s west should each move a star by the printed pixels.
   If west moves nothing and east twice as much, the app sets the axis speed instead, and the RA
   texts change.
3. DEC backlash is large on this mount: on the Veil session of 2026-10-06, 47 pauses gave only 38
   visible moves (LightsCheck, `docs/decisions/0007`), and a pattern that reverses DEC every time
   moves less than planned. The manual (pp. 21-22) describes Settings > Backlash: when an axis
   reverses, the app first turns it quickly by the set amount to take up the slack. With it set
   right, the printed press times hold after a reversal too. Start with a small value: too much
   makes the star jump.
4. EQ mode only. The hand-control note is limited to equatorial mounts, and the app's release
   notes for v2.6.5 (October 2025) say that in AZ tracking a move on one axis interrupts the
   tracking on that axis while the other keeps tracking; read as the pressed axis running at the
   button speed without tracking (a deduction). The script assumes the GTi in EQ mode.

LightsCheck's dithering table measures each step between frames, in pixels and per axis, from the
lights. A session with known press times read there is another way to check 2.

## SynScan lag, alignment and manual dithering: research of 2026-10-12

Summarized from a claude.ai research chat that the owner shared
(`https://claude.ai/share/3e3f3c23-ab80-4b87-92a0-e957adb8c75e`). The sources at the end were read
in that chat, not re-read for this file; "deduction" marks what the chat inferred, not read. The
chat assumed EQ mode, the Wi-Fi link and step 2 = 8x.

### Lag

- Three causes, often confused:
  - Backlash. The app manual says that when the direction reverses at a low speed the motor
    starts but the object moves only after a visible delay. The wait is backlash / speed: 30" of
    backlash (an example, not a measurement) takes ~2 s at step 1 and ~0.25 s at step 2. On Dec
    the angle lost is the same at every step and only the wait changes, which is why step 1 feels
    sluggish after a reversal.
  - The link. Per the Sky-Watcher Motor Controller Command Set, every command goes in its own UDP
    packet and gets a reply in another. The direction is part of the motion mode, which can be set
    only with the motor fully stopped, so a reversal takes several exchanges (stop, check, new
    mode, speed, start); in the high-speed mode (used above 128x, for example) the speed cannot
    change while moving. Deduction: at the low steps a speed change in the same direction is one
    command. The app's default retries and timeout assume an unreliable channel; a lost stop
    packet probably lets the mount run on until the retry (a deduction). On Cloudy Nights one
    user's mount kept moving after the button was released and overshot; another found the app's
    buttons almost instant, so it depends on the link.
  - The person and the camera: reaction time, and the live-view delay when centering on it.
- Measure the link in Utility > Advanced > Diagnostic > Response Time; with long or uneven times
  in Access Point mode the manual recommends a less crowded Wi-Fi channel. A delay only after a
  reversal, with the motor heard starting at once, is backlash; a delay on every press is the
  link.
- 8 s at step 1 against 1 s at step 2: both move ~120" (15"/s against 120"/s). Each 0.1 s of
  delay or hesitation costs ~1.5" at step 1 and ~12" at step 2 (0.25 px and 2 px at 5.99"/px).
  On RA, step 1 only speeds the axis up (2x) or stops it (0x); step 2 against the tracking
  reverses it on the press and again on the release. For centering: coarse on step 2-3, finish on
  1 or 0, always from the same direction. For a dither the precision does not matter, so 1 s at
  step 2 is fine and faster (the chat's last answer).

### RIGHT + UP is an alignment rule, not a dithering one

- The SynScan manuals (the V4 hand-control manual is the one cited) ask to finish centering each
  alignment star with UP and RIGHT. Both axes have backlash and the app stores the motor
  positions when the centering is confirmed; finishing always from the same direction leaves the
  gears at the same end of the slack for every point, so the error is the same everywhere and the
  model absorbs it.
- It matters more on Dec (the chat's reasoning, not Sky-Watcher's words): on RA the tracking takes
  up the slack in a few seconds, and at steps 0-1 the axis does not even reverse; Dec has no
  tracking, so the gears stay where the last press left them.
- UP rather than DOWN is a convention; what counts is always the same direction. Celestron's logic
  is to align with the directions of the GOTO's final approach: against the load, and with the
  tracking on the tracking axis. Sky-Watcher's final GOTO direction was not found, so that reason
  is not confirmed for SynScan, and the chat could not check whether RIGHT is the tracking
  direction. Checked on 2026-10-12: an east press is against the tracking (the V4 manual's note on
  rates 0/1, p. 5; PHD2's developer: at a 0.5x guide speed the RA motor runs at 1.5x to move west
  and at 0.5x to move east; PHD2's log-analysis guide: at 1x or less the RA drive slows or pauses
  and never reverses), so east stops the RA motor at step 1 and reverses it above 1x. That RIGHT
  is east rests only on the owner's reading of the Veil session (Facts below): no Sky-Watcher
  document says which way RIGHT turns RA, and both the forum default that LightsCheck first took
  and an EQMOD user on Stargazers Lounge say right = west. RIGHT is against the tracking only if
  `→` is east, which is unverified.
- A dither stores nothing in the model, so the rule's reason does not apply, and a dither that
  always ends with RIGHT and UP walks the frame in one direction, the opposite of a dither.

### Manual dithering

- A random direction on both axes, kept around the center to hold the framing. A user who
  dithered on one axis only got noise aligned along one direction (AstroBin); LightsCheck's
  walking-noise verdict asks for the same (Facts below).
- RA on step 0 or 1: with assumption 2 the axis only speeds up or stops, so the direction does
  not matter and no backlash comes in. On step 2 and up against the tracking the axis reverses,
  and after the release the star drifts for a few seconds until the gears re-engage. Another
  AstroBin user dithers just so: a short press in a random direction every few frames, at 1x or
  the lowest step.
- Dec: no tracking, so after a reversal the first seconds go into backlash and a short press may
  move nothing (PHD2's manual shows a calibration where many Dec steps moved almost nothing; the
  Veil session in item 3 is the same on this mount). Ways out: presses longer than the backlash,
  a slight imbalance on Dec so the gears stay loaded on one side, or Settings > Backlash (item 3).
- Wait before the next frame. With automatic dithering too, a settle time too short together
  with the Dec backlash spoils the first seconds of the next exposure (SGP forum). For a manual
  dither the chat first estimated a few seconds, then said 5-10 s, which also covers the RA
  reversal after a press against the tracking.

### How far

- For this setup (D3300 + 135 mm, ~6"/px) the target is the DSLR one, 10-15 px, from an earlier
  chat of 2026-10-06 ("Darks vs flats with dithering"), where 1 s at step 2 came out at ~15-20 px.
  At 6"/px, 8 s at step 1 and 1 s at step 2 are both a ~20 px dither on Dec.
- Why a DSLR needs more: Tony Hallas (Astronomy.com) describes DSLR color mottle in blobs of
  5-10 px that darks and flats do not remove, and dithers by three to five star diameters, at
  least 12 px. On the Astro Pixel Processor forum the advice against walking-noise streaks is at
  least 15 px, preferably 20, every few frames. A DSLR user on a star tracker (cited with the
  Stargazers Lounge and AstroBin threads) removed the "rain" pattern from a 1-2 h stack with moves
  of tens of pixels every 15-20 min.
- The chat first said that many use about 5 px. That came from two posts by users dithering
  automatically and often (one with an ASIAIR), was withdrawn in the same chat, and does not apply
  to manual DSLR dithering.
- RA moves cos(dec) of that: about half at +60°, so a high target needs a longer RA press (the
  script's RA table).

### Sources, as the chat cited them

- SynScan App User's Manual 2023-05-25:
  `https://inter-static.skywatcher.com/downloads/synscan_app_manual_en_20230525.pdf`
- SynScan V4 Hand Control Manual:
  `https://inter-static.skywatcher.com/downloads/Synscan_V4_Hand_Control_Manual_SSHCV4-F-161208V1-EN.pdf`
- SynScan V3 Hand Control Manual:
  `https://inter-static.skywatcher.com/upfiles/en_download_caty01415653149.pdf`
- Orion SynScan V5 manual: `https://www.astroshop.ro/Produktdownloads/65046_1_Anleitung-EN.pdf`
- SynScan app release notes: `https://www.skywatcher.com/download/software/synscan-app/about/`
- SharpCap forum, Sky-Watcher on PulseGuide:
  `https://forums.sharpcap.co.uk/viewtopic.php?p=25884` (also `?p=2161`)
- Sky-Watcher Motor Controller Command Set (a copy):
  `https://casite-460905.cloudaccess.net/media/kunena/attachments/5531/skywatcher_motor_controller_command_set.pdf`
- Cloudy Nights:
  `https://www.cloudynights.com/topic/685359-problems-with-synscan-wifi-and-pro-app/`,
  `https://www.cloudynights.com/topic/704013-synscan-app-question/`
- Celestron, backlash:
  `https://www.celestron.com/blogs/knowledgebase/what-is-backlash-and-how-can-i-adjust-it`
- Stargazers Lounge:
  `https://stargazerslounge.com/topic/373890-synscan-2-star-alignment-wont-accept-location`,
  `https://stargazerslounge.com/topic/288375-dither/`
- AstroBin: `https://app.astrobin.com/forum/topic/23203/dithering-no-guiding-system-no-problem`,
  `https://ssr.app.astrobin.com/forum/topic/10413/manual-dithering`,
  `https://app.astrobin.com/forum/topic/94582/dithering`
- Adam Block Studios, walking noise:
  `https://forum.adamblockstudios.com/forums/discussion/comment/3324`
- PHD2, Tools and Utilities: `https://adgsoftware.com/phd2/archive/man-2.5.0dev8/Tools.htm`
- SGP forum: `https://forum.sequencegeneratorpro.com/t/dithering-and-slow-recovery-phd2/194`,
  `https://forum.sequencegeneratorpro.com/t/dithering-not-working/450`
- Tony Hallas, Shooting with a DSLR: `https://www.astronomy.com/observing/shooting-with-a-dslr/`
- Astro Pixel Processor forum:
  `https://www.astropixelprocessor.com/community/main-forum/streaks-in-integrated-output-of-ha-lights/`

Read for the check of `→` = east on 2026-10-12: the V4 hand-control manual above (section 1.3,
p. 5: left/right turn RA, and the rates 0/1 note; section 3.7, p. 18: end with UP and RIGHT); the
SharpCap thread above (`?p=25884`, the Sky-Watcher reply relayed in November 2021); PHD2's
developer, `https://groups.google.com/g/open-phd-guiding/c/a63qU_9UWIM` (2021-02-21); PHD2's
"Analyzing PHD2 Guiding Results", `https://openphdguiding.org/Analyzing_PHD2_Guide_Logs.pdf`;
Stargazers Lounge, EQMOD's buttons:
`https://stargazerslounge.com/topic/376153-mount-north-s-e-and-w`.

## Facts from the rest of the repo, for when the script grows

- On the owner's SynScan `→` moves the mount east and `↑` north (LightsCheck
  `docs/decisions/0014`; a forum default with left = east was wrong for this app). This is the
  owner's reading of the Veil session, not a measurement; checked on 2026-10-12, no source
  confirms or refutes it. The app's manual (2023) and the V4 hand-control manual say only that
  left/right turn RA and up/down Dec, and the app's manual lists no setting that reverses the
  buttons. Two tests settle it: by day, with the camera lowered from the pole, `→` at step 8 for
  2-3 s swings the lens toward the east or the west horizon; at night, a lone `→` press between
  two frames shows in LightsCheck's dithering cell for the next frame, whose letter (`E` or `V`)
  comes from the plate solve, not from the arrow setting.
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
- Assumption 2 above is unmeasured on the owner's mount; the hand-control manuals document it,
  the app's manual does not.
- Where the research of 2026-10-12 and the script differ:
  - the under-1-s warning sends step 2 back to step 1 for the defaults (15 px at step 2 is 0,7 s
    on Dec), while the research found 1 s at step 2 fine for a dither (a 0.1 s delay there is
    ~2 px);
  - the docstring and the closing note say that the manual does not tell whether the arrow speed
    is relative to the sky: true of the app's manual, but the hand-control manuals say it for EQ
    mounts at rates 0 and 1;
  - if that note means that rates 2 and up are axis speeds, RA at step 2 moves the image at 9x
    east and 7x west, not 8x (1/multiple, 12.5 % at step 2): small for a dither, unmeasured, and
    a deduction;
  - the 5-10 s pause after the press is not printed.
- `→` = east on the owner's app is unverified (Facts below); naming the arrow to press (an idea
  below) needs one of its two tests first.
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
