# Explain the End to end tab in place

## Context

The End to end tab (the light series of `process/`, mode 3) shows a verdict, four series
figures, a per-frame table and the mean radial map, and explains none of them. The owner asked
what the series summary meant, what the picture at the bottom right was and what the table's
columns held, then asked for those explanations in the application, in both languages.

Found on 2026-10-26: the window has four folded guides of one shape (`Disclosure` toggle, text
in `SessionMessages` as `T(en, ro)` pairs, a view-model with `IsExpanded`, mirrored in the
README by hand, opened by `WindowLanguageTests`); the End to end tab has none, and ADR 0036
gives it the legend only because the residual map's shape guide does not apply to the fitted
radial term. Every threshold the explanations need is a constant of `Th`, reachable from the
window (`InternalsVisibleTo`).

## Owner's decisions (2026-10-26)

The first proposal, a fifth folded guide, was replaced by the owner with explanations in place:

1. under each row of the series summary a description that stops at the label column and does
   not reach the value;
2. the explanation of the picture under the picture;
3. a hover on each column header of the table that explains that column.

Open with the owner at wrap-up: whether a folded panel is still wanted for what has no place
of its own (what the test is and on which data it applies, the three conditions as a list,
`INAPLICABIL`); the three conditions are told in the notes where they bite, and the refusal
already explains itself in the verdict card.

## Requirement

R-105 in `docs/requirements.md`, `implemented`.

## Steps, in order

1. Worktree `.claude/worktrees/Explain-the-End-to-end-tab`, branch
   `claude/Explain-the-End-to-end-tab`.
2. R-105 and `LightExplanationsTests` (both languages without diacritics, thresholds from `Th`,
   the map's sign convention, the same texts on a refusal and after a reset, the placement in
   the rendered view).
3. Nine texts in `SessionMessages` (`LightMeanNote`, `LightSpreadNote`, `LightSemNote`,
   `LightCorrelationNote`, `LightMapNote`, `LightFrameTip`, `LightRadialTip`, `LightSkyTip`,
   `LightTilesTip`), nine properties on `LightTabViewModel`.
4. `LightView.xaml`: a second row in each summary grid with the note in column 0; `ToolTip` on
   the four headers with a transparent background and a 30 s show duration; the map note in a
   fifth row under the legend.
5. ADR 0064, `two-applications.md`, the README's window section, CHANGELOG.
6. Gate, the rendered tab (`WindowSmokeTests` with `FLATCHECK_DUMP_SCREENS`), commit, merge
   proposal.

## Verification

- `LightExplanationsTests` green in both languages; `WindowLanguageTests` green (it collects
  tooltips, so the English screen holds no Romanian word).
- `scripts/verify.ps1` green.
- The rendered End to end tab looked at: the notes stop at the label column, the map note sits
  under the legend.
