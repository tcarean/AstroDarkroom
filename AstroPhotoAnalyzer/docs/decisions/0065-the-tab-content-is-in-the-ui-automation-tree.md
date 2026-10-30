# 0065. The tab content is in the UI Automation tree

Date: 2026-10-28
Status: accepted 2026-10-29

## Context

On 2026-10-28, opening a simulation row's preview for the owner by driving the running window
over UI Automation, no control inside a tab could be reached: under the `TabControl` and under
the selected tab's item the tree held the seven headers and nothing else, and a point on a tab's
content resolved to the `TabControl` itself. The theme's `TabControl` template
(`Theme/Dark.xaml`) showed the selected content through a `ContentPresenter` without a name, and
WPF finds that presenter by its name alone: `TabControl.SelectedContentPresenter` is the template
part `PART_SelectedContentHost`, the name the stock template gives it, and
`TabItemAutomationPeer.GetChildrenCore` puts the selected tab's content under the tab's item only
through it (the WPF 8 source). So a screen reader such as Narrator read the tab headers and
nothing of the diagnosis under them, and a UI Automation client could press no button inside a
tab. The owner asked for the fix the same day ("Da, repară").

The stock name does more than that. Measured on the reference image on 2026-10-28, in the window
built with and without it:

- `TabControl` gives the presenter the selected `TabItem`'s content alignment. The theme's
  `TabItem` takes Aero2's `Stretch` on both axes, the presenter's own default, so nothing moves: a
  capture of the Tonuri tab is the same, pixel for pixel, with and without the name.
- A click on a tab's header (`TabItem.SetFocus`) moves the keyboard focus into the tab's content
  when it was not already on another header. Without the name the focus stayed on the header:
  the arrows then moved between the tabs (Right went from Rezolutie to Culoare) and Down did not
  scroll the tab. With it the focus lands on the content's first focusable control (on Rezolutie
  the list of rows, on Editare Curbe the `Print` radio button): Up and Down scroll the tab (three
  presses took Rezolutie from 0 to 39.6 %), Left and Right no longer change the tab, Ctrl+Tab and
  Ctrl+Shift+Tab still do, and Shift+Tab goes back to the header. The gallery's Left and Right
  (R-146), caught on the window, still browse it (1 / 5 to 2 / 5 and back). A click alone draws
  no focus rectangle, with the name or without it.

Shown these measurements, the owner first kept the stock name and its focus, the same day
("Intră în conținut"). The code review of that change then found what the focus costs: the control that
takes it brings itself into view (`FrameworkElement.OnGotFocus` calls `BringIntoView`), so a tab
left scrolled down jumps back up to its first control when its header is clicked again.
Simulated in process on Tonuri, scrolled to its end, bringing its first tab stop into view took
its offset from 1619 px to 283 px; the jump was not seen live. Put back to the owner, who chose an
automation peer of our own on 2026-10-29 ("Peer propriu").

## Decision

The theme's template names the presenter `ContentHost`, not `PART_SelectedContentHost`, so WPF
does with it nothing of what it does with the stock name. The window's tabs are an
`AccessibleTabControl` (`Presentation/AccessibleTabControl.cs`), a `TabControl` whose automation
peer gives each tab a subclass of the stock `TabItemAutomationPeer` that adds, under the
selected tab only, the children of `ContentHost`, the children the stock peer adds through the
stock name. The theme's style targets that class, so the template and the class that reads its
part go together: without that style the class would get the system's template, which carries
the stock name.

Everything else is as before this ADR: a click on a tab's header leaves the keyboard focus on the
header, where Left and Right change the tab (on Editare Curbe, while there is a gallery, they
browse it, R-146) and Down does not scroll it, and a tab shown again is where it was left
(measured in process: Tonuri, left at 1200 px, was at 1200 px again after a visit to Culoare).

## Alternatives rejected

- **The stock name**, the decision of 2026-10-28: the content in the tree without a line of
  code, but the focus in the tab after a click on its header, and with it the jump of a
  scrolled tab back up.
- **The stock name, with a focusable root `ScrollViewer` in every scrolled view**, the other
  option put to the owner on 2026-10-28: the focus would land on the scroll viewer, which brings
  nothing of its own content into view, so the position would stay. It undoes, view by view and
  in every view added later, what the name does, and the keyboard still changes: after a click,
  Left and Right no longer change the tab.
- **The stock `TabControl` template.** It would bring back the look the dark theme replaced, and
  with it the stock name and its focus.
- **Leaving it.** The tabs hold the whole diagnosis; a tree with only their headers leaves a
  screen reader nothing to read and a UI Automation check nothing to press.

## Consequences

- R-157 has two tests. `R157_EveryTabsContentIsInTheAutomationTree` selects each of the seven
  tabs over a real result and asserts its view under its item, once, and under no other tab's
  item, and the Tonuri tab's `Preview` buttons among its descendants; a later WPF that added the
  content another way too would fail it by the `once`. `R157_AClickOnATabsHeaderLeavesTheFocusOnIt`
  makes WPF's own lookup of the stock name in the applied template and finds nothing: keyboard
  focus needs a window on screen, which no test shows.
- A tab control added to the window is an `AccessibleTabControl`. A plain `TabControl` gets no
  style from the theme: it would show the system's light look and, with the system's template,
  the stock name and its focus.
- DarksCheck, FlatCheck and LightsCheck carry the same unnamed presenter in their
  `Theme/Dark.xaml`; each is its own project's task, and can take this class together with the
  theme's style for it and the presenter's name.
