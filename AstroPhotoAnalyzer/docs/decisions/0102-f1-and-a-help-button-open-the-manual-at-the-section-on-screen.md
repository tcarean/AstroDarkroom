# 0102. F1 and a Help button open the manual at the section on screen

Date: 2026-11-16
Status: accepted 2026-11-16
Supersedes: 0023 in part (F1 opening "Despre")

## Context

The window had no way to reach its manual, which is the AstroPhotoAnalyzer page of the Astro
Darkroom wiki (`https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer`), with one
section per tab, one per viewing window, one for opening an image and one, "When something goes
wrong", whose table lists every message of the card "The analysis failed". F1 opened "Despre"
(ADR 0023), through `ApplicationCommands.Help`, whose default gesture it is. The window showed no
icon anywhere. The tab on screen is known to the view alone (`MainWindow.Tabs`); the view-model
knows whether a run lasts (`IsBusy`), whether there is a result and whether there is an error.

FlatCheck had just made the same change (its ADR 0112, 2026-11-13). On 2026-11-16 the owner asked
for a Help button with a nice icon, also on F1, opening the manual at the section of the current
tab, done the way FlatCheck did it, and answered two questions in chat: the error card opens the
table of its messages, and F1 in the viewing windows opens their own sections. The branch's
review then found two things FlatCheck shares or that the new button worsened, and the owner
chose to fix both here: F1 held down opened one browser tab per key repeat, since WPF turns every
repeat into the command; and the ICC profile's line in the top bar, in a column measured
unbounded, ran on under the buttons in a narrowed window, which the new button made happen
sooner (measured: with a 64-character profile name, below 1330 to 1370 px before, 1420 to 1460 px
with the button).

## Decision

- F1 and a new `Help` (`Ajutor`) button open the manual in the browser at the section of what is
  on screen, in the order the window stacks its right side: the opening of an image
  (`#opening-an-image`) while a run lasts, its overlay covering the whole right side; the
  selected tab's section when the tabs show; `#when-something-goes-wrong` on the error card (the
  owner's answer, where FlatCheck sends its card to its opening section, because that is where
  FlatCheck's manual explains its card); `#opening-an-image` before any image. Where the tabs and
  the card both show (CR-040, open), the tabs are drawn over the card and win.
- "Despre" keeps its button and loses F1, its tooltip losing the "(F1)", as in FlatCheck.
- The button is an icon followed by the word, just left of `About`. The icon is FlatCheck's: a
  question mark on a disc of the theme's accent colour, drawn in XAML as a `DrawingImage` in the
  theme, with FlatCheck's reasons (no icon font: Segoe Fluent Icons is missing on Windows 10, and a
  glyph cannot be checked in a test the way a drawing can).
- F1 in the four viewing windows opens their own sections (the owner's answer): the dust / sky
  map, the preview and the curve window their headings; the soft proof's full screen, which has no
  heading of its own, `#soft-proof`, whose "Full screen" item explains it. They get no button: a
  viewing window is for looking, and its few controls stay its own.
- The addresses live in the window project (`Presentation/Manual.cs`), as in FlatCheck: the
  console has no tabs, and an address is neither a figure nor a diagnosis, so the rule "Computed
  only in the core" does not reach it. Each tab carries its section in its `Tag`; one method writes
  the log line and opens the address for all five windows, through `Presentation/ExternalLink.cs`,
  where the "Despre" window's link opening moves so that the two share one catch.
- F1 held down opens the manual once: each window that has the Help command remembers that F1
  is down from its key-down until its key-up or until the window loses the focus, and takes the
  key-downs in between in its preview pass, before WPF turns them into the command
  (`Manual.IgnoreHeldF1`). WPF's own `IsRepeat` is not used: it calls a key-down a repeat
  whenever the last key-down it saw was the same key, so when the browser, come to the front,
  took the key-up, it swallowed the next press (the second review of the branch). FlatCheck
  keeps one tab per repeat until it is changed there.
- The profile's line is trimmed at any width of the window: its text column is a star column,
  measured against the room left, in a left-aligned grid, so a short line keeps `Scoate` right
  after it; the bar's middle column stops 20 px short of the buttons (R-155).
- The manual is English in both languages: the wiki has no Romanian page.

## Alternatives rejected

- **`#opening-an-image` for the error card**, as FlatCheck's rule would give for every screen
  without tabs. That section only names the card and sends the reader on to the table; the owner
  chose the table.
- **F1 in the main window only**, as in FlatCheck. FlatCheck has no viewing windows with sections
  of their own; here F1 in them would do nothing, while the manual explains each of them.
- **Shift+F1 for "Despre"**, **the icon alone**, **the top of the page when no tab shows**, **a help
  window inside the program**: rejected for FlatCheck (its ADR 0112), for the same reasons here.

## Consequences

- A wiki heading renamed moves its anchor: `Manual.cs` must follow, or the browser lands at the
  top of the page (GitHub ignores an anchor it does not know). The wiki is outside the
  repository, so no gate test can check the anchors; `docs/design/window.md` says so.
- `HelpTests` pins the addresses, the tabs' tags, F1 pressed in the main window in each of its
  states, from a tab, a field and a button, and in the four viewing windows, F1 held down, the
  button's click, icon, word, tooltip and place in both languages at the window's minimum
  width, and the profile's line shortened before the buttons.
- The "From ICC profile..." and "Despre" windows get no F1: neither has a section of its own (the
  first is explained under "The parameters").
