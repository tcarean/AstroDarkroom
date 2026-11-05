# 0081. The paper's gamut counts the whole colour difference, lightness included, after the round trip with black point compensation, over 4

Date: 2026-11-05
Status: accepted 2026-11-05
Supersedes: 0032 in part (the gamut figure counting the chroma alone, without black point compensation); 0042 in part (the chroma threshold its decision names, the hybrid itself staying)

## Context

The gamut figure of the Editare Curbe tab (R-144, ADR 0032) and the Soft proof tab's warning
(R-161, ADR 0066) counted a pixel as outside the paper's gamut when the round trip sRGB to
printer and back, through the profile's relative colorimetric tables and without black point
compensation, moved its a\*, b\* by more than `CurveGamutDeltaAb` (3): the chroma only, so that
the tones under the paper's black, the Tonuri tab's subject, were not counted twice.

On 2026-11-03 the owner compared the warning with Lightroom Classic's destination gamut warning
(relative intent, black point compensation, simulate paper and ink, the settings WhiteWall asks
for) on the Smida Milky Way TIFF
(`E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida\Processed_1mmPTFE_Flats\finale\starless_denoised_syqon_studio_in_max_0.75_stretch0.12-logd4_Gentle_sat0.25.tif`,
sRGB, 5837 x 3867) through the WhiteWall profile
(`E:\Photografie\Print and ICC profiles\Whitewall\WhiteWall_ICC_Inkjet_Hahnemuehle_Photo_Rag_Baryta.icc`,
v4.3, Lab PCS, paper black L\* 3.38): Lightroom marked much larger zones. Measured the same day
with a scratchpad probe over a copy of the core, the Lightroom screenshot registered on the
tool's reduced image (scale 0.560, a checkerboard blend checked by eye):

| what | share of the image | common with Lightroom |
| --- | --- | --- |
| Lightroom's warning | 0.93 % | |
| the tool's rule (chroma over 3, no BPC) | 0.45 % | 0.24 % |

- Lightroom's extra pixels are the darkest coloured dust: L\* median 6.4 (p10 3.6, p90 12.5),
  chroma 11; their a\*, b\* move by 2 after the trip, but the paper prints them 4 to 6 L\*
  lighter. The tool's extra pixels are the brighter saturated clouds (L\* 13 to 19, chroma 30
  to 37), whose a\*, b\* move by 3.0 to 3.2 without the compensation and by about 1.1 with it,
  which is why Lightroom, set on the compensation, leaves them. The two warnings are two halves
  of one loss: a dark saturated brown the paper cannot make, replaced by a lighter, duller one.
- The profile's own `gamt` tag, evaluated by hand (a 37-node mBA LUT), is not what Lightroom
  uses: at any value over 0 it marks 36 % of the image (IoU 0.03), over 0.5 the 3 % of the
  table below (IoU 0.05).
- Every rule tried against Lightroom's mask (IoU: the common pixels over the pixels either
  marks):

| rule | threshold | share | of Lightroom's hit | of the rule's in Lightroom's | IoU |
| --- | --- | --- | --- | --- | --- |
| whole Delta E76, relative with BPC | 3.0 | 3.12 % | 89.5 % | 26.8 % | 0.260 |
| | 3.5 | 2.04 % | 83.1 % | 38.0 % | 0.353 |
| | **4.0** | **1.33 %** | **71.5 %** | **50.1 %** | **0.417** |
| | 4.5 | 0.89 % | 57.4 % | 60.1 % | 0.415 |
| | 5.0 | 0.62 % | 43.9 % | 66.3 % | 0.359 |
| whole Delta E76, relative, no BPC | 5.0 | 1.99 % | 82.4 % | 38.7 % | 0.358 |
| | 6.0 | 1.08 % | 63.9 % | 55.5 % | 0.423 |
| chroma alone, relative, no BPC (the rule until now) | 3.0 | 0.45 % | 23.2 % | 48.9 % | 0.187 |
| chroma alone, perceptual | 3.0 | 4.50 % | 98.3 % | 19.5 % | 0.194 |
| the profile's `gamt` tag | > 0.5 | 2.97 % | 22.0 % | 6.6 % | 0.053 |

