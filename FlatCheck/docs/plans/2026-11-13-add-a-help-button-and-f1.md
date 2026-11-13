# Add a Help button and F1 that open the manual at the current tab's section

## Context

The FlatCheck window has no way to reach its manual, which lives in the suite's wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck`, one section per tab). The owner asked
for a Help button with an icon in the window, also on F1, that opens the manual at the section
of the tab on screen.

Facts found (2026-11-13):

- F1 opens `About` today: `MainWindow.xaml` binds `ApplicationCommands.Help` (default gesture F1)
  to `ShowAbout`; R-089, ADR 0040, the button's tooltip and the wiki's "The top bar" say so.
- The window has no icon anywhere, only text buttons (`Theme/Dark.xaml`, one `Button` style).
- The selected tab is known only to the view (`Tabs`, an `AccessibleTabControl`); the view-model
  knows `HasFolder`, `HasProblem`, `IsRunning`.
- The wiki page's own contents list confirms the anchors: `#structure`, `#end-to-end`,
  `#exposure`, `#histogram`, `#synthetic`, `#split`, `#opening-a-session`, `#the-session-folder`.
  The wiki is English only. GitHub lands at the top of the page on an anchor it does not know.
- `Presentation/ExternalLink.cs` already opens an address in the browser and only logs a failure.

Owner's answers in this session (they make ADR 0112 `accepted 2026-11-13`):

1. F1 opens the manual; `About` keeps its button and loses the key.
2. The button is an icon followed by the word: `(?) Help` / `(?) Ajutor`, between
   `Choose folder...` and `About`.
3. With no tab on screen the manual opens at the section of the state: `Opening a session` (no
   folder chosen, or the opening analyses running), `The session folder` (the problem card).
   (Changed after the branch review, the owner's answer of 2026-11-13: the problem card opens
   `Opening a session` too, where the wiki explains it; ADR 0112.)

## Requirement (draft, to be accepted with this plan)

**R-141** (section "The window", status `draft` until the tests pass):

> The top bar has a `Help` (`Ajutor`) button between `Choose folder...` and `About`: a question
> mark in a circle, drawn in the theme's accent colour, then the word. The button and the F1
> key, pressed in the main window, open FlatCheck's manual in the system's browser,
> `https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck`, at the section of what is on
> screen: the selected tab's (`#structure`, `#end-to-end`, `#exposure`, `#histogram`,
> `#synthetic`, `#split`), `#the-session-folder` on the problem card, and `#opening-a-session`
> when no folder is chosen and while the analyses of the opening run. The manual is English in
> both languages. Tooltip: `The manual, at the section of this tab (F1)` (`Manualul, la
> sectiunea tabului curent (F1)`). The button is live during an analysis. Each press writes an
> Info line `Help opened: <address>` to the log; a computer without a browser only logs it, as
> for any link. `About` opens from its button alone (R-089). Example: with the Exposure tab
> selected, F1 opens `https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck#exposure`.

**R-089** loses "and F1"; the tooltip of `About` becomes `Version, licence, components`
(`Versiune, licenta, componente`).

## Steps

Worktree first (nothing is built in the main checkout):
`git -C E:\Photografie\Code worktree add FlatCheck/.claude/worktrees/Add-a-help-button-to-FlatCheck -b claude/Add-a-help-button-to-FlatCheck main`

1. **Requirement.** `docs/requirements.md`: add R-141 as `draft`, edit R-089.
2. **Failing tests** in `FlatCheck.Wpf.Tests` (new `HelpTests.cs`, STA through `StaFixture`):
   - `R141_EachStateAndTabHasItsSectionOfTheManual`: a theory over
     `Manual.SectionFor(hasFolder, hasProblem, isRunning, tab)` and `Manual.Url(section)`, the
     eight addresses written out in the test.
   - `R141_TheSixTabsCarryTheirSections`: the six `TabItem`s of a `MainWindow`, in order, carry
     the six tab sections.
   - `R141_HelpAndF1OpenTheManualAtTheSectionOnScreen`: a fresh `MainWindow` with its
     `OpenAddress` seam replaced; `ApplicationCommands.Help.Execute` gives
     `...#opening-a-session`; the Help command's gesture is F1; the log holds `Help opened:`.
   - `R141_TheHelpButtonShowsAnIconAndTheWord` (both languages): the button runs
     `ApplicationCommands.Help`, holds a drawn shape and `Help` / `Ajutor`, the tooltip above;
     the `About` button has no command and no `(F1)` in its tooltip.
   Run them, paste the failing output.
