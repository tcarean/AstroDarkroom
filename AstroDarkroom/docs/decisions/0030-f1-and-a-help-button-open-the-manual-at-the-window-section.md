# 0030. F1 and a Help button open the manual at the window's section

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

The launcher had no way to reach its manual, the Home page of the Astro Darkroom wiki
(`https://github.com/tcarean/AstroDarkroom/wiki`), whose section `The window` describes the
window's top band, rows, bottom bar and the About window. F1 opened About, through
`ApplicationCommands.Help`, whose default gesture it is, and About's tooltip said so; the window
showed no icon on any button. On 2026-11-16 the owner asked for a Help button with an icon, also
on F1, opening the wiki at the section of the tab on screen, done as FlatCheck did it
(FlatCheck's ADR 0112: an icon and the word, before About; F1 moved from About to Help; the
section of what the window shows). The launcher has no tabs: it is one screen. The window has no
test project (ADR 0001); the tests reach the core and read the window's XAML.

## Decision

- F1 and a new `Help` (`Ajutor`) button open the manual in the browser at
  `https://github.com/tcarean/AstroDarkroom/wiki#the-window`, the section of the one screen the
  launcher has. The manual is English in both languages: the wiki has no Romanian page.
- The button is FlatCheck's: the icon, a question mark on a disc of the accent colour drawn as a
  `DrawingImage` (`HelpIcon`, FlatCheck's geometry, in `App.xaml`), then the word. It sits in the
  bottom bar between the language selector and About, as FlatCheck's sits before About.
- About keeps its button and loses F1; its tooltip drops `(F1)`.
- The address lives in the core (`Manual`), beside `VcRuntime.DownloadUrl`: an address is not UI,
  so the rule "Core has no UI" does not keep it out, and there the tests can pin it.
- When Windows will not open the browser, a message box gives the address, as for the Visual
  C++ runtime's link (R-018), through one method for both links; FlatCheck only logs it, but the
  launcher already answers a failed link this way.

## Alternatives rejected

- **The top of the Home page.** The person would have to look for the window among the
  download, the tools and the log; FlatCheck's ADR 0112 rejected the top of its page too.
- **A section per state**, such as `When something goes wrong` while the runtime's warning shows.
  The warning is explained under `The top band`, inside `The window`.
- **Shift+F1 for About.** A second key to learn and to write in the tooltip and the manual, for a
  window opened rarely; FlatCheck chose the button alone.
- **The icon alone, without the word.** It would be the window's only button without text.
- **An icon font (Segoe Fluent Icons, Segoe MDL2 Assets).** Segoe Fluent Icons is missing on
  Windows 10, and a glyph cannot be checked the way a drawing can.
- **A help window inside the program.** It would copy the wiki and drift from it.

## Consequences

- F1 in the About window does nothing, as in FlatCheck: the binding is the main window's.
- A wiki heading renamed moves its anchor: `Manual.Window` must follow, or the browser lands at
  the top of the page. The wiki is outside the repository, so no gate test can check the anchor.
- `HelpTests` pins the address, the binding of F1, the button's place, icon and words, and
  About's click without a key.
