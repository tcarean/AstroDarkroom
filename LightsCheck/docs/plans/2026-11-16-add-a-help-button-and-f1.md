# Add a Help button and F1 that open the manual at the current tab's section

## Context

The LightsCheck window has no way to reach its manual, the LightsCheck page of the suite's wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck`, one section per tab). On
2026-11-16 the owner asked for a Help button with a nice icon, also on F1, that opens the wiki
at the section of the tab on screen, "the way FlatCheck did it" (FlatCheck ADR 0112, R-141,
`FlatCheck.Wpf/Presentation/Manual.cs`, `HelpTests.cs`).

Facts found (2026-11-16):

- F1 opens About today: `MainWindow.xaml` binds `ApplicationCommands.Help` (default gesture F1)
  to `ShowAbout`, and the About button runs that command; its tooltip says `(F1)`, so do
  `README.md:398` and the wiki's "The top bar". No requirement names F1.
- The six tabs are Summary, Frames, Dithering, Quality, Rejected, Report. The wiki page's own
  contents list gives their anchors, `#summary`, `#frames`, `#dithering`, `#quality`,
  `#rejected` and `#report`, plus `#opening-a-session`, whose "Good to know" list explains the
  wait screen, Stop, the "Choose a session folder" screen and the red problem card. Each heading
  appears once, so GitHub adds no `-1` suffix. The wiki is English only.
- Screens shown instead of the tabs: the empty panel (no folder), the problem card
  (`HasProblem`), the wait screen (`IsRunning`), and a blank area after a stopped run. The tabs
  show on `HasResult`, which a new run clears at its start.
- The tab control (`AccessibleTabControl`) has no name; the view-model knows `HasResult` and
  `IsRunning`.
- `AboutWindow.OpenLink` opens a link in the browser and logs a failure at Debug, naming the link
  as written (`R071_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays`). FlatCheck's shared
  `ExternalLink` passes a relative link to the shell instead, which would change what that test
  pins, so the LightsCheck version keeps LightsCheck's catch.
- Measured (temporary test, at the minimum width 960, both languages): the top bar has 920 px
  inside its padding. With a Help button (80 px in English, 88 px in Romanian, plus 10 px
  margin) it fits while the app is idle (864 and 874 px of fixed columns), but not during an
  analysis, when Stop shows (931 and 961 px): About is cut by 11 and 41 px.

What the owner decided (FlatCheck's answers carried over by "the same as FlatCheck", plus the
question asked in this session). These make ADR 0060 `accepted 2026-11-16`:

1. F1 opens the manual; About keeps its button and loses the key.
2. The button is an icon followed by the word, `(?) Help` / `(?) Ajutor`. Its place transposes
   FlatCheck's (between `Choose folder...` and `About` there): just left of `About`, after
   `Write the files...`.
3. Every screen shown instead of the tabs opens `Opening a session`.
4. The window's minimum width goes from 960 to 1040 px, so the bar fits during an analysis too
   (Romanian with Stop: 961 + 40 padding = 1001 px; the folder name keeps about 39 px). This also
   meets the minimum-width case of review entry CR-055 (the Frames tab needs 980 px). The review
   list is not edited (ADR 0046 point 7); the task that takes CR-055 closes it.

## Requirement (draft, accepted with this plan)

**R-091** (section "The window", `draft` until the tests pass):

> The top bar has a `Help` (`Ajutor`) button just left of `About` (`Despre`): a question mark on
> a disc of the theme's accent colour, then the word. The button and the F1 key, pressed in the
> main window, open LightsCheck's manual in the system's browser,
> `https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck`, at the section of what is on
> screen: the selected tab's (`#summary`, `#frames`, `#dithering`, `#quality`, `#rejected`,
> `#report`) when the tabs show, and `#opening-a-session` for every screen shown instead of them:
> before a folder is chosen, during an analysis, after a stopped one, and on the problem card.
> The manual is English in both languages. Tooltip: `The manual, at the section of this tab
> (F1)` (`Manualul, la sectiunea tabului curent (F1)`). The button works during an analysis. Each
> press writes an Info line `Help opened: <address>` to the log; a computer without a browser
> only logs it, as for the About window's links. `About` opens from its button alone, tooltip
> `Version, licence, components` (`Versiune, licenta, componente`). The window is at least
> 1040 px wide, and the top bar fits there in both languages, with Stop shown too. Example: with
> the Dithering tab selected, F1 opens
> `https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck#dithering`.

