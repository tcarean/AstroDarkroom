# Make the soft proof full screen true

Branch `claude/Make-the-soft-proof-full-screen-true`, worktree
`.claude/worktrees/Make-the-soft-proof-full-screen-true` (from `main` at 9fc7cd92).
Written and implemented on the branch on 2026-10-30, in one session, from the owner's request
of that day.

## Context

The Soft proof tab's `Ecran intreg` button (R-163, ADR 0066) opens `SoftProofWindow`: a
maximised window with the title bar and its three buttons, the controls, the caption, the
message and the key hint in a bar at the top, and the proof under the bar. The owner asked for
a true full screen: no window title, no minimise or maximise buttons; the panel with the check
boxes hidden, shown only while the mouse is over its zone; leaving by Esc or by a new Close
button in that panel.

## Requirement

R-164 (window), implemented: the full-screen window covers the whole screen (`WindowStyle`
`None`, `WindowState` `Maximized`, `ResizeMode` `NoResize`), the margin filling the screen
behind the image; the panel (controls, caption, message, key hint, and a `Inchide` / `Close`
button) lies over the top of the image, hidden unless the mouse is over the band it occupies,
which keeps its height while hidden so there is something to point at; `Close` and Esc close
the window. R-163's sentence about the window now points at R-164.

## Test

`SoftProofWindowTests.R164_...` (window project): the three window properties; after a layout
the panel is `Hidden` and its band keeps the panel's height; `PanelShown` true makes it
`Visible`; the Close button's click raises `Closing`. `WindowSmokeTests` renders the window
with the panel shown, so the PNG shows it; `WindowEnglishTests` reads the window's texts (the
hidden panel's texts are still in the tree).

## Code

- `Views/SoftProofWindow.xaml`: the window attributes; a `Grid` with the proof's `Border` and
  `ZoomPanViewer` filling it, and over it, at the top, a transparent `Border` (`PanelZone`)
  holding the panel `Border` (`Panel`), whose `Visibility` is `Hidden` unless the window's
  `PanelShown` is true; a `DockPanel` with the `Close` button on the right and the controls
  on the left.
- `Views/OverlayWindows.xaml.cs`: `PanelShown` dependency property, set from the zone's
  `MouseEnter` and `MouseLeave`; the `Close` button's click handler; the dark title bar call
  goes (there is no title bar).
- `Views/SoftProofControls.xaml`: the `Ecran intreg` tooltip says what the window is now.

## Docs

R-163 and R-164 in `docs/requirements.md`; ADR 0068 (the hover-shown overlay, immediate hide,
the band as the hot zone, against a timer and a toggle key); `docs/design/soft-proof.md`'s
window paragraph; the README's **Full screen** bullet; the CHANGELOG's unreleased Soft proof
bullet (the window is not released yet).

## Not done

The hover is not driven by a real mouse in the tests, and the app was not run by hand in this
session (the kit's rule on live input while the owner may be at the PC): the hiding and showing
rest on WPF's `MouseEnter` / `MouseLeave` on a transparent `Border`, and on the PNGs the smoke
test dumps.
