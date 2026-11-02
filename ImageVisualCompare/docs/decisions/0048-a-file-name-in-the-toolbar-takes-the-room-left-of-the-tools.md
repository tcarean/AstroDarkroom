# 0048. A file name in the toolbar takes the room left of the tools

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The toolbar's pair label gives each file name the width `PairLabelLayout.For` works out (R-065,
ADR 0047). Each name was capped at 360 DIPs, whatever the room, so a long name was trimmed with
most of the toolbar empty between the label and the tools on the right. The owner's names are
long, `starless_denoised_syqon_studio_in_max_0.75_stretch0.12-logd4_Moderate_sat0.25.tif` among
them, and the part that tells two variants apart is at the end, which the ellipsis hid. On
2026-11-02 the owner asked that the names use all the room up to the tools on the right, and be
trimmed only then.

## Decision

- A name has no cap of its own: it takes its own width as long as the five items of the label
  fit the room left of the tools. `PairLabelLayout.MaxName` is gone.
- When they do not fit, the rule is unchanged: the longer name gives first, down to the
  shorter's width, then both equally, down to `MinName` (48 DIPs) or the name's own width.

## Alternatives rejected

- **A larger cap.** Any fixed value trims a name somewhere with room to spare on a wide screen,
  which is what the owner asked to end.
- **Trim at the start of the name instead of the end.** WPF's `TextTrimming` cuts only at the
  end, so it would need a trimming of the project's own; the owner asked for the room, not for
  another cut.

## Consequences

- R-065 says it, and `PairLabelLayoutTests.R065_ALongNameTakesAllTheRoomItNeeds` and
  `PairLabelPanelTests.R065_ALongName_IsShownWhole_WhenTheRoomHoldsIt` fail on the code before
  (the long name was cut at 360).
- When the room is short the names are still cut at the end, so two names that differ only
  there, as the owner's do, read the same in the toolbar. The window's title holds both names,
  but Windows trims it too in a narrow window.
- `PairLabelPanel` and `MainWindow.xaml` are unchanged: the panel already measured the names at
  the whole width of the column left of the tools.
