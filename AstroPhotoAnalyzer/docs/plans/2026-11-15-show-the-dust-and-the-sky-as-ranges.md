# Show the dust and the sky as ranges

## Context

The Tonuri tab gives the dust and the sky as one L\* each. The owner asked to see how wide each
zone is: a dropdown "Show intervals" above the simulation table, `None` by default (the table
exactly as now), `The middle half` and `Without 10 % at each end`; the range in brackets beside
each figure, on every row; the table rewritten when the choice changes; and a clear explanation
of what the two figures are and why the ranges can overlap.

Settled with the owner on 2026-11-15:

- the three choices and their labels, the note under the table, the two guide paragraphs (shown
  side by side and accepted);
- **the dust zone's bracket is always shown**, also when the dust's figure comes from the
  darkest sky; the note then says so, with the report's own reason;
- the stricter dust rule went first (ADR 0099, merged b38d9b29), so the ranges are measured on
  clean classes.

Measured on 2026-11-15, as the image is (patch L\*, after ADR 0099):

| image | dust figure | dust zone, middle half / without 10 % | sky figure | sky, middle half / without 10 % |
| --- | --- | --- | --- | --- |
| reference FITS | 7.7 (class) | 7.7 - 12.9 / 5.8 - 14.9 | 17.6 | 15.4 - 19.0 / 14.0 - 20.0 |
| `de_printat.png` | 4.4 (sky; zone 4.2 %) | 4.9 - 7.6 / 3.1 - 8.5 | 9.1 | 7.8 - 10.8 / 6.2 - 14.5 |
| Rosetta | 6.1 (sky; zone 1.3 %) | 7.1 - 7.8 / 6.6 - 8.0 | 8.2 | 7.4 - 9.8 / 6.8 - 14.3 |
| Veil | 6.4 (sky; zone 0.8 %) | 5.8 - 7.0 / 5.5 - 7.4 | 9.3 | 8.6 - 10.1 / 7.8 - 10.9 |

Branch `claude/Show-the-dust-and-the-sky-as-ranges`, worktree
`.claude/worktrees/Show-the-dust-and-the-sky-as-ranges`, at `main` (b38d9b29) with the draft of
R-046 / R-047 uncommitted. Window only: the console, the saved report and the goldens do not
change.

## Design

**Core, numbers** (`AstroPhotoAnalyzer.Core`)

- `Thresholds.cs`: `RangeHalfCut = 0.25`, `RangeWideCut = 0.10`, each with its comment (the
  owner's choices, ADR 0100; no rule reads them).
- `Analysis/DustAnalyzer.cs`: `LstarRange(Low, High)` and `RangeKind { None, MiddleHalf, Wide }`;
  `DustStats` gains `DustHalf`, `DustWide` (null when the dust class has no patch), `SkyHalf`,
  `SkyWide`. `Measure` reads them from the two sorted sets it already builds (`ClassSets`,
  `Stats.PercentileSorted`): the dust class, and the set that gives the sky's figure (the sky
  class, or all the patches when it is empty). No new pass over the image.
- `Analysis/PrintStretchSimulator.cs`: `SimulationRow` carries the four ranges, copied where
  the row is built (the as-is row and each table row); `DustRange(kind)` / `SkyRange(kind)`
  pick one. `SimulationResult` exposes the reason the darkest sky decides as its own property,
  taken out of `DustDescription` unchanged, so the note reuses the report's words.

**Core, words** (`Report/ReportWriter.cs`)

- `SimulationHeader(RangeKind)` and `SimulationLine(row, RangeKind)`; with `None` both return
  today's text, byte for byte (the existing members stay and delegate).
- With a range, the two columns widen and nothing else moves:

  ```
  fundal-tinta         gamma   L* praf (interval)      L* cer (interval)       negru pur   ...
  0.172 asa cum e      1.000       7.7 (7.7 - 12.9)      17.6 (15.4 - 19.0)       3.7 %   ...
  ```

  The figure stays right-aligned where it is; the bracket follows it, left-aligned and padded;
  blank for a dust class without patches. English: `L* dust (interval)`, `L* sky (interval)`.
