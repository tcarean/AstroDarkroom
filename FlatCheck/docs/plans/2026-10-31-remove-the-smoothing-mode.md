# Remove the smoothing mode (`--smooth` and the Smoothing tab)

Branch `claude/Remove-the-smoothing-mode`, worktree
`FlatCheck/.claude/worktrees/Remove-the-smoothing-mode` (the convention of the Ratio removal,
`docs/plans/2026-10-26-remove-the-ratio-mode.md`, which this plan follows step for step).

## Context

The owner asked on 2026-10-31 to delete the whole smoothing functionality from the console and
the window, to find what the removal leaves unused and to clean it up, keeping nothing that is
not needed. Nothing in the mode's numbers was wrong: ADR 0026 and ADR 0027, with their
measurements on the five real masters, stay as history.

Measured facts from the survey:

- The mode is four core files (`SmoothAnalyzer`, `SmoothScaleAdvisor`, `SmoothReport`,
  `SmoothRun`), one console branch with two flags (`--smooth`, `--scale`), one tab
  (`SmoothTabViewModel`, `SmoothScaleViewModel`, `SmoothView`, `SmoothGuideViewModel`, the
  smoothing block of `SessionMessages`), the Histogram tab's `SMOOTHED` row and its rebuild on a
  write, six test files, ten goldens (`smooth_*`, `en_smooth_*`) and five validation cases
  (V-031 to V-035).
- Helpers that lose their last caller: `Metrics.FineTexture` and `Metrics.BoxBlur`,
  `Img.TileMedians`, `Mosaic.Extract` and `Mosaic.Merge`, the four `Th.Smooth*` thresholds,
  `LogText.Smooth` and `LogText.Advice`, `HistEqReport.SmoothedVersusMaster`,
  `FlatCheckCli.AfterMapPath`, `SirilSession.SmoothedSuffix` / `SmoothedMasterPath` /
  `SmoothedPathBeside`, `HistEqKind.Smoothed`, `HistEqFileRow.IsSmoothed` and `ClosesGroup`
  (equal to `IsMaster` once the group is the master alone), `HistEqTabViewModel.FileWritten`
  (its only writer was the Smoothing tab), the default-cap parameter of
  `MapScaleViewModel`'s constructor (only the Smoothing tab passed one).
- What the mode shared and stays: `FitsWriter` and `LoadedFrame.Cards` (the synthetic flat),
  `LoadedFrame.Planes` (the synthetic refusal of a cube) and `Bitpix` (the load log line),
  `StructureAnalyzer.AnalyzePrepared`, `StructureReport.TestKeys` / `Test` / `TestLabel` (the
  against and split reports), `StructureRows.Tests` (the Structure tab), `Mosaic.Split` /
  `LooksCfa` / `PositionOf`, `Stats.Solve`, `HeavyRunGate`, `TabViewModel.RunGuardedAsync` /
  `EnterGateAsync`, `MainViewModel`'s overwrite delegate (the Synthetic tab).
- No parity golden mentions the mode: the 39 confirmed goldens do not change. The only goldens
  that change are `help.txt` and `en_help.txt` (the usage loses the mode's lines).

## Decisions and assumptions

1. The removal is complete: code, tests, goldens, validation cases, design notes, README. The
   frozen Python prototype stays untouched (ADR 0047).
2. Requirements R-060, R-061, R-062 and R-086 go to status `dropped`; R-085 (the Histogram
   list), R-117 (the folder button, now locked by the Synthetic tab alone), R-081, R-003, R-006
   and R-114 (six tab headers) are reworded to what is left.
3. New ADR `0080-the-smoothing-mode-is-removed.md`: supersedes ADR 0026 and 0027 whole, ADR
   0036 and 0077 in part (the Netezire tab and the Smoothing part of the folder lock). The old
   ADRs change only their Status line (the kit's ADR 0024). `docs/design/modes-smooth.md` and
   `docs/design/smooth-validation.md` are deleted with the decision they explain; their
   figures stay in ADR 0026 / 0027 and in git history.
4. V-031 to V-035 leave `validation.json` and `validation.md`, with a dated History line;
   `ValidationTests` loses the `smooth` kind. No figure of any other case moves, so no
   `/validate` run is needed (no formula changed).
5. The two R-110 tests that used `--smooth` as the refusal after the load are ported to the
   `--synthetic` refusal of a colour cube, which has the same shape, so the behaviour they pin
   keeps a test.
6. Removing a documented console flag is a breaking change by SemVer; the bump is the owner's.

## Steps

1. Worktree and branch; this plan.
2. Core: delete the four files and the helpers above; reword the comments that named the mode.
3. Console: `Options` (the two flags, `Mode.Smooth`), `FlatCheckCli` (the branch, the usage).
4. Window: the tab, its view-models and messages, the Histogram tab's smoothed row and
   `FileWritten`, `MainViewModel`'s wiring, `MapScaleViewModel`'s parameter, the tab counts.
5. Tests and goldens: delete the six files and ten goldens, edit the partial ones, regenerate
   `help.txt` and `en_help.txt`, read their diff.
6. Docs: requirements, validation, ADR 0080 and the Status lines, the design notes and their
   index, README, CLAUDE.md, CHANGELOG.
7. Gate (`scripts/verify.ps1`), read the diff, commit, propose the merge.
