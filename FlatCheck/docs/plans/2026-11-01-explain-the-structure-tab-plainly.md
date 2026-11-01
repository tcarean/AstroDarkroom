# Explain the Structure tab in plain words

Approved in chat on 2026-11-01, right after the End to end tab got the same treatment (ADR 0082,
`claude/Explain-the-End-to-end-tab-plainly`): the owner asked for the Structure tab's explanations
to be understood by anyone, approved the texts one by one in Romanian, with the English rewritten
in the same register, and said yes to changing the texts the window shares with the console,
which recaptures the parity goldens for those text lines.

## What changes

Forty-two texts, no number and no threshold: the thresholds stay read from `Th`.

- Window only: the notes under the five tests and the two informative rows (`StructureReport.TestNote`,
  `VignetteNote`, `CentroidNote`), the note when the filter box is unticked (`FilteredLensNote`),
  the line above the lens picker (`LensChoicePrompt`), the Adobe-missing paragraph
  (`AdobeMissing`), the "How to read" guide (`SessionMessages.HowToReadIntro/Rows`) and the map
  guide (`SessionMessages.MapGuideIntro/Shapes`).
- Shared with the console (`StructureReport`): the local-spot note and warning, the console's
  illumination-offset explanation and its undetermined row, the scale's meanings, "why the
  residual matters", the four advices, the two verdict rows and their closing line, the depth
  advice (two causes, two weights), the depth scale's ACCEPTABIL meaning and note, the
  `--light`-scale note, the comparison summary, the chosen-lens and range-only notes, the note
  under the profile table, the gap meaning.

Kept as they are: the names of the five tests and of the informative rows (labels in every saved
report and in the README), the "cannot compare" reasons (they name the missing input and the
option that fixes it), the Adobe install sentence, the profile row's "gives the verdict" label.
The map guide's row on concentric circles drops the history of the corrected ring artefact,
which stays in the README and the design note.

The words dropped: radial symmetry, purely radial model, fit, gradient (as a term), scale-invariant,
sampled, azimuth, Lambertian, achromatic, mosaic, upper bound (kept as "limita de sus"),
degenerate. In their place: a usual vignetting, symmetric about the centre; one side of the frame
brighter than the other; a small compact spot; the sky.

## Steps

1. A new requirement, R-122, for the Structure tab's plain words, with a vocabulary test over
   the window's texts; the tests that quote the old sentences change with it.
2. The texts, both languages.
3. Goldens: `GoldenOutputTests|EnglishOutputTests`, then `ParityGoldenTests` with the owner's yes
   (given above); every changed line read and tied to a text above, no figure line changed.
4. README's tables of the tab's guides and its quoted sentences, CHANGELOG (Changed), an ADR,
   `python-parity.md`'s list of recaptures.

## What deviated

Noted at the wrap-up of 2026-11-01, after the merge.

- More test assertions than foreseen quoted the old sentences, in `StructureReportTests`,
  `StructureTabTests`, `MapGuideTests` and `GoldenOutputTests`.
- Requirement R-013, which describes the offset note, and four doc comments still quoted the old
  wording; they were brought in line on `claude/Align-notes-with-the-plain-words`.
- By the merge, `main` held the End to end branch, so `main` was merged into this branch first
  and the CHANGELOG conflict resolved there (c5d11078); the merge into `main` is c009b084.
