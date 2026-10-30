# 0066. The soft proof is the paper's colour through the profile, on a grid of 65, shown black-compensated or as the paper

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The window reads the paper's Dmax from an ICC printer profile (ADR 0030) and counts the pixels
outside the paper's gamut (ADR 0032, ADR 0042), but nothing shows what the paper would do to
the image. On 2026-10-30 the owner asked for a soft-proofing tab as close as possible to
Lightroom Classic's: a rendering intent (perceptual, relative), "Simulate Paper & Ink", gamut
warnings that can be switched on and off, the image full-screen inside a margin whose colour
follows the selections; viewing only, no editing, no proof copy; without a profile the tab shows
an error and nothing else. The proposal the owner accepted the same day added three things
beyond Lightroom: a third intent, relative without black point compensation, which is what
the Tonuri tab's "under paper black" counts; a screen-gamut warning with the screen assumed
sRGB, as the whole tool assumes; and the proof live in the tab as well as in a window.

Measured on 2026-10-30 (a scratchpad probe over Wacton.Unicolour 8.0.0, on the lab's two
profiles, the Epson P900 baryta profile installed on the owner's machine and the CMYK
`RSWOP.icm` Windows ships):

- the package evaluates the perceptual, relative and absolute intents and lets each direction
  use its own; it has no black point compensation (BPC), so BPC is the ICC scaling of XYZ,
  written here;
- the paper white under the absolute intent: the Chromatone baryta L\* 97.6 b\* +1.1 (sRGB
  249, 248, 246), the canvas L\* 95.0, the P900 baryta L\* 95.5; the paper black relative to
  the paper white L\* 3.49, 22.3 and 6.54;
- relative without BPC on the canvas sends every grey under L\* 22 to the paper's black (a grey
  at L\* 10 comes back at 22.3); with BPC towards the paper and back towards the screen the same
  grey comes back at L\* 8.0 to 9.0 on every profile: the two intents answer different
  questions, and the first is the one the Tonuri tab's simulation asks (ADR 0025);
- a grid of 65 nodes per channel, read trilinearly, is within Delta E76 1.0 of the exact
  per-pixel proof at the 99th percentile on both lab profiles (max 1.4 on the canvas, 0.7 on the
  baryta) over 8 000 colours, half of them dark and nearly neutral as a sky is; 33 nodes reach
  2.4 to 5.2 at the maximum on the canvas; a 65-grid builds in 1.6 to 2.2 s with the rows in
  parallel on the owner's machine, 33 in 0.2 to 0.5 s; the exact trip per pixel would take
  about 35 s on a 1.4 MP reduction (ADR 0042);
- the CMYK profile proofs through every intent with the same code.

## Decision

1. The proof is computed only in the core (`Analysis/SoftProof.cs`, run by `Runs/ProofRun.cs`
   after the analysis, as the curves are, ADR 0051): source sRGB to XYZ D50; for `Relativ`
   the ICC black point compensation towards the paper (`XYZ' = XYZ x (W - K) / W + K`, K the
   device black through A2B1, the black ADR 0030 reads, W the D50 white), none for
   `Perceptual` and `Relativ, fara compensarea negrului`; the profile's B2A table of the
   intent to device values; A2B1 back, the colour the paper shows relative to its white. That
   colour, in XYZ, is what the grid stores: one table of `ProofGrid` (65) nodes per intent,
   built once per image and intent, the rows in parallel.
2. The display step is applied per pixel after the interpolation and follows the one box
   "Simuleaza hartia si cerneala" (Lightroom's "Simulate Paper & Ink"): off, BPC towards the
   screen, `(XYZ - K) x W / (W - K)`, so the paper's black is the screen's black and its white
   the screen's white; on, the absolute scaling by the media white of the `wtpt` tag, so the
   paper's white shows as its own colour and its black as its own grey. Then XYZ D50 to sRGB
   (the Bradford-adapted matrix, as the package converts), encoded.
3. Two warnings, both decided by the core on the proof's pixels: the paper's gamut by the rule
   of ADR 0042 on the existing `GamutTable`, so that the share equals the Editare Curbe tab's
   figure for the original; the screen's gamut where a display channel leaves 0..1 and the
   clipped colour differs from it by more than `ProofScreenGamutDeltaE` (2.0) in Delta E76.
   The screen is assumed sRGB, as every L\* in the tool is (R-096).
4. The margin is view only: `Automat` is the screen's white with the simulation off and the
   paper's white with it on (what the probe measured as the display colour of sRGB white under
   the absolute scaling), and Lightroom's five fixed backgrounds (white to black) can be chosen
   instead. The state of the controls is the session's: it survives another image and is not
   written to `settings.json`.

## Alternatives rejected

- **lcms2 (`lcms2-dotnet`, `lcmsNET`) for the proof.** The reference engine, with BPC and soft
  proofing built in, but a native library: the console would stop being one copyable file (the
  rule "Package notices", ADR 0030).
- **A grid of 33, as the gamut figure uses.** Delta E76 up to 5.2 against the exact proof on the
  canvas, visible on a sky; 65 costs 1.6 to 2.2 s per intent, once per image, and keeps every
  colour within about 1.
- **The exact trip per pixel.** About 35 s per rendering on one thread; the window would wait
  for every change of intent.
- **Storing the display colour in the grid.** One grid per intent and per state of the box,
  twice the work for a step that is a per-pixel scaling.
- **A monitor profile from Windows for the screen warning.** The tool assumes sRGB everywhere
  (R-096); a profile read from the display would make the proof alone colour-managed. Open for
  a later task.
- **The histogram of the proof, Before/After side by side, the proof copy.** Not asked; the
  `Originalul` box swaps the image inside the same margin, which is what the comparison is for.
- **Remembering the controls in `settings.json`.** The reasoning of ADR 0064: a view opens in a
  state chosen this session, nothing hidden from an earlier one.

## Consequences

- R-160 to R-163 state the behaviour; `SoftProofTests` guards the chain, the warnings and the
  problems on a hand-built profile that now carries B2A tables, `SoftProofTabTests` the tab and
  the window; V-015 holds the figures on the plate-solved FITS through the lab's baryta profile.
- The paper white the tab shows and the margin takes is the `wtpt` tag's, L\* 97.2 on the lab's
  baryta (V-015), 0.4 under the L\* 97.6 the package's own absolute intent gave in the probe,
  which adapts the white its own way; the tag is the figure the profile states, and the
  display step is written here, so the two cannot drift apart.
- `Thresholds` gains `ProofGrid`, `ProofScreenGamutDeltaE` and `ProofMaxSide`, each with its
  provenance; `thresholds.md` their rows.
- Wacton.Unicolour has a third use, named in `THIRD-PARTY-NOTICES.md`, `ThirdParty.cs` and the
  README's table.
- A profile without B2A tables cannot be proofed (the gamut figure already says so); B2A0,
  mandatory in a printer profile, is required too, since the package refuses a profile
  without it.
