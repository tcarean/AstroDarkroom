# 2026-10-28 Add the top band to the window

## Context

The owner asked on 2026-10-28 for FlatCheck's band at the top of CopyWcs's window (the name
with the version, the copyright, the About button) and for a short explanation of what the tool
does, in English and Romanian. Settled in this session: R-038 as drafted, the explanation in the
middle of the band (option A of the mockups, ADR 0008, accepted 2026-10-28), and About moving
out of the row of Preview and Copy, so the window has one About button.

Already on `claude/Add-the-top-band-to-the-window` (worktree
`.claude/worktrees/Add-the-top-band-to-the-window`), not committed:

- `docs/requirements.md`: R-038 `accepted`; R-030, R-035 and R-036 amended (About in the band,
  the band's Romanian words in the R-036 list).
- `docs/decisions/0008-the-window-has-the-siblings-top-band-with-the-explanation-in-its-middle.md`,
  accepted.
- `tests/CopyWcs.Wpf.Tests/WindowSmokeTests.cs`: the theory
  `R038_TheBandShowsTheConsolesFirstTwoLinesTheExplanationAndTheOnlyAboutButton`, English and
  Romanian, fails now (no banner in the window); the R036 word list gains `Versiune`, `licenta`,
  `rezolvat`, `aceeasi`, `pixelii`.

## Steps

1. `src/CopyWcs.Wpf/ViewModels/MainViewModel.cs`: `Banner => VersionInfo.Banner` and
   `Copyright => LicenseInfo.Copyright`, as FlatCheck's `MainViewModel` and this project's
   `AboutViewModel` give them (the core's members are internal, so XAML cannot reach them with
   `x:Static`); the CA1822 suppression with its reason, as `RefusedHeading` has; XML comments.
   They pass the core's two lines through, nothing is computed (ADR 0007).
2. `src/CopyWcs.Wpf/MainWindow.xaml`: the root becomes a grid of two rows. Row 0 is the band,
   FlatCheck's top bar markup: a `Border` on `PanelBg` with a `BorderBr` line of `0,0,0,1` and
   padding `20,12`, three columns: `{Binding Banner}` semi-bold over `{Binding Copyright}` in
   `Faint`; the explanation as an `{l:T En='...', Ro='...'}` pair in `Muted`, centred, with a
   margin before About; the About button with `Command="Help"`, `MinWidth="88"` (so it lines up
   with the Browse buttons under it) and the tooltip pair. Row 1 is the present form with its
   margin. The About button leaves the row of Preview and Copy. The header comment says the band
   is FlatCheck's (ADR 0008). `Height` and `MinHeight` grow by the band's measured height
   (about 60 px expected: 640 to 700, 560 to 620), so the outcome card keeps its room.
3. `src/CopyWcs.Wpf/MainWindow.xaml.cs`: the button now runs the Help command, as F1 does, so
   the click handler goes; one `ShowAbout(object, ExecutedRoutedEventArgs)` serves the
   `CommandBinding`; XML comments updated.
4. Docs: R-038 `implemented`; `docs/design/window.md` (the band and where its lines come from,
   the one handler, the band as a copied piece, the window's height, the R038 test);
   `docs/design/README.md` index row up to R-038; `README.md`, "The window" (the band and
   About); `CHANGELOG.md`, the unreleased window line gains the band (ADR 0008); this plan saved
   as `docs/plans/2026-10-28-add-the-top-band-to-the-window.md`, with "What deviated" at
   wrap-up.

## Verification

- `scripts/verify.ps1 -SkipTests` after each code step; the R038 theory green after steps 1-3,
  R036 and R037 still green.
- `COPYWCS_DUMP_SCREENS=<scratchpad>/after dotnet test tests/CopyWcs.Wpf.Tests`: look at
  `band-en.png`, `band-ro.png` and the `window-*.png` beside the "before" set rendered from
  `main`; measure the band's height in pixels for step 2's window height.
- At the end: `dotnet build CopyWcs.sln -c Release --no-incremental -warnaserror --no-restore`,
  then the full `scripts/verify.ps1`; read the whole diff.
- `/wrap-up`: commit on the branch, then offer the subagent code review before proposing the
  merge.

Out of scope: the version bump (the owner's; 1.1.0 is still open), the About window, the
console.

## What deviated

Nothing in the steps. The band measured 57 px at 720 px wide in both languages (the explanation
in two lines; the bottom line of the band on row 56 of `band-en.png`, `band-ro.png` and
`window-preview.png`), so the planned 700 and 620 stand, the 57 rounded up. The gate ended
green with 172 tests (132 + 40), the 170 of `main` and the two cases of the R038 theory.

Two subagent reviews followed. The first found that the R038 theory pinned neither the band's
left-to-right order nor that Help can run from About, and four inaccurate doc wordings (R-030's
"under the options", ADR 0008 on the siblings' bands and on the tagline, the window note's sizes
and a long line); d3a614b fixed them, and the strengthened theory fails on three broken bands
(the Help handler removed, banner and About swapped, the middle column sized to its text). The
second, on d3a614b alone, found three doc nits (lines the edits left over 96 characters, the
settings column of DarksCheck's and LightsCheck's bands), fixed in dc9f56e. Left to the owner:
the first Tab now lands on About, as in FlatCheck, and the 620 minimum height is more than a
1366 x 768 screen at 125 % offers.
