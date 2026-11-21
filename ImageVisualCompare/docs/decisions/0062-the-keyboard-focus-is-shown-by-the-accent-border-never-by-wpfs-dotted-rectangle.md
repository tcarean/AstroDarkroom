# 0062. The keyboard focus is shown by the accent border, never by WPF's dotted rectangle

Date: 2026-11-21
Status: accepted 2026-11-21

## Context

Everything on screen is dark, whatever the system theme (ADR 0018). The theme styled the
buttons' rest, hover, checked and disabled states and said nothing of the keyboard focus, so
WPF's own mark was used. The review of 2026-11-13 listed it
(`docs/review/2026-11-13-code-review.md`, CR-016).

Read on 2026-11-21 from the properties of the real window's elements, never shown:

- Every button of the theme (`ToolButton`, `ToolToggleButton`, `ChangeButton`, `DialogButton`)
  had WPF's button focus visual: a rectangle stroked `#000000`, 1 thick, dashes `1 2`, 2 inside
  the button, whose own background is `#1B1F26`. No template of the theme had a trigger on a
  focus property.
- The comparison surface, focusable for its shortcuts (R-063), had WPF's default focus visual,
  which stands for the system's: the same stroke, around the whole surface, over the image.
- `DialogButton` lit the accent border on `IsDefault`, which a button keeps wherever the focus
  is. In the padding question, Tab to `No` left the border on `Yes` while Enter and Space
  answered with `No`.

WPF draws a focus visual only in the active window, after a key was used. With the owner at
the PC no window was activated, so the dotted rectangle itself was not seen on screen; the
properties above are what draws it.

## Decision

The keyboard focus is shown in the palette, and WPF's dotted rectangle is drawn nowhere
(R-068):

- A button that has the keyboard focus has the accent border it has under the pointer: a
  trigger on `IsKeyboardFocused` in the templates of `ToolButton` and `ToolToggleButton`, which
  `ChangeButton` and `DialogButton` inherit, and `FocusVisualStyle` null on both.
- A dialog's default button is lit on `IsDefaulted`, not `IsDefault`: WPF keeps `IsDefaulted`
  true on the default button while the keyboard focus is in its window and on nothing that
  takes Enter itself, which every button does. So the one lit button is the one Enter answers
  with: the default while no button has the focus, the focused one after a Tab.
- A link of the About window that has the focus is underlined, as under the pointer, and has
  no focus visual.
- The comparison surface has `FocusVisualStyle` null: nothing is drawn over the image for
  having the focus.
- About's list of components, an `ItemsControl`, is not focusable. WPF makes an `ItemsControl`
  a tab stop, and with the focus it had WPF's mark; it has nothing of its own to focus. The
  branch's review pointed at it, and at the message area of the dialog, a `ScrollViewer`,
  which was read and is no tab stop.

## Alternatives rejected

- **A light dotted rectangle, WPF's mark in another colour.** It would still lie over the edge
  of the image on the surface, and on a button it is a second frame inside the border the
  theme already draws.
- **The accent border on `IsFocused`, the logical focus,** which the tests could set without a
  window. A button keeps the logical focus while another window has the keyboard; the border
  would then say Enter answers where it does not.
- **Keeping `IsDefault` and lighting the focused button too.** Two lit buttons in the padding
  question, and no way to tell which one Enter presses.
- **Making the surface not focusable.** Its shortcuts need the focus (R-063).

## Consequences

- A dialog that is not the active window lights no button: nothing in it has the keyboard, as
  in a Windows dialog. Before, `Yes` and `Close` were lit always.
- When a dialog opens, the focus is on the window itself, not on a button, so the default
  button is lit and Enter answers with it, as before. This is from WPF's source, read on
  2026-11-21 (`HwndKeyboardInputProvider.OnSetFocus` gives the focus to the root element when
  there is none to restore; `Button.UpdateIsDefaulted`; `ButtonBase` sets `AcceptsReturn` for
  buttons only), not seen on screen.
- A toolbar button shows the accent border for the length of a mouse click, when it has the
  focus (R-063); the pointer is over it then, so nothing is seen to change.
- While a link of About has the focus, `Close` is still lit, though Enter then opens the link:
  a link does not declare that it takes Enter. `Close` was lit always before.
- Guards: `ThemeFocusTests.R068_...` set WPF's read-only `IsKeyboardFocused` and `IsDefaulted`
  through their keys, by reflection, on controls with the theme's styles, and read the
  template's border, the link's decoration and the `FocusVisualStyle`s;
  `R068_NoTabStopOfAnyWindow_KeepsWpfsDottedRectangle` walks the three windows' own trees,
  without the parts the controls' templates add. They do not show what
  a Tab in a live dialog draws: that is for a run on screen with the keyboard (ADR 0026), not
  made on 2026-11-21.

Source: docs/review/2026-11-13-code-review.md (CR-016); src/ImageVisualCompare.Wpf/Theme.xaml;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml; src/ImageVisualCompare.Wpf/MessageDialog.xaml;
github.com/dotnet/wpf, `HwndKeyboardInputProvider.cs`, `Button.cs`, `ButtonBase.cs` (branch `main`).
