# 0112. F1 and a Help button open the manual at the section on screen

Date: 2026-11-13
Status: accepted 2026-11-13
Supersedes: 0040 in part (F1 opening "Despre")

## Context

The window had no way to reach its manual, which is the FlatCheck page of the Astro Darkroom
wiki (`https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck`), with one section per tab and
one for opening a session and for the session folder. F1 opened "Despre" (ADR 0040), through
`ApplicationCommands.Help`, whose default gesture it is. The window showed no icon anywhere, only
text buttons. The tab on screen is known to the view alone (`MainWindow.Tabs`); the view-model
knows whether a folder is open, whether it has a problem and whether the opening runs. On
2026-11-13 the owner asked for a Help button with an icon, also on F1, opening the manual at the
section of the current tab, and answered three questions in chat.

## Decision

- F1 and a new `Help` (`Ajutor`) button open the manual in the browser at the section of what is
  on screen: the selected tab's; `The session folder` on the problem card; `Opening a session`
  before a folder is chosen and while the opening's analyses run. "Despre" keeps its button and
  loses F1 (the owner's answers 1 and 3).
- The button is an icon followed by the word, between `Choose folder...` and `About` (answer 2).
  The icon, a question mark in a circle in the theme's accent colour, is drawn in XAML as a
  `DrawingImage` in the theme.
- The addresses live in the window project (`Presentation/Manual.cs`): the console has no tabs,
  and an address is neither a figure nor a diagnosis, so the rule "Computed only in the core"
  does not reach it. Each tab carries its section in its `Tag`; the window picks the section and
  opens it through `ExternalLink`, which only logs a computer without a browser.
- The manual is English in both languages: the wiki has no Romanian page.

## Alternatives rejected

- **Shift+F1 for "Despre".** A second key to learn and to write in the tooltip and the manual, for
  a window opened rarely; the owner chose the button alone.
- **The icon alone, without the word.** It would be the window's only button without text, and
  what it does would show only in the tooltip.
- **The top of the page when no tab shows.** The person would have to look for the section; the
  two states without tabs each have a section that explains them.
- **An icon font (Segoe Fluent Icons, Segoe MDL2 Assets).** Segoe Fluent Icons is missing on
  Windows 10, the owner's system, and a glyph from a font cannot be checked in a test the way a
  drawing can.
- **A help window inside the program.** It would copy the wiki and drift from it; the wiki is
  already the one manual, with its screenshots.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page (GitHub ignores an anchor it does not know). The wiki is outside the
  repository, so no gate test can check the anchors; `docs/design/two-applications.md` says so.
- `HelpTests` pins the eight addresses, the tabs' tags, the command and F1 in the window, and the
  button's icon, word and place in both languages.
- The other tools of the suite keep F1 on "Despre" until they get the same button, each in a task
  of its own.
