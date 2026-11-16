# Add a Help button and F1 that open the manual at the section on screen

## Context

The AstroPhotoAnalyzer window has no way to reach its manual, which lives in the suite's wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer`, one section per tab). On
2026-11-16 the owner asked for a Help button with a nice icon, also on F1, opening the manual at
the section of the current tab, "the way FlatCheck did it" (FlatCheck R-141, ADR 0112, commits
6eb0d217 and 41712d3e).

Facts found (2026-11-16):

- F1 opens `About` today: `MainWindow.xaml` binds `ApplicationCommands.Help` (default gesture F1)
  to `ShowAbout`; R-110, R-119, ADR 0023, the button's tooltip, `docs/design/window.md`, the
  README and the wiki's "The top bar" say so.
- The window has no icon anywhere; `Theme/Dark.xaml` has the same `AccentBr` (#4EA8DE) and
  `WindowBg` that FlatCheck's icon is drawn with.
- The right side stacks, in one grid cell: the invitation (`!HasResult && !HasError`), the error
  card (`HasError`), the tabs (`HasResult`, drawn over the card), `BusyOverlay` over all while
  `IsBusy`. During a run `HasResult` is false, so the invitation is under the overlay. A refused
  field with a result on screen leaves both `HasError` and `HasResult` true and the tabs cover the
  card: that is CR-040, open; Help follows what is drawn, the tabs.
- The wiki page's headings (contents list and the `[The Curve window](#the-curve-window)` link
  confirm the form): `#report`, `#tones`, `#export`, `#resolution`, `#colour`, `#curves-editing`,
  `#colours-for-paper`, `#soft-proof`, `#crop-with-aspect-ratio`, `#opening-an-image`,
  `#when-something-goes-wrong`, `#the-preview-window`, `#the-dust--sky-map-window` (GitHub drops
  the `/`, leaving two hyphens), `#the-curve-window`. The full-screen soft proof has no heading of
  its own: it is the "Full screen" bullet of `Soft proof`. The wiki is English only.
- `AboutWindow.OpenLink` opens a link through the shell and only logs a failure; FlatCheck moved
  the same code into `Presentation/ExternalLink.cs` and reused it for the manual.
- The viewing windows (`Views/OverlayWindows.xaml.cs`: `MapWindow`, `PreviewWindow`,
  `CurveWindow`, `SoftProofWindow`) handle only Esc (and G, O, I in the soft proof); F1 does nothing
  there.

Owner's answers in this session (they make ADR 0102 `accepted 2026-11-16`):

1. "Do as FlatCheck": F1 opens the manual; `About` keeps its button and loses the key; the button
   is the icon followed by the word, just before `About`.
2. The red card "The analysis failed" opens `#when-something-goes-wrong` (the table of its
   messages), not `#opening-an-image`.
3. F1 in the four viewing windows opens their own section too (key only, no button there).

## Texts (new, shown for the rule "Plain words")

| Where | English | Romanian |
| --- | --- | --- |
| Help button | `Help` | `Ajutor` |
| Help tooltip (new) | `The manual, at the section of this tab (F1)` | `Manualul, la sectiunea tabului curent (F1)` |
| About tooltip, today | `Version, licence, components (F1)` | `Versiune, licenta, componente (F1)` |
| About tooltip, proposed | `Version, licence, components` | `Versiune, licenta, componente` |

The same words as FlatCheck's, so the suite says one thing.

## Requirement (draft, accepted with this plan)

**R-186** (status `draft` until the tests pass):

> The top bar has a `Help` (`Ajutor`) button between `Save the report...` and `About`: a question
> mark on a disc of the theme's accent colour, drawn, then the word. The button and the F1 key,
> pressed anywhere in the main window, open AstroPhotoAnalyzer's manual in the system's browser,
> `https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer`, at the section of what is on
> screen: the selected tab's when the tabs show (`#report`, `#tones`, `#export`, `#resolution`,
> `#colour`, `#curves-editing`, `#colours-for-paper`, `#soft-proof`, `#crop-with-aspect-ratio`),
> `#when-something-goes-wrong` on the card "The analysis failed", and `#opening-an-image` before
> an image is analysed and while an analysis runs. F1 in the viewing windows opens theirs: the
> dust / sky map `#the-dust--sky-map-window`, the preview `#the-preview-window`, the curve window
> `#the-curve-window`, the soft proof full screen `#soft-proof`. The manual is English in both
> languages. Tooltips as in the table above; `About` opens from its button alone (R-119). The
> button is live during an analysis, as `About` is. Each press writes the Info line
> `Help opened: <address>` to the log (R-171); a computer without a browser only logs that the
> address could not be opened, as for any link (ADR 0102). Example: with the Export tab selected,
> F1 opens `...wiki/AstroPhotoAnalyzer#export`; on the card of a linear image it opens
> `...#when-something-goes-wrong`.

**R-110** and **R-119** lose "(also F1)" / "or F1", pointing at R-186.

## Steps

Worktree first (nothing is built in the main checkout):
`git -C E:\Photografie\Code worktree add AstroPhotoAnalyzer/.claude/worktrees/Add-a-help-button-to-AstroPhotoAnalyzer -b claude/Add-a-help-button-to-AstroPhotoAnalyzer main`
(the name first planned, `claude/Add-a-help-button-and-F1`, was already the launcher's branch for
the same change, made in a session of its own)