- `RangeNote(simulation, kind)`: null for `None`; otherwise the accepted note, the percentage
  from the chosen cut:
  - RO: `In paranteze: intre ce valori de L* stau dalele zonei, fara cele mai intunecate 25 % si
    fara cele mai deschise 25 % din ele. Verdictul citeste cifra dinaintea parantezei, nu
    intervalul.`
  - EN: `In brackets: the L* values the zone's patches lie between, without the darkest 25 % and
    the lightest 25 % of them. The verdict reads the figure before the brackets, not the range.`
  - added when the dust's figure comes from the darkest sky, the reason being the report's text
    (`prea putin praf: 4.2 % din cadru, sub 5 %, deci nu decide`, or the other one):
    RO `Pe imaginea asta cifra prafului vine din cerul cel mai intunecat, nu din zona de praf
    (<motivul>), asa ca poate cadea in afara parantezei.` /
    EN `On this image the dust's figure comes from the darkest sky, not from the dust zone
    (<reason>), so it can fall outside the brackets.`
  - when no patch is dust: RO `Pe imaginea asta nu s-a gasit praf, asa ca praful nu are
    interval; cifra lui vine din cerul cel mai intunecat.` / EN `On this image no dust was
    found, so the dust has no interval; its figure comes from the darkest sky.`

**Window** (`AstroPhotoAnalyzer.Wpf`)

- `ViewModels/ResultTabs.cs`, `TonesTabViewModel`: `RangeChoices` (the three labels),
  `SelectedRange` (index, 0 at start), `RangeNote`. The table is built by one private method
  that `Populate` and the setter both call, from the result the view-model already keeps; the
  preview commands are rebuilt with the rows. `Clear` leaves the choice alone, so it survives
  another image; nothing is saved between runs.