3. **ADR 0112**, `docs/decisions/0112-f1-and-a-help-button-open-the-manual-at-the-section-on-screen.md`,
   accepted 2026-11-13: the three answers above, plus the two choices of mine: the icon is drawn
   in XAML (no icon font: Segoe Fluent Icons is missing on Windows 10), and the addresses live in
   the window project, since the console has no tabs and an address is neither a figure nor a
   diagnosis. Rejected: Shift+F1 for About, icon alone, top of the page, an icon font, a help
   window inside the program. It supersedes ADR 0040 in part (F1 opening `Despre`); I read the
   kit's ADR 0024 first for the allowed way to mark that on 0040.
4. **Code** (window project only; nothing in the core or the console changes):
   - new `FlatCheck.Wpf/Presentation/Manual.cs`: `enum ManualSection` (eight values),
     `Manual.Page`, `Manual.Url(ManualSection)`, `Manual.SectionFor(bool hasFolder, bool
     hasProblem, bool isRunning, ManualSection? tab)`; GPL header, XML docs.
   - `Presentation/ExternalLink.cs`: add `Open(string address)`; `Open(RequestNavigateEventArgs)`
     calls it (same catch, same log line).
   - `Theme/Dark.xaml`: the icon as a resource (`HelpIcon`, 16 px: circle and question mark as
     vector shapes in `AccentBr`).
   - `MainWindow.xaml`: `Help` command bound to `ShowHelp`; new button `Command="Help"` with
     icon + `{l:T En='Help', Ro='Ajutor'}` and the tooltip; `About` becomes `Click="ShowAbout"`
     with the tooltip without F1; each `TabItem` gets
     `Tag="{x:Static local:ManualSection.Structure}"` and so on; the header comment updated.
   - `MainWindow.xaml.cs`: `ShowHelp` (section from `_vm.HasFolder/HasProblem/IsRunning` and the
     selected tab's `Tag`, log line, `OpenAddress(url)`); `internal Action<string> OpenAddress =
     ExternalLink.Open` as the test seam; `ShowAbout` becomes a click handler.
5. **Look at it.** `FLATCHECK_DUMP_SCREENS=<scratchpad> dotnet test FlatCheck.Wpf.Tests --filter
   WindowSmokeTests`, read the PNG of the main window in both languages, and send the picture of
   the top bar to the owner before the commit, so the icon can still be changed.
6. **Docs.** `docs/design/two-applications.md` (how the section is chosen; the wiki headings and
   `Manual` must move together), `docs/design/code-map.md` (`Manual.cs`, `ExternalLink`,
   `MainWindow`), `docs/design/logging.md` (the `Help opened` line), `README.md` (the window's
   Help button and F1), `CHANGELOG.md` (Added: Help and F1; Changed: F1 no longer opens About),
   R-141 to `implemented`, this plan saved as
   `docs/plans/2026-11-13-add-a-help-button-and-f1.md`.
7. **Wiki** (separate repository, `E:\Photografie\AstroDarkroomWiki`): "The top bar" gains Help
   and drops F1 from About. Edited in the clone only; committing and pushing it is asked at
   wrap-up. The wiki's screenshots show the old top bar; whether to retake them is asked then too.
8. **/wrap-up.** `dotnet build --no-incremental -warnaserror`, then `scripts/verify.ps1` green,
   read the diff, commit on the task branch, propose the code review and the merge.

## Verification

- The four `R141_` tests fail before step 4 and pass after; `RepoInvariantTests` accepts R-141.
- The eight anchors checked against the headings of `FlatCheck.md` in the wiki clone (by hand:
  the wiki is outside the repository, so no gate test can read it).
- The rendered PNG of the main window shows `Choose folder... | (?) Help | About` (and `Ajutor`
  in Romanian), nothing clipped at 960 px, the window's minimum width.
- One real press at the end, with the owner's go: the built window, F1 on the Exposure tab,
  the browser at `#exposure`.
- No golden file, no formula, no figure changes: `/validate` is not needed.

## Not in this task

- The other tools of the suite keep F1 on `About`; the same button there is a task per tool.
- No version bump (the owner's); a new feature would make it a minor one.