1. **Requirement.** `docs/requirements.md`: R-186 as `draft`; R-110, R-119 edited.
2. **Failing tests**, new `AstroPhotoAnalyzer.Wpf.Tests/HelpTests.cs` (collection `fereastra`,
   `StaFixture`, the `OpenAddress` stand-in so no browser starts, F1 raised as a real key-down as
   FlatCheck's `PressF1`):
   - `R186_EachStateAndTabHasItsSectionOfTheManual`: theory over
     `Manual.SectionFor(isBusy, hasResult, hasError, tab)` and `Manual.Url`, every address written
     out, including busy-over-everything and the CR-040 case (result and error: the tab).
   - `R186_TheNineTabsCarryTheirSections`: the nine `TabItem`s' `Tag`s, in order.
   - `R186_HelpAndF1OpenTheManualAtTheSectionOnScreen`: one `MainWindow`; F1 before an image
     (`#opening-an-image`), during a run started and not awaited (`#opening-an-image`), on the
     card of a missing file (`#when-something-goes-wrong`), on the Export and Crop tabs of a
     `DarkTiff` result; the log's `Help opened:` line.
   - `R186_F1InTheViewingWindowsOpensTheirSections`: the four windows, built as
     `OverlayWindowTests` builds them.
   - `R186_TheHelpButtonShowsAnIconAndTheWord` (both languages): the window's content in a host
     `Border` (an unshown window renders white) at the minimum width, 1000 px; the button runs
     `ApplicationCommands.Help`, shows a `DrawingImage` and the word, its tooltip; `About` has no
     command, its tooltip without F1, ends inside the bar's 20 px padding; order
     `Save the report...`, `Help`, `About`.
   Run them, paste the failing output.
3. **ADR 0102**, `docs/decisions/0102-f1-and-a-help-button-open-the-manual-at-the-section-on-screen.md`,
   accepted 2026-11-16, `Supersedes: 0023 in part (F1 opening "Despre")`; ADR 0023's Status line
   gains `; F1 opening "Despre" superseded by 0102` (the kit's ADR 0024). It cites FlatCheck's
   ADR 0112 for the shared choices (drawn icon, no icon font, addresses in the window project:
   an address is neither a figure nor a diagnosis, so "Computed only in the core" does not reach
   it) and records this project's own: the error card's section, the viewing windows, the
   busy state.
4. **Code** (window project only; the core and the console do not change):
   - new `Presentation/Manual.cs`: `public enum ManualSection` (14 values), `internal static class
     Manual` with `Page`, `Url(section)`, `SectionFor(...)` and `Open(section, openAddress)`,
     which writes the log line and calls the opener (one place for all five windows).
   - new `Presentation/ExternalLink.cs`: `Open(RequestNavigateEventArgs)` and `Open(string)`, the
     body of today's `AboutWindow.OpenLink` (same catch, same Debug line); `AboutWindow` calls it.
   - `Theme/Dark.xaml`: `HelpIcon`, FlatCheck's `DrawingImage` (disc in `AccentBr`, question mark
     and dot in `WindowBg`, 16 x 16).
   - `MainWindow.xaml`: `Help` bound to `ShowHelp`; the new button; `About` becomes
     `Click="ShowAbout"`; each `TabItem` gets `Tag="{x:Static local:ManualSection.Report}"` etc.
   - `MainWindow.xaml.cs`: `ShowHelp` (selected tab's `Tag`, `_vm.IsBusy/HasResult/HasError`),
     `internal Action<string> OpenAddress = ExternalLink.Open`, `ShowAbout` as a click handler.
   - `Views/OverlayWindows.xaml.cs`: each of the four windows adds a `Help` command binding to
     `Manual.Open(<its section>, OpenAddress)` and the same `OpenAddress` property.
   XML doc comments on every type and method added or edited; GPL headers on the new files.
5. **Look at it.** `APA_DUMP_SCREENS=<scratchpad> dotnet test AstroPhotoAnalyzer.Wpf.Tests
   --filter "WindowSmokeTests|HelpTests"`, read the top bar's PNG in both languages, and send it
   to the owner before the commit, so the icon can still change.
6. **Docs.** `docs/design/window.md` (how the section is chosen, the stacking order it follows,
   anchors and wiki headings move together and no gate checks them; `Covers:` gains the two new
   files), `docs/design/logging.md` (the `Help opened` line), `README.md` (Help and F1, About
   alone, F1 in the viewing windows), `CHANGELOG.md` (Added: Help and F1; Changed: F1 no longer
   opens About), R-186 to `implemented`, this plan saved as
   `docs/plans/2026-11-16-add-a-help-button-and-f1.md`.
7. **Wiki** (`E:\Photografie\AstroDarkroomWiki`, its own repository): "The top bar" gains Help
   and drops F1 from About; the four windows' sections say F1 opens them. Edited in the clone
   only; its commit and push are asked at wrap-up, and so is retaking the top-bar screenshots.
8. **/wrap-up.** `dotnet build --no-incremental -warnaserror`, `scripts/verify.ps1` green,
   `scripts/check-design.ps1 -Since main`, read the whole diff, commit on the task branch, offer
   the subagent code review, then propose the merge.

## Verification

- The five `R186_` tests fail before step 4 and pass after; `RepoInvariantTests` accepts R-186;
  the existing About and window tests stay green.
- The 14 anchors read from the live wiki page in the built-in browser (the heading ids GitHub
  made), since no gate test can read the wiki.
- The rendered top bar shows `Open image... | Save the report... | (?) Help | About` (`Ajutor` in
  Romanian), nothing clipped at 1000 px.
- With the owner's go, one real press in the built window: F1 on the Export tab, the browser at
  `#export`.
- No golden file, no formula, no figure changes: `/validate` is not needed.

## Not in this task

- CR-040 (a refused field drawn behind the tabs) stays open; Help only follows what is drawn.
- The "From ICC profile..." and About windows get no F1.
- No version bump (the owner's); a new feature would make it a minor one.