Lightroom's rule is not published; no rule matched it exactly, and the two best (the whole
difference with the compensation over 4, without it over 6) are equal within the registration.

The hybrid of ADR 0042 (the grid of 33, the exact trip within 1.0 of the threshold, once per
8-bit colour) was re-measured under the new rule on 2026-11-05 against the exact per-pixel
figure: every class within 0.14 points (the plate-solved FITS on the lab's baryta: all 1.943 %
against 1.917 %, dust 11.10 against 10.96; on its canvas 2.430 against 2.494, dust 15.25
against 15.35; `de_printat.png` on baryta 0.292 against 0.236, dust 0.55 against 0.45), with
5 170, 7 347 and 1 916 exact trips per measure and a first measure of 40 to 110 ms.

The owner chose the change on 2026-11-05, after the measurement and three questions on what
the two warnings meant for the print.

## Decision

1. A pixel is outside the paper's gamut when the whole colour difference between what goes in
   and what comes back from the round trip, lightness included (Delta E76 on L\*, a\*, b\*), is
   more than `CurveGamutDeltaE` (4). The round trip is sRGB to XYZ D50, the black point
   compensation towards the paper (`XYZ x (W - K) / W + K`), the profile's B2A1 table, its A2B1
   table back, the compensation undone (`(XYZ - K) x W / (W - K)`), then Lab. K is the device
   black through A2B1 as the neutral of its Y, the black the soft proof's `Relative` table uses
   (ADR 0066); the three steps are one code in `ProofProfile` (`NeutralBlack`, `Compensate`,
   `Uncompensate`), called by the proof and by the figure, so the two cannot drift apart.
2. The compensation is what keeps the paper's black out of the figure: nothing is clipped at
   the black, so a tone under it is not counted here as well as in the Tonuri tab.
3. The grid, the margin and the cache of ADR 0042 stay as they are; the table holds the three
   displacements.
4. One rule for the Editare Curbe figure and the Soft proof warning, as R-161 requires; the
   texts that name the rule say "on paper they would come out another tone or colour, by over
   4", the number interpolated from `Thresholds`.

## Alternatives rejected

- **Keep the chroma rule and say so in the legend.** It would keep the warning blind to the
  loss the owner looks for on this kind of image: the dark brown dust that comes out lighter.
- **The whole difference without the compensation, over 6.** As close to Lightroom (IoU 0.423)
  but it counts the paper-black clip the Tonuri tab already counts, and the print is made with
  the compensation (the lab's instructions), so the trip without it is not the print's.
- **Two warnings, the colour's and the tone's, each with its share.** Two numbers for one loss,
  and the second half of the question "which" is what the proof itself shows.
- **The profile's `gamt` tag.** Not what Lightroom does (IoU 0.03), and most profiles carry
  none.
- **Threshold 3, the Delta E of the study of 2026-10-05.** 3.1 % marked, a quarter of them in
  Lightroom's; 4 is the value measured against the reference the owner compares with.
- **lcms2's gamut check.** A native library (ADR 0066's reason), and its own rule, not
  Lightroom's either.

## Consequences

- R-144 and R-161 state the rule; `R144_ALightnessShiftAloneCountsAsOutside` and
  `R144_WithTheCompensationAGreyUnderThePaperBlackStaysInside` guard the two halves of it, the
  older R-144 tests take the third displacement and the threshold 4.
- V-008's twenty gamut figures and V-015's share outside the paper gamut move with the rule
  (`../validation.md`, 2026-11-05); every other figure stays.
- `Thresholds.CurveGamutDeltaAb` is gone; `CurveGamutDeltaE` carries the measurement above.
- The rule as built (the hybrid of ADR 0042 in the core), run on the Smida TIFF at the
  window's reduction and scored against the same Lightroom mask on 2026-11-05: 1.37 % marked,
  74.7 % of Lightroom's pixels hit, 57.3 % of the rule's in Lightroom's, IoU 0.480 (the probe's
  exact per-pixel rule gave 1.33 % and 0.417: the 8-bit colours near the threshold and the
  grid away from it decide a few edge pixels the other way).
- The threshold was found on one image and one profile against one reference; a second
  reference image with a Lightroom mask would be the next check, when the owner has one.
