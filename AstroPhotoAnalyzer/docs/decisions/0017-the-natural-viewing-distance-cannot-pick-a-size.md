# 0017. The natural viewing distance cannot pick a size; without stars nothing about detail is measured

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §5 (D3, the two paragraphs "The
natural distance cannot pick a size" and "Without stars nothing about detail is measured") and
`docs/acceptance-de-printat.md` ("Run without `--distance`"); the decision dates from the
acceptance run, phase 7, 2026-09-21. Run without `--distance`, the first version recommended
50 x 33 cm where the conversation had said "up to 100 cm at the natural distance, 70 for the
hallway". At the natural distance, 1.5 x the print's diagonal, the distance from which a star is a
point and the natural distance both scale with the width; their ratio is a constant of the image,
1.018 on the reference image. Every row failed by 2 %, and the rule fell back to the smallest.

## Decision

- With an explicit distance and a known FWHM, the rows separate on a measurement: the largest
  passing row that is not oversize is recommended.
- Without a distance, no row is recommended. The test answers one question, the same at every
  size, "is the image sharp enough for its natural distance", with a tolerance of 15 %
  (`NaturalDistanceTolerance`) for the spread of the FWHM estimator; the report says up to which
  width that holds, that the wall chooses the size, and asks for a distance if the print will be
  seen from closer; the table's footer gives the ratio.
- Without stars, nothing about detail is measured: the pixel-grid floor, 25.4 / (0.29 x d) for a
  given distance and 180 dpi (the lab convention) without one, is a fact about the file, stated
  with how far the file reaches, and no row is recommended.

## Alternatives rejected

- **Recommend the largest row under the limit at the natural distance.** It would land on the
  largest of `--sizes`, a choice of the candidates, not of the image.
- **No tolerance.** The reference image failed every row by 2 %, a difference smaller than the
  estimator's spread.
- **Use the dpi floor as a statement of detail when there are no stars.** A soft nebula may hold
  far less detail than the grid; the floor only says the grid will not show.

## Consequences

- On the reference image, `--aspect 50:35 --distance 1.5` recommends 70 cm, as the conversation
  did; on the full frame the same distance gives 80 cm (FWHM 3.13 against 3.00).
- R-073, R-074 and R-094 state the rules; the Rezolutie tab carries a static note with both.
- Guards: `PrintSizeAnalyzerTests`, `Golden/clean-sky-canvas.txt` (the natural distance),
  `Golden/dark-sky.txt` (an explicit distance), validation case V-002.

Source: docs/specification.md §5; docs/acceptance-de-printat.md.
