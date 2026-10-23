# 0043. The report's figures say what they measure: the dust's Dmax, the levels an export writes, the master's missing-level difference, the pixels a crop removes

Date: 2026-10-23
Status: accepted 2026-10-23

## Context

The code review of area 2 (2026-10-23; `docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`,
"Code review of area 2") found five places where a figure or a flag said more, or something
else, than what the code measured:

1. `DmaxToPassAsIs` tested the dust alone, and the report said `trece asa cum e de la Dmax X`:
   false whenever the as-is sky is under L\* 15 (`de_printat.png`: 2.33, yet an ATENTIE at 2.4;
   `Golden/dark-sky.txt` printed it inside a PROBLEMA section). R-043 and README.md said the
   same. `AlreadyPrintSafe` and the validation key `simulation.alreadyPrintSafe` meant the dust
   alone, true on frames whose verdict is ATENTIE (items 1 and 13).
2. The dark shares "sub nivelul 32 / 64" compared the float sample with N / 255: on an 8-bit
   TIFF, PNG or JPEG, read as n x 257 / 65535, the level N itself counted too (+2 points on the
   reference image's 8-bit version; a flat level-32 file read 100 %) (item 3).
3. `Keep16BitMaster` read `MissingFrom8` alone, so an 8-bit file saved as 16 bits, missing as
   many levels on both paths, got "Pastreaza master-ul de 16 biti" beside the upscale Attention;
   R-082 and R-096 worded the rule differently (item 4).
4. The crop was decided on the ratio (1e-6), not on the pixels: 4961 x 3508 at 297:210 printed an
   Attention that cut 0 px, and 3508 x 2480 cut 1 px and said 0 per side (item 5).

The owner answered on 2026-10-23: "astea vrem sa le fixam pe toate, conform recomandarilor
tale", that is: keep the Dmax figure but name it for what it is, and when the as-is sky is under
`LowSeparationLstar` add that no paper makes the image print-safe as it is and the fix is the
stretch; count the 8-bit level an export would write on every format; `Keep16BitMaster` on the
difference `MissingFrom8 - MissingFrom16`; the crop needed only when at least a pixel is
removed, the two sides named when they differ; rename `AlreadyPrintSafe` for the dust.

## Decision

1. `SimulationResult.DmaxDustOnPaperBlackAsIs` (was `DmaxToPassAsIs`) is the Dmax whose paper
   black is the as-is dust's L\*; the report and the window say `praful ajunge pe negrul hartiei
   de la Dmax X` and, when the as-is sky is under L\* 15, add that as it is no paper makes the
   image print-safe and the fix is lifting the background at the stretch (R-043, R-092).
   `DustOnPaperBlackAsIs` (was `AlreadyPrintSafe`) says the dust alone; the validation keys follow
   the names (`simulation.dustOnPaperBlackAsIs`, `simulation.dmaxDustOnPaperBlackAsIs`).
2. The dark shares count the samples whose 8-bit level, `RasterOps.Level8`, is under
   `DarkLevelA` / `DarkLevelB`, on every format (R-040): what an 8-bit export writes under 32.
3. `Keep16BitMaster` is `Applicable && MissingFrom8 - MissingFrom16 > MissingLevelsWarn`
   (R-082, R-096): the levels 8 bits lose that 16 bits keep.
4. A crop is `Needed` only when the new width or height removes at least one pixel; `CutStartPx`
   and `CutEndPx` (left / top, right / bottom) replace `CutPerSidePx`, and the report and the
   window name both sides when they differ (`0 px la stanga si 1 px la dreapta din latime`),
   through one core text, `ReportWriter.CropCutText` (R-032).

## Alternatives rejected

- **Give the Dmax at which both the dust and the sky pass.** None exists when the sky is the
  cause: the visibility threshold does not move with the paper (ADR 0029); the figure for the
  dust stays useful and is now named for it.
- **Drop the Dmax figure and the dust flag.** The owner kept both; renamed, they say what they
  are, and the validation cases keep guarding them.
- **Compare the dark shares as floats and document the 8-bit tie.** "Sub nivelul 32" would stay
  false on the formats most files come in; the level is what the histogram shows.
- **Skip the master advice on an upscale only.** The difference says the same thing on every
  source and reads as R-096 words it.
- **Keep the ratio tolerance and print the pixels.** A crop that removes nothing is no crop; a
  one-pixel crop is, and its sides differ.

## Consequences

- The goldens change where these texts print (`dark-sky.txt`, `dark-sky-wcs.txt`,
  `clean-sky-canvas.txt`, `mono-png8.txt`); the dark shares move a little on the 16-bit and
  float cases of `docs/validation.json`, and the two keys are renamed there.
- Guards: `R043_TheDmaxFigureNamesTheDustAndSaysWhenNoPaperMakesTheImagePrintSafe`,
  `R040_TheDarkSharesCountTheEightBitLevelAnExportWouldWrite`,
  `R082_AnUpscaledSourceMissesTheSameLevelsOnBothPathsAndKeepsNoMaster`,
  `R032_ACropIsNeededOnlyWhenAPixelIsRemoved`.