The new texts are FlatCheck's, word for word.

## Steps

Worktree (already made, from `main` at dec039cf):
`E:\Photografie\Code\LightsCheck\.claude\worktrees\Add-a-help-button-to-LightsCheck`, branch
`claude/Add-a-help-button-to-LightsCheck`; the project is its `LightsCheck\` folder.

1. **Requirement.** Add R-091 to `docs/requirements.md` as `draft`.
2. **Failing tests**, new `tests/LightsCheck.Wpf.Tests/HelpTests.cs` (`[Collection("window")]`,
   `StaFixture`), modelled on FlatCheck's:
   - `R091_EachStateAndTabHasItsSectionOfTheManual`: a theory over
     `Manual.Url(Manual.SectionFor(hasResult, isRunning, tab))`, with the addresses written out.
   - `R091_TheSixTabsCarryTheirSections`: `MainWindow.Tabs`' six `TabItem` tags, in order.
   - `R091_HelpAndF1OpenTheManualAtTheSectionOnScreen`: windows with `OpenAddress` replaced, F1
     raised as a key-down through a stand-in `PresentationSource` (as in `AboutWindowFitTests`):
     no folder, then a missing folder (problem card), then a stopped run → `#opening-a-session`;
     a real result (`SessionFixture.WithStarSeries`) with tab 2, then 5 → `#dithering`,
     `#report`; the log holds `Help opened: …#dithering` from the `Window` logger.
   - `R091_TheHelpButtonShowsAnIconAndTheWord` (en, ro): the button runs
     `ApplicationCommands.Help`, is enabled, holds an `Image` with a `DrawingImage` and the word,
     with the tooltip above; About has no command and its tooltip names no key; Help sits between
     `Write the files...` and About; `MinWidth` is 1040; with Stop forced visible (the widest
     bar) About ends inside the bar's 20 px right padding. Renders `en-help-top-bar` /
     `ro-help-top-bar` for `LIGHTSCHECK_DUMP_SCREENS`.
   Run them and paste the failing output (a compile failure first, since `Manual` does not
   exist yet).
