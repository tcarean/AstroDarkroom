# Show the total exposure first on the Summary tab's SESSION card

Approved in plan mode on 2026-11-07, with the texts below as proposed.

## Context

On 2026-11-07 the owner asked for the total integration time to be the first value on the
Summary tab's SESSION card, in hours, minutes and seconds, in bold and a slightly larger font.
The figure exists: `Cadence.IntegrationSeconds` (R-006). Today only the interval line shows it,
and only to the minute (`total exposure 2 h 54 min`). Measured with `--inventory` on the three
real sessions: Veil 2026-10-06 10470.4 s (2 h 54 min 30 s), Veil 2026-11-06 9873.7 s
(2 h 44 min 34 s), Smida 2026-08-12 3813.7 s (1 h 3 min 34 s). The owner accepted the
recommendations: count all the frames, label it `total exposure` / `expunere totala`, show it in
the console too, and make it bold and 2 px larger in the window.

Done already on branch `claude/Show-the-total-integration-time-first` (worktree
`LightsCheck/.claude/worktrees/Show-the-total-integration-time-first`), not committed yet:
R-086 (`accepted`), ADR 0042 (`accepted 2026-11-07`; 0041 is taken by the arrows branch), and
five `R086_` tests, all failing now (3 in `LightsCheck.Tests`, 2 in `LightsCheck.Wpf.Tests`).

## Texts you read: original and proposal (plain-words rule; made only on your yes)

| Where | Now | Proposed |
| --- | --- | --- |
| Console and SESSION card, first line | (none) | `total exposure : 2 h 54 min 30 s` / `expunere totala : 2 h 54 min 30 s` |
| Same line, when no frame has a time and an exposure | (none) | `unknown (the frames carry no time or no exposure)` / `necunoscuta (cadrele nu au ora sau expunerea)` |
| README, the window's Summary bullet | `- **Summary**: the report's sections, row by row, each verdict a coloured chip.` | `- **Summary**: the report's sections, row by row, each verdict a coloured chip. The SESSION card opens with the total exposure, in bold and a little larger.` |
| README, the `--inventory` sample | starts with `lights` | first line under the title: `    total exposure     : 2 h 54 min 30 s` |
| README, the sentence after the sample | "`exposure` lists each exposure and ISO the frames use; ..." | before it: "`total exposure` is all the exposures of the night added up, to the second; `interval` gives the same total in minutes." |

## Steps

1. **Core** (`src/LightsCheck.Core/Report/`).
   - `RunReport.DurationToTheSecond(double seconds)`, beside `Duration`, the same rounding
     (`(int)Math.Round`): `2 h 54 min 30 s`, `45 min 12 s`, `38 s`. The units are the same in
     both languages, as in `Duration`.
   - `InventoryReport.TotalLabel` = `T("total exposure", "expunere totala")`, like
     `SeriesReport.ScaleLabel`. `WriteHeader` writes `$"    {TotalLabel,-19}: {Total(...)}"` right
     after the title line. The private `Total(Cadence)` returns the unknown sentence when
     `TimedFrames == 0`, else `DurationToTheSecond(IntegrationSeconds)`.
   - Every new member gets its XML doc comment.
   - Check: the three core `R086_` tests pass, and `verify.ps1 -SkipTests` is green.
2. **Window** (`src/LightsCheck.Wpf/`).
   - `RowVm` gains `bool Emphasis = false`, with a `<param>`.
   - `MainViewModel.Populate` sets `Emphasis` on the session row whose label is
     `InventoryReport.TotalLabel`. The window computes nothing (ADR 0008).
   - In `Views/SummaryView.xaml`, the `ReportRow` template: the label's style is BasedOn `Muted`
     and the value's BasedOn the theme's `TextBlock` style, each with a `DataTrigger` on
     `Emphasis` (Bold; 14 px for the label, 15 px for the value), the pattern DitherView uses for
     its bold paragraph.
   - Check: the two window `R086_` tests pass and the R-030 smoke test shows no binding error.
     A PNG of the Summary tab is rendered with `LIGHTSCHECK_DUMP_SCREENS` and sent to you.
3. **Goldens.** Regenerate them with `LIGHTSCHECK_UPDATE_GOLDEN=1 ... --filter GoldenOutputTests`.
   Expected: six files, one new line 5 each: inventory `10 min 1 s` (5 x 120.2 s), series
   `16 min 0 s` (8 x 120 s), series-classes `22 min 18 s` (10 x 120 s + 0.2 s + 137.5 s; frame 9
   unreadable), in Romanian and English. No other line may change; a diff script checks it.
4. **Docs.**
   - `docs/requirements.md`: R-086 becomes `implemented`.
   - `docs/design/session-and-console.md`: the header's first line, and `DurationToTheSecond`
     beside `Duration`.
   - `docs/design/window.md`: the card's emphasised row and the render check.
   - `README.md`: the texts in the table above.
   - `CHANGELOG.md`: a line under `[Unreleased]` / `### Added`.
   - This plan, saved as `docs/plans/2026-11-07-show-the-total-exposure-first.md`.
5. **Gate.** Run `dotnet build LightsCheck.sln -c Release --no-restore --no-incremental
   -warnaserror`, then the full `scripts/verify.ps1`.
6. **/validate.** `Report/` counts as numeric code (`docs/skills.md`), so the cases run. No
   figure should move. Three failures are expected, none caused by this task:
   - V-008 and V-011, recorded since 2026-10-27: the Veil 2026-10-06 root has no solved FITS.
   - V-014: the Veil 2026-11-06 folder changed today. It now holds 82 lights, not 85, and
     `result_9908s.fit` at the root.

   I paste the table, and V-014 goes to you as a question. I do not re-measure it into place.
7. **Close.** I offer a subagent review of `git diff main...HEAD`. I commit on the branch (the
   hook runs the gate). Before proposing the merge, I check main's latest ADR number again.

## Not in this task

- The wiki page `LightsCheck.md` and its Summary screenshot: you commit and push the wiki.
- The two `### Added` headings under `[Unreleased]` in the CHANGELOG were there before: I add
  the line to the second one and change nothing else.

## What deviated

Nothing in the code, the goldens or the texts. `/validate` ran as planned: 11 of 14 passed,
V-008 and V-011 failed as recorded, and V-014 failed on `bad` (3 recorded, 0 measured): the
owner had deleted frames 16, 58 and 64 from `lights\` that day. At the owner's word V-014 was
re-measured on the folder as it then was (82 lights, no darks, the stack's solved FITS at the
root) and recorded in a commit of its own, with its note in `docs/validation.md`.
