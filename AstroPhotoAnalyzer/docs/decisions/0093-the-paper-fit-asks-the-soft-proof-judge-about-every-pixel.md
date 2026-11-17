# 0093. The paper fit asks the Soft proof judge about every pixel, not about the moved ones alone

Date: 2026-11-14
Status: accepted 2026-11-14; the Soft proof judge's word deciding which pixels are moved superseded by 0109

## Context

R-175 promises that no pixel the chroma alone can bring in is left outside "by the judge of the
Soft proof tab itself". ADR 0083 built the fit on a table of the largest chroma over L\* and
hue, with the judge searched near the table's edge, and decided that a pixel is outside when its
chroma passes that edge. The judge was asked only about a pixel the fit had moved. The code
review of 2026-11-13 (CR-001) showed the gap: the judge is a step function of the pixel's 8-bit
colour (ADR 0042), not monotone in the chroma, so it can reject a pixel the edge keeps and keep
one the edge cuts.

Measured on 2026-11-14 on the tonal subsample of each pair, variant `Exacta`: the judge rejected
581 pixels of the plate-solved FITS on the lab's baryta that the fit never moved (1.9427 %
outside by the judge, 1.9073 % by the fit; 0.0416 % left by the judge, 0.0005 % reported), 791
on the canvas (780 of them far inside the table; 0.0561 % left against 0.0001 % reported, over
the 0.05 points R-175 allows), 580 of `de_printat.png` on baryta, 60 of the Smida TIFF on the
WhiteWall baryta. The mirror case: 33 to 350 pixels per pair were cut though the judge kept
them. The saved file, opened in the Soft proof tab, still showed the first group in red.

## Decision

On a table with a judge, whether a pixel is outside is the judge's answer for the pixel as it
is, on its own samples, for every pixel (`GamutFit.Decide`, `GamutFitTable.JudgeOfPixel`). The
table and the edge search only say where a pixel lands: when the judge rejects a pixel that
edge keeps, the edge is searched again in a band ending at the pixel's own chroma; when the
judge keeps a pixel past the edge, the pixel is not outside and the hard fit leaves it (the
soft knees compress it from a share of its own chroma, and leave it too where the edge search
finds no chroma the judge keeps under it, the grey of its tone included, since nothing nearer
the grey is then known to be inside). The
share left after the fit is the judge's on every pixel, moved or not. The recipe counts the
same pixels.

## Alternatives rejected

- **Count with the judge, fit with the table.** The evidence would be honest, but the pixels
  the judge rejects inside the edge would stay in red in the saved file, which R-175 forbids.
- **Drop the table and search the judge for every pixel.** Exact by construction; the table is
  what keeps the whole-image fit at seconds (ADR 0083), and the judge's word costs one table
  read per pixel.
- **Widen `GamutFitEdgeMargin` until the search covers the disagreements.** On the canvas 780
  of the 791 pixels sit far inside the table; no margin short of the whole chroma reaches them.

## Consequences

- On the four measured pairs the judge and the fit now agree on every pixel, before and after
  the fit, under the three knees (`docs/design/paper-fit.md` has the table). The share outside
  as is equals the Soft proof tab's on the same pixels.
- The whole-image fit of the 22.6 MP FITS takes 3.9 s on baryta and 3.7 s on canvas with cold
  caches, against 3.2 s and 3.3 s.
- V-016's figures move with the rule (the share outside as is from 1.9073 % to 1.9427 %).
- Guards: `R175_TheJudgeDecidesEveryPixelWhateverTheTableSays`,
  `R175_APixelTheJudgeKeepsOverAGreyItRejectsIsLeftByEveryVariant`,
  `R175_WithoutAnEdgeTheKneeKeepsTheChromaAtEveryKnee`.
