# 0016. Help and F1 open the wiki manual at the section of what the outcome card shows

Date: 2026-11-16
Status: accepted 2026-11-16
Supersedes: 0008 in part (About alone on the band's right, opened by F1); 0009 in part (F1 in the About tooltip)

## Context

The window had no way to reach its manual, the CopyWcs page of the Astro Darkroom wiki
(`https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs`, linked from the README since ADR
0015); F1 opened About through `ApplicationCommands.Help` (ADR 0008). On 2026-11-16 the owner
asked for a Help button with a nice icon, also on F1, opening the manual at the section of the
current tab, as FlatCheck does (its ADR 0112). CopyWcs has no tabs: the page has one section
for the window, with a sub-section `The result`, and `When something goes wrong`, a table with
one row per refusal and per warning. Asked which section opens, the owner chose the one of what
the outcome card shows (the recommendation): `The window` before any Preview or Copy, `The
result` when the card shows a result, `When something goes wrong` when it shows `Refused` or a
warning. With Help in the band, its explanation needs 787 px inside the window, 803 px with
the frame, to stay on two lines (measured in both languages); the window opened 800 px wide, so
it opened with three lines, the band 70 px high instead of 57. Asked with renders of both, the
owner chose to open the window 820 px wide (the recommendation).

## Decision

- A `Help` (`Ajutor`) button left of `About` in the band, FlatCheck's drawn icon (a question
  mark on a disc of the accent colour, a `DrawingImage` in `Theme/Dark.xaml`, taken from
  AstroDarkroom's `App.xaml`, whose palette that file already copies) then the word, and the
  tooltip `The manual, at the section about what is on screen (F1)`, ImageVisualCompare's
  words. It and F1 open the manual at the section the card calls for. About keeps its button
  and loses F1, as in FlatCheck, and its tooltip loses the `(F1)` of ADR 0009's `Version,
  licence, components (F1)`. The manual is English in both languages: the wiki has no
  Romanian page.
- The card keeps the last outcome while a run goes, so the section follows the card, not the
  run.
- The window opens 820 px wide instead of 800, so the band keeps two lines and 57 px at the
  size it opens with; the minimum width stays 720 px, where the band takes three lines.
- F1 is read in the window's `PreviewKeyDown`, and a repeat of a held F1 does nothing, as in
  ImageVisualCompare (its ADR 0056): `ApplicationCommands.Help`, FlatCheck's way, runs on every
  repeat of a held key, a browser tab each. The button is a plain `Click`.
- The addresses and the section rule live in the window project (`Presentation/Manual.cs`), as
  FlatCheck's: the console has no manual link, and an address is no sentence about the copy, so
  the rule "The window computes nothing" does not reach it. The window hands the rule the
  card's three facts (an outcome, a refusal, a warning) and opens the address through
  `Presentation/ExternalLink`, which hands the shell only an absolute http or https address
  (ImageVisualCompare's guard) and only logs a computer that cannot open it. The About window
  keeps its own link handler, a piece copied from DarksCheck (ADR 0007), as DarksCheck's own
  Help task kept it.

## Alternatives rejected

- **The top of the window's section whatever the card shows**, as AstroDarkroom's launcher
  does. A refusal is explained in its own table row; the reader would scroll to it.
- **A warning opening `The result`**, since the copy went on. Offered to the owner, not chosen:
  each warning has its row, with what to do, under `When something goes wrong`.
- **`ApplicationCommands.Help` for the button and F1.** A held F1 would open one browser tab
  per repeat, some thirty a second.
- **An icon font (Segoe Fluent Icons, Segoe MDL2 Assets).** Segoe Fluent Icons is missing on
  Windows 10, and the drawn icon keeps the suite's Help buttons alike.
- **The icon alone, without the word.** The band's only button without text; what it does would
  show only in its tooltip.
- **A help window inside the program.** It would copy the wiki and drift from it.
- **Keeping the window 800 px wide.** It would open with the explanation's last words alone on
  a third line and the outcome card 13 px shorter.
- **A minimum width of 820 px too.** Two lines at every width, but 100 px less to narrow the
  window on a small screen, for 13 px of band.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page (GitHub ignores an anchor it does not know). The wiki is outside the
  repository, so no gate test can read the anchors; `HelpTests` pins the three addresses.
- `HelpTests` press F1 through the window's key handler, a repeat included, and click the
  button over each state of the card; R-038's test finds Help, then About without the Help
  command, and lays the band out at the opening width less the frame: a longer explanation or a
  wider button that brings the third line back fails there. `WindowLoggingTests` reads the
  `Help opened: <address>` line.
- The wiki page's "The top band" says F1 opens the manual; its screenshot keeps the band
  without Help until it is taken again.
