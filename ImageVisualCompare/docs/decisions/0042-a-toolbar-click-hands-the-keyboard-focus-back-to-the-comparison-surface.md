# 0042. A toolbar click hands the keyboard focus back to the comparison surface

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The shortcuts of the comparison surface, `+`, `−`, the arrows, `Ctrl+0` and `Ctrl+1`, work
while the surface has the keyboard focus (R-063). The surface takes it on a click on the image
and once a pair is shown (R-060). Every toolbar button is a `Button` in the `ToolButton` style
of `Theme.xaml`, focusable as a `Button` is, so a click on `Fit`, `100%`, `+`, `−`, `Open…` or
`About` moved the focus to the button, and the shortcuts did nothing until the image was
clicked again; the README had a sentence telling the user to do that. On 2026-10-26 the owner
asked for the toolbar to hand the focus back after a click, with a test on the WPF test thread
first (ADR 0032).

## Decision

- `MainWindow` adds one handler for `ButtonBase.ClickEvent` on the toolbar's panel, named
  `Toolbar` in `MainWindow.xaml`. The event bubbles from the button, so the handler runs after
  the button's own `Click` handler, and it calls `Compare.Focus()`: after `About` the dialog has
  closed, after `Open…` the file dialogs have been answered, and after the other four the zoom
  has changed.
- The buttons stay focusable. `Tab` still reaches them from the surface, and `Space` or `Enter`
  on a focused button still clicks it; the hand-back happens on the click, whichever way it was
  made.
- R-063 says so and is `implemented`: `MainWindowTests` clicks each button but `About`, whose
  click opens a modal dialog, on the WPF test thread and asserts that the window's logical focus
  is on the surface. Without a window handle the keyboard focus cannot move, and the logical
  focus is what `Focus()` leaves behind (ADR 0032); the keyboard focus follows it in the running
  application, checked at the launch (the rule "Run the application").

## Alternatives rejected

- **`Focusable="False"` in the `ToolButton` style.** One line, and the focus never leaves the
  surface. But `DialogButton` is built on `ToolButton`, and a dialog's buttons must take the
  focus for `Enter` and `Escape` to answer it (R-062); and the toolbar would drop out of `Tab`
  navigation, with `Ctrl+O` the only key that reaches `Open…`.
- **`Compare.Focus()` in each button's own handler.** Six places that must agree, and a button
  added later would forget it; the one handler on the panel covers every button, present and
  future.

## Consequences

- `R-063` goes from `accepted` to `implemented`; the README no longer tells the user to click
  the image after a toolbar button.
- A new toolbar button inside the `Toolbar` panel gets the hand-back for free; one placed
  outside the panel would not, and `docs/design/shell.md` says so.
- Guards: `MainWindowTests.R063_AToolbarClick_HandsTheFocusBackToTheComparisonSurface`, one
  case per button but `About`.
