# Add a Help button and F1 that open the manual at the current tab's section

## Context

The DarksCheck window has no way to reach its manual, the DarksCheck page of the suite's wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck`, one section per tab). On 2026-11-16
the owner asked for a Help button with a nice icon, also on F1, that opens the wiki at the
section of the tab on screen, "the way FlatCheck did it" (FlatCheck R-141, its ADR 0112,
`FlatCheck.Wpf/Presentation/Manual.cs`, `HelpTests.cs`).

Facts found (2026-11-16):

- F1 opens About today: `MainWindow.xaml` binds `ApplicationCommands.Help` (gesture F1) to
  `ShowAbout`; R-120 ("About (F1)"), the About tooltip, `README.md` and the wiki's "The top bar"
  say so. No ADR states it.
- The states: empty (no folder), problem card (`HasProblem`), tabs (`HasResult`), the wait screen
  over everything (`IsRunning`, every run clears the result first); a stopped run leaves the
  content empty with a status line.
- The wiki page's headings give the anchors `#summary`, `#frames`, `#maps`, `#hot-pixels`,
  `#match-with-lights`, `#index`, `#adu-scale-explained`, `#report`, `#opening-a-folder`,
  `#when-something-goes-wrong` (its own contents list links them). The wiki is English only.
- Links open in `AboutWindow.OpenLink` (Process.Start, a failure only a Debug line, pinned by
  `R101_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays`); there is no `ExternalLink`.
- Measured (temporary test, removed, worktree clean) at the window's minimum width, 960 px,
  Display text mode: the bar is already full, About ends exactly at the bar's edge (940) in both
  languages. A Help button is 80 px (`Help`) / 88 px (`Ajutor`); with it About would end at
  997 / 995, cut by about 57 px. The Frames tab already needs 1020 px (20 + 620 + 360 + 20).

Owner's answers (2026-11-16, they make the ADR `accepted 2026-11-16`):

1. The problem card opens `When something goes wrong`, whose table explains each message of the
   card and what to do (FlatCheck sends its card to its opening section, where its wiki explains it).
2. The window's minimum width goes from 960 to 1020 px, so the bar with Help fits, as does the
   Frames tab (it no longer fits half of a 1920 px screen).

