# 0023. The CFA colour order is read from the file, never guessed, and only names channels

Date: 2026-09-25
Status: accepted 2026-09-25; the ROWORDER rule of the CFA order superseded by 0094

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-expo.md; the decision itself dates
from the histogram card, with the `ROWORDER` rule added after the Tokina sky-flat finding (the
note gives no date). The four Bayer positions get colour labels in the reports and on the
window's charts. The order differs between cameras - RGGB is commonest, but GBRG, GRBG and BGGR
exist - so a label on the wrong channel would look measured when it is not. Two traps were
found on real files. On a D3300 NEF libraw reports `filters = 0xB4B4B4B4` and `cdesc = "RGBG"`,
and `FC(row, col)` returns indices 0/1/3/2 into `cdesc`: the second green sits at index 3, and a
decoder that assumes 1 = G mislabels half the mosaic. On the Tokina sky-flat master (6016 x
4016) Siril's `ROWORDER = 'BOTTOM-UP'` with an even height put an odd sensor row first in the
file, so `RGGB` read as `GBRG`: `--histeq` called position [1,0] "G2" at a third of the green
level, which was the red channel of a blue twilight sky - `--expo` on the source NEF had R
exactly there.

## Decision

A position gets a colour only when the file says which it is: from libraw's `filters` +
`cdesc` on a raw, or from `BAYERPAT` on a FITS, shifted by `XBAYROFF`/`YBAYROFF`
(`CfaPattern.Shift`) and, when `ROWORDER` is `BOTTOM-UP` and the height even, moved by one row
(`CfaPattern.ForRowOrder`, as Siril does when it debayers; an odd height, `TOP-DOWN` or no card
moves nothing). Anything else stays `CFA[y,x]`. The order is used for naming only; no figure
depends on it - the limiting channel is the brightest one whatever colour it is.

## Alternatives rejected

- **Assuming RGGB (or any default) when the file is silent.** The order varies by camera and a
  wrong label would look measured.
- **Reading `FC()` as a colour index with 1 = G.** On Nikon FC gives 0/1/3/2 for RGGB; the
  second green is at index 3.
- **Ignoring `ROWORDER`.** On the Tokina master the two labels and the `--histeq` composite
  were wrong; no figure moved, since every metric is per position, which is exactly why the
  error was invisible to the numbers.

## Consequences

- `CfaPatternTests` carries the D3300 vector (`0xB4B4B4B4`, `"RGBG"`) alongside the four
  textbook masks.
- `ABottomUpFrameOfEvenHeightShiftsTheOrderByOneRow` and the `expo cfa BOTTOM-UP` scenario in
  `crosscheck.py` (byte-identical) pin the row-order shift in both implementations; the
  `expo cfa BAYERPAT` scenario pins the channel-naming line.
- The window's `ChannelPalette` paints all four positions the neutral accent when the order
  is unknown, so the screen cannot imply a measurement that was never made.
- `--histeq`'s composite looks up R/G/B by `CfaPattern` colour, never by position, and falls
  back to the mean of the four greys when the order is unknown (ADR 0025).
- On a raw the channel-order line reads `din libraw` against Python's `din rawpy`; on a FITS
  both read `BAYERPAT`.
- Source note: docs/design/modes-expo.md ("The channel order").