- `Views/TonesView.xaml`: above the table, the label `Arata intervalele` / `Show intervals` and
  the `ComboBox` (the pattern of `MainWindow.xaml`'s medium list), named for UI Automation
  (R-157); under the table the note, hidden while null (`NotNullToVis`).
- The guide: the sentence that defines the two figures leaves "Praful si cerul." and two
  paragraphs follow it, "L* cer si L* praf." and "Intervalele.", with the texts accepted on
  2026-11-15 (they keep the phrases `ViewModelTests` already asserts); every number from
  `Thresholds`. The guide has 8 paragraphs instead of 6.

Not changed, as agreed: the sky's line of the report ("valoarea din mijloc a dalelor de cer"),
the map's legends, the console.

## Steps

1. **Requirement.** R-046 and R-047 in `docs/requirements.md`, reworded for the bracket always
   shown and the note's reason, with the measured examples above; status `draft` until the code.
2. **Decision.** `docs/decisions/0100-...md`, accepted 2026-11-15: two ranges with cuts of 25 %
   and 10 %, window only, the whole spread not offered (one patch decides each end), the dust
   zone's bracket always shown. Rejected: no bracket when the figure comes from the sky; a
   console flag; ranges on the as-is row only.
3. **Failing tests first.** The new members exist as empty stubs so the projects compile; the
   tests fail on their assertions, output pasted.
   - `AstroPhotoAnalyzer.Tests`: `R046_TheRangesAreThePercentilesOfTheTwoPatchSets` (hand-built
     map and L\*s, known answer, both cuts), `R046_EveryRowCarriesItsRangesAtItsOwnLift`
     (band scene: the dust's figure is the low end of its middle half when the class decides;
     the lifted rows' ranges rise with the lift), `R046_WithoutARangeTheTableIsTodaysTextByteForByte`,
     `R046_ARowWritesEachRangeInBracketsAfterItsFigure`, `R046_ADustClassWithoutPatchesLeavesTheBracketBlank`,
     `R046_TheNoteSaysWhereTheDustFigureComesFrom` (both reasons, both languages),
     `R046_TheRangesChangeNoFigureMarkOrRecommendation`.
   - `AstroPhotoAnalyzer.Wpf.Tests`: `R047_TheTableIsUnchangedUntilARangeIsChosen`,
     `R047_ChoosingARangeRewritesTheTableAndKeepsThePreviewButtons`,
     `R047_TheNoteStandsUnderTheTableOnlyWhileARangeIsShown`,
     `R047_TheChoiceSurvivesAnotherImage`, `R047_TheGuideExplainsTheTwoFiguresAndTheIntervals`
     (both languages, no method word, numbers from `Thresholds`), and the dropdown in the
     rendered tab (`WindowSmokeTests` / `WindowEnglishTests`).
   - One existing assertion changes with the requirement and is reported at wrap-up:
     `ViewModelTests`, the guide's paragraph count, 6 to 8.
4. **Core**, then **window**, as designed; `scripts/verify.ps1 -SkipTests` green while editing.
5. **Renders.** `APA_DUMP_SCREENS` on the Tonuri tab with each choice, in both languages: the
   wider rows fit beside the Preview buttons, the note wraps, the dropdown sits above the table.
6. **Goldens and validation.** `APA_UPDATE_GOLDEN=1` must change no golden (any diff is a
   finding). `/validate`: no recorded figure may move; the as-is ranges are added as new keys
   to V-004, V-005, V-010 and V-013 from the run's output, with a dated line in
   `docs/validation.md`.
7. **Documents.** `docs/design/measurements.md`, `window.md`, `thresholds.md`,
   `recommendations-and-report.md`; `README.md` ("The window", Tonuri); `CHANGELOG.md`
   (Added); requirement statuses to `implemented`; this plan saved as
   `docs/plans/2026-11-15-show-the-dust-and-the-sky-as-ranges.md`.
8. **Stale figures from the last review**, documents only, each brought to what
   `docs/validation.json` records: R-142 (peaks 0.749 to 0.762), R-143 (`de_printat.png`:
   `Maxima` gain 1.29 to 1.25, sky 9.4 to 9.1), `validation.md` V-011 (gain 1.06 to 1.07,
   `Maxima` 0.920 and 0.72 % to 0.899 and 0.39 %), and a note on V-008's dust anchor kept
   inside the tolerance.
9. **Wrap-up.** Strict build (`--no-incremental -warnaserror`), the gate, the diff read back,
   small commits on the branch, a code review offered, the merge proposed.

## What may come back to the owner

- A render in which the wider table does not fit: the options would be a narrower bracket or
  the mark moved; shown with the picture.
- Any golden or recorded figure that moves (none is expected).

## Verification

- `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "FullyQualifiedName~R046|FullyQualifiedName~R047"`.
- The window by hand or by render on `starless_rbf0.5_stretch0.17.fit` and `de_printat.png`:
  `None` equals today's table; each choice shows the figures of the table above; on
  `de_printat.png` the note names the reason (`prea putin praf: 4.2 % din cadru, sub 5 %`).
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`, and the validation
  cases (`--filter "Category=Validation"`) with the drift table.

## After the review (2026-11-16)

A subagent review of the commit found no defect in the code. The test gaps and the small
mismatches it named were closed, and the owner accepted three changes of wording, so the texts
quoted above are not the final ones: in the note, the reason now follows "the darkest sky"
(`... vine din cerul cel mai intunecat (<motivul>), nu din zona de praf, asa ca ...`) and the
English says "not the interval"; in the guide's "Intervalele.", "the dust never goes over the
average sky" became "a patch lighter than the average sky is not counted as dust", which
states the rule instead of promising a result: a lifted row can move a patch by a hair. With no
patch classed sky the rule is not applied at all, an exception the guide leaves out there as
it does in the sentence of "Praful si cerul." beside it. Two limits went into
`docs/design/known-limitations.md`: a ranged row is cut at the right under a window of about
1330 px, and a bracket ending at L\* 100.0 can be wider than its place.
