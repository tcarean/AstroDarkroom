# 0117. Section 5 does not warn of banding that section 1 measured and did not find

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

Two sections of the report speak of steps in the sky at 8 bits, on two measurements. Section 1
judges them on the flat areas themselves, since ADR 0060: the share of the frame where a flat
area is wide enough to show on paper. Section 5 judges the denoise on the noise left in the
smooth sky, and when that is under `NoiseBandingLsb` it said "Denoise is too strong for print:
too little noise is left to hide the steps between levels (banding)". Neither read the other.
The code review of 2026-11-13 (CR-015) gave the case, the reference image itself
(`docs/validation.json`, V-001: `noise.sigmaMedianLsb` 0.4518, `noise.bandingRisk` true,
`quantization.plateauVisibleFramePctAtReference` 0): section 1 says "At 8 bits no visible steps
appear in the sky", section 5 warns of banding, in one report.

The noise is a sign that steps may show; the flat areas are the steps, measured. Where both
were measured, the second is the answer to the first.

## Decision

The owner's choice on 2026-11-20, of three put to them with the wording side by side. When the
noise flags a banding risk and section 1 measured the flat areas and found them not visible at
the reference row's dpi (the same condition as section 1's Ok line: the plateau known, a
reference row, `PlateauVisibleAt` false), section 5 says:

- `Denoise is stronger than print needs: the noise left is under the target. Section 1 found no
  visible steps in the sky, so there is no banding to fear; still, for the print version a
  weaker denoise keeps the sky's texture and the faint stars.`
- `Denoise-ul e mai puternic decat cere printul: zgomotul ramas e sub tinta. Sectiunea 1 n-a
  gasit trepte vizibile in cer, deci nu e risc de banding; totusi, pentru versiunea de print, un
  denoise mai slab pastreaza textura cerului si stelele slabe.`

The verdict stays Attention and the evidence is unchanged. When section 1 found visible steps,
or could not measure the flat areas, the sentence is the one it was.

## Alternatives rejected

- **The same sentence with the verdict Ok.** The noise is still under the print target, which
  costs the sky's texture and the faint stars; and an image's overall verdict would change with
  a wording.
- **Leave both as they are.** Each rule follows its requirement, but the reader gets "no steps"
  and "banding" from one report and cannot tell which holds.
- **Have section 1 follow the noise.** ADR 0060 made the measured share the banding verdict
  because the noise alone misjudged the denoised frames.

## Consequences

- On the reference image section 5 reads the new sentence; no figure and no verdict moves.
- The four console snapshots that carry the old sentence (the dark scene, with and without a
  WCS, in both languages) keep it: their section 1 finds visible steps.
- Section 5 names section 1 by its number; a change of the sections' order would have to change
  the sentence.
- Guard: `R095_TheDenoiseLineDoesNotWarnOfBandingThatSectionOneMeasuredAndDidNotFind`.
