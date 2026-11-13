# 0112. F1 and a Help button open the manual at the section on screen

Date: 2026-11-13
Status: accepted 2026-11-13
Supersedes: 0040 in part (F1 opening "Despre")

## Context

The window had no way to reach its manual, which is the FlatCheck page of the Astro Darkroom
wiki (`https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck`), with one section per tab and
one for opening a session, which also explains the card shown when a folder cannot be used. F1
opened "Despre" (ADR 0040), through `ApplicationCommands.Help`, whose default gesture it is. The window showed no icon anywhere, only
text buttons. The tab on screen is known to the view alone (`MainWindow.Tabs`); the view-model
knows whether a folder is open, whether it has a problem and whether the opening runs. On
2026-11-13 the owner asked for a Help button with an icon, also on F1, opening the manual at the
section of the current tab, and answered three questions in chat. The branch's review then
found that the problem card's explanation is under "Opening a session", not under "The session
folder" (the folder's layout), where answer 3 had sent it; asked again, the owner chose
"Opening a session".

## Decision

- F1 and a new `Help` (`Ajutor`) button open the manual in the browser at the section of what is
  on screen: the selected tab's when the tabs show; `Opening a session` for everything shown
  instead of them: before a folder is chosen, while the opening's analyses run, and on the
  problem card. "Despre" keeps its button and loses F1 (the owner's answers 1 and 3, the last as
  revised after the review).
- The button is an icon followed by the word, between `Choose folder...` and `About` (answer 2).
  The icon, a question mark on a disc of the theme's accent colour, is drawn in XAML as a
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
- **The top of the page when no tab shows.** The person would have to look for the section;
  "Opening a session" explains every screen shown instead of the tabs.
- **`The session folder` for the problem card** (answer 3 as first given). That section is the
  folder's layout; the card's reasons and the Siril script are under "Opening a session".
- **A heading of its own for the problem card in the wiki** (`#when-the-folder-cannot-be-used`).
  The owner kept the wiki's structure as it is.
- **An icon font (Segoe Fluent Icons, Segoe MDL2 Assets).** Segoe Fluent Icons is missing on
  Windows 10, the owner's system, and a glyph from a font cannot be checked in a test the way a
  drawing can.
- **A help window inside the program.** It would copy the wiki and drift from it; the wiki is
  already the one manual, with its screenshots.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page (GitHub ignores an anchor it does not know). The wiki is outside the
  repository, so no gate test can check the anchors; `docs/design/two-applications.md` says so.
- `HelpTests` pins the seven addresses, the tabs' tags, F1 pressed in the window, and the
  button's icon, word and place in both languages.
- The other tools of the suite keep F1 on "Despre" until they get the same button, each in a task
  of its own.