3. **ADR 0060**, `docs/decisions/0060-f1-and-a-help-button-open-the-manual-at-the-section-on-screen.md`,
   accepted 2026-11-16: the four points above and my choices: the icon is drawn in XAML
   (FlatCheck's drawing; icon fonts differ between Windows 10 and 11); the addresses live in the
   window project, since the console has no tabs and an address is neither a figure nor a
   diagnosis (the rule "Computed only in the core" does not reach it); the browser is started in
   one place, `ExternalLink`. Rejected: Shift+F1 for About, the icon alone, the top of the page,
   an icon font, a help window inside the program, Stop taking Write's place, leaving the bar
   cut. Cites FlatCheck's ADR 0112 as its model.
4. **Code** (window project only; the core and the console are untouched):
   - new `src/LightsCheck.Wpf/Presentation/Manual.cs`: `enum ManualSection` (OpeningASession and
     the six tabs), `Manual.Page`, `Manual.Url(ManualSection)`,
     `Manual.SectionFor(bool hasResult, bool isRunning, ManualSection? tab)`.
   - new `src/LightsCheck.Wpf/Presentation/ExternalLink.cs`: `Open(Uri link)`, the body of
     `AboutWindow.OpenLink` moved there (same catch, same Debug line, the link named as written);
     `AboutWindow.OpenLink` calls it.
   - `Theme/Dark.xaml`: `HelpIcon`, a `DrawingImage` of 16 units (a disc in `AccentBr`, the
     question mark in `WindowBg`), FlatCheck's geometry.
   - `MainWindow.xaml`: `MinWidth="1040"`; `Help` bound to `ShowHelp`; the new button
     (`Command="Help"`, icon + `{local:T En='Help', Ro='Ajutor'}`, the tooltip,
     `AutomationProperties.Name`) before About; About becomes `Click="ShowAbout"` with the tooltip
     without F1; `x:Name="Tabs"` on the tab control and a `Tag="{x:Static
     local:ManualSection.…}"` on each tab; the header comment.
   - `MainWindow.xaml.cs`: `internal Action<Uri> OpenAddress { get; set; } = ExternalLink.Open;`,
     `ShowHelp` (the section from `_vm.HasResult`, `_vm.IsRunning` and the selected tab's
     `Tag`, the log line, `OpenAddress`); `ShowAbout` becomes a click handler.
   XML documentation on every new or edited type and method.
5. **Look at it.** `LIGHTSCHECK_DUMP_SCREENS=<scratchpad>` with `--filter HelpTests` and the smoke
   tests; read the top-bar PNGs in both languages, then send the owner the picture of the bar
   before the commit, so the icon can still change.
6. **Docs.** `docs/design/window.md` (the Help button, how the section is chosen, the wiki
   headings and `Manual.cs` move together, `ExternalLink`, the minimum width);
   `docs/design/logging.md` (the `Help opened` line in the `MainWindow` row, the link line now
   from `ExternalLink`, the tests paragraph); `README.md:398` (Help and F1, About without F1);
   `CHANGELOG.md` (Added: Help and F1 open the manual; Changed: F1 no longer opens About, the
   minimum width is 1040 px); R-091 to `implemented`; this plan saved as
   `docs/plans/2026-11-16-add-a-help-button-and-f1.md`.
7. **Wiki** (`E:\Photografie\AstroDarkroomWiki`, `LightsCheck.md`, "The top bar"): add Help with
   F1, drop F1 from About. Edited in the clone only. A DarksCheck session is doing the same task
   in parallel and may edit `DarksCheck.md` in the same clone, so only `LightsCheck.md` is
   staged; committing and pushing are asked at wrap-up, and so is whether to retake the
   screenshots that show the old top bar.
8. **/wrap-up.** `dotnet build --no-incremental -warnaserror`, then `scripts/verify.ps1` green,
   read the whole diff, commit on the branch, offer the code review, then propose the merge.

## Verification

- The four `R091_` tests fail before step 4 and pass after; `RepoInvariantTests` accepts R-091.
- `R071_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays` still passes, unchanged, after the
  move to `ExternalLink`.
- The seven anchors checked by hand against the headings of `LightsCheck.md` in the wiki clone
  (no gate test can read the wiki).
- The rendered bar shows `Choose folder... | Write the files... | (?) Help | About` (and
  `Ajutor` / `Despre`), nothing cut at 1040 px.
- One real press at the end, if the owner wants it: the built window, F1 on the Dithering tab,
  the browser at `#dithering`.
- No golden file, no formula, no figure changes: `/validate` is not needed.

## Not in this task

- CR-055's other cases (the Frames tab's scroll bar, long names, the empty tooltip).
- No version bump (the owner's); a new feature would make it a minor one.

## What deviated

- The rendered bar showed a fact the first question had not: at 1040 px during an analysis the
  folder's column keeps 39 px in Romanian, and its label, which wrapped, broke letter by letter
  and made the bar grow from 73 to 118 px (89 in English). Asked on the picture, the owner chose
  to keep 1040 px and to keep the column's texts on one line, cut with an ellipsis. R-091,
  ADR 0060 and `R091_TheHelpButtonShowsAnIconAndTheWord` (the bar's height with Stop shown,
  which failed before the change) say so.
- `ExternalLink.Open` takes a `Uri`, and the window's seam is `Action<Uri>`, so that the About
  window's catch of a relative link stays as it was.
