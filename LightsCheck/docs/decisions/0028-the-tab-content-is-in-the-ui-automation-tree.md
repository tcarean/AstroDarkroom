# 0028. The tab content is in the UI Automation tree

Date: 2026-10-29
Status: accepted 2026-10-29

## Context

The window's tab content was missing from the UI Automation tree, the one a screen reader such
as Narrator and every UI Automation client walk: under the selected tab's item the tree held the
header's text and nothing else. The theme's `TabControl` template (`Theme/Dark.xaml`, the theme
of FlatCheck and AstroPhotoAnalyzer too) shows the selected content through a
`ContentPresenter` without a name, and WPF's `TabItemAutomationPeer` puts the selected tab's
content under the tab's item only through the presenter named `PART_SelectedContentHost`. The
first test of R-067 showed it on the synthetic eight-frame session before the change: no peer of
a tab's content under its item.

AstroPhotoAnalyzer found the same defect on 2026-10-28 and measured what the stock name does
(its ADR 0065): a click on a tab's header then moves the keyboard focus into the tab, after which
Left and Right no longer change the tab, and the control that takes the focus brings itself into
view, so a tab left scrolled down jumps back up to its first control. For that reason the owner
chose there, on 2026-10-29, an automation peer of our own, and asked the same day for the same
fix here.

## Decision

As in AstroPhotoAnalyzer's ADR 0065. The theme's template names the presenter `ContentHost`, not
the stock name. The window's tabs are an `AccessibleTabControl`
(`Presentation/AccessibleTabControl.cs`, AstroPhotoAnalyzer's class in LightsCheck's style),
whose automation peer gives each tab a subclass of the stock `TabItemAutomationPeer` that adds,
under the selected tab only, the children of `ContentHost`. The theme's style targets that
class, and so does the window's own style of the tabs, which `MainWindow.xaml` bases on the
theme's to show the tabs only with a result: an implicit style applies to its exact type only,
so a style still keyed on `TabControl` would no longer be found, and without the theme's style
the class would get the system's template, which carries the stock name.

Everything else is as before: a click on a tab's header leaves the keyboard focus on the header.

## Alternatives rejected

- **The stock name.** No code, but the focus and scroll change above, measured in
  AstroPhotoAnalyzer.
- **The stock name, with a focusable root `ScrollViewer` in every scrolled view**, the other
  option put to the owner there: it undoes the name's effect view by view, in every view added
  later too, and the keyboard still changes.
- **Leaving it.** The tabs hold the whole analysis; a tree with only their headers leaves a
  screen reader nothing to read.

## Consequences

- R-067 has two tests in `AutomationTreeTests`. `R067_EveryTabsContentIsInTheAutomationTree`
  selects each of the six tabs over the synthetic session and asserts its content under its
  item, once, and under no other tab's item, and the Raport tab's text box among its
  descendants. `R067_AClickOnATabsHeaderLeavesTheFocusOnIt` makes WPF's own lookup of the stock
  name in the applied template and finds nothing, since keyboard focus needs a window on screen,
  which no test shows; it passed before the change too, as it guards against the stock name,
  not against the defect.
- A tab control added to the window is an `AccessibleTabControl`; a plain `TabControl` gets no
  style from the theme.
- `Theme/Dark.xaml` is FlatCheck's theme (`docs/design/window.md`); FlatCheck gets the same
  change in a task of its own, started the same day, so the copy and its source keep the same
  tab style.
