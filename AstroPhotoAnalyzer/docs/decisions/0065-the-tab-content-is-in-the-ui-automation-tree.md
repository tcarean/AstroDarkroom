# 0065. The tab content is in the UI Automation tree

Date: 2026-10-28
Status: accepted 2026-10-28

## Context

On 2026-10-28, opening a simulation row's preview for the owner by driving the running window
over UI Automation, no control inside a tab could be reached: under the `TabControl` and under
the selected tab's item the tree held the seven headers and nothing else, and a point on a tab's
content resolved to the `TabControl` itself. The theme's `TabControl` template
(`Theme/Dark.xaml`) shows the selected content through a `ContentPresenter` without a name, and
WPF finds that presenter by its name alone: `TabControl.SelectedContentPresenter` is the template
part `PART_SelectedContentHost`, the name the stock template gives it, and
`TabItemAutomationPeer.GetChildrenCore` puts the selected tab's content under the tab's item only
through it (the WPF 8 source). So a screen reader such as Narrator read the tab headers and
nothing of the diagnosis under them, and a UI Automation client could press no button inside a
tab. The owner asked for the fix the same day ("Da, repară").

## Decision

The theme's `TabControl` template names its presenter `PART_SelectedContentHost`, as the stock
template does. The template's layout, the tabs and their views are unchanged.

WPF reads the name in two more places, and both now apply to this window as to a stock
`TabControl`. Measured on the reference image on 2026-10-28, in the window built with and without
the name:

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

Shown these measurements, the owner kept the stock behaviour the same day ("Intră în conținut").

## Alternatives rejected

- **An automation peer of our own**, the other option put to the owner: a peer for the
  `TabControl` and its items, adding the content under the selected item while the presenter
  keeps no name, so that the focus stays on the header after a click. Some forty lines to
  rebuild what WPF does once the presenter has its name, for a tab control that behaves unlike
  a stock one.
- **The stock `TabControl` template.** It would bring back the look the dark theme replaced, for
  nothing the name does not already give.
- **Leaving it.** The tabs hold the whole diagnosis; a tree with only their headers leaves a
  screen reader nothing to read and a UI Automation check nothing to press.

## Consequences

- R-157; `R157_EveryTabsContentIsInTheAutomationTree` selects each of the seven tabs over a real
  result and asserts its view under its item, and the Tonuri tab's `Preview` buttons among its
  descendants.
- After a click on a tab's header the keyboard acts on the tab, as above.
- DarksCheck, FlatCheck and LightsCheck carry the same unnamed presenter in their
  `Theme/Dark.xaml`; each is its own project's task.
