# 0061. F1 and a Help button open the manual at the section on screen

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

The window had no way to reach its manual, the DarksCheck page of the Astro Darkroom wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck`), with one section per tab, one on
opening a folder (the wait screen and a stopped run included) and one, "When something goes
wrong", whose table gives each message of the problem card and what to do. F1 opened About,
through `ApplicationCommands.Help`, whose default gesture it is. On 2026-11-16 the owner asked for
a Help button with an icon, also on F1, opening the manual at the section of the current tab,
the way FlatCheck does it (FlatCheck's R-141 and ADR 0112), and answered two questions in chat.

Measured (temporary tests in the window's text mode, and the real `MainWindow` shown off-screen).
A window's `MinWidth` counts its frame, 8 px a side on this Windows 10 at 100 % scaling: the
real window at a `MinWidth` of 1160 px had 1144 px of content. (The branch's review read 6.5 to
7.3 px a side at 125 to 200 %, so a test that takes 16 px off never lays out wider than the real
window; Windows 11 was not measured.) The folder's column takes what the others leave, and its
heading, `SIRIL SESSION FOLDER OR LIBRARY OF DARKS`, wraps there; the buttons are cut only once
that column is down to nothing. With 960 px inside, the column had 33 px (43 in Romanian), the
heading nine lines and the bar 190 px (162 in Romanian) instead of 66; the real window at the
old minimum, 960 px, has 16 px less in that column and About still fits. The Help button is
80 px wide in English and 88 px in Romanian. The Frames tab needs 1020 px inside (20 + 620 +
360 + 20) to show without being cut. With Help, the bar's height against the width inside,
English / Romanian: 743 / 836 px at 1020, 257 / 242 at 1040, 162 / 162 at 1060, 95 / 95 at
1100, 69 / 82 at 1144, 69 / 69 at 1160, 66 / 66 at 1300 (the heading on one line). The real
window, at 1 px steps: a 69 px bar in both languages from a `MinWidth` of 1169 px (1153 inside),
82 px in Romanian up to 1168; with Help, About cut by 73 / 71 px at 960 and 13 / 11 px at 1020,
fitting from 1036 px (1020 inside).

## Decision

- As in FlatCheck: F1 and a new `Help` (`Ajutor`) button open the manual in the browser at the
  section of what is on screen; About keeps its button and loses F1. The button is an icon
  followed by the word, the icon a question mark on a disc of the theme's accent colour, drawn
  in XAML as a `DrawingImage` (FlatCheck's drawing). The manual is English in both languages: the wiki has no Romanian
  page. The addresses live in the window project (`Presentation/Manual.cs`): the console has no
  tabs, and an address is neither a figure nor a diagnosis, so the rule "Computed only in the
  core" does not reach it. Each tab carries its section in its `Tag`.
- The problem card opens "When something goes wrong" (the owner's answer 1): that table explains
  each message of the card and what to do, where "Opening a folder" only mentions the card. The
  wait screen, the empty window before a folder and the empty content after a stopped run open
  "Opening a folder", which explains all three.
- The window's minimum width goes from 960 to 1180 px, 1164 inside the frame (the owner's answer
  2, given a third time): the whole bar shows with Help in both languages, the folder's heading
  on at most two lines as at any wider width, with 11 px to spare, and the Frames tab is not cut. The
  owner first chose 1020 px, on the fit of the buttons alone, then 1160 px on the bar's heights,
  measured inside the frame but read as the window's; each new fact went back to the owner as a
  new question, the second one found by the branch's review.
- Help sits just before About, the last of the bar's actions, as in FlatCheck.
- The theme (`Theme/Dark.xaml`) and the About window are LightsCheck's copies, improved there
  first (ADR 0009), so neither changes here: the icon is a resource of the main window, and Help
  opens its address through a new `Presentation/ExternalLink.cs` that handles a failure as
  About's own link handler does (a computer without a browser is only a Debug line of the log).

## Alternatives rejected

- **"Opening a folder" for the problem card** (FlatCheck's choice, where that section explains
  the card). Here the card's reasons are in "When something goes wrong"; the person would have
  to follow one more link.
- **Keeping 960 px.** At the minimum width About would be cut by 73 px (71 in Romanian); it fits
  only from 1036 px up. The cost of 1180 px: the window no longer fits half of a 1920 px screen.
- **1160 px**, the owner's second answer. Inside the frame it leaves 1144 px, at which the
  Romanian heading takes three lines and the bar 82 px.
- **1020 px**, the owner's first answer, chosen on widths measured inside the frame. With
  1020 px inside, the buttons fit but the folder's column keeps 3 to 5 px, its heading wraps one
  letter a line and the bar grows to 743 px (836 in Romanian); the real window at 1020 px also
  cuts About by 13 px (11 in Romanian), its bar 743 px (864 in Romanian).
- **1020 px with the folder's heading on one line, cut with "..."**. The bar keeps its height at
  any width, but at the minimum width neither the heading nor the folder's name shows at all.
- **The icon alone, without the word.** It saves the word and its gap, 36 px (44 in Romanian),
  of the 73 (71) by which About would be cut at 960 px; it would also be the window's only button
  without text, and what it does would show only in the tooltip (FlatCheck's reason too).
- **An icon font (Segoe Fluent Icons, Segoe MDL2 Assets).** Segoe Fluent Icons is missing on
  Windows 10, the owner's system, and a glyph cannot be checked in a test the way a drawing can.
- **A help window inside the program.** It would copy the wiki and drift from it; the wiki is
  already the one manual, with its screenshots.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page (GitHub ignores an anchor it does not know). The wiki is outside the
  repository, so no gate test can check the anchors.
- `HelpTests` pins the ten addresses, the tabs' tags, F1 pressed in the window, the button's
  icon, word and place in both languages, the bar's fit and height at 1180 px less the frame,
  and the Debug line of an address that cannot be opened.
