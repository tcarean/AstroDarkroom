# Add a Help button and F1 that open the launcher's manual

## Context

The owner asked (2026-11-16) for a Help button with a good-looking icon in the launcher, also
on F1, opening the wiki manual at the section of what is on screen, done the way FlatCheck did
it (FlatCheck's ADR 0112, R-141, merged).

Facts found:

- FlatCheck: a `Help` / `Ajutor` button, icon then word, just before `About`; the icon a
  `DrawingImage` (`HelpIcon`: an accent disc, a question mark in the window's background colour,
  16 x 16) in its theme; F1 = `ApplicationCommands.Help` moved from About to Help; About keeps its
  button; the addresses in `Presentation/Manual.cs`; a failure to open the browser only logged.
- The launcher has **no tabs**: one window (top band, rows, bottom bar). Its manual is the
  wiki's Home page (`https://github.com/tcarean/AstroDarkroom/wiki`, the address `README.md`
  already links), whose section `## The window` (`#the-window`) describes exactly this window:
  the top band, the rows, the bottom bar, the About window.
- F1 opens About today: `MainWindow.xaml` binds `Command="Help"` to `ShowAbout`; the About
  button is `Command="Help"`, tooltip `Version, licence, components (F1)` (set in `Relabel()`).
  `README.md` line 29 and the wiki's "The bottom bar" say "About (F1)". No ADR names F1.
- The window has no test project (ADR 0001); tests reach the core, and `R020_...` already reads
  `MainWindow.xaml` as XML to check the window's structure. Core already holds a URL
  (`VcRuntime.DownloadUrl`).
- `MainWindow.OpenRuntimeLink` opens a link through the shell, logs `opened <address>`, and on
  failure shows a message box with the address (R-018).

Assumption (only reading, since there is no tab): the section is that of the one screen,
`#the-window`. F1 inside the About window stays unbound, as in FlatCheck.

## Requirement (draft, accepted with this plan)

**R-021**:

> The main window's bottom bar has a `Help` (`Ajutor`) button between the language selector and
> `About`: a question mark on a disc of the accent colour, then the word, with the tooltip
> `The manual, at the section of this window (F1)` (`Manualul, la sectiunea acestei ferestre
> (F1)`). The button and the F1 key, pressed in the main window, open the launcher's manual, the
> Home page of the suite's wiki, in the browser, at the section of the window,
> `Manual.Window`: `https://github.com/tcarean/AstroDarkroom/wiki#the-window`; the manual is
> English in both languages. The log has the address opened; when Windows will not open it, a
> message box gives the address, as for the runtime's link (R-018). `About` opens from its own
> button only, its tooltip `Version, licence, components` (`Versiune, licenta, componente`)
> (ADR 0030). Example: F1 in the main window, in Romanian, opens
> `https://github.com/tcarean/AstroDarkroom/wiki#the-window`.

R-012: "the Visual C++ runtime's link opened from the main window" becomes "the Visual C++
runtime's link or the manual opened from the main window". R-020: the bottom bar that stays in
view holds "the help and about buttons".

## Steps

Worktree first (nothing built in the main checkout):
`git -C E:\Photografie\Code worktree add AstroDarkroom/.claude/worktrees/Add-a-help-button-and-F1 -b claude/Add-a-help-button-and-F1 main`

1. **Requirement**: R-021 as `draft` in `docs/requirements.md`; R-012 and R-020 edited.
2. **Failing tests**, new `tests/AstroDarkroom.Tests/HelpTests.cs`:
   - `R021_TheManualOpensAtTheWindowsSection`: `Manual.Window` is the address above, the same
     in English and in Romanian.
   - `R021_F1AndTheHelpButtonOpenTheManual_AboutOpensFromItsButtonOnly`: `MainWindow.xaml` read
     as XML (as `R020_...` does): the one `CommandBinding` for `Help` runs `ShowHelp`; a
     `HelpButton` with `Command="Help"` in the bottom bar, in the column after the selector's and
     before `AboutButton`'s, holding an `Image` of `{StaticResource HelpIcon}` and the
     `HelpLabel` text; `AboutButton` has no command and `Click="ShowAbout"`; `App.xaml` has a
     `DrawingImage` keyed `HelpIcon`.
   Run them and paste the red output.
