# 0024. The tab content is in the UI Automation tree

Date: 2026-10-29
Status: accepted 2026-10-29

## Context

The window's tab control showed the selected tab's content through the `ContentPresenter` of
the theme's template (`Theme/Dark.xaml`, LightsCheck's copy, ADR 0009), and that presenter had
no name. WPF's `TabItemAutomationPeer` puts the selected tab's content under the tab's item in
the UI Automation tree only through the presenter named `PART_SelectedContentHost`, the stock
template's name, so a screen reader such as Narrator, or any UI Automation client, found the
eight tab headers and nothing of the checks under them. Measured here before the fix: with a
result and each tab selected in turn, the selected tab's item held only its header's text
(`R079_EveryTabsContentIsInTheAutomationTree`, red).

AstroPhotoAnalyzer found the same defect in the same theme on 2026-10-28 and fixed it on
2026-10-29 (its R-157 and ADR 0065, `main` 311de89). Its ADR records what the stock name does
besides: with it, a click on a tab's header moves the keyboard focus into the tab's content, the
control taking the focus brings itself into view, and a tab left scrolled down jumps back up to
its first control; Left and Right then no longer change the tab. Shown those measurements, the
owner chose there an automation peer of our own, with the presenter left without the stock
name, and on 2026-10-29 asked for the same fix in DarksCheck, FlatCheck and LightsCheck.

## Decision

The same as AstroPhotoAnalyzer's ADR 0065. The theme's template names the presenter
`ContentHost`, not `PART_SelectedContentHost`, so WPF does with it nothing of what it does with
the stock name. The window's tabs are an `AccessibleTabControl`
(`Presentation/AccessibleTabControl.cs`, taken from AstroPhotoAnalyzer), a `TabControl` whose
automation peer gives each tab a subclass of the stock `TabItemAutomationPeer` that adds, under
the selected tab only, the children of `ContentHost`, the children the stock peer adds through
the stock name. The theme's style targets that class, so the template and the class that reads
its part go together: without that style the class would get the system's template, which
carries the stock name.

One change is DarksCheck's own: `MainWindow.xaml` gives its tab control a local style (hidden
until there is a result) based on the theme's, `BasedOn="{StaticResource {x:Type TabControl}}"`.
That key no longer exists once the theme's style targets the class, so the local style targets
`AccessibleTabControl` and is based on that type's style.

Everything else is as before: a click on a tab's header leaves the keyboard focus on the header,
where Left and Right change the tab and Down does not scroll it.

The theme is LightsCheck's copy and stays one (ADR 0009): LightsCheck takes the same change the
same day, and a later change to the theme's tab style or to the class goes there first.

## Alternatives rejected

AstroPhotoAnalyzer's ADR 0065 lists them with their reasons; in short:

- **The stock name.** The content in the tree without a line of code, but the focus moved into
  the tab after a click on its header, and with it the jump of a scrolled tab back up.
- **The stock name, with a focusable root `ScrollViewer` in every scrolled view.** It undoes,
  view by view and in every view added later, what the name does, and the keyboard still
  changes.
- **The stock `TabControl` template.** It would bring back the look the dark theme replaced,
  and with it the stock name and its focus.
- **Leaving it.** The tabs hold every check; a tree with only their headers leaves a screen
  reader nothing to read.

## Consequences

- R-079 has two tests in `tests/DarksCheck.Wpf.Tests/AutomationTreeTests.cs`.
  `R079_EveryTabsContentIsInTheAutomationTree` selects each of the eight tabs over the synthetic
  session and asserts its content under its item, once, and under no other tab's item, and the
  Index tab's `Copiaza eticheta` button among its descendants. The Report tab's content is a
  `Border` around the text, so the test looks for a peer inside the content, not for the
  content's own. `R079_AClickOnATabsHeaderLeavesTheFocusOnIt` makes WPF's own lookup of the
  stock name in the applied template and finds nothing: keyboard focus needs a window on screen,
  which no test shows.
- A tab control added to the window is an `AccessibleTabControl`. A plain `TabControl` gets no
  style from the theme: it would show the system's light look and, with the system's template,
  the stock name and its focus.
