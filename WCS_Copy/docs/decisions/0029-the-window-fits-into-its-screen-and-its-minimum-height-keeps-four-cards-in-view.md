# 0029. The window fits into its screen, and its minimum height keeps four cards in view

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The window opened 700 units high at the centre of the screen, with a minimum of 620, and had no
code that looked at the screen. CR-012 of the review of 2026-11-16 read two cases. A 1920 x 1080
laptop at 150 % has a work area of about 680 units: the window would run some 20 units under the
taskbar. And at the minimum height a result with warnings may leave the cards list no room.
The entry was `to measure`. On 2026-11-18 a render of the window shown off the screen at its
minimum, 720 x 620 (704 x 581 inside), with a forced copy that gives two warnings (different
sizes, a mirrored plate solve), the drop line and the two closing lines, left the cards list
63 px in the preview and 29 px after the copy: one card, in both languages. The laptop itself was
not measured: the three screens of this PC are 1080p at 100 %. AstroDarkroom fixed the same
defect in its main window (its CR-005) by asking Windows for the work area of the window's
screen.

## Decision

The window fits into the work area of the screen it opens on once it has its handle (R-055): its
height and its minimum height are capped at the area's, and its top kept inside the area, by
`ScreenArea.WorkAreaOf` and `WindowFit.Top`, copied from AstroDarkroom (ADR 0007) into
`src/CopyWcs.Wpf/Presentation/`, since they place a window and say nothing about a copy. The
minimum height becomes 680: the same render gives the list 89 px, four cards (84 px, three, at
675). The owner chose both, the fit and the four cards, on 2026-11-18.

## Alternatives rejected

- **The fit alone, the minimum left at 620.** The one-card case stays, on every screen.
- **The whole outcome card scrolling as one.** A list that scrolls inside a page that scrolls;
  the list already scrolls, and four cards show the copy's first keys.
- **Capping the maximum height too, as AstroDarkroom's window does.** That window sizes to its
  content and cannot be resized; this one can, and a user who drags it to a taller screen keeps
  the room.

## Consequences

On a screen shorter than 680 units the minimum follows the screen, and the list has less than
four cards again; such a screen is not one the owner has. The window's opening height on this
PC's screens (work area 1040) is unchanged. Guarded by
`WindowSmokeTests.R055_TheWindowFitsIntoTheWorkAreaOfItsScreen` and
`R055_AtItsMinimumHeightTheCardsListShowsFourCards`; the call of `ScreenArea.WorkAreaOf` itself
is not tested, as in AstroDarkroom.