3. **ADR 0030** `docs/decisions/0030-f1-and-a-help-button-open-the-manual-at-the-window-section.md`,
   accepted 2026-11-16 (the owner's request: as FlatCheck, its ADR 0112). Decisions: the section
   is `#the-window`, the launcher having one screen; the button and icon as FlatCheck's (the
   same drawing, copied into `App.xaml`); F1 moves from About to Help; the address in the core
   beside `VcRuntime.DownloadUrl` (an address is not UI, and the tests reach only the core,
   ADR 0001); a failure gives the message box the runtime's link gives (R-018), through one
   helper for both links, where FlatCheck only logs. Rejected: the top of the Home page; a
   section per state (the runtime's warning to "When something goes wrong"); Shift+F1 for About;
   the icon alone; an icon font (Segoe Fluent Icons missing on Windows 10); a help window inside
   the program. Not done: F1 in the About window, as in FlatCheck.
4. **Code**:
   - new `src/AstroDarkroom.Core/Manual.cs`: `Manual.Page` (the wiki's Home) and `Manual.Window`
     (`Page + "#the-window"`), GPL header, XML docs, a comment that a renamed wiki heading moves
     the anchor.
   - `src/AstroDarkroom.Wpf/App.xaml`: the `HelpIcon` `DrawingImage` (FlatCheck's geometry,
     `AccentBr` disc, `WindowBg` question mark).
   - `src/AstroDarkroom.Wpf/MainWindow.xaml`: the binding `Help` → `ShowHelp`; the bottom bar's
     grid gets a fourth column; `HelpButton` (`Command="Help"`, a horizontal panel: the 16 px
     icon, then `HelpLabel`), `AboutButton` with `Click="ShowAbout"` and an 8 px left margin;
     the header comment updated.
   - `src/AstroDarkroom.Wpf/MainWindow.xaml.cs`: `Relabel()` sets `HelpLabel.Text`, the
     button's tooltip and automation name, and About's tooltip without F1; `ShowHelp` opens
     `Manual.Window` through a new private `OpenInBrowser(address)`, the body of
     `OpenRuntimeLink` moved out (same log lines, same message box), which `OpenRuntimeLink`
     now calls; `ShowAbout` becomes a click handler.
5. **Look at it**: build, render the real `MainWindow` off screen in both languages with a
   scratchpad harness (never activated, no taskbar button), check nothing is clipped or trimmed
   in the 680 px window's bottom bar, and send the PNG of the bottom bar before the commit, so
   the icon can still be changed. Check in the built-in browser that
   `https://github.com/tcarean/AstroDarkroom/wiki#the-window` lands on "The window".
6. **Docs**: `docs/design/launcher.md` (Covers gains `Manual.cs`; the bottom bar, `Relabel`,
   `ShowHelp`, `OpenInBrowser`), `docs/design/logging.md` (the `OpenInBrowser` row: the
   runtime's link and the manual), `README.md` (Help and F1; About without F1), `CHANGELOG.md`
   (Added: Help and F1; Changed: F1 no longer opens About), R-021 `implemented`, this plan saved
   as `docs/plans/2026-11-16-add-a-help-button-and-f1.md`.
7. **Wiki** (`E:\Photografie\AstroDarkroomWiki`, Home.md "The bottom bar"): Help (or F1) opens
   this manual at "The window"; About without F1. Edited in the clone only; commit and push
   asked at wrap-up, and whether to retake `img/AstroDarkRoom/main.PNG`, which shows the old bar.
8. **/wrap-up**: `dotnet build --no-incremental -warnaserror`, `scripts/verify.ps1` green, the
   whole diff read, commit on the task branch, then the code review and the merge proposed.

## Verification

- The `R021_` tests red before step 4, green after; `RepoInvariantTests` accepts R-021.
- The rendered bottom bar reads `Copyright ... | English Romana | (?) Help | About` (and
  `Ajutor`, `Despre`), the copyright not trimmed at 680 px.
- The wiki anchor checked in the browser (no gate test can read the wiki).
- A real press of F1 in the published launcher only with the owner's go.
- No golden, formula or figure changes: `/validate` not needed.

## Not in this task

- F1 in the About window; the other tools' Help buttons (one task per tool).
- No version bump (the owner's); a new feature would make it a minor one.

## Deviations (2026-11-16, at wrap-up)

- A third test, `R021_TheButtonsWordsAndTips_InBothLanguages`, reads `MainWindow.xaml.cs` as text
  and pins the button's word and the two tooltips: the plan's two tests left R-021's wordings
  unchecked, the window having no test project.
- The help button's automation name is set in `Relabel()` from its word, as FlatCheck's is.
- The wiki anchor: the live Home page carries `user-content-the-window` (read in the built-in
  browser); the browser pane was hidden, so the scroll to it was not watched.
- The real press of F1 in a published suite folder (`docs/skills.md`, `/wrap-up`) waits for the
  owner's go, when the PC is free.
