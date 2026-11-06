# 0082. The paper fit reduces the chroma of what the paper cannot hold, tone and hue kept, in three variants, and writes a FITS or a 16-bit TIFF

Date: 2026-11-06
Status: accepted 2026-11-06
Supersedes: 0032 in part (decision 5, the gamut as a figure only with the saturation corrected at soft-proofing; decision 6, the FITS as the tool's one export)

## Context

Since ADR 0081 the tool marks the pixels the paper cannot hold the way Lightroom Classic does
(the whole colour difference after the compensated round trip over 4). On 2026-11-03 the owner
asked how to make those pixels print better on the Smida Milky Way TIFF through the WhiteWall
baryta profile, and the answer was a Lightroom step: a luminance-range mask on the dark tones
with the saturation lowered. On 2026-11-06 the owner asked whether that step could be automated
in the tool, generically, and asked for both: the recipe as a recommendation, and a new,
corrected FITS or TIFF written by the tool, with the originals never modified, offered as
several variants in a tab like Editare Curbe.

Measured on 2026-11-06 with a scratchpad probe over a copy of the core, on the plate-solved FITS
through the lab's baryta profile (`Hahnemuhle PRBaryta.icm`), on the Smida TIFF through the
WhiteWall profile, and on the plate-solved FITS through the canvas profile (`HARMAN_Canvas.icc`):

- Every pixel the paper cannot hold fits once its chroma alone is reduced along its own hue at
  its own L\*: on the FITS the share kept is 0.95 at the median (p10 0.88), the chroma given up
  0.6 units at the median (p90 1.4); on the Smida TIFF 0.86 (p10 0.68) and 1.8 (p90 6.4). None
  was out of reach on the two baryta profiles. The pixels outside are the dark saturated dust:
  99 % under L\* 15 on the FITS, 97 % on the TIFF.
- The lift the Tonuri tab recommends does not help them: on the TIFF a background of 0.16
  takes the share outside from 1.37 % to 1.84 %, since the lift raises the chroma with the
  tone. Lowering the chroma of the pixels under L\* 15 to 60 % leaves 0.16 % (12 % of them);
  the warm horizon cast the Culoare tab reports is most of their chroma (removed in the
  simulation, 71 % of them fit).
- A hard fit (only the pixels outside moved, onto the edge) touches 1.88 % of the FITS's
  pixels (1.37 % of the TIFF's), chroma lost 0.70 at the median (1.85), 4.2 at the 99 % mark
  (14.1). A soft knee from 90 % of the edge touches 4.06 % (2.26 %) with 0.47 (0.74) at the
  median; from 80 %, 6.11 % (3.50 %) with 0.65 (0.53); from 70 %, 8.54 % (5.34 %). The share
  of neighbouring pixels whose factor differs by more than 0.1 is 0.6 to 2.0 % in every
  variant: the mapping is a function of the colour alone, so it makes no speckle.
- The exact search per pixel (eight round trips through the profile) would take minutes on a
  22.6 MP image. A table of the largest chroma over L\* (101 nodes) and hue (121 nodes), built in
  1.3 s with the rows in parallel and read bilinearly, differs from the exact search by 0.004
  in the factor at the median and 0.025 at most on the two baryta profiles, where the table
  shortened by 3 % leaves no pixel outside; on canvas, whose gamut edge is sharper near its
  black (L\* 22), the table misses by up to 0.28 and leaves 0.23 % outside: the exact search
  must decide near the edge there, the way ADR 0042's hybrid decides near the threshold.
- ImageSharp 3.1.11 writes a TIFF with the ICC profile set on its metadata, but only 8 bits per
  channel whatever is asked (every read-back sample is a multiple of 257). The owner's inputs
  are all 16-bit (and 32-bit float FITS). The 16-bit samples of the Smida TIFF are 135 MB
  uncompressed, 123 MB under zlib and 109 MB with the TIFF horizontal predictor, at 6 s.

## Decision

1. **The fit is per colour, chroma only** (R-175): a pixel the paper cannot hold (the rule of
   ADR 0081) keeps its L\* and its hue and has its chroma reduced to the largest the paper holds
   at that L\* and hue. A pixel whose tone itself is out of reach stays as it is and is counted.
   The largest chroma comes from a table over L\* and hue, `GamutFitLstarNodes` (101) x
   `GamutFitHueNodes` (121), built once per image and profile, the exact search deciding for
   every pixel whose chroma reaches within `GamutFitEdgeMargin` of the table's edge or passes it,
   once per 16-bit colour, so that the share left outside is within 0.05 points of the exact
   per-pixel search on the three measured pairs (built: 0.001 %, 0.003 % and 0.000 % left, where
   the table alone leaves 0.19 %, 0.59 % and 0.36 %; `paper-fit.md`).
2. **Three variants** (R-176): `Exacta` (a hard fit), `Lina` and `Foarte lina` (a smooth
   compression of the chroma from `GamutFitKneeSoft` (90 %) and `GamutFitKneeSofter` (80 %) of
   the edge, asymptotic to it), computed after the analysis like the curves (ADR 0051), each
   with its evidence and the recipe for a hand-made mask (the tone band holding 90 % of the
   pixels outside and the uniform saturation factor that brings 99 % of them in).
3. **Two kinds of file** (R-177): a FITS as R-145, or a 16-bit uncompressed TIFF written by the
   tool's own writer with the input's ICC profile embedded (the standard sRGB when the input
   has none), the kind chosen in the save dialog, the input's kind proposed. The input is never
   opened for writing. ImageSharp stays a reader.
4. **A new tab** (R-178), `Culori pentru hartie`, between Editare Curbe and Soft proof: the
   gallery of the original and the three variants, a box that paints in red what is still
   outside, the evidence under each, `Salveaza ca...`.

## Alternatives rejected

- **The recipe alone, the edit left to Lightroom.** Proposed first (cheapest, no editing in a
  diagnostic tool); the owner wanted the corrected file too, since the manual mask is by
  luminance, not by what the paper holds, and is redone by hand for every print.
- **A global or a dark-tones desaturation** (what the Lightroom mask does). It desaturates
  pixels the paper holds: 0.75 everywhere touches every pixel to fix 1.4 % of them.
- **The exact search per pixel.** Minutes per image; the table with the exact search at the
  edge gives the same share within 0.05 points.
- **A table over sRGB (as the gamut figure's grid of 33).** The factor jumps from 1 to under
  1 at the edge, which a grid over the colour rounds inward (ADR 0042); over L\* and hue the
  largest chroma is a smooth surface.
- **A 3D LUT written for Siril or Lightroom instead of a file.** Neither reads a LUT on a
  stretched image in the owner's workflow; the file is what the lab takes.
- **An 8-bit TIFF through ImageSharp.** The encoder's only depth; the inputs are 16-bit, and
  a corrected file with less precision than its source was not acceptable to the owner.
- **A 16-bit TIFF with Deflate.** 109 MB against 135 MB, at 6 s and a compressor to maintain;
  Siril writes the inputs uncompressed at the same size.
- **A proof render per variant in the gallery.** The variants differ by a chroma shift of 1 to
  2 units on 2 to 6 % of the pixels, invisible side by side; the red layer shows what each
  variant leaves.

## Consequences

- R-175 to R-178 state the behaviour; `GamutFitTests` guards the fit, the variants, the
  evidence and the two writers, `GamutFitTabTests` the tab; V-016 holds the figures on the
  plate-solved FITS through the lab's baryta profile.
- `Thresholds` gains the table's nodes, the edge margin, the two knees and the recipe's three
  figures, each with its provenance; `thresholds.md` their rows.
- The core writes a second kind of file, `Input/TiffWriter.cs`, 16-bit uncompressed RGB with
  the ICC profile; `input.md` describes it; a 16-bit TIFF read back by ImageSharp and by an
  independent reader guards it.
- The gamut rule of ADR 0081 now feeds an edit; a change of the rule changes the fit, and V-016
  moves with it.
- The save of a whole 22.6 MP image takes 11 to 27 s on the owner's machine (the exact search
  for every pixel that lands on the edge, 300 000 to 550 000 of them, cached per 16-bit
  colour), against about a second for the curves' FITS; it runs under a wait cursor. A cell
  coarser than the 16-bit colour was tried for the cache and rejected: in the dark tones the
  edge moves within one cell by more than the search's tolerance (`paper-fit.md`).
- The curves and the fit are two separate files: a curve saved then fitted, or the reverse, is
  two saves through Siril. Combining them in one save is a decision for later, if asked.
