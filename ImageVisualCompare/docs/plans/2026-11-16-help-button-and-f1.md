# A Help button and F1 that open the wiki manual at the section on screen

## Context

The owner asked (2026-11-16) for a Help button with a nice icon, also on F1, that opens the
wiki manual at the section of what is on screen, "as FlatCheck did it" (FlatCheck ADR 0112,
commits 6eb0d217 and 41712d3e). ImageVisualCompare has no tabs, so two questions were put and
answered:

1. **Which section** - by the view on screen: the normal view opens "The window", Histogram
   opens "Histogram", False colours opens "False colours"; while the loading veil is on screen
   (an open, Change A/B, a view switch) it opens "Opening a pair", which explains the veil.
2. **The status bar's key reminder** gets `F1 help` at its end (`F1: ajutor` in Romanian).

The manual is `https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare` (README line
23); the four anchors exist on the page today (`#the-window`, `#histogram`, `#false-colours`,
`#opening-a-pair`; checked against the wiki clone's headings).

## What is the same as FlatCheck

- A `Help` (`Ajutor`) button between `Open…` and `About`: a drawn icon, a question mark on a
  disc of the accent colour (`AccentBrush` #3FE0C8, the mark in `ChromeBrush`), 16 x 16, then
  the word. A `DrawingImage` in `Theme.xaml`, not a font glyph (Segoe Fluent Icons is missing on
  Windows 10).
- Tooltip with F1 in it. `About` keeps its button; F1 never opened About here, so nothing is
  superseded.
- Each press logs the Info line `Help opened: <address>`; a computer without a browser only
  logs that the link could not be opened.
- The manual is English in both languages.

## What differs, and why (goes in the ADR)

- **The section rule and the addresses live in Core** (`src/ImageVisualCompare.Core/Manual.cs`:
  `ManualSection`, `Manual.Page`, `Manual.Url`, `Manual.SectionFor(opening, pairShown, mode)`),
  not in the window project as in FlatCheck: this project's rule "Core has no UI" puts every
  decision the window shows in Core, unit tested without WPF; the window only reads its state
  and opens the address.
- **F1 goes through the window's `OnPreviewKeyDown`**, beside `Ctrl+O`, `H` and `F`, which already
  work wherever the focus is; not through `ApplicationCommands.Help`. A held F1 opens the manual
  once, its repeats doing nothing (as a held H, ADR 0044), so a held key does not open thirty
  browser tabs a second.
- **A dark `ToolTip` style** (implicit, in `Theme.xaml`): the application shows no tooltip today,
  and WPF's default one is light, against ADR 0018.

## Texts (English / Romanian), for the owner to see

| Where | English | Romanian |
| --- | --- | --- |
| button | `Help` | `Ajutor` |
| tooltip | `The manual, at the section about what is on screen (F1)` | `Manualul, la secțiunea despre ce e pe ecran (F1)` |
| hint, appended | ` · F1 help` | ` · F1: ajutor` |

## Steps

Branch `claude/Open-the-wiki-manual-from-a-Help-button-and-F1`, in its own worktree:
`git worktree add .claude/worktrees/Open-the-wiki-manual-from-a-Help-button-and-F1 -b claude/Open-the-wiki-manual-from-a-Help-button-and-F1 main`
(from `ImageVisualCompare/`). Plan saved to `docs/plans/2026-11-16-help-button-and-f1.md`.

1. **Requirements** (`docs/requirements.md`): new **R-066**, in the "Window and chrome" block of ten (the button, its place, icon,
   tooltip, the four sections and when each applies, F1 anywhere in the window, a held F1 opens
   once, live while nothing loads, the log line, English manual; example: in Histogram, F1 opens
   `...wiki/ImageVisualCompare#histogram`). Update R-054 (the hint), R-060 (the toolbar list),
   R-063 (Help in the list of buttons that hand the focus back; F1 anywhere), R-101 (Romanian
   example), R-121 (Help in the log).
2. **Tests first** (`tests/ImageVisualCompare.Tests/`):
   - `ManualTests.cs` (Core): the four addresses; `SectionFor` for opening / no pair / each mode.
   - `MainWindowTests`: F1 pressed on `Compare` (a `Keyboard.PreviewKeyDownEvent` with a test
     `PresentationSource`, as FlatCheck's `HelpTests.PressF1`) opens `#opening-a-pair` while a
     load stalls, then `#the-window`, `#histogram`, `#false-colours` after `ToggleModeAsync`; a
     repeat opens nothing (`OnHelpKey(isRepeat: true)`); the Help click opens the same address;
     the button holds a `DrawingImage` and the word, sits between `Open…` and `About`. The
     browser never starts: the window's `OpenAddress` delegate is replaced. `HelpButton` joins the
     R-063 focus theory (with `OpenAddress` stubbed).
   - `UiTextTests.R101_TheMainWindow_InRomanian`: `Ajutor`, the tooltip, the new hint (the
     assertion of the hint changes with R-054, the intended change).
   - `WindowLoggingTests`: `Help opened: <address>`.
3. **Code**:
   - `Core/Manual.cs` (new), `Core/UiText.cs` (`Help`, `HelpTip`, the hint).
   - `Wpf/Services/ExternalLink.cs` (new): About's `OpenLink` body moved there as
     `Open(string)` + `Open(RequestNavigateEventArgs)`, same log line; `AboutWindow` calls it.
   - `MainWindow.xaml`: the button (`HelpButton`, a `StackPanel` with the `Image` and
     `HelpText`), `ToolButton` style. `MainWindow.xaml.cs`: texts, tooltip and automation name
     from `UiText`; `ShowHelp(from)`; `OnHelpKey(isRepeat)`; F1 in `OnPreviewKeyDown`;
     `internal Action<string> OpenAddress`.
   - `Theme.xaml`: `HelpIcon` and the dark `ToolTip` style.
4. **Docs**: ADR **0056** (the owner's two answers, the three differences above, alternatives
   rejected: top of the page, ApplicationCommands.Help, a glyph font, a help window of its own);
   `docs/design/shell.md` (Covers + how Help works), `code-map.md`, `language.md` if it lists
   members; `CLAUDE.md` Layout (`Manual.cs`); `README.md` (the toolbar list, the hint, which is
   already out of date there, lacking H and F, a sentence on Help/F1); `CHANGELOG.md` Added.
5. **Run it**: an off-screen render of the real window (the scratchpad harness of the memory note
   "Off-screen WPF window render") to look at the button and the tooltip; the real launch on the
   pair and its log (the wrap-up step) only when the owner is not using the PC, asked first.
6. **Wiki** (`E:\Photografie\AstroDarkroomWiki`, a `claude/` branch): a `Help` row in the top
   bar's table (key `F1`), the hint quoted in "The status bar", a sentence on what Help opens;
   `toolbar-tools.png` and `status-bar.png` re-rendered with the harness. The eight full-window
   screenshots keep the old toolbar; said to the owner, not annotated on the page. Merge and
   push asked.
7. **Wrap-up**: `scripts/verify.ps1` green (with `--no-incremental -warnaserror` before the last
   run), read the diff, commit on the branch, offer a subagent review, propose the merge.

## Verification

- `scripts/verify.ps1` green; the new R-066 tests and the changed R-054/R-101 assertions pass.
- The off-screen render shows `? Help` between `Open…` and `About`, the icon crisp at 100 %.
- In the running app (when allowed): F1 in the normal view, after `H`, after `F`, and during a
  load opens the four addresses in the browser; the log has the four `Help opened:` lines.

## Deviations

- The requirement is **R-066**, not R-124: each block of ten IDs is one area, and the button
  belongs to "Window and chrome" (R-060 to R-065), not to the log's block.
- `status-bar.png` of the wiki was not rendered again: its crop ends at the memory readout,
  before the key reminder, so the change does not show in it. Only `toolbar-tools.png` was.
- `docs/design/testing.md` was updated too: it said a window key cannot be pressed in a test,
  which the F1 tests now do with a presentation source of their own.
- The launch of the real application on the pair (the wrap-up step of `docs/skills.md`) waits
  for the owner to be away from the PC; the off-screen render of the real window, in English
  and Romanian, showed the button, the icon and the new key reminder.
- After the two subagent reviews (c80c459d and the commit after it): the held-F1 test presses a
  real repeat through the window's key handler (`KeyEventArgs`'s internal `SetRepeat`), so
  `OnHelpKey` is private and not called by a test; `ExternalLink` hands the shell only an absolute
  http or https address (`IsWebAddress`, tested on every address the windows open) and logs
  anything else with a line of its own, `the link <text> is not a web address, so it was not
  opened`, where About's old opener had refused a relative link through `AbsoluteUri`.
