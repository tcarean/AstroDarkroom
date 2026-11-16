# 0056. Help and F1 open the wiki manual at the section of the view on screen

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

The window had no way to reach its manual, the ImageVisualCompare page of the Astro Darkroom
wiki (`https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare`, linked from the README
since ADR 0050). F1 did nothing. On 2026-11-16 the owner asked for a Help button with a nice
icon, also on F1, opening the manual at the section of the current tab, as FlatCheck does (its
ADR 0112). This window has no tabs, so two questions were put in chat and answered:

1. Which section: the one of the view on screen (the recommendation). The normal view opens
   "The window", which explains the top bar, the comparison area and the status bar;
   "Histogram" and "False colours" open their own sections; while the loading veil is on screen
   (an open, a change of one image, a switch of view) it opens "Opening a pair", which explains
   the veil.
2. The status bar's key reminder ends with `F1 help` (`F1: ajutor`), the recommendation.

## Decision

- A `Help` (`Ajutor`) button between `Open…` and `About`, with FlatCheck's icon, a question mark
  on a disc of the accent colour drawn as a `DrawingImage` in `Theme.xaml`, then the word, and a
  tooltip that names F1. It and F1 open the manual through `ExternalLink`, which only logs a
  computer without a browser; `About` keeps its button and was never on F1. The manual is English
  in both languages, the wiki having no Romanian page.
- The section rule and the addresses are Core's (`Manual.cs`), not the window project's as in
  FlatCheck: this project's rule "Core has no UI" puts each decision the window shows in Core,
  tested without WPF. The window hands it three facts (a pair being loaded, a pair shown, the
  mode it is shown in) and opens the address.
- F1 is read in the window's `PreviewKeyDown`, beside `Ctrl+O`, `H` and `F`, which already work
  wherever the focus is, and a repeat of a held F1 does nothing, as for a held H (ADR 0044).
- The application gets an implicit `ToolTip` style in its palette, FlatCheck's; Help's tooltip is
  the first the window shows, and WPF's default one is light (ADR 0018).

## Alternatives rejected

- **The top of the page.** The reader would look for the section; the view on screen says which
  one they need.
- **"Images of different sizes" for a padded pair.** Offered to the owner, not chosen; the normal
  view's section covers the status bar's `padded` reading and links that section.
- **`ApplicationCommands.Help`, as FlatCheck has it.** Its gesture fires on every repeat of a held
  key, one browser tab each, and the window's other keys are read in `PreviewKeyDown` already.
- **A glyph of Segoe MDL2 Assets** (the font of the Change buttons' icon). One colour only, while
  FlatCheck's drawn icon carries the accent and keeps the suite's two Help buttons alike.
- **A help window inside the program.** It would copy the wiki and drift from it.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page. The wiki is outside the repository, so no gate test can read the anchors;
  `ManualTests` pins the four addresses.
- The wiki manual's top bar table, its status bar text and its two crops of the bars change with
  this decision (ADR 0050).
