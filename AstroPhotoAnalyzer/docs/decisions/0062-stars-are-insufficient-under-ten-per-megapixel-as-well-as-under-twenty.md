# 0062. The stars are insufficient under ten per megapixel, as well as under twenty in all

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

The FWHM, the effective detail and the size table rest on the stars; under `StarMinCount` (20)
they are not computed and the size is judged by the pixel grid (ADR 0017, ADR 0054). On the
owner's 2026-08-12 core denoised by Syqon at its strongest setting, a starless frame, 48
candidates pass the 12-sigma clause, soft blobs the denoiser left (median FWHM 6.98 px, Q1
5.77, Q3 8.39), and the report computed a "real detail of ~836 px across the width" on them.
Measured on 2026-10-27, stars per megapixel: the frames with stars, Rosetta 650 on 8.6 MP (76),
Orion 1389 on 10.2 MP (136), the reference image 6274 on 23.1 MP (272), Andromeda 3116 on
9.4 MP (332); the starless or denoised ones, that core 48 on 22.6 MP (2.1), Syqon 0.75 and 0.5
14 and 9 (0.6, 0.4), the plate-solved FITS, Rosetta, the Veil and Orion starless 5, 1, 1 and 0.
A count alone cannot tell 48 blobs on 23 MP from 48 stars on a small crop; a density can.

## Decision

The stars are insufficient when fewer than `StarMinCount` (20) or fewer than
`StarMinPerMegapixel` (10) per megapixel of the frame, `StarResult.DensityPerMp` = count /
(width x height / 10^6). Ten is seven times under the sparsest real field measured and five
times over the most misleading starless one; the absolute floor stays for small crops. The
report's size section, Appendix D and the Rezolutie tab name both figures and both thresholds.

## Alternatives rejected

- **Raise `StarMinCount` to 100 or more.** A 2 MP crop with 60 real stars would lose its FWHM;
  a density scales with the frame.
- **A stricter FWHM range.** The blobs at 6-8 px are inside the range real stars take on a
  soft or a nebulous frame (Andromeda's median is 5.3 px).

## Consequences

- The strongest Syqon frame is judged by the pixel grid like its two lighter siblings; no
  reference image moves (the reference image sits at 272 per megapixel, the starless FITS
  frames were insufficient already).
- Guards: `StarAnalyzerTests` (R-070), the golden files, the validation cases.
