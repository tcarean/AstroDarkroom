# 0068. The soft proof full screen is borderless, with its panel shown by hover

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The Soft proof tab's `Ecran intreg` (R-163, ADR 0066) opened a maximised window: the title bar
with its three buttons, a bar at the top with the controls, the caption, the message and the
key hint, and the proof under the bar. On 2026-10-30 the owner asked for a true full screen:
no title, no minimise or maximise buttons, the panel of controls hidden except while the mouse
is over its zone, and the way out by Esc or by a new Close button in that panel. The window is
for looking at a print simulation; a title bar and a permanent bar of controls take screen from
the image and put the program's chrome beside the paper's white.

## Decision

`SoftProofWindow` is a borderless window over the whole screen (`WindowStyle` `None`,
`WindowState` `Maximized`, `ResizeMode` `NoResize`: the combination WPF fills the screen with,
taskbar included, without any extent past the screen's edge). The proof and its margin fill the
window. The former bar becomes a panel laid over the top of the image, with the button
`Inchide` (`Close`) at its right end, and is hidden until the mouse is over the band it
occupies (R-164).

The band is the panel's own area: a transparent `Border` at the top of the window holding the
panel, whose `Visibility` is `Hidden` rather than `Collapsed`, so the band keeps the panel's
height and there is something to point at. The band's `MouseEnter` and `MouseLeave` set the
window's `PanelShown`: the panel shows the moment the mouse enters the band and hides the
moment it leaves. No delay, no fade, no timer, no toggle key.

Esc still closes (R-163); `Inchide` does the same, for whoever reaches for the mouse. The
window keeps its title, which the taskbar shows.

## Alternatives rejected

- **A timer: the panel shows on any mouse move and hides after a few seconds still**, as
  video players do. It needs a delay to tune, hides the panel while the user is reading it,
  and shows it over the image at every nudge of the mouse. The owner asked for the panel over
  its zone, which a band at the top gives exactly.
- **A toggle key for the panel** (Tab, as in Lightroom). A key is invisible: nothing on screen
  says it exists, and the window already has four keys to remember. The band is found by
  moving the mouse up, which is what a user does to look for controls.
- **A thin hot strip at the very top edge, the panel sliding out below it.** A few pixels are
  hard to hit; a band of the panel's own height is not, and costs nothing while hidden, since
  the proof is drawn under it.
- **Keeping the title bar and hiding only the controls.** The owner asked for the title bar
  gone; a borderless maximised window is the one way WPF gives to cover the taskbar.

## Consequences

- R-164 added; R-163's sentence about the window points at it. `SoftProofWindowTests` covers
  the window's three properties, the hidden panel and its band, and the Close button; the hover
  itself is WPF's and is not driven in the tests.
- The dark title bar helper is no longer called for this window: there is no title bar.
- The smoke test renders the window with `PanelShown` true, so the PNG shows the panel.
- A tooltip or the margin list's drop-down opened from the panel is part of the band's subtree
  for `IsMouseOver`, so hovering them keeps the panel shown; this was not measured on a live
  window in the task's session, and is the first thing to check if the panel is seen to flicker.
