# 0089. The Structure tab's map guide is open by default

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The guide under the Structure tab's residual map, `How to read the map` (`Cum citesc harta`), was
folded by default since it was added: six rows of text under every map, on every run, were thought
to drown the figures (ADR 0036 lists the folded guide among the map's viewing aids). On
2026-11-02 the owner asked for it open by default. The guide's card is capped at 300 px by its own
scroll bar (`MapGuidePanel`), so it cannot take the map's whole row. Measured with the real
window shown off-screen and a 3:2 master: at 1920 x 1040, the owner's screens with the window
maximized, the map is drawn 714 x 476 px folded and 609 x 406 open, 85 % of its size; at
1360 x 820, the restored window, 490 x 326 and 279 x 186.

## Decision

`MapGuideViewModel.IsExpanded` starts true: the guide is open when the window starts. A fold by
the user holds until the window closes, across folders, as before (`Reset` does not touch it).
The tab's other guide, `How to read the tab`, and the other tabs' guides keep starting folded.

## Alternatives rejected

- **Open only in a large window.** A rule on the window's size for a viewing aid; the window
  opens maximized, and on the owner's screens the map keeps 85 % of its size.
- **Remembering the fold across launches.** Nobody asked for it, and it would be a setting for
  one panel.
- **Every guide open.** The owner asked for the map's; `How to read the tab` sits under the
  verdict, which must stay the first thing visible.

## Consequences

- R-127. `TheGuideStartsCollapsedAndKeepsItsStateAcrossReset` became
  `R127_TheMapGuideStartsOpenAndKeepsTheUsersFoldAcrossReset`; `R127_TheMapGuideIsOpenOnAFreshTab`
  checks the open guide on a rendered tab. No figure, no console text and no golden file moves.
- In a restored, smaller window the map shrinks more (279 x 186 px at 1360 x 820); one click on
  the guide's heading gives it back.
- Source notes: `docs/design/two-applications.md`; ADR 0036 carries a restated note.
