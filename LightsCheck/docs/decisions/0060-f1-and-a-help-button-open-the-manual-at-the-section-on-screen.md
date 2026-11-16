# 0060. F1 and a Help button open the manual at the section on screen

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

The window had no way to reach its manual, the LightsCheck page of the Astro Darkroom wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck`), with one section per tab and one,
"Opening a session", that explains the wait screen, Stop, the "Choose a session folder" screen
and the red problem card. F1 opened About, through `ApplicationCommands.Help`, whose default
gesture it is; the About button ran that command. The tab on screen is known to the view alone;
the view-model knows whether there is a result and whether an analysis runs. On 2026-11-16 the
owner asked for a Help button with an icon, also on F1, opening the wiki at the section of the
current tab, done the way FlatCheck did it (FlatCheck's ADR 0112).

Measured on 2026-11-16: a window's minimum width counts the frame Windows 10 draws around it,
8 px on each side, so a window 960 px wide holds content 944 px wide, and its top bar 904 px
inside its padding. With a Help button of 80 px in English and 88 px in Romanian, plus its 10 px
margin, the bar's fixed columns need 864 and 874 px while no analysis runs, and 931 and 961 px
during one, when Stop shows too: About was cut by 27 and 57 px. The owner was first asked with
the frame left out (920 px, cut by 11 and 41 px); the branch's review found the frame, and the
figures here are the corrected ones. They change none of the answers below.

## Decision

- F1 and a new `Help` (`Ajutor`) button open the manual in the browser at the section of what is
  on screen: the selected tab's when the tabs show; `Opening a session` for every screen shown
  instead of them: before a folder is chosen, during an analysis, after a stopped one, and on the
  problem card. About keeps its button and loses F1 (FlatCheck's answers, carried over by the
  owner's "the same as FlatCheck").
- F1 held down opens the manual once. WPF turns every repeat of a held key into the command,
  some thirty a second, and each would open the browser again; the window takes the repeats of
  F1 before they become the command, as ImageVisualCompare does since its R-066 (found by the
  branch's review).
- The button is an icon followed by the word, just left of About, after `Write the files...`:
  FlatCheck's place, between its folder button and About, transposed to LightsCheck's bar. The
  icon is FlatCheck's: a question mark on a disc of the theme's accent colour, drawn in XAML as a
  `DrawingImage` in the theme.
- The window's minimum width goes from 960 to 1040 px, so that the bar fits during an analysis
  too: in Romanian, with Stop, it needs 1001 px of the 1024 px of content such a window holds,
  which leaves about 23 px for the folder's column (the owner's answer of 2026-11-16 to the
  question with the measurement above).
- The folder's column, the one that gives way, keeps each of its texts on one line, cut with an
  ellipsis, as the folder's path already was. Its label and `(none chosen)` wrapped: on a render
  1040 px wide during an analysis the Romanian label broke letter by letter and the bar grew
  from 73 to 118 px (89 in English); at 960 px it already broke over three lines before this
  change (the owner's second answer of 2026-11-16, given on that render).
- The addresses live in the window project (`Presentation/Manual.cs`): the console has no tabs,
  and an address is neither a figure nor a diagnosis, so the rule "Computed only in the core"
  does not reach it. Each tab carries its section in its `Tag`; the window picks the section.
- The browser is started in one place, `Presentation/ExternalLink.cs`, for the manual and for the
  About window's links. It keeps the About window's catch: a link that cannot be opened is a
  Debug line naming the link as written, and the window stays.
- The manual is English in both languages: the wiki has no Romanian page.

## Alternatives rejected

- **Shift+F1 for About.** A second key to learn and to write in the tooltip and the manual, for a
  window opened rarely; FlatCheck's owner chose the button alone.
- **The icon alone, without the word.** It would be the window's only button without text, and
  what it does would show only in the tooltip; it would also have fitted at 960 px.
- **Stop taking the place of `Write the files...` during an analysis.** It keeps 960 px, but the
  buttons to its left would shift at every run's start and end.
- **Leaving the bar cut at 960 px during an analysis.** About would lose its end whenever the
  window is at its narrowest while an analysis runs.
- **A minimum width of about 1136 px**, at which the folder's label stays whole even during an
  analysis (put to the owner as 1120, the frame left out). The window would no longer fit a
  1366 x 768 laptop screen at 125 % (1093 px wide).
- **Letting the folder's label wrap.** The bar would grow taller at every run's start, at the
  narrowest.
- **The top of the page when no tab shows.** The person would have to look for the section;
  "Opening a session" explains every screen shown instead of the tabs.
- **An icon font (Segoe Fluent Icons, Segoe MDL2 Assets).** Segoe Fluent Icons is missing on
  Windows 10, the owner's system, and a glyph from a font cannot be checked in a test the way a
  drawing can.
- **A help window inside the program.** It would copy the wiki and drift from it; the wiki is
  already the one manual, with its screenshots.
- **FlatCheck's `ExternalLink` as it is.** It hands a relative link to the shell as written,
  which opens a file of that name in the current folder when there is one; LightsCheck's About
  window refuses it before anything starts, and keeps doing so.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page (GitHub ignores an anchor it does not know). The wiki is outside the
  repository, so no gate test can check the anchors; `docs/design/window.md` says so.
- `HelpTests` pins the seven addresses, the tabs' tags, F1 pressed in the window in each state,
  the button's icon, word, tooltip and place in both languages, and the bar's fit and height at
  the minimum width with Stop shown.
- The minimum width of 1040 px also covers the Frames tab's 980 px, a case of review entry
  CR-055, which the task that takes that entry closes (ADR 0046).
