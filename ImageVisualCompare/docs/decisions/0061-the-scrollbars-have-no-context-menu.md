# 0061. The scrollbars have no context menu

Date: 2026-11-21
Status: accepted 2026-11-21

## Context

The two scrollbars that float over the image (ADR 0019) are WPF `ScrollBar`s with the styles
`ThinVerticalScrollBar` and `ThinHorizontalScrollBar` of `Theme.xaml`, a thumb and nothing else.
The styles set no `ContextMenu`, and a `ScrollBar` whose `ContextMenu` nothing has set is given
WPF's own as its menu is about to open. The review of 2026-11-13 listed what a right click then
shows (`docs/review/2026-11-13-code-review.md`, CR-015).

Measured on 2026-11-21 on the surface at 100 %, the menu's opening raised on each bar: the
vertical bar then holds a menu of seven items, `Scroll Here`, `Top`, `Bottom`, `Page Up`,
`Page Down`, `Scroll Up`, `Scroll Down` (the horizontal one `Left Edge`, `Right Edge`,
`Page Left`, `Page Right`, `Scroll Left`, `Scroll Right`), with a background of `#F5F5F5` and
no style of the theme: a light menu over a dark image (against ADR 0018), in English under
`--lang=ro` too (against R-101). The bars have WPF's `SmallChange` of 0.1 and `LargeChange` of
1 on a range counted in image pixels, 0 to 2959 there, so `Page Down` moves the view by one
image pixel and `Scroll Down` by a tenth of one.

The owner chose on 2026-11-21, of two options (no menu; a dark menu in the interface's
language with steps that mean something).

## Decision

A right click on a scrollbar opens nothing. The two styles set `ContextMenu` to null: a value
that comes from a style, null included, is a value set, and WPF puts its own menu only where
the property is at its default (R-043).

## Alternatives rejected

- **A dark menu worded by `UiText`, its steps from `ScrollBarState`.** Seven texts in two
  languages, a menu style, and steps to define (a page as one view of the image), for something
  nobody asked for: the wheel, the drag, the minimap and the thumb already move the view, and
  `Fit` and `100%` are on the toolbar.
- **Leaving the menu and giving the bars real steps.** The menu would still be light and in
  English.
- **`ContextMenuService.IsEnabled="False"` on the bars.** Not tried: by WPF's documentation it
  stops an element's menu too. The null value is one line as well, and it is the one the test
  can read back.

## Consequences

- The bars keep WPF's `SmallChange` and `LargeChange`: nothing reaches them now, since the
  bars have no arrows, no track to click, are not focusable, and have no menu.
- Guard: `CompareViewTests.R043_ARightClickOnAScrollbar_OpensNoMenu` raises the menu's opening
  on both bars of a surface never shown and reads the property. A real right click on screen
  was not made on 2026-11-21 (the owner was at the PC).

Source: docs/review/2026-11-13-code-review.md (CR-015); src/ImageVisualCompare.Wpf/Theme.xaml;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml and CompareView.xaml.cs.