Taken from FlatCheck as they are (the owner's "do the same"): F1 moves from About to Help, About
keeps its button; the button is the drawn icon followed by the word; the addresses live in the
window project; the manual is English in both languages; the log line `Help opened: <address>`.
Assumption: Help goes just before About (in FlatCheck it is the button before About too).

New texts (FlatCheck's wording, plain words):

| Where | English | Romanian |
| --- | --- | --- |
| Help button | `Help` | `Ajutor` |
| Help tooltip | `The manual, at the section of this tab (F1)` | `Manualul, la sectiunea tabului curent (F1)` |
| About tooltip (was `... (F1)`) | `Version, licence, components` | `Versiune, licenta, componente` |

## Requirement (draft, accepted with this plan)

**R-125** (section "The window", `draft` until the tests pass):

> The top bar has a `Help` (`Ajutor`) button just before `About`: a question mark on a disc of the
> theme's accent colour, drawn, then the word. The button and the F1 key, pressed in the main
> window, open DarksCheck's manual in the system's browser,
> `https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck`, at the section of what is on screen:
> the selected tab's (`#summary`, `#frames`, `#maps`, `#hot-pixels`, `#match-with-lights`,
> `#index`, `#adu-scale-explained`, `#report`), `#when-something-goes-wrong` on the problem card,
> and `#opening-a-folder` before a folder is chosen, on the wait screen and after a stopped run.
> The manual is English in both languages. Tooltip `The manual, at the section of this tab (F1)`
> (`Manualul, la sectiunea tabului curent (F1)`). The button stays live during an analysis. Each
> press is an Info line `Help opened: <address>` of the log; a computer without a browser is only
> a Debug line, as for About's links. About opens from its button alone, tooltip `Version,
> licence, components` (`Versiune, licenta, componente`). The window is at least 1020 px wide, so
> the whole bar shows in both languages. Example: with the Maps tab selected, F1 opens
> `https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck#maps`.

**R-120** loses "(F1)".

## Steps

All in the worktree `DarksCheck/.claude/worktrees/Add-a-help-button-to-DarksCheck` (branch
`claude/Add-a-help-button-to-DarksCheck`, already made from `main`); nothing built in the main
checkout.

1. **Plan and requirement.** Save this plan as `docs/plans/2026-11-16-add-a-help-button-and-f1.md`;
   `docs/requirements.md`: R-125 as `draft`, R-120 edited.
2. **Failing tests**, new `tests/DarksCheck.Wpf.Tests/HelpTests.cs` (STA via `StaFixture`, a copy
   of FlatCheck's shape):
   - `R125_EachStateAndTabHasItsSectionOfTheManual`: theory over
     `Manual.Url(Manual.SectionFor(hasProblem, hasResult, isRunning, tab))`, the addresses written out.
   - `R125_TheEightTabsCarryTheirSections`: the `TabItem` tags of `MainWindow.Tabs`, in order.
   - `R125_HelpAndF1OpenTheManualAtTheSectionOnScreen`: `new MainWindow(vm) { OpenAddress = opened.Add }`,
     F1 raised as a key event (FlatCheck's `PressF1`): no folder → `#opening-a-folder`; a missing
     folder → `#when-something-goes-wrong`; `WindowFixture.Session()` opened, tab 2 → `#maps`,
     tab 4 → `#match-with-lights`; the log holds `Help opened: ...#maps` from logger `Window`.
   - `R125_TheHelpButtonShowsAnIconAndTheWord` (both languages): content hosted in a `Border`
     (an unshown window renders white), Display text mode, laid out at 1020 px: the button runs
     `ApplicationCommands.Help`, is enabled, holds a `DrawingImage` and the word, the tooltip
     above, sits just before About; About has no command and the new tooltip; About ends inside
     the bar's 20 px right padding; the window's `MinWidth` is 1020.
   Run them, paste the failing output.
3. **ADR 0061**, `docs/decisions/0061-f1-and-a-help-button-open-the-manual-at-the-section-on-screen.md`,
   accepted 2026-11-16: FlatCheck's ADR 0112 followed; the two answers above with the measured
   widths. Rejected: `Opening a folder` for the card; keeping 960 px (About cut by 57 px); the icon
   alone (still about 20 px over at 960, and the window's only button without a word); an icon
   font (Segoe Fluent Icons is missing on Windows 10); a help window inside the program.
4. **Code** (window project only; no core, console, golden or figure changes):
   - new `src/DarksCheck.Wpf/Presentation/Manual.cs`: `enum ManualSection` (ten values),
     `Manual.Page`, `Manual.Url`, `Manual.SectionFor` (running → opening; problem → when
     something goes wrong; a result with a tab → the tab; else opening). GPL header, XML docs.
   - new `src/DarksCheck.Wpf/Presentation/ExternalLink.cs`: `Open(RequestNavigateEventArgs)` with
     `AboutWindow.OpenLink`'s exact logic (absolute address read inside the try, the written one
     logged), and `Open(string)` for Help; `AboutWindow.OpenLink` calls it. `R101_...` stays as it is
     and must pass unchanged.
   - `Theme/Dark.xaml`: `HelpIcon`, FlatCheck's `DrawingImage` (disc in `AccentBr`, mark in `WindowBg`).
   - `MainWindow.xaml`: `MinWidth="1020"`; `Help` bound to `ShowHelp`; the Help button before About
     (`AutomationProperties.Name` the word); About `Click="ShowAbout"` and the new tooltip;
     `x:Name="Tabs"` and each `TabItem`'s `Tag="{x:Static local:ManualSection.X}"`; header comment.
   - `MainWindow.xaml.cs`: `ShowHelp` (section from `_vm` and the selected tab's `Tag`, Info line,
     `OpenAddress`); `internal Action<string> OpenAddress { get; set; } = ExternalLink.Open`;
     `ShowAbout` becomes a click handler.
5. **Look at it.** `DARKSCHECK_DUMP_SCREENS=<scratchpad>` with `--filter HelpTests`, read the two
   PNGs of the top bar, and show the owner the picture before the commit, so the icon can change.
6. **Docs.** `docs/design/window.md` (Help, `Manual`, `ExternalLink`, the wiki headings and
   `Manual` move together, no gate test can read the wiki), `docs/design/logging.md` (the
   `MainWindow` row gains Help; About's link line now in `ExternalLink`), `README.md` (Help and F1,
   About without F1), `CHANGELOG.md` (Added: Help and F1; Changed: F1 no longer opens About, the
   window is at least 1020 px wide), R-125 to `implemented`.
7. **Wiki** (`E:\Photografie\AstroDarkroomWiki`, `DarksCheck.md`, "The top bar"): a Help bullet,
   About without F1. Edited in the clone only; commit and push asked at wrap-up, and whether to
   retake the screenshots that show the old bar.
8. **/wrap-up**: `dotnet build --no-incremental -warnaserror`, `scripts/verify.ps1` green, read
   the diff, commit on the branch, offer the subagent review, propose the merge.

## Verification

- The four `R125_` tests fail before step 4 and pass after; `RepoInvariantTests` accepts R-125;
  `R101_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays` passes untouched.
- The ten anchors checked by hand against the headings of `DarksCheck.md` in the wiki clone.
- The rendered top bar at 1020 px shows `Choose folder... | Write the files... | (?) Help | About`
  (and `Ajutor` / `Despre`), nothing cut.
- One real press with the owner's go: the built window, F1 on the Maps tab, the browser at `#maps`.
- No golden file, formula or figure moves: `/validate` is not needed.

## Not in this task

- No version bump (the owner's); a new feature would make it a minor one.
- LightsCheck and AstroDarkroom have their own Help branches in other sessions.

## What deviated

- **The minimum width is 1160 px, not 1020.** The rendered bar at 1020 px showed the folder's
  column at 3 to 5 px, its heading wrapped one letter a line and the bar 743 px tall (836 in
  Romanian; today at 960 px it is already 190 px). Measured at ten widths, put to the owner
  again: 1160 px, the narrowest at which the heading stays on two lines (69 px). `HelpTests`
  also pins the bar's height. ADR 0061 has the table.
- **The theme and the About window are untouched.** Both are LightsCheck's copies, improved
  there first (ADR 0009): the icon is a resource of `MainWindow.xaml`, and `ExternalLink` serves
  Help only; About keeps its own link handler, so `R101_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays`
  is untouched.
- The wiki edit is in the clone, not committed yet; the clone also holds other sessions'
  uncommitted edits (`AstroPhotoAnalyzer.md`, `Home.md`), which this task does not touch.
- **After the branch's review: 1180 px, not 1160.** The review found that `MinWidth` counts the
  window's 16 px frame, which the layout tests and the height table had not: a real window shown
  off-screen at 1160 px had 1144 px inside, and in Romanian the heading took three lines (82 px).
  Measured on the real window at six widths, put to the owner a third time: 1180 px (two lines
  from 1169, measured at 1 px steps after the second review, which also corrected figures of
  ADR 0061 that mixed widths inside the frame with the window's). `HelpTests` now lays out at the minimum less the frame, and tests the Debug line of
  an address the shell refuses, which the review found untested.
