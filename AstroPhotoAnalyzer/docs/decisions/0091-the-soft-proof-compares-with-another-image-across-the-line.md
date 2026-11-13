# 0091. The soft proof is compared with another image's across the line, same size only

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The Soft proof tab (R-163, ADR 0066) shows one image through the paper's profile. On 2026-11-13
the owner asked for "compare with another soft proof": choose another image, have the same
proof with the same settings applied to it in the same tab, and compare the two "in a way
similar to Visual Image Compare"; the chosen image can be changed for another or cancelled,
which leaves the compare mode; and it works in full screen too. The files a user has at that
point are versions of one picture: the image analysed, the FITS of a curve (R-145), the file of
the paper fit (R-177), another stretch. The comparison ImageVisualCompare makes, a vertical
line dragged over two images that zoom and pan as one, is already in this window as
`BeforeAfterViewer` (R-179, ADR 0090). The draft of R-165 and R-166, the new texts, three
assumptions and one question (images of two sizes) were put to the owner the same day, each
with a recommendation, and accepted as recommended.

## Decision

1. The comparison is the line of R-179, in the tab and in the full-screen window: left of it
   the analysed image, right of it the other one, the two file names as labels. The compare
   mode is entered with `Compara cu alta imagine...`, changed with `Schimba imaginea...` and
   left with `Opreste comparatia`; there is no key for it.
2. The other image is proofed by the core (R-165) from the analysed image's own set: the same
   profile object, so the same grid per intent, and its own mask and share. Every control acts
   on both sides: the intent, the red layer, `Originalul`, the margin, and the keys G, O and I.
3. An image of another size in pixels is refused with a sentence naming both sizes. The line
   compares the same place of two images, which only means something pixel for pixel.
4. An image that looks linear is refused by the rule and with the sentence of R-015, as the
   analysed image would be (ADR 0008).
5. The comparison belongs to the analysed image: opening another image ends it; an analysis
   run again on the same file keeps it and proofs the other file again. The other file is not
   remembered between runs of the program (as the tab's controls, ADR 0066).
6. The file dialog is opened by the controls' own view, over the window they are in, so that
   it is owned by the full-screen window when pressed there; a dialog owned by the main window
   could open behind the full screen.

## Alternatives rejected

- **Accept two sizes, each at its own size from the top left corner, as ImageVisualCompare
  does (its ADR 0014).** A reduced export of the same picture would still not lie over the
  original, so the line would cut two different places; and each side would be reduced by its
  own step. It costs a shared canvas in the viewer and a rule for the reduction, for a case
  the versions of one picture do not meet.
- **Two viewers side by side.** The owner asked for ImageVisualCompare's way, and a small
  difference of tone is seen across an edge, not across a gap (ADR 0090).
- **A second analysis of the other image.** The comparison needs its pixels through the
  profile, not its figures; an analysis takes 10 to 15 s on a 23 MP image and would raise the
  question of whose figures the other tabs show.
- **Proofing the other image with its own preparation (`ProofRun.Prepare` on a second
  result).** It would open the profile again and build each intent's grid twice (1.6 to 2.2 s
  each, ADR 0066), and "the same settings" would hold by convention instead of by sharing one
  object.
- **Keeping the other file when another image is opened.** It was chosen as a version of the
  image on screen; against an unrelated image it would mostly be refused for its size.
- **A separate place for the other image's refusal.** The tab's message already shows a
  reason in red beside a proof that stays (a refused intent, R-163); the refusal uses it.

## Consequences

- R-165 and R-166 are new; `SoftProofCompareTests` holds the core's side (the shared profile,
  the mask, the refusals, the two sentences) and `SoftProofTabTests` the tab's (both sides
  following every control, a refused file, the same image run again, another image, the two
  views and the buttons). The file dialog itself is not driven by a test and is checked in the
  running window.
- `ProofSet` keeps the gamut table and the analysed image's size, so that the other image is
  judged by the same table and its size compared without the analysis.
- The tab and the full-screen window each hold two viewers, the single one and the comparison,
  one of them shown: entering the compare mode starts at the fit, leaving it shows the single
  image as it was left, and a changed file keeps the line and the zoom, since its size is the
  same.
- The full-screen window's panel, hidden unless the mouse is over its band (ADR 0068), also
  shows while the tab has a reason in red: after the file dialog the mouse is rarely at the top,
  and a refusal nobody sees looks like a button that did nothing (the review of 2026-11-13).
- The analysis of another image that fails ends the comparison too (`MainViewModel` tells the
  tab, with the image's path), so that the first image opened again comes back alone; a failed
  run of the same image forgets nothing (the two reviews of 2026-11-13).
- `ProofTexts` changed, which is under `Report/`: `/validate` runs before the task ends.
- Left for a task of its own if wanted: two sizes; a key for the compare mode in full screen;
  remembering the other file.
