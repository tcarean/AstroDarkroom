# 0074. The tab content is in the UI Automation tree

Date: 2026-10-29
Status: accepted 2026-10-29

Numbered 0074 while `main` held 0070: 0071 to 0073 were taken by branches open on 2026-10-29
(the diagnostic log's review fixes, the missing Adobe profiles), so the numbers do not collide
at their merges.

## Context

The window's tab content was missing from the UI Automation tree, the one a screen reader such as
Narrator and every UI Automation client walk: under the `TabControl` and under the selected tab's
item there were the seven headers and nothing else. The theme's `TabControl` template
(`Theme/Dark.xaml`) showed the selected content through a `ContentPresenter` without a name, and
WPF's `TabItemAutomationPeer` puts the selected tab's content under the tab's item only through
the presenter named `PART_SelectedContentHost`, the stock template's name. Measured in process on
2026-10-29, by `R114_EveryTabsContentIsInTheAutomationTree` before the fix: with a session open
and Structure selected, the tab's item held only its header's text.

AstroPhotoAnalyzer, whose theme is the same, had the same defect. Its ADR 0065 records the
measurements made there on 2026-10-28: the stock name also makes a click on a tab's header move
the keyboard focus into the tab, where Left and Right no longer change the tab, and the control
that takes the focus brings itself into view, so a tab left scrolled down jumps back up when its
header is clicked again. Shown those, the owner chose there, on 2026-10-29, an automation peer of
our own, and the same day, once the same fix had been described to them for DarksCheck,
FlatCheck and LightsCheck, asked for it here ("nu poti sa le fixezi de aici tu?").

## Decision

As in AstroPhotoAnalyzer: the theme's template names the presenter `ContentHost`, not
`PART_SelectedContentHost`, so WPF does with it nothing of what it does with the stock name. The
window's tabs are an `AccessibleTabControl` (`Presentation/AccessibleTabControl.cs`, the class
AstroPhotoAnalyzer has), a `TabControl` whose automation peer gives each tab a subclass of the
stock `TabItemAutomationPeer` that adds, under the selected tab only, the children of
`ContentHost`, the children the stock peer adds through the stock name. The theme's style targets
that class, so the template and the class that reads its part go together: without that style
the class would get the system's template, which carries the stock name.

`MainWindow.xaml` gives the tabs a style of its own, for the trigger that shows them only when
the folder is good. It was `BasedOn` the key `{x:Type TabControl}`, which no longer exists once
the theme's style targets the subclass, so it is now based on `{x:Type local:AccessibleTabControl}`.
The tabs are named `Tabs`, by which the tests find them.

Everything else is as before this ADR: a click on a tab's header leaves the keyboard focus on the
header.

## Alternatives rejected

AstroPhotoAnalyzer's ADR 0065 lists them with their reasons: the stock name (the focus in the tab
after a click, and with it the jump of a scrolled tab), the stock name with a focusable root
`ScrollViewer` in every scrolled view, the stock `TabControl` template, and leaving it. Nothing in
this window changes those reasons.

## Consequences

- R-114 has two tests in `AutomationTreeTests`. `R114_EveryTabsContentIsInTheAutomationTree`
  selects each of the seven tabs over an open session and asserts its view under its item, once,
  and under no other tab's item; before the fix it failed with only the header's text under the
  item. `R114_AClickOnATabsHeaderLeavesTheFocusOnIt` makes WPF's own lookup of the stock name in
  the applied template and finds nothing: keyboard focus needs a window on screen, which no test
  shows.
- A tab control added to the window is an `AccessibleTabControl`. A plain `TabControl` gets no
  style from the theme: it would show the system's light look and, with the system's template,
  the stock name and its focus.
- The class is a copy of AstroPhotoAnalyzer's: a fix to one is a fix to look for in the other.
