# 0028. Each medium's Dmax has a recorded source; baryta is 2.4, measured on the lab's profile

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

`Parameters.DmaxOf` held baryta 2.0, canvas 1.5 and acrylic C-print 2.2 with a "~" and no
source; they came from the conversation that started the tool. Baryta's value decides what the
reference image is told: at 2.0 paper black is L\* 9.0 and `de_printat.png` gets "re-stretch to
0.17", from Dmax 2.34 it passes as it is. The docs-health audit of 2026-10-02 listed the missing
source as a gap. The owner prints at the lab Chromatone, on Hahnemühle Photo Rag Baryta, on a
Canon imagePROGRAF iPF8300 with LUCIA EX inks; the lab publishes its ICC profiles and names an
X-Rite i1 Pro as its instrument.

Measured on 2026-10-05, the device black (RGB 0, 0, 0) of each profile's relative colorimetric
table (`A2B1`), by a hand-written reader and again by Wacton.Unicolour 8.0.0, the two agreeing
to the last digit shown on the two v2 profiles:

| profile | made with | black, relative to paper white | Dmax relative | Dmax absolute |
|---|---|---|---|---|
| `Hahnemuhle PRBaryta.icm` (Chromatone; v2, lut16) | MonacoPROFILER, 2012-03-07 | L\* 3.49 | 2.41 | 2.44 |
| `HARMAN_Canvas.icc` (Chromatone's page; v2, lut16) | LOGO ProfileMaker, 2010-09-07, Eye-One Pro | L\* 20.84 | 1.49 | 1.54 |
| `HFA_EpsSC-P900_PK_PhotoRagBaryta.icc` (Hahnemühle, Epson P900; v4, lutAToB) | - | L\* 6.54 | 2.14 | 2.18 |

The absolute figure multiplies by the paper white's Y (`wtpt`: 0.929 baryta, 0.907 canvas); the
baryta profile's `bkpt` tag gives 2.46. The third row was read by the package only. Neither
Chromatone profile names its printer; the baryta one does not follow Hahnemühle's naming, so it
is most likely the lab's own, which is an inference. The perceptual table says nothing about
the paper: it puts every profile's black at the ICC reference medium's L\* 3.14 (Dmax 2.46).

`de_printat.png`, full frame, no WCS, on 2026-10-05:

| Dmax | paper black | pixels under it | the dust (L\* 4.1) | background recommendation |
|---|---|---|---|---|
| 2.0 | L\* 9.0 | 45.6 % | under | re-stretch to 0.17 |
| 2.35 | L\* 4.0 | 1.3 % | on the edge | print-safe as it is |
| 2.44 | L\* 3.3 | 0.6 % | above | print-safe as it is |

## Decision

1. Baryta is **2.4**: the lab's profile, relative to paper white (2.41), rounded to the one
   decimal the other media have. Relative, not absolute, because the image's white prints as
   paper white, so "which L\* of the image falls under paper black" is asked on that scale.
2. Canvas stays **1.5**, now supported by the Harman profile (1.49). Acrylic C-print stays
   **2.2** and is recorded as unsourced: the rule of thumb of the starting conversation.
3. The Dmax 2.35 of validation case V-003 has no recorded source either. It stays as a guard on
   the paper-black maths, next to the image's own threshold of 2.34, and is quoted as nothing
   more.
4. The names of the media are one list, `Parameters.MediumNames`, in the enum's order; the
   console's `--help`, its unknown-medium error and the window's combo read it.
5. The window's Dmax field shows the Dmax in use: the chosen medium's, written into the field
   when a medium is chosen, over whatever it held; emptied by hand it still means the medium's.
   A Dmax equal to the medium's is not explicit (`Parameters.DmaxIsExplicit`), in the window and
   in the console alike, so the report does not call the field's default "(explicit)".
6. The synthetic scenes of `Scenes.cs` were built around the old paper black of L\* 9.0. The
   tests and the two golden scenarios that test "the background under paper black" name that
   paper themselves (`Scenes.OnBuiltForPaper`, `--dmax 2.0`); the scenes and their figures stay.

## Alternatives rejected

- **Keep baryta at 2.0 and pass `--dmax` for the lab.** The default would stay the one value
  with no source and would push the owner's own prints towards a greyer sky than the paper needs.
- **The absolute Dmax (2.44).** It is what a densitometer review quotes, but the tool compares
  it with L\* values of an image whose white becomes paper white; both round to 2.4.
- **Darken the scenes until they fall under L\* 3.6.** Nearly every figure of some seventeen
  tests and of all the golden files would change, and the dark scene would stop resembling the
  reference image, whose sky is at 0.103.
- **A hand-written ICC reader as the source of record.** It agrees with the package on v2; the
  package also reads v4, and is the reader the tool itself will use (the next task).

## Consequences

- At Dmax 2.4 an ordinary astrophoto sky (0.08 to 0.10) is no longer under paper black: the
  background alarm fires rarely on baryta, which is what the measurement says. The measured 2.41
  is within 0.1 of the reference image's threshold (2.34), the margin Anexa B itself prints, and
  the profile dates from 2012; a test strip from the lab settles more than the profile can.
- The print-black model is a clip: everything under paper black prints as the same black. The
  lab converts with black point compensation, which compresses the shadows onto paper black
  instead of clipping them. The tool does not model that (`docs/design/known-limitations.md`); a
  task of its own.
- The simulation grid (0.10 to 0.20) and the help text's print range (0.15 to 0.25) were sized
  for the old paper black: on the reference image the exact target (0.094) is under the grid's
  floor. With paper black out of the way, what limits the print is whether the sky can be seen;
  ADR 0029, made in the same task, adds that rule.
- The level-L\* ladder marks the paper in use: the chosen medium's row, or the explicit Dmax's
  own row even when another medium has that Dmax (R-042).
- A new medium, or a new value, needs its source here. `docs/design/extending.md`, "A new print
  medium", lists every place.
- Guards: `R020_EachMediumHasItsDmax`, `R020_ADmaxEqualToTheMediumsIsNotExplicit`,
  `R020_TheMediumNamesAreTheCoresAndRoundTrip`, `R020_ChoosingAMediumFillsTheDmaxFieldWithItsDmax`,
  `PaperBlackLstarPerMedium`; in `docs/validation.json`, V-005 on the default medium, and V-001 and
  V-004 on the old paper (`dmax` 2.0), which keep the re-stretch path guarded on the real images.
