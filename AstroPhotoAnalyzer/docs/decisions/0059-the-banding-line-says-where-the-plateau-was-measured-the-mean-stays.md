# 0059. The banding line says where the plateau was measured; the plateau stays the mean, its statistic an open question

Date: 2026-10-27
Status: accepted 2026-10-27; the plateau's statistic superseded by 0060

## Context

On the owner's 2026-08-12 Milky Way core denoised by Syqon at 0.75, section 1 warned of a
banding risk at 8 bits (a plateau of 15.1 px) while section 5 called the denoise right (a
median sigma of 0.82 LSB on 313 smooth tiles). Measured on 2026-10-27, tile by tile, on three
denoise strengths: the plateau's strict criterion (range under 6 levels) rejects any tile with
real noise of about 1 LSB or more, so on that frame only 11 tiles pass, 0.2 % of the frame,
the patches the denoiser wiped to a sigma of 0.01; at Syqon 0.5, 8 such tiles with a plateau
of 40 px; at Syqon 1, 26 % of the frame passes, the mean plateau is 4.7 px ("no visible
banding") and 2 % of the tiles are flat over all 64 px. The two sections speak of different
places, and the mean plateau neither compares across denoise strengths nor shows the worst
tiles of a heavily denoised frame (ADR 0015 chose the mean, with the acceptance script's
figure as its guard).

## Decision

The report says where the plateau was measured: the banding line's "because", Appendix C and
the Export tab's plateau row give the number of clean tiles, their share of the frame
(`PlateauFramePct`, the tiles accepted over the random positions tried) and the noise left on
them (`PlateauSigmaLsb`, the median of the tiles' sigmas by the noise analyzer's own formula);
the denoise line's "because" always gives the share of smooth tiles under `NoiseBandingLsb`.
The plateau itself stays ADR 0015's mean, and the denoise verdict stays the median: the
evidence is added, the rules are not moved. Whether a share-of-flat-tiles or a percentile of
the per-tile plateau should replace the mean is left open, to be measured on the five real
images and the three Syqon strengths in a task of its own before deciding.

## Alternatives rejected

- **Change the plateau statistic now.** The right one is not measured yet, and the owner asked
  for the evidence first; `known-limitations.md` records the mean's blind spot.
- **Move the denoise verdict to the 10 % quantile or the minimum.** The median says how hard
  the denoise is, which is what the user sets; the wiped tail is a different fact, now visible
  in the share of tiles under 0.5 LSB and in the plateau's own figures.

## Consequences

- A banding warning beside a denoise in target reads as what it is: a few wiped patches, not
  the sky; the cure in the line (16 bits, or dither at the export) is unchanged.
- `quantization.plateauFramePct` and `quantization.plateauSigmaLsb` are recorded for the
  analysis cases of `docs/validation.json`; no existing figure moves.
- Guards: `QuantizationAnalyzerTests` (R-080), `RecommendationsTests` (R-091, R-095), the
  golden files.
