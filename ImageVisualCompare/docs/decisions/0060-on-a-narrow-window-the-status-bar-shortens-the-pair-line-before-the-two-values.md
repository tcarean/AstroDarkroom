# 0060. On a narrow window the status bar shortens the pair line before the two values

Date: 2026-11-21
Status: accepted 2026-11-21

## Context

The status bar holds, from the left, the pair line (R-050), the coordinates under the pointer
and the pixel's value in A and in B (R-051), the memory (R-052) and the hint (R-054), and at
the right the version and the copyright, which are never trimmed. The first five stood in a
horizontal `StackPanel`, which gives every child the width it asks for; on a window too narrow
for them the bar was cut from the right, so the memory went first, then B's value, then A's,
while the pair line stayed whole. The review of 2026-11-13 listed it
(`docs/review/2026-11-13-code-review.md`, CR-013) from an estimate of the widths.

Measured on 2026-11-21 on the real `MainWindow`, never shown, laid out at a client width, with
the read-outs at their longest (`5937, 3887`, `A 255 255 255`, `B 255 255 255`). Visible width
of each item, of its full width, in device-independent pixels, in English:

| Client (window) | Pair | Pair line | Coordinates | A | B | Memory | Hint |
|---|---|---|---|---|---|---|---|
| 1264 (1280) | padded | 419 of 419 | 96 of 96 | 128 of 128 | 128 of 128 | 86 of 156 | 0 |
| 1008 (1024) | padded | 419 of 419 | 96 of 96 | 118 of 128 | 0 of 128 | 0 | 0 |
| 944 (960) | padded | 419 of 419 | 96 of 96 | 54 of 128 | 0 of 128 | 0 | 0 |
| 944 (960) | same size | 214 of 214 | 96 of 96 | 128 of 128 | 115 of 128 | 0 | 0 |
| 784 (800) | same size | 214 of 214 | 96 of 96 | 99 of 128 | 0 of 128 | 0 | 0 |

So on a window 960 wide, half of a 1920 screen, a padded pair showed no pixel of B's value and
`A 255 2` of A's; a pair of one size lost B at 800. In Romanian the pair line is 24 to 33
longer and the cut comes that much sooner. The version and the copyright take 303, and the
three read-outs with their margins 400.

The owner chose on 2026-11-21, of two options (shorten the pair line; put the values first).

## Decision

The order on screen stays. On a window too narrow for the whole bar the items give up their
room in this order: the hint, then the memory, then the pair line, trimmed with an ellipsis;
the coordinates and the two values stay whole for as long as the room left of the version and
the copyright holds the three, and then the coordinates go first and B's value last (R-054).

It is layout only, in `MainWindow.xaml`: the pair line and the three read-outs are the
children of a `DockPanel` docked left in the bar, the read-outs docked right inside it and
written B first, the pair line last, filling, with `TextTrimming="CharacterEllipsis"`. A
`DockPanel` gives its children their room in the order they are written, and on a window wide
enough it is as wide as its four texts, which then stand as they stood in the row.

The same widths after the change: at a client of 1008 the padded pair line has 242 of its 419
and at 944 it has 183; at 784 it has 20, an ellipsis; the three read-outs are whole at all
three.

## Alternatives rejected

- **The values first, left of the pair line.** They would never be cut, with no layout of
  their own. But the bar would change on every window, wide ones too, and with the pointer off
  the image some 400 pixels at the left of the bar would stand empty before the pair line.
- **A panel of the project's own, with the widths from a Core record,** as the toolbar's pair
  label has (`PairLabelPanel`, ADR 0048). The toolbar needed it for two names that share the
  room; here one text gives and the rest are fixed, which a `DockPanel` already does, with no
  arithmetic anywhere.
- **Narrower boxes for the read-outs when room is short.** Each has a fixed width so that it
  does not move as the numbers change (96, 128, 128); a box that shrank would bring that back.

## Consequences

- The read-outs keep their fixed boxes, blank part included. A pair of one size on a client of
  944 wide therefore has its pair line trimmed by 32 (182 of 214), where before nothing that
  could be read was lost there: B's box was cut by 13 of blank. The two values are the same
  either way.
- The memory, when it is cut, shows 16 less than before: its right margin is now inside the
  room it is given.
- Below a client of 743 wide (the bar's padding of 24, the 303 and 16 of the version and the
  copyright, the 400 of the read-outs) the read-outs no longer fit beside the version and the
  copyright; the coordinates are then cut first. No smallest width is set for the window.
- `README.md`, "The status bar", says the order; the Help manual in the wiki waits for the
  owner's word (ADR 0050).
- Guards: `MainWindowTests.R054_OnANarrowWindow_ThePairLineIsShortened_AndTheValuesStayWhole`
  (clients of 1008, 944 and 784) and `R054_OnAWideWindow_TheBarIsLaidOutAsBefore`, on a window
  never shown; they follow the layout clips, since WPF lays a child that got too little room
  out at the size it asked for and clips it.

Source: docs/review/2026-11-13-code-review.md (CR-013);
src/ImageVisualCompare.Wpf/MainWindow.xaml; src/ImageVisualCompare.Wpf/Theme.xaml.
