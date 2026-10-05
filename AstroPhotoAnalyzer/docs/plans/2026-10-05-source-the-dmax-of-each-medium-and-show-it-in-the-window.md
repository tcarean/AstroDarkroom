# Source the Dmax of each medium and show it in the window

Date: 2026-10-05. Approved by the owner in chat, decision by decision; the decisions are ADR
0028. The first of two tasks; the second reads a Dmax from an ICC profile.

## Why

Three gaps of the docs-health audit of 2026-10-02: the checklist for a new print medium named
five places of about twenty, nothing said where the list of media comes from, and the Dmax of
each medium had no recorded source although baryta's decides the reference image's verdict.

## Measured first

The device black of the lab's ICC profiles (Chromatone: Hahnemühle Photo Rag Baryta on a Canon
iPF8300, and Harman Canvas), relative colorimetric table, by a hand-written reader and by
Wacton.Unicolour 8.0.0: baryta 2.41 relative to paper white (2.44 absolute), canvas 1.49 (1.54).
The reference image at Dmax 2.0, 2.35 and 2.44. The tables are in ADR 0028.

## Decided by the owner

1. Baryta becomes 2.4; canvas 1.5 and acrylic 2.2 stay, each with its source recorded.
2. The Dmax read from a profile is the one relative to paper white.
3. The window's Dmax field is filled with the chosen medium's Dmax; choosing a medium writes
   over the field; an emptied field means the medium's; a Dmax equal to the medium's is not
   explicit.
4. The tests and golden scenarios built around the old paper black of L\* 9.0 name that paper
   themselves (`--dmax 2.0`); the scenes stay.
5. Black point compensation is recorded as a limit of the model, a task of its own.
6. Two branches: this one, then the ICC reader with Wacton.Unicolour (approved as a package),
   a button beside the Dmax field listing the installed printer profiles, and `--icc <file>` in
   the console.

## Steps of this task

1. Requirements: R-016, R-020, R-101.
2. Tests first: the media names from the core, a Dmax equal to the medium's is not explicit,
   the header and Anexa F keep a Dmax's second decimal (the bug: `--dmax 2.35` read `Dmax 2.4`),
   choosing a medium fills the field.
3. Code: `Parameters` (2.4, `MediumNames`, `DmaxIsExplicit`), `Options.Media` and `--help` from
   the core's list, `ReportWriter` and the ladder through `Fmt.Dmax`, `ParameterFields` and the
   field's label.
4. The tests built on the 2.0 paper: `Scenes.OnBuiltForPaper`, `--dmax 2.0`.
5. Goldens regenerated, every changed line tied to its cause.
6. ADR 0028; `extending.md` (the full checklist), `console.md` (where the media come from),
   `input.md`, `window.md`, `known-limitations.md` (BPC), README, CHANGELOG.
7. `/validate`: V-001 and V-004 drift on every Dmax-dependent figure; the owner decided that both
   keep their figures and name their paper (`dmax` 2.0), and that V-005 records the new default.

## Added during the task

- After the review: the level-L\* ladder marks the paper in use (R-042); the grid's floor is
  described as the simulation's, not the paper's.
- After the owner tried the window: the sky under L\* 15 is an Attention and the recommended
  row has the sky at L\* 15 or above (ADR 0029), in this branch, so that the new default does
  not reach `main` telling the reference image it can be printed darker.
