# 0014. The light in every frame is read per side, after the bias, and named

Date: 2026-10-20
Status: accepted 2026-10-20

## Context

ADR 0012 tells a leak from the glow by the warming of the series: the per-frame test names
the frames that carry light over what the warming explains, and the split of a region
separates the part that grows with the warming from a constant part. On the owner's leak set
of 2026-10-20 (a lamp behind the camera at the uncovered eyepiece for frames 1 to 25, then in
front of the capped lens for 26 to 30) the console read `ATENTIE: 5 cadre au lumina peste ce
da incalzirea seriei ... scoate-le din darks\`. The owner objected that every frame leaks. He
was right about the words: light present in every frame cannot move the per-frame test, which
reads changes between frames, so it lands in the constant part by construction; the console
printed it (`constant +0.17, din care in bias -0.01, ramane +0.18`) but nothing judged it,
since the map's threshold is 1 ADU-eq (the hand analysis' "a leak would be at the ADU level")
and the remainder after the bias had no threshold of its own. The last five frames' +1.9 ADU
raw everywhere are the front position: the cap or the mount passes more light than the
eyepiece, so that finding stood, but its sentence implied the rest were clean.

Measured on 2026-10-20 over all eight regions, dark-minus-bias constant part on the frames not
lit, grouped by side (an edge and its two corners), ADU-eq:

| side | leak set (frames 1-25) | Smida (clean) |
| --- | --- | --- |
| top | +0.19 (top +0.22, top-left +0.18, top-right +0.18) | +0.05 |
| right | +0.11 | +0.08 |
| left | +0.02 | -0.02 |
| bottom | -0.05 | -0.01 |

The top side is the eyepiece band, 0.75 ADU raw over the top third (the raised mirror's
shadow), in all 25 frames. The single regions are noisier: Smida's top-right reaches 0.10
against the leak set's 0.18; a side averages three regions' extrapolation noise down. On the
leak set the dark-minus-bias median, 0.54 ADU-eq, is under the 0.66 the index predicts from
Smida's 0.14 at a 4.7x lower index, so there is no uniform light on top of the band.

## Decision

1. **The constant light is read per side**: for `sus`, `jos`, `stanga`, `dreapta`, the side's
   per-frame offset to the centre (the mean of the edge and its two corners) is fitted on the
   ratio like a region; its constant part carries the standard error of the intercept, the
   residual scale x sqrt(1 / n + mean(ratio)^2 / Sxx), and with biases the bias master's
   offset of the side is taken out (R-029). A leak through the eyepiece or the mount is a band
   along an edge, which lifts a whole side.
2. **`Th.LeakConstantAdu` = 0.15 ADU-eq and `Th.LeakConstantSigmas` = 3**: a side is constant
   light when its remainder passes both the floor and three times its own error, with biases
   only. Smida's largest side is 0.08 and the leak set's top is 0.19 ± 0.01: a margin of about
   two each way on the floor, thin, to be re-read when a third session with biases exists (a
   dated note here, the kit's ADR 0024). The error condition keeps a short or noisy series
   from naming what it cannot resolve: the synthetic test frames (256 x 192 px, regions of 256
   pixels) carry 0.1 .. 0.2 ADU of intercept error where the D3300's 2.4 Mpx regions carry
   0.01. The sides need `Th.LeakSidesMinFrames` (5) frames left after the lit ones, since the
   error of a two-parameter line read from three residuals is near zero by construction and
   three frames could pass the sigma condition on noise (the code review of 2026-10-20).
   Without biases the sides are printed as constants and the row says they cannot be told
   from a readout offset.
3. **The words.** A side over the threshold makes check 2 `ATENTIE` with `lumina constanta in
   toate cadrele: +0.19 ADU-eq pe latura de sus fata de centru, nu e in bias: scurgere de
   lumina (ocular, montura)`; the lit-frame sentence says `in plus fata de restul seriei` and
   keeps `scoate-le din darks\` only when no side is over the threshold; the parts that hold
   are joined with `; in plus `. The row `lumina constanta` prints the four sides. The
   per-frame table shows the worst side's offset in every frame (`latura`), after the bias
   when there are biases, so the band is seen frame by frame.
4. **The limit is said**: light uniform over the frame and present in every frame has no
   reference in the darks alone; the row says so when no side is over the threshold.

## Alternatives rejected

- **The verdict region alone.** The largest region on dark minus bias is the corner the glow
  lifts too (top-left on the D3300); the band's own measure is the top side.
- **Every region separately.** Noisier (Smida's top-right 0.10 against the leak set's 0.18)
  and a band would be reported three times.
- **Lowering `Th.LeakAdu` on the map.** The map's offset mixes glow, readout and light; the
  remainder after the bias is the quantity that is light by construction, and it has a floor
  of its own.
- **A calibrated dark current against the index, to catch uniform light.** One point with
  biases per temperature so far (Smida, the leak set); a later task once the libraries have
  biases.

## Consequences

- `LeakAnalyzer.SidesOf`, `SideLight`, `MapSide`, the constant in `Thresholds.cs`,
  `LeakVerdict.Sides` / `WorstSide` / `ConstantLight`, the row and the column in `DarkReport`;
  `R029_LightInEveryFrameIsReadPerSideAndTheBiasClearsAReadoutBand` guards the band, the
  readout band and the no-bias case; V-003 and V-008 record the worst side.
- The owner's remedy for a constant leak is in the words: cover the eyepiece and reshoot;
  dropping frames does not help.
